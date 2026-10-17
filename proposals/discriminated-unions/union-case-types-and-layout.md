# Per-Case Records and Generated Union Layouts

This document explores separating two choices in the [union-based design](discriminated-unions.md): the record kind of each declared case and the storage representation of the enclosing union. There is one `union` declaration form. Each case chooses `record class` or `record struct`, and an attribute can select a source-generated representation.

The existing custom-union pattern is sufficient to consume the generated representation. Supporting it directly on a `union` declaration needs a compiler extension that delegates storage and member synthesis to generated partial declarations. The examples below demonstrate the resulting ordinary C# and identify that extension.

## Example Walkthrough

An expression tree benefits from both kinds of cases. Small leaves can be stored inline, while recursive addition nodes provide the reference needed to keep an inline representation finite:

```csharp
[UnionLayout(typeof(CompactLayout))]
public readonly partial union Expr
{
    case struct Number(double Value);
    case struct Name(string Identifier);
    case Add(Expr Left, Expr Right);
}

Expr expression = Expr.Add(Expr.Number(1), Expr.Name("x"));

static double Evaluate(Expr expression, IReadOnlyDictionary<string, double> names)
    => expression switch
    {
        Expr.Number(var value) => value,
        Expr.Name(var name) => names[name],
        Expr.Add(var left, var right) => Evaluate(left, names) + Evaluate(right, names),
        _ => throw new InvalidOperationException()
    };
```

`case` declares a sealed nested [`record class`](../csharp-9.0/records.md). `case struct` declares a nested [`record struct`](../csharp-10.0/record-structs.md), with the enclosing union's readonlyness. The parameter list is the record's primary constructor. A record struct can be stored inline, in an array, or in a box. The enclosing union remains a struct in every representation.

Without the attribute, ordinary object-backed storage boxes `Number` and `Name` when they enter the union and stores an `Add` reference directly. The illustrated compact strategy stores a number in a `double` field, a name's identifier in a reference field, or an `Add` object in that same reference field. Creating and extracting the leaves then requires no boxing. An `Add` retains its object identity.

The recursion is `Expr` to an `Add` reference to its child `Expr` values. Making `Add` a struct would require a different storage decision: object-backed storage can box it, while a strategy that insists on storing it inline must reject the resulting recursive layout. A hybrid strategy could retain boxing for that case.

The attribute and strategy names in this document are provisional. The separate [`ref` extension](union-enum-ref-like-types.md) retains its own questions about ref safety and object-valued access.

## Compiler and Generator Responsibilities

The compiler defines case declarations, construction syntax, the case set, matching, and the public union contract. The generator supplies fields and implementations for that contract. The runtime lays out the resulting fields, tracks GC references, and implements native calling conventions.

