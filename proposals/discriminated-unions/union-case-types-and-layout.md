# Generated Union Layouts

This proposal lets attributes select source-generated storage for a [union](discriminated-unions.md), independently of its declared cases. The union proposal defines `case` and `case struct`, their record types, and their semantics. This document explores how generators can implement those semantics with different layouts.

## Motivation

Applications have different requirements and constraints around allocation, value size, copying, runtime support, and interoperability. Authors should be able to choose storage strategies that meet those needs, balancing the [tradeoffs](../../meetings/working-groups/discriminated-unions/Union%20implementation%20challenges.md#challenges-with-union-structs) for their workloads.

The language would define case semantics while generators supply storage strategies. Attributes would let authors choose strategies supplied by our generators or third-party generators. Choosing an attribute is less direct than using a keyword, but lets strategies evolve without committing the language to one balance of these costs.

The layouts below illustrate the flexibility of the extension point across allocation, value size, copying, safety, and interoperability requirements. They are exploratory examples with varying degrees of validation. Their inclusion does not imply a commitment to ship each strategy or a conclusion that every design is complete, correct, or suitable for adoption.

## Example Walkthrough

An expression tree can use struct leaves for efficiency and class nodes for recursion:

```csharp
[UnionLayout(Layout = .Compact)]
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

To see the generated code, jump to [Expanded Storage and Access Code](#expanded-storage-and-access-code).

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

The examples use an enum-valued `Layout` property to select a strategy. The final selection mechanism remains open and could use enums, named constants, types, or another approach.

Generated representations implement the union proposal's [construction and access APIs](discriminated-unions.md#access-apis), including its `IUnionMembers` provider, and the [shared equality members](union-enum-common.md#equality-and-hashing). Switching strategies must preserve these public members and the specified case semantics, including rejection of null case references.

The generator owns the creation and access members that implement its storage strategy. Conflicting user declarations are errors. Other user-declared members remain supported, including equality customizations that use public case access under the shared customization rules. User-declared instance fields remain prohibited.

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

Each strategy must specify its supported compiler and runtime targets. Unsupported declarations or targets require a documented fallback that preserves case semantics or a generator diagnostic. For example, separate fields cannot directly store a recursive struct case, and unmanaged overlap cannot store a string payload. A fallback may use boxing or separate typed fields, according to the strategy's policy.

### Default Object-Backed Storage

Without any layout attribute, the compiler supplies the ordinary object-backed representation. For `Expr`, all case values occupy the same field:

```csharp
private readonly object? _value; // boxed Number, boxed Name, or an Add reference
```

This is simple and keeps every union value to one reference, making the wrapper cheap to copy. Class cases retain their original references. Struct cases require boxing on entry and unboxing for typed extraction, adding allocations, GC pressure, and indirection.

### Separate Case Fields

A strategy selected with `[UnionLayout(Layout = .SeparateFields)]` could give each case its own typed field. For the walkthrough's `Expr`, it would emit:

```csharp
private readonly byte _case;
private readonly Number _number;
private readonly Name _name;
private readonly Add? _add;
```

This is simple to generate and makes reads and writes straightforward: creation and extraction copy complete case values directly, without boxing or rearranging payloads. It also accommodates opaque structs. Every instance reserves space for all cases, including inactive ones. Many or large struct cases can therefore make the union very large, increasing memory use and copying costs.

### Compact Storage

This follows the [conservative layout](union-enum-non-boxed-layout.md#conservative-storage) in the non-boxed-storage proposal. The walkthrough's `.Compact` strategy shares a reference slot between cases. More varied payloads can share primitive storage too:

```csharp
[UnionLayout(Layout = .Compact)]
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

On targets supporting [`ExtendedLayoutAttribute`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/ExtendedLayoutAttribute.cs), a generator can use the `CStruct` and `CUnion` members of [`ExtendedLayoutKind`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/ExtendedLayoutKind.cs) to group the integers and overlap that group with the `long`:

```csharp
[ExtendedLayout(ExtendedLayoutKind.CStruct)]
internal struct IntPair
{
    public int First;  // Text.Code or Buffer.Offset
    public int Second; // Buffer.Count
}

[ExtendedLayout(ExtendedLayoutKind.CUnion)]
internal struct NumericStorage
{
    public IntPair Pair;
    public long Wide; // Sequence.Value
}
```

The runtime determines field offsets, size, and alignment for the target platform. The pair could also be represented by an [inline array](../csharp-12.0/inline-arrays.md) of two `int` values inside the `CUnion`. Only the unmanaged numeric alternatives overlap. References and `T` stay in separate fields so the runtime can determine their GC layout.

Creation and extraction use the same typed fields: `Buffer` writes and reads `Pair.First` and `Pair.Second`, while `Sequence` writes and reads `Wide`. These operations preserve payload values regardless of byte order.

This strategy aims to minimize union size by decomposing struct cases and reusing fields, without boxing. The tradeoff is rearranging ("swizzling") payloads during creation and extraction, along with type checks when reading shared reference fields. The generated code is more complex than with separate case fields. Primitive storage, reference slots, and typed slots are sized independently, so their combined size can exceed that of any individual case. The size benefit must be weighed against packing, extraction, and JIT optimization costs on the intended runtimes and workloads.

Decomposition is safe for the compiler-defined case records with only synthesized primary-constructor state. Arbitrary nested structs, external types, and type parameters retain typed storage. If future case bodies add state or user-defined accessors, the generator must preserve the complete case value or use an explicit decomposition contract. Running user getters or reconstructing through a validating constructor would change semantics.

The [checked packing opt-in](union-enum-non-boxed-layout.md#possible-checked-packing-opt-in) can likewise be implemented by a generator. It emits the consumer-side size and reference-containment checks and ensures they run before every raw packing or extraction path, including extraction from `default`. A static constructor on the payload type alone remains insufficient.

### Hybrid Storage

A generator can store selected struct cases inline and box others. For example, consider a variant of `Expr` where `Add` is also a struct case:

```csharp
case struct Add(Expr Left, Expr Right);
```

Storing that case inline would require `Expr` to contain itself. A strategy selected with `[UnionLayout(Layout = .Hybrid)]` can instead emit:

```csharp
private readonly byte _case;
private readonly int _number;
private readonly object? _reference; // Name.Identifier or a boxed Add
```

`Number` and `Name` avoid boxing, while boxing `Add` keeps the recursive layout finite. `Add` remains a record struct when used independently. The same policy could limit union size by boxing unusually large cases. Boxed cases incur allocation, GC pressure, and unboxing costs, and the generator must support both storage paths.

### Runtime-Computed Unmanaged Overlap

When payloads are unmanaged, runtime-computed overlap can avoid the separate typed slots needed for otherwise opaque type parameters. For example:

```csharp
[UnionLayout(Layout = .Unmanaged)]
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

A generator targeting a compiler and runtime with extended-layout support can emit:

```csharp
[ExtendedLayout(ExtendedLayoutKind.CUnion)]
internal struct UnmanagedStorage<T> where T : unmanaged
{
    public T Item;
    public long Integer;
}
```

The runtime overlaps the fields at offset zero and computes their maximum size and alignment for the actual `T`. This combines compact storage with direct whole-payload reads and writes, even when the generator cannot know `T`'s byte size. The runtime rejects instantiations containing GC references, including ones constructed through reflection that bypass the C# `unmanaged` check.

## Defaults and Observable Behavior

Layout strategies preserve the union proposal's [default-case semantics](discriminated-unions.md#default-values).

In the expanded `Expr`, zero-initialization represents no case, so `Value` is nullable. If `Number` were designated `default`, zero-initialization would instead represent `Number(0)` and `Value` would be non-nullable. Object-backed storage can use null storage for that designated case, without a separate no-case encoding.

Layout choices also affect `Unsafe.SizeOf`, allocation, array density, and unmanaged classification. Native interop can make layout an ABI contract. These consequences need explicit strategy contracts, even though the ordinary managed field arrangement remains an implementation detail. Compiler and generator upgrades must preserve the public members and any promised native representation.

## Swift Interop

A native binding may need to match a Swift enum's layout and calling convention. A binding generator could target the Swift layout support proposed in [runtime issue #100896](https://github.com/dotnet/runtime/issues/100896). Its [approved API](https://github.com/dotnet/runtime/issues/100896#issuecomment-2110876749) includes `ExtendedLayoutKind.SwiftStruct`, `SwiftEnum`, and a `RequiredDiscriminatorBits` property. The runtime would compute the native layout, while the generator supplies the union's construction, access, and native-call members.

For example, consider this Swift type:

```swift
@frozen public enum NativeReading {
    case number(Int64)
    case pair(Int32, Int32)
    case empty
}
```

The C# declaration can select a strategy supplied by a binding generator:

```csharp
[UnionLayout(Layout = .Swift)]
public readonly partial union NativeReading
{
    case struct Number(long Value) = default;
    case struct Pair(int First, int Second);
    case struct Empty;
}
```

Using the proposed runtime API, the generator could emit:

```csharp
[ExtendedLayout(ExtendedLayoutKind.SwiftEnum, RequiredDiscriminatorBits = 2)]
public readonly partial union NativeReading
{
    [ExtendedLayout(ExtendedLayoutKind.SwiftStruct)]
    private struct NumberPayload
    {
        public long Value;
    }

    [ExtendedLayout(ExtendedLayoutKind.SwiftStruct)]
    private struct PairPayload
    {
        public int First;
        public int Second;
    }

    private readonly NumberPayload _number;
    private readonly PairPayload _pair;

    // Construction, access, and equality members omitted.
}
```

`SwiftStruct` describes each case's associated data. `SwiftEnum` combines those payloads with room for a discriminator identifying the three cases. The data-free `Empty` case needs only a discriminator encoding. For this native type, all-zero storage represents `number(0)`, matching the designated C# default case.

Native-call declarations can then use the union directly with [`CallConvSwift`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callconvswift):

```csharp
internal static partial class NativeMethods
{
    [DllImport("LayoutProbe", EntryPoint = "make_native_reading")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    internal static extern NativeReading MakeNumber(long value);

    [DllImport("LayoutProbe", EntryPoint = "read_native_reading")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    internal static extern long Read(NativeReading value);
}
```

For Swift functions exporting these names, `MakeNumber(42)` would produce `NativeReading.Number(42)`, and `Read(NativeReading.Pair(1, 2))` would return 3.

This strategy depends on the proposed Swift layout support. The current implementation exposes `CStruct` and `CUnion`. Construction and extraction also need runtime operations or Swift helpers to initialize and inspect the active case, as [discussed in the runtime proposal](https://github.com/dotnet/runtime/issues/100896#issuecomment-2065428148). A binding generator must define its supported types, ABI, and ownership handling.

Local probes with Swift 6.4 and .NET 11 RC1 CoreCLR on ARM64 verified a concrete representation of this enum through native arguments, returns, `inout` mutation, buffers, and an enclosing struct. The generated layout above targets the proposed runtime API.

## Applying the Mechanism to the Other Designs

The same implementation mechanism can serve ordinary `union X(A, B)` declarations. Their arbitrary existing types require conservative typed storage or boxing where needed to preserve the [creation-equivalence and reference-identity requirements](union-enum-non-boxed-layout.md#scope). This does not imply adding the declared-record cases' equality policy to those unions.

An `enum struct` could also delegate its storage to a generator, adding its own conversion and query members to the required shape. Compact storage, separate fields, runtime overlap, and packing checks remain reusable. The compiler would need an equivalent representation-delegation hook for that declaration form.

## Evidence

The storage and access implementation was exercised as a custom union on .NET 11 RC1 ARM64. Tests covered conversions, recursive patterns, typed and boxed access, failed extraction, null payloads, rejection of null class inputs, designated defaults, equality, and hashing. A warmed 100,000-iteration loop performing typed leaf construction, pattern extraction, equality, and hashing allocated zero bytes on its thread. The enclosing struct occupied 16 bytes, compared with the object-backed wrapper's single reference. This also makes each `Add` larger, trading larger child fields for fewer leaf allocations.

Additional probes checked separate-field, compact, and hybrid representations, including a boxed recursive struct case. Extended-layout probes checked a `CStruct` pair and a `long` inside a `CUnion`, an inline-array pair alternative, and separate generic storage containing references.

No generator or compiler extension was built. The proposed output requires the [compiler changes described above](#compiler-and-generator-responsibilities). [Case-construction binding](union-enum-common.md#binding-case-access) and metadata remain shared design work.

## Further Extensions

For performance-sensitive workloads, a generator could offer an explicitly unsafe strategy that omits runtime type checks when extracting from shared reference slots. Such a strategy would rely on callers to prevent tearing, for example through synchronization or exclusive ownership.

If that requirement is violated, a torn read could combine a discriminator from one write with a reference from another. Unchecked access could then treat that reference as the wrong type, violating type and memory safety. This would be an explicit opt-in tradeoff for that strategy. Whether removing those checks materially improves performance would need measurement.
