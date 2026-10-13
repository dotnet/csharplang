# Discriminated Unions and Enhanced Enums for C#

This proposal introduces enhanced enums as an elegant way to build discriminated unions in C#. Building on [type unions](../../../proposals/csharp-15.0/unions.md) and [closed hierarchies](../../../proposals/csharp-15.0/closed-hierarchies.md), enhanced enums provide familiar, concise syntax for algebraic sum types where cases are known at declaration time.

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

The [unions proposal](../../../proposals/csharp-15.0/unions.md#summary) describes type unions. They provide:

- Implicit conversions from case types to the union type
- Pattern matching that unwraps union contents
- Exhaustiveness checking in switch expressions
- Enhanced nullability tracking
- Flexible storage strategies (boxing or non-boxing)

An `enum struct` uses these union behaviors to test and extract its active case. An `enum class` is an abstract base class whose cases derive from it; reference conversions and pattern matching operate on the case instance itself. Both forms declare their case types together with the discriminated union.

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

Each case with a parameter list generates a nested record type. `enum class` generates `sealed record class` types that derive from the enum's base class; `enum struct` generates `readonly record struct` types. While these are the generated types, the struct union implementation may optimize internal storage.

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

### Shape Enums: Discriminated Unions Made Elegant

Shape enums define a discriminated union and its cases with integrated syntax:

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

**`enum struct`** creates discriminated unions with optimized value-type storage:
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

Shape enums generate nested case types. An `enum class` uses inheritance to represent its cases; an `enum struct` stores a discriminant and case data and implements the union access patterns. The following translations illustrate the proposed API surface, rather than prescribing exact storage layouts or metadata names.

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

```csharp
public enum struct Option<T>
{
    None,
    Some(T value)
}

// Conceptually translates to:
[Union]
public struct Option<T> : IUnion
{
    public readonly struct None { }
    public readonly record struct Some(T value);

    // Zero represents no case in this declaration.
    private byte _discriminant;
    // The payload slot may be unused and contain default(T).
    private T? _value;

    public bool IsNone => _discriminant == 1;
    public bool IsSome => _discriminant == 2;
    public bool HasValue => _discriminant != 0;

    public object? Value => _discriminant switch
    {
        1 => new None(),
        2 => new Some(_value!),
        _ => null
    };
    
    // Non-boxing access pattern
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

The `[Union]` attribute identifies the struct as a union type. Its public constructors and `Value` property satisfy the basic union pattern, and `HasValue` with the `TryGetValue` overloads satisfy the non-boxing access pattern. `IUnion` provides access through the union interface described in the [unions proposal](../../../proposals/csharp-15.0/unions.md).

If this declaration instead uses `None = default`, the `None` constructor sets the discriminant to zero, `IsNone` and `TryGetValue(out None)` test for zero, and `Value` maps zero to the `None` case. `HasValue` is then always true for values produced by the generated APIs, including `default(Option<T>)`.

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
- The basic union pattern's `Value` property. It returns a boxed active case or null for an uninitialized enum. Access through `Value` may allocate; the case predicates, `TryGetValue`, and case conversions do not require boxing.

The implicit operators make case-to-enum conversion available to callers that do not recognize the union pattern. The explicit operators provide checked extraction, which the type-union proposal does not otherwise supply.

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

Struct case type tests use union pattern matching at compile time. They do not establish CLR inheritance between the enum and the case types. Boxing an enum struct boxes the enum itself, so a case type test on a value whose static type is `object` does not unwrap it. The `as` operator does not invoke the generated conversions; `TryGetValue` provides conditional case extraction for structs.

Generated members must agree on the active case. Extracting a struct case produces a value, not a reference into the enum's storage. Its payload is reconstructed as needed by the storage strategy, and managed-reference payloads retain their original references.

The proposed API uses ordinary constructor syntax, such as `new Result.Success("ok")`, rather than a generated same-name factory. Ordinary C# cannot declare a nested case type alongside a method or property with the same name. User-declared members that conflict with generated members are errors; additional construction shorthand and the final generated API names remain open questions.

## 6. Pattern Matching and Behaviors

### Unified Pattern Matching

Class cases use normal reference type patterns and record deconstruction. Struct cases use union patterns to test and extract the active case without boxing:

```csharp
var message = operation switch
{
    Open(var path) => $"Opening {path}",
    Close => "Closing file",
    Read(_, var offset, var count) => $"Reading {count} bytes at {offset}",
    Write(var buffer) => $"Writing {buffer.Length} bytes"
};
```

For an `enum class`, the `null` pattern matches a null enum reference. For an `enum struct` without a designated default case, it matches the uninitialized representation, following the union pattern's logical-null behavior (`HasValue` is false). With a designated default case, zero matches that case and does not match `null`. A case containing a null payload still matches its case type.

### Exhaustiveness

The compiler tracks all declared cases. Both constant and shape enums can be open or closed. Constant enums follow the [Closed Enums proposal](../../../proposals/closed-enums.md); class shape enums also require the hierarchy information described above.

Open enums can be used to signal that the enum author may add new cases in future versions—consumers must handle unknown cases defensively (e.g., with a default branch). Closed enums signal that there is no need to handle unknown cases, such as when the case set is complete and will never change—the compiler ensures exhaustive matching without requiring a default case.

For shape enums, this open/closed distinction concerns evolution of the declared case set. It does not permit arbitrary user-defined subclasses or arbitrary discriminant values.

Covering all declared cases does not cover the no-case state. A possibly-null class input needs a `null` arm or a fallback to handle null. A struct without a designated default case likewise needs to handle its uninitialized representation unless analysis establishes that a case is active. A struct with a designated default case has no additional uninitialized state: covering that case also covers zero-initialized values. Diagnostics should account for these states even when every case type is non-nullable; the no-case state is distinct from payload nullability.

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

### All Union Behaviors

Shape enums provide:
- Implicit conversions from case values
- Nullability tracking
- Well-formedness guarantees

For classes these follow from reference semantics and the declared hierarchy; for structs they follow from the generated union members. The default-value rules above remain part of the observable behavior of both forms.

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

### Union Foundation

Shape enums are discriminated unions expressed through enum syntax. The two representations reuse different language mechanisms:

- `enum class` uses a base class and sealed case types, with ordinary reference conversions and pattern matching.
- `enum struct` uses generated case types and the union access patterns, allowing inline storage and non-boxing case access.
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
- Size equals discriminator plus space for largest case
- Inline storage, no heap allocation
- Optimized layout per union's non-boxing pattern

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

Shape enums benefit from all union optimizations:
- Singleton cases to shared instances
- Small structs fitting in registers
- Pattern matching via optimized paths
- Exhaustive switches avoiding default branches

## 10. Open Questions

1. **Case accessibility**: Should individual cases support access modifiers in addition to the proposed public nested case types?
2. **Partial support**: Should enhanced enums support `partial` for source generators?
3. **Generated API placement**: Should the struct union members be public directly, as illustrated, or use a union member provider to reduce name conflicts?
4. **Serialization**: How should enhanced enums interact with System.Text.Json?
5. **Additional state**: Should shape enums allow instance fields outside case data?
6. **Custom constructors**: Should enums allow custom constructors that delegate to cases?
7. **Construction shorthand**: Should `Result.Ok(42)` be supported in addition to `new Result.Ok(42)`, and how would it coexist with the nested case type name?
8. **Generic cases**: Should cases support independent generic parameters?
9. **Interface implementation**: Should enhanced enums automatically implement `IEquatable<T>`?
10. **Exact lowering**: What names and shapes should the generated APIs and hierarchy metadata have, and are additional convenience APIs needed?

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
