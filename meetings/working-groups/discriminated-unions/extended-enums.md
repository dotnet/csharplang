# Discriminated Unions and Enhanced Enums for C#

This proposal extends C#'s union capabilities by introducing enhanced enums as algebraic data types. While [type unions](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md) solve the problem of values that can be one of several existing types, enhanced enums provide rich, exhaustive case-based types with associated data, building on the familiar enum keyword.

## 1. Overview

### Two Complementary Features

C# will gain two separate features for different modeling needs: type unions (already specified) and enhanced enums (this proposal). These features work together but solve distinct problems in the type system.

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

**Enhanced enums** define a single type with multiple shapes or cases. You use them for algebraic data types where the focus is on the different forms a value can take, not on combining pre-existing types.

### Limitations of Current Enums

Today's C# enums have served us well but have significant limitations:

1. **No associated data**: Cases are merely integral values, unable to carry additional information
2. **Not truly exhaustive**: Any integer can be cast to an enum type, breaking exhaustiveness guarantees
2. **Limited to integers**: Cannot use other primitive types like strings or doubles

Enhanced enums address all these limitations while preserving the conceptual simplicity developers expect.

### Building on Familiar Concepts

By extending the existing `enum` keyword rather than introducing entirely new syntax, enhanced enums provide progressive disclosure. Simple enums remain simple, while advanced scenarios become possible without abandoning familiar patterns.

## 3. Type Unions (Brief Overview)

### Core Concepts

