# Readonly references

* [x] Proposed
* [x] Prototype
* [ ] Implementation: Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

The "readonly references" feature is actually a group of features that leverage the efficiency of passing variables by reference, but without exposing the data to modifications.

# Readonly ref parameters. (aka `in` parameters)

There is an existing proposal that touches this topic https://github.com/dotnet/roslyn/issues/115 as a special case of readonly parameters without going into many details. 
Here I just want to acknowledge that the idea by itself is not very new.


## Motivation

C# lacks an efficient way of expressing a desire to pass struct variables into method calls for readonly purposes with no intention of modifying. Regular by-value argument passing implies copying, which adds unnecessary costs.  That drives users to use by-ref argument passing and rely on comments/documentation to indicate that the data is not supposed to be mutated by the callee. It is not a good solution for many reasons.
The examples are numerous - vector/matrix math operators in graphics libraries like [XNA](https://msdn.microsoft.com/en-us/library/bb194944.aspx) are known to have ref operands purely because of performance considerations. There is code in Roslyn compiler itself that uses structs to avoid allocations and then passes them by reference to avoid copying costs.


## Solution

`ref readonly` parameters. 
Similarly to the `out` parameters, `ref readonly` parameters are passed as managed references with additional guarantee from the callee. The guarantee in this case is that callee will not make any assignments through the parameter nor it will create a writeable reference aliases to the data referenced by the parameter.

```C#
static Vector3 Add (ref readonly Vector3 v1, ref readonly Vector3 v2)
{
    // not OK!!
    v1 = default(Vector3);

    // not OK!!
    v1.X = 0;

    // not OK!!
    foo(ref v1.X);

    // OK
    return new Vector3(v1.X +v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
}
```

## Syntax

One of proposed syntaxes is to use existing `in` keyword as a shorter form of `ref readonly`. It could be that both syntaxes are allowed or that we pick just one of them.

While syntax is TBD, I will use `in` as it is significantly shorter than `ref readonly`.

```C# 
static Vector3 Add (in Vector3 v1, in Vector3 v2)
{
    // OK
    return new Vector3(v1.X +v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
}
```

## Use of `in` in signatures.

`in` will be allowed everywhere where `out` is allowed - methods, delegates, lambdas... 
(What about indexers, operators? - TBD) 

`in` would not be allowed in combination with `out` or with anything that `out` does not combine with.
For the purpose of OHI (Overloading, Hiding, Implementing), `in` will behave similarly to an `out` parameter. All the same rules apply, in particular it is not permitted to overload on `ref/out/in` differences.

For the purpose of binding and overload resolution `in` behaves similarly to `out` as well. `in` is basically just a new element of `RefKind` enum, in addition to current `{None, Ref, Out}`.

For the purpose of generic variance, `in` is nonvariant.

## Use of `in` at call sites.

Unlike `out` parameters, `in` does not need to be matched with an LValue(*) argument. At the call-site `in` parameters are no different from regular by-value parameters.

Example: 
Notice that there are no `in` modifiers at the call. 
Also note that passing an RValue(*) is acceptable.

```C#
static Vector3 ShiftRightTwice(in Vector3 v1)
{
    // + UnitX  twice        
    return Add(Add(v1, Vector3.UnitX), Vector3.UnitX);
}

static Vector3 Add (in Vector3 v1, in Vector3 v2)
{
    // OK
    return new Vector3(v1.X +v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
}
```

In cases of variables without a home a temporary variable will be used for a short term capturing of the value.

`in` parameters are not writeable and rules similar to readonly fields apply. - I.E. fields of a readonly ref parameter of a struct type are recursively not writeable as well.

It is permitted to use `in` parameters for passing further or recursively as `in` parameters, or for other `ref readonly` purposes as discussed further.

(*) The notion of [LValue/RValue](https://en.wikipedia.org/wiki/Value_(computer_science)#lrvalue) vary between languages.  
Here, by LValue I mean an expression that can be assigned to or passed by reference.
And RValue means an expression that yields a temporary result which does not persist on its own.  


## Passing `readonly` fields as `in` parameters.

When passing `readonly` fields (outside of a ctor) we have two choices - pass through a local copy or pass a direct reference. For the purpose of just `in` parameters the difference is semantically unobservable. However the direct reference is more efficient. We will work with CLR to allow the latter.

NOTE: adding `ref readonly` _returns_ as a feature makes copying of `ref readonlys` at the call sites observable, thus further cementing the requirement that `readonly` fields are _not_ passed by copy.

## `in` parameters and capturing of stack variables.

For the purpose of lambda/async capturing `in` will behave the same as `out` - i.e. no capturing allowed.
 
We can, alternatively, allow capturing of copied temps (by ref or by value), but that would break the aliasing behavior of `in` and would result in subtle changes in behavior based on whether variables are captured or not, which by itself is not a stable guarantee and may depend on various factors including debug vs. release. Besides, copying would defeat the purpose of the feature that is to reduce copying. As such it is advisable to not allow capturing of `in` parameters.

Example of observable aliasing/copying if capture via copying would be allowed:

```C#
static Vector3 v;

static void Main()
{
    Test(v);
}

static void Test(in Vector3 v1)
{
    v = Vector3.UnitX;
    Debug.Assert(v1 == Vector3.UnitX);

    // uncomment this to see behavior of code above change
    // Func<Vector3> f = () => v1;
}
```

## Aliasing behavior in general

Just like `out` variables, `in` variables are aliases. 
While callee is not allowed to write into them, there should not be an assumption that they cannot change (deterministically or not) between reads. As such creating a copy of an `in` parameter can be observable.

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

## Conversions at the call site.

Aliasing indirectly affects what conversions are allowed at the call-site. 
While explicit and identity conversions are ok, it is not clear whether implicit conversions are ok since they only can be allowed via implicit copying. 
For starters, let's assume implicit conversions are _not_ ok, but may consider alternatives.

Not allowing implicit conversions would at least be consistent with `out` parameters, although less intuitive since `in` is not spelled out at the call site. It would also be simpler in terms of impact on overload resolution and betterness analysis.

## Invoking instance struct methods on `in` parameters.
Since all regular instance methods of a struct can potentially mutate the instance or ref-expose `this`, an intermediate copy must be created, as already a case when receiver is a readonly field.

However, since there is no backward compatibility considerations and there are workarounds, compiler should give a warning to ensure the implicit copying is noted by the user.

## Metadata representaion.

We're using a combination of attributes and modifiers on signatures:

1) `IsReadOnlyAttribute` is added to the framework, to be used on parameters and return types, to indicate that they are `ref readonly`. For older frameworks, the compiler will generate an embedded (hidden) attribute with the same name into the module being built, and we will have a mechanism for disallowing using this attribute in source code.
2) We will use a `modreq` with the framework type `IsConst`, to prevent other compilers and older versions of the C# compiler to read such signatures. This might not be needed on some cases like parameters of non-virtual methods, as downlevel use is safe. For tools and other compilers reading these metadata, they should always rely on the attribute, as it is mandatory.

