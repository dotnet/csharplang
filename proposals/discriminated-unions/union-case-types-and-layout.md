# Per-Case Records and Generated Union Layouts

This document explores separating two choices in the [union-based design](discriminated-unions.md): the record kind of each declared case and the storage representation of the enclosing union. The enclosing declaration uses `union` for every storage strategy. Each case chooses `record class` or `record struct`, and an attribute can select a source-generated representation.

## Motivation

A recursive tree can benefit from class nodes and small struct leaves, independently of how the enclosing union stores them. Different applications make different tradeoffs between allocations, value size, copying, and payload rearrangement, as [earlier implementation discussions](../../meetings/working-groups/discriminated-unions/Union%20implementation%20challenges.md#challenges-with-union-structs) explore.

The language would define case semantics while generators supply storage strategies. Attributes would let authors choose strategies supplied by our generators or third-party generators. Choosing an attribute is less direct than using a keyword, but lets strategies evolve without committing the language to one balance of these costs.

The layouts below illustrate the flexibility of the extension point across allocation, value size, copying, safety, and interoperability requirements. They are exploratory examples with varying degrees of validation. Their inclusion does not imply a commitment to ship each strategy or a conclusion that every design is complete, correct, or suitable for adoption.

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
        null => throw new ArgumentException("The expression has no case.", nameof(expression)),
    };
