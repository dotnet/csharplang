# Discriminated Unions and Enhanced Enums for C#

This proposal introduces enhanced enums as an elegant way to build discriminated unions in C#. Building on [type unions](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md), enhanced enums provide familiar, concise syntax for algebraic sum types where cases are known at declaration time.

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
union Result { string, ValidationError, NetworkException }

// Shape enums - discriminated unions with integrated case definitions
enum PaymentResult
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

Type unions are fully specified in the [unions proposal](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md#summary). They provide:

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
- Having a parameter list on any enum member

```csharp
enum class Result { Success, Failure }  // shape enum via 'class' keyword
enum struct Result { Success, Failure } // shape enum via 'struct' keyword
enum Result { Success(), Failure() }    // shape enum via parameter lists
```

When created via parameter lists alone, defaults to `enum class`:

```csharp
enum Result { Ok(int value), Error }    // implicitly 'enum class'
```

#### Data-Carrying Cases

```csharp
enum Result
{
    Success(string id),
    Failure(int code, string message)
}
```

Each case with a parameter list generates a nested record type. `enum class` generates `sealed record class` types; `enum struct` generates `readonly record struct` types. While these are the generated types, the union implementation may optimize internal storage.

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
enum Bad { A = 1, B(string x) }

// ✗ Invalid - shape enums cannot have base types
enum struct Bad : int { A, B }
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
enum FileOperation
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
public union Result
{
    Success,
    Failure;
    
    public sealed record class Success(string value);
    public sealed record class Failure(int code);
}
```

Singleton cases generate types with shared instances:

```csharp
enum class State { Ready, Processing, Complete }

// Translates to:
public union State
{
    Ready, Processing, Complete;
    
    public sealed class Ready 
    {
        public static readonly Ready Instance = new();
        private Ready() { }
    }
    // Similar for Processing and Complete
}
```

### `enum struct` Translation

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

    // Optimized layout: discriminator + space for largest case
    private byte _discriminant;
    private T _value;
    
    object? IUnion.Value => _discriminant switch
    {
        1 => new None(),
        2 => new Some(_value),
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
            value = new Some(_value);
            return true;
        }
        value = default!;
        return false;
    }
    
    // Constructors and factories
    public Option(None _) => _discriminant = 1;
    public Option(Some some) => (_discriminant, _value) = (2, some.value);
    
    public static Option<T> None => new Option<T>(new None());
    public static Option<T> Some(T value) => new Option<T>(new Some(value));
}
```

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

The compiler tracks all declared cases. Both constant and shape enums can be open or closed (see [Closed Enums proposal](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md)). Open enums can be used to signal that the enum author may add new cases in future versions—consumers must handle unknown cases defensively (e.g., with a default branch). Closed enums signal that there is no need to handle unknown cases, such as when the case set is complete and will never change—the compiler ensures exhaustive matching without requiring a default case. For constant enums, "open" means values outside the declared set can be cast to the enum type.

```csharp
closed enum Status { Active, Pending(DateTime since), Inactive }

// Compiler knows this is exhaustive - no default needed
var description = status switch
{
    Active => "Currently active",
    Pending(var date) => $"Pending since {date}",
    Inactive => "Not active"
};
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
enum OrderStatus
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
- Size equals discriminator plus space for largest case
- Inline storage, no heap allocation
- Optimized layout per union's non-boxing pattern

### Allocation Patterns

```csharp
// Allocation per construction
enum class Result { Ok(int value), Error(string message) }
var r1 = Result.Ok(42);  // Heap allocation

// No allocation
enum struct Result { Ok(int value), Error(string message) }  
var r2 = Result.Ok(42);  // Stack only
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
