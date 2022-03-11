Low Level Struct Improvements
=====

## Summary
This proposal is an aggregation of several different proposals for `struct` performance improvements: `ref` fields and the ability to override lifetime defaults. The goal being a design which takes into account the various proposals to create a single overarching feature set for low level `struct` improvements.

## Motivation
Earlier versions of C# added a number of low level performance features to the language: `ref` returns, `ref struct`, function pointers, etc. ... These enabled .NET developers to create write highly performant code while continuing to leverage the C# language rules for type and memory safety.  It also allowed the creation of fundamental performance types in the .NET libraries like `Span<T>`.

As these features have gained traction in the .NET ecosystem developers, both internal and external, have been providing us with information on remaining friction points in the ecosystem. Places where they still need to drop to `unsafe` code to get their work done, or require the runtime to special case types like `Span<T>`. 

Today `Span<T>` is accomplished by using the `internal` type `ByReference<T>` which the runtime effectively treats as a `ref` field. This provides the benefit of `ref` fields but with the downside that the language provides no safety verification for it, as it does for other uses of `ref`. Further only dotnet/runtime can use this type as it's `internal`, so 3rd parties can not design their own primitives based on `ref` fields. Part of the [motivation for this work](https://github.com/dotnet/runtime/issues/32060) is to remove `ByReference<T>` and use proper `ref` fields in all code bases. 

This proposal plans to address these issues by building on top of our existing low level features. Specifically it aims to:

- Allow `ref struct` types to declare `ref` fields.
- Allow the runtime to fully define `Span<T>` using the C# type system and remove special case type like `ByReference<T>`
- Allow `struct` types to return `ref` to their fields.
- Allow runtime to remove `unsafe` uses caused by limitations of lifetime defaults
- Allow the declaration of safe `fixed` buffers for managed and unmanaged types in `struct`

## Compat Considerations
The biggest challenge in the proposals presented here is they must be compatible with our existing [span safety rules](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md). That is the language can't introduce new lifetime safety errors for existing code patterns and that presents a challenge with several of the proposed features. It's important to understand these challenges as they significantly impact the design of the features.

To understand the challenges here let's first consider how `Span<T>` will look once `ref` fields are supported.

<a name="new-span"></a>

```c#
readonly ref struct Span<T>
{
    readonly ref T _field;
    int _length;

    // This constructor does not exist today but will be added as a part 
    // of changing Span<T> to have ref fields. It is a convenient, and
    // safe, way to create a length one span over a stack value that today 
    // requires unsafe code.
    [RefFieldEscapes]
    public Span(ref T value)
    {
        _field = ref value;
        _length = 1;
    }
}
```

