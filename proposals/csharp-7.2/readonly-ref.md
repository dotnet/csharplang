# Readonly references

* [x] Proposed
* [x] Prototype
* [x] Implementation: Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

The "readonly references" feature is actually a group of features that leverage the efficiency of passing variables by reference, but without exposing the data to modifications:  
- `in` parameters
- `ref readonly` returns
- `readonly` structs
- `ref`/`in` extension methods
- `ref readonly` locals
- `ref` conditional expressions

## Passing arguments as readonly references.

There is an existing proposal that touches this topic https://github.com/dotnet/roslyn/issues/115 as a special case of readonly parameters without going into many details.
Here I just want to acknowledge that the idea by itself is not very new.

### Motivation

Prior to this feature C# did not have an efficient way of expressing a desire to pass struct variables into method calls for readonly purposes with no intention of modifying. Regular by-value argument passing implies copying, which adds unnecessary costs.  That drives users to use by-ref argument passing and rely on comments/documentation to indicate that the data is not supposed to be mutated by the callee. It is not a good solution for many reasons.  
The examples are numerous - vector/matrix math operators in graphics libraries like [XNA](https://msdn.microsoft.com/en-us/library/bb194944.aspx) are known to have ref operands purely because of performance considerations. There is code in Roslyn compiler itself that uses structs to avoid allocations and then passes them by reference to avoid copying costs.

### Solution (`in` parameters)

Similarly to the `out` parameters, `in` parameters are passed as managed references with additional guarantees from the callee.  
Unlike `out` parameters which _must_ be assigned by the callee before any other use, `in` parameters cannot be assigned by the callee at all.

As a result `in` parameters allow for effectiveness of indirect argument passing without exposing arguments to mutations by the callee.

### Declaring `in` parameters

`in` parameters are declared by using `in` keyword as a modifier in the parameter signature.

For all purposes the `in` parameter is treated as a `readonly` variable. Most of the restrictions on the use of `in` parameters inside the method are the same as with `readonly` fields.

> Indeed an `in` parameter may represent a `readonly` field. Similarity of restrictions is not a coincidence.

For example fields of an `in` parameter which has a struct type are all recursively classified as `readonly` variables .

```csharp
static Vector3 Add (in Vector3 v1, in Vector3 v2)
{
    // not OK!!
    v1 = default(Vector3);

    // not OK!!
    v1.X = 0;

    // not OK!!
    foo(ref v1.X);

    // OK
    return new Vector3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
}
```

- `in` parameters are allowed anywhere where ordinary byval parameters are allowed. This includes indexers, operators (including conversions), delegates, lambdas, local functions.

> ```csharp
>  (in int x) => x                                                     // lambda expression  
>  TValue this[in TKey index];                                         // indexer
>  public static Vector3 operator +(in Vector3 x, in Vector3 y) => ... // operator
>  ```

- `in` is not allowed in combination with `out` or with anything that `out` does not combine with.

- It is not permitted to overload on `ref`/`out`/`in` differences.

- It is permitted to overload on ordinary byval and `in` differences.

- For the purpose of OHI (Overloading, Hiding, Implementing), `in` behaves similarly to an `out` parameter.
All the same rules apply.
For example the overriding method will have to match `in` parameters with `in` parameters of an identity-convertible type.

- For the purpose of delegate/lambda/method group conversions, `in` behaves similarly to an `out` parameter.
Lambdas and applicable method group conversion candidates will have to match `in` parameters of the target delegate with `in` parameters of an identity-convertible type.

- For the purpose of generic variance, `in` parameters are nonvariant.

> NOTE: There are no warnings on `in` parameters that have reference or primitives types.
It may be pointless in general, but in some cases user must/want to pass primitives as `in`. Examples - overriding a generic method like `Method(in T param)` when `T` was substituted to be `int`, or when having methods like `Volatile.Read(in int location)`
>
> It is conceivable to have an analyzer that warns in cases of inefficient use of `in` parameters, but the rules for such analysis would be too fuzzy to be a part of a language specification.

### Use of `in` at call sites. (`in` arguments)

There are two ways to pass arguments to `in` parameters.

#### `in` arguments can match `in` parameters:

An argument with an `in` modifier at the call site can match `in` parameters.

```csharp
int x = 1;

void M1<T>(in T x)
{
  // . . .
}

var x = M1(in x);  // in argument to a method

class D
{
    public string this[in Guid index];
}

D dictionary = . . . ;
var y = dictionary[in Guid.Empty]; // in argument to an indexer
```

- `in` argument must be a _readable_ LValue(*).
Example: `M1(in 42)` is invalid

> (*) The notion of [LValue/RValue](https://en.wikipedia.org/wiki/Value_(computer_science)#lrvalue) vary between languages.  
Here, by LValue I mean an expression that represent a location that can be referred to directly.
And RValue means an expression that yields a temporary result which does not persist on its own.  

- In particular it is valid to pass `readonly` fields, `in` parameters or other formally `readonly` variables as `in` arguments.
Example: `dictionary[in Guid.Empty]` is legal. `Guid.Empty` is a static readonly field.

- `in` argument must have type _identity-convertible_ to the type of the parameter.
Example: `M1<object>(in Guid.Empty)` is invalid. `Guid.Empty` is not _identity-convertible_ to `object`

The motivation for the above rules is that `in` arguments guarantee _aliasing_ of the argument variable. The callee always receives a direct reference to the same location as represented by the argument.

- in rare situations when `in` arguments must be stack-spilled due to `await` expressions used as operands of the same call, the behavior is the same as with `out` and `ref` arguments - if the variable cannot be spilled in referentially-transparent manner, an error is reported.

Examples:
1. `M1(in staticField, await SomethingAsync())`  is valid.
`staticField` is a static field which can be accessed more than once without observable side effects. Therefore both the order of side effects and aliasing requirements can be provided.
2. `M1(in RefReturningMethod(), await SomethingAsync())`  will produce an error.
`RefReturningMethod()` is a `ref` returning method. A method call may have observable side effects, therefore it must be evaluated before the `SomethingAsync()` operand. However the result of the invocation is a reference that cannot be preserved across the `await` suspension point which make the direct reference requirement impossible.

> NOTE: the stack spilling errors are considered to be implementation-specific limitations. Therefore they do not have effect on overload resolution or lambda inference.

#### Ordinary byval arguments can match `in` parameters:

Regular arguments without modifiers can match `in` parameters. In such case the arguments have the same relaxed constraints as an ordinary byval arguments would have.

The motivation for this scenario is that `in` parameters in APIs may result in inconveniences for the user when arguments cannot be passed as a direct reference - ex: literals, computed or `await`-ed results or arguments that happen to have more specific types.  
All these cases have a trivial solution of storing the argument value in a temporary local of appropriate type and passing that local as an `in` argument.  
To reduce the need for such boilerplate code compiler can perform the same transformation, if needed, when `in` modifier is not present at the call site.  

In addition, in some cases, such as invocation of operators, or `in` extension methods, there is no syntactical way to specify `in` at all. That alone requires specifying the behavior of ordinary byval arguments when they match `in` parameters.

In particular:

- it is valid to pass RValues.
A reference to a temporary is passed in such case.
Example:
```csharp
Print("hello");      // not an error.

void Print<T>(in T x)
{
  //. . .
}
```

- implicit conversions are allowed.

> This is actually a special case of passing an RValue  

A reference to a temporary holding converted value is passed in such case.
Example:
```csharp
Print<int>(Short.MaxValue)     // not an error.
```

- in a case of a receiver of an `in` extension method (as opposed to `ref` extension methods), RValues or implicit _this-argument-conversions_ are allowed.
A reference to a temporary holding converted value is passed in such case.
Example:
```csharp
public static IEnumerable<T> Concat<T>(in this (IEnumerable<T>, IEnumerable<T>) arg)  => . . .;

("aa", "bb").Concat<char>()    // not an error.
```
More information on `ref`/`in` extension methods is provided further in this document.

- argument spilling due to `await` operands could spill "by-value", if necessary.
In scenarios where providing a direct reference to the argument is not possible due to intervening `await` a copy of the argument's value is spilled instead.  
Example:
```csharp
M1(RefReturningMethod(), await SomethingAsync())   // not an error.
```
Since the result of a side-effecting invocation is a reference that cannot be preserved across `await` suspension, a temporary containing the actual value will be preserved instead (as it would in an ordinary byval parameter case).

#### Omitted optional arguments

It is permitted for an `in` parameter to specify a default value. That makes the corresponding argument optional.

Omitting optional argument at the call site results in passing the default value via a temporary.

```csharp
Print("hello");      // not an error, same as
Print("hello", c: Color.Black);

void Print(string s, in Color c = Color.Black)
{
    // . . .
}
```

### Aliasing behavior in general

Just like `ref` and `out` variables, `in` variables are references/aliases to existing locations.

While callee is not allowed to write into them, reading an `in` parameter can observe different values as a side effect of other evaluations.

Example:

```C#
static Vector3 v = Vector3.UnitY;

static void Main()
{
    Test(v);
}

static void Test(in Vector3 v1)
{
    Debug.Assert(v1 == Vector3.UnitY);
    // changes v1 deterministically (no races required)
    ChangeV();
    Debug.Assert(v1 == Vector3.UnitX);
}

static void ChangeV()
{
    v = Vector3.UnitX;
}
```

### `in` parameters and capturing of local variables.  
For the purpose of lambda/async capturing `in` parameters behave the same as `out` and `ref` parameters.

- `in` parameters cannot be captured in a closure
- `in` parameters are not allowed in iterator methods
- `in` parameters are not allowed in async methods

### Temporary variables.  
Some uses of `in` parameter passing may require indirect use of a temporary local variable:  
- `in` arguments are always passed as direct aliases when call-site uses `in`. Temporary is never used in such case.
- `in` arguments are not required to be direct aliases when call-site does not use `in`. When argument is not an LValue, a temporary may be used.
- `in` parameter may have default value. When corresponding argument is omitted at the call site, the default value are passed via a temporary.
- `in` arguments may have implicit conversions, including those that do not preserve identity. A temporary is used in those cases.
- receivers of ordinary struct calls may not be writeable LValues (**existing case!**). A temporary is used in those cases.

The life time of the argument temporaries matches the closest encompassing scope of the call-site.

The formal life time of temporary variables is semantically significant in scenarios involving escape analysis of variables returned by reference.

### Metadata representation of `in` parameters.
When `System.Runtime.CompilerServices.IsReadOnlyAttribute` is applied to a byref parameter, it means that the the parameter is an `in` parameter.

In addition, if the method is *abstract* or *virtual*, then the signature of such parameters (and only such parameters) must have `modreq[System.Runtime.InteropServices.InAttribute]`.

**Motivation**: this is done to ensure that in a case of method overriding/implementing the `in` parameters match.

Same requirements apply to `Invoke` methods in delegates.

**Motivation**: this is to ensure that existing compilers cannot simply ignore `readonly` when creating or assigning delegates.

## Returning by readonly reference.

### Motivation
The motivation for this sub-feature is roughly symmetrical to the reasons for the `in` parameters - avoiding copying, but on the returning side. Prior to this feature, a method or an indexer had two options: 1) return by reference and be exposed to possible mutations or 2) return by value which results in copying.

