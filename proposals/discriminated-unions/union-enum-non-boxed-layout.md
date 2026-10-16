# Non-Boxed Storage for Unions and Data-Carrying Enums

The `struct` keyword in [`enum struct`](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md) and [`union struct`](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md) selects an opinionated layout that stores case data inline, avoiding boxing during ordinary creation and extraction. This proposal describes a shared implementation approach. Each language proposal defines its own case types, construction, conversions, defaults, and matching.

The layout is an implementation detail. Compilers may change it, including to use future runtime capabilities, while preserving the specified API and observable semantics. No particular size, field layout, native ABI, or persistent byte representation is guaranteed.

## Scope

A `union` without the `struct` modifier retains the existing object-backed struct representation. A non-null `enum class` value is the case instance itself. Neither uses the inline storage described here.

For a `union struct`, the union pattern's object-valued `Value` property can box a value-type case. Construction directly from a case value, `HasValue`, and `TryGetValue` avoid that access. Support for ref-like values is explored in the [separate ref-like proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-ref-like-types.md).

Unions of existing types may need to retain boxed values, potentially alongside unboxed fields. For example, a listed struct type may implement a listed interface type. Preserving the [union creation equivalence rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#well-formedness) and the identity of an incoming interface reference can require retaining the original box.

Packing and extraction below implement the creation and access operations defined by the two language proposals. They do not introduce additional language conversions or a shared public API.

## Storage Strategy

The simplest layout is a discriminator followed by a separate field for each case type. The containing struct's size is roughly the sum of all case sizes plus the discriminator and padding. Conversions can copy the selected case directly into or out of its field.

Our initial expectation is that this approach would often make `union struct` and `enum struct` types too large to be practical. Even a handful of cases could substantially increase storage and copying costs. This assessment needs performance validation and sign-off from LDM and the runtime team. The compact layout below aims to minimize storage safely with only a small runtime cost for rearranging data during case creation and extraction.

We aim to provide a default that balances space and speed. We are also open to letting authors configure that balance, most likely through attributes, similar to existing .NET layout controls such as [StructLayout](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute). Whether to offer customization, its form and scope, and its availability in the initial feature are all undecided.

### Concurrent Access and Safety

`union struct` and `enum struct` values follow the existing [atomicity rules for struct reads and writes](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/variables.md#96-atomicity-of-variable-references). Unsynchronized accesses may tear, observing a discriminator from one write and payload from another. Such observations are unspecified and accessors may throw, but must preserve memory and type safety under the usual safety assumptions for payload types and user code. Neither copying nor `readonly` makes a concurrent read atomic.

To preserve memory safety, GC reference locations must be determined by the runtime layout independently of the discriminator, including during assignment. No location may alternate between managed references and unmanaged data, and arbitrary bits must never become references.

Shared reference slots require checked extraction: a reader testing a string case could observe an array from a concurrent write. Extraction must check the reference actually read or use an equivalent safe mechanism. The discriminator alone cannot justify an unchecked cast.

### Conservative Storage

A candidate inline layout uses a discriminator and three shared pools:

1. **Unmanaged data**: A byte region for the [predefined types](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#primitive-types-and-layout-assumptions) below, sized for the greatest combined requirement of any case, including padding and alignment. Cases share the region, with distinct locations for components within each case.
2. **Managed references**: Enough slots for the case containing the most references. A slot is normally `object`, but can be more specific when that type accommodates every value assigned to it, such as a slot used only for strings.
3. **Typed values**: Slots for arbitrary value types and type parameters not known to be reference types. Each type needs enough slots for its maximum count in any case. Distinct types cannot generally share a slot. Reference-constrained type parameters can use the reference pool.

Compatibility of a container's own unmanaged classification is an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#unmanaged-classification).

A `struct` constraint does not rule out references. Even `where T : unmanaged` need not provide a declaration-time byte size, so such parameters can remain in typed slots. The pool maxima can come from different cases, making their combined size greater than any individual case.

### Primitive Types and Layout Assumptions

The compiler may rely on the predefined types' unmanagedness and fixed managed sizes from the [existing `sizeof` rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12818-the-sizeof-operator):

| Types | Size in bytes |
| --- | --- |
| `bool`, `byte`, `sbyte` | 1 |
| `char`, `short`, `ushort` | 2 |
| `int`, `uint`, `float` | 4 |
| `long`, `ulong`, `double` | 8 |
| `decimal` | 16 |

These require no annotation and describe managed storage, rather than interop marshaling. Native-sized integers (`nint`/`IntPtr`, `nuint`/`UIntPtr`) are unmanaged but platform-dependent in size. Portable output must accommodate that, for example through native-sized typed fields. No blanket layout assumption applies to other framework or user-defined structs.

### External Types and Decomposition

In the conservative layout, structs from referenced assemblies are stored in fields of their own types. The runtime determines their size and GC layout, keeping storage safe if a dependency grows or acquires references. A library update can add managed references to a previously unmanaged struct. Recompiling a consumer that relies on an `unmanaged` constraint detects that change, but the [CLR does not enforce the constraint](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.3/blittable.md#detailed-design) when loading existing binaries. The [checked packing opt-in](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#possible-checked-packing-opt-in) could permit raw storage for such types.

The compiler can arrange the payload components of case records directly. Decomposing a nested user struct must preserve all value state without invoking user getters, setters, or constructors, and respect accessibility, safety, and supported versioning. Otherwise, the struct is stored in a field of its declared type. Being in the current assembly is insufficient when layout dependencies cross assembly boundaries.

Unmanagedness alone does not establish byte layout. Raw packing must account for architecture, alignment, and padding. The type definition seen at compile time does not guarantee its layout at runtime.

### Mixed-Data Example

These declarations use the same payloads and can use the same storage strategy:

```csharp
enum struct Message<T>
{
    Text(string Content, int Code),
    Buffer(byte[] Data, int Offset, int Count),
    Pair(string First, string Second),
    Timestamp(DateTime Value),
    Values(T First, T Second)
}

union struct Message<T>
{
    case Text(string Content, int Code);
    case Buffer(byte[] Data, int Offset, int Count);
    case Pair(string First, string Second);
    case Timestamp(DateTime Value);
    case Values(T First, T Second);
}
```

The candidate fields below apply to either declaration. Case types, construction, and access APIs differ as specified by their respective proposals:

```csharp
// Candidate storage; case types and APIs omitted:
public struct Message<T>
{
    private byte _discriminator;
    private UnmanagedData _unmanaged; // Buffer needs two ints
    private object? _reference0;      // Text, Buffer, or Pair
    private string? _reference1;      // Pair only
    private DateTime _timestamp;      // external struct kept opaque
    private T _value0, _value1;       // Values; T may contain references

    [InlineArray(2)]
    private struct UnmanagedData { private int _element0; }
}
```

### Packing and Extraction

In the shared-pool layout, creation from a case packs components into their assigned slots and sets the discriminator. Extraction checks for the requested case and reconstructs the case value. Mixed layouts require rearranging components rather than reinterpreting the shared storage as a complete case. For example, `Buffer` uses `_reference0` and both integers in `_unmanaged`, while `Pair` uses both reference slots. Unused reference and typed slots are set to `default` to avoid retaining references from other cases.

When every case is unmanaged and the required layout facts are established, complete case values can safely overlap in one region sized and aligned for the largest case, with a separate discriminator. Packing and extraction can then copy each case as a whole.

### Possible Checked Packing Opt-in

An optional packing contract could let a consumer store otherwise opaque structs in raw storage. A compiler-recognized attribute would assert the payload's unmanagedness and fixed byte size, at its declaration or at a case parameter use site:

```csharp
// In a referenced library:
[Unmanaged(size: 8)]
public readonly record struct Point(int X, int Y);

// Consumer assertions can also cover unannotated external types:
enum struct Position
{
    At(Point Point),
    Timestamp([Unmanaged(size: 8)] DateTime Value)
}
```

For either placement, the compiler must verify that the payload is a struct satisfying C#'s unmanaged rules. The annotation is not proof. It may diagnose size mismatches known statically. The name, syntax, and precise placement rules remain open.

For a declaration-site assertion, the compiler prepends runtime checks to the type's static constructor, preserving user initialization:

```csharp
static Point()
{
    __ValidateNoReferences<Point>();
    __ValidateSize<Point>(8);
}

private static void __ValidateNoReferences<T>() where T : struct
{
    if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        throw new InvalidOperationException("The unmanaged reference-containment assertion failed.");
}

private static void __ValidateSize<T>(int expectedSize) where T : struct
{
    if (Unsafe.SizeOf<T>() != expectedSize)
        throw new InvalidOperationException("The unmanaged size assertion failed.");
}
```

Both checks use actual managed representation, not marshaling size. The illustrative helpers can be shared across types. Failure must prevent raw access with a managed exception, such as a type-initialization failure, and raw copies must remain in bounds, as with [`MemoryMarshal.Read` and `Write`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/MemoryMarshal.cs). These guarantees would remove the need for `unsafe`.

An enum or union using raw packing must validate its own size and reference-containment assumptions through a generated static constructor, once per enclosing type, including each closed generic instantiation. Constructors and creation from case values must trigger this before packing, under the [struct static-constructor rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#16410-static-constructors). Extraction must likewise ensure validation has completed before reconstructing payloads from raw storage, including for a designated `default` case. Returning typed `default` payloads or a failed `TryGetValue` result requires no raw access.

Validation in the payload type's static constructor alone is insufficient: defaults, arrays, layout queries, and whole-value copies can bypass it. A later library could also remove the annotation or validate a different size. A consumer reserving eight bytes must validate its own eight-byte expectation, rejecting updates that ordinary typed storage could accept.

The `DateTime` assertion uses its current eight-byte managed size. The language does not guarantee that size. These cases could share an eight-byte region, separately from the discriminator. The annotation permits packing without requiring it and establishes only reference containment and total size. Field offsets, alignment, and accessibility need separate justification, or values can be copied opaquely. Adoption remains a joint LDM and runtime-team decision supported by performance measurements.

## Equality and Hashing

For data-carrying enums and unions with case declarations, equality and hashing implement the [shared value-equality rules](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#equality-and-hashing) without boxing the active case or accessing an object-valued `Value` property. In the compact layout, equality first checks that the discriminators identify the same case, then compares corresponding payload fields with their declared types' equality comparers. Hashing can combine the case discriminator with those fields' hash codes.

The enclosing struct need not produce the same hash code as an extracted case, including the boxed case returned by `Value`.

For example, `Reading` and `Range` can share their first `double` slot. `Reading` also uses a string slot, while `Range` uses a second `double` slot:

```csharp
enum struct Measurement
{
    Missing = default,
    Reading(double Value, string? Unit),
    Range(double Minimum, double Maximum)
}

union struct Measurement
{
    case Missing = default;
    case Reading(double Value, string? Unit);
    case Range(double Minimum, double Maximum);
}
```

The decomposed storage and synthesized equality members could be as follows. Case types and other members are omitted:

```csharp
public struct Measurement : IEquatable<Measurement>
{
    private byte _discriminator; // 0: Missing, 1: Reading, 2: Range
    private double _value0;      // Reading.Value or Range.Minimum
    private double _value1;      // Range.Maximum
    private string? _unit;       // Reading.Unit

    public readonly bool Equals(Measurement other)
    {
        if (_discriminator != other._discriminator)
            return false;

        return _discriminator switch
        {
            0 => true,
            1 => EqualityComparer<double>.Default.Equals(_value0, other._value0) &&
                 EqualityComparer<string?>.Default.Equals(_unit, other._unit),
            2 => EqualityComparer<double>.Default.Equals(_value0, other._value0) &&
                 EqualityComparer<double>.Default.Equals(_value1, other._value1),
            _ => throw new InvalidOperationException()
        };
    }

    public override readonly bool Equals(object? obj)
        => obj is Measurement other && Equals(other);

    public override readonly int GetHashCode()
        => _discriminator switch
        {
            0 => 0,
            1 => HashCode.Combine(_discriminator, _value0, _unit),
            2 => HashCode.Combine(_discriminator, _value0, _value1),
            _ => throw new InvalidOperationException()
        };

    public static bool operator ==(Measurement left, Measurement right)
        => left.Equals(right);

    public static bool operator !=(Measurement left, Measurement right)
        => !(left == right);
}
```

If future [case bodies](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#customizing-case-records) permit custom case equality or hashing, the implementation must honor those members. Reconstructing a typed case and invoking its methods can do this without boxing the case.

## Case Dispatch

The [shared switch-emission discussion](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-switch-emit.md#non-boxed-cases) describes dispatch through typed case access, avoiding boxing, and possible compiler and JIT optimizations.

## Open Questions

### Unmanaged Classification

The unmanaged classification of a `union struct` or `enum struct` is observable through constraints, pointers, and `stackalloc`, even though its physical representation is an implementation detail. What compatibility guarantee should apply when a compiler changes that representation?

LDM could allow classification to change in either direction, accepting potential source breaks, or require an unmanaged container to remain unmanaged when its cases, payload types, and constraints are unchanged. The latter still allows managed-to-unmanaged changes while restricting future storage choices. Changes to declared cases or payload dependencies are separate library-evolution concerns.
