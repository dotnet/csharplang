# Discriminated Unions and Enhanced Enums for C#

This proposal introduces enhanced enums as an elegant way to build discriminated unions in C#. Building on the foundational [type unions](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md) feature, enhanced enums provide familiar, concise syntax for the common pattern of defining algebraic sum types where the cases are known at declaration time.

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

### Building on Type Unions

C# gains a layered approach to union types: [type unions](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md) provide the foundational building block for combining types, while enhanced enums (this proposal) offer elegant syntax for the discriminated union pattern where you define the cases and their union together.

### At a Glance

```csharp
// Type unions - the foundation for combining existing types
union Result { string, ValidationError, NetworkException }

// Shape enums - elegant discriminated unions with integrated case definitions
enum PaymentResult
{
    Success(string transactionId),
    Declined(string reason),
    PartialRefund(string originalId, decimal amount)
}
```

Type unions are the lower-level building block—you use them when the types already exist and you need to express "one of these" relationships. Shape enums build on this foundation to provide the natural way to express discriminated unions, where you want to define the cases and their union as a cohesive unit.

## 2. Motivation and Design Philosophy

### From Type Unions to Discriminated Unions

Type unions solve the fundamental problem of representing a value that can be one of several types. However, a particularly important use case for unions is the discriminated union pattern, where:

- The cases are defined together as a logical unit
- Each case may carry different data
- The set of cases is typically closed and known at design time

Shape enums provide elegant syntax for this discriminated union pattern. Rather than manually defining types and then combining them with a union declaration, shape enums let you express the entire discriminated union naturally in a single declaration.

### Limitations of Current Enums

Today's C# enums have served us well but have significant limitations:

1. **No associated data**: Cases are merely integral values, unable to carry additional information
2. **Not truly exhaustive**: Any integer can be cast to an enum type, breaking exhaustiveness guarantees
3. **Limited to integers**: Cannot use other primitive types like strings or doubles

Enhanced enums address all these limitations while preserving the conceptual simplicity developers expect.

### Building on Familiar Concepts

By extending the existing `enum` keyword rather than introducing entirely new syntax, enhanced enums provide a grow-up story. Simple enums remain simple, while advanced scenarios become possible without abandoning familiar patterns. Most importantly, shape enums are not a separate feature from unions—they are the idiomatic way to express discriminated unions in C#.

## 3. Type Unions (Foundation)

### Core Concepts

