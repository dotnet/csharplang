# Discriminated Unions and Enhanced Enums for C#

This proposal introduces enhanced enums as a way to express discriminated unions in C#. Like [type unions](../../../proposals/csharp-15.0/unions.md), they represent a value drawn from a set of possible case types. Enhanced enums define the enum and its named, data-carrying cases together, with a choice of class or struct representation.

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

Type unions specify a set of possible case types, which may be existing types. Shape enums define fresh, named cases together with the data each case carries. Both can express algebraic sum types, but shape enums preserve the identity of each declared case even when multiple cases have the same payload types.

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

### From Type Unions to Discriminated Unions

Type unions represent "one of several types". Discriminated unions give each alternative a distinct case identity and its own payload shape, where:

- Cases are defined together as a logical unit
- Each case may carry different data
- The set of cases is typically closed and known at design time

Shape enums provide natural syntax for this pattern, defining the enum and its case types in a single declaration.

### Limitations of Current Enums

Today's C# enums have significant limitations:

1. **No associated data**: Cases are merely integral values
2. **Not truly exhaustive**: Any integer can be cast to an enum type
3. **Limited to integers**: Cannot use strings or doubles

Enhanced enums address all these limitations while preserving conceptual simplicity.

### Building on Familiar Concepts

By extending the existing `enum` keyword, enhanced enums provide a grow-up story. Simple enums remain simple, while advanced scenarios become possible without abandoning familiar patterns.

## 3. Type Unions and Shape Enums

The [unions proposal](../../../proposals/csharp-15.0/unions.md) describes union types whose contents can be one of their case types. Those case types can be existing types, may overlap, and can carry data through their own members. A shape enum instead declares fresh case types together with the enum. For example, `Left(int value)` and `Right(int value)` remain different cases even though both carry an `int`.

The related behaviors include case-to-enclosing-type conversion, matching against case types, and exhaustiveness analysis. Shape enums specify those behaviors for their own representations:

- An `enum class` is an abstract base class whose sealed case types derive from it. Reference conversions and pattern matching operate on the case instance itself.
- An `enum struct` stores a discriminant and the active case's data. Generated members support construction, case tests, and extraction without boxing; the compiler recognizes its declared case types for pattern matching.