### Solution (`ref readonly` returns)  
The feature allows a member to return variables by reference without exposing them to mutations.

### Declaring `ref readonly` returning members

A combination of modifiers `ref readonly` on the return signature is used to to indicate that the member returns a readonly reference.

For all purposes a `ref readonly` member is treated as a `readonly` variable - similar to `readonly` fields and `in` parameters.

For example fields of `ref readonly` member which has a struct type are all recursively classified as `readonly` variables. - It is permitted to pass them as `in` arguments, but not as `ref` or `out` arguments.

```csharp
ref readonly Guid Method1()
{
}

Method2(in Method1()); // valid. Can pass as `in` argument.

Method3(ref Method1()); // not valid. Cannot pass as `ref` argument
```

- `ref readonly` returns are allowed in the same places were `ref` returns are allowed.
This includes indexers, delegates, lambdas, local functions.

- It is not permitted to overload on `ref`/`ref readonly` /  differences.

- It is permitted to overload on ordinary byval and `ref readonly` return differences.

- For the purpose of OHI (Overloading, Hiding, Implementing), `ref readonly` is similar but distinct from `ref`.
For example the a method that overrides `ref readonly` one, must itself be `ref readonly` and have identity-convertible type.

- For the purpose of delegate/lambda/method group conversions, `ref readonly` is similar but distinct from `ref`.
Lambdas and applicable method group conversion candidates have to match `ref readonly` return of the target delegate with `ref readonly` return of the type that is identity-convertible.

