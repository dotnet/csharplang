# Discriminated Unions and Enhanced Enums for C#

This proposal extends C#'s union capabilities by introducing enhanced enums as [algebraic sum types](https://en.wikipedia.org/wiki/Sum_type). While [type unions](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md) solve the problem of values that can be one of several existing types, enhanced enums provide rich, exhaustive case-based types with associated data, building on the familiar enum keyword.

It also aims to consolidate the majority of the design space and feedback over many years in this repository, especially from:

- [#113](https://github.com/dotnet/csharplang/issues/113) - Main champion issue for discriminated unions, 7+ years of discussion and multiple LDM meetings

<details>
  <summary>as well as...</summary>

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

### Two Complementary Features

C# will gain two separate features for different modeling needs: [type unions](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md) and enhanced enums (this proposal). These features work together but solve distinct problems in the type system.

### At a Glance

```csharp
// Type unions - one value, multiple possible types
union Result { string, ValidationError, NetworkException }

// Enhanced enums - one type, multiple possible shapes  
enum PaymentResult
{
    Success(string transactionId),
    Declined(string reason),
    PartialRefund(string originalId, decimal amount)
}
```

Type unions excel when you need to handle values that could be any of several existing types. Enhanced enums shine when modeling a single concept that can take different forms, each potentially carrying different data.

## 2. Motivation and Design Philosophy

### Distinct Problem Spaces

Type unions and enhanced enums address fundamentally different modeling needs:

**Type unions** bring together disparate existing types. You use them when the types already exist and you need to express "this or that" relationships. The focus is on the types themselves.

**Enhanced enums** define a single type with multiple shapes or cases. You use them for algebraic sum types where the focus is on the different forms a value can take, not on combining pre-existing types.

### Limitations of Current Enums

Today's C# enums have served us well but have significant limitations:

1. **No associated data**: Cases are merely integral values, unable to carry additional information
2. **Not truly exhaustive**: Any integer can be cast to an enum type, breaking exhaustiveness guarantees
2. **Limited to integers**: Cannot use other primitive types like strings or doubles

Enhanced enums address all these limitations while preserving the conceptual simplicity developers expect.

### Building on Familiar Concepts

By extending the existing `enum` keyword rather than introducing entirely new syntax, enhanced enums provide a grow-up story. Simple enums remain simple, while advanced scenarios become possible without abandoning familiar patterns.

## 3. Type Unions (Brief Overview)

### Core Concepts

Type unions are fully specified in the [unions proposal](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md#summary). They provide:

- Implicit conversions from case types to the union type
- Pattern matching that unwraps union contents
- Exhaustiveness checking in switch expressions
- Enhanced nullability tracking

### Relationship to This Proposal

This proposal builds on type unions for shape enums. Shape enums become a convenient syntax for declaring both the case types and their union in a single declaration, with all behaviors deriving from the underlying union machinery.

## 4. Enhanced Enums

### Design Principles

Enhanced enums follow these core principles:

- **Progressive enhancement**: Simple enums stay simple; complexity is opt-in
- **Data carrying**: Each case can carry along its own constituent data in a safe and strongly typed manner
- **Familiar syntax**: Builds on existing enum and record/primary-constructor concepts
- **Exhaustiveness**: The compiler tracks all declared cases. Both constant and shape enums can be open or closed (see [Closed Enums proposal](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md))

### Syntax Extensions

Enhanced enums extend the traditional enum syntax in three orthogonal ways:

#### Extended Base Types

Traditional enums only support integral types. Enhanced enums support any constant-bearing type:

```csharp
enum Traditional : int { A = 1, B = 2 }
enum Priority : string { Low = "low", Medium = "medium", High = "high" }
enum TranscendentalConstants : double { Pi = 3.14159, E = 2.71828 }
```

#### Shape Declarations

Any of the following creates a shape enum (C#'s implementation of algebraic sum types):
- Adding `class` or `struct` after `enum`
- Having a parameter list on any enum member

```csharp
enum class Result { Success, Failure }  // shape enum via 'class' keyword
enum struct Result { Success, Failure } // shape enum via 'struct' keyword
enum Result { Success(), Failure() }    // shape enum via parameter lists
```

When created via parameter lists alone, it defaults to `enum class` (reference type):

```csharp
enum Result { Ok(int value), Error }    // implicitly 'enum class'
// equivalent to:
enum class Result { Ok(int value), Error }
```

#### Data-Carrying Cases

Shape enum members can have parameter lists, similar to a record's primary constructor, to carry data:

```csharp
enum Result
{
    Success(string id),
    Failure(int code, string message)
}
```

#### Combination Rules

- **Constant enums**: Can use extended base types but NOT have parameter lists
- **Shape enums**: Can have parameter lists but NOT specify a base type
- **Mixing cases**: Cannot mix constant values and parameterized cases in the same enum

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

For the complete formal grammar specification, see [Appendix A: Grammar Changes](#appendix-a-grammar-changes).

### Constant Value Enums

Enhanced constant enums extend traditional enums to support any primitive type that can have compile-time constants:

```csharp
enum Priority : string 
{
    Low = "low",
    Medium = "medium", 
    High = "high"
}

enum IrrationalConstants : double
{
    Pi = 3.14159265359,
    E = 2.71828182846,
    GoldenRatio = 1.61803398875
}
```

These compile to subclasses of `System.Enum` with the appropriate backing field `value__` with the appropriate underlying type. Unlike integral enums, non-integral constant enums require explicit values for each member.

Enhanced constant enums are similar to classic enums in that they are open by default, but can be potentially 'closed' (see [Closed Enums](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md)).

### Shape Enums

Shape enums are C#'s implementation of algebraic sum types. They provide convenient syntax for declaring a set of case types and their union in a single declaration.

#### Basic Shape Declarations

Shape enums can mix cases with and without data:

```csharp
enum FileOperation
{
    Open(string path),
    Close,
    Read(byte[] buffer, int offset, int count),
    Write(byte[] buffer)
}
```

Each case with parameters generates a corresponding type (typically a record). Cases without parameters generate singleton types. The enum itself becomes a union of these generated types.

#### Reference Type and Value Type

**`enum class`** creates a union where the case types are reference types:

```csharp
enum class WebResponse
{
    Success(string content),
    Error(int statusCode, string message),
    Timeout
}
```

Benefits:
- Cheap to pass around (pointer-sized union)
- No risk of struct tearing
- Natural null representation

**`enum struct`** creates a union with optimized value-type storage:

```csharp
enum struct Option<T>
{
    None,
    Some(T value)
}
```

Benefits:
- No heap allocation
- Better cache locality
- Reduced GC pressure

Similar to evolution of `records`, these variations can ship at separate times.

#### Members and Methods

Enums can contain members just like unions. This applies to both constant and shape enums:

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
- Instance methods, properties and indexers (members that add no additional state)
- Static members
- Nested types

## 5. Translation Strategy

### Shape Enum Translation Overview

Shape enums are syntactic sugar that generates:
1. Individual case types (as records or similar types)
2. A union type that combines these cases
3. Convenience members for construction and pattern matching

### `enum class` Translation

An `enum class` generates:
1. A set of record class types for each case
2. A union declaration combining these types

```csharp
enum class Result
{
    Success(string value),
    Failure(int code)
}

// Conceptually translates to:


// Generated union
public union Result
{
    Success,
    Failure;
    
    // Generated case types
    public sealed record class Success(string value);
    public sealed record class Failure(int code);
}

Singleton cases (those without parameters) generate types with shared instances:

```csharp
enum class State { Ready, Processing, Complete }

public union State
{
    Ready, Processing, Complete;
    
    // Conceptually translates to:
    public sealed class Ready 
    {
        public static readonly Ready Instance = new();
        private Ready() { }
    }
    // Similar for Processing and Complete
}
```

### `enum struct` Translation

An `enum struct` also generates case types and a union, but the union uses an optimized storage layout as permitted by the [non-boxing union access pattern](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md#union-patterns):

```csharp
enum struct Option<T>
{
    None,
    Some(T value)
}

// Conceptually translates to:

// Generated union with optimized storage
public struct Option<T> : IUnion
{
    // Generated case types
    public readonly struct None { }
    public readonly record struct Some<T>(T value);

    // Optimized layout: discriminator + space for largest case
    private byte _discriminant;
    private T _value;  // Space for Some's data
    
    // Implements IUnion.Value
    object? IUnion.Value => _discriminant switch
    {
        1 => newNone(),
        2 => new Some<T>(_value),
        _ => null
    };
    
    // Non-boxing access pattern
    public bool HasValue => _discriminant != 0;
    
    public bool TryGetValue(out None value)
    {
        value = default;
        return _discriminant == 1;
    }
    
    public bool TryGetValue(out Some<T> value)
    {
        if (_discriminant == 2)
        {
            value = new Some<T>(_value);
            return true;
        }
        value = default!;
        return false;
    }
    
    // Constructors
    public Option(None _) => _discriminant = 1;
    public Option(Some<T> some) => (_discriminant, _value) = (2, some.value);
    
    // Convenience factories
    public static Option<T> None => new Option<T>(new None());
    public static Option<T> Some(T value) => new Option<T>(new Some<T>(value));
}
```

For more complex cases with multiple fields of different types, the compiler would allocate:
- A discriminator field
- Unmanaged memory sufficient for the largest unmanaged data
- Reference fields sufficient for the maximum number of references in any case

This optimized layout provides the benefits of struct storage while maintaining full union semantics.

## 6. Pattern Matching and Behaviors

### Pattern Matching

Shape enums inherit all pattern matching behavior from their underlying union implementation. The compiler provides convenient syntax that maps to the underlying union patterns:

```csharp
var message = operation switch
{
    Open(var path) => $"Opening {path}",
    Close => "Closing file",
    Read(_, var offset, var count) => $"Reading {count} bytes at {offset}",
    Write(var buffer) => $"Writing {buffer.Length} bytes"
};
```

This works because:
- Each case name corresponds to a generated type
- The union's pattern matching unwraps to check these types
- Deconstruction works via the generated records' deconstructors

### Exhaustiveness

Shape enums benefit from union exhaustiveness checking. When all case types are handled, the switch is exhaustive:

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

Open vs closed shape enums follow the same rules as type unions for exhaustiveness.

### Other Union Behaviors

Shape enums automatically inherit from unions:
- **Implicit conversions** from case values to the enum type
- **Nullability tracking** for the union's contents
- **Well-formedness** guarantees about values

See the [unions proposal](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md#union-behaviors) for complete details on these behaviors.

## 7. Examples and Use Cases

### Migrating Traditional Enums

Traditional enums can be progressively enhanced:

```csharp
// Step 1: Traditional enum
enum OrderStatus { Pending = 1, Processing = 2, Shipped = 3, Delivered = 4 }

// Step 2: Add data to specific states
enum OrderStatus
{
    Pending,
    Processing(DateTime startedAt),
    Shipped(string trackingNumber),
    Delivered(DateTime deliveredAt)
}

// Step 3: Add methods for common operations
enum OrderStatus
{
    Pending,
    Processing(DateTime startedAt),
    Shipped(string trackingNumber),
    Delivered(DateTime deliveredAt);
    
    public bool IsComplete => this switch
    {
        Delivered => true,
        _ => false
    };
}
```

### Result and Option Types

Enhanced enums make functional patterns natural:

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

Enhanced enums excel at modeling state machines with associated state data:

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

Extending the existing `enum` keyword rather than introducing new syntax provides several benefits:

- **Familiarity**: Developers already understand enums conceptually
- **Progressive disclosure**: Simple cases remain simple
- **Cognitive load**: One concept (enums) instead of two (enums + algebraic sum types)
- **Migration path**: Existing enums can be enhanced incrementally

### Building on Unions

By implementing shape enums as syntactic sugar over type unions, we ensure:
- Consistent semantics between the two features
- All union optimizations and improvements benefit shape enums
- Reduced implementation complexity
- No risk of behavioral divergence

### Storage Strategy Trade-offs

The distinction between `enum class` (reference types) and `enum struct` (optimized value types) allows developers to choose the right trade-off for their scenario, similar to the choice between `record class` and `record struct`.

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
var r1 = Result.Ok(42);  // Heap allocation for Ok instance

// No allocation
enum struct Result { Ok(int value), Error(string message) }  
var r2 = Result.Ok(42);  // Stack only, value stored inline
```

### Optimization Opportunities

The compiler can optimize:
- Singleton cases to shared instances
- Small enum structs to fit in registers
- Pattern matching via union's optimized paths
- Exhaustive switches to avoid default branches

## 10. Open Questions

Several design decisions remain open:

1. **Nested type accessibility**: Should users be able to reference the generated case types directly (e.g., `Result_Success`), or should they remain compiler-only?

2. **Partial support**: Should enhanced enums support `partial` for source generators?

3. **Default values**: What should `default(EnumType)` produce for shape enums? The union default (null `Value`)?

4. **Serialization**: How should enhanced enums interact with System.Text.Json and other serializers?

5. **Additional state**: Should shape enums allow instance fields outside of case data? The union structure could accommodate this.

6. **Custom constructors**: Should enums allow custom constructors that delegate to cases? Should cases support multiple constructors?

7. **Construction syntax**: Should we use `Result.Ok(42)` or `new Result.Ok(42)` or support both? This ties into [target-typed static member lookup](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-lookup.md).

8. **Generic cases**: Should cases support independent generic parameters? For example: `enum Result { Ok<T>(T value), Error(string message) }`. This would likely only work for `enum class`.

9. **Interface implementation**: Should enhanced enums automatically implement interfaces like `IEquatable<T>` when appropriate?

10. **Exact lowering**: Should the spec define the exact names and shapes of generated types, or leave these as implementation details?

## Appendix A: Grammar Changes

```antlr
enum_declaration
    : attributes? enum_modifier* 'enum' ('class' | 'struct')? identifier enum_base? enum_body ';'?
    ;

enum_base
    : ':' enum_underlying_type
    ;

enum_underlying_type
    : simple_type // equivalent to all integral types, fp-types, decimal, bool and char
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
