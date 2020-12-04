Low Level Struct Improvements
=====

## Summary
This proposal is an aggregation of several different proposals for `struct` 
performance improvements. The goal being a design which takes into account the
various proposals to create a single overarching feature set for `struct` 
improvements.

## Motivation
Over the last few releases C# has added a number of low level performance 
features to the language: `ref` returns, `ref struct`, function pointers, 
etc. ... These enabled .NET developers to create write highly performant code
while continuing to leverage the C# language rules for type and memory safety.
It also allowed the creation of fundamental performance types in the .NET 
libraries like `Span<T>`.

As these features have gained traction in the .NET ecosystem developers, both
internal and external, have been providing us with information on remaining 
friction points in the ecosystem. Places where they still need to drop to 
`unsafe` code to get their work, or require the runtime to special case types
like `Span<T>`. 

This proposal aims to address many of these concerns by building on top of our
existing low level features. Specifically it aims to:

- Allow `ref struct` types to declare `ref` fields.
- Allow the runtime to fully define `Span<T>` using the C# type system and 
remove special case type like `ByReference<T>`
- Allow `struct` types to return `ref` to their fields.
- Allow the declaration of safe `fixed` buffers for managed and unmanaged types
in `struct`

## Detailed Design 
The rules for `ref struct` safety are defined in the 
[span-safety document](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md). 
This document will describe the required changes to this document as a result of
this proposal. Once accepted as an approved feature these changes will be
incorporated into that document.

### Provide ref fields
The language will allow developers to declare `ref` fields inside of a 
`ref struct`. This can be useful for example when encapsulating large 
mutable `struct` instances or defining high performance types like `Span<T>`
in libraries besides the runtime.

