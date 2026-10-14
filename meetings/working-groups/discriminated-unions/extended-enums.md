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

C# gains a layered approach to union types: type unions provide the foundation for combining types, while enhanced enums offer elegant syntax for discriminated unions where you define cases and their union together.

```csharp
// Type unions - combine existing types
union Result(string, ValidationError, NetworkException);

// Shape enums - discriminated unions with integrated case definitions
enum struct PaymentResult // or `enum class`
{
    Success(string transactionId),
    Declined(string reason),
    PartialRefund(string originalId, decimal amount)
}
```

## 2. Motivation and Design Philosophy

### From Type Unions to Discriminated Unions

Type unions solve the fundamental problem of representing "one of several types". A particularly important pattern is discriminated unions, where:

- Cases are defined together as a logical unit
- Each case may carry different data
- The set of cases is typically closed and known at design time

Shape enums provide natural syntax for this pattern—expressing the entire discriminated union in a single declaration rather than manually defining and combining types.

### Limitations of Current Enums

Today's C# enums have significant limitations:

1. **No associated data**: Cases are merely integral values
2. **Not truly exhaustive**: Any integer can be cast to an enum type
3. **Limited to integers**: Cannot use strings or doubles

Enhanced enums address all these limitations while preserving conceptual simplicity.

### Building on Familiar Concepts

By extending the existing `enum` keyword, enhanced enums provide a grow-up story. Simple enums remain simple, while advanced scenarios become possible without abandoning familiar patterns.

## 3. Type Unions (Foundation)