- For the purpose of generic variance, `ref readonly` returns are nonvariant.

> NOTE: There are no warnings on `ref readonly` returns that have reference or primitives types.
It may be pointless in general, but in some cases user must/want to pass primitives as `in`. Examples - overriding a generic method like `ref readonly T Method()` when `T` was substituted to be `int`.
>
>It is conceivable to have an analyzer that warns in cases of inefficient use of `ref readonly` returns, but the rules for such analysis would be too fuzzy to be a part of a language specification.

### Returning from `ref readonly` members
Inside the method body the syntax is the same as with regular ref returns. The `readonly` will be inferred from the containing method.

The motivation is that `return ref readonly <expression>` is unnecessary long and only allows for mismatches on the `readonly` part that would always result in errors.
The `ref` is, however, required for consistency with other scenarios where something is passed via strict aliasing vs. by value.

> Unlike the case with `in` parameters, `ref readonly` returns never return via a local copy. Considering that the copy would cease to exist immediately upon returning such practice would be pointless and dangerous. Therefore `ref readonly` returns are always direct references.

Example:

```csharp
struct ImmutableArray<T>
{
    private readonly T[] array;

    public ref readonly T ItemRef(int i)
    {
        // returning a readonly reference to an array element
        return ref this.r1;
    }
}

```