```

To see what `CompactLayout` generates, jump to [Expanded Storage and Access Code](#expanded-storage-and-access-code).

`case` declares a sealed nested [`record class`](../csharp-9.0/records.md). `case struct` declares a nested [`record struct`](../csharp-10.0/record-structs.md), with the enclosing union's readonlyness. The parameter list is the record's primary constructor. The enclosing union remains a struct in every representation.

Without the attribute, ordinary object-backed storage boxes `Number` and `Name` when they enter the union and stores an `Add` reference directly. The illustrated compact strategy stores a number in an `int` field, a name's identifier in a reference field, or an `Add` object in that same reference field. Creating and extracting the leaves then requires no boxing. An `Add` retains its object identity.

Because `Add` is a class, its child expressions live in a separate object. If `Add` were a struct, a layout strategy would need reference indirection, such as boxing, to avoid making `Expr` contain itself.

## Compiler and Generator Responsibilities

The [current union declaration](../csharp-15.0/unions.md#union-declarations) synthesizes an object-backed `Value` property and case constructors. It prohibits additional instance fields and a union member provider. A generator cannot replace that representation by adding a partial declaration.

The attribute selects a generator's storage strategy. We propose also having the compiler recognize `UnionLayoutAttribute` in `System.Runtime.CompilerServices` so generated partial declarations can replace the default representation. The compiler would:

1. Require the union and any enclosing types to be partial.
2. Declare the nested case records before generators run. A generator can find the attribute with [`ForAttributeWithMetadataName`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.syntaxvalueprovider.forattributewithmetadataname) and inspect case symbols, primary constructors, constraints, and readonlyness without depending on another generator's output.
3. Suppress the default storage and implementation members, allowing the generator to supply them in matching `partial union` declarations.
4. Validate the completed union. Its creation members must describe exactly the declared cases, and its required access and equality members must be present. Missing implementation is an error, even when no generator is installed.

The compiler validates member shapes. Their behavior follows the same trust model as [custom unions](../csharp-15.0/unions.md#well-formedness).

`CompactLayout` is a marker type supplied by a generator package, giving the generator a compiler-checked strategy identity. The type supplies no implementation. The selector's final form remains open: an enum could name a fixed set of strategies, or string identifiers could allow an extensible set without marker types.

All unions with case declarations, including the default object-backed form, should expose the same per-case constructors, `Value`, `HasValue`, typed `TryGetValue` overloads, and [shared equality members](union-enum-common.md#equality-and-hashing). They also declare and implement a public nested `IUnionMembers` provider whose static `Create` overloads identify the declared cases. [Union conversions](../csharp-15.0/unions.md#union-member-providers) call those factories, so other constructor overloads do not add case types. Switching strategies must preserve these public members and the specified case semantics.

Class cases are non-nullable. Union constructors and `Create` factories taking such a case throw `ArgumentNullException` for a null case reference, including with the default object-backed representation.

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
    public Expr(Add value) : this(AddCase, 0, value ?? throw new ArgumentNullException(nameof(value))) { }

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

`Name.Identifier` and the `Add` reference share one GC reference slot. The casts check the reference actually read, preserving type safety if a concurrent struct copy tears. Every constructor initializes all storage, so changing cases does not retain an unused reference. This strategy follows the [existing concurrency requirements](union-enum-non-boxed-layout.md#concurrent-access-and-safety).

Equality and hashing use typed payloads without reading `Value`, following the [shared value-equality rules](union-enum-common.md#equality-and-hashing).

## Storage Strategies

These strategies build on the [non-boxed storage exploration](union-enum-non-boxed-layout.md). The examples show instance fields and omit the creation, access, and equality members illustrated above.

A strategy that cannot support a declaration must either use a documented fallback that preserves its case semantics or report a generator diagnostic. For example, separate fields cannot directly store a recursive struct case, and unmanaged overlap cannot store a string payload. A strategy may permit boxing as a fallback or reject those declarations.

### Default Object-Backed Storage

Without any layout attribute, the compiler supplies the ordinary object-backed representation. For `Expr`, all case values occupy the same field:

```csharp
private readonly object? _value; // boxed Number, boxed Name, or an Add reference
```

This is simple and keeps every union value to one reference, making the wrapper cheap to copy. Class cases retain their original references. Struct cases require boxing on entry and unboxing for typed extraction, adding allocations, GC pressure, and indirection.

### Separate Case Fields

A strategy selected with `[UnionLayout(typeof(SeparateFieldsLayout))]` could give each case its own typed field. For the walkthrough's `Expr`, it would emit:

```csharp
private readonly byte _case;
private readonly Number _number;
private readonly Name _name;
private readonly Add? _add;
```

This is simple to generate and makes reads and writes straightforward: creation and extraction copy complete case values directly, without boxing or rearranging payloads. It also accommodates opaque structs. Every instance reserves space for all cases, including inactive ones. Many or large struct cases can therefore make the union very large, increasing memory use and copying costs.

### Compact Storage

This follows the [conservative layout](union-enum-non-boxed-layout.md#conservative-storage) in the non-boxed-storage proposal. The walkthrough's `CompactLayout` shares a reference slot between cases. More varied payloads can share primitive storage too:

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

This strategy aims to minimize union size by decomposing struct cases and reusing fields, without boxing. The tradeoff is rearranging ("swizzling") payloads during creation and extraction, along with type checks when reading shared reference fields. The generated code is more complex than with separate case fields. Primitive storage, reference slots, and typed slots are sized independently, so their combined size can exceed that of any individual case.

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

`Number` and `Name` avoid boxing, while boxing `Add` keeps the recursive layout finite. `Add` remains a record struct when used independently. The same policy could limit union size by boxing unusually large cases. Boxed cases incur allocation, GC pressure, and unboxing costs, and the generator must support both storage paths.

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

The runtime overlaps the fields at offset zero and computes their maximum size and alignment for the actual `T`. This combines compact storage with direct whole-payload reads and writes, even when the generator cannot know `T`'s byte size. The runtime rejects instantiations containing GC references, including ones constructed through reflection that bypass the C# `unmanaged` check.

This requires a compiler and runtime that support extended layout. A generator can select a conservative typed-field implementation on older targets when its policy permits that choice, or report that the requested strategy is unsupported.

## Defaults and Observable Behavior

Case record kinds and the designated default case are declaration semantics. A layout strategy must preserve them. This exploration retains `= default` for struct cases, with the [existing default-payload and nullability rules](union-enum-common.md#nullability-of-default-cases).

In the expanded `Expr`, zero-initialization represents no case, so `Value` is nullable. If `Number` were designated `default`, zero-initialization would instead represent `Number(0)` and `Value` would be non-nullable. Object-backed storage can use null storage for that designated case, without a separate no-case encoding.

Rejecting null class inputs satisfies the [union well-formedness rules](../csharp-15.0/unions.md#well-formedness): a rejected input creates no union value, and every class-case creation member rejects null in the same way, preserving creation equivalence.

For struct cases, `Value` may box a fresh copy on each access. Its contract cannot guarantee box identity, even if the object-backed implementation retains the original box.

Layout choices also affect `Unsafe.SizeOf`, allocation, array density, and unmanaged classification. Native interop can make layout an ABI contract. These consequences need explicit strategy contracts, even though the ordinary managed field arrangement remains an implementation detail. Compiler and generator upgrades must preserve the public members and any promised native representation.

## Swift Interop

A specialized binding generator can provide working Swift interop for concrete, frozen enums with trivial payloads. With the proposed compiler hook, it can emit the native representation on the union itself, implement its case operations, and generate call declarations and marshalling. We verified the representation below with Swift 6.4 and .NET 11 RC1 CoreCLR on ARM64, including native arguments, returns, `inout` mutation, buffers, and an enclosing struct.

### A Concrete Frozen Enum

Consider this Swift type:

```swift
@frozen public enum NativeReading {
    case number(Int64)
    case pair(Int32, Int32)
    case empty
}
```

The C# declaration can select a strategy supplied by a binding generator for this Swift type:

```csharp
[UnionLayout(typeof(SwiftNativeReadingLayout))]
public readonly partial union NativeReading
{
    case struct Number(long Value) = default;
    case struct Pair(int First, int Second);
    case struct Empty;
}
```

For the tested target, the native payload occupies eight bytes and its discriminator is the ninth byte: `number` uses 0, `pair` uses 1, and `empty` uses 2. Its size is 9, stride is 16, and alignment is 8. The generator can emit this storage and construction code:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 9)]
public readonly partial union NativeReading
{
    [FieldOffset(0)] private readonly long _number;
    [FieldOffset(0)] private readonly int _first;
    [FieldOffset(4)] private readonly int _second;
    [FieldOffset(8)] private readonly byte _tag;

    public NativeReading(Number value)
    {
        this = default;
        _number = value.Value;
        _tag = 0;
    }

    public NativeReading(Pair value)
    {
        this = default;
        _first = value.First;
        _second = value.Second;
        _tag = 1;
    }

    public NativeReading(Empty value) { this = default; _tag = 2; }

    public bool HasValue => true;

    public object Value
        => _tag switch
        {
            0 => NativeReading.Number(_number),
            1 => NativeReading.Pair(_first, _second),
            2 => NativeReading.Empty,
        };

    // The union member provider, typed extraction, and equality members
    // follow the earlier example and add no instance storage.
}
```