The first challenge is the span safety rules make a [hard assumption](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md#span-constructor) that `Span<T>` has no such constructor. By extension this also introduced a hard assumption that `ref` fields do not exist. This resulted in a significant simplification of the rules but it allows for a number of patterns that make introducing `ref` fields significantly more difficult. 

Consider the following method signature:

```c#
Span<T> CreateSpan<T>(ref T parameter)
{
    // The implementation of this method is irrelevant when considering the lifetime of the 
    // returned Span<T>. The rules disallow capture of `parameter` hence the return of 
    // CreateSpan<T> is always safe-to-escape to the calling method
}
```

By the existing span safety rules the return of an invocation of this method always has a //*safe-to-escape* value of *calling method*. That is because in the span safety document `ref` fields do not exist and hence there is no way for the return to capture `parameter` hence there is no constraint on the return.  The implementation of the method is completely irrelevant here, which is why it's omitted from the sample, because there is simply no way for the `ref` to be captured. This means no matter how this method is called the return is *safe-to-escape* to the *calling method*.

This is not a hypothetical pattern, there are APIs today in .NET which have this basic structure. Very likely in customer code as well. One such example is [AsnDecoder.ReadEnumeratedBytes](https://github.com/dotnet/runtime/blob/3580ba795d92444e99fe5a5bfa4883458a0d4ac5/src/libraries/System.Formats.Asn1/src/System/Formats/Asn1/AsnDecoder.Enumerated.cs#L48-L52). 


```c#
Span<T> CompatExample(ref int p)
{
    // Okay
    int local = 42;
    return CreateSpan<int>(ref local);

    // Okay
    Span<T> span = stackalloc int[1];
    return CreateSpan<int>(ref span[0]);

    // Okay
    int[] array = new int[42];
    return CreatSpan(ref array[0]);
}
```

All of the above `return` statements are legal by our existing rules because the return of `CreateSpan` is always *safe-to-escape* to the *calling method*. The challenge in this proposal, which comes up many times, is they **must** remain legal in the version of the language which implements these features and / or when `Span<T>` moves to using `ref` fields. The above patterns can legally exist today and cannot become errors when moving to a new language version. 

Further the design must ensure that once `ref` fields exist that implementations of `CreateSpan<T>` cannot suddenly begin capture the input arguments by `ref`. For example: 

<a name="new-span-challenges"></a>

```c#
Span<int> UseSpan()
{
    // This code is 100% legal and safe in C# today. The span safety rules and .NET runtime 
    // ensure that CreateSpan **cannot** capture the parameter in the returned Span<T>. Hence
    // the result is always returnable. 
    // 
    // The rules created for ref fields must ensure this remains legal else it becomes a 
    // breaking change when moving to the new compiler.
    int local = 42;
    Span<int> span = CreateSpan(ref local);
    return span;
}

Span<T> CreateSpan<T>(ref T parameter)
{
    // This will create a length one Span<T> over the value referred to by `parameter`.
    // Effectively `span[0]` and `parameter` refer to the same location. 
    Span<T> span = new Span<T>(ref parameter);

    // Error: this must be illegal because our existing span safety rules assume the returned
    // Span<T> cannot capture `parameter`. Existing code could be using `CreateSpan(ref someLocal)`
    // and passing the returned Span<T> to the caller. That code is legal today and this 
    // proposal should not introduce new errors when calling CreateSpan<T>.
    //
    // Another way of thinking about this sample is that it can be safely done. It is only
    // returning references to values that live beyond this method call. But our existing
    // rules hide that this could be happening and callers do not account for it.
    return span;
}
```

It will be possible for such methods to exist. Specifically methods which take `ref` parameters, capture them in `ref` fields and return them. But they require a declarative opt-in to let the compiler, and developer, know that it is happening and to adjust the span safety rules accordingly.

It is important to understand these compat considerations before diving too far into the proposal here as they are central to parts of the design.

## Detailed Design 
The rules for `ref struct` safety are defined in the [span safety document](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md). This document will describe the required changes to this document as a result of this proposal. Once accepted as an approved feature these changes will be incorporated into that document.

### Provide ref fields
The language will allow developers to declare `ref` fields inside of a `ref struct`. This can be useful for example when encapsulating large mutable `struct` instances or defining high performance types like `Span<T>` in libraries besides the runtime.

The set of changes to our span safety rules necessary to allow `ref` fields is small and targeted. However this section of the proposal is quite involved. The reason is to make sure the reader understands both the new rules and **why** they were chosen. There are a number of subtle interactions between `ref` fields and our [compat considerations](#compat-considerations) that are easy to miss. This section is meant to call these out and explain how they fit into the chosen rules.

The [new Span<T> definition](#new-span) also reveals several [challenges](#new-span-challenges) that must be resolved for the lifetime of types that contain `ref` fields. They must both take into account the lifetime of captured values as well as the compat considerations.

The rules we define for `ref` fields must ensure the `Span<T>` constructor properly restricts the *safe-to-escape* scope of constructed objects in the cases it captures `ref` state. At the same time it must ensure that we don't break the existing consumption rules for methods like `CreateSpan<T>`. 

<a name="ref-field-escapes"></a>

To accomplish this two new annotations will be introduced to help control how arguments influence lifetime of method calls: `[RefFieldEscapes]` and `[DoesNotEscape]`. The annotation `[RefFieldEscapes]` when applied to a `ref` parameter signifies that it can be captured as a `ref` field in a returned `ref struct`.

The `[DoesNotEscape]` annotation is not necessary for `ref` fields but more for reducing friction when dealing with specific classes of methods. It will be discussed in detail [later on](#does-not-escape) but needs to be introduced now as it impacts method invocation rules.

To recognize these annotations the span safety rules for method invocation need to be updated. The first change is recognizing the impact the annotations have on arguments. For a given argument `a` that is passed to `ref` parameter `p`: 

> 1. If `p` is `[RefFieldEscapes] ref` or `[RefFieldEscapes] in` and `a` is 
>     1. A `[RefFieldEscapes]` parameter it contributes *safe-to-escape* to *calling method*
>     2. A known heap location it contributes *safe-to-escape* to *calling method*
>     3. It contributes *safe-to-escape* to *current method*
> 2. If `p` is `[DoesNotEscape]` then `a` does not contribute *safe-to-escape* when considering arguments.

This change allows us to keep our existing rules for rvalue method invocation:

> An rvalue resulting from a method invocation e1.M(e2, ...) is *safe-to-escape* from the smallest of the following scopes:
> * The entire enclosing method
> * The *safe-to-escape* of all argument expressions (including the receiver)

The rules for `ref` field assignment will be discussed in detail [later on](#ref-field-reassignment) the doc. For now readers only need to know constructors can initialize `ref` fields with parameters marked as `[RefFieldEscapes]` or known heap locations.

Let's examine these rules in the context of samples to better understand their impact and how they maintain the required compat considerations.

```c#
ref struct RS
{
    ref int _field;

    public RS(int[] array, int index)
    {
        // Okay: as fields are initialized with known heap values. Caller does not need a visible
        // change as it does not provide values that participate in this capture.
        _field = ref array[index];
    }

    public RS([RefFieldEscapes] ref int value)
    {
        // Okay: the parameter `value` is annotated with [RefFieldEscapes] which allows for 
        // it to be assigned to a ref field. This also makes the capture visible to the 
        // caller
        _field = ref value;
    }

    public RS(ref (int, int) tuple)
    {
        // Error: even though the ref-safe-to-escape of `tuple.Item1` is to the calling method, the 
        // parameter is not annotated with [RefFieldEscapes] hence it cannot be ref reassigned to a 
        // ref field
        _field = ref tuple.Item1;
    }

    static RS CreateRS([RefFieldEscapes] ref int p1, ref int p2)
    {
        // Okay: The RS argument `p1' lines up with a [RefFieldEscapes] argument
        // but it is also a [RefFieldEscapes] parameter hence it contributes a safe-to-escape of 
        // calling method (rule 1.1 above)
        return new RS(ref p1);

        // Okay: This is a known heap value hence it is safe-to-escape to the calling
        // method (rule 1.2 above)
        return new RS(new int[1]);

        // Error: This is the same analysis as above but in this case `p2` is not 
        // [RefFieldEscapes] hence it contributes safe-to-escape of current method
        // (rule 1.3 above)
        return new RS(ref p2);

        // Error: This is the same analysis as above and once again by rule 1.3 is 
        // only safe-to-escape to the current method
        int local = 42;
        return new RS(ref local);
    }
}
```

The samples here have the same patterns as the [compat considerations](#compat-considerations) above. This means it will allow the introduction of `ref` fields without breaking existing code.

This proposal also requires that the span safety rules for field lifetimes be expanded as the rules today simply don't explicitly account for `ref` fields. It's important to note that our expansion of the rules here is not defining new behavior but rather accounting for behavior that has long existed. The safety rules around using `ref struct` fully acknowledge and account for the possibility that `ref struct` will contain `ref` fields and that `ref` fields will be exposed to consumers. The most prominent example of this is the indexer on `Span<T>`:

``` cs
readonly ref struct Span<T>
{
    public ref T this[int index] => ...; 
}
```

This directly exposes the `ref` state inside `Span<T>` and the span safety rules account for this. Whether that was implemented as `ByReference<T>` or `ref` fields is immaterial to those rules. This is true even though normal fields cannot be returned by `ref`. Effectively the rules have **always** allowed for the following: 

```c#
ref struct S
{
    int _field1;
    ref int _field2;

    internal ref int Prop1 => ref _field1;  // Error: can't escape `this` by ref
    internal ref int Prop2 => ref _field2;  // Okay 
}
```

As a part of allowing `ref` fields though we must define their rules such that they fit into the existing consumption rules for `ref struct`. Specifically this must account for the fact that it's legal *today* for a `ref struct` to return its `ref` state as `ref` to the consumer. 

To understand the proposed changes it's helpful to first review the existing rules for method invocation around *ref-safe-to-escape* and how they account for a `ref struct` exposing `ref` state today:

> An lvalue resulting from a ref-returning method invocation e1.M(e2, ...) is *ref-safe-to-escape* the smallest of the following scopes:
> 1. The entire enclosing method
> 2. The *ref-safe-to-escape* of all ref and out argument expressions (excluding the receiver)
> 3. For each in parameter of the method, if there is a corresponding expression that is an lvalue, its *ref-safe-to-escape*, otherwise the nearest enclosing scope
> 4. the *safe-to-escape* of all argument expressions (including the receiver)

The fourth item provides the critical safety point around a `ref struct` exposing `ref` state to callers. When the `ref` state stored in a `ref struct` refers to the stack then the *safe-to-escape* scope for that `ref struct` will be at most the scope which defines the state being referred to. Hence limiting the *ref-safe-to-escape* of invocations of a `ref struct` to the *safe-to-escape* scope of the receiver ensures the lifetimes are correct.

Consider as an example the indexer on `Span<T>` which is returning `ref` fields by `ref` today. The fourth item here is what provides the safety here:

```c#
ref int Examples()
{
    Span<int> s1 = stackalloc int[5];

    // Error: illegal because the *safe-to-escape* scope of `s1` is the current
    // method scope hence that limits the *ref-safe-to-escape" to the current
    // method scope as well.
    return ref s1[0];

    // Okay: legal because the *safe-to-escape* scope of `s2` is outside
    // the current method scope hence the *ref-safe-to-escape* is as well
    Span<int> s2 = default;
    return ref s2[0];
}
```

To account for `ref` fields the *ref-safe-to-escape* rules for fields will be adjusted to the following:

> An lvalue designating a reference to a field, e.F, is *ref-safe-to-escape* (by reference) as follows:
> 1. If `F` is a `ref` field and `e` is `this`, it is *ref-safe-to-escape* from the enclosing method.
> 2. Else if `F` is a `ref` field its *ref-safe-to-escape* scope is the *safe-to-escape* scope of `e`.
> 3. Else if `e` is of a reference type, it is *ref-safe-to-escape* from the enclosing method.
> 4. Else its *ref-safe-to-escape* is taken from the *ref-safe-to-escape* of `e`.

This explicitly allows for `ref` fields being returned as `ref` from a `ref struct` but not normal fields (that will be covered later). 

```c#
ref struct RS
{
    ref int _refField;
    int _field;

    // Okay: this falls into bullet one above. 
    public ref int Prop1 => ref _refField;

    // Error: This is bullet four above and the *ref-safe-to-escape* of `this`
    // in a `struct` is the current method scope.
    public ref int Prop2 => ref _field;

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
        ref int local1 = ref rs.Prop1;

        // Okay: this falls into bullet two above and the *safe-to-escape* of
        // `rs` is outside the current method scope. Hence the *ref-safe-to-escape*
        // of `local1` is outside the current method scope.
        return ref local;

        // Okay: this falls into bullet two above and the *safe-to-escape* of
        // `rs` is outside the current method scope. Hence the *ref-safe-to-escape*
        // of `local1` is outside the current method scope.
        //
        // In fact in this scenario you can guarantee that the value returned
        // from Prop1 must exist on the heap. 
        RS local2 = CreateRS();
        return ref local2.Prop1;

        // Error: the *safe-to-escape* of `local4` here is the current method 
        // scope by the revised constructor rules. This falls into bullet two 
        // above and hence based on that allowed scope.
        int local3 = 42;
        var local4 = new RS(ref local3);
        return ref local4.Prop1;

    }
}
```

<a name="ref-field-reassignment"></a>

The rules for `ref` reassignment will be adjusted to account for `ref` fields as follows:

> For a ref reassignment in the form ...
> 1. `x.e1 = ref e2`: the `e2` must be a `[RefFieldEscapes]` parameter or refer to a known heap location else it is an error
> 2. `e1 = ref e2`: the *ref-safe-to-escape* of `e2` must be at least as wide a scope as the *ref-safe-to-escape* of `e1`.

Examples of these rules in practice:

```c#
ref struct SmallSpan
{
    public ref int _field;

    // Notice once again we're back at the same problem as the original 
    // CreateSpan method: a method returning a ref struct and taking a ref
    // parameter
    SmallSpan TrickyRefAssignment(ref int i)
    {
        // *safe-to-escape* is outside the current method by current rules.
        SmallSpan s = default;

        // The *ref-safe-to-escape* of 'i' is the same as the *safe-to-escape*
        // of 's' hence most assignment rules would allow it.
        s._field = ref i;

        // Error: this must be disallowed for the exact same reasons we can't 
        // return a Span<T> wrapping the parameter: the consumption rules
        // believe such state smuggling cannot exist
        return s;
    }

    SmallSpan SafeRefAssignment()
    {
        int[] array = new int[] { 42, 13 };
        SmallSpan s = default;

        // Okay: the value being assigned here is known to refer to the heap 
        // hence it is allowed by our rules above because it requires no changes
        // to existing method invocation rules (hence preserves compat)
        s._field = ref array[i];

        return s;
    }

    SmallSpan BadUsage()
    {
        // Legal today and must remain legal (and safe)
        int i = 0;
        return TrickyRefAssignment(ref i);
    }
}
```

There are designs choices we could make to allow more flexible `ref` re-assignment of fields. For example it could be allowed in cases where we knew the receiver had a *safe-to-escape* scope that was not outside the current method scope. Further we could provide syntax for making such downward facing values easier to declare: essentially values that have *safe-to-escape* scopes restricted to the current method. Such a design is discussed [here](https://github.com/dotnet/csharplang/discussions/1130)). However extra complexity of such rules do not seem to be worth the limited cases this enables. Should compelling samples come up we can revisit this decision.

This means though that `ref` fields are largely in practice `readonly ref`. The main exceptions being object initializers and when the value is known to refer to the heap.

A `ref` field will be emitted into metadata using the `ELEMENT_TYPE_BYREF` signature. This is no different than how we emit `ref` locals or `ref` arguments. For example `ref int _field` will be emitted as `ELEMENT_TYPE_BYREF ELEMENT_TYPE_I4`. This will require us to update ECMA335 to allow this entry but this should be rather straight forward.

Developers can continue to initialize a `ref struct` with a `ref` field using the `default` expression in which case all declared `ref` fields will have the value `null`. Any attempt to use such fields will result in a `NullReferenceException` being thrown.

```c#
struct S1 
{
    public ref int Value;
}

S1 local = default;
local.Value.ToString(); // throws NullReferenceException
```

While the C# language pretends that a `ref` cannot be `null` this is legal at the runtime level and has well defined semantics. Developers who introduce `ref` fields into their types need to be aware of this possibility and should be **strongly** discouraged from leaking this detail into consuming code. Instead `ref` fields should be validated as non-null using the [runtime helpers](https://github.com/dotnet/runtime/pull/40008) and throwing when an uninitialized `struct` is used incorrectly.

```c#
struct S1 
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

The `ref` fields feature requires runtime support and changes to the ECMA spec to allow the construct. As such these will only be enabled when the corresponding feature flag is set in corelib. The issue tracking the exact API is tracked here 

https://github.com/dotnet/runtime/issues/64165

Detailed Notes:
- A `ref` field can only be declared inside of a `ref struct` 
- A `ref` field cannot be declared `static`
- A `ref` field can only be `ref` assigned 
    - in the constructor of the declaring type
    - when the RHS is known to be a heap location
- The reference assembly generation process must preserve the presence of a `ref` field inside a `ref struct` 
- A `readonly ref struct` must declare its `ref` fields as `readonly ref`
- The span safety rules for constructors, fields and assignment must be updated as outlined in this document.
- The span safety rules need to include the definition of `ref` values that "refer to the heap". 

### Provide lifetime annotations
The [span safety document](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md) assigns escape scopes to locations based on their declaration: parameters are *ref-safe-to-escape* to calling method, `this` in a `struct` is *ref-safe-to-escape* within the current method, etc ... These defaults were chosen to make `Span<T>` and `ref` returns work with the predominant coding patterns in .NET.

While the defaults have allowed broad adoption of `ref struct` within .NET they do create a number of friction points in low level code. For example the inability to return fields of `struct` by `ref` from instance methods. These friction points often force developers to resort to using `unsafe` which de-values our `ref struct` efforts. 

The lifetime defaults for these locations is not fundamental to correctness. For example parameters could default to *ref-safe-to-escape* to within the current method or `this` in a `struct` could be *ref-safe-to-escape* to the calling method in our span safety rules and it would not make `ref struct` usage unsafe. It would simply impact the usability of the resulting feature.

This, and several other friction points, can be removed if the language provides developers a way to invert the defaults by applying attributes to specific locations. The language can recognize these attributes and simply adjust the lifetime calculation for locations when evaluating span safety.

#### RefThisEscapes
One of the most notable friction points is the inability to return fields by `ref` in instance members of a `struct`. This means developers can't create `ref` returning methods / properties and have to resort to exposing fields directly. This reduces the usefulness of `ref` returns in `struct` where it is often the most desired. 

```c#
struct S
{
     int _field;

    // Error: this, and hence _field, can't return by ref
    public ref int Prop => ref _field;
}
```

The [rationale](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md#struct-this-escape) for this default is reasonable but there is nothing inherently wrong with a `struct` escaping `this` by reference, it is simply the default chosen by the span safety rules. 

To fix this the attribute `System.Runtime.CompilerServices.RefThisEscapesAttribute` will be used to specify that `this` is *ref-safe-to-escape* to the calling method. It can be applied to individual methods or properties in `struct`. When applied to `struct` declaration itself it is treated as if it were applied to all methods / properties in the type. 

```c#
// Option 1 
struct S
{
    int _field;

    // The ref-safe-to-escape for this is now the calling method hence this is legal
    [RefThisEscapes]
    public ref int Prop => ref _field;
}

// Option 2 
// The ref-safe-to-escape for this is now the calling method on all members
[RefThisEscapes]
struct S
{
    int _field;

    public ref int Prop => ref _field;
}
```

This will naturally, by the existing rules in the span safety spec, allow for returning transitive fields in addition to direct fields.

```c#
[RefThisEscapes]
struct Child
{
    int _value;
    public ref int Value => ref _value;
}

[RefThisEscapes] 
struct Container
{
    Child _child;

    // In this case the ref-safe-to-escape of `_child` is to the calling method because that is 
    // the value of `this` and fields derive it from their receiver. From there method invocation 
    // rules take over 
    public ref int Value => ref _child.Value;
}
```

This require the following changes to the span safety document: 

- The method invocation rules will include the *ref-safe-to-escape* value of the receiver when it is marked by `[RefThisEscapes]`
- The parameter lifetime rules will change to note that the *ref-safe-to-escape* of `this` is dependent on `[RefThisEscapes]`

Detailed Notes:
- An instance method or property annotated with `[RefThisEscapes]` has *ref-safe-to-escape* of `this` set to the *calling method*
- A `struct` annotated with `[RefThisEscapes]` has the same effect of annotating every instance method and property with `[RefThisEscapes]`
- A member annotated with `[RefThisEscapes]` cannot implement an interface.
- It is an error to use `[RefThisEscapes]` on 
    - Any type other than a `struct` (although it is legal for all variations like `readonly struct`)
    - Any member that is not declared on a `struct`
    - Any `static` member or constructor on a `struct`

#### RefFieldEscapes
Methods that return `ref struct` that capture `ref` parameters as fields must declare that they do so. Otherwise it would violate our [compat requirements](#compat-considerations).  This will be done by annotating such methods with `System.Runtime.CompilerServices.RefFieldEscapesAttribute`. 

This attribute can be applied to methods, constructors and operators. Applying to any other member will be an error. 

The semantics of this attribute, and how it impacts span safety rules, are described [above](#ref-field-escapes)

#### DoesNotEscape
<a name="does-not-escape">
One source of repeated friction in low level code is the default escape for parameters is permissive. They are *safe-to-escape* to the *calling method*. This is a sensible default because it lines up with the coding patterns of .NET as a whole. In low level code though there is a larger use of  `ref struct` and this default can cause friction with other parts of the span safety rules.

The main friction point occurs because of the following constraint around method invocations:

> For a method invocation if there is a ref or out argument of a ref struct type (including the receiver), with safe-to-escape E1, then no argument (including the receiver) may have a narrower safe-to-escape than E1

This rule most commonly comes into play with instance methods on `ref struct` where at least one parameter is also a `ref struct`. This is a common pattern in low level code where `ref struct` types commonly leverage `Span<T>` parameters in their methods. For example it will occur on any writer style `ref struct` that uses `Span<T>` to pass around buffers.

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

Essentially this rule exists because the language must assume that all inputs to a method escape to their maximum allowed scope. When there are `ref` or `out` parameters, including the receivers, it's possible for the inputs to escape as fields of those `ref` values (as happens in `RS.Set` above).

In practice though there are many such methods which pass `ref struct` as parameters that never intend to capture them in output. It is just a value that is used within the current method. For example:

```c#
ref struct JsonReader
{
    Span<char> _buffer;
    int _position;

    internal bool TextEquals(ReadOnySpan<char> text)
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

        // Error: The safe-to-escape of `span` is the current method scope 
        // while `reader` is outside the current method scope hence this fails
        // by the above rule.
        if (reader.TextEquals(span)
        {
            ...
        })
    }
}
```

In order to work around this low level code will resort to `unsafe` tricks to lie to the compiler about the lifetime of their `ref struct`. This significantly reduces the value proposition of `ref struct` as they are meant to be a means to avoid `unsafe` while continuing to write high performance code.

The other place where parameter default escape scope causes friction is when they are re-assigned within a method body. For instance if a method body decides to conditionally apply escaping to input by using stack allocated values. Once again this runs into some friction.

```c#
void WriteData(ReadOnlySpan<char> data)
{
    if (data.Contains(':'))
    {
        Span<char> buffer = stackalloc char[256];
        Escape(data, buffer, out var length);

        // Error: Cannot assign `buffer` to `data` here as the safe-to-escape
        // scope of `buffer` is to the current method scope while `data` is
        // outside the current method scope
        data = buffer.Slice(0, length);
    }

    WriteDataCore(data);
}
```

This pattern is fairly common across .NET code and it works just fine when a `ref struct` is not involved. Once users adopt `ref struct` though it forces them to change their patterns here and often they just resort to `unsafe` to work around the limitations here.

To remove this friction the language will provide the attribute `System.Runtime.CompilerServices.DoesNotEscapeAttribute`. This can be applied to non-ref parameters of methods and when done it changes the *safe-to-escape* scope to the current method. 

```c#
class C
{
    static Span<int> M1(Span<int> p1, [DoesNotEscape] Span<int> p2)
    {
        // Okay: the safe-to-escape here is still outside the enclosing scope
        // of the current method.
        return p1; 

        // Error: the [DoesNotEscape] attribute changes the safe-to-escape*
        // to be limited to the current method scope. Hence it cannot be 
        // returned
        return p2; 

        // Error: `local` has the same safe-to-escape as `p2` hence it cannot
        // be returned.
        Span<int> local = p2;
        return local; 
    }
}
```

This attribute cannot be used on `ref` or `out` parameters. Those always implicitly escape to the calling method (that is how `ref` works). Instead `in` is likely a more appropriate designation for those scenarios.

To account for this change the "Parameters" section of the span safety document will be updated to include the following bullet:

- If the parameter is marked with `[DoesNotEscape]` it is *safe-to-escape* to the *current method*

It's important to note that this will naturally block the ability for such parameters to escape by being stored as fields. Receivers that are passed by `ref`, or `this` on `ref struct`, have a *safe-to-escape* scope outside the current method. Hence assignment from a `[DoesNotEscape]` parameter to a field on such a value fails by existing field assignment rules: the scope of the receiver is greater than the value being assigned.

```c#
ref struct S
{
    Span<int> _field;

    void M1(Span<int> p1, [DoesNotEscape] Span<int> p2)
    {
        // Okay: the *safe-to-escape* here is still outside the enclosing scope
        // of the current method and hence the same as the receiver.
        _field = p1;

        // Error: the [DoesNotEscape] attribute changes the *safe-to-escape* 
        // to be limited to the current method scope. Hence it cannot be 
        // assigned to a receiver that has a *safe-to-escape* scope outside the 
        // current method.
        _field = p2;
    }
}
```

Given that parameters are restricted in this way we will also update the "Method Invocation" section to relax its rules. In all cases where it is considering the *safe-to-escape* lifetimes of arguments the spec will change to ignore those arguments which line up to parameters which are marked as `[DoesNotEscape]`. Because these arguments cannot escape their lifetime does not need to be considered when considering the lifetime of returned values.

For example the last line of calculating *safe-to-escape* of returns will change to 

> the safe-to-escape of all argument expressions including the receiver. **This will exclude all arguments that line up with parameters marked as [DoesNotEscape]**

Detailed Notes:
- The `[DoesNotEscape]` and `[RefThisEscapes]` cannot be combined on the same method 
- The `[DoesNotEscape]` attribute cannot be used on parameters that are `ref`, `out` or `in`.

### Safe fixed size buffers
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

The resulting `Span<T>` instance will have a length equal to the size declared on the `fixed` buffer. The *safe-to-escape* scope of the returned value will be equal to the *safe-to-escape* scope of the container, just as it would if the backing data was accessed as a field.

For each `fixed` declaration in a type where the element type is `T` the language will generate a corresponding `get` only indexer method whose return type is `ref T`. The indexer will be annotated with the `[RefThisEscapes]` attribute as the implementation will be returning fields of the declaring type. The accessibility of the member will match the accessibility on the `fixed` field.

For example, the signature of the indexer for `CharBuffer.Data` will be the following:

```c#
[RefThisEscapes]
internal ref char <>DataIndexer(int index) => ...;
```

If the provided index is outside the declared bounds of the `fixed` array then an `IndexOutOfRangeException` will be thrown. In the case a constant value is provided then it will be replaced with a direct reference to the appropriate element. Unless the constant is outside the declared bounds in which case a compile time error would occur.

There will also be a named accessor generated for each `fixed` buffer that provides by value `get` and `set` operations. Having this means that `fixed` buffers will more closely resemble existing array semantics by having a `ref` accessor as well as byval `get` and `set` operations. This means compilers will have the same flexibility when emitting code consuming `fixed` buffers as they do when consuming arrays. This should be operations like `await` over `fixed` buffers easier to emit. 

This also has the added benefit that it will make `fixed` buffers easier to consume from other languages. Named indexers is a feature that has existed since the 1.0 release of .NET. Even languages which cannot directly emit a named indexer can generally consume them (C# is actually a good example of this).

The backing storage for the buffer will be generated using the `[InlineArray]` attribute. This is a mechanism discussed in [issue 12320](https://github.com/dotnet/runtime/issues/12320) which allows specifically for the case of efficiently declaring sequence of fields of the same type. This particular issue is still under active discussion and the expectation is that the implementation of this feature will follow however that discussion goes.

## Considerations

### Why do we need [RefFieldEscapes]?
The biggest challenge posed by the [compat considerations](#compat-considerations) is that methods cannot capture and return `ref` parameters as `ref` fields. This is a hard assumption in the rules and there are many API patterns today that take advantage of this. In order to have methods that capture `ref` parameters as `ref` fields there must be some form of explicit opt-in that is visible to calling methods.

Several ideas for having implicit opt-in to `ref` capture were explored and discarded: 

- Special casing constructors. It is possible to have constructors of `ref struct` that **directly** define `ref` fields be implicitly opt-in to `[RefFieldEscapes]` semantics. However this does not generalize to factory methods and hence is not a general solution that we can use.
- Special casing methods that return `ref struct` that define a `ref`. There are no such methods today because `ref` fields do not exist hence we could say that methods which return `ref struct` that defined a `ref` field have opted-in to `[RefFieldEscape]` semantics. This works but it essentially prevents any existing `ref struct` from adding `ref` fields. Doing so would cause span safety rules to be interpreted differently in all methods that returned the type. 

These implicit opt-in strategies all have significant holes while an explicit opt-in is fully generalizable and makes the span safety rule different explicit in the code.

### Reference Assemblies
A reference assembly for a compilation using features described in this proposal must maintain the elements that convey span safety information. That means all lifetime annotation attributes and `[RefFieldEscapes]` must be preserved in their original position. Any attempt to replace or omit them can lead to invalid reference assemblies.

Representing `ref` fields is more nuanced. Ideally a `ref` field would appear in a reference assembly as would any other field. However a `ref` field represents a change to the metadata format and that can cause issues with tool chains that are not updated to understand this metadata change. A concrete example is C++/CLI which will likely error if it consumes a `ref` field. Hence it's advantageous if `ref` fields can be omitted from reference assemblies in our core libraries. 

A `ref` field by itself has no impact on span safety rules. As a concrete example consider that flipping the existing `Span<T>` definition to use a `ref` field has no impact on consumption. Hence the `ref` itself can be omitted safely. However a `ref` field does have other impacts to consumption that must be preserved: 

- A `ref struct` which has a `ref` field is never considered `unmanaged` 
- The type of the `ref` field impacts infinite generic expansion rules. Hence if the type of a `ref` field contains a type parameter that must be preserved 

Given those rules here is a valid reference assembly transformation for a `ref struct`: 

```c#
// Impl assembly 
ref struct S<T> {
    ref T _field;
}

// Ref assembly 
ref struct S<T> {
    object _o; // force managed 
    T _f; // mantain generic expansion protections
}
```

## Open Issues

### Keywords vs. attributes
This design calls for using attributes to annotate the new lifetime rules. This also could've been done just as easily with contextual keywords. For instance `[DoesNotEscape]` could map to `scoped`. However keywords, even the contextual ones, generally must meet a very high bar for inclusion. They take up valuable language real estate and are more prominent parts of the language. This feature, while valuable, is going to serve a minority of C# developers.

On the surface that would seem to favor not using keywords but there are two important points to consider: 

1. The annotations will effect program semantics. Having attributes impact program semantics is a line C# is reluctant to cross and it's unclear if this is the feature that should justify the language taking that step.
1. The developers most likely to use this feature intersect strongly with the set of developers that use function pointers. That feature, while also used by a minority of developers, did warrant a new syntax and that decision is still seen as sound. 

Taken together this means syntax should be considered.

A rough sketch of the syntax would be: 

- `[DoesNotEscape]` maps to `scoped` 
- `[RefThisEscapes]` maps to `escapes`
- `[RefFieldEscapes]` maps to `escapes` but attached to the `ref` of the method.
- `[RefFieldDoesNotEscape]` (assuming the [breaking change](#breaking)) maps to `scoped` on the `ref`

Examples:

```c#
Span<T> CreateSpan<T>(escapes ref T value) => new Span<T>(value)

escapes struct S 
{
    int field;

    ref int Prop => ref field;
}
```

### Take the breaking change
<a name="breaking"></a>
This section explores what the design would be if limited compat breaks were allowed. The advantage of the breaks is a simplification of the lifetime rules and allowing for more "correct by construction" API designs. This section will use syntax vs. attributes as that is preferred by those who also favor limited compat breaks.

In brief, the two proposed compat changes are:

* A `ref` parameter on method returning a `ref struct` would be capturable as a `ref` field
* A `out` parameter would be considered `ref-safe-to-escape` within the *current method*.

In this design a `ref` field in a constructor can be effectively ref reassigned to any value that is *ref-safe-to-escape* to the *calling method*. Specifically the rules for ref reassignment will be adjusted to account for `ref` fields as follows:

> For a ref reassignment in the form ...
> 1. `x.e1 = ref e2`: where `x` is *safe-to-escape* to *calling method* then `e2` must be *ref-safe-to-escape* to the *calling method*
> 2. `e1 = ref e2`: the *ref-safe-to-escape* of `e2` must be at least as wide a scope as the *ref-safe-to-escape* of `e1`.

That means the desired `Span<T>` constructor works without any extra annotation:

```c#
Span(ref T value)
{
    // `this` is safe-to-escape and `value` is ref-safe-to-escape to calling method hence 
    // legal by rule 1 above
    _field = ref value;
}
```

The keyword `scoped` will be used to restrict the lifetime of a value. It can be applied to a `ref` or a value and has the impact of restricting the *ref-safe-to-escape* or *safe-to-escape* lifetime, respectively, to the *current method*. For example: 

| Parameter | ref-safe-to-escape | safe-to-escape |
|---|---|---|
| `Span<int> s` | *current method* | *calling method* | 
| `scoped Span<int> s` | *current method* | *current method* | 
| `ref Span<int> s` | *calling method* | *calling method* | 
| `scoped ref Span<int> s` | *current method* | *calling method* | 
| `ref scoped Span<int> s` | *current method* | *current method* | 

The declaration `scoped ref scoped Span<int>` is allowed but is redundant with `ref scoped Span<int>`. The *ref-safe-to-escape* of a value can never exceed the *safe-to-escape* hence once the *safe-to-escape* is restricted via `ref scoped` the *ref-safe-to-escape* is implicitly restricted.

This also means that the `this` parameter of a `struct` can now be completely expressed as `scoped ref T`. Previously it had to be special cased as a `ref T` that had different *ref-safe-to-escape* rules than other `ref`. Now it can be expressed as a general concept vs. being a special cased item.

These annotations can be applied to locals as well as parameters and have the same impact to lifetimes. They cannot be applied to any other location including fields, array elements, etc ... Further while `scoped` can be applied to any `ref` or `in` it can only be applied to by-values which are `ref struct`. Having `scoped int` adds no value to the rules and will be prevented to avoid confusion.

The first compat change will be that `out` parameters will be considered as `scoped out` going forward. That means they cannot be returned by `ref` anymore.

```c#
ref int Sneaky(out int i) 
{
    i = 42;

    // Error: ref-safe-to-escape of out is now the current method
    return ref i;
}
```

This change to `out` reduces the overall compat impact of this change. The ability to return `out` by reference is effectively compiler trivia. However it negatively impacts analysis because the rules must consider the case that it is returned by `ref`. Hence `out` arguments, even though 99% of the time are not returned by ref must be considered as such and that conflates lifetime issues.

The span safety rules for method invocation will be updated in several ways. The first is by recognizing the impact that `scoped` has on arguments. For a given argument `a` that is passed to parameter `p`:

> 1. If `p` is `scoped ref` or `scoped in` then `a` does not contribute *ref-safe-to-escape* when considering arguments
> 2. If `p` is `scoped` then `a` does not contribute *safe-to-escape* when considering arguments. Note that `ref scoped` is included here as the value is `scoped`

The language "does not contribute" means the arguments are simply not considered when calculating the *ref-safe-to-escape* or *safe-to-escape* value of the method return respectively. That is because the values can't contribute to that lifetime (the `scoped` prevents it).

The method invocation for lvalue returns can now be simplified. The receiver no longer needs to be special cased, in the case of `struct` it is now simply a `scoped ref T`, nor do `out` parameters need to be considered in the argument list. 

> An lvalue resulting from a ref-returning method invocation `e1.M(e2, ...)` is *ref-safe-to-escape* the smallest of the following scopes:
> 1. The *calling method*
> 2. The *ref-safe-to-escape* of all `ref` arguments
> 3. The *ref-safe-to-escape* of all `in` parameters when the argument is an lvalue otherwise *current method*
> 4. The *safe-to-escape* of all argument expressions

The method invocation for rvalue returns needs to change as follows to account for `ref` field returns.

> An rvalue resulting from a method invocation `e1.M(e2, ...)` is *safe-to-escape* from the smallest of the following scopes:
> 1. The *calling method*
> 2. The *safe-to-escape* of all argument expressions
> 3. When the return is a `ref struct` then *ref-safe-to-escape* of all `ref` and `in` arguments

This rule now lets us define the two variants of desired methods:

```c#
Span<int> CreateWithoutCapture(scoped ref int value)
{
    // Error: RValue Rule 3 specifies that the safe-to-escape be limited to the ref-safe-to-escape
    // of the ref argument. That is the current method for value hence this is not allowed.
    return new Span<int>(ref value);
}

Span<int> CreateAndCapture(ref int value)
{
    // Okay: RValue Rule 3 specifies that the safe-to-escape be limited to the ref-safe-to-escape
    // of the ref argument. That is the calling method for value hence this is not allowed.
    return new Span<int>(ref value)
}

Span<int> ComplexScopedRefExample(scoped ref Span<int> span)
{
    // Okay: the safe-to-escape of `span` is *calling method* hence this is legal.
    return span;

    // Okay: the local `refLocal` has a ref-safe-to-escape of *current method* and a 
    // safe-to-escape of *calling method*. In the call below it is passed to a 
    // parameter that is `scoped ref` which means it does not contribute 
    // ref-safe-to-escape. It only contributes its safe-to-escape hence the returned
    // rvalue ends up as safe-to-escape of *calling method*
    Span<int> local = default;
    ref Span<int> refLocal = ref local;
    return ComplexScopedRefExample(ref refLocal);

    // Error: similar analysis as above but the safe-to-escape scope of `stackLocal` is 
    // *current method* hence this is illegal
    Span<int> stackLocal = stackalloc int[42];
    return ComplexScopedRefExample(ref stackLocal);
}
```

These compat changes would impact methods that have the following properties:

- Have a `Span<T>` or `ref struct`
    - Where the `ref struct` is a return type, `ref` or `out` parameter
    - Has an additional `in` or `ref` parameter excluding the receiver

To understand the impact it's helpful to break APIs into categories:

1. Want consumers to account for `ref` being captured as a `ref` field. Prime example is the `Span(ref T value)` constructors 
2. Do not want consumers to account for `ref` being captured as a `ref` field. These though break into two catogries
    1. Unsafe APIs. These are APIS inside the `Unsafe` and `MemoryMarshal` types, of which `MemoryMarshal.CreateSpan` is the most prominent. These APIs do capture the `ref` unsafely but they are also known to be unsafe APIs.
    2. Safe APIs. These are APIs which take `ref` parameters for efficiency but it is not actually captured anywhere. Examples are small but one is `AsnDecoder.ReadEnumeratedBytes`

This change primarily benefits (1) above. These are expected to make up the majority of APIs that take a `ref` and return a `ref struct` going forward. The changes negatively impact (2.1) and (2.2) as it breaks the existing calling semantics because the lifetime rules change. 

The APIs in category (2.1) though are largely authored by Microsoft or by developers who stand the most to benefit from `ref` fields (the Tanner's of the world). It is reasonable to assume this class of developers would be ammenable to a compatability tax on upgrade to C# 11 in the form of a few annotations to retain the existing semantics if `ref` fields were provided in return.

The APIs in category (2.2) are the biggest issue. It is unknown how many such APIs exist and it's unclear if these would be more / less frequent in 3rd party code. The expectation is there is a very small number of them, particularly if we take the compat break on `out`. Searches so far have revealed a very small number of these existing in `public` surface area. This is a hard pattern to search for though as it requires semantic analysis. Before taking this change a tool based approach would be needed to verify the assumptions around this impacting a small number of known cases.

For both cases in category (2) though the fix is straight forward. The `ref` parameters that do not want to be considered capturable must add `scoped` to the `ref`. In (2.1) this will likely also force the developer to use `Unsafe` or `MemoryMarshal` but that is expected for unsafe style APIs.

Ideally the language could reduce the impact of silent breaking changes by issuing a warning when an API silently falls into the troublesome behavior. That would be a method that both takes a `ref`, returns `ref struct` but does not actually capture the `ref` in the `ref struct`. The compiler could issue a diagnostic in that case informing developers such `ref` should be annotated as `scoped ref` instead. 

The challenge to keep in mind with respect to compat is this breaking change and the impact of the breaking change can appear in different libraries. Consider the following:

```c#
// Widget.Library.dll
Span<int> CreateSpan(ref int i)
{
    ...
    return new Span<int>(new int[i]);
}

// App.exe
Span<int> Method()
{
    int local = 42;
    var span = CreateSpan(ref local);
    return span;
}
```

Imagine if the order here is `Widget.Library.dll` moved to C# 11 first. That implicitly moved `CreateSpan` to `[RefFieldEscapes]` behavior silently. The code was legal before hence the move to the new rules will silently succeed. A new version is shipped to NuGet.org. Now the App.exe author upgrades to the new version and suddenly they cannot compile. The compiler operating by the new rules says `span` is only *safe-to-escape* to the current method and flags the `return` as an error. The author of App.exe is stuck because the fix is for `CreateSpan` to be marked as `[RefFieldDoesNotEscape]`. The only recourse is `unsafe` APIs.

This would also have an impact on APIs that have `out` parameters and attempt to return them as `ref`. For example:

```c#
ref T StrangeIdentity<T>(out T value)
{
    value = ...;
    return ref value;
}
```

This is an unlikely combination and the language could benefit strongly by considering `out` parameters as not returnable by `ref`. 

Consideration was given in this section to also taking a breaking change for *ref-safe-to-escape* defaults for `this` on a `struct`. Specifically considering changing it to be `ref T` vs. `scoped ref T`. That has the advantage that there is no need for the `escapes` keyword anymore as every value already has maximum return as the default hence only `scoped` is needed to restrict the behavior. The impact of this change is far more significant. It impacts all `struct` instances that have a `ref` returning method which is likely too big of a surface area to take a casual break to.

### Allow fixed buffer locals
This design allows for safe `fixed` buffers that can support any type. One possible extension here is allowing such `fixed` buffers to be declared as local variables. This would allow a number of existing `stackalloc` operations to be replaced with a `fixed` buffer. It would also expand the set of scenarios we could have stack style allocations as `stackalloc` is limited to unmanaged element types while `fixed` buffers are not. 

```c#
class FixedBufferLocals
{
    void Example()
    {
        Span<int> span = stakalloc int[42];
        int buffer[42];
    }
}
```

This holds together but does require us to extend the syntax for locals a bit.  Unclear if this is or isn't worth the extra complexity. Possible we could decide no for now and bring back later if sufficient need is demonstrated.

Example of where this would be beneficial: https://github.com/dotnet/runtime/pull/34149

### To use modreqs or not
A decision needs to be made if methods marked with new lifetime attributes should or should not translate to `modreq` in emit. There would be effectively a 1:1 mapping between annotations and `modreq` if this approach was taken.

The rationale for adding a `modreq` is the attributes change the semantics of span safety. Only languages which understand these semantics should be calling the methods in question. Further when applied to OHI scenarios, the lifetimes become a contract that all derived methods must implement. Having the annotations exist without `modreq` can lead to situations where `virtual` method chains with conflicting lifetime annotations are loaded (can happen if only one part of `virtual` chain is compiled and other is not). 

The initial span safety work did not use `modreq` but instead relied on languages and the framework to understand. At the same time though all of the elements that contribute to the span safety rules are a strong part of the method signature: `ref`, `in`, `ref struct`, etc ... Hence any change to the existing rules of a method already results in a binary change to the signature. To give the new lifetime annotations the same impact they will need `modreq` enforcement.

The concern is whether or not this is overkill. It does have the negative impact that making signatures more flexible, by say adding `[DoesNotEscape]` to a paramater, will result in a binary compat change. That trade off means that over time frameworks like BCL likely won't be able to relax such signatures. It could be mitigated to a degree by taking some approach the language does with `in` parameters and only apply `modreq` in virtual positions. 

### Allow multi-dimensional fixed buffers
Should the design for `fixed` buffers be extended to include multi-dimensional style arrays? Essentially allowing for declarations like the following:

```c#
struct Dimensions
{
    int array[42, 13];
}
```

## Future Considerations

### Allowing attributes on locals
Another friction point for developers using `ref struct` is local variables can suffer from the same issues as parameters with respect to their lifetimes being decided at declaration. Than can make it difficult to work with `ref struct` that are assigned on multiple paths where at least one of the paths is a limited *safe-to-escape* scope. 

```c#
int length = ...;
Span<byte> span;
if (length > StackAllocLimit)
{
    span = new Span(new byte[length]);
}
else
{
    // Error: The *safe-to-escape* of `span` was decided to be outside the 
    // current method scope hence it can't be the target of a stackalloc
    span = stackalloc byte[length];
}
```

For `Span<T>` specifically developers can work around this by initializing the local with a `stackalloc` of size zero. This changes the *safe-to-escape* scope to be the current method and is optimized away by the compiler. It's effectively a syntax for making a `[DoesNotEscape]` local.

```c#
int length = ...;
Span<byte> span = stackalloc byte[0];
if (length > StackAllocLimit)
{
    span = new Span(new byte[length]);
}
else
{
    // Okay
    span = stackalloc byte[length];
}
```

This only works for `Span<T>` though, there is no general purpose mechanism for `ref struct` values. However the `[DoesNotEscape]` attribute provides exactly the semantics that are desired here. If we decide in the future to allow attributes to apply to local variables it would provide immediate relief to this scenario.

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

This snippet wants to mutate a parameter by escaping elements of the data. The escaped data can be stack allocated for efficiency. Even though the parameter is not escaped the compiler assigns it a *safe-to-escape* scope of outside the enclosing method because it is a parameter. This means in order to use stack allocation the implementation must use `unsafe` in order to assign back to the parameter after escaping the data.

### Fun Samples

#### Frugal list

```c#
struct FrugalList<T>
{
    private T _item0;
    private T _item1;
    private T _item2;

    public int Count = 3;

    public ref T this[int index]
    {
        [RefThisEscapes]
        get
        {
            switch (index)
            {
                case 0: return ref _item1;
                case 1: return ref _item2;
                case 2: return ref _item3;
                default: throw null;
            }
        }
    }
}
```

#### Stack based linked list

```c#
ref struct StackLinkedListNode<T>
{
    T _value;
    ref StackLinkedListNode<T> _next;

    public T Value => _value;

    public bool HasNext => !Unsafe.IsNullRef(ref _next);

    public ref StackLinkedListNode<T> Next 
    {
        get
        {
            if (!HasNext)
            {
                throw new InvalidOperationException("No next node");
            }

            return ref _next;
        }
    }

    public StackLinkedListNode(T value)
    {
        this = default;
        _value = value;
    }

    public StackLinkedListNode(T value, ref StackLinkedListNode<T> next)
    {
        _value = value;
        _next = ref next;
    }
}
```



