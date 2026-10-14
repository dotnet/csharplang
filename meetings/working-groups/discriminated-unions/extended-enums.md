# Discriminated Unions and Enhanced Enums for C#

This proposal introduces enhanced enums as a way to express discriminated unions in C#. Like [type unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md), they represent a value drawn from a set of possible case types. Enhanced enums define the enum and its named, data-carrying cases together, with a choice of class or struct representation.

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

The [unions proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) describes union types whose contents can be one of their case types. Those case types can be existing types, may overlap, and can carry data through their own members. A shape enum instead declares fresh case types together with the enum. For example, `Left(int value)` and `Right(int value)` remain different cases even though both carry an `int`.

The related behaviors include case-to-enclosing-type conversion, matching against case types, and exhaustiveness analysis. Shape enums specify those behaviors for their own representations:

- An `enum class` is an abstract base class whose sealed case types derive from it. Reference conversions and pattern matching operate on the case instance itself.
- An `enum struct` stores a discriminator and the active case's data. Generated members support construction, case tests, and extraction without boxing; the compiler recognizes its declared case types for pattern matching.

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

Cases generate nested record types. `enum class` generates `sealed record class` types that derive from the enum's base class. `enum struct` generates `record struct` types; when the enum has the `readonly` modifier, its generated cases are `readonly record struct` types. Cases without payloads also have record semantics. The struct implementation may optimize internal storage while preserving those semantics.

#### Case Construction Expressions

Case construction is part of the enum's source-level behavior. Ordinary enums use `Color.Red` to obtain a value; adding a payload to a case should add arguments to that form, giving `E.Case(arguments)`. It should not require adding `new`.

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

A case declared without a parameter list is a value expression when accessed in a value context. A case declared with a parameter list is constructed by invoking its case name with the corresponding arguments, including an empty argument list for a declaration such as `Empty()`. Argument binding follows the generated case constructor's rules. Construction creates an active case and preserves its payload, including null payloads where permitted. The enum's default-value policy remains a separate choice.

The type of either case construction expression is the enclosing enum type. This follows the behavior of ordinary enum member access and allows a variable initialized from one case to later hold another:

```csharp
var option = Option<int>.Some(42); // Option<int>
option = Option<int>.None;
```

The nested case type remains available in type contexts, including type patterns, declarations, casts, and `typeof`. Direct construction with `new E.Case(arguments)` retains ordinary object-creation behavior and produces the nested case type. Thus `Option<int>.Some caseValue = new(42)` and `Option<int>.Some(42)` serve different purposes: the first explicitly constructs a case value, while the second constructs an enum value.

This is a language rule for declared enum cases. The compiler binds the case name according to its type, value, invocation, or pattern context, and lowers enum construction to the representation's case construction and case-to-enum conversion. Data-free class cases can use their shared instances; data-free struct cases produce the corresponding active case. A case's metadata must allow a consuming compiler to recognize these operations across assemblies. The precise encoding remains open.

The lowering does not need a nested type and an ordinary same-name factory method or property in the same declaration, which would conflict in C#. The case declaration supplies the source-level operation; generated implementation members can have distinct metadata names. User-declared members that conflict with a case name require a diagnostic.

##### Target-Typed Case Construction

With [target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md), the expected enum type supplies the qualifier for these same operations:

```csharp
void UseInt(Option<int> value) { /* ... */ }

UseInt(.None);
UseInt(.Some(42));

Option<int> Empty() => .None;
Option<int> Present(int value) => .Some(value);
```

The enum proposal defines the case's value and invocation behavior; target-typed access finds that case on the expected enum type. Its invocation binding must compose with enum case construction as well as ordinary static method calls. The leading dot should therefore allow `.Case(arguments)` with the same construction behavior as `E.Case(arguments)`, without requiring `new`. This does not depend on making arbitrary type names callable or adopting a general type-to-value conversion feature.