Today `ref` fields accomplished in the runtime by using the `ByReference<T>` 
type which the runtime treats effective as a `ref` field. This means though 
that only the runtime repository can take full advantage of `ref` field like 
behavior and all uses of it require manual verification of safety. Part of the 
[motivation for this work](https://github.com/dotnet/runtime/issues/32060) is
to remove `ByReference<T>` and use proper `ref` fields in all code bases. 
The challenging part about allowing `ref` fields declarations though comes in
defining rules such that `Span<T>` can be defined using `ref` fields without 
breaking compatibility with existing code. 

Before diving into the problems here it should be noted that `ref` fields only
require a small number of targeted changes to our existing span safety rules. In
some cases it's not even to support new features but to rationalize our existing
`Span<T>` usage of `ref` data. This section of the proposal is quite involved
though because I feel it's important to communicate the "why" of these changes
in as much detail as possible and providing supporting samples. This is to both 
ensure the changes are sound as well as giving future developers a better 
understanding of the choices made here.

To understand the challenges here let's first consider how `Span<T>` will look
once `ref` fields are supported.

```cs
// This is the eventual definition of Span<T> once we add ref fields into the
// language
readonly ref struct Span<T>
{
    ref readonly T _field;
    int _length;

    // This constructor does not exist today however will be added as a
    // part of changing Span<T> to have ref fields. It is a convenient, and
    // safe, way to create a length one span over a stack value that today 
    // requires unsafe code.
    public Span(ref T value)
    {
        ref _field = ref value;
        _length = 1;
    }
}
```

The constructor defined here presents a problem because its return values must
necessarily have restricted lifetimes for many inputs. Consider that if a 
local parameter is passed by `ref` into this constructor that the returned 
`Span<T>` must have a *safe-to-escape* scope of the local's declaration scope.

```cs
Span<int> CreatingAndReturningSpan()
{
    int i = 42;

    // This must be an error in the new design because it stores stack 
    // state in the Span. 
    return new Span<int>(ref i);

    // This must be legal in the new design because it is legal today (it 
    // cannot store stack state)
    return new Span<int>(new int[] { });
}
```

At the same time it is legal to have methods today which take a `ref` parameter 
and return a `Span<T>`.  These methods bear a lot of similarity to the newly 
added `Span<T>` constructor: take a `ref`, return a `Span<T>`. However the
lifetime of the return value of these methods is never restricted by the inputs.
The existing span safety rules consider such values as effectively always 
*safe-to-escape* outside the enclosing method.

```cs
class ExistingScenarios
{
    Span<T> CreateSpan<T>(ref T p)
    {
        // The implementation of this method is irrelevant. From the point of
        // the consumer the returned value is always safe to return.
        ... 
    }

    Span<T> Examples<T>(ref T p, T[] array)
    {
        // Legal today
        return CreateSpan(ref p); 

        // Legal today, must remain legal
        T local = default;
        return CreateSpan(ref local);

        // Legal for any possible value that could be used as an argument
        return CreateSpan(...);
    }
}
```

The reason that all of the above samples are legal is because in the 
existing design there is no way for the return `Span<T>` to store a reference 
to the input state of the method call. This is because the span safety rules 
explicitly depend on `Span<T>` not having a constructor which takes a `ref` 
parameter and stores it as a field. 

```cs
class ExistingAssumptions
{
    Span<T> CreateSpan<T>(ref T p)
    {
        // !!! Cannot happen today !!!
        // The existing span safety rules specifically call out that this method
        // cannot exist hence they can assume all returns from CreateSpan are
        // safe to return.
        return new Span<T>(ref p);
    }
}
```

The rules we define for `ref` fields must ensure the `Span<T>` constructor 
properly restricts the *safe-to-escape* scope of constructed objects in the 
cases it captures `ref` state. At the same time it must ensure that we don't 
break the existing consumption rules for methods like `CreateSpan<T>`. 

```cs
class GoalOfRefFields
{
    Span<T> CreateSpan<T>(ref T p)
    {
        // ERROR: the existing consumption rules for CreateSpan believe this 
        // can never happen hence we must continue to enforce that it cannot
        return new Span<T>(ref p);

        // Okay: this is legal today
        return new Span<int>(new int[] { });
    }

    Span<int> ConsumptionCompatibility()
    {
        // Okay: this is legal today and must remain legal.
        int local = 42;
        return CreateSpan(ref local);

        // Okay: the arguments don't actually matter here. Literally any value 
        // could be passed to this method and the return of it would still be 
        // *safe-to-escape* outside the enclosing method. 
        return CreateSpan(...);
    }
}
```

This tension between allowing constructors such as `Span<T>(ref T field)` and 
ensuring compatibility with `ref struct` returning methods like `CreateSpan<T>` 
is a key pivot point in the design of `ref` fields.

To do this we will change the escape rules for a constructor invocation, which
today are the same as method invocation, on a `ref struct` that **directly** 
contains a `ref` field as follows:
- If the constructor contains any `ref`, `out` or `in` parameters, and the 
arguments do not all refer to the heap, then the *safe-to-escape* of the return
will be the current scope
- Else if the constructor contains any `ref struct` parameters then the 
*safe-to-escape* of the return will be the current scope
- Else the *safe-to-escape* will be the outside the method scope

Let's examine these rules in the context of samples to better understand their
impact.

```cs
ref struct RS
{
    ref int _field;

    public RS(int[] array, int index)
    {
        ref _field = ref array[index];
    }

    public RS(ref int i)
    {
        ref _field = ref i;
    }

    static RS CreateRS(ref int i)
    {
        // The implementation of this method is irrelevant to the safety rule
        // examples below. The returned value is always *safe-to-escape* outside
        // the enclosing method scope
    }

    static RS RuleExamples(ref int i, int[] array)
    {
        var rs1 = new RS(ref i);

        // ERROR by bullet 1: the safe-to-escape scope of 'rs1' is the current
        // scope.
        return rs1; 

        var rs2 = new RS(array, 0);

        // Okay by bullet 2: the safe-to-escape scope of 'rs2' is outside the 
        // method scope.
        return rs2; 

        int local = 42;

        // ERROR by bullet 1: the safe-to-escape scope is the current scope
        return new RS(ref local);
        return new RS(ref i);

        // Okay because rules for method calls have not changed. This is legal
        // today hence it must be legal in the presence of ref fields.
        return CreateRS(ref local);
        return CreateRS(ref i);
    }
}
```

It is important to note that for the purposes of the rule above any use of 
constructor chaining via `this` is considered a constructor invocation. The
result of the chained constructor call is considered to be returning to the 
original constructor hence *safe-to-escape* rules come into play. That is 
important in avoiding unsafe examples like the following:

```cs
ref struct RS1
{
    ref int _field;
    public RS1(ref int p)
    {
        ref _field = ref p;
    }
}

ref struct RS2
{
    RS1 _field;
    public RS2(RS1 p)
    {
        // Okay
        _field = p;
    }

    public RS2(ref int i)
    {
        // ERROR: The *safe-to-escape* scope of the constructor here is the 
        // current method scope while the *safe-to-escape* scope of `this` is
        // outside the current method scope hence this assignment is illegal
        _field = new RS1(ref i);
    }

    public RS2(ref int i)  
        // ERROR: the *safe-to-escape* return of :this the current method scope
        // but the 'this' parameter has a *safe-to-escape* outside the current
        // method scope
        : this(new RS1(ref i))
    {

    }
}
```

The limiting of the constructor rules to just `ref struct` that directly contain
 `ref` field is another important compatibility concern. Consider that the 
majority of `ref struct` defined today indirectly contain `Span<T>` references. 
That mean by extension they will indirectly contain `ref` fields once `Span<T>`
adopts `ref` fields. Hence it's important to ensure the *safe-to-return* rules
of constructors on these types do not change. That is why the restrictions
must only apply to types that directly contain a `ref` field.

Example of where this comes into play.

```cs
ref struct Container
{
    LargeStruct _largeStruct;
    Span<int> _span;

    public Container(in LargeStruct largeStruct, Span<int> span)
    {
        _largeStruct = largeStruct;
        _span = span;
    }
}
```

Much like the `CreateSpan<T>` example before the *safe-to-escape* return of the 
`Container` constructor is not impacted by the `largeStruct` parameter. If the
new constructor rules were applied to this type then it would break 
compatibility with existing code. The existing rules are also sufficient for 
existing constructors to prevent them from simulating `ref` fields by storing
them into `Span<T>` fields.

```cs
ref struct RS4
{
    Span<int> _span;

    public RS4(Span<int> span)
    {
        // Legal today and the rules for this constructor invocation 
        // remain unchanged
        _span = span;
    }

    public RS4(ref int i)
    {
        // ERROR. Bullet 1 of the new constructor rules gives this newly created
        // Span<T> a *safe-to-escape* of the current scope. The 'this' parameter
        // though has a *safe-to-escape* outside the current method. Hence this
        // is illegal by assignment rules because it's assigning a smaller scope
        // to a larger one.
        _span = new Span(ref i);
    }

    // Legal today, must remain legal for compat. If the new constructor rules 
    // applied to 'RS4' though this would be illegal. This is why the new 
    // constructor rules have a restriction to directly defining a ref field
    // 
    // Only ref struct which explicitly opt into ref fields would see a breaking
    // change here.
    static RS4 CreateContainer(ref int i) => new RS4(ref i);
}
```

This design also requires that the rules for field lifetimes be expanded as the
rules today simply don't account for them. It's important to note that our 
expansion of the rules here is not defining new behavior but rather accounting 
for behavior that has long existed. The safety rules around using `ref struct` 
fully acknowledge and account for the possibility that `ref struct` will 
contain `ref` state and that `ref` state will be exposed to consumers. The most
prominent example of this is the indexer on `Span<T>`:

``` cs
readonly ref struct Span<T>
{
    public ref T this[int index] => ...; 
}
```

This directly exposes the `ref` state inside `Span<T>` and the span safety 
rules account for this. Whether that was implemented as `ByReference<T>` or `ref`
fields is immaterial to those rules. As a part of allowing `ref` fields though 
we must define their rules such that they fit into the existing consumption 
rules for `ref struct`. Specifically this must account for the fact that it's 
legal *today* for a `ref struct` to return its `ref` state as `ref` to the 
consumer. 

To understand the proposed changes it's helpful to first review the existing 
rules for method invocation around *ref-safe-to-escape* and how they account for 
a `ref struct` exposing `ref` state today:

> An lvalue resulting from a ref-returning method invocation e1.M(e2, ...) is *ref-safe-to-escape* the smallest of the following scopes:
> 1. The entire enclosing method
> 2. The *ref-safe-to-escape* of all ref and out argument expressions (excluding the receiver)
> 3. For each in parameter of the method, if there is a corresponding expression that is an lvalue, its *ref-safe-to-escape*, otherwise the nearest enclosing scope
> 4. the *safe-to-escape* of all argument expressions (including the receiver)

The fourth item provides the critical safety point around a `ref struct` 
exposing `ref` state to callers. When the `ref` state stored in a `ref struct` 
refers to the stack then the *safe-to-escape* scope for that `ref struct` will 
be at most the scope which defines the state being referred to. Hence limiting
the *ref-safe-to-escape* of invocations of a `ref struct` to the 
*safe-to-escape* scope of the receiver ensures the lifetimes are correct.

Consider as an example the indexer on `Span<T>` which is returning `ref` fields
by `ref` today. The fourth item here is what provides the safety here:

```cs
ref int Examples()
{
    Span<int> s1 = stackalloc int[5];
    // ERROR: illegal because the *safe-to-escape* scope of `s1` is the current
    // method scope hence that limits the *ref-safe-to-escape" to the current
    // method scope as well.
    return ref s1[0];

    // SUCCESS: legal because the *safe-to-escape* scope of `s2` is outside
    // the current method scope hence the *ref-safe-to-escape* is as well
    Span<int> s2 = default;
    return ref s2[0];
}
```

To account for `ref` fields the *ref-safe-to-escape* rules for fields will be 
adjusted to the following:

> An lvalue designating a reference to a field, e.F, is *ref-safe-to-escape* (by reference) as follows:
> - If `F` is a `ref` field and `e` is `this`, it is *ref-safe-to-escape* from the enclosing method.
> - Else if `F` is a `ref` field its *ref-safe-to-escape* scope is the *safe-to-escape* scope of `e`.
> - Else if `e` is of a reference type, it is *ref-safe-to-escape* from the enclosing method.
> - Else its *ref-safe-to-escape* is taken from the *ref-safe-to-escape* of `e`.

This explicitly allows for `ref` fields being returned as `ref` from a 
`ref struct` but not normal fields (that will be covered later). 

```cs
ref struct RS
{
    ref int _refField;
    int _field;

    // Okay: this falls into bullet one above. 
    public ref int Prop1 => ref _refField;

    // ERROR: This is bullet four above and the *ref-safe-to-escape* of `this`
    // in a `struct` is the current method scope.
    public ref int Prop2 => ref _field;

    public RS(int[] array)
    {
        ref _refField = ref array[0];
    }

    public RS(ref int i)
    {
        ref _refField = ref i;
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

        // ERROR: the *safe-to-escape* of `local4` here is the current method 
        // scope by the revised constructor rules. This falls into bullet two 
        // above and hence based on that allowed scope.
        int local3 = 42;
        var local4 = new RS(ref local3);
        return ref local4.Prop1;

    }
}
```

The rules for assignment also need to be adjusted to account for `ref` fields.
This design only allows for `ref` assignment of a `ref` field during object 
construction or when the value is known to refer to the heap. Object 
construction includes in the constructor of the declaring type, inside 
`init` accessors and inside object initializer expressions. Further the `ref` 
being assigned to the `ref` field in this case must have *ref-safe-to-escape* 
greater than the receiver of the field: 

- Constructors: The value must be *ref-safe-to-escape* outside the constructor
- `init` accessors:  The value limited to values that are known to refer to the 
heap as accessors can't have `ref` parameters
- object initializers: The value can have any *ref-safe-to-escape* value as this
will feed into the calculation of the *safe-to-escape* of the constructed 
object by existing rules.

A `ref` field can only be assigned outside a constructor when the value is known
to refer to the heap. That is allowed because it is both safe at the assignment
location (meets the field assignment rules for ensuring the value being 
assigned has a lifetime at least as large as the receiver) as well as requires
no updates to the existing method invocation rules. 

This design does not allow for general `ref` field assignment outside object
construction due to existing limitations on lifetimes. Specifically it poses 
challenges for scenarios like the following:

```cs
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
        ref s._field = ref i;

        // ERROR: this must be disallowed for the exact same reasons we can't 
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
        ref s._field = ref array[i];

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

There are designs choices we could make to allow more flexible `ref` 
re-assignment of fields. For example it could be allowed in cases where we knew
the receiver had a *safe-to-escape* scope that was not outside the current 
method scope. Further we could provide syntax for making such downward facing
values easier to declare: essentially values that have *safe-to-escape* scopes
restricted to the current method. Such a design is discussed [here](https://github.com/dotnet/csharplang/discussions/1130)).
However extra complexity of such rules do not seem to be worth the limited cases
this enables. Should compelling samples come up we can revisit this decision.

This means though that `ref` fields are largely in practice `ref readonly`. The
main exceptions being object initializers and when the value is known to refer 
to the heap.

A `ref` field will be emitted into metadata using the `ELEMENT_TYPE_BYREF` 
signature. This is no different than how we emit `ref` locals or `ref` 
arguments. For example `ref int _field` will be emitted as
`ELEMENT_TYPE_BYREF ELEMENT_TYPE_I4`. This will require us to update ECMA335 
to allow this entry but this should be rather straight forward.

Developers can continue to initialize a `ref struct` with a `ref` field using 
the `default` expression in which case all declared `ref` fields will have the 
value `null`. Any attempt to use such fields will result in a
`NullReferenceException` being thrown.

```cs
struct S1 
{
    public ref int Value;
}

S1 local = default;
local.Value.ToString(); // throws NullReferenceException
```

While the C# language pretends that a `ref` cannot be `null` this is legal at the
runtime level and has well defined semantics. Developers who introduce `ref` 
fields into their types need to be aware of this possibility and should be 
**strongly** discouraged from leaking this detail into consuming code. Instead
`ref` fields should be validated as non-null using the [runtime helpers](https://github.com/dotnet/runtime/pull/40008) 
and throwing when an uninitialized `struct` is used incorrectly.

```cs
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

Misc Notes:
- A `ref` field can only be declared inside of a `ref struct` 
- A `ref` field cannot be declared `static`
- A `ref` field can only be `ref` assigned in the constructor of the declaring
type.
- The reference assembly generation process must preserve the presence of a 
`ref` field inside a `ref struct` 
- A `ref readonly struct` must declare its `ref` fields as `ref readonly`
- The span safety rules for constructors, fields and assignment must be updated
as outlined in this document.
- The span safety rules need to include the definition of `ref` values that 
"refer to the heap". 

### Provide struct this escape annotation
The rules for the scope of `this` in a `struct` limit the *ref-safe-to-escape*
scope to the current method. That means neither `this`, nor any of its fields
can return by reference to the caller.

```cs
struct S
{
    int _field;
    // Error: this, and hence _field, can't return by ref
    public ref int Prop => ref _field;
}
```

There is nothing inherently wrong with a `struct` escaping `this` by reference.
Instead the justification for this rule is that it strikes a balance between the 
usability of `struct` and `interfaces`. If a `struct` could escape `this` by 
reference then it would significantly reduce the use of `ref` returns in 
interfaces.

```cs
interface I1
{
    ref int Prop { get; }
}

struct S1 : I1
{
    int _field;
    public ref int Prop => _ref field;

    // When T is a struct type, like S1 this would end up returning a reference
    // to the parameter
    static ref int M<T>(T p) where T : I1 => ref p.Prop;
}
```

The justification here is reasonable but it also introduces unnecessary
friction on `struct` members that don't participate in interface invocations. 

One key compatibility scenario that we have to keep in mind when approaching 
changes here is the following:

```cs
struct S1
{
    ref int GetValue() => ...
}

class Example
{
    ref int M()
    {
        // Okay: this is always allowed no matter how `local` is initialized
        S1 local = default;
        return local.GetValue();
    }
}
```

This works because the safety rules for `ref` return today do not take into 
account the lifetime of `this` (because it can't return a `ref` to internal
state). This means that `ref` returns from a `struct` can return outside the 
enclosing method scope except in cases where there are `ref` parameters or a 
`ref struct` which is not *safe-to-escape* outside the enclosing method scope. 
Hence the solution here is not as easy as allowing `ref` return of fields in 
non-interface methods.

To remove this friction the language will provide the attribute `[ThisRefEscapes]`.
When this attribute is applied to an instance method, instance property or 
instance accessor of a `struct` or `ref struct` then the `this` parameter will
be considered *ref-safe-to-escape* outside the enclosing method.

This allows for greater flexibility in `struct` definitions as they can begin 
returning `ref` to their fields. That allows for types like `FrugalList<T>`:

```cs
struct FrugalList<T>
{
    private T _item0;
    private T _item1;
    private T _item2;

    public int Count = 3;

    public ref T this[int index]
    {
        [ThisRefEscapes]
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

This will naturally, by the existing rules in the span safety spec, allow
for returning transitive fields in addition to direct fields.

```cs
struct ListWithDefault<T>
{
    private FrugalList<T> _list;
    private T _default;

    public ref T this[int index]
    {
        [ThisRefEscapes]
        get
        {
            if (index >= _list.Count)
            {
                return ref _default;
            }

            return ref _list[index];
        }
    }
}
```

Members which contain the `[ThisRefEscapes]` attribute cannot be used to implement 
interface members. This would hide the lifetime nature of the member at 
the `interface` call site and would lead to incorrect lifetime calculations.

To account for this change the "Parameters" section of the span safety document
will be updated to include the following:

- If the parameter is the `this` parameter of a `struct` type, it is 
*ref-safe-to-escape* to the top scope of the enclosing method unless the 
method is annotated with `[ThisRefEscapes]` in which case it is *ref-safe-to-escape*
outside the enclosing method.

Misc Notes:
- A member marked as `[ThisRefEscapes]` can not implement an `interface` method
or be `overrides`
- A member marked as `[ThisRefEscapes]` will be emitted with a `modreq` on that
attribute.
- The `RefEscapesAttribute` will be defined in the 
`System.Runtime.CompilerServices` namespace.

### Safe fixed size buffers
The language will relax the restrictions on fixed sized arrays such that they 
can be declared in safe code and the element type can be managed or unmanaged. 
This will make types like the following legal:

```cs
internal struct CharBuffer
{
    internal fixed char Data[128];
}
```

These declarations, much like their `unsafe` counter parts, will define a 
sequence of `N` elements in the containing type. These members can be accessed 
with an indexer and can also be converted to `Span<T>` and `ReadOnlySpan<T>` 
instances.

When indexing into a `fixed` buffer of type `T` the `readonly` state of the 
container must be taken into account.  If the container is `readonly` then the
indexer returns `ref readonly T` else it returns `ref T`. 

Accessing a `fixed` buffer without an indexer has no natural type however it is
convertible to `Span<T>` types. In the case the container is `readonly` the 
buffer is implicitly convertible to `ReadOnlySpan<T>`, else it can implicitly 
convert to `Span<T>` or `ReadOnlySpan<T>` (the `Span<T>` conversion is 
considered *better*). 

The resulting `Span<T>` instance will have a length equal to the size declared
on the `fixed` buffer. The *safe-to-escape* scope of the returned value will
be equal to the *safe-to-escape* scope of the container.

For each `fixed` declaration in a type where the element type is `T` the 
language will generate a corresponding `get` only indexer method whose return
type is `ref T`. The indexer will be annotated with the `[ThisRefEscapes]` attribute
as the implementation will be returning fields of the declaring type. The 
accessibility of the member will match the accessibility on the `fixed` field.

For example, the signature of the indexer for `CharBuffer.Data` will be the 
following:

```cs
[ThisRefEscapes]
internal ref char <>DataIndexer(int index) => ...;
```

If the provided index is outside the declared bounds of the `fixed` array then
an `IndexOutOfRangeException` will be thrown. In the case a constant value is 
provided then it will be replaced with a direct reference to the appropriate 
element. Unless the constant is outside the declared bounds in which case a 
compile time error would occur.

There will also be a named accessor generated for each `fixed` buffer that 
provides by value `get` and `set` operations. Having this means that `fixed` 
buffers will more closely resemble existing array semantics by having a `ref`
accessor as well as byval `get` and `set` operations. This means compilers will
have the same flexibility when emitting code consuming `fixed` buffers as they 
do when consuming arrays. This should be operations like `await` over `fixed` 
buffers easier to emit. 

This also has the added benefit that it will make `fixed` buffers easier to 
consume from other languages. Named indexers is a feature that has existed since
the 1.0 release of .NET. Even languages which cannot directly emit a named 
indexer can generally consume them (C# is actually a good example of this).

There will also be a by value `get` and `set` accessor generated for every 

The backing storage for the buffer will be generated using the 
`[InlineArray]` attribute. This is a mechanism discussed in [isuse 12320](https://github.com/dotnet/runtime/issues/12320) 
which allows specifically for the case of efficiently declaring sequence of 
fields of the same type.

This particular issue is still under active discussion and the expectation is
that the implementation of this feature will follow however that discussion
goes.

### Provide parameter does not escape annotations
One source of repeated friction in low level code is the default escape scope
for parameters being *safe-to-escape* outside the enclosing method body. This
is a sensible default because it lines up with the coding patterns of .NET as
a whole. In low level code there is a larger usage of `ref struct` and this 
default scope can cause friction with other parts of our span safety rules.

The main friction point occurs because of the following constraint around method
invocations:

> For a method invocation if there is a ref or out argument of a ref struct type (including the receiver), with safe-to-escape E1, then no argument (including the receiver) may have a narrower safe-to-escape than E1

This rule most commonly comes into play with instance methods on `ref struct` 
where at least one parameter is also a `ref struct`. This is a common pattern
in low level code where `ref struct` types commonly leverage `Span<T>` 
parameters in their methods. Consider any builder or writer style object that 
uses `Span<T>` to pass around buffers.

This rule exists to prevent scenarios like the following:

```cs
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

Essentially this rule exists because the language must assume that all inputs 
to a method escape to their maximum allowed scope. In the above case the 
language must assume that parameters escape into fields of the receiver.

In practice though there are many such methods which never escape the parameter.
It is just a value that is used within the implementation. 

```cs
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

        // Error: The *safe-to-escape* of `span` is the current method scope 
        // while `reader` is outside the current method scope hence this fails
        // by the above rule.
        if (reader.TextEquals(span)
        {
            ...
        })
    }
}
```

In order to work around this low level code will resort to `unsafe` tricks to 
lie to the compiler about the lifetime of their `ref struct`. This significantly
reduces the value proposition of `ref struct` as they are meant to be a means 
to avoid `unsafe` while continuing to write high performance code.

The other place where parameter default escape scope causes friction is when 
they are re-assigned within a method body. For instance if a method body decides
to conditionally apply escaping to input by using stack allocated values. Once
again this runs into some friction.

```cs
void WriteData(ReadOnlySpan<char> data)
{
    if (data.Contains(':'))
    {
        Span<char> buffer = stackalloc char[256];
        Escape(data, buffer, out var length);

        // Error: Cannot assign `buffer` to `data` here as the *safe-to-escape*
        // scope of `buffer` is to the current method scope while `buffer` is
        // outside the current method scope
        data = buffer.Slice(0, length);
    }

    WriteDataCore(data);
}
```

This pattern is fairly common across .NET code and it works just fine when 
a `ref struct` is not involved. Once users adopt `ref struct` though it forces them
to change their patterns here and often they just resort to `unsafe` to work
around the limitations here.

To remove this friction the language will provide the attribute 
`[DoesNotEscape]`. This can be applied to parameters of any type or instance 
members defined on `ref struct`. When applied to parameters the *safe-to-escape*
and *ref-safe-to-escape* scope will be the current method scope. When applied to
instance members of `ref struct` the same limitation will apply to the `this`
parameter.

```cs
class C
{
    static Span<int> M1(Span<int> p1, [DoesNotEscape] Span<int> p2)
    {
        // Okay: the *safe-to-escape* here is still outside the enclosing scope
        // of the current method.
        return p1; 

        // ERROR: the [DoesNotEscape] attribute changes the *safe-to-escape* 
        // to be limited to the current method scope. Hence it cannot be 
        // returned
        return p2; 

        // ERROR: `local` has the same *safe-to-escape* as `p2` hence it cannot
        // be returned.
        Span<int> local = p2;
        return p2; 
    }
}
```

To account for this change the "Parameters" section of the span safety document
will be updated to include the following bullet:

- If the parameter is marked with `[DoesNotEscape]`it is *safe-to-escape* and
*ref-safe-to-escape* to the scope of the containing method. 

It's important to note that this will naturally block the ability for such 
parameters to escape by being stored as fields. Receivers that are passed by 
`ref`, or `this` on `ref struct`, have a *safe-to-escape* scope outside the 
current method. Hence assignment from a `[DoesNotEscape]` parameter to a field
on such a value fails by existing field assignment rules: the scope of the 
receiver is greater than the value being assigned.

```cs
ref struct S
{
    Span<int> _field;

    void M1(Span<int> p1, [DoesNotEscape] Span<int> p2)
    {
        // Okay: the *safe-to-escape* here is still outside the enclosing scope
        // of the current method and hence the same as the receiver.
        _field = p1;

        // ERROR: the [DoesNotEscape] attribute changes the *safe-to-escape* 
        // to be limited to the current method scope. Hence it cannot be 
        // assigned to a receiver than has a *safe-to-escape* scope outside the 
        // current method.
        _field = p2;
    }
}
```

Given that parameters are restricted in this way we will also update the 
"Method Invocation" section to relax its rules. In all cases where it is 
considering the *ref-safe-to-escape* or *safe-to-escape* lifetimes of arguments
the spec will change to ignore those arguments which line up to parameters 
which are marked as `[DoesNotEscape]`. Because these arguments cannot escape 
their lifetime does not need to be considered when considering the lifetime 
of returned values.

For example the last line of calculating *safe-to-escape* of returns will change
to 

> the safe-to-escape of all argument expressions including the receiver. **This will exclude all arguments that line up with parameters marked as [DoesNotEscape]**

Misc Notes:
- The  `DoesNotEscapeAttribute` will be defined in the 
`System.Runtime.CompilerServices` namespace.
- The `DoesNotEscapeAttribute` cannot be combined with the `[ThisRefEscapes]`
attribute, doing so results in an error.
- The `DoesNotEscapeAttribute` will be emitted as a `modreq`

## Considerations

### Keywords vs. attributes
This design calls for using attributes to annotate the new lifetime rules for 
`struct` members. This also could've been done just as easily with
contextual keywords. For instance: `scoped` and `escapes` could have been 
used instead of `DoesNotEscape` and `ThisRefEscapes`.

Keywords, even the contextual ones, have a much heavier weight in the language
than attributes. The use cases these features solve, while very valuable, 
impact a small number of developers. Consider that only a fraction of 
high end developers are defining `ref struct` instances and then consider that 
only a fraction of those developers will be using these new lifetime features.
That doesn't seem to justify adding a new contextual keyword to the language.

This does mean that program correctness will be defined in terms of attributes
though. That is a bit of a gray area for the language side of things but an 
established pattern for the runtime. 

## Open Issues

### Allow fixed buffer locals
This design allows for safe `fixed` buffers that can support any type. One 
possible extension here is allowing such `fixed` buffers to be declared as 
local variables. This would allow a number of existing `stackalloc` operations
to be replaced with a `fixed` buffer. It would also expand the set of scenarios
we could have stack style allocations as `stackalloc` is limited to unmanaged
element types while `fixed` buffers are not. 

```cs
class FixedBufferLocals
{
    void Example()
    {
        Span<int> span = stakalloc int[42];
        int buffer[42];
    }
}
```

This holds together but does require us to extend the syntax for locals a bit. 
Unclear if this is or isn't worth the extra complexity. Possible we could decide
no for now and bring back later if sufficient need is demonstrated.

Example of where this would be beneficial: https://github.com/dotnet/runtime/pull/34149

### Allow multi-dimensional fixed buffers
Should the design for `fixed` buffers be extended to include multi-dimensional
style arrays? Essentially allowing for declarations like the following:

```cs
struct Dimensions
{
    int array[42, 13];
}
```

## Future Considerations

### Allowing attributes on locals
Another friction point for developers using `ref struct` is local variables 
can suffer from the same issues as parameters with respect to their lifetimes 
being decided at declaration. Than can make it difficult to work with 
`ref struct` that are assigned on multiple paths where at least one of the 
paths is a limited *safe-to-escape* scope. 

```cs
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

For `Span<T>` specifically developers can work around this by initializing the 
local with a `stackalloc` of size zero. This changes the *safe-to-escape* scope
to be the current method and is optimized away by the compiler. It's effectively
a syntax for making a `[DoesNotEscape]` local.

```cs
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

This only works for `Span<T>` though, there is no general purpose mechanism for
`ref struct` values. However the `[DoesNotEscape]` attribute provides exactly 
the semantics that are desired here. If we decide in the future to allow 
attributes to apply to local variables it would provide immediate relief to this
scenario.

## Related Information

### Issues
The following issues are all related to this proposal:

- https://github.com/dotnet/csharplang/issues/1130
- https://github.com/dotnet/csharplang/issues/1147
- https://github.com/dotnet/csharplang/issues/992
- https://github.com/dotnet/csharplang/issues/1314
- https://github.com/dotnet/csharplang/issues/2208
- https://github.com/dotnet/runtime/issues/32060

### Proposals
The following proposals are related to this proposal:

- https://github.com/dotnet/csharplang/blob/725763343ad44a9251b03814e6897d87fe553769/proposals/fixed-sized-buffers.md

### Existing samples

[Utf8JsonReader](https://github.com/dotnet/runtime/blob/f1a7cb3fdd7ffc4ce7d996b7ac6867ffe2c953b9/src/libraries/System.Text.Json/src/System/Text/Json/Reader/Utf8JsonReader.cs#L523-L528)

This particular snippet requires unsafe because it runs into issues with passing
around a `Span<T>` which can be stack allocated to an instance method on a 
`ref struct`. Even though this parameter is not captured the language must assume
it is and hence needlessly causes friction here.

[Utf8JsonWriter](https://github.com/dotnet/runtime/blob/f1a7cb3fdd7ffc4ce7d996b7ac6867ffe2c953b9/src/libraries/System.Text.Json/src/System/Text/Json/Writer/Utf8JsonWriter.WriteProperties.String.cs#L122-L127)

This snippet wants to mutate a parameter by escaping elements of the data. The
escaped data can be stack allocated for efficiency. Even though the parameter
is not escaped the compiler assigns it a *safe-to-escape* scope of outside the
enclosing method because it is a parameter. This means in order to use stack
allocation the implementation must use `unsafe` in order to assign back to the 
parameter after escaping the data.

### Fun Samples

```cs
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
        ref _next = ref next;
    }
}
```
