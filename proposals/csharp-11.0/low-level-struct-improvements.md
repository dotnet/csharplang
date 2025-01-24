# Low Level Struct Improvements

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Champion issues: <https://github.com/dotnet/csharplang/issues/1147>, <https://github.com/dotnet/csharplang/issues/6476>

## Summary
This proposal is an aggregation of several different proposals for `struct` performance improvements: `ref` fields and the ability to override lifetime defaults. The goal being a design which takes into account the various proposals to create a single overarching feature set for low level `struct` improvements.

> Note: Previous versions of this spec used the terms "ref-safe-to-escape" and "safe-to-escape", which were introduced in the [Span safety](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md) feature specification. The [ECMA standard committee](https://www.ecma-international.org/task-groups/tc49-tg2/) changed the names to ["ref-safe-context"](https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/variables#972-ref-safe-contexts) and ["safe-context"](https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/structs#16412-safe-context-constraint), respectively. The values of the safe context have been refined to use "declaration-block", "function-member", and "caller-context" consistently. The speclets had used different phrasing for these terms, and also used "safe-to-return" as a synonym for "caller-context". This speclet has been updated to use the terms in the C# 7.3 standard.

Not all the features outlined in this document have been implemented in C# 11. C# 11 includes:

1. `ref` fields and `scoped`
1. `[UnscopedRef]`

These features remain open proposals for a future version of C#:

1. `ref` fields to `ref struct`
1. Sunset restricted types

## Motivation
Earlier versions of C# added a number of low level performance features to the language: `ref` returns, `ref struct`, function pointers, etc. ... These enabled .NET developers to write highly performant code while continuing to leverage the C# language rules for type and memory safety.  It also allowed the creation of fundamental performance types in the .NET libraries like `Span<T>`.

As these features have gained traction in the .NET ecosystem developers, both internal and external, have been providing us with information on remaining friction points in the ecosystem. Places where they still need to drop to `unsafe` code to get their work done, or require the runtime to special case types like `Span<T>`. 

Today `Span<T>` is accomplished by using the `internal` type `ByReference<T>` which the runtime effectively treats as a `ref` field. This provides the benefit of `ref` fields but with the downside that the language provides no safety verification for it, as it does for other uses of `ref`. Further only dotnet/runtime can use this type as it's `internal`, so 3rd parties can not design their own primitives based on `ref` fields. Part of the [motivation for this work](https://github.com/dotnet/runtime/issues/32060) is to remove `ByReference<T>` and use proper `ref` fields in all code bases. 

This proposal plans to address these issues by building on top of our existing low level features. Specifically it aims to:

- Allow `ref struct` types to declare `ref` fields.
- Allow the runtime to fully define `Span<T>` using the C# type system and remove special case type like `ByReference<T>`
- Allow `struct` types to return `ref` to their fields.
- Allow runtime to remove `unsafe` uses caused by limitations of lifetime defaults
- Allow the declaration of safe `fixed` buffers for managed and unmanaged types in `struct`

## Detailed Design 
The rules for `ref struct` safety are defined in the [span safety document](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md) using the previous terms. Those rules have been incorporated into the C# 7 standard in [§9.7.2](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/variables.md#972-ref-safe-contexts) and [§16.4.12](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/structs.md#16412-safe-context-constraint). This document will describe the required changes to this document as a result of this proposal. Once accepted as an approved feature these changes will be incorporated into that document.

Once this design is complete our `Span<T>` definition will be as follows:

<a name="new-span"></a>

```c#
readonly ref struct Span<T>
{
    readonly ref T _field;
    readonly int _length;

    // This constructor does not exist today but will be added as a part 
    // of changing Span<T> to have ref fields. It is a convenient, and
    // safe, way to create a length one span over a stack value that today 
    // requires unsafe code.
    public Span(ref T value)
    {
        _field = ref value;
        _length = 1;
    }
}
```

### Provide ref fields and scoped
The language will allow developers to declare `ref` fields inside of a `ref struct`. This can be useful for example when encapsulating large mutable `struct` instances or defining high performance types like `Span<T>` in libraries besides the runtime.

``` C#
ref struct S 
{
    public ref int Value;
}
```

A `ref` field will be emitted into metadata using the `ELEMENT_TYPE_BYREF` signature. This is no different than how we emit `ref` locals or `ref` arguments. For example `ref int _field` will be emitted as `ELEMENT_TYPE_BYREF ELEMENT_TYPE_I4`. This will require us to update ECMA335 to allow this entry but this should be rather straight forward.

Developers can continue to initialize a `ref struct` with a `ref` field using the `default` expression in which case all declared `ref` fields will have the value `null`. Any attempt to use such fields will result in a `NullReferenceException` being thrown.

```c#
ref struct S 
{
    public ref int Value;
}

S local = default;
local.Value.ToString(); // throws NullReferenceException
```

While the C# language pretends that a `ref` cannot be `null` this is legal at the runtime level and has well defined semantics. Developers who introduce `ref` fields into their types need to be aware of this possibility and should be **strongly** discouraged from leaking this detail into consuming code. Instead `ref` fields should be validated as non-null using the [runtime helpers](https://github.com/dotnet/runtime/pull/40008) and throwing when an uninitialized `struct` is used incorrectly.

```c#
ref struct S1 
{
    private ref int Value;

    public int GetValue()
    {
        if (System.Runtime.CompilerServices.Unsafe.IsNullRef(ref Value))
        {
            throw new InvalidOperationException(...);
        }

        return Value;
    }
}
```

A `ref` field can be combined with `readonly` modifiers in the following ways:

- `readonly ref`: this is a field that cannot be ref reassigned outside a constructor or `init` methods. It can be value assigned though outside those contexts
- `ref readonly`: this is a field that can be ref reassigned but cannot be value assigned at any point. This how an `in` parameter could be ref reassigned to a `ref` field.
- `readonly ref readonly`: a combination of `ref readonly` and `readonly ref`. 

```c#
ref struct ReadOnlyExample
{
    ref readonly int Field1;
    readonly ref int Field2;
    readonly ref readonly int Field3;

    void Uses(int[] array)
    {
        Field1 = ref array[0];  // Okay
        Field1 = array[0];      // Error: can't assign ref readonly value (value is readonly)
        Field2 = ref array[0];  // Error: can't repoint readonly ref
        Field2 = array[0];      // Okay
        Field3 = ref array[0];  // Error: can't repoint readonly ref
        Field3 = array[0];      // Error: can't assign ref readonly value (value is readonly)
    }
}
```

A `readonly ref struct` will require that `ref` fields are declared `readonly ref`. There is no requirement that they are declared `readonly ref readonly`. This does allow a `readonly struct` to have indirect mutations via such a field but that is no different than a `readonly` field that pointed to a reference type today ([more details](#reason-readonly-shallow))

A `readonly ref` will be emitted to metadata using the `initonly` flag, same as any other field. A `ref readonly` field will be attributed with `System.Runtime.CompilerServices.IsReadOnlyAttribute`. A `readonly ref readonly` will be emitted with both items.

This feature requires runtime support and changes to the ECMA spec. As such these will only be enabled when the corresponding feature flag is set in corelib. The issue tracking the exact API is tracked here https://github.com/dotnet/runtime/issues/64165

The set of changes to our safe context rules necessary to allow `ref` fields is small and targeted. The rules already account for `ref` fields existing and being consumed from APIs. The changes need to focus on only two aspects: how they are created and how they are ref reassigned. 

First the rules establishing *ref-safe-context* values for fields need to be updated for `ref` fields as follows:

<a name="rules-field-lifetimes"></a>

> An expression in the form `ref e.F` *ref-safe-context* as follows:
> 1. If `F` is a `ref` field its *ref-safe-context* is the *safe-context* of `e`.
> 2. Else if `e` is of a reference type, it has *ref-safe-context* of *caller-context*
> 3. Else its *ref-safe-context* is taken from the *ref-safe-context* of `e`.

This does not represent a rule change though as the rules have always accounted for `ref` state to exist inside a `ref struct`. This is in fact how the `ref` state in `Span<T>` has always worked and the consumption rules correctly account for this. The change here is just accounting for developers to be able to access `ref` fields directly and ensure they do so by the existing rules implicitly applied to `Span<T>`. 

This does mean though that `ref` fields can be returned as `ref` from a `ref struct` but normal fields cannot.

```c#
ref struct RS
{
    ref int _refField;
    int _field;

    // Okay: this falls into bullet one above. 
    public ref int Prop1 => ref _refField;

    // Error: This is bullet four above and the ref-safe-context of `this`
    // in a `struct` is function-member.
    public ref int Prop2 => ref _field;
}
```

This may seem like an error at first glance but this is a deliberate design point. Again though, this is not a new rule being created by this proposal, it is instead acknowledging the existing rules `Span<T>` behaved by now that developers can declare their own `ref` state.

Next the rules for ref reassignment need to be adjusted for the presence of `ref` fields. The primary scenario for ref reassignment is `ref struct` constructors storing `ref` parameters into `ref` fields. The support will be more general but this is the core scenario. To support this the rules for ref reassignment will be adjusted to account for `ref` fields as follows:

#### Ref reassignment rules
<a name="rules-ref-reassignment"></a>

The left operand of the `= ref` operator must be an expression that binds to a ref local variable, a ref parameter (other than `this`), an out parameter, **or a ref field**.

> For a ref reassignment in the form `e1 = ref e2` both of the following must be true:
> 1. `e2` must have *ref-safe-context* at least as large as the *ref-safe-context* of `e1`
> 2. `e1` must have the same *safe-context* as `e2` [Note](#examples-ref-reassignment-safety)

That means the desired `Span<T>` constructor works without any extra annotation:

```c#
readonly ref struct Span<T>
{
    readonly ref T _field;
    readonly int _length;

    public Span(ref T value)
    {
        // Falls into the `x.e1 = ref e2` case, where `x` is the implicit `this`. The 
        // safe-context of `this` is *return-only* and ref-safe-context of `value` is 
        // *caller-context* hence this is legal.
        _field = ref value;
        _length = 1;
    }
}
```

The change to ref reassignment rules means `ref` parameters can now escape from a method as a `ref` field in a `ref struct` value. As discussed in the [compat considerations section](#new-span-challenges) this can change the rules for existing APIs that never intended for `ref` parameters to escape as a `ref` field. The lifetime rules for parameters are based solely on their declaration not on their usage. All `ref` and `in` parameters have *ref-safe-context* of *caller-context* and hence can now be returned by `ref` or a `ref` field. In order to support APIs having `ref` parameters that can be escaping or non-escaping, and thus restore C# 10 call site semantics, the language will introduce limited lifetime annotations.

#### `scoped` modifier
<a name="rules-scoped"></a>

The keyword `scoped` will be used to restrict the lifetime of a value. It can be applied to a `ref` or a value that is a `ref struct` and has the impact of restricting the *ref-safe-context* or *safe-context* lifetime, respectively, to the *function-member*. For example: 

| Parameter or Local | ref-safe-context | safe-context |
|---|---|---|
| `Span<int> s` | *function-member* | *caller-context* | 
| `scoped Span<int> s` | *function-member* | *function-member* | 
| `ref Span<int> s` | *caller-context* | *caller-context* | 
| `scoped ref Span<int> s` | *function-member* | *caller-context* | 

In this relationship the *ref-safe-context* of a value can never be wider the *safe-context*.  

This allows for APIs in C# 11 to be annotated such that they have the same rules as C# 10:

```c#
Span<int> CreateSpan(scoped ref int parameter)
{
    // Just as with C# 10, the implementation of this method isn't relevant to callers.
}

Span<int> BadUseExamples(int parameter)
{
    // Legal in C# 10 and legal in C# 11 due to scoped ref
    return CreateSpan(ref parameter);

    // Legal in C# 10 and legal in C# 11 due to scoped ref
    int local = 42;
    return CreateSpan(ref local);

    // Legal in C# 10 and legal in C# 11 due to scoped ref
    Span<int> span = stackalloc int[42];
    return CreateSpan(ref span[0]);
}
```

The `scoped` annotation also means that the `this` parameter of a `struct` can now be defined as `scoped ref T`. Previously it had to be special cased in the rules as `ref` parameter that had different *ref-safe-context* rules than other `ref` parameters (see all the references to including or excluding the receiver in the safe context rules). Now it can be expressed as a general concept throughout the rules which further simplifies them.

The `scoped` annotation can also be applied to the following locations:

- locals: This annotation sets the lifetime as *safe-context*, or *ref-safe-context* in case of a `ref` local, to of *function-member* irrespective of the initializer lifetime. 

```c#
Span<int> ScopedLocalExamples()
{
    // Error: `span` has a safe-context of *function-member*. That is true even though the 
    // initializer has a safe-context of *caller-context*. The annotation overrides the 
    // initializer
    scoped Span<int> span = default;
    return span;

    // Okay: the initializer has safe-context of *caller-context* hence so does `span2` 
    // and the return is legal.
    Span<int> span2 = default;
    return span2;

    // The declarations of `span3` and `span4` are functionally identical because the 
    // initializer has a safe-context of *function-member* meaning the `scoped` annotation
    // is effectively implied on `span3`
    Span<int> span3 = stackalloc int[42];
    scoped Span<int> span4 = stackalloc int[42];
}
```

Other uses for `scoped` on locals are discussed [below](#examples-scoped-locals).

The `scoped` annotation cannot be applied to any other location including returns, fields, array elements, etc ... Further while `scoped` has impact when applied to any `ref`, `in` or `out` it only has impact when applied to values which are `ref struct`. Having declarations like `scoped int` has no impact because a non `ref struct` is always safe to return. The compiler will create a diagnostic for such cases to avoid developer confusion.

#### Change the behavior of `out` parameters
<a name="out-compat-change"></a>

To further limit the impact of the compat change of making `ref` and `in` parameters returnable as `ref` fields, the language will change the default *ref-safe-context* value for `out` parameters to be *function-member*. Effectively `out` parameters are implicitly `scoped out` going forward. From a compat perspective this means they cannot be returned by `ref`:

```c#
ref int Sneaky(out int i) 
{
    i = 42;

    // Error: ref-safe-context of out is now function-member
    return ref i;
}
```

This will increase the flexibility of APIs that return `ref struct` values and have `out` parameters because it does not have to consider the parameter being captured by reference anymore. This is important because it's a common pattern in reader style APIs:

```c#
Span<byte> Read(Span<byte> buffer, out int read)
{
    // .. 
}

Span<byte> Use()
{
    var buffer = new byte[256];

    // If we keep current `out` ref-safe-context this is an error. The language must consider
    // the `read` parameter as returnable as a `ref` field
    //
    // If we change `out` ref-safe-context this is legal. The language does not consider the 
    // `read` parameter to be returnable hence this is safe
    int read;
    return Read(buffer, out read);
}
```

The language will also no longer consider arguments passed to an `out` parameter to be returnable. Treating the input to an `out` parameter as returnable was extremely confusing to developers. It essentially subverts the intent of `out` by forcing developers to consider the value passed by the caller which is never used except in languages that don't respect `out`. Going forward languages that support `ref struct` must ensure the original value passed to an `out` parameter is never read. 

C# achieves this via it's definite assignment rules. That both achieves our ref safe context rules as well as allowing for existing code which assigns and then returns `out` parameters values.

```c#
Span<int> StrangeButLegal(out Span<int> span)
{
    span = default;
    return span;
}
```

Together these changes mean the argument to an `out` parameter does not contribute *safe-context* or *ref-safe-context* values to method invocations. This significantly reduces the overall compat impact of `ref` fields as well as simplifies how developers think about `out`. An argument to an `out` parameter does not contribute to the return, it is simply an output. 

#### Infer *safe-context* of declaration expressions
<a id="infer-safe-to-escape-of-declaration-expressions"></a>
The *safe-context* of a declaration variable from an `out` argument (`M(x, out var y)`) or deconstruction (`(var x, var y) = M()`) is the *narrowest* of the following:
* caller-context
* if out variable is marked `scoped`, then *declaration-block* (i.e. function-member or narrower).
* if out variable's type is `ref struct`, consider all arguments to the containing invocation, including the receiver:
  * *safe-context* of any argument where its corresponding parameter is not `out` and has *safe-context* of *return-only* or wider
  * *ref-safe-context* of any argument where its corresponding parameter has *ref-safe-context* of *return-only* or wider
    
See also [Examples of inferred *safe-context* of declaration expressions](#examples-of-inferred-safe-context-of-declaration-expressions).

#### Implicitly `scoped` parameters
<a name="implicitly-scoped"></a>
Overall there are two `ref` location which are implicitly declared as `scoped`:
- `this` on a `struct` instance method
- `out` parameters 

The ref safe context rules will be written in terms of `scoped ref` and `ref`. For ref safe context purposes an `in` parameter is equivalent to `ref` and `out` is equivalent to `scoped ref`. Both `in` and `out` will only be specifically called out when it is important to the semantic of the rule. Otherwise they are just considered `ref` and `scoped ref` respectively.

When discussing the *ref-safe-context* of arguments that correspond to `in` parameters they will be generalized as `ref` arguments in the spec. In the case the argument is an lvalue then the *ref-safe-context* is that of the lvalue, otherwise it is *function-member*. Again `in` will only be called out here when it is important to the semantic of the current rule.

#### Return-only safe context
<a name="return-only"></a>
The design also requires that the introduction of a new safe-context: *return-only*. This is similar to *caller-context* in that it can be returned but it can **only** be returned through a `return` statement. 

The details of *return-only* is that it's a context which is greater than *function-member* but smaller than *caller-context*. An expression provided to a `return` statement must be at least *return-only*. As such most existing rules fall out. For example assignment into a `ref` parameter from an expression with a *safe-context* of *return-only* will fail because it's smaller than the `ref` parameter's *safe-context* which is *caller-context*. The need for this new escape context will be discussed [below](#rules-unscoped). 

There are three locations which default to *return-only*:
- A `ref` or `in` parameter will have a *ref-safe-context* of *return-only*. This is done in part for `ref struct` to prevent [silly cyclic assignment](#cyclic-assignment) issues. It is done uniformly though to simplify the model as well as minimize compat changes.
- A `out` parameter for a `ref struct` will have *safe-context* of *return-only*. This allows for return and `out` to be equally expressive. This does not have the silly cyclic assignment problem because `out` is implicitly `scoped` so the *ref-safe-context* is still smaller than the *safe-context*.
- A `this` parameter for a `struct` constructor will have a *safe-context* of *return-only*. This falls out due to being modeled as `out` parameters. 

Any expression or statement which explicitly returns a value from a method or lambda must have a *safe-context*, and if applicable a *ref-safe-context*, of at least *return-only*. That includes `return` statements, expression bodied members and lambda expressions.

Likewise any assignment to an `out` must have a *safe-context* of at least *return-only*. This is not a special case though, this just follows from the existing assignment rules.

Note: An expression whose type is not a `ref struct` type always has a *safe-context* of *caller-context*. 

#### Rules for method invocation
<a name="rules-method-invocation"></a>

The ref safe context rules for method invocation will be updated in several ways. The first is by recognizing the impact that `scoped` has on arguments. For a given argument `expr` that is passed to parameter `p`:

> 1. If `p` is `scoped ref` then `expr` does not contribute *ref-safe-context* when considering arguments.
> 2. If `p` is `scoped` then `expr` does not contribute *safe-context* when considering arguments. 
> 3. If `p` is `out` then `expr` does not contribute *ref-safe-context* or *safe-context* [more details](#out-compat-change)

The language "does not contribute" means the arguments are simply not considered when calculating the *ref-safe-context* or *safe-context* value of the method return respectively. That is because the values can't contribute to that lifetime as the `scoped` annotation prevents it.

The method invocation rules can now be simplified. The receiver no longer needs to be special cased, in the case of `struct` it is now simply a `scoped ref T`. The value rules need to change to account for `ref` field returns:

> A value resulting from a method invocation `e1.M(e2, ...)`, where `M()` does not return ref-to-ref-struct, has a *safe-context* taken from the narrowest of the following:
> 1. The *caller-context*
> 2. When the return is a `ref struct` the *safe-context* contributed by all argument expressions
> 3. When the return is a `ref struct` the *ref-safe-context* contributed by all `ref` arguments
>
> If `M()` does return ref-to-ref-struct, the *safe-context* is the same as the *safe-context* of all arguments which are ref-to-ref-struct. It is an error if there are multiple arguments with different *safe-context* because of [method arguments must match](#rules-method-arguments-must-match).

The `ref` calling rules can be simplified to:

> A value resulting from a method invocation `ref e1.M(e2, ...)`, where `M()` does not return ref-to-ref-struct, is *ref-safe-context* the narrowest of the following contexts:
> 1. The *caller-context*
> 2. The *safe-context* contributed by all argument expressions
> 3. The *ref-safe-context* contributed by all `ref` arguments
>
> If `M()` does return ref-to-ref-struct, the *ref-safe-context* is the narrowest *ref-safe-context* contributed by all arguments which are ref-to-ref-struct.

This rule now lets us define the two variants of desired methods:

```c#
Span<int> CreateWithoutCapture(scoped ref int value)
{
    // Error: value Rule 3 specifies that the safe-context be limited to the ref-safe-context
    // of the ref argument. That is the *function-member* for value hence this is not allowed.
    return new Span<int>(ref value);
}

Span<int> CreateAndCapture(ref int value)
{
    // Okay: value Rule 3 specifies that the safe-context be limited to the ref-safe-context
    // of the ref argument. That is the *caller-context* for value hence this is not allowed.
    return new Span<int>(ref value);
}

Span<int> ComplexScopedRefExample(scoped ref Span<int> span)
{
    // Okay: the safe-context of `span` is *caller-context* hence this is legal.
    return span;

    // Okay: the local `refLocal` has a ref-safe-context of *function-member* and a 
    // safe-context of *caller-context*. In the call below it is passed to a 
    // parameter that is `scoped ref` which means it does not contribute 
    // ref-safe-context. It only contributes its safe-context hence the returned
    // rvalue ends up as safe-context of *caller-context*
    Span<int> local = default;
    ref Span<int> refLocal = ref local;
    return ComplexScopedRefExample(ref refLocal);

    // Error: similar analysis as above but the safe-context of `stackLocal` is 
    // *function-member* hence this is illegal
    Span<int> stackLocal = stackalloc int[42];
    return ComplexScopedRefExample(ref stackLocal);
}
```

#### Rules for object initializers

The *safe-context* of an object initializer expression is narrowest of:

1. The *safe-context* of the constructor call.
2. The *safe-context* and *ref-safe-context* of arguments to member initializer indexers that can escape to the receiver.
3. The *safe-context* of the RHS of assignments in member initializers to non-readonly setters or *ref-safe-context* in case of ref assignment.

Another way of modeling this is to think of any argument to a member initializer that can be assigned to the receiver as being an argument to the constructor. This is because the member initializer is effectively a constructor call.

```c#
Span<int> heapSpan = default;
Span<int> stackSpan = stackalloc int[42];
var x = new S(ref heapSpan)
{
    Field = stackSpan;
}

// Can be modeled as 
var x = new S(ref heapSpan, stackSpan);
```

This modeling is important because it demonstrates that our [MAMM](#rules-method-arguments-must-match) need to account specially for member initializers. Consider that this particular case needs to be illegal as it allows for a value with a narrower *safe-context* to be assigned to a higher one.

### Method arguments must match
<a name="rules-method-arguments-must-match"></a>

The presence of `ref` fields means the rules around method arguments must match need to be updated as a `ref` parameter can now be stored as a field in a `ref struct` argument to the method. Previously the rule only had to consider another `ref struct` being stored as a field. The impact of this is discussed in [the compat considerations](#compat-considerations). The new rule is ... 

> For any method invocation `e.M(a1, a2, ... aN)`
> 1. Calculate the narrowest *safe-context* from:
>     - *caller-context*
>     - The *safe-context* of all arguments
>     - The *ref-safe-context* of all ref arguments whose corresponding parameters have a *ref-safe-context* of *caller-context*
> 2. All `ref` arguments of `ref struct` types must be assignable by a value with that *safe-context*. This is a case where `ref` does **not** generalize to include `in` and `out`

> For any method invocation `e.M(a1, a2, ... aN)`
> 1. Calculate the narrowest *safe-context* from:
>     - *caller-context*
>     - The *safe-context* of all arguments
>     - The *ref-safe-context* of all ref arguments whose corresponding parameters are not `scoped` 
> 2. All `out` arguments of `ref struct` types must be assignable by a value with that *safe-context*.

The presence of `scoped` allows developers to reduce the friction this rule creates by marking parameters which are not returned as `scoped`. This removes their arguments from (1) in both cases above and provides greater flexibility to callers.

Impact of this change is discussed more deeply [below](#examples-method-arguments-must-match). Overall this will allow developers to make call sites more flexible by annotating non-escaping ref-like values with `scoped`.

#### Parameter scope variance
<a name="scoped-mismatch"></a>

The `scoped` modifier and `[UnscopedRef]` attribute (see [below](#rules-unscoped)) on parameters also impacts our object overriding, interface implementation and `delegate` conversion rules. The signature for an override, interface implementation or `delegate` conversion can: 
- Add `scoped` to a `ref` or `in` parameter
- Add `scoped` to a `ref struct` parameter
- Remove `[UnscopedRef]` from an `out` parameter
- Remove `[UnscopedRef]` from a `ref` parameter of a `ref struct` type

Any other difference with respect to `scoped` or `[UnscopedRef]` is considered a mismatch.

The compiler will report a diagnostic for _unsafe scoped mismatches_ across overrides, interface implementations, and delegate conversions when:
- The method has a `ref` or `out` parameter of `ref struct` type with a mismatch of adding `[UnscopedRef]` (not removing `scoped`).
  (In this case, a [silly cyclic assignment](#cyclic-assignment) is possible, hence no other parameters are necessary.)
- Or both of these are true:
  - The method returns a `ref struct` or returns a `ref` or `ref readonly`, or the method has a `ref` or `out` parameter of `ref struct` type.
  - The method has at least one additional `ref`, `in`, or `out` parameter, or a parameter of `ref struct` type.

The diagnostic is not reported in other cases because:
- The methods with such signatures cannot capture the refs passed in, so any scoped mismatch is not dangerous.
- These include very common and simple scenarios (e.g., plain old `out` parameters which are used in `TryParse` method signatures)
  and reporting scoped mismatches just because they are used across language version 11 (and hence the `out` parameter is differently scoped) would be confusing.

The diagnostic is reported as an _error_ if the mismatched signatures are both using C#11 ref safe context rules; otherwise, the diagnostic is a _warning_.

The scoped mismatch warning may be reported on a module compiled with C#7.2 ref safe context rules where `scoped` is not available. In some such cases, it may be necessary to suppress the warning if the other mismatched signature cannot be modified.

The `scoped` modifier and `[UnscopedRef]` attribute also have the following effects on method signatures:
- The `scoped` modifier and `[UnscopedRef]` attribute do not affect hiding
- Overloads cannot differ only on `scoped` or `[UnscopedRef]`

The section on `ref` field and `scoped` is long so wanted to close with a brief summary of the proposed breaking changes:

* A value that has *ref-safe-context* to the *caller-context* is returnable by `ref` or `ref` field.
* A `out` parameter would have a  *safe-context* of *function-member*.

Detailed Notes:
- A `ref` field can only be declared inside of a `ref struct` 
- A `ref` field cannot be declared `static`, `volatile` or `const`
- A `ref` field cannot have a type that is `ref struct`
- The reference assembly generation process must preserve the presence of a `ref` field inside a `ref struct` 
- A `readonly ref struct` must declare its `ref` fields as `readonly ref`
- For by-ref values the `scoped` modifier must appear before `in`, `out`, or `ref`
- The span safety rules document will be updated as outlined in this document
- The new ref safe context rules will be in effect when either 
    - The core library contains the feature flag indicating support for `ref` fields
    - The `langversion` value is 11 or higher

### Syntax
[13.6.2 Local variable declarations](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md#1362-local-variable-declarations): added `'scoped'?`.
```antlr
local_variable_declaration
    : 'scoped'? local_variable_mode_modifier? local_variable_type local_variable_declarators
    ;

local_variable_mode_modifier
    : 'ref' 'readonly'?
    ;
```

[13.9.4 The `for` statement](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md#1394-the-for-statement): added `'scoped'?` _indirectly_ from `local_variable_declaration`.

[13.9.5 The `foreach` statement](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md#1395-the-foreach-statement): added `'scoped'?`.
```antlr
foreach_statement
    : 'foreach' '(' 'scoped'? local_variable_type identifier 'in' expression ')'
      embedded_statement
    ;
```

[12.6.2 Argument lists](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1262-argument-lists): added `'scoped'?` for `out` declaration variable.
```antlr
argument_value
    : expression
    | 'in' variable_reference
    | 'ref' variable_reference
    | 'out' ('scoped'? local_variable_type)? identifier
    ;
```

[12.7 Deconstruction expressions](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#127-deconstruction):
```antlr
[TBD]
```

[15.6.2 Method parameters](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#1562-method-parameters): added `'scoped'?` to `parameter_modifier`.
```antlr
fixed_parameter
    : attributes? parameter_modifier? type identifier default_argument?
    ;

parameter_modifier
    | 'this' 'scoped'? parameter_mode_modifier?
    | 'scoped' parameter_mode_modifier?
    | parameter_mode_modifier
    ;

parameter_mode_modifier
    : 'in'
    | 'ref'
    | 'out'
    ;
```

[20.2 Delegate declarations](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/delegates.md#202-delegate-declarations): added `'scoped'?` _indirectly_ from `fixed_parameter`.

[12.19 Anonymous function expressions](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1219-anonymous-function-expressions): added `'scoped'?`.
```antlr
explicit_anonymous_function_parameter
    : 'scoped'? anonymous_function_parameter_modifier? type identifier
    ;

anonymous_function_parameter_modifier
    : 'in'
    | 'ref'
    | 'out'
    ;
```

### Sunset restricted types
The compiler has a concept of a set of "restricted types" which is largely undocumented. These types were given a special status because in C# 1.0 there was no general purpose way to express their behavior. Most notably the fact that the types can contain references to the execution stack. Instead the compiler had special knowledge of them and restricted their use to ways that would always be safe: disallowed returns, cannot use as array elements, cannot use in generics, etc ...

Once `ref` fields are available and extended to support `ref struct` these types can be correctly defined in C# using a combination of `ref struct` and `ref` fields. Therefore when the compiler detects that a runtime supports `ref` fields it will no longer have a notion of restricted types. It will instead use the types as they are defined in the code. 

To support this our ref safe context rules will be updated as follows:

- `__makeref` will be treated as a method with the signature `static TypedReference __makeref<T>(ref T value)`
- `__refvalue` will be treated as a method with the signature `static ref T __refvalue<T>(TypedReference tr)`. The expression `__refvalue(tr, int)` will effectively use the second argument as the type parameter.
- `__arglist` as a parameter will have a *ref-safe-context* and *safe-context* of *function-member*. 
- `__arglist(...)` as an expression will have a *ref-safe-context* and *safe-context* of *function-member*. 

Conforming runtimes will ensure that `TypedReference`, `RuntimeArgumentHandle` and `ArgIterator` are defined as `ref struct`. Further `TypedReference` must be viewed as having a `ref` field to a `ref struct` for any possible type (it can store any value). That combined with the above rules will ensure references to the stack do not escape beyond their lifetime.

Note: strictly speaking this is a compiler implementation detail vs. part of the language. But given the relationship with `ref` fields it is being included in the language proposal for simplicity.

### Provide unscoped
One of the most notable friction points is the inability to return fields by `ref` in instance members of a `struct`. This means developers can't create `ref` returning methods / properties and have to resort to exposing fields directly. This reduces the usefulness of `ref` returns in `struct` where it is often the most desired. 

```c#
struct S
{
    int _field;

    // Error: this, and hence _field, can't return by ref
    public ref int Prop => ref _field;
}
```

The [rationale](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md#struct-this-escape) for this default is reasonable but there is nothing inherently wrong with a `struct` escaping `this` by reference, it is simply the default chosen by the ref safe context rules. 

<a name="rules-unscoped"></a>

To fix this the language will provide the opposite of the `scoped` lifetime annotation by supporting an `UnscopedRefAttribute`. This can be applied to any `ref` and it will change the *ref-safe-context* to be one level wider than its default. For example:

| UnscopedRef applied to | Original *ref-safe-context* | New *ref-safe-context* |
| --- | --- | --- |
| instance member | function-member | return-only |
| `in` / `ref` parameter | return-only | caller-context |
| `out` parameter | function-member | return-only |

When applying `[UnscopedRef]` to an instance method of a `struct` it has the impact of modifying the implicit `this` parameter. This means `this` acts as an unannotated `ref` of the same type. 

```c#
struct S
{
    int field; 

    // Error: `field` has the ref-safe-context of `this` which is *function-member* because 
    // it is a `scoped ref`
    ref int Prop1 => ref field;

    // Okay: `field` has the ref-safe-context of `this` which is *caller-context* because 
    // it is a `ref`
    [UnscopedRef] ref int Prop1 => ref field;
}
```

The annotation can also be placed on `out` parameters to restore them to C# 10 behavior.

```c#
ref int SneakyOut([UnscopedRef] out int i)
{
    i = 42;
    return ref i;
}
```

For the purposes of ref safe context rules, such an `[UnscopedRef] out` is considered simply a `ref`. Similar to how `in` is considered `ref` for lifetime purposes. 

The `[UnscopedRef]` annotation will be disallowed on `init` members and constructors inside `struct`. Those members are already special with respect to `ref` semantics as they view `readonly` members as mutable. This means taking `ref` to those members appears as a simple `ref`, not `ref readonly`. This is allowed within the boundary of constructors and `init`. Allowing `[UnscopedRef]` would permit such a `ref` to incorrectly escape outside the constructor and permit mutation after `readonly` semantics had taken place.

The attribute type will have the following definition:

```c#
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter,
        AllowMultiple = false,
        Inherited = false)]
    public sealed class UnscopedRefAttribute : Attribute
    {
    }
}
```

Detailed Notes:
- An instance method or property annotated with `[UnscopedRef]` has *ref-safe-context* of `this` set to the *caller-context*.
- A member annotated with `[UnscopedRef]` cannot implement an interface.
- It is an error to use `[UnscopedRef]` on 
    - A member that is not declared on a `struct`
    - A `static` member, `init` member or constructor on a `struct`
    - A parameter marked `scoped`
    - A parameter passed by value
    - A parameter passed by reference that is not implicitly scoped

### ScopedRefAttribute
The `scoped` annotations will be emitted into metadata via the type `System.Runtime.CompilerServices.ScopedRefAttribute` attribute. The attribute will be matched by namespace-qualified name so the definition does not need to appear in any specific assembly.

The `ScopedRefAttribute` type is for compiler use only - it is not permitted in source. The type declaration is synthesized by the compiler if not already included in the compilation.

The type will have the following definition:

```c#
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class ScopedRefAttribute : Attribute
    {
    }
}
```

The compiler will emit this attribute on the parameter with `scoped` syntax. This will only be emitted when the syntax causes the value to differ from its default state. For example `scoped out` will cause no attribute to be emitted.

### RefSafetyRulesAttribute
There are several differences in the _ref safe context_ rules between C#7.2 and C#11. Any of these differences could result in breaking changes when recompiling with C#11 against references compiled with C#10 or earlier.
1. unscoped `ref`/`in`/`out` parameters may escape a method invocation as a `ref` field of a `ref struct` in C#11, not in C#7.2
1. `out` parameters are implicitly scoped in C#11, and unscoped in C#7.2
1. `ref`/`in` parameters to `ref struct` types are implicitly scoped in C#11, and unscoped in C#7.2

To reduce the chance of breaking changes when recompiling with C#11, we will update the C#11 compiler to use the ref safe context rules _for method invocation_ that _match the rules that were used to analyze the method declaration_. Essentially, when analyzing a call to a method compiled with an older compiler, the C#11 compiler will use C#7.2 ref safe context rules. 

To enable this, the compiler will emit a new `[module: RefSafetyRules(11)]` attribute when the module is compiled with `-langversion:11` or higher or compiled with a corlib containing the feature flag for `ref` fields.

The argument to the attribute indicates the language version of the _ref safe context_ rules used when the module was compiled.
The version is currently fixed at `11` regardless of the actual language version passed to the compiler.

The expectation is that future versions of the compiler will update the ref safe context rules and emit attributes with distinct versions.

If the compiler loads a module that includes a `[module: RefSafetyRules(version)]` _with a `version` other than `11`_, the compiler will report a warning for the unrecognized version if there are any calls to methods declared in that module.

When the C#11 compiler _analyzes a method call_:
- If the module containing the method declaration includes `[module: RefSafetyRules(version)]`, regardless of `version`, the method call is analyzed with C#11 rules.
- If the module containing the method declaration is from source, and compiled with `-langversion:11` or with a corlib containing the feature flag for `ref` fields, the method call is analyzed with C#11 rules.
- _If the module containing the method declaration references `System.Runtime { ver: 7.0 }`, the method call is analyzed with C#11 rules. This rule is a temporary mitigation for modules compiled with earlier previews of C#11 / .NET 7 and will be removed later._
- Otherwise, the method call is analyzed with C#7.2 rules.

A pre-C#11 compiler will ignore any `RefSafetyRulesAttribute` and analyze method calls with C#7.2 rules only.

The `RefSafetyRulesAttribute` will be matched by namespace-qualified name so the definition does not need to appear in any specific assembly.

The `RefSafetyRulesAttribute` type is for compiler use only - it is not permitted in source. The type declaration is synthesized by the compiler if not already included in the compilation.

```csharp
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
    internal sealed class RefSafetyRulesAttribute : Attribute
    {
        public RefSafetyRulesAttribute(int version) { Version = version; }
        public readonly int Version;
    }
}
```

### Safe fixed size buffers

Safe fixed size buffers was not delivered in C# 11. This feature may be implemented in a future version of C#.

<details>

The language will relax the restrictions on fixed sized arrays such that they can be declared in safe code and the element type can be managed or unmanaged.  This will make types like the following legal:

```c#
internal struct CharBuffer
{
    internal char Data[128];
}
```

These declarations, much like their `unsafe` counter parts, will define a sequence of `N` elements in the containing type. These members can be accessed with an indexer and can also be converted to `Span<T>` and `ReadOnlySpan<T>` instances.

When indexing into a `fixed` buffer of type `T` the `readonly` state of the container must be taken into account.  If the container is `readonly` then the indexer returns `ref readonly T` else it returns `ref T`. 

Accessing a `fixed` buffer without an indexer has no natural type however it is convertible to `Span<T>` types. In the case the container is `readonly` the buffer is implicitly convertible to `ReadOnlySpan<T>`, else it can implicitly convert to `Span<T>` or `ReadOnlySpan<T>` (the `Span<T>` conversion is considered *better*). 

The resulting `Span<T>` instance will have a length equal to the size declared on the `fixed` buffer. The *safe-context* of the returned value will be equal to the *safe-context* of the container, just as it would if the backing data was accessed as a field.

For each `fixed` declaration in a type where the element type is `T` the language will generate a corresponding `get` only indexer method whose return type is `ref T`. The indexer will be annotated with the `[UnscopedRef]` attribute as the implementation will be returning fields of the declaring type. The accessibility of the member will match the accessibility on the `fixed` field.

For example, the signature of the indexer for `CharBuffer.Data` will be the following:

```c#
[UnscopedRef] internal ref char DataIndexer(int index) => ...;
```

If the provided index is outside the declared bounds of the `fixed` array then an `IndexOutOfRangeException` will be thrown. In the case a constant value is provided then it will be replaced with a direct reference to the appropriate element. Unless the constant is outside the declared bounds in which case a compile time error would occur.

There will also be a named accessor generated for each `fixed` buffer that provides by value `get` and `set` operations. Having this means that `fixed` buffers will more closely resemble existing array semantics by having a `ref` accessor as well as byval `get` and `set` operations. This means compilers will have the same flexibility when emitting code consuming `fixed` buffers as they do when consuming arrays. This should make operations like `await` over `fixed` buffers easier to emit. 

This also has the added benefit that it will make `fixed` buffers easier to consume from other languages. Named indexers is a feature that has existed since the 1.0 release of .NET. Even languages which cannot directly emit a named indexer can generally consume them (C# is actually a good example of this).

The backing storage for the buffer will be generated using the `[InlineArray]` attribute. This is a mechanism discussed in [issue 12320](https://github.com/dotnet/runtime/issues/12320) which allows specifically for the case of efficiently declaring sequence of fields of the same type. This particular issue is still under active discussion and the expectation is that the implementation of this feature will follow however that discussion goes.

</details>

### Initializers with `ref` values in `new` and `with` expressions

In section [12.8.17.3 Object initializers](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#128173-object-initializers), we update the grammar to:

```antlr
initializer_value
    : 'ref' expression // added
    | expression
    | object_or_collection_initializer
    ;
```

In the section for [`with` expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#with-expression), we update the grammar to:
```antlr
member_initializer
    : identifier '=' 'ref' expression // added
    | identifier '=' expression
    ;
```

The left operand of the assignment must be an expression that binds to a ref field.  
The right operand must be an expression that yields an lvalue designating a value of the same type as the left operand.  

We add a similar rule to [ref local reassignment](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.3/ref-local-reassignment.md):  
If the left operand is a writeable ref (i.e. it designates anything other than a `ref readonly` field), then the right operand must be a writeable lvalue.

The escape rules for [constructor invocations](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md#constructor-invocations) remain:
> A `new` expression that invokes a constructor obeys the same rules as a method invocation that is considered to return the type being constructed.

Namely the rules of [method invocation](#rules-method-invocation) updated above:
> An rvalue resulting from a method invocation `e1.M(e2, ...)` has *safe-context* from the smallest of the following contexts:
> 1. The *caller-context*
> 2. The *safe-context* contributed by all argument expressions
> 3. When the return is a `ref struct` then *ref-safe-context* contributed by all `ref` arguments

For a `new` expression with initializers, the initializer expressions count as arguments (they contribute their *safe-context*) and the `ref` initializer expressions count as `ref` arguments (they contribute their *ref-safe-context*), recursively.

## Changes in unsafe context

Pointer types ([section 23.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#233-pointer-types)) are extended to allow managed types as referent type.
Such pointer types are written as a managed type followed by a `*` token. They produce a warning.

The address-of operator ([section 23.6.5](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2365-the-address-of-operator)) is relaxed to accept a variable with a managed type as its operand.

The `fixed` statement ([section 23.7](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#237-the-fixed-statement)) is relaxed to accept _fixed_pointer_initializer_ that is the address of a variable of managed type `T` or that is an expression of an _array_type_ with elements of a managed type `T`.

The stack allocation initializer ([section 12.8.22](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12822-stack-allocation)) is similarly relaxed.

## Considerations
There are considerations other parts of the development stack should consider when evaluating this feature.

### Compat Considerations
<a name="compat-considerations">

The challenge in this proposal is the compatibility implications this design has to our existing [span safety rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md), or [§9.7.2](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/variables.md#972-ref-safe-contexts). While those rules fully support the concept of a `ref struct` having `ref` fields they do not allow for APIs, other than `stackalloc`, to capture `ref` state that refers to the stack. The ref safe context rules have a [hard assumption](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md#span-constructor), or [§16.4.12.8](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/structs.md#164128-constructor-invocations) that a constructor of the form `Span(ref T value)` does not exist. That means the safety rules do not account for a `ref` parameter being able to escape as a `ref` field hence it allows for code like the following.

```c#
Span<int> CreateSpanOfInt()
{
    // This is legal according to the 7.2 span rules because they do not account
    // for a constructor in the form Span(ref T value) existing. 
    int local = 42;
    return new Span<int>(ref local);
}
```

<a name="ways-to-escape"></a>

Effectively there are three ways for a `ref` parameter to escape from a method invocation: 

1. By value return
2. By `ref` return
3. By `ref` field in `ref struct` that is returned or passed as `ref` / `out` parameter

The existing rules only account for (1) and (2). They do not account for (3) hence gaps like returning locals as `ref` fields are not accounted for. This design must change the rules to account for (3). This will have a small impact to compatibility for existing APIs. Specifically it will impact APIs that have the following properties.

- Have a `ref struct` in the signature
    - Where the `ref struct` is a return type, `ref` or `out` parameter
    - Has an additional `in` or `ref` parameter excluding the receiver

In C# 10 callers of such APIs never had to consider that `ref` state input to the API could be captured as a `ref` field. That allowed for several patterns to exist, safely in C# 10, that will be unsafe in C# 11 due to the ability for `ref` state to escape as a `ref` field. For example:

<a name="new-span-challenges"></a>

```c#
Span<int> CreateSpan(ref int parameter)
{
    // The implementation of this method is irrelevant when considering the lifetime of the 
    // returned Span<T>. The ref safe context rules only look at the method signature, not the 
    // implementation. In C# 10 ref fields didn't exist hence there was no way for `parameter`
    // to escape by ref in this method
}

Span<int> BadUseExamples(int parameter)
{
    // Legal in C# 10 but would be illegal with ref fields
    return CreateSpan(ref parameter);

    // Legal in C# 10 but would be illegal with ref fields
    int local = 42;
    return CreateSpan(ref local);

    // Legal in C# 10 but would be illegal with ref fields
    Span<int> span = stackalloc int[42];
    return CreateSpan(ref span[0]);
}
```

The impact of this compatibility break is expected to be very small. The impacted API shape made little sense in the absence of `ref` fields hence it is unlikely customers created many of these. Experiments running tools to spot this API shape over existing repositories back up that assertion. The only repository with any significant counts of this shape is [dotnet/runtime](https://github.com/dotnet/runtime) and that is because that repo can create `ref` fields via the `ByReference<T>` intrinsic type.

Even so the design must account for such APIs existing because it expresses a valid pattern, just not a common one. Hence the design must give developers the tools to restore the existing lifetime rules when upgrading to C# 10. Specifically it must provide mechanisms that allow developers to annotate `ref` parameters as unable to escape by `ref` or `ref` field. That allows customers to define APIs in C# 11 that have the same C# 10 callsite rules.

### Reference Assemblies
A reference assembly for a compilation using features described in this proposal must maintain the elements that convey ref safe context information. That means all lifetime annotation attributes must be preserved in their original position. Any attempt to replace or omit them can lead to invalid reference assemblies.

Representing `ref` fields is more nuanced. Ideally a `ref` field would appear in a reference assembly as would any other field. However a `ref` field represents a change to the metadata format and that can cause issues with tool chains that are not updated to understand this metadata change. A concrete example is C++/CLI which will likely error if it consumes a `ref` field. Hence it's advantageous if `ref` fields can be omitted from reference assemblies in our core libraries. 

A `ref` field by itself has no impact on ref safe context rules. As a concrete example consider that flipping the existing `Span<T>` definition to use a `ref` field has no impact on consumption. Hence the `ref` itself can be omitted safely. However a `ref` field does have other impacts to consumption that must be preserved: 

- A `ref struct` which has a `ref` field is never considered `unmanaged` 
- The type of the `ref` field impacts infinite generic expansion rules. Hence if the type of a `ref` field contains a type parameter that must be preserved 

Given those rules here is a valid reference assembly transformation for a `ref struct`: 

```c#
// Impl assembly 
ref struct S<T>
{
    ref T _field;
}

// Ref assembly 
ref struct S<T>
{
    object _o; // force managed 
    T _f; // maintain generic expansion protections
}
```

### Annotations
Lifetimes are most naturally expressed using types. A given program's lifetimes are safe when the lifetime types type check. While the syntax of C# implicitly adds lifetimes to values, there is an underlying type system that describes the fundamental rules here. It's often easier to discuss the implication of changes to the design in terms of these rules so they are included here for discussion sake.

Note that this is not meant to be a 100% complete documentation. Documenting every single behavior isn't a goal here. Instead it's meant to establish a general understanding and common verbiage by which the model, and potential changes to it, can be discussed.

Usually it's not necessary to directly talk about lifetime types. The exceptions are places where lifetimes can vary based on particular "instantiation" sites. This is a kind of polymorphism and we call these varying lifetimes "generic lifetimes", represented as generic parameters. C# does not provide syntax for expressing lifetime generics, so we define an implicit "translation" from C# to an expanded lowered language that contains explicit generic parameters.

The below examples make use of named lifetimes. The syntax `$a` refers to a lifetime named `a`. It is a lifetime that has no meaning by itself but can be given a relationship to other lifetimes via the `where $a : $b` syntax. This establishes that `$a` is convertible to `$b`. It may help to think of this as establishing that `$a` is a lifetime at least as long as `$b`.

There are a few predefined lifetimes for convenience and brevity below:

- `$heap`: this is the lifetime of any value that exists on the heap. It is available in all contexts and method signatures.
- `$local`: this is the lifetime of any value that exists on the method stack. It's effectively a name place holder for *function-member*. It is implicitly defined in methods and can appear in method signatures except for any output position.
- `$ro`: name place holder for *return only*
- `$cm`: name place holder for *caller-context* 

There are a few predefined relationships between lifetimes:

- `where $heap : $a` for all lifetimes `$a`
- `where $cm : $ro` 
- `where $x : $local` for all predefined lifetimes. User defined lifetimes have no relationship to local unless explicitly defined.

Lifetime variables when defined on types can be invariant or covariant. These are expressed using the same syntax as generic parameters:

```csharp
// $this is covariant
// $a is invariant
ref struct S<out $this, $a> 
```

The lifetime parameter `$this` on type definitions is _not_ predefined but it does have a few rules associated with it when it is defined:
- It must be the first lifetime parameter.
- It must be covariant: `out $this`. 
- The lifetime of `ref` fields must be convertible to `$this`
- The `$this` lifetime of all non-ref fields must be `$heap` or `$this`.

The lifetime of a ref is expressed by providing a lifetime argument to the ref. For example a `ref` that refers to the heap is expressed as `ref<$heap>`.

When defining a constructor in the model the name `new` will be used for the method. It is necessary to have a parameter list for the returned value as well as the constructor arguments. This is necessary to express the relationship between constructor inputs and the constructed value. Rather than having `Span<$a><$ro>` the model will use `Span<$a> new<$ro>` instead. The type of `this` in the constructor, including lifetimes, will be the defined return value.

The basic rules for the lifetime are defined as:

- All lifetimes are expressed syntactically as generic arguments, coming before type arguments. This is true for predefined lifetimes except `$heap` and `$local`. 
- All types `T` that are not a `ref struct` implicitly have lifetime of `T<$heap>`. This is implicit, there is no need to write `int<$heap>` in every sample.
- For a `ref` field defined as `ref<$l0> T<$l1, $l2, ... $ln>`:
    - All lifetimes `$l1` through `$ln` must be invariant. 
    - The lifetime of `$l0` must be convertible to `$this`
- For a `ref` defined as `ref<$a> T<$b, ...>`, `$b` must be convertible to `$a`
- The `ref` of a variable has a lifetime defined by:
    - For a `ref` local, parameter, field or return of type `ref<$a> T` the lifetime is `$a`
    - `$heap` for all reference types and fields of reference types
    - `$local` for everything else
- An assignment or return is legal when the underlying type conversion is legal
- Lifetimes of expressions can be made explicit by using cast annotations:
    - `(T<$a> expr)` the value lifetime is explicitly `$a` for `T<...>`
    - `ref<$a> (T<$b>)expr` the value lifetime is `$b` for `T<...>` and the ref lifetime is `$a`.

For the purpose of lifetime rules a `ref` is considered part of the type of the expression for purposes of conversions. It is logically represented by converting `ref<$a> T<...>` to `ref<$a, T<...>>` where `$a` is covariant and `T` is invariant. 

Next let's define the rules that allow us to map C# syntax to the underlying model.

For brevity sake a type which has no explicit lifetime parameters treated as if there is `out $this` defined and applied to all fields of the type. A type with a `ref` field must define explicit lifetime parameters.

These rules exists to support our existing invariant that `T` can be assigned to `scoped T` for all types. That maps down to `T<$a, ...>` being assignable to `T<$local, ...>` for all lifetimes known to be convertible to `$local`. Further this supports other items like being able to assign `Span<T>` from the heap to those on the stack. This does exclude types where fields have differing lifetimes for non-ref values but that is the reality of C# today. Changing that would require a significant change of C# rules that would need to be mapped out. 

The type of `this` for a type `S<out $this, ...>` inside an instance method is implicitly defined as the following:
- For normal instance method: `ref<$local> S<$cm, ...>`
- For instance method annotated with `[UnscopedRef]`: `ref<$ro> S<$cm, ...>`

The lack of an explicit `this` parameter forces the implicit rules here. For complex samples and discussions consider writing as a `static` method and making `this` an explicit parameter.

```csharp
ref struct S<out $this>
{
    // Implicit this can make discussion confusing 
    void M<$ro, $cm>(ref<$ro> S<$cm> s) {  }

    // Rewrite as explicit this to simplify discussion
    static void M<$ro, $cm>(ref<$local> S<$cm> this, ref<$ro> S<$cm> s) { }
}
```

The C# method syntax maps to the model in the following ways: 

- `ref` parameters have a ref lifetime of `$ro`
- parameters of type `ref struct` have a this lifetime of `$cm`
- ref returns have a ref lifetime of `$ro`
- returns of type `ref struct` have a value lifetime of `$ro`
- `scoped` on a parameter or `ref` changes the ref lifetime to be `$local`

Given that let's explore a simple example that demonstrates the model here: 

```csharp
ref int M1(ref int i) => ...

// Maps to the following. 

ref<$ro> int Identity<$ro>(ref<$ro> int i)
{
    // okay: has ref lifetime $ro which is equal to $ro
    return ref i;

    // okay: has ref lifetime $heap which convertible $ro
    int[] array = new int[42];
    return ref array[0];

    // error: has ref lifetime $local which has no conversion to $a hence 
    // it's illegal
    int local = 42;
    return ref local;
}
```

Now let's explore the same example using a `ref struct`: 

```csharp
ref struct S
{
    ref int Field;

    S(ref int f)
    {
        Field = ref f;
    }
}

S M2(ref int i, S span1, scoped S span2) => ...

// Maps to 

ref struct S<out $this>
{
    // Implicitly 
    ref<$this> int Field;

    S<$ro> new<$ro>(ref<$ro> int f)
    {
        Field = ref f;
    }
}

S<$ro> M2<$ro>(
    ref<$ro> int i,
    S<$ro> span1)
    S<$local> span2)
{
    // okay: types match exactly
    return span1;

    // error: has lifetime $local which has no conversion to $ro
    return span2;

    // okay: type S<$heap> has a conversion to S<$ro> because $heap has a
    // conversion to $ro and the first lifetime parameter of S<> is covariant
    return default(S<$heap>)

    // okay: the ref lifetime of ref $i is $ro so this is just an 
    // identity conversion
    S<$ro> local = new S<$ro>(ref $i);
    return local;

    int[] array = new int[42];
    // okay: S<$heap> is convertible to S<$ro>
    return new S<$heap>(ref<$heap> array[0]);

    // okay: the parameter of the ctor is $ro ref int and the argument is $heap ref int. These 
    // are convertible.
    return new S<$ro>(ref<$heap> array[0]);

    // error: has ref lifetime $local which has no conversion to $a hence 
    // it's illegal
    int local = 42;
    return ref local;
}
```

Next let's see how this helps with the cyclic self assignment problem:

```csharp
ref struct S
{
    int field;
    ref int refField;

    static void SelfAssign(ref S s)
    {
        s.refField = ref s.field;
    }
}

// Maps to 

ref struct S<out $this>
{
    int field;
    ref<$this> int refField;

    static void SelfAssign<$ro, $cm>(ref<$ro> S<$cm> s)
    {
        // error: the types work out here to ref<$cm> int = ref<$ro> int and that is 
        // illegal as $ro has no conversion to $cm (the relationship is the other direction)
        s.refField = ref<$ro> s.field;
    }
}
```

Next let's see how this helps with the silly capture parameter problem: 

```csharp
ref struct S
{
    ref int refField;

    void Use(ref int parameter)
    {
        // error: this needs to be an error else every call to this.Use(ref local) would fail 
        // because compiler would assume the `ref` was captured by ref.
        this.refField = ref parameter;
    }
}

// Maps to 

ref struct S<out $this>
{
    ref<$this> int refField;
    
    // Using static form of this method signature so the type of this is explicit. 
    static void Use<$ro, $cm>(ref<$local> S<$cm> @this, ref<$ro> int parameter)
    {
        // error: the types here are:
        //  - refField is ref<$cm> int
        //  - ref parameter is ref<$ro> int
        // That means the RHS is not convertible to the LHS ($ro is not covertible to $cm) and 
        // hence this reassignment is illegal
        @this.refField = ref<$ro> parameter;
    }
}
```

## Open Issues

### Change the design to avoid compat breaks
This design proposes several compatibility breaks with our existing ref-safe-context rules. Even though the breaks are believed to be minimally impactful significant consideration was given to a design which had no breaking changes.

The compat preserving design though was significantly more complex than this one. In order to preserve compat `ref` fields need distinct lifetimes for the ability to return by `ref` and return by `ref` field. Essentially it requires us to provide *ref-field-safe-context* tracking for all parameters to a method. This needs to be calculated for all expressions and tracked in all values virtually everywhere that *ref-safe-context* is tracked today.

Further this value has relationships with *ref-safe-context*. For example it's non-sensical to have a value can be returned as a `ref` field but not directly as `ref`. That is because `ref` fields can be trivially returned by `ref` already (`ref` state in a `ref struct` can be returned by `ref` even when the containing value cannot). Hence the rules further need constant adjustment to ensure these values are sensible with respect to each other. 

Also it means the language needs syntax to represent `ref` parameters that can be returned in three different ways: by `ref` field, by `ref` and by value. The default being returnable by `ref`. Going forward though the more natural return, particularly when `ref struct` are involved is expected to be by `ref` field or `ref`. That means new APIs require an extra syntax annotation to be correct by default. This is undesirable. 

These compat changes though will impact methods that have the following properties:

- Have a `Span<T>` or `ref struct`
    - Where the `ref struct` is a return type, `ref` or `out` parameter
    - Has an additional `in` or `ref` parameter (excluding the receiver)

To understand the impact it's helpful to break APIs into categories:

1. Want consumers to account for `ref` being captured as a `ref` field. Prime example is the `Span(ref T value)` constructors 
2. Do not want consumers to account for `ref` being captured as a `ref` field. These though break into two categories
    1. Unsafe APIs. These are APIS inside the `Unsafe` and `MemoryMarshal` types, of which `MemoryMarshal.CreateSpan` is the most prominent. These APIs do capture the `ref` unsafely but they are also known to be unsafe APIs.
    2. Safe APIs. These are APIs which take `ref` parameters for efficiency but it is not actually captured anywhere. Examples are small but one is `AsnDecoder.ReadEnumeratedBytes`

This change primarily benefits (1) above. These are expected to make up the majority of APIs that take a `ref` and return a `ref struct` going forward. The changes negatively impact (2.1) and (2.2) as it breaks the existing calling semantics because the lifetime rules change. 

The APIs in category (2.1) though are largely authored by Microsoft or by developers who stand the most to benefit from `ref` fields (the Tanner's of the world). It is reasonable to assume this class of developers would be amenable to a compatibility tax on upgrade to C# 11 in the form of a few annotations to retain the existing semantics if `ref` fields were provided in return.

The APIs in category (2.2) are the biggest issue. It is unknown how many such APIs exist and it's unclear if these would be more / less frequent in 3rd party code. The expectation is there is a very small number of them, particularly if we take the compat break on `out`. Searches so far have revealed a very small number of these existing in `public` surface area. This is a hard pattern to search for though as it requires semantic analysis. Before taking this change a tool based approach would be needed to verify the assumptions around this impacting a small number of known cases.

For both cases in category (2) though the fix is straight forward. The `ref` parameters that do not want to be considered capturable must add `scoped` to the `ref`. In (2.1) this will likely also force the developer to use `Unsafe` or `MemoryMarshal` but that is expected for unsafe style APIs.

Ideally the language could reduce the impact of silent breaking changes by issuing a warning when an API silently falls into the troublesome behavior. That would be a method that both takes a `ref`, returns `ref struct` but does not actually capture the `ref` in the `ref struct`. The compiler could issue a diagnostic in that case informing developers such `ref` should be annotated as `scoped ref` instead. 

**Decision** This design can be achieved but the resulting feature is more difficult to use to the point the decision was made to take the compat break.

**Decision** The compiler will provide a warning when a method meets the criteria but does not capture the `ref` parameter as a `ref` field. This should suitably warn customers on upgrade about the potential issues they are creating

### Keywords vs. attributes
This design calls for using attributes to annotate the new lifetime rules. This also could've been done just as easily with contextual keywords. For instance `[DoesNotEscape]` could map to `scoped`. However keywords, even the contextual ones, generally must meet a very high bar for inclusion. They take up valuable language real estate and are more prominent parts of the language. This feature, while valuable, is going to serve a minority of C# developers.

On the surface that would seem to favor not using keywords but there are two important points to consider: 

1. The annotations will effect program semantics. Having attributes impact program semantics is a line C# is reluctant to cross and it's unclear if this is the feature that should justify the language taking that step.
1. The developers most likely to use this feature intersect strongly with the set of developers that use function pointers. That feature, while also used by a minority of developers, did warrant a new syntax and that decision is still seen as sound. 

Taken together this means syntax should be considered.

A rough sketch of the syntax would be: 

- `[RefDoesNotEscape]` maps to `scoped ref` 
- `[DoesNotEscape]` maps to `scoped`
- `[RefDoesEscape]` maps to `unscoped`

**Decision** Use syntax for `scoped` and `scoped ref`; use attribute for `unscoped`.

### Allow fixed buffer locals
This design allows for safe `fixed` buffers that can support any type. One possible extension here is allowing such `fixed` buffers to be declared as local variables. This would allow a number of existing `stackalloc` operations to be replaced with a `fixed` buffer. It would also expand the set of scenarios we could have stack style allocations as `stackalloc` is limited to unmanaged element types while `fixed` buffers are not. 

```c#
class FixedBufferLocals
{
    void Example()
    {
        Span<int> span = stackalloc int[42];
        int buffer[42];
    }
}
```

This holds together but does require us to extend the syntax for locals a bit.  Unclear if this is or isn't worth the extra complexity. Possible we could decide no for now and bring back later if sufficient need is demonstrated.

Example of where this would be beneficial: https://github.com/dotnet/runtime/pull/34149

**Decision** hold off on this for now

### To use modreqs or not
A decision needs to be made if methods marked with new lifetime attributes should or should not translate to `modreq` in emit. There would be effectively a 1:1 mapping between annotations and `modreq` if this approach was taken.

The rationale for adding a `modreq` is the attributes change the semantics of ref safe context rules. Only languages which understand these semantics should be calling the methods in question. Further when applied to OHI scenarios, the lifetimes become a contract that all derived methods must implement. Having the annotations exist without `modreq` can lead to situations where `virtual` method chains with conflicting lifetime annotations are loaded (can happen if only one part of `virtual` chain is compiled and other is not). 

The initial ref safe context work did not use `modreq` but instead relied on languages and the framework to understand. At the same time though all of the elements that contribute to the ref safe context rules are a strong part of the method signature: `ref`, `in`, `ref struct`, etc ... Hence any change to the existing rules of a method already results in a binary change to the signature. To give the new lifetime annotations the same impact they will need `modreq` enforcement.

The concern is whether or not this is overkill. It does have the negative impact that making signatures more flexible, by say adding `[DoesNotEscape]` to a parameter, will result in a binary compat change. That trade off means that over time frameworks like BCL likely won't be able to relax such signatures. It could be mitigated to a degree by taking some approach the language does with `in` parameters and only apply `modreq` in virtual positions. 

**Decision** Do not use `modreq` in metadata. The difference between `out` and `ref` is not `modreq` but they now have different ref safe context values. There is no real benefit to only half enforcing the rules with `modreq` here.

### Allow multi-dimensional fixed buffers
Should the design for `fixed` buffers be extended to include multi-dimensional style arrays? Essentially allowing for declarations like the following:

```c#
struct Dimensions
{
    int array[42, 13];
}
```

**Decision** Do not allow for now

### Violating scoped
The runtime repository has several non-public APIs that capture `ref` parameters as `ref` fields. These are unsafe because the lifetime of the resulting value is not tracked. For example the `Span<T>(ref T value, int length)` constructor.

The majority of these APIs will likely choose to have proper lifetime tracking on the return which will be achieved simply by updating to C# 11. A few though will want to keep their current semantics of not tracking the return value because their entire intent is to be unsafe. The most notable examples are `MemoryMarshal.CreateSpan` and `MemoryMarshal.CreateReadOnlySpan`. This will be achieved by marking the parameters as `scoped`.

That means the runtime needs an established pattern for unsafely removing `scoped` from a parameter:

1. `Unsafe.AsRef<T>(in T value)` could expand its existing purpose by changing to `scoped in T value`. This would allow it to both remove `in` and `scoped` from parameters. It then becomes the universal "remove ref safety" method
2. Introduce a new method whose entire purpose is to remove `scoped`: `ref T Unsafe.AsUnscoped<T>(scoped in T value)`. This removes `in` as well because if it did not then callers still need a combination of method calls to "remove ref safety" at which point the existing solution is likely sufficient.

### Unscoped this by default?
The design only has two locations which are `scoped` by default: 

- `this` is `scoped ref` 
- `out` is `scoped ref`

The decision on `out` is to significantly reduce the compat burden of `ref` fields and at the same time is a more natural default. It lets developers actually think of `out` as data flowing outward only where as if it's `ref` then the rules must consider data flowing in both directions. This leads to significant developer confusion.

The decision on `this` is undesirable because it means a `struct` cannot return a field by `ref`. This is an important scenario to high perf developers and the `[UnscopedRef]` attribute was added essentially for this one scenario.

Keywords have a high bar and adding it for a single scenario is suspect. As such thought was given to whether we could avoid this keyword at all by making `this` simply `ref` by default and not `scoped ref`. All members that need `this` to be `scoped ref` could do so by marking the method `scoped` (as a method can be marked `readonly` to create a `readonly ref` today).

On a normal `struct` this is mostly a positive change as it only introduces compat issues when a member has a `ref` return. There are **very** few of these methods and a tool could spot these and convert them to `scoped` members quickly. 

On a `ref struct` this change introduces significantly bigger compat issues. Consider the following:

```c#
ref struct Sneaky
{
    int Field;
    ref int RefField;

    public void SelfAssign()
    {
        // This pattern of ref reassign to fields on this inside instance methods would now
        // completely legal.
        RefField = ref Field;
    }

    static Sneaky UseExample()
    {
        Sneaky local = default;

        // Error: this is illegal, and must be illegal, by our existing rules as the 
        // ref-safe-context of local is now an input into method arguments must match. 
        local.SelfAssign();

        // This would be dangerous as local now has a dangerous `ref` but the above 
        // prevents us from getting here.
        return local;
    }
}
```

Essentially it would mean all instance method invocations on *mutable* `ref struct` locals would be illegal unless the local was further marked as `scoped`. The rules have to consider the case where fields were ref reassigned to other fields in `this`. A `readonly ref struct` doesn't have this problem because the `readonly` nature prevents ref reassignment. Still this would be a significant back compat breaking change as it would impact virtually every existing mutable `ref struct`. 

A `readonly ref struct` though is still problematic once we expand to having `ref` fields to `ref struct`. It allows for the same basic problem by just moving the capture into the value of the `ref` field: 

```c#
readonly ref struct ReadOnlySneaky
{
    readonly int Field;
    readonly ref ReadOnlySpan<int> Span;

    public void SelfAssign()
    {
        // Instance method captures a ref to itself
        Span = new ReadOnlySpan<int>(ref Field, 1);
    }
}
```

Some thought was given to the idea of having `this` have different defaults based on the type of `struct` or member. For example:

 - `this` as `ref`: `struct`, `readonly ref struct` or `readonly member`
 - `this` as `scoped ref`: `ref struct` or `readonly ref struct` with `ref` field to `ref struct`

 This minimizes compat breaks and maximizes flexibility but at the cost of complicating the story for customers. It also doesn't fully solve the problem because future features, like safe `fixed` buffers, require that a mutable `ref struct` have `ref` returns for fields which don't work by this design alone as it would fall into the `scoped ref` category. 

**Decision** Keep `this` as `scoped ref`. That means the preceding sneaky examples produce compiler errors.

### ref fields to ref struct
This feature opens up a new set of ref safe context rules because it allows for a `ref` field to refer to a `ref struct`. This generic nature of `ByReference<T>` meant that up until now the runtime could not have such a construct. As a result all of our rules are written under the assumption this is not possible. The `ref` field feature is largely not about making new rules but codifying the existing rules in our system. Allowing `ref` fields to `ref struct` requires us to codify new rules because there are several new scenarios to consider.

The first is that a `readonly ref` is now capable of storing `ref` state. For example:

```c#
readonly ref struct Container
{
    readonly ref Span<int> Span;

    void Store(Span<int> span)
    {
        Span = span;
    }
}
```

This means when thinking about method arguments must match rules we must consider `readonly ref T` is potential method output when `T` potentially has a `ref` field to a `ref struct`.

The second issue is language must consider a new type of safe context: *ref-field-safe-context*. All `ref struct` which transitively contain a `ref` field have another escape scope representing the value(s) in the `ref` field(s). In the case of multiple `ref` fields they can be collectively tracked as a single value. The default value for this for parameters is *caller-context*. 

```c#
ref struct Nested
{
    ref Span<int> Span;
}

Span<int> M(ref Nested nested) => nested.Span;
```

This value is not related to the *safe-context* of the container; that is as the container context gets smaller it has no impact on the *ref-field-safe-context* of the `ref` field values. Further the *ref-field-safe-context* can never be smaller than the *safe-context* of the container.

```c#
ref struct Nested
{
    ref Span<int> Span;
}

void M(ref Nested nested)
{
    scoped ref Nested refLocal = ref nested;

    // the ref-field-safe-context of local is still *caller-context* which means the following
    // is illegal
    refLocal.Span = stackalloc int[42];

    scoped Nested valLocal = nested;

    // the ref-field-safe-context of local is still *caller-context* which means the following
    // is still illegal
    valLocal.Span = stackalloc int[42];
}
```

This *ref-field-safe-context* has essentially always existed. Up until now `ref` fields could only point to normal `struct` hence it was trivially collapsed to *caller-context*.  To support `ref` fields to `ref struct` our existing rules need to be updated to take into account this new *ref-safe-context*.

Third the rules for ref reassignment need to be updated to ensure that we don't violate *ref-field-context* for the values. Essentially for `x.e1 = ref e2` where the type of `e1` is a `ref struct` the *ref-field-safe-context* must be equal. 

These problems are very solvable. The compiler team has sketched out a few versions of these rules and they largely fall out from our existing analysis. The problem is there is no consuming code for such rules that helps prove out there correctness and usability. This makes us very hesitant to add support because of the fear we'll pick wrong defaults and back the runtime into usability corner when it does take advantage of this. This concern is particularly strong because .NET 8 likely pushes us in this direction with `allow T: ref struct` and `Span<Span<T>>`. The rules would be better written if it's done in conjunction with consumption code.

**Decision** Delay allowing `ref` field to `ref struct` until .NET 8 where we have scenarios that will help drive the rules around these scenarios. This has not been implemented as of .NET 9

### What will make C# 11.0?
The features outlined in this document don't need to be implemented in a single pass. Instead they can be implemented in phases across several language releases in the following buckets:

1. `ref` fields and `scoped`
2. `[UnscopedRef]`
3. `ref` fields to `ref struct`
4. Sunset restricted types
5. fixed sized buffers

What gets implemented in which release is merely a scoping exercise. 

**Decision** Only (1) and (2) made C# 11.0. The rest will be considered in future versions of C#.

## Future Considerations

### Advanced lifetime annotations
The lifetime annotations in this proposal are limited in that they allow developers to change the default escape / don't escape behavior of values. This does add powerful flexibility to our model but it does not radically change the set of relationships that can be expressed. At the core the C# model is still effectively binary: can a value be returned or not?

That allows limited lifetime relationships to be understood. For example a value that can't be returned from a method has a smaller lifetime than one that can be returned from a method. There is no way to describe the lifetime relationship between values that can be returned from a method though. Specifically there is no way to say that one value has a larger lifetime than the other once it's established both can be returned from a method. The next step in our lifetime evolution would be allowing such relationships to be described. 

Other methods such as Rust allow this type of relationship to be expressed and hence can implement more complex `scoped` style operations. Our language could similarly benefit if such a feature were included. At the moment there is no motivating pressure to do this but if there is in the future our `scoped` model could be expanded to include it in a fairly straight forward fashion. 

Every `scoped` could be assigned a named lifetime by adding a generic style argument to the syntax. For example `scoped<'a>` is a value that has lifetime `'a`. Constraints like `where` could then be used to describe the relationships between these lifetimes.

```c#
void M(scoped<'a> ref MyStruct s, scoped<'b> Span<int> span)
  where 'b >= 'a
{
    s.Span = span;
}
```

This method defines two lifetimes `'a` and `'b` and their relationship, specifically that `'b` is greater than `'a`. This allows for the callsite to have more granular rules for how values can be safely passed into methods vs. the more coarse grained rules present today.

## Related Information

### Issues
The following issues are all related to this proposal:

- https://github.com/dotnet/csharplang/issues/1130
- https://github.com/dotnet/csharplang/issues/1147
- https://github.com/dotnet/csharplang/issues/992
- https://github.com/dotnet/csharplang/issues/1314
- https://github.com/dotnet/csharplang/issues/2208
- https://github.com/dotnet/runtime/issues/32060
- https://github.com/dotnet/runtime/issues/61135
- https://github.com/dotnet/csharplang/discussions/78

### Proposals
The following proposals are related to this proposal:

- https://github.com/dotnet/csharplang/blob/725763343ad44a9251b03814e6897d87fe553769/proposals/fixed-sized-buffers.md

### Existing samples

[Utf8JsonReader](https://github.com/dotnet/runtime/blob/f1a7cb3fdd7ffc4ce7d996b7ac6867ffe2c953b9/src/libraries/System.Text.Json/src/System/Text/Json/Reader/Utf8JsonReader.cs#L523-L528)

This particular snippet requires unsafe because it runs into issues with passing around a `Span<T>` which can be stack allocated to an instance method on a `ref struct`. Even though this parameter is not captured the language must assume it is and hence needlessly causes friction here.

[Utf8JsonWriter](https://github.com/dotnet/runtime/blob/f1a7cb3fdd7ffc4ce7d996b7ac6867ffe2c953b9/src/libraries/System.Text.Json/src/System/Text/Json/Writer/Utf8JsonWriter.WriteProperties.String.cs#L122-L127)

This snippet wants to mutate a parameter by escaping elements of the data. The escaped data can be stack allocated for efficiency. Even though the parameter is not escaped the compiler assigns it a *safe-context* of outside the enclosing method because it is a parameter. This means in order to use stack allocation the implementation must use `unsafe` in order to assign back to the parameter after escaping the data.

### Fun Samples

#### ReadOnlySpan\<T>

```c#
public readonly ref struct ReadOnlySpan<T>
{
    readonly ref readonly T _value;
    readonly int _length;

    public ReadOnlySpan(in T value)
    {
        _value = ref value;
        _length = 1;
    }
}
```

#### Frugal list

```c#
struct FrugalList<T>
{
    private T _item0;
    private T _item1;
    private T _item2;

    public int Count = 3;

    public FrugalList(){}

    public ref T this[int index]
    {
        [UnscopedRef] get
        {
            switch (index)
            {
                case 0: return ref _item0;
                case 1: return ref _item1;
                case 2: return ref _item2;
                default: throw null;
            }
        }
    }
}
```

### Examples and Notes 
Below are a set of examples demonstrating how and why the rules work the way they do. Included are several examples showing dangerous behaviors and how the rules prevent them from happening. It's important to keep these in mind when making adjustments to the proposal.

#### Ref reassignment and call sites

Demonstrating how [ref reassignment](#rules-ref-reassignment) and [method invocation](#rules-method-invocation) work together.

```c#
ref struct RS
{
    ref int _refField;

    public ref int Prop => ref _refField;

    public RS(int[] array)
    {
        _refField = ref array[0];
    }

    public RS(ref int i)
    {
        _refField = ref i;
    }

    public RS CreateRS() => ...;

    public ref int M1(RS rs)
    {
        // The call site arguments for Prop contribute here:
        //   - `rs` contributes no ref-safe-context as the corresponding parameter, 
        //      which is `this`, is `scoped ref`
        //   - `rs` contribute safe-context of *caller-context*
        // 
        // This is an lvalue invocation and the arguments contribute only safe-context 
        // values of *caller-context*. That means `local1` has ref-safe-context of 
        // *caller-context*
        ref int local1 = ref rs.Prop;

        // Okay: this is legal because `local` has ref-safe-context of *caller-context*
        return ref local1;

        // The arguments contribute here:
        //   - `this` contributes no ref-safe-context as the corresponding parameter
        //     is `scoped ref`
        //   - `this` contributes safe-context of *caller-context*
        //
        // This is an rvalue invocation and following those rules the safe-context of 
        // `local2` will be *caller-context*
        RS local2 = CreateRS();

        // Okay: this follows the same analysis as `ref rs.Prop` above
        return ref local2.Prop;

        // The arguments contribute here:
        //   - `local3` contributes ref-safe-context of *function-member*
        //   - `local3` contributes safe-context of *caller-context*
        // 
        // This is an rvalue invocation which returns a `ref struct` and following those 
        // rules the safe-context of `local4` will be *function-member*
        int local3 = 42;
        var local4 = new RS(ref local3);

        // Error: 
        // The arguments contribute here:
        //   - `local4` contributes no ref-safe-context as the corresponding parameter
        //     is `scoped ref`
        //   - `local4` contributes safe-context of *function-member*
        // 
        // This is an lvalue invocation and following those rules the ref-safe-context 
        // of the return is *function-member*
        return ref local4.Prop;
    }
}
```

#### Ref reassignment and unsafe escapes
<a name="examples-ref-reassignment-safety"></a>

The reason for the following line in the [ref reassignment rules](#rules-ref-reassignment) may not be obvious at first glance:

> `e1` must have the same *safe-context* as `e2`

This is because the lifetime of the values pointed to by `ref` locations are invariant. The indirection prevents us from allowing any kind of variance here, even to narrower lifetimes. If narrowing is allowed then it opens up the following unsafe code:

```csharp
void Example(ref Span<int> p)
{
    Span<int> local = stackalloc int[42];
    ref Span<int> refLocal = ref local;

    // Error:
    // The safe-context of refLocal is narrower than p. For a non-ref reassignment 
    // this would be allowed as its safe to assign wider lifetimes to narrower ones.
    // In the case of ref reassignment though this rule prevents it as the 
    // safe-context values are different.
    refLocal = ref p;

    // If it were allowed this would be legal as the safe-context of refLocal
    // is *caller-context* and that is satisfied by stackalloc. At the same time
    // it would be assigning through p and escaping the stackalloc to the calling
    // method
    // 
    // This is equivalent of saying p = stackalloc int[13]!!! 
    refLocal = stackalloc int[13];
}
```

For a `ref` to non `ref struct` this rule is trivially satisfied as the values all have the same *safe-context*. This rule really only comes into play when the value is a `ref struct`. 

This behavior of `ref` will also be important in a future where we allow `ref` fields to `ref struct`. 

#### scoped locals
<a name="examples-scoped-locals"></a>

The use of `scoped` on locals will be particularly helpful to code patterns which conditionally assign values with different *safe-context* to locals. It means code no longer needs to rely on initialization tricks like `= stackalloc byte[0]` to define a local *safe-context* but now can simply use `scoped`. 

```c#
// Old way 
// Span<byte> span = stackalloc byte[0];
// New way 
scoped Span<byte> span;
int len = ...;
if (len < MaxStackLen)
{
    span = stackalloc byte[len];
}
else
{
    span = new byte[len];
}
```

This pattern comes up frequently in low level code. When the `ref struct` involved is `Span<T>` the above trick can be used. It is not applicable to other `ref struct` types though and can result in low level code needing to resort to `unsafe` to work around the inability to properly specify the lifetime. 

#### scoped parameter values
<a name="examples-method-arguments-must-match"></a>

One source of repeated friction in low level code is the default escape for parameters is permissive. They are *safe-context* to the *caller-context*. This is a sensible default because it lines up with the coding patterns of .NET as a whole. In low level code though there is a larger use of  `ref struct` and this default can cause friction with other parts of the ref safe context rules.

The main friction point occurs because of the [method arguments must match](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md#method-arguments-must-match) rule. This rule most commonly comes into play with instance methods on `ref struct` where at least one parameter is also a `ref struct`. This is a common pattern in low level code where `ref struct` types commonly leverage `Span<T>` parameters in their methods. For example it will occur on any writer style `ref struct` that uses `Span<T>` to pass around buffers.

This rule exists to prevent scenarios like the following:

```c#
ref struct RS
{
    Span<int> _field;
    void Set(Span<int> p)
    {
        _field = p;
    }

    static void DangerousCode(ref RS p)
    {
        Span<int> span = stackalloc int[] { 42 };

        // Error: if allowed this would let the method return a reference to 
        // the stack
        p.Set(span);
    }
}
```

Essentially this rule exists because the language must assume that all inputs to a method escape to their maximum allowed *safe-context*. When there are `ref` or `out` parameters, including the receivers, it's possible for the inputs to escape as fields of those `ref` values (as happens in `RS.Set` above).

In practice though there are many such methods which pass `ref struct` as parameters that never intend to capture them in output. It is just a value that is used within the current method. For example:

```c#
ref struct JsonReader
{
    Span<char> _buffer;
    int _position;

    internal bool TextEquals(ReadOnlySpan<char> text)
    {
        var current = _buffer.Slice(_position, text.Length);
        return current == text;
    }
}

class C
{
    static void M(ref JsonReader reader)
    {
        Span<char> span = stackalloc char[4];
        span[0] = 'd';
        span[1] = 'o';
        span[2] = 'g';

        // Error: The safe-context of `span` is function-member 
        // while `reader` is outside function-member hence this fails
        // by the above rule.
        if (reader.TextEquals(span))
        {
            ...
        }
    }
}
```

In order to work around this low level code will resort to `unsafe` tricks to lie to the compiler about the lifetime of their `ref struct`. This significantly reduces the value proposition of `ref struct` as they are meant to be a means to avoid `unsafe` while continuing to write high performance code.

This is where `scoped` is an effective tool on `ref struct` parameters because it removes them from consideration as being returned from the method according to the updated [method arguments must match rule](#rules-method-arguments-must-match). A `ref struct` parameter which is consumed, but never returned, can be labeled as `scoped` to make call sites more flexible. 

```c#
ref struct JsonReader
{
    Span<char> _buffer;
    int _position;

    internal bool TextEquals(scoped ReadOnlySpan<char> text)
    {
        var current = _buffer.Slice(_position, text.Length);
        return current == text;
    }
}

class C
{
    static void M(ref JsonReader reader)
    {
        Span<char> span = stackalloc char[4];
        span[0] = 'd';
        span[1] = 'o';
        span[2] = 'g';

        // Okay: the compiler never considers `span` as capturable here hence it doesn't
        // contribute to the method arguments must match rule
        if (reader.TextEquals(span))
        {
            ...
        }
    }
}
```

#### Preventing tricky ref assignment from readonly mutation
<a name="tricky-ref-assignment"></a>
When a `ref` is taken to a `readonly` field in a constructor or `init` member the type is `ref` not `ref readonly`. This is a long standing behavior that allows for code like the following:

```c#
struct S
{
    readonly int i; 

    public S(string s)
    {
        M(ref i);
    }

    static void M(ref int i) { }
}
```

That does pose a potential problem though if such a `ref` were able to be stored into a `ref` field on the same type. It would allow for direct mutation of a `readonly struct` from an instance member:

```c#
readonly ref struct S
{ 
    readonly int i; 
    readonly ref int r; 
    public S()
    {
        i = 0;
        // Error: `i` has a narrower scope than `r`
        r = ref i;
    }

    public void Oops()
    {
        r++;
    }
}
```

The proposal prevents this though because it violates the ref safe context rules. Consider the following:

- The *ref-safe-context* of `this` is *function-member* and *safe-context* is *caller-context*. These are both standard for `this` in a `struct` member.
- The *ref-safe-context* of `i` is *function-member*. This falls out from the [field lifetimes rules](#rules-field-lifetimes). Specifically rule 4.

At that point the line `r = ref i` is illegal by [ref reassignment rules](#rules-ref-reassignment). 

These rules were not intended to prevent this behavior but do so as a side effect. It's important to keep this in mind for any future rule update to evaluate the impact to scenarios like this.

#### Silly cyclic assignment
<a name="cyclic-assignment"></a>
One aspect this design struggled with is how freely a `ref` can be returned from a method. Allowing all `ref` to be returned as freely as normal values is likely what most developers intuitively expect. However it allows for pathological scenarios that the compiler must consider when calculating ref safety. Consider the following: 

```c#
ref struct S
{
    int field;
    ref int refField;

    static void SelfAssign(ref S s)
    {
        // Error: s.field can only escape the current method through a return statement
        s.refField = ref s.field;
    }
}
```

This is not a code pattern that we expect any developers to use. Yet when a `ref` can be returned with the same lifetime as a value it is legal under the rules. The compiler must consider all legal cases when evaluating a method call and this leads to such APIs being effectively unusable. 

```c#
void M(ref S s)
{
    ...
}

void Usage()
{
    // safe-context to caller-context
    S local = default; 

    // Error: compiler is forced to assume the worst and concludes a self assignment
    // is possible here and must issue an error.
    M(ref local);
}
```

To make these APIs usable the compiler ensures that the `ref` lifetime for a `ref` parameter is smaller than lifetime of any references in the associated parameter value. This is the rationale for having *ref-safe-context* for `ref` to `ref struct` be *return-only* and `out` be *caller-context*. That prevents cyclic assignment because of the difference in lifetimes.

Note that `[UnscopedRef]` [promotes](#rules-unscoped) the *ref-safe-context* of any `ref` to `ref struct` values to *caller-context*
and hence it allows for cyclic assignment and forces a viral use of `[UnscopedRef]` up the call chain:

```c#
S F()
{
    S local = new();
    // Error: self assignment possible inside `S.M`.
    S.M(ref local);
    return local;
}

ref struct S
{
    int field;
    ref int refField;

    public static void M([UnscopedRef] ref S s)
    {
        // Allowed: s has both safe-context and ref-safe-context of caller-context
        s.refField = ref s.field;
    }
}
```

Similarly `[UnscopedRef] out` allows a cyclic assignment because the parameter has both *safe-context* and *ref-safe-context* of *return-only*.

Promoting `[UnscopedRef] ref` to *caller-context* is useful when the type is *not* a `ref struct`
(note that we want to keep the rules simple so they don't distinguish between refs to ref vs non-ref structs):

```c#
int x = 1;
F(ref x).RefField = 2;
Console.WriteLine(x); // prints 2

static S F([UnscopedRef] ref int x)
{
    S local = new();
    local.M(ref x);
    return local;
}

ref struct S
{
    public ref int RefField;

    public void M([UnscopedRef] ref int data)
    {
        RefField = ref data;
    }
}
```

In terms of advanced annotations the `[UnscopedRef]` design creates the following:

```
ref struct S { }

// C# code
S Create1(ref S p)
S Create2([UnscopedRef] ref S p)

// Annotation equivalent
scoped<'b> S Create1(scoped<'a> ref scoped<'b> S)
scoped<'a> S Create2(scoped<'a> ref scoped<'b> S)
  where 'b >= 'a
```

#### readonly cannot be deep through ref fields
<a name="reason-readonly-shallow"></a>

Consider the below code sample:

```c#
ref struct S
{
    ref int Field;

    readonly void Method()
    {
        // Legal or illegal?
        Field = 42;
    }
}
```

When designing the rules for `ref` fields on `readonly` instances in a vacuum the rules can be validly designed such that the above is legal or illegal. Essentially `readonly` can validly be deep through a `ref` field or it can apply only to the `ref`. Applying only to the `ref` prevents ref reassignment but allows normal assignment which changes the referred to value.

This design does not exist in a vacuum though, it is designing rules for types that already effectively have `ref` fields. The most prominent of which, `Span<T>`, already has a strong dependency on `readonly` not being deep here. Its primary scenario is the ability to assign to the `ref` field through a `readonly` instance. 

```c#
readonly ref struct SpanOfOne
{
    readonly ref int Field;

    public ref int this[int index]
    {
        get
        {
            if (index != 1)
                throw new Exception();
            return ref Field;
        }
    }
}
```

This means we must choose the shallow interpretation of `readonly`.

#### Modeling constructors 
One subtle design question is: How are constructors bodies modeled for ref safety? Essentially how is the following constructor analyzed? 

```c#
ref struct S
{
    ref int field;

    public S(ref int f)
    {
        field = ref f;
    }
}
```

There are roughly two approaches:

1. Model as a `static` method where `this` is a local where its *safe-context* is *caller-context*
2. Model as a `static` method where `this` is an `out` parameter. 

Further a constructor must meet the following invariants:

1. Ensure that `ref` parameters can be captured as `ref` fields. 
2. Ensure that `ref` to fields of `this` cannot be escaped through `ref` parameters. That would violate [tricky ref assignment](#tricky-ref-assignment). 

The intent is to pick the form that satisfies our invariants without introduction of any special rules for constructors. Given that the best model for constructors is viewing `this` as an `out` parameter. The *return only* nature of the `out` allows us to satisfy all the invariants above without any special casing: 

```c#
public static void ctor(out S @this, ref int f)
{
    // The ref-safe-context of `ref f` is *return-only* which is also the 
    // safe-context of `this.field` hence this assignment is allowed
    @this.field = ref f;
}
```

#### Method arguments must match
The method arguments must match rule is a common source of confusion for developers. It's a rule which has a number of special cases that are hard to understand unless you are familiar with the reasoning behind the rule. For the sake of better understanding the reasons for the rule we will simplify *ref-safe-context* and *safe-context* to simply *context*. 

Methods can pretty liberally return state passed to them as parameters. Essentially any reachable state which is unscoped can be returned (including returning by `ref`). This can be returned directly through a `return` statement or indirectly by assigning into a `ref` value. 

Direct returns don't pose much problems for ref safety. The compiler simply needs to look at all the returnable inputs to a method and then it effectively restricts the return value to be the minimum *context* of the input. That return value then goes through normal processing.

Indirect returns pose a significant problem because all `ref` are both an input and output to the method. These outputs already have a known *context*. The compiler can't infer new ones, it has to consider them at their current level. That means the compiler has to look at every single `ref` which is assignable in the called method, evaluate it's *context*, and then verify no returnable input to the method has a smaller *context* than that `ref`. If any such case exists then the method call must be illegal because it could violate `ref` safety. 

Method arguments must match is the process by which the compiler asserts this safety check.

A different way to evaluate this which is often easier for developers to consider is to do the following exercise: 

1. Look at the method definition identify all places where state can be indirectly returned:
    a. Mutable `ref` parameters pointing to `ref struct`
    b. Mutable `ref` parameters with ref assignable `ref` fields
    c. Assignable `ref` params or `ref` fields pointing to `ref struct` (consider recursively)
2. Look at the call site
    a. Identify the contexts that line up with the locations identified above
    b. Identify the contexts of all inputs to the method that are returnable (don't line up with `scoped` parameters)

If any value in 2.b is smaller than 2.a then the method call must be illegal. Let's look at a few examples to illustrate the rules:

```c#
ref struct R { }

class Program
{
    static void F0(ref R a, scoped ref R b) => throw null;

    static void F1(ref R x, scoped R y)
    {
        F0(ref x, ref y);
    }
}
```

Looking at the call to `F0` lets go through (1) and (2). The parameters with potential for indirect return are `a` and `b` as both can be directly assigned. The arguments which line up to those parameters are:

- `a` which maps to `x` that has *context* of *caller-context*
- `b` which maps to `y` that has with *context* of *function-member*

The set of returnable input to the method are

- `x` with *escape-scope* of *caller-context*
- `ref x` with *escape-scope* of *caller-context*
- `y` with *escape-scope* of *function-member*

The value `ref y` is not returnable since it maps to a `scoped ref` hence it is not considered an input. But given that there is at least one input with a smaller *escape scope* (`y` argument) than one of the outputs (`x` argument) the method call is illegal. 

A different variation is the following:

```c#
ref struct R { }

class Program
{
    static void F0(ref R a, ref int b) => throw null;

    static void F1(ref R x)
    {
        int y = 42;
        F0(ref x, ref y);
    }
}
```

Again the parameters with potential for indirect return are `a` and `b` as both can be directly assigned. But `b` can be excluded because it does not point to a `ref struct` hence cannot be used to store `ref` state. Thus we have:

- `a` which maps to `x` that has *context* of *caller-context*

The set of returnable input to the method are:

- `x` with *context* of *caller-context*
- `ref x` with *context* of *caller-context*
- `ref y` with *context* of *function-member*

Given that there is at least one input with a smaller *escape scope* (`ref y` argument) than one of the outputs (`x` argument) the method call is illegal. 

This is the logic that the method arguments must match rule is trying to encompass. It goes further as it considers both `scoped` as a way to remove inputs from consideration and `readonly` as a way to remove `ref` as an output (can't assign into a `readonly ref` so it can't be a source of output). These special cases do add complexity to the rules but it's done so for the benefit of the developer. The compiler seeks to remove all inputs and outputs it knows can't contribute to the result to give developers maximum flexibility when calling a member. Much like overload resolution it's worth the effort to make our rules more complex when it creates more flexibility for consumers.

#### Examples of inferred *safe-context* of declaration expressions
<a id="examples-of-inferred-safe-to-escape-of-declaration-expressions"></a>

Related to [Infer *safe-context* of declaration expressions](#infer-safe-to-escape-of-declaration-expressions).
    
```cs
ref struct RS
{
    public RS(ref int x) { } // assumed to be able to capture 'x'

    static void M0(RS input, out RS output) => output = input;

    static void M1()
    {
        var i = 0;
        var rs1 = new RS(ref i); // safe-context of 'rs1' is function-member
        M0(rs1, out var rs2); // safe-context of 'rs2' is function-member
    }

    static void M2(RS rs1)
    {
        M0(rs1, out var rs2); // safe-context of 'rs2' is function-member
    }

    static void M3(RS rs1)
    {
        M0(rs1, out scoped var rs2); // 'scoped' modifier forces safe-context of 'rs2' to the current local context (function-member or narrower).
    }
}

```

Note that the local context which results from the `scoped` modifier is the narrowest which could possibly be used for the variable--to be any narrower would mean the expression refers to variables which are only declared in a narrower context than the expression.