- An argument of `return ref` must be an LValue (**existing rule**)
- An argument of `return ref` must be "safe to return" (**existing rule**)
- In a `ref readonly` member an argument of `return ref` is _not required to be writeable_ .
For example such member can ref-return a readonly field or one of its `in` parameters.

### Safe to Return rules.
Normal safe to return rules for references will apply to readonly references as well.

Note that a `ref readonly` can be obtained from a regular `ref` local/parameter/return, but not the other way around. Otherwise the safety of `ref readonly` returns is inferred the same way as for regular `ref` returns.

Considering that RValues can be passed as `in` parameter and returned as `ref readonly` we need one more rule - **RValues are not safe-to-return by reference**.

> Consider the situation when an RValue is passed to an `in` parameter via a copy and then returned back in a form of a `ref readonly`. In the context of the caller the result of such invocation is a reference to local data and as such is unsafe to return.
> Once RValues are not safe to return, the existing rule `#6` already handles this case.

Example:
```csharp
ref readonly Vector3 Test1()
{
    // can pass an RValue as "in" (via a temp copy)
    // but the result is not safe to return
    // because the RValue argument was not safe to return by reference
    return ref Test2(default(Vector3));
}

ref readonly Vector3 Test2(in Vector3 r)
{
    // this is ok, r is returnable
    return ref r;
}
```

Updated `safe to return` rules:

1.	**refs to variables on the heap are safe to return**
2.	**ref/in parameters are safe to return**
`in` parameters naturally can only be returned as readonly.
3.	**out parameters are safe to return** (but must be definitely assigned, as is already the case today)
4.	**instance struct fields are safe to return as long as the receiver is safe to return**
5.	**'this' is not safe to return from struct members**
6.	**a ref, returned from another method is safe to return if all refs/outs passed to that method as formal parameters were safe to return.**
*Specifically it is irrelevant if receiver is safe to return, regardless whether receiver is a struct, class or typed as a generic type parameter.*
7.	**RValues are not safe to return by reference.**
*Specifically RValues are safe to pass as in parameters.*

> NOTE: There are additional rules regarding safety of returns that come into play when ref-like types and ref-reassignments are involved.
> The rules equally apply to `ref` and `ref readonly` members and therefore are not mentioned here.