`Number` is the designated default case because all-zero native storage represents `number(0)`. If a native type's zero representation is invalid, the generator must diagnose the unsupported declaration or emit boundary validation and marshalling that reject invalid managed defaults.

### Native Calls

For example, the Swift library can export functions using Swift's calling convention:

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

The generated declarations use [`CallConvSwift`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callconvswift) and the union type directly:

```csharp
internal static partial class NativeMethods
{
    [DllImport("LayoutProbe", EntryPoint = "make_native_reading")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvSwift) })]
    internal static extern NativeReading MakeNumber(long value);

    [DllImport("LayoutProbe", EntryPoint = "read_native_reading")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvSwift) })]
    internal static extern long Read(NativeReading value);
}
```

`MakeNumber(42)` produces `NativeReading.Number(42)`. Passing it back to `Read` returns 42. `Read(NativeReading.Pair(1, 2))` returns 3, and `Read(NativeReading.Empty)` returns -1. A Swift `inout NativeReading` parameter can similarly be bound as `ref NativeReading`.

### Buffers and Enclosing Structs

The nine-byte managed type has an array stride of 9, while Swift places these enum elements 16 bytes apart. A generated marshaller can allocate an eight-byte-aligned native buffer and copy nine bytes per element at sixteen-byte intervals. For example, given a Swift function taking `UnsafePointer<NativeReading>` and an `Int32` count and returning the sum of the readings, the generator can emit:

```csharp
internal static unsafe partial class NativeMethods
{
    [DllImport("LayoutProbe", EntryPoint = "sum_native_readings")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvSwift) })]
    private static extern long Sum(void* values, int count);

    internal static long Sum(ReadOnlySpan<NativeReading> values)
    {
        if (values.IsEmpty)
            return 0;

        void* buffer = NativeMemory.AlignedAlloc(checked((nuint)values.Length * 16), 8);
        try
        {
            for (int i = 0; i < values.Length; i++)
                Unsafe.WriteUnaligned((byte*)buffer + (nuint)i * 16, values[i]);

            return Sum(buffer, values.Length);
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }
}
```

Reading an element written by Swift uses `Unsafe.ReadUnaligned<NativeReading>((byte*)buffer + (nuint)i * 16)`. We verified native reads and writes using this representation. Swift's `Array<NativeReading>` collection has its own representation and would need a separate binding.