Union types can also be classes or structs, while the current `union` declaration syntax generates a struct storing its contents in an object reference. Shape enums offer the class hierarchy and inline struct representations described here. Whether either representation should literally implement the type-union pattern is a separate design choice discussed in [Appendix B](#appendix-b-possible-connections-to-type-unions).

## 4. Enhanced Enums

### Design Principles

- **Progressive enhancement**: Simple enums stay simple; complexity is opt-in
- **Data carrying**: Each case can carry its own constituent data
- **Familiar syntax**: Builds on existing enum and record concepts
- **Known cases**: A shape enum declares its case set together with its case types

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

Create a shape enum by:
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

Each case with a parameter list generates a nested record type. `enum class` generates `sealed record class` types that derive from the enum's base class; `enum struct` generates `readonly record struct` types. While these are the generated types, the struct implementation may optimize internal storage.

TODO: Should the structs be readonly?  Seems like that goes against normal structs (including `record struct`).  Seems like that could be opt in with `readonly enum struct X`.

#### Combination Rules

- **Constant enums**: Can use extended base types but NOT have parameter lists
- **Shape enums**: Can have parameter lists but NOT specify a base type
- **Mixing cases**: Cannot mix constant values and parameterized cases. An `enum struct` may designate one case without a parameter list as `= default`; this selects the case represented by zero-initialized storage, rather than assigning a constant value.

```csharp
// ✓ Valid - constant enum with string base
enum Status : string { Active = "A", Inactive = "I" }

// ✓ Valid - shape enum with data
enum class Result { Ok(int value), Error(string msg) }

// ✓ Valid - designate a data-free struct case as the default
enum struct Option<T> { None = default, Some(T value) }

// ✗ Invalid - cannot mix constants and shapes
enum class Bad { A = 1, B(string x) }

// ✗ Invalid - class defaults are always null
enum class Bad { None = default, Some(int value) }

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

### Shape Enums: Data-Carrying Cases

Shape enums define the enum type and its cases with integrated syntax:

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

**`enum class`** creates an abstract base class with sealed reference type cases:
- Cheap to pass around (pointer-sized)
- No struct tearing risk
- Natural null representation

**`enum struct`** creates an enum with inline value-type case storage:
- Inline case storage without boxing on construction
- Better cache locality
- Reduced GC pressure

#### Default Values

For an `enum class`, `default(E)` is `null`. It represents no case, even when the declaration includes a data-free case named `None`. Case type tests fail for this value. Ordinary reference conversions apply: a downcast of `null` to a case type produces `null`, whereas a downcast of a non-null instance of a different case throws `InvalidCastException`.

For an `enum struct`, the discriminant value zero is reserved for an *uninitialized value* unless a default case is designated. All ordinary cases have nonzero discriminants, including data-free cases. The zero-initialized representation matches no case; its generated `HasValue` property is false, every case predicate and `TryGetValue` returns false, and a cast to any case throws `InvalidCastException`. This allows declarations in which every case requires state without inventing a case containing default payload values.

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

The designation is allowed only on an `enum struct` case declared without a parameter list, and at most once per declaration. The designated case uses discriminant zero; all other cases use nonzero discriminants. Constructing or converting the designated case to the enum produces the same representation as `default(E)`. No payload needs initialization for that case. Declarations with only data-carrying cases therefore retain the uninitialized zero representation.

These rules also apply to zero-initialized array elements and fields. The designation defines the meaning of zero-initialized storage and does not depend on executing a constructor. Apart from the meaning of zero, numeric discriminant values are implementation details.

A null payload does not mean that the enum has no active case. For example, `new Option<string?>.Some(null)` creates a `Some` case, and converting it to `Option<string?>` preserves that case identity.

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

Shape enums generate nested case types. An `enum class` uses inheritance to represent its cases; an `enum struct` stores a discriminant and case data and generates members for case access. The following translations illustrate the proposed API surface, rather than prescribing exact storage layouts or metadata names.

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

The enum type is the base class, and a variable of that type holds the actual case reference. The base is abstract, so there is no separate base-class value that represents no case. Since the generated cases are record classes, the illustrative base is also a record class.

Only the declared case types may derive from an `enum class`. This restriction applies when consuming the type from another assembly as well. The private constructor in the illustration does not by itself enforce the restriction: record copy constructors also permit derivation in ordinary C#. Enum case metadata and consuming compiler checks must preserve the declared hierarchy, along the lines of the [closed-hierarchy proposal](../../../proposals/csharp-15.0/closed-hierarchies.md#lowering). The exact metadata encoding remains open.

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

Matching a data-free case tests its type, rather than its reference identity. Record operations such as `with` may produce another instance of the same case type; those instances still represent the same case.

### `enum struct` Translation

An `enum struct` stores a discriminant and shared inline storage for its case data. The goal is to use the smallest storage that can safely represent any case without boxing its data.

The runtime does not allow a managed reference to overlap non-reference data. The garbage collector must be able to identify reference locations from the type's layout, independently of the current discriminant. Consequently, a single byte buffer large enough for the largest case cannot generally hold arbitrary case values that are reinterpreted on access.

#### Shared Storage

The compiler separates the case data into storage that can safely be shared:

1. **Unmanaged data**: An inline array of bytes large enough for the largest combined unmanaged payload of any one case, accounting for required alignment and padding. Unmanaged fields from different cases share this buffer; fields within the same case occupy distinct locations.
2. **Managed references**: Reference fields shared across cases, with enough slots for the case containing the most references. Each slot is normally typed as `object`, but can have a more specific reference type when that type can represent every value assigned to the slot. For example, a slot used only for strings can be typed as `string`.
3. **Generic data**: Strongly typed fields for type parameters whose constraints do not establish that they are reference types. For each such type parameter `T`, there are enough `T` slots for the maximum number of `T` values in any one case. Slots can be shared between cases, but slots for distinct type parameters cannot generally be combined. A type parameter known to be a reference type, such as `T where T : class`, can instead use the managed-reference slots.

A `struct` constraint does not establish that a type parameter is unmanaged: a struct may contain references. Even an `unmanaged` constraint does not generally give the compiler a fixed byte size at declaration time, so generic data may still require typed slots.

Value-type fields that themselves contain references also need a GC-safe representation. When the compiler can safely decompose and reconstruct such a value, its constituent data can use the same storage pools. Otherwise, it must remain in a strongly typed slot, shared with fields of the same type in other cases.

Each pool is sized for its maximum requirement across cases, rather than the sum of all cases' requirements. Those maxima may come from different cases, so the resulting payload can be larger than the largest individual case. Exact field ordering, alignment, and slot assignment are implementation details.

For example:

```csharp
enum struct Message<T>
{
    Text(string text, int code),
    Buffer(byte[] data, int offset, int count),
    Pair(string first, string second),
    Values(T first, T second)
}

// Conceptual storage (generated case types and accessors omitted):
public struct Message<T>
{
    private byte _discriminant;

    // Buffer has the largest unmanaged payload: two ints.
    private UnmanagedData _unmanaged;

    // Pair needs two references. The first slot also stores Text's string
    // or Buffer's byte array; the second is used only for a string.
    private object? _reference0;
    private string? _reference1;

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

Converting a case value to the union packs its constituent data into the assigned slots and sets the discriminant. Extracting a case checks the discriminant, reads the slots, and reconstructs the case value. These operations rearrange (or "swizzle") the data between the case's own layout and the union's shared layout; they cannot generally reinterpret the union's payload as the complete case type.

In the example above, constructing a union from `Buffer` stores its array in `_reference0` and its two integers in `_unmanaged`. Extracting `Buffer` reconstructs the case from those locations. Constructing from `Pair` instead uses both reference slots and needs no unmanaged data. Unused reference and typed slots are initialized or cleared to their default values, so the union does not retain references belonging to an inactive case.

Storing every complete case in a separate field would avoid this rearrangement, but would make the union pay for the sum of the cases' sizes even though only one case is active. That cost is especially significant for structs, which are copied by value and stored inline in arrays and containing objects. Sharing storage and reconstructing cases is therefore the general strategy for compact, non-boxing storage of mixed case data.

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
    public readonly struct None { }
    public readonly record struct Some(T value);

    // Zero represents no case in this declaration.
    // None needs no payload; Some needs one typed T slot.
    private byte _discriminant;
    // The payload slot may be unused and contain default(T).
    private T? _value;

    public bool IsNone => _discriminant == 1;
    public bool IsSome => _discriminant == 2;
    public bool HasValue => _discriminant != 0;

    // Non-boxing case access
    public bool TryGetValue(out None value)
    {
        value = default;
        return _discriminant == 1;
    }

    public bool TryGetValue(out Some value)
    {
        if (_discriminant == 2)
        {
            value = new Some(_value!);
            return true;
        }
        value = default;
        return false;
    }

    // Construction and case-to-enum conversions
    public Option(None _) => _discriminant = 1;
    public Option(Some some) => (_discriminant, _value) = (2, some.value);

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

If this declaration instead uses `None = default`, the `None` constructor sets the discriminant to zero, and `IsNone` and `TryGetValue(out None)` test for zero. `HasValue` is then always true for values produced by the generated APIs, including `default(Option<T>)`.

#### Fully Unmanaged Cases

When all case data is unmanaged and the case layouts are statically known, the compiler overlaps the complete case values in a single unmanaged storage region. The payload then requires only the size of the largest case, subject to alignment and padding, in addition to the discriminant.

This layout preserves each case's own representation, so constructing or extracting a case copies its complete value without swizzling individual fields. Authors who need minimal storage and no rearrangement of case data get that layout when their cases meet these requirements. Generic cases that still require typed slots remain subject to the constraints described above.

### Generated APIs and Conversions

The proposed minimum API exposes the nested case types so that source code can construct, test, and extract them. Case types are public within the enclosing enum, so their effective accessibility is bounded by that enum's accessibility. Nested cases of a generic enum use the enclosing type parameters and constraints.

Data-carrying cases have the constructors, payload properties, and deconstruction supported by their generated record types. Data-free class cases expose a shared `Instance`; data-free struct cases can be constructed with `new E.Case()`.

For classes, ordinary inheritance provides implicit case-to-enum reference conversions, `is` type and declaration patterns, `as`, and explicit enum-to-case reference conversions:

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
- A boolean property named `Is` followed by the case name, such as `IsNone`, testing only case identity.
- `HasValue`, which is false for the uninitialized representation and true for any active case, including a designated default case.
- A `TryGetValue(out Case value)` overload for each case. It returns true and supplies the case value when that case is active; otherwise it returns false and assigns `default(Case)` to the out parameter.
- An explicit conversion from the enum type to each case type. It returns the active case value or throws `InvalidCastException` if a different case, or no case, is active.

The generated operators allow callers to construct and extract cases through ordinary C# conversions. The case predicates, `TryGetValue`, and case conversions do not require boxing.

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

Struct case type tests are recognized by the compiler when the input is statically known to be a shape enum. They do not establish CLR inheritance between the enum and the case types. Boxing an enum struct boxes the enum itself, so a case type test on a value whose static type is `object` does not extract its case. The `as` operator does not invoke the generated conversions; `TryGetValue` provides conditional case extraction for structs.

Generated members must agree on the active case. Extracting a struct case produces a value, not a reference into the enum's storage. Its payload is reconstructed as needed by the storage strategy, and managed-reference payloads retain their original references.

The proposed API uses ordinary constructor syntax, such as `new Result.Success("ok")`, rather than a generated same-name factory. Ordinary C# cannot declare a nested case type alongside a method or property with the same name. User-declared members that conflict with generated members are errors; additional construction shorthand and the final generated API names remain open questions.

## 6. Pattern Matching and Behaviors

### Case Pattern Matching

Class cases use normal reference type patterns and record deconstruction. For structs, a type or declaration pattern naming a declared case tests whether that case is active and, on success, makes its value available without boxing. Property and positional patterns for that case operate on the extracted case value. Patterns examining the enum's own members operate on the enum value.

```csharp
var message = operation switch
{
    Open(var path) => $"Opening {path}",
    Close => "Closing file",
    Read(_, var offset, var count) => $"Reading {count} bytes at {offset}",
    Write(var buffer) => $"Writing {buffer.Length} bytes"
};
```

For an `enum class`, the `null` pattern matches a null enum reference. For an `enum struct`, `HasValue` distinguishes the uninitialized representation from an active case. Whether the `null` pattern should also match the uninitialized representation remains an open question. With a designated default case, zero represents an active case. A case containing a null payload still matches its case type.

### Switch Translation

The emit strategy for `switch` statements and expressions over shape enums remains open. The following strategies are candidates to validate through benchmarks and inspection of generated code. We believe the compiler should be able to emit case tests that the runtime can optimize efficiently.

Current JIT code generation does not yet produce the intended dispatch optimizations for the patterns below. In local .NET 11 RC1 checks on ARM64 with 16 sealed class cases and 16 struct cases, both exact-type comparison chains and discriminant-predicate chains remained linear in fully optimized code and Tier 1 code with dynamic PGO. Struct predicates inlined into discriminant comparisons, but those comparisons did not become a jump table or comparison tree. We believe these transformations should be possible with additional runtime support; performance validation is still needed to choose the default strategy.

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
// Evaluate the input and unwrap its active case reference once,
// if the union representation uses a wrapper.
var __case = /* active case reference from e */;
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

Although the emitted tests form a linear chain, they compare one runtime type against distinct known types. For a large number of cases, we believe the runtime could recognize this structure and choose a balanced comparison tree or another efficient dispatch strategy. When that strategy beats a linear chain or profile-guided ordering needs validation.

#### `enum struct` Case Tests

For an `enum struct`, we expect generated case predicates along these lines:

```csharp
public bool IsC1 => _discriminant == 1;
public bool IsC2 => _discriminant == 2;
public bool IsC3 => _discriminant == 3;
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
if (__value._discriminant == 1) { /* C1 body */ }
else if (__value._discriminant == 2) { /* C2 body */ }
else if (__value._discriminant == 3) { /* C3 body */ }
```

These are comparisons of the same integral discriminant against distinct constants. We believe the runtime could select a jump table for dense case values, a comparison tree for sparse values, or a linear chain when that is more efficient. It should also be able to eliminate redundant discriminant checks in case extraction after a successful predicate. In the local checks, marking the struct conversions for inlining allowed their redundant checks to be removed, but the dispatch chain remained linear. Predicate inlining alone does not establish that the comparison chain will become a jump table or tree.

#### Runtime Recognition and Alternatives

We should consider teaching the runtime to recognize exact-type comparison chains and discriminant predicates with distinct case bodies. The runtime already has a [limited switch-recognition pass](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/switchrecognition.cpp#L28) for constant-comparison chains whose successful tests all reach the same destination. It runs after inlining, but stops recognizing a chain when successful tests reach different destinations, as they do in these DU switches. Extending this recognition is one possible starting point.

Another option is an attribute in `System.Runtime.CompilerServices` identifying a well-behaved discriminated union and describing its case mapping and representation. If optimizing these patterns in arbitrary user code raises correctness concerns, the attribute could provide an explicit opt-in with a well-defined contract. That contract would specify how case identity remains stable during dispatch, that case predicates are side-effect-free and agree with the case mapping, and how extraction behaves after a successful test.

The presence of the attribute would assert that the type follows this contract, giving the runtime a basis for DU-specific optimization. For a struct, the metadata could identify the discriminant field, allowing the runtime to read it directly when optimizing case dispatch. These optimizations must preserve the observable semantics of the switch, including its guards and case bodies. No attribute name or contract is selected here.

Class-based unions could also use the same predicate-and-extraction emit as struct-based unions if the runtime can optimize the class representation efficiently. The final choice should follow performance validation, including switches with many cases, different case distributions, guards, and both statement and expression forms.

If these approaches do not produce satisfactory results, a final fallback is to expose a discriminant and have the compiler emit an integral switch over it directly. This would use the runtime's existing integral-switch optimizations without requiring it to recover a switch from a comparison chain. Class-based unions would also need a discriminant mapping for their cases. Case extraction must still preserve the conversion semantics, including reconstructing data from shared storage when necessary.

### Exhaustiveness

The compiler tracks all declared cases. Both constant and shape enums can be open or closed. Constant enums follow the [Closed Enums proposal](../../../proposals/closed-enums.md); class shape enums also require the hierarchy information described above.

Open enums can be used to signal that the enum author may add new cases in future versions—consumers must handle unknown cases defensively (e.g., with a default branch). Closed enums signal that there is no need to handle unknown cases, such as when the case set is complete and will never change—the compiler ensures exhaustive matching without requiring a default case.

For shape enums, this open/closed distinction concerns evolution of the declared case set. It does not permit arbitrary user-defined subclasses or arbitrary discriminant values.

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

The generated APIs preserve the declared case identity. An active case remains active when one of its payload members is null, and extracting it preserves the payload members' declared types and nullability annotations.

Classes follow ordinary reference nullability rules. For structs, analysis of whether a case is active is separate from the nullability of its payload. Construction from a case, successful case tests, and successful `TryGetValue` calls establish the active case; the default-value rules determine whether zero-initialized storage has an active case.

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
        Ok(var value) => new Result<U, E>.Ok(mapper(value)),
        Error(var err) => new Result<U, E>.Error(err)
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

## 8. Design Decisions and Trade-offs

### Why Extend `enum`

- **Familiarity**: Developers already understand enums conceptually
- **Progressive disclosure**: Simple cases remain simple
- **Cognitive load**: One concept instead of two
- **Migration path**: Existing enums can be enhanced incrementally

### Two Representations

Shape enums provide the same case declaration syntax with two representations:

- `enum class` uses a base class and sealed case types, with ordinary reference conversions and pattern matching.
- `enum struct` uses a discriminant, inline case storage, and generated members for non-boxing case access.
- Both expose their declared case set for exhaustiveness checking, with the default and no-case behavior specified above.

### Storage Strategy Trade-offs

The distinction between `enum class` and `enum struct` allows developers to choose the right trade-off, similar to choosing between `record class` and `record struct`.

## 9. Performance Characteristics

### Memory Layout

**`enum class`**:
- A variable holds the case reference itself (8 bytes on 64-bit)
- Case instances allocated on heap
- Singleton pattern for parameter-less cases

**`enum struct`**:
- Size includes the discriminant and the maximum storage needed in each shared pool, with required alignment and padding
- Inline storage, no heap allocation
- Mixed case data is packed and reconstructed through the enum's non-boxing accessors
- Fully unmanaged cases with statically known layouts share space for the largest complete case, without swizzling
- Generated predicates and extraction methods access cases without boxing

### Allocation Patterns

```csharp
// Allocation per construction
enum class Result { Ok(int value), Error(string message) }
Result r1 = new Result.Ok(42); // Heap allocation

// No allocation
enum struct Result { Ok(int value), Error(string message) }  
Result r2 = new Result.Ok(42); // Inline case storage; no allocation for construction
```

### Optimization Opportunities

Potential optimizations include:
- Singleton cases to shared instances
- Small structs fitting in registers
- Pattern matching via optimized paths
- Using the known case set to reduce redundant case checks

## 10. Open Questions

1. **Case accessibility**: Should individual cases support access modifiers in addition to the proposed public nested case types?
2. **Partial support**: Should enhanced enums support `partial` for source generators?
3. **Optional type-union integration**: Should either representation implement the union pattern, as discussed in [Appendix B](#appendix-b-possible-connections-to-type-unions)?
4. **Serialization**: How should enhanced enums interact with System.Text.Json?
5. **Additional state**: Should shape enums allow instance fields outside case data?
6. **Custom constructors**: Should enums allow custom constructors that delegate to cases?
7. **Construction shorthand**: Should `Result.Ok(42)` be supported in addition to `new Result.Ok(42)`, and how would it coexist with the nested case type name?
8. **Generic cases**: Should cases support independent generic parameters?
9. **Interface implementation**: Should enhanced enums automatically implement `IEquatable<T>`?
10. **Exact lowering**: What names and shapes should the generated APIs and hierarchy metadata have, and are additional convenience APIs needed?
11. **Uninitialized matching**: Should the `null` pattern match an uninitialized enum struct, or should this state be tested only through case-state APIs?

## Appendix A: Grammar Changes

```antlr
enum_declaration
    : attributes? enum_modifier* 'enum' identifier enum_base? enum_body ';'?
    | attributes? enum_modifier* 'enum' ('class' | 'struct') identifier type_parameter_list?
      type_parameter_constraints_clause* shape_enum_body ';'?
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

The `= default` form is permitted only in an `enum struct`, at most once per declaration. It designates a case without a parameter list. Other explicit case initializers are not permitted in shape enums. Type parameter lists and constraints are permitted on shape enums, while constant enums retain their existing non-generic declarations.

## Appendix B: Possible Connections to Type Unions

Shape enums and [type unions](../../../proposals/csharp-15.0/unions.md) both describe a value drawn from a set of possible case types. Shape enums declare fresh, named case types together with the enum and preserve their case identity, even when different cases carry the same payload types. This makes connections between the features possible, but does not require the same implementation or all of the same language behaviors.

The design above does not require either representation to implement the union pattern, carry `[Union]`, or implement `IUnion`. Whether to generate union-pattern implementations for structs, classes, both, or neither is an LDM decision. The following discusses possible integration rather than specifying additional requirements.

### Union Types, Declarations, and Runtime Access

The unions proposal distinguishes a *union type* from a *union declaration*. A custom union type can be a class or struct. Compiler recognition uses `[Union]` and a pattern of creation and access members. The `union` declaration syntax chooses a particular struct implementation that stores its contents in an object reference. Implementing the custom-union pattern would therefore allow shape enums to retain their own representation and storage strategy.

The `IUnion` interface is a separate option for runtime access to a value's contents. Implementing it alone does not opt a type into compiler union behavior. This distinction is described in the [union patterns update](union-patterns-update.md#marking-union-types-with-an-attribute).

### Possible Integration for `enum class`

An enum class could implement the union pattern while remaining a base class with sealed case types. Using a [union member provider](../../../proposals/csharp-15.0/unions.md#union-member-providers), generated `Create` methods could return the supplied case reference unchanged, and the provider's `Value` property could return `this`. An optional `IUnion` implementation could expose that same reference to runtime consumers. None of these members would require a wrapper allocation.

This could give class enums a common access protocol with other union types and allow code that understands that protocol to consume them uniformly.

The language benefits are limited, however. Inheritance already supplies case-to-enum conversions, type tests, `as`, downcasts, record deconstruction, and normal null behavior. Hierarchy metadata can supply the declared case set for exhaustiveness. Union-pattern integration would introduce additional APIs or provider metadata largely to describe behavior that these mechanisms already provide. It would also need to preserve the expected reference semantics and the restriction that only declared case types derive from the enum; the union pattern alone does not enforce that restriction.

The `Value` view would refer to the case instance itself, rather than to a distinct contained object. Runtime consumers would need to account for that when interpreting or recursively inspecting union values.

### Possible Integration for `enum struct`

Struct enums have a closer fit with the custom-union pattern. The generated constructors could identify the case types, and the generated `HasValue` and `TryGetValue` members could satisfy the non-boxing access pattern. Integration would additionally require `[Union]` and an object-valued `Value` property that returns a boxed active case, or null when no case is active. `IUnion` could optionally expose this view at runtime.

Potential benefits include:

- Reusing the existing rules for case construction, case type matching, deconstruction, and nullable-union matching instead of specifying all of those rules independently.
- Allowing a consuming compiler that understands the union pattern to recognize the generated members and case types, even without support for the shape-enum declaration syntax.
- Preserving inline storage and non-boxing access through `TryGetValue`; the object-valued view would not require storing cases as boxed objects.
- Reusing the member-provider mechanism when protocol names conflict with the enum's public API. For example, an `Option<T>` might want its public `Value` to mean the payload and its public `HasValue` to mean `Some`, while the union protocol's members mean the active case and the presence of any case, including `None`.

The main proposal currently exposes `HasValue` directly with the active-case meaning. Changing its name or placement to accommodate a domain-specific property would be an additional API decision.

The costs and semantic choices include:

- The mandatory `Value` property would box struct cases when accessed and would require every case to be convertible to `object`. Future support for ref-like cases would require changes to that contract or a different integration strategy.
- Exposing protocol members directly would reserve public API names. A member provider could hide those members from the enum's direct public surface, but would add generated interface metadata.
- Union type patterns target the contained case value, while untyped property and positional patterns inspect the enum itself. These rules would need to be reconciled with patterns intended to test or examine the enum struct itself. The distinction is described in the [LDM decision on pattern targets](../../2026/LDM-2026-08-12.md#union-pattern-matching-targets).
- Union null patterns would make an uninitialized struct match `null`, through its null `Value`. This is a possible choice for enum structs, but does not follow just from reserving discriminant zero. A designated default case would instead have a non-null case value, and a case with a null payload would remain an active case.
- Union exhaustiveness treats the declared case types as exhaustive. The enum proposal's separate open/closed policy, including handling future cases, would need to be preserved explicitly.

Both struct default strategies can satisfy the current union contract. An uninitialized default would expose a nullable `Value`; a designated default case could expose a non-nullable `Value` and report `HasValue` as true. The generated annotations and access members would need to agree on those meanings.

### Scope of an LDM Decision

LDM could choose full union-pattern integration, reuse selected compiler machinery without generating the pattern, or keep the features independent. Such a decision should determine which representations participate, whether protocol members are exposed directly or through a provider, whether `IUnion` is useful for runtime interoperability, and how pattern targets, uninitialized matching, and open/closed exhaustiveness interact.

The enum-specific default designation, storage strategy, `IsCase` predicates, and checked struct extraction remain part of the enum design. Any JIT optimization contract would likewise need to describe the enum's stronger case and representation guarantees; `[Union]` alone supports arbitrary custom implementations and does not identify a discriminant field or its mapping. Using similar constructor and `TryGetValue` APIs does not by itself mandate union-pattern integration.