### Aliasing behavior.
`ref readonly` members provide the same aliasing behavior as ordinary `ref` members (except for being readonly).
Therefore for the purpose of capturing in lambdas, async, iterators, stack spilling etc... the same restrictions apply. - I.E. due to inability to capture the actual references and due to side-effecting nature of member evaluation such scenarios are disallowed.

> It is permitted and required to make a copy when `ref readonly` return is a receiver of regular struct methods, which take `this` as an ordinary writeable reference. Historically in all cases where such invocations are applied to readonly variable a local copy is made.

### Metadata representation.
When `System.Runtime.CompilerServices.IsReadOnlyAttribute` is applied to the return of a byref returning method, it means that the method returns a readonly reference.

In addition, the result signature of such methods (and only those methods) must have `modreq[System.Runtime.CompilerServices.IsReadOnlyAttribute]`.

**Motivation**: this is to ensure that existing compilers cannot simply ignore `readonly` when invoking methods with `ref readonly` returns

## Readonly structs
In short - a feature that makes `this` parameter of all instance members of a struct, except for constructors, an `in` parameter.

### Motivation
Compiler must assume that any method call on a struct instance may modify the instance. Indeed a writeable reference is passed to the method as `this` parameter and fully enables this behavior. To allow such invocations on `readonly` variables, the invocations are applied to temp copies. That could be unintuitive and sometimes forces people to abandon `readonly` for performance reasons.  
Example: https://codeblog.jonskeet.uk/2014/07/16/micro-optimization-the-surprising-inefficiency-of-readonly-fields/

After adding support for `in` parameters and `ref readonly` returns the problem of defensive copying will get worse since readonly variables will become more common.

### Solution
Allow `readonly` modifier on struct declarations which would result in `this` being treated as `in` parameter on all struct instance methods except for constructors.

```csharp
static void Test(in Vector3 v1)
{
    // no need to make a copy of v1 since Vector3 is a readonly struct
    System.Console.WriteLine(v1.ToString());
}

readonly struct Vector3
{
    . . .

    public override string ToString()
    {
        // not OK!!  `this` is an `in` parameter
        foo(ref this.X);

        // OK
        return $"X: {X}, Y: {Y}, Z: {Z}";
    }
}
```

### Restrictions on members of readonly struct
- Instance fields of a readonly struct must be readonly.  
**Motivation:** can only be written to externally, but not through members.
- Instance autoproperties of a readonly struct must be get-only.  
**Motivation:** consequence of restriction on instance fields.
- Readonly struct may not declare field-like events.  
**Motivation:** consequence of restriction on instance fields.

### Metadata representation.
When `System.Runtime.CompilerServices.IsReadOnlyAttribute` is applied to a value type, it means that the the type is a `readonly struct`.

In particular:
-  The identity of the `IsReadOnlyAttribute` type is unimportant. In fact it can be embedded by the compiler in the containing assembly if needed.