# Readonly ref returns

"Readonly ref returns" is an augmentation of ref returns that allows returning references to variables without exposing them to modification.

# Readonly ref locals

Locals are not supported for now. No plans to support them in this proposal.

## Motivation
The motivation for this sub-feature is roughly symmetrical to the reasons for the `in` parameters - avoiding copying, but on the returning side. A method or an indexer of a nontrivial struct type has currently two options -return by reference and be exposed to possible mutations or copy the value.

## Solution
`ref readonly` _returns_. (a more concise modifier like `in` so far has been elusive for this case).

```C#
struct ImmutableArray<T>
{
    private readonly T[] array;

    public ref readonly T RefAt(int i)
    {
        // returning a ref readonly 
        return ref this.r1;
    }
}
```

`readonly` on the ref return will prevent the caller from using the result for indirect writing or for obtaining writeable references.

## Syntax
 
`ref readonly` will be used to modify member signatures to indicate the return is passed as a readonly ref.

It seems unnecessary to use `readonly` in the return statement. Just `ref` would be sufficient. 
We could require the whole `return ref readonly foo`, but it seems it would only add the requirement that all the returns within a given method must agree on the `readonly` part and agree with the signature of the member. Omitting `readonly` at the return site makes `readonly` implicit on all returns in a method/lambda.
 
We should, however, require `ref` for consistency with other scenarios where something is passed via an alias vs. by value.


## Use of `ref readonly' returns in signatures

