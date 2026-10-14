# Discriminated Unions and Enhanced Enums for C#

This proposal introduces enhanced enums as an elegant way to build discriminated unions in C#. Building on [type unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md), enhanced enums provide familiar, concise syntax for algebraic sum types where cases are known at declaration time.

It consolidates design feedback from many years of repository discussions, especially [#113](https://github.com/dotnet/csharplang/issues/113) and related issues.

<details>
  <summary>Key discussion threads...</summary>

  - [#75](https://github.com/dotnet/csharplang/issues/75) - Early comprehensive discussion exploring DU syntax options including enum class
  - [#2962](https://github.com/dotnet/csharplang/discussions/2962) - Major discussion on Andy Gocke's DU proposal, debating enum class vs enum struct
  - [#7016](https://github.com/dotnet/csharplang/issues/7016) - Fast, efficient unions proposal focusing on struct-based implementations
  - [#3760](https://github.com/dotnet/csharplang/discussions/3760) - Community "shopping list" of desired discriminated union features
  - [#7544](https://github.com/dotnet/csharplang/issues/7544) - Simple encoding of unions exploring type unions vs tagged unions
  - [#8804](https://github.com/dotnet/csharplang/discussions/8804) - String-based enums for cloud services with extensibility needs
  - [#1860](https://github.com/dotnet/csharplang/issues/1860) - Long-running request for string enum support citing TypeScript/Java
  - [#9010](https://github.com/dotnet/csharplang/discussions/9010) - "Closed" enum types that guarantee exhaustiveness
  - [#6927](https://github.com/dotnet/csharplang/discussions/6927) - Constant enums discussion around strict value enforcement
  - [#7854](https://github.com/dotnet/csharplang/issues/7854) - Exhaustiveness checking for ADT patterns using private constructors
  - [#8942](https://github.com/dotnet/csharplang/discussions/8942) - Track subtype exhaustiveness for closed hierarchies
  - [#8926](https://github.com/dotnet/csharplang/discussions/8926) - Extensive discussion on Option<T> as canonical DU use case
  - [#7010](https://github.com/dotnet/csharplang/discussions/7010) - Union types discussion heavily featuring Option<T> and Result<T>
  - [#274](https://github.com/dotnet/csharplang/discussions/274) - Java-style class-level enums with methods and constructors
  - [#8987](https://github.com/dotnet/csharplang/discussions/8987) - Champion "permit methods in enum declarations"
  - [#5937](https://github.com/dotnet/csharplang/discussions/5937) - Smart Enums In C# Like Java" (extra state!)
  - [#782](https://github.com/dotnet/csharplang/discussions/782) Sealed enums (completeness checking in switch statements)
  - [#2669](https://github.com/dotnet/csharplang/discussions/2669) Feature request: Partial enums
</details>

## 1. Overview

The declaration forms below contrast combining existing types with defining named cases:

```csharp
// Type unions - combine existing case types
union Result(string, ValidationError, NetworkException);

// Shape enums - define data-carrying cases together
enum struct PaymentResult // or `enum class`
{
    Success(string transactionId),
    Declined(string reason),
    PartialRefund(string originalId, decimal amount)
}
```

## 2. Motivation and Design Philosophy

### Limitations of Current Enums

Today's C# enums have significant limitations:

1. **No associated data**: Cases are merely integral values
2. **Not truly exhaustive**: Any integer can be cast to an enum type
3. **Limited to integers**: Cannot use strings or doubles

Enhanced enums address all these limitations while preserving conceptual simplicity.

### Building on Familiar Concepts

By extending the existing `enum` keyword, enhanced enums provide a grow-up story. Simple enums remain simple, while advanced scenarios become possible without abandoning familiar patterns.

## 3. Type Unions and Shape Enums

[Type unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) can combine existing, possibly overlapping case types. Shape enums declare fresh, named cases together with the enum: `Left(int value)` and `Right(int value)` remain distinct even though both carry an `int`.

Shape enums specify their own [class](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#enum-class-translation) and [struct](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#enum-struct-translation) representations, conversions, and patterns. Whether to implement the type-union pattern is a separate choice discussed in [Appendix B](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#appendix-b-possible-connections-to-type-unions).

## 4. Enhanced Enums

### Syntax Extensions

Enhanced enums extend traditional enum syntax in three orthogonal ways:

#### Extended Base Types

Support any constant-bearing type:

```csharp
enum Traditional : int { A = 1, B = 2 }
enum Priority : string { Low = "low", Medium = "medium", High = "high" }
enum TranscendentalConstants : double { Pi = 3.14159, E = 2.71828 }
```

These compile to `System.Enum` subclasses with the appropriate `value__` backing field. Non-integral constant enums require explicit values for each member.

#### Shape Declarations

Use `enum class` or `enum struct` to declare a shape enum:

```csharp
enum class Result { Success, Failure }  // shape enum via 'class' keyword
enum struct Result { Success, Failure } // shape enum via 'struct' keyword
```

#### Data-Carrying Cases

```csharp
enum class Result // or enum struct
{
    Success(string id),
    Failure(int code, string message)
}
```

Cases, including data-free cases, generate nested record types. Their class/struct form follows the enum declaration; [Readonly, Copying, and `with`](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#readonly-copying-and-with) specifies mutability.

#### Case Construction Expressions

Ordinary enums use `Color.Red` to obtain a value. Adding a payload adds arguments to that form, giving `E.Case(arguments)` without requiring `new`.

```csharp
enum struct Option<T>
{
    None,
    Some(T value)
}

Option<int> empty = Option<int>.None;
Option<int> present = Option<int>.Some(42);

enum class State
{
    Ready,
    Running(int attempt)
}

State ready = State.Ready;
State running = State.Running(3);
```

A case declared without a parameter list is a value expression when accessed in a value context. A case declared with a parameter list is constructed by invoking its case name with the corresponding arguments, including an empty argument list for a declaration such as `Empty()`. Argument binding follows the generated case constructor's rules. Construction creates an active case and preserves its payload.

The type of either case construction expression is the enclosing enum type. A variable initialized from one case can therefore hold another:

```csharp
var option = Option<int>.Some(42); // Option<int>
option = Option<int>.None;
```

The nested case type remains available in type contexts, including type patterns, declarations, casts, and `typeof`. Direct construction with `new E.Case(arguments)` retains ordinary object-creation behavior and produces the nested case type. For example, `Option<int>.Some caseValue = new(42)` constructs the case value directly.

The compiler binds the case name according to its type, value, invocation, or pattern context and lowers enum construction through the case constructor and case-to-enum conversion. Metadata must identify these operations to consuming compilers; its encoding remains open.

Generated implementation members can have distinct metadata names; no ordinary same-name factory is needed beside the nested type, which C# would forbid. A user-declared member conflicting with a case name requires a diagnostic.

Target-typed access to these operations is discussed under [Contextual Case Construction and Inference](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#contextual-case-construction-and-inference).

#### Combination Rules

- **Constant enums**: Can use extended base types but NOT have parameter lists
- **Shape enums**: Can have parameter lists but NOT specify a base type
- **Mixing cases**: Cannot mix constant values and parameterized cases. For the `= default` form, see [Default Values](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#default-values).

```csharp
// ✓ Valid - constant enum with string base
enum Status : string { Active = "A", Inactive = "I" }

// ✓ Valid - shape enum with data
enum class Result { Ok(int value), Error(string msg) }

// ✗ Invalid - cannot mix constants and shapes
enum class Bad { A = 1, B(string x) }

// ✗ Invalid - shape enums cannot have base types
enum struct Bad : int { A, B }

// ✗ Invalid - Constant enums cannot have parameter lists
enum Bad { A(), B() }
```

For the complete formal grammar, see [Appendix A: Grammar Changes](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#appendix-a-grammar-changes).

### Shape Enums

```csharp
enum class FileOperation // or enum struct
{
    Open(string path),
    Close,
    Read(byte[] buffer, int offset, int count),
    Write(byte[] buffer)
}
```

#### Reference Type and Value Type

Choose `enum class` for reference-type cases or `enum struct` for inline value storage:

```csharp
// Allocation per construction
enum class Result { Ok(int value), Error(string message) }
var r1 = Result.Ok(42); // Heap allocation

// No allocation
enum struct Result { Ok(int value), Error(string message) }
var r2 = Result.Ok(42); // Inline case storage; no allocation for construction
```

#### Readonly, Copying, and `with`

An unmodified enum struct is mutable; `readonly enum struct` follows the ordinary [readonly struct rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1622-struct-modifiers), including the usual constructor and initialization exceptions. The modifier constrains instance state, not reassignment of a variable.

The generated case types follow the corresponding [record-struct rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#properties): a mutable enum struct generates mutable record-struct cases with `get`/`set` payload properties; a readonly enum struct generates readonly record-struct cases with `get`/`init` payload properties. This choice applies to generated cases. User-declared nested types follow the ordinary rules for nested declarations. Generated case queries, extraction methods, and equality members are readonly instance members.

Assignment follows ordinary class and struct rules: enum classes copy a reference; enum structs copy the discriminator and stored payload. Extracting a struct case produces a separate value whose mutation does not update the enum. Copies are shallow, preserving reference payload identity; `readonly` does not make referenced objects immutable.

Case types support `with` through their ordinary record behavior. A `with` expression on a class case clones that record; one on a struct case copies it and applies the member initializers. The result can be converted back to the enum:

```csharp
enum struct Progress
{
    Waiting,
    Running(int count)
}

Progress progress = Progress.Running(1);
Progress.Running running = (Progress.Running)progress;
running.count = 2; // changes the extracted case, not progress
int originalCount = ((Progress.Running)progress).count; // 1
progress = running with { count = 3 }; // assigns a new enum value
```

The same copy-and-update operation is available for readonly case records through their init-only properties:

```csharp
readonly enum struct Snapshot
{
    Ready,
    Running(int count)
}

Snapshot snapshot = Snapshot.Running(1);
Snapshot.Running running = (Snapshot.Running)snapshot;
// running.count = 2; // error: count is init-only
snapshot = running with { count = 2 }; // allowed: update a copy, then reassign
```

An enum class is itself a record base, so `e with { }` clones the runtime case record and preserves its case identity. An enum struct supports the ordinary [struct `with` expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#allow-with-expression-on-structs), so `e with { }` copies the enum value, including its active case or uninitialized state. Member initializers are bound against the receiver's static type. Case-specific payload properties are members of the case type, so updating them requires a case-typed receiver as above; they are not projected onto the enclosing enum. Changing cases constructs a new enum value.

#### Equality and Hashing

Equality and hashing follow the record nature of the cases. Class cases use their generated record equality, including case type identity. For a struct enum, synthesized equality requires the same discriminator and, for an active case, that the corresponding case records consider themselves equal. Case-record equality follows the ordinary record rules, including `EqualityComparer<T>.Default` for payload members. Different cases therefore compare unequal under synthesized equality even when their payloads are equal.

Both representations expose the ordinary record equality surface, including strongly typed `Equals`, `IEquatable<E>`, `Equals(object)`, and `==`/`!=` for the enum type `E`. Struct hashing follows the active case's record hashing and may incorporate case identity. These operations describe the logical case value: unused storage slots and padding do not participate, and an implementation need not reconstruct a case solely to perform them.

With synthesized equality, two uninitialized values of the same enum struct are equal and have the same hash code; they are unequal to any active case. A designated default case instead participates as that active case. Class null comparisons follow ordinary record reference equality. Hash values and the precise hash-combining algorithm remain implementation details.

#### Recursive Payloads

Enum structs obey the existing [struct value-semantics rules for cyclic instance-field dependencies](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1642-value-semantics). An inline case payload cannot make the enum depend on itself, directly or indirectly through other value types or constructed generic value types. Choosing shared storage does not relax that rule.

```csharp
enum struct Expr
{
    Number(int value),
    Add(Expr left, Expr right) // error: recursive value-type layout
}
```

The same declaration using `enum class` is permitted because its recursive payloads are references. A struct enum can likewise use a reference type, such as a class wrapper or an array, to break the layout dependency. Wrapping the payload in another value type, including `Nullable<Expr>`, does not itself introduce indirection.

#### Default Values

For an `enum class`, `default(E)` is `null` and represents no case, even if a data-free case is named `None`.

For an `enum struct`, discriminator zero denotes an *uninitialized value* unless a default case is designated. All ordinary cases have nonzero discriminators, including data-free cases. This accommodates declarations in which every case requires state without inventing a case containing default payload values.

```csharp
enum struct Option<T>
{
    None,
    Some(T value)
}

Option<int> uninitialized = default;       // no active case
Option<int> none = new Option<int>.None(); // active None case
Option<int> some = new Option<int>.Some(0); // active Some case, with value 0
```

An author may instead designate one data-free case as the default:

```csharp
enum struct Option<T>
{
    None = default,
    Some(T value)
}

Option<int> option = default;
bool isNone = option is Option<int>.None; // true
bool hasValue = option.HasValue;          // true: None is an active case
```

The designation is permitted only on an `enum struct` case without a parameter list, at most once per declaration. It gives that case discriminator zero, so constructing or converting it produces `default(E)` without initializing a payload.

These rules also apply to zero-initialized array elements and fields. The designation defines the meaning of zero-initialized storage and does not depend on executing a constructor. Apart from the meaning of zero, numeric discriminator values are implementation details.

#### Members and Methods

Enhanced enums can contain members:

```csharp
enum class Result<T>
{
    Success(T value),
    Error(string message);

    public bool IsSuccess => this switch
    {
        Success(_) => true,
        _ => false
    };

    public T GetValueOrDefault(T defaultValue) => this switch
    {
        Success(var value) => value,
        _ => defaultValue
    };
}
```

Members are restricted to:
- Instance methods, properties, indexers and events (no additional state)
- Static members
- Nested types

## 5. Translation Strategy

The translations illustrate the proposed API surface; exact metadata names remain open.

### `enum class` Translation

```csharp
public enum class Result
{
    Success(string value),
    Failure(int code)
}

// Conceptually translates to:
public abstract record class Result
{
    private Result() { }

    public sealed record class Success(string value) : Result;
    public sealed record class Failure(int code) : Result;
}
```

The enum is an abstract record base class, and its values hold the actual case reference.

Only the declared case types may derive from an `enum class`. This restriction applies when consuming the type from another assembly as well. The private constructor in the illustration does not by itself enforce the restriction: record copy constructors also permit derivation in ordinary C#. Enum case metadata and consuming compiler checks must preserve the declared hierarchy, along the lines of the [closed-hierarchy proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/closed-hierarchies.md#lowering). The exact metadata encoding remains open.

Data-free cases generate types with shared instances:

```csharp
public enum class State { Ready, Processing, Complete }

// Conceptually translates to:
public abstract record class State
{
    private State() { }

    public sealed record class Ready : State
    {
        public static readonly Ready Instance = new();
        private Ready() { }
    }
    // Similar for Processing and Complete
}
```

### `enum struct` Translation

The storage layout and emit strategy for an `enum struct` are compiler implementation details. They can, and likely will, change in future compiler versions, including to use new runtime capabilities, while preserving the specified API surface and observable semantics. No particular size, field layout, native ABI, or persistent byte representation is guaranteed.

The following conservative candidate aims for compact, non-boxing storage. Its packing, copying, and storage costs require performance validation; minimum size is not a requirement.

#### Tearing and Safety

Enum structs follow the existing [atomicity rules for struct reads and writes](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/variables.md#96-atomicity-of-variable-references). Unsynchronized concurrent reads and writes may tear: the discriminator and payload need not come from the same write. Case and payload observations are unspecified in that situation, and generated accessors may throw. Copying the enum or marking it `readonly` does not make a concurrent read an atomic snapshot.

Tearing must preserve memory and type safety under the usual safety assumptions for payload types and user code. The GC must identify reference locations from the runtime type's layout independently of the discriminator, including during assignment. No location may alternate between reference and non-reference storage, and arbitrary payload bits must never become managed references. Shared reference slots also require checked extraction: a reader observing a string case may read an array stored by another write. It must check the reference actually read, or use an equivalent safe mechanism; the discriminator alone cannot justify an unchecked cast.

#### Conservative Storage

One candidate uses a discriminator and three shared storage pools:

1. **Unmanaged data**: A byte region for the [predefined types](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#primitive-types-and-layout-assumptions) below, sized for the greatest combined requirement of any case, including padding and alignment. Different cases share the region; components of one case occupy distinct locations.
2. **Managed references**: Enough reference slots for the case containing the most references. A slot is normally `object`, but can use a more specific type if that type can represent every value assigned to it; a slot used only for strings can be `string`.
3. **Typed values**: Strongly typed slots for arbitrary value types and type parameters not known to be reference types. Each type needs enough slots for its maximum requirement in any case. Distinct types cannot generally share a typed slot. A reference-constrained type parameter, such as `T where T : class`, can use the reference pool.

A `struct` constraint does not rule out references. Even `where T : unmanaged` need not provide a declaration-time byte size, so such parameters can remain in typed slots.

The maxima may come from different cases, so the combined pools can be larger than the largest individual case.

#### Primitive Types and Layout Assumptions

The compiler may rely on the following predefined types being unmanaged and having the fixed managed sizes specified by the [existing `sizeof` rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12818-the-sizeof-operator):

| Types | Size in bytes |
| --- | --- |
| `bool`, `byte`, `sbyte` | 1 |
| `char`, `short`, `ushort` | 2 |
| `int`, `uint`, `float` | 4 |
| `long`, `ulong`, `double` | 8 |
| `decimal` | 16 |

These assumptions require no layout annotation. They describe managed storage, rather than interop marshaling representations. Native-sized integers (`nint`/`IntPtr` and `nuint`/`UIntPtr`) are also unmanaged, but their size depends on the execution platform. A representation must accommodate that dependency, for example with native-sized typed fields, instead of assuming one fixed size in portable output. No blanket layout assumption is made for other framework or user-defined structs.

#### External Types and Decomposition

By default, structs from other assemblies remain opaque typed values. The runtime determines their actual size and GC layout, keeping storage safe if a dependency changes size or acquires reference fields. Compile-time `unmanaged` classification is insufficient: the language enforces the constraint but the [CLR does not](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.3/blittable.md#detailed-design). Recompiling against a reference-containing definition rejects an unmanaged-constrained use; replacing an existing binary's dependency need not reject that instantiation.

The possible [checked packing opt-in](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#possible-checked-packing-opt-in) would allow raw storage for these types.

The compiler can directly arrange its generated case types' declared payload components. Decomposing a nested user-defined struct must preserve all value state without invoking user getters, setters, or constructors, and respect accessibility, type safety, and supported versioning. Otherwise the value remains in a typed slot. Declaration in the current assembly is insufficient when layout-relevant dependencies cross assembly boundaries.

Unmanagedness does not establish byte layout. Raw packing must account for architecture, alignment, and padding; reference-assembly metadata does not promise a fixed execution-time layout.

#### Mixed-Data Example

For example, the candidate pools above could represent:

```csharp
enum struct Message<T>
{
    Text(string text, int code),
    Buffer(byte[] data, int offset, int count),
    Pair(string first, string second),
    Timestamp(DateTime value),
    Values(T first, T second)
}

// Conceptual storage (generated case types and accessors omitted):
public struct Message<T>
{
    private byte _discriminator;

    // Buffer has the largest unmanaged payload: two ints.
    private UnmanagedData _unmanaged;

    // Pair needs two references. The first slot also stores Text's string
    // or Buffer's byte array; the second is used only for a string.
    private object? _reference0;
    private string? _reference1;

    // DateTime has no guaranteed size or reference layout across versions.
    // Keep Timestamp's payload in a typed slot.
    private DateTime _timestamp;

    // Values needs two T slots. T may be a value type containing references.
    private T _value0;
    private T _value1;

    [InlineArray(8)]
    private struct UnmanagedData
    {
        private byte _element0;
    }
}
```

#### Case Conversions

Converting a case value to the enum packs its constituent data into the assigned slots and sets the discriminator. Extracting a case checks the discriminator, reads the slots, and reconstructs the case value. These operations rearrange (or "swizzle") the data between the case's own layout and the enum's shared layout; they cannot generally reinterpret the enum's payload as the complete case type.

In the example above, constructing an enum from `Buffer` stores its array in `_reference0` and its two integers in `_unmanaged`. Extracting `Buffer` reconstructs the case from those locations. Constructing from `Pair` instead uses both reference slots and needs no unmanaged data. Unused reference and typed slots are initialized or cleared to their default values, so the enum does not retain references belonging to an inactive case.

Storing every complete case in a separate field would avoid this rearrangement, but would pay for the sum of the cases' sizes even though only one case is active. That cost is especially significant for structs, which are copied by value and stored inline in arrays and containing objects. Shared storage reduces that cost at the expense of packing and reconstruction. The compiler may choose either strategy or a hybrid based on their storage and execution costs.

For `Option<T>`, the shared storage is simply one typed `T` slot:

```csharp
public enum struct Option<T>
{
    None,
    Some(T value)
}

// Conceptually translates to:
public struct Option<T>
{
    public record struct None;
    public record struct Some(T value);
    // Enum-level record equality members are omitted from this illustration.

    // Zero represents no case in this declaration.
    // None needs no payload; Some needs one typed T slot.
    private byte _discriminator;
    // The payload slot may be unused and contain default(T).
    private T? _value;

    public readonly bool IsNone => _discriminator == 1;
    public readonly bool IsSome => _discriminator == 2;
    public readonly bool HasValue => _discriminator != 0;

    // Non-boxing case access
    public readonly bool TryGetValue(out None value)
    {
        value = default;
        return _discriminator == 1;
    }

    public readonly bool TryGetValue(out Some value)
    {
        if (_discriminator == 2)
        {
            value = new Some(_value!);
            return true;
        }
        value = default;
        return false;
    }

    // Construction and case-to-enum conversions
    public Option(None _) => _discriminator = 1;
    public Option(Some some) => (_discriminator, _value) = (2, some.value);

    public static implicit operator Option<T>(None value) => new(value);
    public static implicit operator Option<T>(Some value) => new(value);

    // Checked case extraction
    public static explicit operator None(Option<T> value) =>
        value.TryGetValue(out None result) ? result : throw new InvalidCastException();
    public static explicit operator Some(Option<T> value) =>
        value.TryGetValue(out Some result) ? result : throw new InvalidCastException();
}
```

The compiler recognizes the struct as a shape enum through its declaration and emitted case metadata. The generated predicates and `TryGetValue` methods provide non-boxing case access; the constructor and conversion operators provide construction and checked extraction. The exact metadata encoding remains open.

If this declaration instead uses `None = default`, the `None` constructor sets the discriminator to zero, and `IsNone` and `TryGetValue(out None)` test for zero. `HasValue` is then always true for values produced by the generated APIs, including `default(Option<T>)`.

#### Fully Unmanaged Cases

When all case data is unmanaged and the required layout facts are established, complete case values can overlap in one unmanaged region sized for the largest case, with the necessary padding and alignment and a separate discriminator. Preserving each case's representation lets construction and extraction copy complete values without swizzling individual fields.

#### Unmanaged Classification

An enum struct's classification as an unmanaged type is observable through `where T : unmanaged`, pointers, and `stackalloc`, even though its storage layout is an implementation detail. A future compiler could otherwise change that classification by choosing a different representation for the same declared cases.

LDM needs to decide what compatibility guarantee applies to changes in the compiler's representation. Options include:

- Allowing classification to change in either direction, accepting that a change from unmanaged to managed can break code that depended on the previous classification.
- Requiring a constructed enum type that was unmanaged to remain unmanaged when its declared cases, payload types, and constraints are unchanged, while allowing changes from managed to unmanaged. This would constrain future representations to remain reference-free for those types.

This question concerns the enum type's own language-level classification. Changes to its declared cases or payload dependencies raise separate compatibility concerns. The checked packing opt-in below instead concerns assumptions about individual payload types.

#### Possible Checked Packing Opt-in

A compiler-recognized attribute could permit raw packing of a payload type by asserting unmanagedness and a fixed byte size. It could appear on the payload type's declaration or on an enum case parameter, allowing consumers to opt in without changing a referenced library. The name, syntax, and detailed placement rules remain open. For example:

```csharp
// Defined by a referenced library:
[Unmanaged(size: 8)]
public readonly record struct Point(int X, int Y);

// Consuming code:
enum struct Position
{
    Unknown,
    At(Point point)
}
```

For either placement, the compiler must verify that the payload is a struct satisfying the ordinary C# unmanaged rules; the annotation is not evidence of unmanagedness. It may also diagnose a size mismatch when the managed size is statically known.

For a declaration-site annotation, the compiler would prepend runtime validation to the type's static constructor, preserving user-written initialization:

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

Both checks are mandatory and use the actual runtime managed representation, not interop marshaling size. The helpers are illustrative generated code, with placement that can allow sharing across types. Validation failure must prevent raw access and produce a managed exception, such as a type-initialization failure. Raw copies must stay within storage bounds, as illustrated by the existing checks in [`MemoryMarshal.Read` and `Write`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/MemoryMarshal.cs). With these guarantees, the opt-in need not require `unsafe`.

A consuming enum that chooses raw packing must also validate its own recorded claims once per constructed enum type, through a generated static constructor. Generated constructors and case-to-enum conversions must trigger that initialization before packing, following the existing [struct static-constructor rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#16410-static-constructors). Extraction from a non-default enum can rely on completed validation without repeating the checks; alternative lowerings must preserve that guarantee.

The payload type's initializer alone is insufficient: defaults, arrays, layout queries, and whole-value copies can bypass it. A later library may also remove the annotation or validate a new size successfully. An old consumer that reserved eight bytes must still validate its own expectation of eight bytes. This explicit layout dependency can reject updates that would remain compatible with ordinary typed storage.

The zero-initialized values described under [Default Values](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#default-values) have no packed payload to reconstruct, so they need no extra layout validation. Failed extraction can assign a typed `default` to its out parameter, and copying these zero values remains safe.

A use-site assertion could opt the earlier `DateTime` payload into packing:

```csharp
enum struct CompactMessage
{
    Timestamp([Unmanaged(size: 8)] DateTime value),
    Count(long value)
}
```

Here eight bytes is a claim about `DateTime`'s current managed size, not a language guarantee. The same validation rules would permit both cases to share an eight-byte region, with the discriminator stored separately. An annotation permits packing without requiring it.

The contract establishes only reference containment and total managed size. Field offsets, alignment, and accessibility require separate justification; the compiler can instead copy values opaquely. Whether to add this contract remains a joint decision for LDM and the runtime team, supported by performance measurements.

### Generated APIs and Conversions

The proposed minimum API exposes the nested case types so that source code can construct, test, and extract them. Case types are public within the enclosing enum, so their effective accessibility is bounded by that enum's accessibility. Nested cases of a generic enum use the enclosing type parameters and constraints.

Case constructors, payload properties, and deconstruction follow the generated record types' rules. Data-free struct cases can be constructed with `new E.Case()`.

Classes use ordinary reference conversions, including `as`: downcasting null returns null, while downcasting a non-null instance of another case throws `InvalidCastException`. For example:

```csharp
Result result = new Result.Success("ok");
if (result is Result.Success success)
{
    Console.WriteLine(success.value);
}
Result.Success extracted = (Result.Success)result;
Result.Failure? failure = result as Result.Failure; // null

Result? missing = default;
Result.Success? absent = (Result.Success?)missing; // null
```

For structs, we propose generating the following members in addition to the case types:

- A constructor and implicit conversion from each case type to the enum type.
- A boolean property named `Is` followed by the case name, such as `IsNone`, testing only case identity and returning false when no case is active.
- `HasValue`, which is false for the uninitialized representation and true for any active case, including a designated default case.
- A `TryGetValue(out Case value)` overload for each case. It returns true and supplies the case value when that case is active; otherwise it returns false and assigns `default(Case)` to the out parameter.
- An explicit conversion from the enum type to each case type. It returns the active case value or throws `InvalidCastException` if a different case, or no case, is active.

These case queries and conversions do not require boxing.

```csharp
Option<int> option = new Option<int>.Some(42);
if (option is Option<int>.Some some)
{
    Console.WriteLine(some.value);
}
if (option.TryGetValue(out Option<int>.Some value))
{
    Console.WriteLine(value.value);
}
Option<int>.Some extracted = (Option<int>.Some)option;
```

The `as` operator does not invoke the generated struct conversions; use `TryGetValue` for conditional extraction.

Generated members must agree on the active case. Extraction follows the [copy semantics](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#readonly-copying-and-with) above.

User-declared members conflicting with generated members are errors, except for customization permitted by ordinary record rules. Final generated API names remain open; enum-valued construction is specified under [Case Construction Expressions](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#case-construction-expressions).

## 6. Pattern Matching and Behaviors

### Case Pattern Matching

Class cases use normal reference type patterns and record deconstruction. For structs, a type or declaration pattern naming a declared case tests whether that case is active and, on success, makes its value available without boxing. Property and positional patterns for that case operate on the extracted case value. Patterns examining the enum's own members operate on the enum value.

Struct case type tests are recognized by the compiler when the input is statically known to be a shape enum. They do not establish CLR inheritance between the enum and the case types. Boxing an enum struct boxes the enum itself, so a case type test on a value whose static type is `object` does not extract its case. Data-free cases match by case type, even if record operations produced another instance of that type.

```csharp
var message = operation switch
{
    Open(var path) => $"Opening {path}",
    Close => "Closing file",
    Read(_, var offset, var count) => $"Reading {count} bytes at {offset}",
    Write(var buffer) => $"Writing {buffer.Length} bytes"
};
```

The `null` pattern matches a null class reference. Whether it should also match an uninitialized enum struct remains open.

### Switch Translation

The emit strategy remains open. The candidates below aim to let the runtime optimize case tests. Validation should cover many cases, different distributions, guards, and both switch statements and expressions.

In local .NET 11 RC1 checks on ARM64 with 16 sealed class cases and 16 struct cases, both exact-type and discriminator-predicate chains remained linear in fully optimized code and Tier 1 code with dynamic PGO. Struct predicates inlined, but the JIT did not recover a jump table or tree. The strategies would need additional runtime support.

The switch input is evaluated once. The examples show distinct cases without guards; a full translation must also preserve null handling, source ordering where it affects behavior, guards, deconstruction, and the fallback behavior of the original statement or expression. Only case tests whose reordering preserves those semantics can be reorganized.

#### `enum class` Case Tests

For an `enum class`, an initial strategy is to compare the active case's exact runtime type against the sealed generated case types. For example:

```csharp
enum class E
{
    C1(int value),
    C2(int value),
    C3(int value)
}

switch (e)
{
    case E.C1(var value):
        HandleC1(value);
        break;
    case E.C2(var value):
        HandleC2(value);
        break;
    case E.C3(var value):
        HandleC3(value);
        break;
}
```

The conceptual translation is:

```csharp
// Evaluate the input once; the enum value is the case reference.
var __case = e;
var __type = __case?.GetType();

if (__type == typeof(E.C1))
{
    var __c1 = (E.C1)__case;
    HandleC1(__c1.value);
}
else if (__type == typeof(E.C2))
{
    var __c2 = (E.C2)__case;
    HandleC2(__c2.value);
}
else if (__type == typeof(E.C3))
{
    var __c3 = (E.C3)__case;
    HandleC3(__c3.value);
}
// Emit the original null and fallback behavior as needed.
```

Because the case types are sealed, each exact type comparison identifies one case. The runtime can potentially replace `GetType()` and `typeof` equality with comparisons of method-table pointers, performing dispatch directly on the case reference's type identity. The successful comparison also establishes the case type for the subsequent extraction.

For a large case set, the runtime could recognize the distinct type identities and choose a balanced comparison tree or another dispatch strategy. Its benefit relative to a linear chain or profile-guided ordering needs validation.

#### `enum struct` Case Tests

For an `enum struct`, we expect generated case predicates along these lines:

```csharp
public readonly bool IsC1 => _discriminator == 1;
public readonly bool IsC2 => _discriminator == 2;
public readonly bool IsC3 => _discriminator == 3;
```

An initial translation of the corresponding switch uses these predicates followed by case extraction:

```csharp
var __value = e;
if (__value.IsC1)
{
    E.C1 __c1 = (E.C1)__value;
    HandleC1(__c1.value);
}
else if (__value.IsC2)
{
    E.C2 __c2 = (E.C2)__value;
    HandleC2(__c2.value);
}
else if (__value.IsC3)
{
    E.C3 __c3 = (E.C3)__value;
    HandleC3(__c3.value);
}
// Emit the original fallback behavior as needed.
```

Inlining the predicates should expose a sequence equivalent to:

```csharp
if (__value._discriminator == 1) { /* C1 body */ }
else if (__value._discriminator == 2) { /* C2 body */ }
else if (__value._discriminator == 3) { /* C3 body */ }
```

The runtime could select a jump table for dense values, a tree for sparse values, or a linear chain, and eliminate extraction checks dominated by a successful predicate. In the local checks, marking the struct conversions for inlining removed their redundant checks.

#### Runtime Recognition and Alternatives

We should consider teaching the runtime to recognize exact-type comparison chains and discriminator predicates with distinct case bodies. The runtime already has a [limited switch-recognition pass](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/switchrecognition.cpp#L28) for constant-comparison chains whose successful tests all reach the same destination. It runs after inlining, but stops recognizing a chain when successful tests reach different destinations, as they do in these DU switches. Extending this recognition is one possible starting point.

An attribute in `System.Runtime.CompilerServices` could instead opt a well-behaved DU into optimization if general pattern recognition raises correctness concerns. Its contract would specify stable case identity during dispatch, side-effect-free predicates agreeing with the case mapping, and extraction after a successful test. Struct metadata could identify the discriminator field for direct runtime access. No attribute name or final contract is selected.

Class enums could also use predicate-and-extraction emit if the runtime can optimize that representation efficiently.

If these approaches do not produce satisfactory results, a final fallback is to expose a discriminator and have the compiler emit an integral switch over it directly. This would use the runtime's existing integral-switch optimizations without requiring it to recover a switch from a comparison chain. Class enums would also need a discriminator mapping for their cases. We would prefer to avoid the [versioning contract this would expose](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#library-evolution).

### Exhaustiveness

The compiler tracks all declared cases. Both constant and shape enums can be open or closed. Constant enums follow the [Closed Enums proposal](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md); class shape enums also require the hierarchy information described above.

Open enums can be used to signal that the enum author may add new cases in future versions—consumers must handle unknown cases defensively (e.g., with a default branch). Closed enums signal that there is no need to handle unknown cases, such as when the case set is complete and will never change—the compiler ensures exhaustive matching without requiring a default case.

For shape enums, this open/closed distinction concerns evolution of the declared case set. It does not permit arbitrary user-defined subclasses or arbitrary discriminator values.

Covering all declared cases does not cover the no-case state. A possibly-null class input needs a `null` arm or a fallback to handle null. A struct without a designated default case likewise needs to handle its uninitialized representation, for example with a `{ HasValue: false }` arm or a fallback, unless analysis establishes that a case is active. A struct with a designated default case has no additional uninitialized state: covering that case also covers zero-initialized values. Diagnostics should account for these states even when every case type is non-nullable; the no-case state is distinct from payload nullability.

For constant enums, "open" also means values outside the declared set can be safely freely cast to the enum type.

```csharp
closed enum class Status { Active, Pending(DateTime since), Inactive }

Status? status = GetStatus();

// All cases and null are handled; no fallback needed.
var description = status switch
{
    Status.Active => "Currently active",
    Status.Pending(var date) => $"Pending since {date}",
    Status.Inactive => "Not active",
    null => "No status"
};

enum Priority : string { Low = "low", Medium = "medium", High = "high" }

// Default case is needed, other string values may be converted to Priority, or new cases may be added in the future:
var value = priority switch
{
    Low => -1,
    Medium => 0,
    High => 1,
    _ => /* fallback to low priority */ -1,
}
```

### Case State and Nullability

Extraction preserves payload members' declared types and nullability annotations. A null payload does not remove case identity: `new Option<string?>.Some(null)` remains an active `Some` after conversion to the enum.

Classes follow ordinary reference nullability rules. For structs, active-case analysis is separate from payload nullability; construction from a case, successful case tests, and successful `TryGetValue` calls establish the active case.

### Library Evolution

Adding a case to a closed enum breaks its [closed-case contract](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#exhaustiveness).

Case-set policy does not by itself guarantee source, binary, or layout compatibility. Renaming or removing a public case, changing its payload constructor or properties, or changing between class and struct representations affects existing APIs. Changing the designated default case changes the meaning of zero-initialized storage and is a behavioral breaking change. Adding a struct case can enlarge the unmanaged, reference, or typed storage pools and change alignment. Libraries that depend on fixed native layouts or ahead-of-time ABI contracts need to account for that separately from open-case matching.

The compiler's case mapping must also support the intended versioning policy. If a consuming assembly embeds numeric tags for case tests, existing case mappings must remain valid across supported updates or the tests must obtain the mapping from the defining type. Reordering declarations must not cause a previously compiled test for one case to recognize another. Exposing a discriminator for switch lowering would make that mapping a stable API and ABI contract: reordering declarations or inserting a case anywhere would need to preserve existing mappings rather than assigning tags in declaration order. Storage tags are otherwise implementation details. Previously compiled switches retain their normal fallback or unmatched-switch behavior when encountering a case they did not know about.

#### Known-Case Coverage with a Future-Case Fallback

A plain fallback handles future cases, but also hides omissions of cases already known when the consumer is compiled. We would like an opt-in way to mark a fallback as intended for future cases, following the idea of Swift's [`@unknown default`](https://github.com/swiftlang/swift-evolution/blob/main/proposals/0192-non-exhaustive-enums.md#unknown).

Such a fallback still matches all remaining values at runtime. The compiler would additionally warn when the preceding patterns do not fully cover a declared case known at compilation, using the existing exhaustiveness analysis. A guarded or partial payload pattern therefore does not necessarily cover an entire case. Recompiling against a library that adds a case would prompt the consumer to review it while retaining a working fallback. This is a diagnostic refinement of the existing exhaustiveness rules. The handling of null and uninitialized values remains as specified above. The exact opt-in syntax or mechanism remains an LDM decision.

## 7. Examples and Use Cases

### Migrating Traditional Enums

```csharp
// Traditional enum
enum OrderStatus { Pending = 1, Processing = 2, Shipped = 3, Delivered = 4 }

// Enhanced with data
enum struct OrderStatus
{
    Pending,
    Processing(DateTime startedAt),
    Shipped(string trackingNumber),
    Delivered(DateTime deliveredAt);

    public bool IsComplete => this is Delivered;
}
```

### Result and Option Types

```csharp
closed enum class Result<T, E>
{
    Ok(T value),
    Error(E error);

    public Result<U, E> Map<U>(Func<T, U> mapper) => this switch
    {
        Ok(var value) => .Ok(mapper(value)),
        Error(var err) => .Error(err)
    };
}

closed enum struct Option<T>
{
    None = default,
    Some(T value);

    public T GetOrDefault(T defaultValue) => this switch
    {
        Some(var value) => value,
        None => defaultValue
    };
}
```

### Contextual Case Construction and Inference

Shape enums should compose with [target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md) and the proposed inference improvements:

- [Generalized generic type inference](https://github.com/dotnet/csharplang/pull/10311), which provides shared inference machinery and lets target types contribute bounds alongside arguments.
- [Inference for constructor calls](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md), tracked by [#9627](https://github.com/dotnet/csharplang/issues/9627), including the [updated constructor inference proposal](https://github.com/dotnet/csharplang/pull/10312) and its [type-group lookup](https://github.com/dotnet/csharplang/pull/10310) foundation.
- [Inference for type patterns](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns.md).

These are proposed language features; the examples below describe their intended composition with shape enums. They are not available in current C# merely by declaring a shape enum.

#### Use the Expected Enum Type

[Case construction expressions](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#case-construction-expressions) define `E.None` and `E.Some(arguments)` as enum construction expressions. When the expected type is `Option<int>`, a leading-dot case name can be looked up on that constructed type. There is no need to repeat either `Option` or `int`, or to introduce `new` when the case carries data:

```csharp
enum struct Option<T>
{
    None,
    Some(T value)
}

void UseInt(Option<int> value) { /* ... */ }

UseInt(.None);
UseInt(.Some(42));

Option<int> Empty() => .None;
Option<int> Present(int value) => .Some(value);
```

In the `Map` example above, the return type supplies `Result<U, E>` to the switch arms, so `.Error(err)` selects the output enum's case even though `U` does not occur in its payload. The direct nested-type form `new .Some(42)` remains available as well.

#### Infer Generic Factory Results from Context

Generalized generic type inference addresses a related problem: a generic factory may lack argument information for one or more enum type parameters. For the struct declaration above, illustrative factories could be:

```csharp
static class Option
{
    public static Option<T> CreateNone<T>() => new Option<T>.None();
    public static Option<T> CreateSome<T>(T value) => new Option<T>.Some(value);
}

Option<int> empty = Option.CreateNone(); // infer T = int from the target
UseInt(Option.CreateNone());             // infer T = int from the parameter
UseInt(.CreateNone());                   // also omit the factory qualifier
Func<Option<int>> noneFactory = Option.CreateNone; // infer T from the delegate result
```

`.CreateNone()` additionally uses [factory-container lookup](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#factory-containers) on the nongeneric sibling `Option`. These factories are illustrative; shape enums need not generate them.

The result type `Option<T>` must fit the target `Option<int>`, supplying the missing bound for `T`. The delegate result similarly supplies a bound for `noneFactory`; arguments and targets can determine different type parameters. Constraints, applicability, result conversion, and overload selection still apply. The precise contexts and binding stages supplying target information remain part of the inference design.

Constructor inference extends the shared machinery to `new` expressions, but inference for omitted *containing-type* arguments in names such as `new Option.Some(42)` remains an open issue. The leading-dot form avoids that issue by supplying the already constructed containing type `Option<int>`.

#### Case Patterns and Limits

Target-typed member access also shortens case patterns, using the pattern input type:

```csharp
int GetOrDefault(Option<int> option, int fallback) => option switch
{
    .Some(var value) => value,
    .None => fallback,
    _ => fallback // no active case, if permitted by the default-value policy
};
```

`.None` tests case identity, rather than constructing a value to compare. Type-pattern inference addresses the related case of naming a generic case type with omitted type arguments; leading-dot lookup already supplies the containing arguments here.

The expected type must come from somewhere. For example, another argument can first determine a generic method's type parameter, then provide the case argument's target:

```csharp
void Use<T>(Option<T> value, T fallback) { /* ... */ }

Use(.None, 42);          // T = int from the second argument
Use(.Some(42), 0);       // T = int from the second argument
```

`var value = .None` has no target type and remains an error. A call with only a context-dependent case argument cannot infer an otherwise unknown `T` merely from the name `None`. Likewise, overloads accepting different enum types do not become unambiguous merely because only one has a case with that name: target-typed member access does not use member existence to choose the overload. Qualification or explicit type arguments remain available where context is insufficient.

### State Machines

```csharp
enum class ConnectionState
{
    Disconnected,
    Connecting(DateTime attemptStarted, int attemptNumber),
    Connected(IPEndPoint endpoint, DateTime connectedAt),
    Reconnecting(IPEndPoint lastEndpoint, int retryCount, DateTime nextRetryAt),
    Failed(string reason, Exception exception);

    public ConnectionState HandleTimeout() => this switch
    {
        Connecting(var started, var attempts) when attempts < 3 =>
            new ConnectionState.Reconnecting(null, attempts + 1, DateTime.Now.AddSeconds(Math.Pow(2, attempts))),
        Connecting(_, _) =>
            new ConnectionState.Failed("Connection timeout", new TimeoutException()),
        Connected(var endpoint, _) =>
            new ConnectionState.Reconnecting(endpoint, 1, DateTime.Now.AddSeconds(1)),
        _ => this
    };
}
```

## 8. Open Questions

1. **Case accessibility**: Should individual cases support access modifiers in addition to the proposed public nested case types?
2. **Partial support**: Should enhanced enums support `partial` for source generators?
3. **Optional type-union integration**: Should either representation implement the union pattern, as discussed in [Appendix B](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#appendix-b-possible-connections-to-type-unions)?
4. **Serialization**: How should enhanced enums interact with System.Text.Json?
5. **Additional state**: Should shape enums allow instance fields outside case data?
6. **Custom constructors**: Should enums allow custom constructors that delegate to cases?
7. **Generic cases**: Should cases support independent generic parameters?
8. **Exact lowering**: What names and shapes should the generated APIs and hierarchy metadata have, and are additional convenience APIs needed?
9. **Uninitialized matching**: Should the `null` pattern match an uninitialized enum struct, or should this state be tested only through case-state APIs?
10. **Future-case fallbacks**: What syntax or mechanism should opt into known-case coverage diagnostics while retaining a fallback for future cases?
11. **Checked packing**: Should a checked `[Unmanaged(size: ...)]` contract permit raw payload storage, and where should the annotation be placed?
12. **Unmanaged classification**: What compatibility guarantee should apply when the compiler changes an enum struct's representation, as discussed under [Unmanaged Classification](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#unmanaged-classification)?

## Appendix A: Grammar Changes

```antlr
enum_declaration
    : attributes? enum_modifier* 'enum' identifier enum_base? enum_body ';'?
    | attributes? shape_enum_modifier* 'enum' ('class' | 'struct') identifier type_parameter_list?
      type_parameter_constraints_clause* shape_enum_body ';'?
    ;

shape_enum_modifier
    : enum_modifier
    | 'readonly'
    ;

enum_base
    : ':' enum_underlying_type
    ;

enum_underlying_type
    : simple_type // all integral types, fp-types, decimal, bool and char
    | 'string'
    | type_name  // Must resolve to one of the above
    ;

enum_body
    : '{' enum_member_declarations? '}'
    | '{' enum_member_declarations ';' class_member_declarations '}'
    ;

enum_member_declarations
    : enum_member_declaration (',' enum_member_declaration)*
    ;

enum_member_declaration
    : attributes? identifier enum_member_initializer?
    ;

enum_member_initializer
    : '=' constant_expression
    ;

shape_enum_body
    : '{' shape_enum_case_declarations? '}'
    | '{' shape_enum_case_declarations ';' class_member_declarations '}'
    ;

shape_enum_case_declarations
    : shape_enum_case_declaration (',' shape_enum_case_declaration)*
    ;

shape_enum_case_declaration
    : attributes? identifier parameter_list?
    | attributes? identifier '=' 'default'
    ;
```

For the `= default` restrictions, see [Default Values](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#default-values). Type parameter lists and constraints are permitted on shape enums; constant enums retain their non-generic declarations. The shape-enum modifiers additionally permit `readonly` for `enum struct`, with the restrictions described above; it is not permitted for `enum class` or constant enums.

## Appendix B: Possible Connections to Type Unions

The design above does not require either representation to implement the union pattern, carry `[Union]`, or implement `IUnion`. This appendix considers possible integration.

### Union Types, Declarations, and Runtime Access

The unions proposal distinguishes a *union type* from a *union declaration*. A custom union type can be a class or struct. Compiler recognition uses `[Union]` and a pattern of creation and access members. The `union` declaration syntax chooses a particular struct implementation that stores its contents in an object reference. Implementing the custom-union pattern would therefore allow shape enums to retain their own representation and storage strategy.

The `IUnion` interface is a separate option for runtime access to a value's contents. Implementing it alone does not opt a type into compiler union behavior. This distinction is described in the [union patterns update](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/union-patterns-update.md#marking-union-types-with-an-attribute).

### Possible Integration for `enum class`

An enum class could implement the union pattern while remaining a base class with sealed case types. Using a [union member provider](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-member-providers), generated `Create` methods could return the supplied case reference unchanged, and the provider's `Value` property could return `this`. An optional `IUnion` implementation could expose that same reference to runtime consumers. None of these members would require a wrapper allocation.

This could give class enums a common access protocol with other union types and allow code that understands that protocol to consume them uniformly.

The language benefits are limited: the [class representation and APIs](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#enum-class-translation) already supply most of these behaviors. Integration mainly adds interoperability, at the cost of extra APIs or provider metadata. It must preserve the enum's reference semantics and declared-hierarchy restriction, which the union pattern alone does not enforce.

Runtime consumers must account for this self-reference when recursively inspecting contents.

### Possible Integration for `enum struct`

Struct enums have a closer fit with the custom-union pattern. The generated constructors could identify the case types, and the generated `HasValue` and `TryGetValue` members could satisfy the non-boxing access pattern. Integration would additionally require `[Union]` and an object-valued `Value` property. `IUnion` could optionally expose this view at runtime.

Potential benefits include:

- Reusing the existing rules for case construction, case type matching, deconstruction, and nullable-union matching instead of specifying all of those rules independently.
- Allowing a consuming compiler that understands the union pattern to recognize the generated members and case types, even without support for the shape-enum declaration syntax.
- Preserving inline storage and non-boxing access through `TryGetValue`; the object-valued view would not require storing cases as boxed objects.

The costs and semantic choices include:

- The mandatory `Value` property would box struct cases when accessed and would require every case to be convertible to `object`. Future support for ref-like cases would require changes to that contract or a different integration strategy.
- Direct exposure reserves public API names. For example, `Option<T>` might want `Value` to mean its payload and `HasValue` to mean `Some`, while the protocol means the active case and the presence of any case, including `None`. A member provider avoids these conflicts but adds interface metadata; changing the proposed public `HasValue` placement would be an additional API decision.
- Union type patterns target the contained case value, while untyped property and positional patterns inspect the enum itself. These rules would need to be reconciled with patterns intended to test or examine the enum struct itself. The distinction is described in the [LDM decision on pattern targets](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-08-12.md#union-pattern-matching-targets).
- Union null patterns would select the currently open [uninitialized-null matching option](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#case-pattern-matching), using a nullable `Value`. A designated default case could instead use a non-nullable `Value`. The generated annotations and access members must agree.
- Union exhaustiveness treats the declared case types as exhaustive. The enum proposal's separate open/closed policy, including handling future cases, would need to be preserved explicitly.

### Scope of an LDM Decision

LDM could choose full union-pattern integration, reuse selected compiler machinery without generating the pattern, or keep the features independent. Such a decision should determine which representations participate, whether protocol members are exposed directly or through a provider, whether `IUnion` is useful for runtime interoperability, and how pattern targets, uninitialized matching, and open/closed exhaustiveness interact.

A JIT optimization contract would need stronger case and representation guarantees than `[Union]`, which supports arbitrary custom implementations and identifies no discriminator field or mapping.