## `ref`/`in` extension methods
There is actually an existing proposal (https://github.com/dotnet/roslyn/issues/165) and corresponding prototype PR (https://github.com/dotnet/roslyn/pull/15650).
I just want to acknowledge that this idea is not entirely new. It is, however, relevant here since `ref readonly` elegantly removes the most contentious issue about such methods - what to do with RValue receivers.

The general idea is allowing extension methods to take the `this` parameter by reference, as long as the type is known to be a struct type.

```csharp
public static void Extension(ref this Guid self)
{
    // do something
}
```

The reasons for writing such extension methods are primarily:  
1.	Avoid copying when receiver is a large struct
2.	Allow mutating extension methods on structs

The reasons why we do not want to allow this on classes  
1.	It would be of very limited purpose.
2.	It would break long standing invariant that a method call cannot turn non-`null` receiver to become `null` after invocation.
> In fact, currently a non-`null` variable cannot become `null` unless _explicitly_ assigned or passed by `ref` or `out`.
> That greatly aids readability or other forms of "can this be a null here" analysis.
3.	It would be hard to reconcile with "evaluate once" semantics of null-conditional accesses.
Example:
`obj.stringField?.RefExtension(...)` - need to capture a copy of `stringField` to make the null check meaningful, but then assignments to `this` inside RefExtension would not be reflected back to the field.

An ability to declare extension methods on **structs** that take the first argument by reference was a long-standing request. One of the blocking consideration was "what happens if receiver is not an LValue?".

- There is a precedent that any extension method could also be called as a static method (sometimes it is the only way to resolve ambiguity). It would dictate that RValue receivers should be disallowed.
- On the other hand there is a practice of making invocation on a copy in similar situations when struct instance methods are involved.

The reason why the "implicit copying" exists is because the majority of struct methods do not actually modify the struct while not being able to indicate that. Therefore the most practical solution was to just make the invocation on a copy, but this practice is known for harming performance and causing bugs.

Now, with availability of `in` parameters, it is possible for an extension to signal the intent. Therefore the conundrum can be resolved by requiring `ref` extensions to be called with writeable receivers while `in` extensions permit implicit copying if necessary.

```csharp
// this can be called on either RValue or an LValue
public static void Reader(in this Guid self)
{
    // do something nonmutating.
    WriteLine(self == default(Guid));
}

// this can be called only on an LValue
public static void Mutator(ref this Guid self)
{
    // can mutate self
    self = new Guid();
}
```

### `in` extensions and generics.
The purpose of `ref` extension methods is to mutate the receiver directly or by invoking mutating members. Therefore `ref this T` extensions are allowed as long as `T` is constrained to be a struct.

On the other hand `in` extension methods exist specifically to reduce implicit copying. However any use of an `in T` parameter will have to be done through an interface member. Since all interface members are considered mutating, any such use would require a copy. - Instead of reducing copying, the effect would be the opposite. Therefore `in this T` is not allowed when `T` is a generic type parameter regardless of constraints.

### Valid kinds of extension methods (recap):
The following forms of `this` declaration in an extension method are now allowed:
1) `this T arg` - regular byval extension. (**existing case**)
- T can be any type, including reference types or type parameters.
Instance will be the same variable after the call.
Allows implicit conversions of _this-argument-conversion_ kind.
Can be called on RValues.

- `in this T self` - `in` extension.
T must be an actual struct type.
Instance will be the same variable after the call.
Allows implicit conversions of _this-argument-conversion_ kind.
Can be called on RValues (may be invoked on a temp if needed).

- `ref this T self` - `ref` extension.
T must be a struct type or a generic type parameter constrained to be a struct.
Instance may be written to by the invocation.
Allows only identity conversions.
Must be called on writeable LValue. (never invoked via a temp).

## Readonly ref locals.

### Motivation.
Once `ref readonly` members were introduced, it was clear from the use that they need to be paired with appropriate kind of local. Evaluation of a member may produce or observe side effects, therefore if the result must be used more than once, it needs to be stored. Ordinary `ref` locals do not help here since they cannot be assigned a `readonly` reference.   

### Solution.
Allow declaring `ref readonly` locals. This is a new kind of `ref` locals that is not writeable. As a result `ref readonly` locals can accept references to readonly variables without exposing these variables to writes.

### Declaring and using `ref readonly` locals.

The syntax of such locals uses `ref readonly` modifiers at declaration site (in that specific order). Similarly to ordinary `ref` locals, `ref readonly` locals must be ref-initialized at declaration. Unlike regular `ref` locals, `ref readonly` locals can refer to `readonly` LValues like `in` parameters, `readonly` fields, `ref readonly` methods.

For all purposes a `ref readonly` local is treated as a `readonly` variable. Most of the restrictions on the use are the same as with `readonly` fields or `in` parameters.

For example fields of an `in` parameter which has a struct type are all recursively classified as `readonly` variables .   

```csharp
static readonly ref Vector3 M1() => . . .

static readonly ref Vector3 M1_Trace()
{
    // OK
    ref readonly var r1 = ref M1();

    // Not valid. Need an LValue
    ref readonly Vector3 r2 = ref default(Vector3);

    // Not valid. r1 is readonly.
    Mutate(ref r1);

    // OK.
    Print(in r1);

    // OK.
    return ref r1;
}
```

### Restrictions on use of `ref readonly` locals
Except for their `readonly` nature, `ref readonly` locals behave like ordinary `ref` locals and are subject to exactly same restrictions.  
For example restrictions related to capturing in closures, declaring in `async` methods or the `safe-to-return` analysis equally applies to `ref readonly` locals.