`ref readonly` will be allowed in the same places were `ref` returns are allowed. For all the OHI purposes it will behave as another RefKind on the return. I.E. `readonly` would need to match exactly when overriding, it will not be possible to overload just on `readonly` difference, etc...
 
From the point of implementation it would be essentially the same RefKind as for `in` parameters applicable to returns as well.

For the purpose of variance, readonly ref will work as non-variant.

## Returning `readonly` fields as `ref readonly`s.

Unlike the requirements of `in`, where pass-by-copy is possible, while not the most efficient, returning a `readonly` field as a `ref readonly` requires that it is a true reference.  
In particular, the requirements of the ref returns have indirect effect on requirements of `in`. As long as `in` parameters and `readonly` fields are both returnable, the copying would be: 

1. observable and 
2. would go against the goals of the feature to reduce copying.

Here is an ugly, but legal and very explicit example where copying would be observable:

```C#
class Program
{
    private readonly Vector3 v = Vector3.UnitY;

    public Program()
    {
        CompareFirstLast(
            FetchRef(), 
            v = Vector3.UnitX,    // can assign since we are in a ctor
            v,                    // making a copy here would be observable
            v = Vector3.UnitZ);   // can assign since we are in a ctor            
    }

    ref readonly Vector3 FetchRef()
    {
        // making a copy here would be observable
        return ref v;
    }

    bool CompareFirstLast(
        in Vector3 first, 
        Vector3 dummy1, 
        in Vector3 last, 
        Vector3 dummy2)
    {
        // should be the same value regardless of assignments
        // since these are refs to the same variable
        Debug.Assert(first == last);
    }
}
```

Note: Since CLR does not differentiate readonly and writeable references. The distinction may not be required for general scenarios like JIT-compilation. However, some work will be needed to introduce `ref readonly` notion within the Verification infrastructure. At least if we want passing references to `readonly` fields be formally verifiable.

## Aliasing behavior.

A care must be made to preserve the aliasing behavior and not allow capturing by-value. As a result, readonly refs should be treated the same as other refs for the purpose of capturing in lambdas, async, iterators, stack spilling etc... - I.E. most scenarios would be disallowed.

It would be ok to make a copy when `ref readonly` return is a receiver of regular struct methods, which take `this` as an ordinary writeable refs. Historically we would make an invocation on a copy. 

In theory we could produce a warning when making a copy of the receiver since there are no backward compat considerations here. The options for fixing the warning are limited though, so it is debatable whether a warning is desirable.

## Safe to Return rules.

Normal safe to return rules for references will apply to readonly references as well. 

Note that a `ref readonly` can be obtained through a regular ref, but not the other way around. Otherwise the safety of `ref readonly`s is inferred the same way as for the regular refs.

Considering that RValues can be passed as `in` parameter we need one more rule - **RValues are not safe-to-return by reference**.

We also must consider the situation of RValues passed as `in` parameters via a copy and then coming back in a form of a `ref readonly` and thus the result of the invocation is clearly unsafe to return.
Once RValues are not safe to return, the existing rule `#6` already handles this case.