The [current union declaration](../csharp-15.0/unions.md#union-declarations) always synthesizes an object-backed `Value` property and case constructors. It prohibits additional instance fields and a union member provider. A generator cannot remove that backing field or replace those members by adding another partial declaration.

We propose recognizing a `UnionLayoutAttribute` on a union declaration as a request for an externally supplied representation. Its `Type` argument selects a strategy understood by an installed generator, as in `typeof(CompactLayout)` above. The provisional attribute would be defined in `System.Runtime.CompilerServices`, letting the generator find it by its fully qualified metadata name. The compiler need not understand the individual strategies. This mode would:

1. Require the union and any enclosing types to be partial.
2. Expose the nested case record symbols and their primary constructors before generators run.
3. Suppress the default storage and implementation members, allowing generated fields, constructors, and accessors in matching `partial union` declarations.
4. Validate the completed union after generation. Its creation members must describe exactly the declared cases, and its required access and equality members must be present.

In this mode, field and constructor declarations follow ordinary struct rules. A generator that owns the representation can diagnose conflicting user-declared fields. The compiler validates the required member shapes, while their behavioral correctness follows the same trust model as [custom unions](../csharp-15.0/unions.md#well-formedness).

The public contract must remain independent of the selected strategy. All unions with case declarations, including the default object-backed form, should expose the same per-case constructors, `Value`, `HasValue`, typed `TryGetValue` overloads, and the [shared equality members](union-enum-common.md#equality-and-hashing). They should also have the same public `IUnionMembers` provider described below. Switching strategies must preserve those signatures so previously compiled consumers keep their construction and access operations.

This changes synthesis for the new case-declaration feature. Existing unions of arbitrary types keep their current default behavior.

## Generator Inputs and Output

A generator can discover attributed declarations with `ForAttributeWithMetadataName`, then inspect their case symbols, record kinds, primary-constructor parameters, constraints, and readonlyness. The compiler creates these symbols directly from the case declarations. No generator needs to consume another generator's output to discover the cases.

The generator emits a partial declaration with the same containing types and constraints. It supplies the fields, constructor bodies, accessors, equality members, and a public nested `IUnionMembers` interface. That provider's static `Create` overloads identify the cases using the [existing union-member pattern](../csharp-15.0/unions.md#union-member-providers). The union implements the provider's instance members. Other constructor overloads therefore do not accidentally add case types.

Ordinary union conversions call those factories. Typed patterns use `HasValue` and `TryGetValue`, and can avoid the object-valued `Value` property. Consumers of a compiled assembly need the resulting members and case metadata, without running the generator themselves.

The representation marker must cause missing implementation to fail compilation even if the generator package or analyzer is absent. Silently falling back to object-backed storage would defeat an author's explicit allocation or interop requirement.

## Expanded Storage and Access Code

The following expansion uses ordinary nested records in the first partial declaration and the representation implementation in the second. It is compilable C# using the existing custom-union pattern. Constructor calls show the lowering of case-construction expressions. Compatibility attributes for the new case syntax are omitted here.

<details>
<summary>Complete expanded C# for Expr</summary>

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[Union]
public readonly partial struct Expr
{
    public readonly record struct Number(double Value);
    public readonly record struct Name(string Identifier);
    public sealed record class Add(Expr Left, Expr Right);
}

public readonly partial struct Expr : IUnion, Expr.IUnionMembers, IEquatable<Expr>
{
    private const byte NumberCase = 1, NameCase = 2, AddCase = 3;
    private readonly byte _case;
    private readonly double _number;
    private readonly object? _reference;

    private Expr(byte caseId, double number, object? reference)
    {
        _case = caseId;
        _number = number;
        _reference = reference;
    }

    public Expr(Number value) : this(NumberCase, value.Value, null) { }
    public Expr(Name value) : this(NameCase, 0, value.Identifier) { }
    public Expr(Add value) : this(value is null ? (byte)0 : AddCase, 0, value) { }

    public interface IUnionMembers
    {
        public static Expr Create(Number value)
            => new Expr(value);

        public static Expr Create(Name value)
            => new Expr(value);

        public static Expr Create(Add value)
            => new Expr(value);

        object? Value { get; }
        bool HasValue { get; }
        bool TryGetValue(out Number value);
        bool TryGetValue(out Name value);
        bool TryGetValue([NotNullWhen(true)] out Add? value);
    }

    public object? Value
        => _case switch
        {
            0 => null,
            NumberCase => new Number(_number),
            NameCase => new Name((string)_reference!),
            AddCase => (Add?)_reference,
            _ => throw new InvalidOperationException()
        };

    public bool HasValue => _case != 0;

    public bool TryGetValue(out Number value)
    {
        value = _case == NumberCase ? new Number(_number) : default;
        return _case == NumberCase;
    }

    public bool TryGetValue(out Name value)
    {
        value = _case == NameCase ? new Name((string)_reference!) : default;
        return _case == NameCase;
    }

    public bool TryGetValue([NotNullWhen(true)] out Add? value)
    {
        value = _case == AddCase ? _reference as Add : null;
        return value is not null;
    }

    public bool Equals(Expr other)
        => _case == other._case && (_case switch
        {
            0 => true,
            NumberCase => EqualityComparer<double>.Default.Equals(_number, other._number),
            NameCase => EqualityComparer<string>.Default.Equals((string)_reference!, (string)other._reference!),
            AddCase => EqualityComparer<Add?>.Default.Equals((Add?)_reference, (Add?)other._reference),
            _ => throw new InvalidOperationException()
        });

    public override bool Equals(object? obj)
        => obj is Expr other && Equals(other);

    public override int GetHashCode()
        => _case switch
        {
            0 => 0,
            NumberCase => HashCode.Combine(NumberCase, _number),
            NameCase => HashCode.Combine(NameCase, (string?)_reference),
            AddCase => HashCode.Combine(AddCase, (Add?)_reference),
            _ => throw new InvalidOperationException()
        };

    public static bool operator ==(Expr left, Expr right)
        => left.Equals(right);

    public static bool operator !=(Expr left, Expr right)
        => !left.Equals(right);
}
```

</details>

In generated source, reconstruction uses `Expr.Number(_number)` and `Expr.Name(...)`. The displayed `new` calls are their constructor lowering. This keeps the existing source-level restriction on constructing case types with `new`.

`Name.Identifier` and the `Add` reference share one GC reference slot. The casts check the reference actually read, preserving type safety if a concurrent struct copy tears. Every constructor initializes all storage, so changing cases does not retain an unused reference. The [existing concurrency requirements](union-enum-non-boxed-layout.md#concurrent-access-and-safety) apply unchanged.

The class case is stored as the original object. Its extraction and equality delegate to that record. The struct cases use typed payload comparisons, preserving the record rule that `==` delegates to `Equals`, including equality of `double.NaN` payloads. Neither equality nor hashing reads `Value`.

## Rebuilding the Non-Boxed Layout Strategies

The [non-boxed layout exploration](union-enum-non-boxed-layout.md) can be expressed through generated fields and methods:

| Storage choice | Generated representation |
| --- | --- |
| Object-backed | One object field, boxing struct cases on entry and retaining class references. |
| Separate case fields | A discriminator and a typed field for each struct case, with reference fields for class cases. |
| Shared pools | Shared primitive storage and checked reference slots, plus typed fields for opaque structs and type parameters. |
| Hybrid | Inline selected struct cases and box others, including large or recursively stored cases. Case record kinds remain unchanged. |
| Native layout | Fields and layout attributes determined by a specific native ABI, together with matching creation and access operations. |

For example, a generator can overlay primitive payloads in a nongeneric helper, keeping references in separate fields:

```csharp
[StructLayout(LayoutKind.Explicit)]
internal struct NumericStorage
{
    [FieldOffset(0)] public double Number;
    [FieldOffset(0)] public int First;
    [FieldOffset(4)] public int Second;
}
```

The runtime computes alignment from the typed fields. A generator can use this helper from a generic union, while keeping each opaque `T` in its own typed field. The helper must be outside any generic containing type because ordinary explicit-layout generic types are unsupported.

Decomposition is safe for the compiler-defined case records with only synthesized primary-constructor state. Arbitrary nested structs, external types, and type parameters retain typed storage. If future case bodies add state or user-defined accessors, the generator must preserve the complete case value or use an explicit decomposition contract. Running user getters or reconstructing through a validating constructor would change semantics.

The [checked packing opt-in](union-enum-non-boxed-layout.md#possible-checked-packing-opt-in) can likewise be implemented by a generator. It emits the consumer-side size and reference-containment checks and ensures they run before every raw packing or extraction path, including extraction from `default`. A static constructor on the payload type alone remains insufficient.

### Runtime-Computed Unmanaged Overlap

There is also an existing runtime-supported alternative to fixed offsets. Current .NET 11 sources contain [`ExtendedLayoutAttribute`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/ExtendedLayoutAttribute.cs) and the `CStruct` and `CUnion` members of [`ExtendedLayoutKind`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/ExtendedLayoutKind.cs). A generator can emit:

```csharp
[ExtendedLayout(ExtendedLayoutKind.CUnion)]
internal struct UnmanagedStorage<T> where T : unmanaged
{
    public T Item;
    public long Integer;
}
```

The runtime overlaps the fields at offset zero and computes their maximum size and alignment for the actual `T`. This works for generic payloads without embedding a compile-time byte size. It rejects instantiations containing GC references, including ones constructed through reflection that bypass the C# `unmanaged` check. It can be used for unmanaged storage inside a larger struct whose reference slots remain separate.

This requires a compiler and runtime that support extended layout. A generator can select a conservative typed-field implementation on older targets when its policy permits that choice, or report that the requested strategy is unsupported.

## Defaults and Observable Behavior

Case record kinds and the designated default case are declaration semantics. A layout strategy must preserve them. This exploration retains `= default` for struct cases, with the [existing default-payload and nullability rules](union-enum-common.md#nullability-of-default-cases).

In the expanded `Expr`, zero-initialization represents no case. If `Number` were designated `default`, the generator would map the zero discriminator to `Number(0)`. It would also reserve a separate no-case encoding for a null reference supplied through `Create(Add)`. Mapping that null to the active default case would violate the existing union creation rules. `Value` therefore remains nullable when null class inputs are possible, even with a designated default case.

Object-backed storage can make the same distinction with one field: null storage denotes the designated default, while a private sentinel denotes no case. Its `Value` accessor translates the sentinel to null and null storage to the designated case value. A compact representation can instead reserve a nonzero discriminator for no case.

For class cases, `Value` and typed extraction preserve the entered object. For struct cases, optimized storage returns a reconstructed value and `Value` may box a fresh copy on each access. The shared contract cannot guarantee box identity for those copies. The ordinary object-backed implementation can retain its original box, but callers cannot rely on that caching across layout choices.

Layout choices also affect `Unsafe.SizeOf`, allocation, array density, and unmanaged classification. Native interop can make layout an ABI contract. These consequences need explicit strategy contracts, even though the ordinary managed field arrangement remains an implementation detail. Compiler and generator upgrades must preserve the public members and any promised native representation.

## Swift Interop

[Runtime issue #100896](https://github.com/dotnet/runtime/issues/100896) is directly relevant to moving layout policy into attributes and generators. Its [approved API](https://github.com/dotnet/runtime/issues/100896#issuecomment-2110876749) uses `LayoutKind.Extended`, `ExtendedLayoutAttribute`, and kinds including `CUnion`, `SwiftStruct`, and `SwiftEnum`. The current implementation exposes the two C layout kinds. The issue remains open for the remaining kinds. The earlier `CustomLayout` names in the issue's opening description are superseded by that review.

`CUnion` supplies overlapping payload storage, without supplying a discriminated union's tag or access operations. Swift enum layout also involves trailing padding, spare bits, and architecture-specific rules. A generator still needs to implement the logical case API and preserve bits used by the native discriminator.

### A Concrete Frozen Enum

Consider this Swift type:

```swift
@frozen public enum NativeReading {
    case number(Int64)
    case pair(Int32, Int32)
    case empty
}
```

On the tested ARM64 Swift toolchain, its payload occupies eight bytes and its discriminator is the ninth byte: `number` uses 0, `pair` uses 1, and `empty` uses 2. Its size is 9, stride is 16, and alignment is 8. A binding generator can represent an individual value on the tested CoreCLR with:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 9)]
public struct NativeReadingStorage
{
    [FieldOffset(0)] public long Number;
    [FieldOffset(0)] public int First;
    [FieldOffset(4)] public int Second;
    [FieldOffset(8)] public byte Tag;
}
```

For direct native representation, the generator places these fields and the layout attribute on the union itself. Creation assigns the payload fields and the ABI-defined tag. Extraction switches on `Tag` and constructs the corresponding record struct. This can back a union with `Number(long Value)`, `Pair(int First, int Second)`, and `Empty` struct cases using the same member pattern as `Expr`. `Number` must be the designated default case because all-zero native storage represents `number(0)`.

The experiment passed this storage to and from actual Swift functions using [`CallConvSwift`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callconvswift). The functions used Swift's native calling convention, with explicit symbol names for P/Invoke lookup. Number round-trips and pair and empty decoding all succeeded. This verifies the individual frozen-type case on that toolchain and runtime.

<details>
<summary>Native-call probe</summary>

The Swift library exports these functions using Swift's calling convention:

```swift
@_silgen_name("make_native_reading")
public func makeNativeReading(_ value: Int64) -> NativeReading { .number(value) }

@_silgen_name("read_native_reading")
public func readNativeReading(_ value: NativeReading) -> Int64 {
    switch value {
    case .number(let number): number
    case .pair(let first, let second): Int64(first) + Int64(second)
    case .empty: -1
    }
}
```

After building the library with `swiftc -emit-library -module-name LayoutProbe InteropProbe.swift -o libLayoutProbe.dylib`, the managed declarations use the matching calling convention:

```csharp
internal static class NativeMethods
{
    [DllImport("LayoutProbe", EntryPoint = "make_native_reading")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvSwift) })]
    internal static extern NativeReadingStorage MakeNumber(long value);

    [DllImport("LayoutProbe", EntryPoint = "read_native_reading")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvSwift) })]
    internal static extern long Read(NativeReadingStorage value);
}
```

`MakeNumber(42)` returned `Number = 42` and `Tag = 0`. Passing it back to `Read` returned 42. Passing `First = 1`, `Second = 2`, and `Tag = 1` returned 3, and passing `Tag = 2` returned -1.

</details>

The managed explicit-layout type's array stride was 9, while Swift's was 16. Matching the bytes of one value therefore does not establish compatibility for arrays or enclosing aggregates. A generator can emit a marshaller that copies each element at the native stride. General direct representation requires runtime layout support for the relevant size, stride, alignment, and calling-convention rules.

### Spare Bits and Generic Payloads

A frozen Swift enum with `none` and `bit(Bool)` used one byte in the same experiment: `bit(false)` was `0x00`, `bit(true)` was `0x01`, and `none` was `0x02`. A generator for that concrete binding can encode and decode those bytes with a checked switch. A separate tag byte would give the wrong representation, and reading a tagged byte as an ordinary C# `bool` would lose the distinction.

For an open generic payload, the usable spare bits and discriminator location can depend on the actual runtime type. A general generator cannot bake in a single offset and mask. Supporting that strategy requires runtime operations that identify the case, initialize its discriminator, and project its payload while preserving shared bits, or Swift-side helper functions that perform those operations. Merely computing the containing type's size does not supply those access operations.

Concrete frozen bindings can generate the access code demonstrated above. Bindings for resilient Swift enums need Swift metadata and value-witness operations or a marshalling boundary that keeps their representation opaque. Swift-owned indirect cases and objects also require retain/release and ownership conversion. A generator cannot insert retain/release into ordinary struct copies. Such cases need a managed owner and a marshalling boundary, or lifetime support from the language or runtime. A managed `Add` reference is a GC reference and cannot serve as a Swift object pointer.

The generator can therefore support bounded native strategies today, while the wider Swift layout kinds remain separate runtime capabilities. It must reject a native strategy when it cannot establish the complete layout and access contract.

## Applying the Mechanism to the Other Designs

The same implementation mechanism can serve ordinary `union X(A, B)` declarations. Their arbitrary existing types require conservative typed storage or boxing where needed to preserve the [creation-equivalence and reference-identity requirements](union-enum-non-boxed-layout.md#scope). This does not imply adding the declared-record cases' equality policy to those unions.

An `enum struct` could also delegate its storage to a generator, adding its own conversion and query members to the required shape. The shared pools, typed fields, runtime overlap, and packing checks remain reusable. The compiler would need an equivalent representation-delegation hook for that declaration form.

Mixed per-case kinds do not transfer directly to `enum class`: its cases derive from the enclosing record, which a record struct cannot do. The union approach can mix the kinds because its relationship to each case is expressed by construction and access members. This exploration leaves the existing enum-based proposal's type model intact.

## Evidence and Remaining Language Work

The expanded `Expr` was compiled and exercised with .NET 11 RC1 on ARM64. Tests covered union conversions and recursive patterns, typed and boxed access, failed extraction, null payloads and null class inputs, default values, recursive equality, hashing, and `NaN`. Additional variants verified designated-default cases and their distinction from null class inputs in both compact and single-reference storage. A warmed 100,000-iteration loop performing typed leaf construction, pattern extraction, equality, and hashing allocated zero bytes on its thread. The enclosing struct occupied 24 bytes, compared with the default object-backed wrapper's single reference. This also makes each `Add` larger, trading larger child fields for fewer leaf allocations.

The runtime experiment also verified generic `CUnion` sizes for eight- and twenty-four-byte payloads and rejection of a reference-containing instantiation. Swift 6.4 experiments measured the native layouts above and exercised native Swift calls from CoreCLR.

These checks validate the emitted representation and its consumption through existing union support. A generator was not built. The remaining compiler work is the `case struct` declaration syntax, exposing case symbols to generators, the attribute for selecting generated storage, and validation of the completed partial union against the shared public contract. The [case-construction binding](union-enum-common.md#binding-case-access) and metadata work already tracked by the main proposals still applies.