Using `Size = 16` for the union also worked for individual native calls and pinned managed arrays used as native buffers. That padding is not a universal solution: Swift can place a following `UInt8` field at offset 9 inside an enclosing struct. A binding generator must preserve that aggregate layout, for example:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 10)]
public struct NativeEnvelope
{
    [FieldOffset(0)] public NativeReading Reading;
    [FieldOffset(9)] public byte Marker;
}
```

We verified native arguments and returns for this aggregate with the nine-byte enum representation. Its native stride is again 16. Ordinary nesting of the sixteen-byte enum representation puts `Marker` at offset 16. The generator must account for native size, alignment, stride, and call conventions together.

### Limits and Runtime Support

A frozen Swift enum with `none` and `bit(Bool)` used one byte in the same experiment: `bit(false)` was `0x00`, `bit(true)` was `0x01`, and `none` was `0x02`. A generator for that concrete binding can encode and decode those bytes with a checked switch. A separate tag byte would give the wrong representation, and reading a tagged byte as an ordinary C# `bool` would lose the distinction.

For an open generic payload, the usable spare bits and discriminator location can depend on the actual runtime type. A general generator cannot bake in a single offset and mask. Supporting that strategy requires runtime operations that identify the case, initialize its discriminator, and project its payload while preserving shared bits, or Swift-side helper functions that perform those operations. Merely computing the containing type's size does not supply those access operations.

Bindings for resilient Swift enums likewise need Swift metadata and value-witness operations or a marshalling boundary that keeps their representation opaque. Swift-owned indirect cases and objects require retain/release and destruction operations. A generator cannot insert those operations into ordinary C# struct copies. Such cases need a managed owner and a marshalling boundary, or lifetime support from the language or runtime. A managed `Add` reference is a GC reference and cannot serve as a Swift object pointer.

[Runtime issue #100896](https://github.com/dotnet/runtime/issues/100896) addresses complementary layout support. Its [approved API](https://github.com/dotnet/runtime/issues/100896#issuecomment-2110876749) includes `ExtendedLayoutKind.SwiftStruct` and `SwiftEnum`, but the current implementation exposes only `CStruct` and `CUnion`. `CUnion` supplies overlapping storage, without supplying a Swift enum's discriminator or projection operations. Even full layout support would still need case access and ownership handling.

The demonstrated explicit-size representation works on the tested ARM64 CoreCLR. [Earlier runtime investigation](https://github.com/dotnet/runtime/issues/100896#issuecomment-2079979240) reported differences across runtimes, and Swift's spare-bit rules vary by architecture. A binding generator must target a supported ABI and runtime, use marshalling where necessary, or diagnose an unsupported combination. Ordinary source generation alone does not establish direct representation for arbitrary Swift enums.

## Applying the Mechanism to the Other Designs

The same implementation mechanism can serve ordinary `union X(A, B)` declarations. Their arbitrary existing types require conservative typed storage or boxing where needed to preserve the [creation-equivalence and reference-identity requirements](union-enum-non-boxed-layout.md#scope). This does not imply adding the declared-record cases' equality policy to those unions.

An `enum struct` could also delegate its storage to a generator, adding its own conversion and query members to the required shape. Compact storage, separate fields, runtime overlap, and packing checks remain reusable. The compiler would need an equivalent representation-delegation hook for that declaration form.

Mixed per-case kinds do not transfer directly to `enum class`: its cases derive from the enclosing record, which a record struct cannot do. The union approach can mix the kinds because its relationship to each case is expressed by construction and access members. This exploration leaves the existing enum-based proposal's type model intact.

## Evidence

The storage and access implementation was exercised as a custom union on .NET 11 RC1 ARM64. Tests covered conversions, recursive patterns, typed and boxed access, failed extraction, null payloads, rejection of null class inputs, designated defaults, equality, and hashing. A warmed 100,000-iteration loop performing typed leaf construction, pattern extraction, equality, and hashing allocated zero bytes on its thread. The enclosing struct occupied 16 bytes, compared with the object-backed wrapper's single reference. This also makes each `Add` larger, trading larger child fields for fewer leaf allocations.

Additional probes checked separate-field, compact, and hybrid representations, including a boxed recursive struct case.

No generator or compiler extension was built. The proposed output requires the [compiler changes described above](#compiler-and-generator-responsibilities). [Case-construction binding](union-enum-common.md#binding-case-access) and metadata remain shared design work.

## Further Extensions

For performance-sensitive workloads, a generator could offer an explicitly unsafe strategy that omits runtime type checks when extracting from shared reference slots. Such a strategy would rely on callers to prevent tearing, for example through synchronization or exclusive ownership.

If that requirement is violated, a torn read could combine a discriminator from one write with a reference from another. Unchecked access could then treat that reference as the wrong type, violating type and memory safety. This would be an explicit opt-in tradeoff for that strategy. Whether removing those checks materially improves performance would need measurement.