Example:
```C#
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
*Specifically RValues are safe to pass as readonly ref / in parameters.*


A special note must be made about the life time of the temps used as an implementation detail of passing RValue as an `in` parameter. The temp must exist as long as any reference to it can possibly exist. 
Considering that readonly refs cannot be persisted into regular ref locals, the duration of the encompassing expression could be sufficient. (encompassing expression is the one that is not an operand/argument/receiver to another one)
For simplicity, the extent of the temps could be widened to the nearest encompassing Block/Sequence.

As a last resort the temps could be method-wide. We currently do it already in very rare cases (see comment in `EmitAssignmentValue` and testcase `IncrementPropertyOfTypeParameterReturnValue`), but I'd rather tighten the existing case since the pattern of taking a ref off an RValue by the means of spilling into a temp will now be more common.

# ref/in extension methods
There is actually existing proposal (https://github.com/dotnet/roslyn/issues/165) and corresponding PR (https://github.com/dotnet/roslyn/pull/15650). 
I just want to acknowledge that this idea is not entirely new. It is, however, relevant here since `ref readonly` elegantly removes the most contentious issue about such method - what to do with RValue receivers.

The general idea of the proposal is to allow extension methods to take the `this` parameter by reference, as long as the type is known to be a struct type (I.E. struct or a generic type with `struct` constraint).

```C#
public static void Extension(ref this Guid self)
{
    // do something
}
```

The reasons for writing such extension methods are primarily:  
1.	Avoid copying when receiver is a large struct
2.	Allow mutating extension methods on structs

The reasons why we do not want to allow this on classes  
1.	It would be of very limited purpose
2.	It would be hard to reconcile with "evaluate once" semantics of null-conditional accesses.

Example:

`obj.stringField?.RefExtension(...)`  -  need to make a copy of stringField for the null check, but then assignments to `this` inside RefExtension would not be reflected back to the field...

An ability to declare extension methods on structs that take the first argument by reference was a long-standing request. One of the blocking consideration was "what happens if receiver is not an LValue". 

An analogy with static methods would dictate that it is disallowed, but it would not be consistent with the practice of making invocation on a copy in similar situations when instance methods are involved. In addition, many of the calls for allowing ref extension methods on structs were motivated by the performance and implicit copying would diminish the benefit.

The reason why the "implicit copying" exists is because the majority of struct methods do not actually modify the struct while not being able to indicate that. Historically the most practical solution was to just make the invocation on a copy, but this practice is known for harming performance and causing bugs.

Now, assuming availability of `in` parameters, it feels reasonable to assume that ref extension methods are introduced specifically to apply mutations to `this` and thus require that the receiver is writeable. 
In addition it would also be allowed to have `in` extension methods that would not have such restriction, while naturally would not be able to mutate `this`.

```C#
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

# Readonly structs

In short - a feature that makes all members of a struct, except constructors to have `this` parameter as a `ref readonly`.

## Motivation

Compiler must assume that any method call on a struct instance may modify the instance. Indeed a writeable reference is passed to the method as `this` parameter that fully enables this behavior. To allow such invocations on `readonly` variables, the invocation are applied to temp copies. That could be unintuitive and sometimes forces people to abandon `readonly` for performance reasons. (Example: https://codeblog.jonskeet.uk/2014/07/16/micro-optimization-the-surprising-inefficiency-of-readonly-fields/ )

The problem will get worse since the implicit copying will be happening when invoking struct methods on `in` variables. - We can give warnings, but we must make defensive copies and that might force users into choosing between performance and more control over sideeffects.

## Solution
Allow `readonly` modifier on struct declarations which would result in `this` being an `in` parameter on all struct instance methods except for constructors.

```C#
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
        // not OK!!  "this" is an `in` parameter
        foo(ref this.X);

        // OK
        return $"X: {X}, Y: {Y}, Z: {Z}";
    }
}
```

The feature is surprisingly uncontroversial. The only obvious question is whether there is a need for an option to `opt out` some of the methods as mutators.

So far it feels like per-member control over `readonly` is an unnecessary complication, which also can be added later if found to be necessary.   
Current assumptions are that "Mixed" mutable/immutable structs are not common. Besides even partially mutable struct variables would generally need to be LValues and thus would not impacted by the implicit copying.


## Drawbacks
[drawbacks]: #drawbacks

I can see two major arguments against:

1) The problems that are solved here are very old. Why suddenly solve them now, especially since it would not help existing code? 

As we find C# and .Net used in new domains, some problems become more prominent.  
As examples of environments that are more critical than average about computation overheads, I can list

* cloud/datacenter scenarios where computation is billed for and responsiveness is a competitive advantage.
* Games/VR/AR with soft-realtime requirements on latencies     

This feature does not sacrifice any of the existing strengths such as type-safety, while allowing to lower overheads in some common scenarios.


2) Can we reasonably guarantee that the callee will play by the rules when it opts into `readonly` contracts?

We have similar trust when using `out`. Incorrect implementation of `out` can cause unspecified behavior, but in reality it rarely happens.  

Making the formal verification rules familiar with `ref readonly` would further mitigate the trust issue.

## Alternatives
[alternatives]: #alternatives

The main competing design is really "do nothing".

## Unresolved questions
[unresolved]: #unresolved-questions

* Actual syntax need to be vetted.
* Applicability beyond methods - indexers, operators . . .
* Diagnostics related to implicit copying of `this`
* Conversions at 'in' call sites.
* Need to agree on metadata representation.
* Changes to the Verification rules
* Spec 7.5.3.1 would have to change to reflect that ref extension methods arguments are now passed without using a 'ref' keyword.

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.


