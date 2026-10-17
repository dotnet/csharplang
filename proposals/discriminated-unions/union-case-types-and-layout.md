# Per-Case Records and Generated Union Layouts

This document explores separating two choices in the [union-based design](discriminated-unions.md): the record kind of each declared case and the storage representation of the enclosing union. The enclosing declaration uses `union` for every storage strategy. Each case chooses `record class` or `record struct`, and an attribute can select a source-generated representation.

Different applications make different tradeoffs between allocations, value size, copying, and the work needed to pack and unpack payloads. [Earlier implementation discussions](../../meetings/working-groups/discriminated-unions/Union%20implementation%20challenges.md#challenges-with-union-structs) highlight how rearranging payloads for compact storage can offset the benefits of avoiding allocations.

The language would define the required behavior and member shapes while leaving storage policy to the implementation. We could ship generators for common strategies without making their policies part of the language. Authors could supply their own implementations or select third-party generators through attributes. Choosing an attribute is less direct than using a keyword, but lets strategies evolve independently and avoids committing the language to one balance of these costs.

## Example Walkthrough

An expression tree can use struct leaves for efficiency and class nodes for recursion:

```csharp
[UnionLayout(typeof(CompactLayout))]
public readonly partial union Expr
{
    case struct Number(int Value);
    case struct Name(string Identifier);
    case Add(Expr Left, Expr Right);
}

Expr expression = Expr.Add(Expr.Number(1), Expr.Name("x"));

static int Evaluate(Expr expression, IReadOnlyDictionary<string, int> names)
    => expression switch
    {
        Expr.Number(var value) => value,
        Expr.Name(var name) => names[name],
        Expr.Add(var left, var right) => Evaluate(left, names) + Evaluate(right, names),
    };
```

To see what `CompactLayout` generates, jump to [Expanded Storage and Access Code](#expanded-storage-and-access-code).

`case` declares a sealed nested [`record class`](../csharp-9.0/records.md). `case struct` declares a nested [`record struct`](../csharp-10.0/record-structs.md), with the enclosing union's readonlyness. The parameter list is the record's primary constructor. The enclosing union remains a struct in every representation.

Without the attribute, ordinary object-backed storage boxes `Number` and `Name` when they enter the union and stores an `Add` reference directly. The illustrated compact strategy stores a number in an `int` field, a name's identifier in a reference field, or an `Add` object in that same reference field. Creating and extracting the leaves then requires no boxing. An `Add` retains its object identity.

Because `Add` is a class, its child expressions live in a separate object. If `Add` were a struct, a layout strategy would need to box it to avoid making `Expr` contain itself.

## Compiler and Generator Responsibilities

The [current union declaration](../csharp-15.0/unions.md#union-declarations) synthesizes an object-backed `Value` property and case constructors. It prohibits additional instance fields and a union member provider. A generator cannot replace that representation by adding a partial declaration.

We propose recognizing a `UnionLayoutAttribute` in `System.Runtime.CompilerServices` as a request for an explicitly supplied representation, whether handwritten or generated. The compiler would:

1. Require the union and any enclosing types to be partial.
2. Declare the nested case records before generators run. A generator can find the attribute with [`ForAttributeWithMetadataName`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.syntaxvalueprovider.forattributewithmetadataname) and inspect case symbols, primary constructors, constraints, and readonlyness without depending on another generator's output.
3. Suppress the default storage and implementation members, allowing the generator to supply them in matching `partial union` declarations.
4. Validate the completed union. Its creation members must describe exactly the declared cases, and its required access and equality members must be present. Missing implementation is an error, even when no generator is installed.

The compiler validates member shapes. Their behavior follows the same trust model as [custom unions](../csharp-15.0/unions.md#well-formedness).

`CompactLayout` is a marker type supplied by a generator package, giving the generator a compiler-checked strategy identity. The type supplies no implementation. The selector's final form remains open: an enum could name a fixed set of strategies, or string identifiers could allow an extensible set without marker types.

All unions with case declarations, including the default object-backed form, should expose the same per-case constructors, `Value`, `HasValue`, typed `TryGetValue` overloads, and [shared equality members](union-enum-common.md#equality-and-hashing). They also declare and implement a public nested `IUnionMembers` provider whose static `Create` overloads identify the declared cases. [Union conversions](../csharp-15.0/unions.md#union-member-providers) call those factories, so other constructor overloads do not add case types. Switching strategies must preserve this public contract for previously compiled consumers.

The generator owns the creation and access members that implement its storage strategy. Conflicting user declarations are errors. Other user-declared members remain supported, including equality customizations that use public case access under the shared customization rules. User-declared instance fields remain prohibited.

Existing unions of arbitrary types keep their current default behavior.

## Expanded Storage and Access Code

The generator contributes the following partial declaration:

<details>
<summary>Generated C# for Expr</summary>

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

// The compiler supplies Expr's nested case records and IUnion implementation.
public readonly partial union Expr : Expr.IUnionMembers, IEquatable<Expr>
{
    private const byte NumberCase = 1, NameCase = 2, AddCase = 3;
    private readonly byte _case;
    private readonly int _number;
    private readonly object? _reference;

    private Expr(byte caseId, int number, object? reference)
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
        public static Expr Create(Number value) => new Expr(value);
        public static Expr Create(Name value) => new Expr(value);
        public static Expr Create(Add value) => new Expr(value);

        bool HasValue { get; }
        object? Value { get; }

        bool TryGetValue(out Number value);
        bool TryGetValue(out Name value);
        bool TryGetValue([NotNullWhen(true)] out Add? value);
    }

    public bool HasValue => _case != 0;

    public object? Value
        => _case switch
        {
            0 => null,
            NumberCase => Expr.Number(_number),
            NameCase => Expr.Name((string)_reference!),
            AddCase => (Add?)_reference,
        };

    public bool TryGetValue(out Number value)
    {
        value = _case == NumberCase ? Expr.Number(_number) : default;
        return _case == NumberCase;
    }

    public bool TryGetValue(out Name value)
    {
        value = _case == NameCase ? Expr.Name((string)_reference!) : default;
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
            NumberCase => EqualityComparer<int>.Default.Equals(_number, other._number),
            NameCase => EqualityComparer<string?>.Default.Equals((string?)_reference, (string?)other._reference),
            AddCase => EqualityComparer<Add?>.Default.Equals((Add?)_reference, (Add?)other._reference),
        });

    public override bool Equals(object? obj) => obj is Expr other && Equals(other);

    public override int GetHashCode()
        => _case switch
        {
            0 => 0,
            NumberCase => HashCode.Combine(NumberCase, _number),
            NameCase => HashCode.Combine(NameCase, (string?)_reference),
            AddCase => HashCode.Combine(AddCase, (Add?)_reference),
        };

    public static bool operator ==(Expr left, Expr right) => left.Equals(right);
    public static bool operator !=(Expr left, Expr right) => !(left == right);
}
```

</details>

`Name.Identifier` and the `Add` reference share one GC reference slot. The casts check the reference actually read, preserving type safety if a concurrent struct copy tears. Every constructor initializes all storage, so changing cases does not retain an unused reference. The [existing concurrency requirements](union-enum-non-boxed-layout.md#concurrent-access-and-safety) apply unchanged.

Equality and hashing use typed payloads without reading `Value`, following the [shared value-equality rules](union-enum-common.md#equality-and-hashing).

## Storage Strategies

These strategies build on the [non-boxed storage exploration](union-enum-non-boxed-layout.md). The examples show instance fields and omit the creation, access, and equality members illustrated above.

### Default Object-Backed Storage

Without any layout attribute, the compiler supplies the ordinary object-backed representation. For `Expr`, all case values occupy the same field:

```csharp
private readonly object? _value; // boxed Number, boxed Name, or an Add reference
```

This keeps the enclosing union small regardless of the case sizes. Struct cases are boxed on entry, while class cases retain their original references. It is useful when minimizing the wrapper's size matters more than avoiding those allocations.

### Separate Case Fields

A strategy selected with `[UnionLayout(typeof(SeparateFieldsLayout))]` could give each case its own typed field. For the walkthrough's `Expr`, it would emit:

```csharp
private readonly byte _case;
private readonly Number _number;
private readonly Name _name;
private readonly Add? _add;
```

Creation and extraction can copy complete case values directly. This can be useful when minimizing conversion work matters more than the enclosing union's size. Each instance reserves space for every case, including inactive ones. Whole-value storage also accommodates opaque structs without decomposing their state.

### Shared Pools

The walkthrough's `CompactLayout` shares a reference slot between cases. More varied payloads can share primitive storage too:

```csharp
[UnionLayout(typeof(CompactLayout))]
public readonly partial union Message<T>
{
    case struct Text(string Content, int Code);
    case struct Buffer(byte[] Data, int Offset, int Count);
    case struct Sequence(long Value);
    case struct Item(T Value);
}
```

A generator could emit these fields:

```csharp
private readonly byte _case;
private readonly NumericStorage _numeric; // Text.Code, Buffer.Offset/Count, or Sequence.Value
private readonly object? _reference;      // Text.Content or Buffer.Data
private readonly T _item;                 // Item.Value
```

The nongeneric helper overlaps the primitive payloads:

```csharp
[StructLayout(LayoutKind.Explicit)]
internal struct NumericStorage
{
    [FieldOffset(0)] public int First;  // Text.Code or Buffer.Offset
    [FieldOffset(4)] public int Second; // Buffer.Count
    [FieldOffset(0)] public long Wide;  // Sequence.Value
}
```

`T` stays in a typed field so the runtime can determine its size and GC references.

This is useful when many cases can reuse a few storage locations. It reduces reserved space at the cost of rearranging payloads during creation and extraction. Each pool is sized independently, so their combined size can exceed that of any individual case.

The runtime computes alignment from the typed fields. The explicit-layout helper must be outside any generic containing type because ordinary explicit-layout generic types are unsupported.

Decomposition is safe for the compiler-defined case records with only synthesized primary-constructor state. Arbitrary nested structs, external types, and type parameters retain typed storage. If future case bodies add state or user-defined accessors, the generator must preserve the complete case value or use an explicit decomposition contract. Running user getters or reconstructing through a validating constructor would change semantics.

The [checked packing opt-in](union-enum-non-boxed-layout.md#possible-checked-packing-opt-in) can likewise be implemented by a generator. It emits the consumer-side size and reference-containment checks and ensures they run before every raw packing or extraction path, including extraction from `default`. A static constructor on the payload type alone remains insufficient.

### Hybrid Storage

A generator can store selected struct cases inline and box others. For example, consider a variant of `Expr` where `Add` is also a struct case:

```csharp
case struct Add(Expr Left, Expr Right);
```

Storing that case inline would require `Expr` to contain itself. A strategy selected with `[UnionLayout(typeof(HybridLayout))]` can instead emit:

```csharp
private readonly byte _case;
private readonly int _number;
private readonly object? _reference; // Name.Identifier or a boxed Add
```

`Number` and `Name` still avoid boxing. The generator boxes `Add` on entry and unboxes it for typed extraction, keeping the recursive layout finite. This lets `Add` remain a record struct when used independently of the union. A policy could also box unusually large struct cases to limit the union's size.

### Runtime-Computed Unmanaged Overlap

When payloads are unmanaged, runtime-computed overlap can avoid the separate typed slots needed for otherwise opaque type parameters. For example:

```csharp
[UnionLayout(typeof(UnmanagedLayout))]
public readonly partial union Sample<T> where T : unmanaged
{
    case struct Item(T Value);
    case struct Counter(long Value);
}
```

The generator can represent both cases with one payload region and a case discriminator:

```csharp
private readonly byte _case;
private readonly UnmanagedStorage<T> _payload;
```

Current .NET 11 sources contain [`ExtendedLayoutAttribute`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/ExtendedLayoutAttribute.cs) and the `CStruct` and `CUnion` members of [`ExtendedLayoutKind`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/ExtendedLayoutKind.cs). A generator can emit the payload helper as:

```csharp
[ExtendedLayout(ExtendedLayoutKind.CUnion)]
internal struct UnmanagedStorage<T> where T : unmanaged
{
    public T Item;
    public long Integer;
}
```

The runtime overlaps the fields at offset zero and computes their maximum size and alignment for the actual `T`. This is useful when a generator cannot know `T`'s byte size at compile time but wants the two cases to share storage. The runtime rejects instantiations containing GC references, including ones constructed through reflection that bypass the C# `unmanaged` check.

This requires a compiler and runtime that support extended layout. A generator can select a conservative typed-field implementation on older targets when its policy permits that choice, or report that the requested strategy is unsupported.

## Defaults and Observable Behavior

Case record kinds and the designated default case are declaration semantics. A layout strategy must preserve them. This exploration retains `= default` for struct cases, with the [existing default-payload and nullability rules](union-enum-common.md#nullability-of-default-cases).

In the expanded `Expr`, zero-initialization represents no case. If `Number` were designated `default`, the generator would map the zero discriminator to `Number(0)`. It would also reserve a separate no-case encoding for a null reference supplied through `Create(Add)`. Mapping that null to the active default case would violate the existing union creation rules. `Value` therefore remains nullable when null class inputs are possible, even with a designated default case.

Object-backed storage can make the same distinction with one field: null storage denotes the designated default, while a private sentinel denotes no case. Its `Value` accessor translates the sentinel to null and null storage to the designated case value. A compact representation can instead reserve a nonzero discriminator for no case.

For struct cases, `Value` may box a fresh copy on each access. Its contract cannot guarantee box identity, even if the object-backed implementation retains the original box.

Layout choices also affect `Unsafe.SizeOf`, allocation, array density, and unmanaged classification. Native interop can make layout an ABI contract. These consequences need explicit strategy contracts, even though the ordinary managed field arrangement remains an implementation detail. Compiler and generator upgrades must preserve the public members and any promised native representation.

## Swift Interop

A native binding may need the union to match an existing enum's ABI. [Runtime issue #100896](https://github.com/dotnet/runtime/issues/100896) is directly relevant to moving layout policy into attributes and generators. Its [approved API](https://github.com/dotnet/runtime/issues/100896#issuecomment-2110876749) uses `LayoutKind.Extended`, `ExtendedLayoutAttribute`, and kinds including `CUnion`, `SwiftStruct`, and `SwiftEnum`. The current implementation exposes the two C layout kinds. The issue remains open for the remaining kinds.

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

On Swift 6.4 and .NET 11 RC1, passing this storage through native Swift functions with [`CallConvSwift`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callconvswift) succeeded for number round-trips and pair and empty decoding.

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

## Applying the Mechanism to the Other Designs

The same implementation mechanism can serve ordinary `union X(A, B)` declarations. Their arbitrary existing types require conservative typed storage or boxing where needed to preserve the [creation-equivalence and reference-identity requirements](union-enum-non-boxed-layout.md#scope). This does not imply adding the declared-record cases' equality policy to those unions.

An `enum struct` could also delegate its storage to a generator, adding its own conversion and query members to the required shape. The shared pools, typed fields, runtime overlap, and packing checks remain reusable. The compiler would need an equivalent representation-delegation hook for that declaration form.

Mixed per-case kinds do not transfer directly to `enum class`: its cases derive from the enclosing record, which a record struct cannot do. The union approach can mix the kinds because its relationship to each case is expressed by construction and access members. This exploration leaves the existing enum-based proposal's type model intact.

## Evidence

The storage and access implementation was exercised as a custom union on .NET 11 RC1 ARM64. Tests covered conversions, recursive patterns, typed and boxed access, failed extraction, null payloads and class inputs, designated defaults, equality, and hashing. A warmed 100,000-iteration loop performing typed leaf construction, pattern extraction, equality, and hashing allocated zero bytes on its thread. The enclosing struct occupied 16 bytes, compared with the object-backed wrapper's single reference. This also makes each `Add` larger, trading larger child fields for fewer leaf allocations.

Additional probes checked separate-field, shared-pool, and hybrid representations, including a boxed recursive struct case.

No generator or compiler extension was built. The proposed output requires the [compiler changes described above](#compiler-and-generator-responsibilities). [Case-construction binding](union-enum-common.md#binding-case-access) and metadata remain shared design work.