Type unions are fully specified in the [unions proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#summary). They provide:

- Implicit conversions from case types to the union type
- Pattern matching that unwraps union contents
- Exhaustiveness checking in switch expressions
- Enhanced nullability tracking
- Flexible storage strategies (boxing or non-boxing)

## 4. Enhanced Enums

### Design Principles

- **Progressive enhancement**: Simple enums stay simple; complexity is opt-in
- **Data carrying**: Each case can carry its own constituent data
- **Familiar syntax**: Builds on existing enum and record concepts
- **Union foundation**: Shape enums are discriminated unions

### Syntax Extensions

Enhanced enums extend traditional enum syntax in three orthogonal ways:

#### Extended Base Types

Support any constant-bearing type:

```csharp
enum Traditional : int { A = 1, B = 2 }
enum Priority : string { Low = "low", Medium = "medium", High = "high" }
enum TranscendentalConstants : double { Pi = 3.14159, E = 2.71828 }
```

#### Shape Declarations

Create a shape enum (discriminated union) by:
- Adding `class` or `struct` after `enum`

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

Each case with a parameter list generates a nested record type. `enum class` generates `sealed record class` types; `enum struct` generates `readonly record struct` types. While these are the generated types, the union implementation may optimize internal storage.

TODO: Should the structs be readonly?  Seems like that goes against normal structs (including `record struct`).  Seems like that could be opt in with `readonly enum struct X`.

#### Combination Rules

- **Constant enums**: Can use extended base types but NOT have parameter lists
- **Shape enums**: Can have parameter lists but NOT specify a base type
- **Mixing cases**: Cannot mix constant values and parameterized cases

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

For the complete formal grammar, see [Appendix A: Grammar Changes](#appendix-a-grammar-changes).

### Constant Value Enums

Enhanced constant enums support any primitive type with compile-time constants:

```csharp
enum Priority : string 
{
    Low = "low",
    Medium = "medium", 
    High = "high"
}
```

These compile to `System.Enum` subclasses with the appropriate `value__` backing field. Non-integral constant enums require explicit values for each member.

### Shape Enums: Discriminated Unions Made Elegant

Shape enums combine type unions with convenient integrated syntax:

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

```csharp
enum class WebResponse
{
    Success(string content),
    Error(int statusCode, string message),
    Timeout
}

enum struct Option<T>
{
    None,
    Some(T value)
}
```

**`enum class`** creates discriminated unions with reference type cases:
- Cheap to pass around (pointer-sized)
- No struct tearing risk
- Natural null representation

**`enum struct`** creates discriminated unions with optimized value-type storage:
- No heap allocation
- Better cache locality
- Reduced GC pressure

#### Members and Methods

Enums can contain members just like unions:

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

Shape enums translate directly to unions—generating case types as nested types and creating a union that combines them.

### `enum class` Translation

```csharp
enum class Result
{
    Success(string value),
    Failure(int code)
}

// Translates to:
public union Result(Success, Failure)
{    
    public sealed record class Success(string value);
    public sealed record class Failure(int code);
}
```

Singleton cases generate types with shared instances:

```csharp
enum class State { Ready, Processing, Complete }

// Translates to:
public union State(Ready, Processing, Complete)
{    
    public sealed class Ready 
    {
        public static readonly Ready Instance = new();
        private Ready() { }
    }
    // Similar for Processing and Complete
}
```

### `enum struct` Translation

The storage layout and emit strategy for an `enum struct` are compiler implementation details. They can, and likely will, change in future compiler versions, including to take advantage of new runtime capabilities. The compiler is free to change the representation and generated code while preserving the specified API surface and observable semantics. This proposal does not guarantee a particular size, field layout, native ABI, or byte representation suitable for persistence.

The following describes a conservative storage strategy and possible optimizations. The aim is compact, non-boxing case storage, with the cost of packing and reconstructing data balanced against storage size and the runtime's ability to optimize the generated code. These trade-offs require performance validation; minimum storage is not a requirement on every implementation.

#### Tearing and Safety

Enum structs follow the existing [atomicity rules for struct reads and writes](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/variables.md#96-atomicity-of-variable-references). Unsynchronized concurrent reads and writes may tear: the discriminator and payload need not come from the same write. Case and payload observations are unspecified in that situation, and generated accessors may throw. Copying the enum or marking it `readonly` does not make a concurrent read an atomic snapshot.

Tearing must not introduce memory or type unsafety in the conservative implementation. The garbage collector must be able to identify reference locations from the runtime type's layout independently of the discriminator, including during an assignment. No location may alternate between managed-reference storage and non-reference storage according to the active case. Arbitrary payload bits must never be interpreted as managed references.

Reference-slot sharing must also preserve reference type safety. A reader might observe the discriminator for a string case while a shared `object` slot contains an array from another case. Extraction must check the reference it actually reads, or use an equivalent mechanism that preserves type safety; the discriminator alone cannot justify an unchecked reference reinterpretation. These guarantees concern the generated representation and accessors, within the usual safety assumptions for the payload types and user code.

#### Conservative Storage

A conservative implementation stores the discriminator and payload in compiler-generated fields, using opaque, strongly typed slots for value types whose representation cannot safely be assumed. Typed slots let the runtime determine their size and GC layout from the types actually loaded. This default strategy requires no user layout assertions and must remain safe across supported dependency updates, including a payload struct acquiring reference fields or changing size.

One candidate shares storage in the following pools:

1. **Unmanaged data**: A byte region for primitive data whose size and reference-free representation are established as described below. It is large enough for the greatest combined requirement of any case, with the required padding and alignment. Data from different cases can share this region; components of the same case occupy distinct locations.
2. **Managed references**: Reference fields shared across cases, with enough slots for the case containing the most references. Each slot is normally typed as `object`, but can have a more specific reference type when that type can represent every value assigned to it. For example, a slot used only for strings can be typed as `string`. Reconstruction obeys the reference type checks described above.
3. **Typed values**: Strongly typed fields for arbitrary value-type payloads and type parameters not known to be reference types. Slots of the same type can be shared across cases, with enough slots for that type's maximum requirement in any case. Distinct types cannot generally share a typed slot. A type parameter known to be a reference type, such as `T where T : class`, can use the managed-reference pool.

A `struct` constraint does not establish that a type parameter contains no references. An `unmanaged` constraint establishes that property to the C# compiler, but does not generally provide a fixed byte size at declaration time or a runtime-enforced layout contract. Such parameters can remain in typed slots even when constrained to `unmanaged`.

Each pool is sized for its maximum requirement across cases. Those maxima may come from different cases, so the combined payload can be larger than the largest individual case. Separate complete-case fields or hybrid representations are also possible when their storage and copying costs are preferable.

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

By default, a struct from another assembly remains an opaque typed value, even when its compile-time definition satisfies `unmanaged`. The [`unmanaged` constraint is enforced by the language, not the CLR](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.3/blittable.md#detailed-design). Recompiling a caller against a new reference-containing definition rejects an unmanaged-constrained use, but replacing the dependency of an existing binary does not necessarily reject that instantiation. The default enum representation must not turn that change into an unchecked copy of references into byte storage.

A possible user [opt-in described below](#possible-checked-packing-opt-in) would allow placing these values in unmanaged storage, subject to compile-time and runtime validation of their unmanagedness and size.

The compiler can directly arrange the declared payload components of its generated case types. Decomposing a nested user-defined struct requires preserving all of its value state and reconstructing it without invoking user getters, setters, or constructors. It must also respect accessibility, type safety, and supported versioning. If those conditions cannot be established, the value remains in a typed slot. Being declared in the current assembly is not enough when layout-relevant dependencies cross an assembly boundary.

Unmanagedness and byte layout are separate questions. Architecture, native-sized fields, alignment, and padding can prevent a fixed layout from being known even for a concrete unmanaged struct. Reference-assembly metadata used for compilation is not a promise that the execution-time type has one fixed size or layout. Any decomposition or raw packing must account for all of the assumptions it needs.

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

`Timestamp` keeps its `DateTime` in a typed slot. Although its current implementation is unmanaged and eight bytes, `DateTime` is outside the predefined-type guarantees above. Its current representation is not a permanent layout contract, so the conservative strategy does not put it in `_unmanaged`.

#### Case Conversions

Converting a case value to the enum packs its constituent data into the assigned slots and sets the discriminator. Extracting a case checks the discriminator, reads the slots, and reconstructs the case value. These operations rearrange (or "swizzle") the data between the case's own layout and the enum's shared layout; they cannot generally reinterpret the enum's payload as the complete case type.

In the example above, constructing an enum from `Buffer` stores its array in `_reference0` and its two integers in `_unmanaged`. Extracting `Buffer` reconstructs the case from those locations. Constructing from `Pair` instead uses both reference slots and needs no unmanaged data. Unused reference and typed slots are initialized or cleared to their default values, so the enum does not retain references belonging to an inactive case.

Storing every complete case in a separate field would avoid this rearrangement, but would pay for the sum of the cases' sizes even though only one case is active. That cost is especially significant for structs, which are copied by value and stored inline in arrays and containing objects. Shared storage reduces that cost at the expense of packing and reconstruction. The compiler may choose either strategy or a hybrid based on their storage and execution costs.

For `Option<T>`, the shared storage is simply one typed `T` slot:

```csharp
enum struct Option<T>
{
    None,
    Some(T value)
}

// Conceptually translates to:
public struct Option<T> : IUnion
{
    public readonly struct None { }
    public readonly record struct Some(T value);

    // None needs no payload; Some needs one typed T slot.
    private byte _discriminator;
    private T _value;
    
    object? IUnion.Value => _discriminator switch
    {
        1 => new None(),
        2 => new Some(_value),
        _ => null
    };
    
    // Non-boxing access pattern
    public bool TryGetValue(out None value)
    {
        value = default;
        return _discriminator == 1;
    }

    public bool TryGetValue(out Some value)
    {
        if (_discriminator == 2)
        {
            value = new Some(_value);
            return true;
        }
        value = default!;
        return false;
    }
    
    // Constructors and implicit case conversions
    public Option(None _) => _discriminator = 1;
    public Option(Some some) => (_discriminator, _value) = (2, some.value);

    public static implicit operator Option<T>(None value) => new(value);
    public static implicit operator Option<T>(Some value) => new(value);
}
```

The `TryGetValue` accessors reconstruct case values without boxing. Access through the `object`-typed `IUnion.Value` property boxes the returned case struct.

#### Fully Unmanaged Cases

When all case data is unmanaged and the required layout facts can be established safely, overlapping complete case values in one unmanaged region is a possible optimization. The region can hold the largest case, subject to alignment and padding, in addition to the discriminator.

Preserving each case's representation allows construction and extraction to copy complete values without swizzling individual fields. The compiler may use this strategy when it satisfies the safety and versioning requirements above. Unmanagedness alone does not guarantee this layout, a minimum enum size, or the absence of reconstruction. Generic cases and external structs may still require typed slots.

#### Possible Checked Packing Opt-in

We should consider a compiler-recognized attribute asserting that a payload type will remain unmanaged and have a specified byte size. Both declaration-site and use-site assertions could be supported: a supplying library could annotate its struct declaration, or a consuming enum could annotate an individual case parameter without requiring the referenced type to be annotated. For example, a library could declare the contract using illustrative syntax:

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

For either placement, the compiler must verify at compilation that the asserted payload type is a struct satisfying the ordinary C# unmanaged rules. A reference-containing type is an error regardless of the supplied size; the compiler does not trust the attribute as evidence of unmanagedness. It may also diagnose a size mismatch when the managed size is statically known. The attribute name, syntax, and detailed placement rules remain design questions.

For an annotation on a type, the compiler would inject checks at the beginning of its static constructor, preserving any user-written initialization. For `Point`, the generated validation would be conceptually equivalent to this constructor and these helpers:

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

The validation helpers are illustrative compiler-generated code, rather than proposed public APIs; their placement can allow sharing across types. Both checks are mandatory: a type can remain unmanaged while growing beyond the reserved storage. The checks use its actual runtime managed representation, rather than its interop marshaling size, under the ordinary static-initialization rules for each constructed type.

A consuming enum that chooses raw packing must validate its recorded layout assumptions once per constructed enum type, through a generated static constructor. Generated constructors and case-to-enum conversions must trigger that initialization before packing any payload, following the existing [struct static-constructor rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#16410-static-constructors). Extraction from a non-default enum can then rely on the completed validation; it does not require another reference-containment or size check on each access. Any alternative lowering must preserve this guarantee.

An all-zero enum can be created by default values or array initialization without running an instance constructor. This is safe: zero denotes no case or a designated data-free default case, and no packed payload needs to be reconstructed. A failed extraction may assign a typed `default` to its out parameter. Merely creating or copying such a value requires no additional layout validation.

Using a payload type does not by itself ensure that its own static constructor has run: default values, array initialization, layout queries, and whole-value copies can bypass it. The enum therefore performs its own validation using the size recorded when it was compiled. A later library version could also change or remove the annotation, or change its size from 8 to 16 and validate that new size successfully. The old consumer must still validate against its own expected size of 8. Calling only the current payload type's initializer is insufficient.

Alternatively, a consuming enum could make a use-site assertion about `DateTime`, even though the framework type is not annotated:

```csharp
enum struct CompactMessage
{
    Timestamp([Unmanaged(size: 8)] DateTime value),
    Count(long value)
}
```

Here, `8` is `DateTime`'s current managed size, rather than a language guarantee. The compiler must verify its unmanagedness at compilation. If the enum uses raw packing, its generated static initialization must also check that `RuntimeHelpers.IsReferenceOrContainsReferences<DateTime>()` is false and that `Unsafe.SizeOf<DateTime>()` is 8 as part of that one-time validation. These checks would permit the two cases to share an eight-byte unmanaged payload region, with the discriminator stored separately. The annotation permits that packing without requiring it; its placement on the parameter is illustrative.

Failed validation must prevent the raw operation and produce a managed exception, such as a type-initialization failure. Process termination is not required. Raw copies must remain within their storage bounds. The checks in [`MemoryMarshal.Read` and `Write`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/MemoryMarshal.cs) provide an existing model: they reject reference-containing types and check buffer capacity before copying.

With these guarantees, the opt-in need not require `unsafe`: an incorrect assertion fails safely instead of permitting memory corruption. The annotation establishes only reference containment and total managed size; it grants no unchecked assumptions about internal field offsets, alignment, or field accessibility. The compiler can copy values opaquely, or separately establish any additional layout facts it needs. Checked reference extraction and the tearing guarantees above continue to apply.

The annotation makes the versioning trade-off explicit. A dependency update can cause validation of an annotated type or consuming enum to fail even if its ordinary typed API would still work. The conservative default avoids introducing that hidden layout dependency. Neither the annotation nor its byte-size assertion promises a particular layout for the enum itself. Whether to add this optimization contract remains a joint decision for LDM and the runtime team, supported by performance measurements.

## 6. Pattern Matching and Behaviors

### Unified Pattern Matching

Shape enums inherit all union pattern matching behavior:

```csharp
var message = operation switch
{
    Open(var path) => $"Opening {path}",
    Close => "Closing file",
    Read(_, var offset, var count) => $"Reading {count} bytes at {offset}",
    Write(var buffer) => $"Writing {buffer.Length} bytes"
};
```

### Exhaustiveness

The compiler tracks all declared cases. Both constant and shape enums can be open or closed (see [Closed Enums proposal](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md)).

Open enums can be used to signal that the enum author may add new cases in future versions—consumers must handle unknown cases defensively (e.g., with a default branch). Closed enums signal that there is no need to handle unknown cases, such as when the case set is complete and will never change—the compiler ensures exhaustive matching without requiring a default case.

For constant enums, "open" also means values outside the declared set can be safely freely cast to the enum type.

```csharp
closed enum Status { Active, Pending(DateTime since), Inactive }

// Compiler knows this is exhaustive - no default needed
var description = status switch
{
    Active => "Currently active",
    Pending(var date) => $"Pending since {date}",
    Inactive => "Not active"
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

### All Union Behaviors

Shape enums automatically get:
- Implicit conversions from case values
- Nullability tracking
- Well-formedness guarantees

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
enum class Result<T, E>
{
    Ok(T value),
    Error(E error);
    
    public Result<U, E> Map<U>(Func<T, U> mapper) => this switch
    {
        Ok(var value) => Result<U, E>.Ok(mapper(value)),
        Error(var err) => Result<U, E>.Error(err)
    };
}

enum struct Option<T>
{
    None,
    Some(T value);
    
    public T GetOrDefault(T defaultValue) => this switch
    {
        Some(var value) => value,
        None => defaultValue
    };
}
```

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
            ConnectionState.Reconnecting(null, attempts + 1, DateTime.Now.AddSeconds(Math.Pow(2, attempts))),
        Connecting(_, _) => 
            ConnectionState.Failed("Connection timeout", new TimeoutException()),
        Connected(var endpoint, _) => 
            ConnectionState.Reconnecting(endpoint, 1, DateTime.Now.AddSeconds(1)),
        _ => this
    };
}
```

## 8. Design Decisions and Trade-offs

### Why Extend `enum`

- **Familiarity**: Developers already understand enums conceptually
- **Progressive disclosure**: Simple cases remain simple
- **Cognitive load**: One concept instead of two
- **Migration path**: Existing enums can be enhanced incrementally

### Union Foundation

Shape enums are discriminated unions expressed through enum syntax. By building on union machinery:
- All union optimizations automatically benefit shape enums
- No risk of semantic divergence between features
- Simple mental model: shape enums generate types and combine them with a union
- Future union enhancements immediately apply

### Storage Strategy Trade-offs

The distinction between `enum class` and `enum struct` allows developers to choose the right trade-off, similar to choosing between `record class` and `record struct`.

## 9. Performance Characteristics

### Memory Layout

**`enum class`**:
- Union contains single reference (8 bytes on 64-bit)
- Case instances allocated on heap
- Singleton pattern for parameter-less cases

**`enum struct`**:
- Layout and size are compiler implementation details; shared pools, typed slots, and separate-case storage are candidate strategies
- Inline storage, no heap allocation
- The conservative strategy preserves type and memory safety, including during torn reads and writes
- Sharing mixed case storage can require packing and reconstruction through non-boxing accessors
- Complete unmanaged cases may overlap when their layout assumptions can be established safely; packing external value types into raw storage would require the proposed checked opt-in

### Allocation Patterns

```csharp
// Allocation per construction
enum class Result { Ok(int value), Error(string message) }
var r1 = Result.Ok(42);  // Heap allocation

// No allocation
enum struct Result { Ok(int value), Error(string message) }  
var r2 = Result.Ok(42);  // No allocation for case construction
```

### Optimization Opportunities

Shape enums benefit from all union optimizations:
- Singleton cases to shared instances
- Small structs fitting in registers
- Pattern matching via optimized paths
- Exhaustive switches avoiding default branches

## 10. Open Questions

1. **Nested type accessibility**: Should users reference generated case types directly?
2. **Partial support**: Should enhanced enums support `partial` for source generators?
3. **Default values**: What should `default(EnumType)` produce for shape enums?
4. **Serialization**: How should enhanced enums interact with System.Text.Json?
5. **Additional state**: Should shape enums allow instance fields outside case data?
6. **Custom constructors**: Should enums allow custom constructors that delegate to cases?
7. **Construction syntax**: `Result.Ok(42)` or `new Result.Ok(42)` or both?
8. **Generic cases**: Should cases support independent generic parameters?
9. **Interface implementation**: Should enhanced enums automatically implement `IEquatable<T>`?
10. **Exact lowering**: Should the spec define exact names and shapes of generated types?
11. **Checked packing**: Should a checked `[Unmanaged(size: ...)]` contract permit raw payload storage, and where should the annotation be placed?

## Appendix A: Grammar Changes

```antlr
enum_declaration
    : attributes? enum_modifier* 'enum' ('class' | 'struct')? identifier enum_base? enum_body ';'?
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
    | parameter_list
    ;
```