## Ternary `ref` expressions. (aka "Conditional LValues")

### Motivation
Use of `ref` and `ref readonly` locals exposed a need to ref-initialize such locals with one or another target variable based on a condition.

A typical workaround is to introduce a method like:

```csharp
ref T Choice(bool condition, ref T consequence, ref T alternative)
{
    if (condition)
    {
         return ref consequence;
    }
    else
    {
         return ref alternative;
    }
}
```

Note that `Choice` is not an exact replacement of a ternary since _all_ arguments must be evaluated at the call site, which was leading to unintuitive behavior and bugs.

The following will not work as expected:

```csharp
    // will crash with NRE because 'arr[0]' will be executed unconditionally
    ref var r = ref Choice(arr != null, ref arr[0], ref otherArr[0]);
```

### Solution
Allow special kind of conditional expression that evaluates to a reference to one of LValue argument based on a condition.

### Using `ref` ternary expression.

The syntax for the `ref` flavor of a conditional expression is ` <condition> ? ref <consequence> : ref <alternative>;`

Just like with the ordinary conditional expression only `<consequence>` or `<alternative>` is evaluated depending on result of the boolean condition expression.

Unlike ordinary conditional expression, `ref` conditional expression:
- requires that `<consequence>` and `<alternative>` are LValues.
- `ref` conditional expression itself is an LValue and
- `ref` conditional expression is writeable if both `<consequence>` and `<alternative>` are writeable LValues

Examples:  
`ref` ternary is an LValue and as such it can be passed/assigned/returned by reference;
```csharp
     // pass by reference
     foo(ref (arr != null ? ref arr[0]: ref otherArr[0]));

     // return by reference
     return ref (arr != null ? ref arr[0]: ref otherArr[0]);
```

Being an LValue, it can also be assigned to.
```csharp
     // assign to
     (arr != null ? ref arr[0]: ref otherArr[0]) = 1;

     // error. readOnlyField is readonly and thus conditional expression is readonly
     (arr != null ? ref arr[0]: ref obj.readOnlyField) = 1;
```

Can be used as a receiver of a method call and skip copying if necessary.
```csharp
     // no copies
     (arr != null ? ref arr[0]: ref otherArr[0]).StructMethod();

     // invoked on a copy.
     // The receiver is `readonly` because readOnlyField is readonly.
     (arr != null ? ref arr[0]: ref obj.readOnlyField).StructMethod();

     // no copies. `ReadonlyStructMethod` is a method on a `readonly` struct
     // and can be invoked directly on a readonly receiver
     (arr != null ? ref arr[0]: ref obj.readOnlyField).ReadonlyStructMethod();
```

`ref` ternary can be used in a regular (not ref) context as well.
```csharp
     // only an example
     // a regular ternary could work here just the same
     int x = (arr != null ? ref arr[0]: ref otherArr[0]);
```

### Drawbacks
[drawbacks]: #drawbacks

I can see two major arguments against enhanced support for references and readonly references:

1) The problems that are solved here are very old. Why suddenly solve them now, especially since it would not help existing code?

As we find C# and .Net used in new domains, some problems become more prominent.  
As examples of environments that are more critical than average about computation overheads, I can list

* cloud/datacenter scenarios where computation is billed for and responsiveness is a competitive advantage.
* Games/VR/AR with soft-realtime requirements on latencies     

This feature does not sacrifice any of the existing strengths such as type-safety, while allowing to lower overheads in some common scenarios.

2) Can we reasonably guarantee that the callee will play by the rules when it opts into `readonly` contracts?

We have similar trust when using `out`. Incorrect implementation of `out` can cause unspecified behavior, but in reality it rarely happens.  

Making the formal verification rules familiar with `ref readonly` would further mitigate the trust issue.

### Alternatives
[alternatives]: #alternatives

The main competing design is really "do nothing".

### Unresolved questions
[unresolved]: #unresolved-questions

### Design meetings

https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-02-22.md
https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-03-01.md
https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-08-28.md
https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-09-25.md
https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-09-27.md