Type unions are fully specified in the [unions proposal](https://raw.githubusercontent.com/dotnet/csharplang/refs/heads/main/proposals/unions.md#summary). They provide:

- Implicit conversions from case types to the union type
- Pattern matching that unwraps union contents
- Exhaustiveness checking in switch expressions
- Enhanced nullability tracking

### Relationship to This Proposal

This proposal leaves type unions unchanged. Enhanced enums are built independently, though both features share conceptual ground in making C#'s type system more expressive. Where unions excel at "or" relationships between types, enhanced enums excel at modeling variants within a single type.

## 4. Enhanced Enums

### Design Principles

Enhanced enums follow these core principles:

- **Progressive enhancement**: Simple enums stay simple; complexity is opt-in
- **Exhaustiveness**: The compiler knows all possible cases.  See [Closed Enums](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md) for more details.
- **Type safety**: Each case's data is strongly typed
- **Familiar syntax**: Builds on existing enum concepts

### Syntax Extensions

Enhanced enums extend the traditional enum syntax in three orthogonal ways:

#### Extended Base Types

Traditional enums only support integral types. Enhanced enums support any constant-bearing type:

```csharp
enum Traditional : int { A = 1, B = 2 }
enum Extended : string { Active = "active", Inactive = "inactive" }
enum Extended : double { Pi = 3.14159, E = 2.71828 }
```

#### Shape Declarations

A shape enum (ADT) is created by EITHER:
- Adding `class` or `struct` after `enum`, OR (inclusive)  
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

Shape enum members can have parameter lists to carry data:

```csharp
enum Result
{
    Success(string id),
    Failure(int code, string message)
}
```

#### Combination Rules

- **Constant enums**: Can use extended base types but NOT have parameter lists.
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

enum MathConstants : double
{
    Pi = 3.14159265359,
    E = 2.71828182846,
    GoldenRatio = 1.61803398875
}
```

These compile to subclasses of `System.Enum` with the appropriate backing field `value__` with the appropriate underlying type. Unlike integral enums, non-integral constant enums require explicit values for each member.

Enhanced constant enums are similar to classical enums in that they are open by default, but can be potentially 'closed' (see [Closed Enums](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md)).  Open and closed enums with non-integral backing types behave similarly to their integral counterparts.  For example, allowing/disallowing conversions from their underlying type, and treating pattern matching as exhaustive or not depending on if all declared values were explicitly matched.

### Shape Enums

Shape enums are C#'s implementation of algebraic data types, allowing each case to carry different data.

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

Each case defines a constructor (and corresponding destructor) pattern. Cases without parameter lists are singletons, while cases with parameters create new instances.

#### Reference vs Value Semantics

**`enum class`** creates reference-type enums, stored on the heap:

```csharp
enum class WebResponse
{
    Success(string content),
    Error(int statusCode, string message),
    Timeout
}
```

Benefits:
- Cheap to pass around (pointer-sized)
- No risk of struct tearing
- Natural null representation

**`enum struct`** creates value-type enums, optimized for stack storage:

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

#### Members and Methods

Enhanced enums can contain members just like unions:

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
- Methods and properties (no additional state)
- Static members
- Nested types

## 5. Pattern Matching

### Enhanced Enum Patterns

Enhanced enums support natural pattern matching syntax:

```csharp
var message = operation switch
{
    Open(var path) => $"Opening {path}",
    Close => "Closing file",
    Read(_, var offset, var count) => $"Reading {count} bytes at {offset}",
    Write(var buffer) => $"Writing {buffer.Length} bytes"
};
```

The compiler understands the structure of each case and provides appropriate deconstruction.

### Exhaustiveness

Switch expressions over enhanced enums are exhaustive when all cases are handled:

```csharp
enum Status { Active, Pending(DateTime since), Inactive }

// Compiler knows this is exhaustive - no default needed
var description = status switch
{
    Active => "Currently active",
    Pending(var date) => $"Pending since {date}",
    Inactive => "Not active"
};
```

### Comparison with Union Patterns

Enhanced enums and type unions have different pattern matching behaviors:

```csharp
// Union - patterns apply to the contained type
union Animal { Dog, Cat }
var sound = animal switch
{
    Dog d => d.Bark(),    // Matches the Dog inside the union
    Cat c => c.Meow()     // Matches the Cat inside the union
};

// Enhanced enum - patterns match the enum's cases
enum Animal { Dog(string name), Cat(int lives) }
var description = animal switch  
{
    Dog(var name) => $"Dog named {name}",  // Matches the Dog case
    Cat(var lives) => $"Cat with {lives} lives"  // Matches the Cat case
};
```

## 6. Translation Strategies

### `enum class` Implementation

Shape enums declared with `enum class` translate to abstract base classes with nested record types:

```csharp
enum class Result
{
    Success(string value),
    Failure(int code)
}

// Translates to approximately:
abstract class Result : System.Enum
{
    private Result() { }
    
    public sealed record Success(string value) : Result;
    public sealed record Failure(int code) : Result;
}
```

Singleton cases (those without parameters) use a shared instance:

```csharp
enum class State { Ready, Processing, Complete }

// Translates to approximately:
abstract class State : System.Enum
{
    private State() { }
    
    public sealed class Ready : State 
    {
        public static readonly State Instance = new Ready();
        private Ready() { }
    }
    // Similar for Processing and Complete
}
```

### `enum struct` Implementation

Shape enums declared with `enum struct` use a layout-optimized struct approach:

```csharp
enum struct Option<T>
{
    None,
    Some(T value)
}

// Translates to approximately:
struct Option<T> : System.Enum
{
    private byte _discriminant;
    private T _value;
    
    public bool IsNone => _discriminant == 0;
    public bool IsSome => _discriminant == 1;
    
    public T GetSome() 
    {
        if (_discriminant != 1) throw new InvalidOperationException();
        return _value;
    }
}
```

For complex cases with multiple fields of different types, the compiler employs union-like storage optimization:

```csharp
enum struct Message
{
    Text(string content),
    Binary(byte[] data, int length),
    Error(int code, string message)
}

// Uses overlapping storage for fields, minimizing struct size
```

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
        Delivered(_) => true,
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
        Ok(var value) => new Ok(mapper(value)),
        Error(var err) => new Error(err)
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
            new Reconnecting(null, attempts + 1, DateTime.Now.AddSeconds(Math.Pow(2, attempts))),
        Connecting(_, _) => 
            new Failed("Connection timeout", new TimeoutException()),
        Connected(var endpoint, _) => 
            new Reconnecting(endpoint, 1, DateTime.Now.AddSeconds(1)),
        _ => this
    };
}
```

## 8. Design Decisions and Trade-offs

### Why Extend `enum`

Extending the existing `enum` keyword rather than introducing new syntax provides several benefits:

- **Familiarity**: Developers already understand enums conceptually
- **Progressive disclosure**: Simple cases remain simple
- **Cognitive load**: One concept (enums) instead of two (enums + ADTs)
- **Migration path**: Existing enums can be enhanced incrementally

## 9. Performance Characteristics

### Memory Layout

**`enum class`**:
- Single pointer per instance (8 bytes on 64-bit)
- Heap allocation for each unique case instance
- Singleton pattern for parameter-less cases

**`enum struct`**:
- Size equals discriminant (typically 1-4 bytes) plus largest case data
- Stack allocated or embedded in containing types
- Potential for struct tearing with concurrent access

### Allocation Patterns

```csharp
// Allocation per call
enum class Result { Ok(int value), Error(string message) }
var r1 = new Ok(42);  // Heap allocation

// No allocation
enum struct Result { Ok(int value), Error(string message) }  
var r2 = new Ok(42);  // Stack only
```

### Optimization Opportunities

The compiler can optimize:
- Singleton cases to shared instances
- Small enum structs to fit in registers
- Pattern matching to jump tables
- Exhaustive switches to avoid default branches

## 10. Runtime Representation

Enhanced enums map to CLR types as follows:

### Constant Enums
- Subclass `System.Enum` with appropriate backing field
- Metadata preserves enum semantics for reflection
- Compatible with existing enum APIs

### Shape Enums
- **`enum class`**: Abstract class hierarchy with sealed nested classes
- **`enum struct`**: Struct with discriminant and union-style storage
- Custom attributes mark these as compiler-generated enhanced enums

### Interop Considerations

Enhanced enums maintain compatibility with:
- Existing `System.Enum` APIs where applicable
- Reflection-based frameworks
- Debugger visualization
- Binary serialization (with caveats for shape enums)

### 10. Open Questions

Several design decisions remain open:

1. Can users reference the generated nested types directly, or should they remain compiler-only?
2. Should enhanced enums support `partial` for source generators?
3. What should `default(EnumType)` produce for shape enums?
4. How should enhanced enums interact with System.Text.Json and other serializers?
5. Enums *could* allow for state, outside of the individual shape cases.  There is a clear place to store these in both the `enum class` and `enum struct` layouts.  Should we allow this? Or could it be too confusing?
6. Enums *could* allow for constructors, though they would likely need to defer to an existing case.  Should we allow this?  Similarly, should individual cases allow for multiple constructors?  Perhaps that is better by allowing cases to have their own record-like bodies.

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