Type unions are fully specified in the [unions proposal](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md#summary). They provide the foundational machinery:

- Implicit conversions from case types to the union type
- Pattern matching that unwraps union contents
- Exhaustiveness checking in switch expressions
- Enhanced nullability tracking
- Flexible storage strategies (boxing or non-boxing)

### The Building Block

Type unions are the essential building block that makes discriminated unions possible. They handle all the complex mechanics of storing values of different types, pattern matching, and ensuring type safety. Shape enums leverage all this machinery while providing a more convenient and integrated syntax for the common discriminated union pattern.

## 4. Enhanced Enums

### Design Principles

Enhanced enums follow these core principles:

- **Progressive enhancement**: Simple enums stay simple; complexity is opt-in
- **Data carrying**: Each case can carry along its own constituent data in a safe and strongly typed manner
- **Familiar syntax**: Builds on existing enum and record/primary-constructor concepts
- **Union foundation**: Shape enums are discriminated unions built on the type union machinery

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

Any of the following creates a shape enum (a discriminated union with integrated case definitions):
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

This syntax isn't just similar to records, .each case with a parameter list is shorthand for defining a nested `record` type with that name and those parameters. For `enum class`, the compiler generates nested `sealed record class` types. For `enum struct`, it generates nested `readonly record struct` types. These are real record types with all the expected record behaviors (equality, deconstruction, etc.).

Note that while these record types are what the enum declaration synthesizes, the union implementation *itself* is free to optimize its internal storage. For example, an `enum struct` might store case data inline rather than storing references to record instances, as long as it can reconstruct the record values when needed (such as for pattern matching or the IUnion.Value property).
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

### Shape Enums: Discriminated Unions Made Elegant

Shape enums are discriminated unions expressed through familiar enum syntax. They combine the power of type unions with the convenience of defining cases and their union together. When you write a shape enum, you're creating a complete discriminated union—the compiler generates the case types, creates the union that combines them, and provides convenient access patterns.

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

This single declaration creates a complete discriminated union: the case types, the union that combines them, and all the machinery for pattern matching and exhaustiveness checking.

#### Reference Type and Value Type

**`enum class`** creates a discriminated union where the case types are reference types:

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

**`enum struct`** creates a discriminated union with optimized value-type storage:

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

### Shape Enums as Discriminated Unions

Shape enums translate directly to the union pattern—they generate the case types as nested types and create a union that combines them. This isn't just an implementation detail; it's the fundamental design: shape enums ARE discriminated unions with convenient integrated syntax.

### `enum class` Translation

An `enum class` generates a union with nested record classes:

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
    
    // Generated case types
    public sealed record class Success(string value);
    public sealed record class Failure(int code);
}
```

Singleton cases (those without parameters) generate types with shared instances:

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

An `enum struct` also generates a union with nested types, but uses the union's non-boxing access pattern for optimized storage:

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
    public readonly record struct Some(T value);

    // Optimized layout: discriminator + space for largest case
    private byte _discriminant;
    private T _value;  // Space for Some's data
    
    // Implements IUnion.Value
    object? IUnion.Value => _discriminant switch
    {
        1 => new None(),
        2 => new Some(_value),
        _ => null
    };
    
    // Non-boxing access pattern
    public bool HasValue => _discriminant != 0;
    
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
    
    // Constructors
    public Option(None _) => _discriminant = 1;
    public Option(Some some) => (_discriminant, _value) = (2, some.value);
    
    // Convenience factories
    public static Option<T> None => new Option<T>(new None());
    public static Option<T> Some(T value) => new Option<T>(new Some(value));
}
```

This optimized layout leverages the flexibility of the union pattern while providing the performance characteristics developers expect from value types.

## 6. Pattern Matching and Behaviors

### Unified Pattern Matching

Shape enums ARE unions, so they inherit all union pattern matching behavior directly. There's no separate implementation or semantics—the patterns you write against shape enums are handled by the exact same union machinery:

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

Because shape enums are unions, they get union exhaustiveness checking for free:

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

Shape enums automatically get all union behaviors:
- **Implicit conversions** from case values to the enum type
- **Nullability tracking** for the union's contents
- **Well-formedness** guarantees about values

There's no duplication or risk of divergence—shape enums are unions with convenient syntax.

## 7. Examples and Use Cases

### Migrating Traditional Enums

Traditional enums can be progressively enhanced to become discriminated unions:

```csharp
// Step 1: Traditional enum
enum OrderStatus { Pending = 1, Processing = 2, Shipped = 3, Delivered = 4 }

// Step 2: Transform into discriminated union with data
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

Shape enums provide the natural way to express these fundamental discriminated union patterns:

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

Enhanced enums excel at modeling state machines—a classic discriminated union use case:

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

### Shape Enums ARE Discriminated Unions

This is not just an implementation detail—it's the core design principle. Shape enums are the idiomatic way to express discriminated unions in C#. By building directly on the union machinery:

- All union optimizations automatically benefit shape enums
- There's no risk of semantic divergence between features
- The mental model is simple: shape enums generate types and combine them with a union
- Future union enhancements immediately apply to shape enums

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

Because shape enums are unions, they benefit from all union optimizations:
- Singleton cases to shared instances
- Small structs fitting in registers
- Pattern matching via union's optimized paths
- Exhaustive switches avoiding default branches

## 10. Open Questions

Several design decisions remain open:

1. **Nested type accessibility**: Should users be able to reference the generated case types directly (e.g., `Result.Success`), or should they remain compiler-only?

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