These leading-dot examples require the proposed target-typed member access feature. As with its other uses, a target type must be available, and the case name does not resolve an otherwise ambiguous overload. Generic inference can help provide that type, as discussed in [Contextual Case Construction and Inference](#contextual-case-construction-and-inference).

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

#### Readonly, Copying, and `with`

An `enum struct` supports the `readonly` modifier with the same meaning and restrictions as an ordinary [readonly struct](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1622-struct-modifiers). An unmodified enum struct is mutable. A `readonly enum struct` has readonly instance storage and cannot mutate `this` in its instance members, subject to the usual constructor and initialization rules. Readonly values can still be assigned to mutable variables; the modifier constrains the value's instance state, rather than preventing reassignment of a variable.

The generated case types follow the corresponding [record-struct rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#properties): a mutable enum struct generates mutable record-struct cases with `get`/`set` payload properties; a readonly enum struct generates readonly record-struct cases with `get`/`init` payload properties. This choice applies to generated cases. User-declared nested types follow the ordinary rules for nested declarations. Generated case queries, extraction methods, and equality members are readonly instance members.

Copying follows the usual class and struct rules. Assigning an enum class copies a reference to its case instance. Assigning an enum struct copies the discriminator and stored payload. Extracting a struct case produces a separate case value, reconstructed from the stored payload as needed; changing that case value does not update the enum from which it was extracted. Reference payloads retain their original references in either representation, so these copies are shallow. `readonly` does not make referenced objects immutable.

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

An enum class is itself a record base, so `e with { }` clones the runtime case record and preserves its case identity. An enum struct supports the ordinary [struct `with` expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#allow-with-expression-on-structs), so `e with { }` copies the enum value, including its active case or uninitialized state. Member initializers are bound against the receiver's static type. Case-specific payload properties are members of the case type, so updating them requires a case-typed receiver as above; they are not projected onto the enclosing enum. These operations preserve shallow-copy semantics, and a new enum value is constructed to change cases.

#### Equality and Hashing

Equality and hashing follow the record nature of the cases. Class cases use their generated record equality, including case type identity. For a struct enum, the generated equality members compare case identity and then use the corresponding case record's equality. Thus two values of the same case compare their payloads using the ordinary record rules, including `EqualityComparer<T>.Default` for payload members; different cases remain unequal even when their payloads are equal.

Both representations expose the ordinary record equality surface, including strongly typed `Equals`, `IEquatable<E>`, `Equals(object)`, and `==`/`!=` for the enum type `E`. Struct hashing follows the active case's record hashing and may incorporate case identity. These operations describe the logical case value: unused storage slots and padding do not participate, and an implementation need not reconstruct a case solely to perform them. Ordinary record customization rules apply where matching members can be declared.

Two uninitialized values of the same enum struct are equal and have the same hash code; they are unequal to any active case. A designated default case instead participates as that active case. Class null comparisons follow ordinary record reference equality. A null payload remains part of its active case, so `Some(null)` is distinct from `None` and from a no-case value. Hash values and the precise hash-combining algorithm remain implementation details.

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

For an `enum class`, `default(E)` is `null`. It represents no case, even when the declaration includes a data-free case named `None`. Case type tests fail for this value. Ordinary reference conversions apply: a downcast of `null` to a case type produces `null`, whereas a downcast of a non-null instance of a different case throws `InvalidCastException`.

For an `enum struct`, the discriminator value zero is reserved for an *uninitialized value* unless a default case is designated. All ordinary cases have nonzero discriminators, including data-free cases. The zero-initialized representation matches no case; its generated `HasValue` property is false, every case predicate and `TryGetValue` returns false, and a cast to any case throws `InvalidCastException`. This allows declarations in which every case requires state without inventing a case containing default payload values.

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

The designation is allowed only on an `enum struct` case declared without a parameter list, and at most once per declaration. The designated case uses discriminator zero; all other cases use nonzero discriminators. Constructing or converting the designated case to the enum produces the same representation as `default(E)`. No payload needs initialization for that case. Declarations with only data-carrying cases therefore retain the uninitialized zero representation.

These rules also apply to zero-initialized array elements and fields. The designation defines the meaning of zero-initialized storage and does not depend on executing a constructor. Apart from the meaning of zero, numeric discriminator values are implementation details.

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

Shape enums generate nested case types. An `enum class` uses inheritance to represent its cases; an `enum struct` stores a discriminator and case data and generates members for case access. The following translations illustrate the proposed API surface, rather than prescribing exact storage layouts or metadata names.

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

Matching a data-free case tests its type, rather than its reference identity. Record operations such as `with` may produce another instance of the same case type; those instances still represent the same case.

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

The annotation makes the versioning trade-off explicit. A dependency update can cause validation of an annotated type or consuming enum to fail even if its ordinary typed API would still work. The conservative default avoids introducing that hidden layout dependency. Neither the annotation nor its byte-size assertion promises a particular layout for the enum itself. Whether to add this optimization contract remains an LDM decision supported by performance measurements.

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

The proposed API supports explicit case-type construction, such as `new Result.Success("ok")`, as well as the enum-level [case construction expressions](#case-construction-expressions). Ordinary C# cannot declare a nested case type alongside a method or property with the same name, so enum-level construction is a compiler-recognized case operation. Matching record members follow ordinary record customization rules; other user-declared members that conflict with generated members are errors. The final generated API names remain an open question.

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

Current JIT code generation does not yet produce the intended dispatch optimizations for the patterns below. In local .NET 11 RC1 checks on ARM64 with 16 sealed class cases and 16 struct cases, both exact-type comparison chains and discriminator-predicate chains remained linear in fully optimized code and Tier 1 code with dynamic PGO. Struct predicates inlined into discriminator comparisons, but those comparisons did not become a jump table or comparison tree. We believe these transformations should be possible with additional runtime support; performance validation is still needed to choose the default strategy.

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

Although the emitted tests form a linear chain, they compare one runtime type against distinct known types. For a large number of cases, we believe the runtime could recognize this structure and choose a balanced comparison tree or another efficient dispatch strategy. When that strategy beats a linear chain or profile-guided ordering needs validation.

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

These are comparisons of the same integral discriminator against distinct constants. We believe the runtime could select a jump table for dense case values, a comparison tree for sparse values, or a linear chain when that is more efficient. It should also be able to eliminate redundant discriminator checks in case extraction after a successful predicate. In the local checks, marking the struct conversions for inlining allowed their redundant checks to be removed, but the dispatch chain remained linear. Predicate inlining alone does not establish that the comparison chain will become a jump table or tree.

#### Runtime Recognition and Alternatives

We should consider teaching the runtime to recognize exact-type comparison chains and discriminator predicates with distinct case bodies. The runtime already has a [limited switch-recognition pass](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/switchrecognition.cpp#L28) for constant-comparison chains whose successful tests all reach the same destination. It runs after inlining, but stops recognizing a chain when successful tests reach different destinations, as they do in these DU switches. Extending this recognition is one possible starting point.

Another option is an attribute in `System.Runtime.CompilerServices` identifying a well-behaved discriminated union and describing its case mapping and representation. If optimizing these patterns in arbitrary user code raises correctness concerns, the attribute could provide an explicit opt-in with a well-defined contract. That contract would specify how case identity remains stable during dispatch, that case predicates are side-effect-free and agree with the case mapping, and how extraction behaves after a successful test.

The presence of the attribute would assert that the type follows this contract, giving the runtime a basis for DU-specific optimization. For a struct, the metadata could identify the discriminator field, allowing the runtime to read it directly when optimizing case dispatch. These optimizations must preserve the observable semantics of the switch, including its guards and case bodies. No attribute name or contract is selected here.

Class enums could also use the same predicate-and-extraction emit as struct enums if the runtime can optimize the class representation efficiently. The final choice should follow performance validation, including switches with many cases, different case distributions, guards, and both statement and expression forms.

If these approaches do not produce satisfactory results, a final fallback is to expose a discriminator and have the compiler emit an integral switch over it directly. This would use the runtime's existing integral-switch optimizations without requiring it to recover a switch from a comparison chain. Class enums would also need a discriminator mapping for their cases. Case extraction must still preserve the conversion semantics, including reconstructing data from shared storage when necessary.

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

The generated APIs preserve the declared case identity. An active case remains active when one of its payload members is null, and extracting it preserves the payload members' declared types and nullability annotations.

Classes follow ordinary reference nullability rules. For structs, analysis of whether a case is active is separate from the nullability of its payload. Construction from a case, successful case tests, and successful `TryGetValue` calls establish the active case; the default-value rules determine whether zero-initialized storage has an active case.

### Library Evolution

The [open/closed case-set policy](#exhaustiveness) describes whether consumers must account for cases added by later library versions. An open shape enum may acquire new declared cases; existing consumers use their fallback for those cases. Adding a case to a closed shape enum changes its closed-case contract and is a breaking change. Neither policy permits callers to invent case types or arbitrary discriminator values.

Case-set policy does not by itself guarantee source, binary, or layout compatibility. Renaming or removing a public case, changing its payload constructor or properties, or changing between class and struct representations affects existing APIs. Changing the designated default case changes the meaning of zero-initialized storage and is a behavioral breaking change. Adding a struct case can enlarge the unmanaged, reference, or typed storage pools and change alignment. Libraries that depend on fixed native layouts or ahead-of-time ABI contracts need to account for that separately from open-case matching.

The compiler's case mapping must also support the intended versioning policy. If a consuming assembly embeds numeric tags for case tests, existing case mappings must remain valid across supported updates or the tests must obtain the mapping from the defining type. Reordering declarations must not cause a previously compiled test for one case to recognize another. Exposing a discriminator for switch lowering would require an explicit compatibility contract for that API; storage tags are otherwise implementation details. Previously compiled switches retain their normal fallback or unmatched-switch behavior when encountering a case they did not know about.

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

[Case construction expressions](#case-construction-expressions) define `E.None` and `E.Some(arguments)` as enum construction expressions. When the expected type is `Option<int>`, a leading-dot case name can be looked up on that constructed type. There is no need to repeat either `Option` or `int`, or to introduce `new` when the case carries data:

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

`.Some(42)` uses the enum's case invocation behavior after target-typed lookup supplies `Option<int>`. It constructs an active case with the payload and produces an `Option<int>` value. The same syntax applies to class cases, whose enum value is a reference to the constructed case instance. In the `Map` example above, the return type supplies `Result<U, E>` to the switch arms, so `.Error(err)` selects the output enum's case even though `U` does not appear in that case's payload.

For `.None`, the enum's data-free case access produces an active `None` value, using a shared instance for a class case or the corresponding active case for a struct. This follows ordinary enum member access: callers write `Color.Red`, and adding a payload adds arguments to that form. It does not require a general type-to-value conversion rule.

Constructing `None` does not imply that `default(Option<int>)` represents `None`; default-value policy is a separate choice. The nested case types remain available in type contexts. The target-typed member access proposal's direct nested-type construction form, `new .Some(42)`, can also construct a case value and convert it to the enum.

Target-typed invocation binding must therefore support enum case construction as well as ordinary static method calls. The enum supplies the source-level case operation; its lowering does not need an ordinary same-name factory beside the nested case type, which would conflict in C#. The case construction rules above define that behavior; this section describes how target typing and inference make it convenient to use.

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

The last form additionally uses the target-typed member access proposal's [factory-container lookup](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#factory-containers) on the nongeneric sibling `Option`. These factories illustrate composition with the other proposals; shape enums do not need to generate them.

For `CreateNone<T>`, the result type `Option<T>` must fit the target `Option<int>`, supplying the missing bound for `T`. The generalized proposal also lets a method group use the destination delegate's result type, as in `noneFactory`, and allows arguments and targets to determine different type parameters in one inference operation. Constraints, applicability, result conversion, and overload selection still need to succeed; gathering target-derived bounds does not replace those checks. The precise contexts and binding stages that supply target information remain part of the inference design. These examples require that information to flow from assignments, returns, and resolved argument parameter types.

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

Here `.Some` resolves to `Option<int>.Some`. A data-free case pattern tests the case, rather than comparing against the value produced by type-to-value conversion. The type-pattern inference proposal serves the related scenario where a generic case type is named directly with its type arguments omitted; leading-dot lookup already supplies the containing type arguments for these nested cases.

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

## 8. Design Decisions and Trade-offs

### Why Extend `enum`

- **Familiarity**: Developers already understand enums conceptually
- **Progressive disclosure**: Simple cases remain simple
- **Cognitive load**: One concept instead of two
- **Migration path**: Existing enums can be enhanced incrementally

### Two Representations

Shape enums provide the same case declaration syntax with two representations:

- `enum class` uses a base class and sealed case types, with ordinary reference conversions and pattern matching.
- `enum struct` uses a discriminator, inline case storage, and generated members for non-boxing case access.
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
- Layout and size are compiler implementation details; shared pools, typed slots, and separate-case storage are candidate strategies
- Inline storage, no heap allocation
- The conservative strategy preserves type and memory safety, including during torn reads and writes
- Sharing mixed case storage can require packing and reconstruction through non-boxing accessors
- Complete unmanaged cases may overlap when their layout assumptions can be established safely; packing external value types into raw storage would require the proposed checked opt-in
- Generated predicates and extraction methods access cases without boxing

### Allocation Patterns

```csharp
// Allocation per construction
enum class Result { Ok(int value), Error(string message) }
var r1 = Result.Ok(42); // Heap allocation

// No allocation
enum struct Result { Ok(int value), Error(string message) }  
var r2 = Result.Ok(42); // Inline case storage; no allocation for construction
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
7. **Generic cases**: Should cases support independent generic parameters?
8. **Exact lowering**: What names and shapes should the generated APIs and hierarchy metadata have, and are additional convenience APIs needed?
9. **Uninitialized matching**: Should the `null` pattern match an uninitialized enum struct, or should this state be tested only through case-state APIs?
10. **Future-case fallbacks**: What syntax or mechanism should opt into known-case coverage diagnostics while retaining a fallback for future cases?
11. **Checked packing**: Should a checked `[Unmanaged(size: ...)]` contract permit raw payload storage, and where should the annotation be placed?

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

The `= default` form is permitted only in an `enum struct`, at most once per declaration. It designates a case without a parameter list. Other explicit case initializers are not permitted in shape enums. Type parameter lists and constraints are permitted on shape enums, while constant enums retain their existing non-generic declarations. The shape-enum modifiers additionally permit `readonly` for `enum struct`, with the restrictions described above; it is not permitted for `enum class` or constant enums.

## Appendix B: Possible Connections to Type Unions

Shape enums and [type unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) both describe a value drawn from a set of possible case types. Shape enums declare fresh, named case types together with the enum and preserve their case identity, even when different cases carry the same payload types. This makes connections between the features possible, but does not require the same implementation or all of the same language behaviors.

The design above does not require either representation to implement the union pattern, carry `[Union]`, or implement `IUnion`. Whether to generate union-pattern implementations for structs, classes, both, or neither is an LDM decision. The following discusses possible integration rather than specifying additional requirements.

### Union Types, Declarations, and Runtime Access

The unions proposal distinguishes a *union type* from a *union declaration*. A custom union type can be a class or struct. Compiler recognition uses `[Union]` and a pattern of creation and access members. The `union` declaration syntax chooses a particular struct implementation that stores its contents in an object reference. Implementing the custom-union pattern would therefore allow shape enums to retain their own representation and storage strategy.

The `IUnion` interface is a separate option for runtime access to a value's contents. Implementing it alone does not opt a type into compiler union behavior. This distinction is described in the [union patterns update](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/union-patterns-update.md#marking-union-types-with-an-attribute).

### Possible Integration for `enum class`

An enum class could implement the union pattern while remaining a base class with sealed case types. Using a [union member provider](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-member-providers), generated `Create` methods could return the supplied case reference unchanged, and the provider's `Value` property could return `this`. An optional `IUnion` implementation could expose that same reference to runtime consumers. None of these members would require a wrapper allocation.

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
- Union type patterns target the contained case value, while untyped property and positional patterns inspect the enum itself. These rules would need to be reconciled with patterns intended to test or examine the enum struct itself. The distinction is described in the [LDM decision on pattern targets](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-08-12.md#union-pattern-matching-targets).
- Union null patterns would make an uninitialized struct match `null`, through its null `Value`. This is a possible choice for enum structs, but does not follow just from reserving discriminator zero. A designated default case would instead have a non-null case value, and a case with a null payload would remain an active case.
- Union exhaustiveness treats the declared case types as exhaustive. The enum proposal's separate open/closed policy, including handling future cases, would need to be preserved explicitly.

Both struct default strategies can satisfy the current union contract. An uninitialized default would expose a nullable `Value`; a designated default case could expose a non-nullable `Value` and report `HasValue` as true. The generated annotations and access members would need to agree on those meanings.

### Scope of an LDM Decision

LDM could choose full union-pattern integration, reuse selected compiler machinery without generating the pattern, or keep the features independent. Such a decision should determine which representations participate, whether protocol members are exposed directly or through a provider, whether `IUnion` is useful for runtime interoperability, and how pattern targets, uninitialized matching, and open/closed exhaustiveness interact.

The enum-specific default designation, storage strategy, `IsCase` predicates, and checked struct extraction remain part of the enum design. Any JIT optimization contract would likewise need to describe the enum's stronger case and representation guarantees; `[Union]` alone supports arbitrary custom implementations and does not identify a discriminator field or its mapping. Using similar constructor and `TryGetValue` APIs does not by itself mandate union-pattern integration.
