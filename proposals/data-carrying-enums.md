# Data-Carrying Enums

Champion issue: <https://github.com/dotnet/csharplang/issues/10378>
Discussion: <https://github.com/dotnet/csharplang/discussions/10379>

This proposal brings discriminated unions to C# through the familiar enum model: a value is one of several named cases, and each case can carry data. The new `enum class` and `enum struct` forms provide reference-type and value-type representations with record-like behavior for their cases.

It is a follow-up to the [type unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) proposal. Type unions combine existing types, while data-carrying enums declare a type and its cases together.

It consolidates design feedback from many years of repository discussions, especially [#113](https://github.com/dotnet/csharplang/issues/113) and related issues.

<details>
  <summary>Key discussion threads...</summary>

  - [#75](https://github.com/dotnet/csharplang/issues/75) - Early comprehensive discussion exploring DU syntax options including `enum class`
  - [#2962](https://github.com/dotnet/csharplang/discussions/2962) - Major discussion on Andy Gocke's DU proposal, debating `enum class` vs `enum struct`
  - [#7016](https://github.com/dotnet/csharplang/issues/7016) - Fast, efficient unions proposal focusing on struct-based implementations
  - [#3760](https://github.com/dotnet/csharplang/discussions/3760) - Community "shopping list" of desired discriminated union features
  - [#7544](https://github.com/dotnet/csharplang/issues/7544) - Simple encoding of unions exploring type unions vs tagged unions
  - [#9010](https://github.com/dotnet/csharplang/discussions/9010) - "Closed" enum types that guarantee exhaustiveness
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

## Example Walkthrough

An `Option<T>` represents either a value (`Some`) or its absence (`None`). Declare the cases together, followed by any members that operate on the enum.

Case instances are created without `new`. `Some` takes its associated value as an argument, while the data-free `None` case is accessed like an ordinary enum value.

For example:

```csharp
using System;

public closed enum struct Option<T>
{
    None = default,
    Some(T Value);

    public T GetValueOrDefault(T fallback)
        => this switch
        {
            .Some(var value) => value,
            .None => fallback
        };
}

static Option<T> Parse<T>(string text) where T : IParsable<T>
    => T.TryParse(text, provider: null, out var value)
        ? .Some(value)
        : .None;

Option<int> result = Parse("42");

string message = result switch
{
    .Some(var value) => $"Parsed {value}",
    .None => "Not an integer"
};

Console.WriteLine(message); // Parsed 42
Console.WriteLine(result.GetValueOrDefault(0)); // 42
```

The [`closed` modifier](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#exhaustiveness-and-evolution) fixes the set of cases. Designating `None` as the [`default` case](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#default-values) also accounts for zero-initialized values, so these switches can cover just `Some` and `None`.

This example uses `enum struct` for value-type semantics. [`enum class`](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#enum-class-translation) provides reference-type semantics with the same case construction and pattern syntax.

`Parse("42")` relies on [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md) to infer `T` from `Option<int>`. The `.Some(...)` and `.None` forms depend on the [contextual syntax extensions](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#contextual-construction-and-inference), avoiding the need to repeat the enclosing enum type.

## Declarations

For user-declared functionality (like `GetValueOrDefault` in the walkthrough), this proposal builds on [Members in Enum Declarations](https://github.com/dotnet/csharplang/blob/main/proposals/enum-members.md). That proposal defines the syntax and rules for adding members. It is not required for the members that the compiler generates under this proposal.

Data-carrying enum cases use their enclosing enum's type parameters and constraints. Whether a case can declare [independent type parameters](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#independent-generic-case-parameters) is an open question.

### Declaration Rules

Data-carrying enum declarations may list interfaces, but cannot specify a base class. Whether to allow [record inheritance for `enum class`](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#deriving-enum-class-from-a-record) remains an open question. Interfaces are implemented by the enclosing type under ordinary interface-implementation rules.

Cases cannot be assigned constant values. An `enum struct` may designate a case with `= default`, as described under [Default Values](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#default-values).

User-declared members may customize generated members where ordinary record rules allow it. Other conflicts with generated members or case type names are errors. Whether to permit additional instance state or custom constructors is discussed under [Additional Members](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#additional-members).

For example, a data-carrying enum can implement [`ISpanFormattable`](https://learn.microsoft.com/en-us/dotnet/api/system.ispanformattable) to format either distance case in meters:

```csharp
public closed enum class Distance : ISpanFormattable
{
    Meters(double Value),
    Kilometers(double Value);

    private double InMeters
        => this switch
        {
            Meters(var value) => value,
            Kilometers(var value) => value * 1000
        };

    public string ToString(string? format, IFormatProvider? formatProvider)
        => InMeters.ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        => InMeters.TryFormat(destination, out charsWritten, format, provider);
}
```

### Partial Declarations

Data-carrying enums support `partial` under the ordinary partial-type rules. Cases from all parts form one case set, with unique case names and at most one designated `default` case across the entire declaration.

Authors can separate cases and members into different parts. A members-only part can be written as `partial enum struct Option<T> { /* members */ }`.

### Grammar

The `readonly` and `ref` modifiers apply only to `enum struct` types. The `closed` modifier controls [case-set evolution](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#exhaustiveness-and-evolution).

The body grammar below is subject to these rules: a cases-only body may have a trailing comma, and a members-only body needs no leading separator. A semicolon is required between cases and members and is permitted only when both are present. No comma is permitted immediately before it.

```antlr
data_enum_declaration
    : attributes? data_enum_modifier* 'enum' ('class' | 'struct') identifier type_parameter_list?
      (':' interface_type_list)? type_parameter_constraints_clause* data_enum_body ';'?
    ;

data_enum_modifier
    : enum_modifier
    | 'partial'
    | 'readonly'
    | 'ref'
    | 'closed'
    ;

data_enum_body
    : '{' (data_enum_case_declarations ','?)? ';'? class_member_declaration* '}'
    ;

data_enum_case_declarations
    : data_enum_case_declaration (',' data_enum_case_declaration)*
    ;

data_enum_case_declaration
    : attributes? identifier parameter_list? ('=' 'default' '!'?)?
    ;
```

The grammar uses the ordinary `parameter_list`. Whether to permit an [empty parameter list](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#empty-parameter-lists) remains open.

Whether the semicolon between cases and members is required remains an [open question in Members in Enum Declarations](https://github.com/dotnet/csharplang/blob/main/proposals/enum-members.md#separating-values-from-members).

## Construction and Case Access

An ordinary enum value can be obtained using the syntactic form `E.Case`, such as `Color.Red`. Data-carrying enums extend this syntax to `E.Case(arguments)`:

```csharp
var some = Option<int>.Some(42); // Option<int>.Some
Option<int> option = some;
option = Option<int>.None;

void UseSome(Option<int>.Some value) { /* ... */ }
UseSome(Option<int>.Some(42));
```

A case without a parameter list is a value expression in a value context. A case with a parameter list is invoked with arguments bound under the corresponding record constructor's rules. Both forms produce a value of the case type.

Object-creation expressions targeting a data-carrying enum or a generated case type are errors, including explicit and target-typed `new`, with or without arguments:

```csharp
var invalid = new Option<int>.Some(42); // error
Option<int>.Some alsoInvalid = new(42); // error
Option<int> stillInvalid = new();       // error
```

### Contextual Construction and Inference

[Target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md) lets the expected enum type supply the qualifier for case construction. The [case-to-enum conversion](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#generated-apis-and-conversions) then satisfies that context:

```csharp
void UseInt(Option<int> value) { /* ... */ }

UseInt(.None);
UseInt(.Some(42));
Option<int> Empty() => .None;
```

The same lookup applies in target-typed switch arms:

```csharp
closed enum class Result<T, E> { Ok(T Value), Error(E Value) }

Result<U, E> Map<T, U, E>(Result<T, E> result, Func<T, U> mapper)
    => result switch
    {
        .Ok(var value) => .Ok(mapper(value)),
        .Error(var error) => .Error(error)
    };
```

Contextual case construction should compose with [generalized type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-generalized-type-inference/proposals/generalized-type-inference.md), which allows information to flow between arguments and their context. For example, another argument can determine a generic method's type parameter and thereby the expected enum type:

```csharp
void Use<T>(Option<T> value, T fallback) { /* ... */ }

Use(.None, 42); // T = int from the second argument
```

Ordinary applicability, conversions, constraints, and overload selection still apply. `var value = .None` has no target type and is an error. A case name alone cannot determine an unknown type argument or disambiguate overloads taking different enums. Inference from case arguments, including `Option.Some(42)` and `Use(.Some(42))`, is a separate [open question](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#inference-from-case-arguments).

### Generated APIs and Conversions

Case-to-enum conversions use existing C# conversion rules. `enum class` cases have implicit reference conversions to their base enum and explicit reference conversions back, with ordinary type tests and `as`. For `enum struct`, the compiler generates operators governed by the existing user-defined conversion rules. The proposed minimum API adds:

- An `implicit operator` from each case type to the enum.
- A boolean `IsCase` property for each case, such as `IsSome`, testing case identity.
- `HasValue`, which is false when uninitialized and true for any active case, including a designated `default` case.
- A `TryGetValue(out Case value)` overload for each case. When the case is active, it returns true and assigns the case value to the out parameter. Otherwise it returns false and assigns `default(Case)`.
- An `explicit operator` from the enum to each case type, returning its value if active or throwing `InvalidCastException` otherwise.

<details>
<summary>Example: generated case-access API for Option&lt;T&gt;</summary>

Public signatures for the earlier `Option<T>` (bodies, storage, and ordinary record members omitted):

```csharp
public struct Option<T>
{
    public record struct None;
    public record struct Some(T Value);

    public readonly bool IsNone { get; }
    public readonly bool IsSome { get; }
    public readonly bool HasValue { get; }

    public readonly bool TryGetValue(out None value) { ... }
    public readonly bool TryGetValue(out Some value) { ... }

    public static implicit operator Option<T>(None value) { ... }
    public static implicit operator Option<T>(Some value) { ... }

    public static explicit operator None(Option<T> value) { ... }
    public static explicit operator Some(Option<T> value) { ... }
}
```

</details>

## Defaults and Matching

### Default Values

For an `enum class`, `default(E)` is `null` and represents no case.

For an `enum struct`, discriminator zero denotes an *uninitialized value* unless a `default` case is designated. Cases not designated as `default` have nonzero discriminators, including data-free cases. Without `= default` on `None`, the walkthrough's `Option<T>` would have these values:

```csharp
Option<int> uninitialized = default;   // no active case
Option<int> none = Option<int>.None;   // active None
Option<int> some = Option<int>.Some(0); // active Some with payload 0
```

An `enum struct` may designate at most one case with `= default`, with or without payload parameters. This maps discriminator zero to that case, with each payload field receiving its type's default value in a zero-initialized enum:

```csharp
Option<int> option = default;
bool isNone = option is Option<int>.None; // true
bool hasValue = option.HasValue;          // true

enum struct Measurement
{
    Missing,
    Reading(double Value, string? Unit) = default
}

Measurement measurement = default;
bool isReading = measurement is Measurement.Reading(0, null); // true
```

Constructing or converting the designated case preserves the supplied payload, including non-default values. Zero-initialization of fields and array elements follows the same rules and does not invoke case constructors or use optional parameter defaults. Apart from the meaning of zero, numeric discriminator values are implementation details.

Nullable analysis checks a designated `default` case as if each payload property were initialized with `default` of its declared type. For example, `Reading(double Value, string Unit) = default` produces a warning at the designation because zero-initialization leaves `Unit` null despite its non-nullable declaration. The `string? Unit` in the example above permits that value.

Writing `Reading(double Value, string Unit) = default!` suppresses these nullability warnings for the designated case. The suppression does not change the default payload values.

### Case Patterns

`enum class` cases use reference type patterns and record deconstruction. For an `enum struct`, a pattern naming a declared case tests case identity and extracts the case value. Property and positional patterns then inspect that value:

```csharp
if (option is Option<int>.Some(var value))
{
    Console.WriteLine(value);
}
```

Payloads keep their declared types and nullability. For `Option<string?>.Some(null)`, the pattern `Option<string?>.Some(var value)` succeeds and binds `value` to null.

Patterns that extract a case from an `enum struct` require an input statically known to be that enum type. They do not establish CLR inheritance. A boxed `enum struct` is still an enum, so testing an `object` input against a case type does not extract that case. Patterns on the enum's own members inspect the enum itself.

Target-typed static member access also permits `.Some(var value)` and `.None` using the pattern input type. [Type-pattern inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns.md) addresses naming generic case types with omitted type arguments.

### Exhaustiveness and Evolution

Data-carrying enums follow the case-set policy of [open/closed enums](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md). A `closed` enum promises a fixed case set, allowing exhaustive matching without a future-case fallback. An enum without `closed` may gain cases, so consumers need a fallback for them. This uses that proposal's evolution policy, rather than its rules for integral enum values and conversions.

Class null references follow the ordinary nullable-reference and switch rules. Whether exhaustiveness must account for an uninitialized struct, and whether `null` should match it, remain [open questions](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#uninitialized-values-in-matching).

Renaming or removing public cases, changing payload parameters or properties, or changing between class and struct representations affects existing APIs. Changing the designated `default` case changes the meaning of zero-initialized storage and is a behavioral breaking change. Adding a struct case can increase storage or alignment requirements. Previously compiled switches retain their normal fallback or unmatched-switch behavior for newly added cases.

## Type Model

Each data-carrying enum case declares a public nested type that can be used like any other type. Its effective accessibility is bounded by the enclosing enum. Case types are records except in the [ref-like form](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#ref-like-enums). Record behavior supplies payload properties, deconstruction, and case equality. See [Construction and Case Access](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#construction-and-case-access) for restrictions on `new`.

Data-carrying enum storage layout and switch emit are compiler implementation details. These strategies are expected to evolve with future compilers and runtime capabilities. Changes must preserve the specified API and observable semantics. No particular size, field layout, native ABI, or persistent byte representation is guaranteed. [Implementation Details](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#implementation-details) discusses candidate strategies.

### `enum class` Translation

An `enum class` is an abstract record base with a private parameterless constructor, no copy constructor, and sealed nested case records. A value holds the actual case reference. For example:

```csharp
public enum class Result
{
    Success(string Value),
    Failure(int Code)
}
```

Its conceptual type model is:

```csharp
public abstract record class Result
{
    private Result() { }

    public sealed record class Success(string Value) : Result;
    public sealed record class Failure(int Code) : Result;
}
```

The base has no instance state, so case copy constructors call `base()`. Only the declared cases may derive from the enum. The private constructor and sealed cases prevent external derivation, including across assembly boundaries.

### `enum struct` Translation

An `enum struct` stores its active case's data inline. Its case types are separate `record struct` types, with conversions between the cases and the enum:

```csharp
// Conceptual type model and simple storage for Option<T>:
public struct Option<T>
{
    public record struct None;
    public record struct Some(T Value);

    private byte _discriminator;
    private T _value; // Some's payload, when active

    // Generated APIs omitted. General storage strategies are discussed later.
}
```

These sketches describe the type relationships and use ordinary nested record constructors in emitted code. The struct case types' mutability is described under [Readonly, Copying, and `with`](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#readonly-copying-and-with).

### Ref-Like Enums

The `ref` modifier on `enum struct` makes both the enclosing enum and every generated case type a `ref struct`, including data-free cases. Payloads may therefore be ref-like types or type parameters declared with `allows ref struct`:

```csharp
public ref enum struct RefOption<T> where T : allows ref struct
{
    None,
    Some(T Value)
}

Span<int> buffer = stackalloc int[16];
RefOption<Span<int>> option = RefOption<Span<int>>.Some(buffer);
```

Its conceptual expansion uses ordinary `ref struct` types with generated payload properties and deconstruction:

```csharp
public ref struct RefOption<T> where T : allows ref struct
{
    public ref struct None { }

    public ref struct Some
    {
        public T Value { get; set; }

        public Some(T Value) => this.Value = Value;

        public readonly void Deconstruct(out T Value) => Value = this.Value;
    }

    private byte _discriminator;
    private T _value;

    // Generated case-access APIs and metadata omitted.
}
```

The existing [ref-safety and anti-constraint rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/ref-struct-interfaces.md#ref-struct-generic-parameters) apply to case construction, conversions, pattern extraction, and `TryGetValue`. Ref-like payloads remain in typed fields. Case types retain the applicable record-like payload properties, deconstruction, and copying behavior.

#### Equality, Hashing, and Printing

Ref-like enums and their cases do not receive synthesized record equality, hashing, or printing. The [equality and hashing rules](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#equality-and-hashing) apply only to non-ref-like enums.

By default, the compiler emits `Equals(object)` and `GetHashCode()` overrides that throw `NotSupportedException`, following [`Span<T>`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Span.cs) and [`ReadOnlySpan<T>`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/ReadOnlySpan.cs). Authors provide any custom equality, hashing, or formatting on the enclosing enum, rather than on individual case types. For example, they may declare `==`/`!=` or override `ToString()`.

### Metadata and Compatibility

The compiler emits a marker identifying the enum, its cases, and their construction operations. Supporting C# compilers use it to enforce the [construction rules](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#construction-and-case-access) when consuming another assembly. The [metadata encoding](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#metadata-encoding) remains to be designed.

The enum and its generated case types also carry a nonoptional [CompilerFeatureRequired](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.compilerfeaturerequiredattribute) attribute with a feature name such as `"DataCarryingEnums"` (TBD). Compilers that recognize the attribute but lack feature support reject use of these types, preventing acceptance of `new E.Case(...)` that would become invalid after a compiler upgrade. To cover compilers predating the attribute, the compiler also emits an error-level `ObsoleteAttribute` compatibility guard that supporting compilers ignore, following the [required-members pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/required-members.md#metadata-representation).

## Value Semantics

Data-carrying enum cases use the familiar record rules. The enclosing enum adds the behavior needed to represent any one of those case values, including copying and equality for the struct representation.

### Readonly, Copying, and `with`

`enum struct` types currently follow ordinary [`readonly struct` rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1622-struct-modifiers): an unmodified `enum struct` is mutable, and `readonly enum struct` is readonly. Generated cases have the enum's readonlyness and follow [`record struct` property rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#properties), using `get`/`set` properties for mutable cases and `get`/`init` for readonly cases. Whether to make `enum struct` types [always readonly](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#always-readonly-enum-struct-types) remains open. Generated queries, extraction methods, and equality members are `readonly` instance members.

Assignment follows ordinary class and struct rules and copies payloads shallowly. Extracting a struct case produces an independent value, so changing it does not update the enum:

```csharp
enum struct Progress { Waiting, Running(int Count) }

Progress progress = Progress.Running(1);
Progress.Running running = (Progress.Running)progress;
running.Count = 2; // progress still contains Count = 1
progress = running with { Count = 3 }; // update a case copy, then convert back
```

Case types support ordinary record `with` expressions. On an `enum class` receiver, `e with { }` clones the runtime case record, preserving case identity. On an `enum struct`, ordinary [struct `with` rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#allow-with-expression-on-structs) copy its active case or uninitialized state. Initializers bind against the receiver's static type, so case-specific payload updates require a case-typed receiver.

### Equality and Hashing

`enum class` cases use record equality, including case type identity. `enum struct` equality requires the same discriminator and, for an active case, equality of the corresponding case records. Payload comparisons follow ordinary record rules, including `EqualityComparer<T>.Default`. Unused storage and padding do not participate.

Both enum forms expose strongly typed `Equals`, `IEquatable<E>`, `Equals(object)`, and `==`/`!=` for the enum type `E`. Struct hashing follows the active case's record hashing and may incorporate case identity. Hash values and combining algorithms are implementation details.

Two uninitialized values of the same `enum struct` are equal, have equal hashes, and are unequal to any active case. A designated `default` case participates as that active case.

### Recursive Payloads

`enum struct` types obey the existing [cyclic instance-field dependency rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1642-value-semantics). A case payload cannot create a direct or indirect recursive value-type layout, regardless of the storage strategy:

```csharp
enum struct Expr
{
    Number(int Value),
    Add(Expr Left, Expr Right) // error
}
```

Using `enum class`, or introducing a class or array payload, breaks the layout dependency through reference indirection. Another value type does not itself provide indirection.

### Concurrent Access

`enum struct` types follow the existing [atomicity rules for struct reads and writes](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/variables.md#96-atomicity-of-variable-references). Unsynchronized accesses may tear, observing case identity and payload from different writes. Such observations are unspecified and accessors may throw, but must preserve memory and type safety under the usual safety assumptions for payload types and user code. Neither copying nor `readonly` makes a concurrent read atomic.

## Implementation Details

The storage and switch-translation strategies below aim to implement data-carrying enums efficiently. Storage must remain safe even during torn accesses, and dispatch choices must preserve switch semantics.

Data-free `enum class` cases use shared instances.

### Struct Storage

The simplest layout is a discriminator followed by a separate field for each case type. The enum's size is roughly the sum of all case sizes plus the discriminator and padding. Conversions can copy the selected case directly into or out of its field.

Our initial expectation is that this approach would often make `enum struct` types too large to be practical. Even a handful of cases could substantially increase storage and copying costs. This assessment needs performance validation and sign-off from LDM and the runtime team. The compact layout below aims to minimize storage safely with only a small runtime cost for rearranging data during case conversions.

We aim to provide a default that balances space and speed. We are also open to letting authors configure that balance, most likely through attributes, similar to existing .NET layout controls such as [StructLayout](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute). Whether to offer customization, its form and scope, and its availability in the initial feature are all undecided.

#### Safety Requirements

To uphold the [concurrent-access guarantees](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#concurrent-access), GC reference locations must be determined by the runtime layout independently of the discriminator, including during assignment. No location may alternate between managed references and unmanaged data, and arbitrary bits must never become references.

Shared reference slots require checked extraction: a reader testing a string case could observe an array from a concurrent write. Extraction must check the reference actually read or use an equivalent safe mechanism. The discriminator alone cannot justify an unchecked cast.

#### Conservative Storage

A candidate `enum struct` layout uses a discriminator and three shared pools:

1. **Unmanaged data**: A byte region for the [predefined types](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#primitive-types-and-layout-assumptions) below, sized for the greatest combined requirement of any case, including padding and alignment. Cases share the region, with distinct locations for components within each case.
2. **Managed references**: Enough slots for the case containing the most references. A slot is normally `object`, but can be more specific when that type accommodates every value assigned to it, such as a slot used only for strings.
3. **Typed values**: Slots for arbitrary value types and type parameters not known to be reference types. Each type needs enough slots for its maximum count in any case. Distinct types cannot generally share a slot. Reference-constrained type parameters can use the reference pool.

Compatibility of the enum's own unmanaged classification is an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#unmanaged-classification).

A `struct` constraint does not rule out references. Even `where T : unmanaged` need not provide a declaration-time byte size, so such parameters can remain in typed slots. The pool maxima can come from different cases, making their combined size greater than any individual case.

#### Primitive Types and Layout Assumptions

The compiler may rely on the predefined types' unmanagedness and fixed managed sizes from the [existing `sizeof` rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12818-the-sizeof-operator):

| Types | Size in bytes |
| --- | --- |
| `bool`, `byte`, `sbyte` | 1 |
| `char`, `short`, `ushort` | 2 |
| `int`, `uint`, `float` | 4 |
| `long`, `ulong`, `double` | 8 |
| `decimal` | 16 |

These require no annotation and describe managed storage, rather than interop marshaling. Native-sized integers (`nint`/`IntPtr`, `nuint`/`UIntPtr`) are unmanaged but platform-dependent in size. Portable output must accommodate that, for example through native-sized typed fields. No blanket layout assumption applies to other framework or user-defined structs.

#### External Types and Decomposition

In the conservative layout, structs from referenced assemblies remain opaque typed values. The runtime determines their size and GC layout, keeping storage safe if a dependency grows or acquires references. Compile-time `unmanaged` classification is insufficient: C# enforces that constraint but the [CLR does not](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.3/blittable.md#detailed-design). Recompilation against a reference-containing definition rejects an unmanaged-constrained use. Replacing a binary dependency need not reject the instantiation. The [checked packing opt-in](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#possible-checked-packing-opt-in) could permit raw storage for such types.

The compiler can arrange generated cases' declared payload components directly. Decomposing a nested user struct must preserve all value state without invoking user getters, setters, or constructors, and respect accessibility, safety, and supported versioning. Otherwise it remains typed. Being in the current assembly is insufficient when layout dependencies cross assembly boundaries.

Unmanagedness alone does not establish byte layout. Raw packing must account for architecture, alignment, and padding. Reference metadata does not guarantee execution-time layout.

#### Mixed-Data Example

```csharp
enum struct Message<T>
{
    Text(string Content, int Code),
    Buffer(byte[] Data, int Offset, int Count),
    Pair(string First, string Second),
    Timestamp(DateTime Value),
    Values(T First, T Second)
}

// Candidate storage; case types and APIs omitted:
public struct Message<T>
{
    private byte _discriminator;
    private UnmanagedData _unmanaged; // Buffer needs two ints
    private object? _reference0;      // Text, Buffer, or Pair
    private string? _reference1;      // Pair only
    private DateTime _timestamp;      // external struct kept opaque
    private T _value0, _value1;       // Values; T may contain references

    [InlineArray(2)]
    private struct UnmanagedData { private int _element0; }
}
```

#### Packing and Extraction

In the shared-pool layout, case-to-enum conversion packs components into their assigned slots and sets the discriminator. Extraction checks case identity and reconstructs the value. Mixed layouts require rearranging, or "swizzling," components rather than reinterpreting the shared storage as a complete case. For example, `Buffer` uses `_reference0` and both integers in `_unmanaged`, while `Pair` uses both reference slots. Unused reference and typed slots are defaulted so inactive cases retain no references.

When every case is unmanaged and the required layout facts are established, complete case values can safely overlap in one region sized and aligned for the largest case, with a separate discriminator, avoiding field swizzling.

#### Possible Checked Packing Opt-in

An optional packing contract could let a consumer store otherwise opaque structs in raw storage. A compiler-recognized attribute would assert the payload's unmanagedness and fixed byte size, at its declaration or at an enum case parameter use site:

```csharp
// In a referenced library:
[Unmanaged(size: 8)]
public readonly record struct Point(int X, int Y);

// Consumer assertions can also cover unannotated external types:
enum struct Position
{
    At(Point Point),
    Timestamp([Unmanaged(size: 8)] DateTime Value)
}
```

For either placement, the compiler must verify that the payload is a struct satisfying C#'s unmanaged rules. The annotation is not proof. It may diagnose size mismatches known statically. The name, syntax, and precise placement rules remain open.

For a declaration-site assertion, the compiler prepends runtime checks to the type's static constructor, preserving user initialization:

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

Both checks use actual managed representation, not marshaling size. The illustrative helpers can be shared across types. Failure must prevent raw access with a managed exception, such as a type-initialization failure, and raw copies must remain in bounds, as with [`MemoryMarshal.Read` and `Write`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/MemoryMarshal.cs). These guarantees would remove the need for `unsafe`.

A packing enum must validate its own recorded claims once per constructed enum type through a generated static constructor. Constructors and case-to-enum conversions must trigger this before packing, under the [struct static-constructor rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#16410-static-constructors). Extraction must likewise ensure validation has completed before reconstructing payloads from raw storage, including for a designated `default` case. Returning typed `default` payloads or a failed `TryGetValue` result requires no raw access.

The payload initializer alone is insufficient: defaults, arrays, layout queries, and whole-value copies can bypass it. A later library could also remove the annotation or validate a different size. A consumer reserving eight bytes must validate its own eight-byte expectation, rejecting updates that ordinary typed storage could accept.

The `DateTime` assertion uses its current eight-byte managed size. The language does not guarantee that size. These cases could share an eight-byte region, separately from the discriminator. The annotation permits packing without requiring it and establishes only reference containment and total size. Field offsets, alignment, and accessibility need separate justification, or values can be copied opaquely. Adoption remains a joint LDM and runtime-team decision supported by performance measurements.

### Switch Translation

Switches over data-carrying enums identify the active case before extracting its payload. The candidate translations below use a chain of exact-type tests for `enum class` or case predicates for `enum struct`, which the runtime could optimize.

The input is evaluated once. Reordering tests must preserve null handling, guards, deconstruction, source ordering where observable, and the original fallback behavior. The sketches below omit those details and use distinct unguarded cases.

#### `enum class` Case Tests

For an `enum class` with cases `E.C1`, `E.C2`, and `E.C3`, compare the exact runtime type against each sealed case type, then extract the payload:

```csharp
var __case = e;
var __type = __case?.GetType();
if (__type == typeof(E.C1)) { HandleC1(((E.C1)__case).Value); }
else if (__type == typeof(E.C2)) { HandleC2(((E.C2)__case).Value); }
else if (__type == typeof(E.C3)) { HandleC3(((E.C3)__case).Value); }
```

The runtime can compare method-table pointers for exact-type tests. Distinct case identities could support a balanced tree or profile-guided ordering, and a successful comparison establishes the type for extraction.

#### `enum struct` Case Tests

For an `enum struct`, test case predicates before extracting the payload:

```csharp
var __value = e;
if (__value.IsC1) { HandleC1(((E.C1)__value).Value); }
else if (__value.IsC2) { HandleC2(((E.C2)__value).Value); }
else if (__value.IsC3) { HandleC3(((E.C3)__value).Value); }
```

Inlining predicates such as `IsC1 => _discriminator == 1` exposes integral comparisons:

```csharp
if (__value._discriminator == 1) { /* C1 body */ }
else if (__value._discriminator == 2) { /* C2 body */ }
else if (__value._discriminator == 3) { /* C3 body */ }
```

With additional recognition, the runtime could choose a dense jump table, sparse tree, or chain and eliminate checks dominated by a successful predicate.

#### Performance and Current Runtime Behavior

Local .NET 11 RC1 ARM64 checks with 16 sealed class cases and 16 struct cases found that exact-type and discriminator-predicate chains remained linear in fully optimized code and Tier 1 with dynamic PGO, even after predicate inlining.

Preliminary M4 Mac benchmarks indicate that linear dispatch performs well and appears sufficient through 32 cases in the tested patterns. Larger case sets may warrant better emit or JIT optimization without blocking initial implementation. Validation on x64 and other architectures, varied usage patterns and case distributions, guards, and both switch statements and expressions remains warranted.

Marking struct conversions for inlining removed redundant extraction checks in the local tests.

#### Runtime Recognition and Alternatives

The [existing switch-recognition pass](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/switchrecognition.cpp#L28) runs after inlining, but handles comparison chains whose successful tests share a destination. Different case bodies stop recognition. Extending it is one possible approach.

If general recognition raises correctness concerns, an attribute in `System.Runtime.CompilerServices` could opt well-behaved data-carrying enums into optimization. Its contract would cover stable case identity during dispatch, side-effect-free predicates agreeing with the case mapping, and extraction after a successful test. It could identify a struct's discriminator field for direct runtime access. This optional optimization contract is distinct from the required source-semantics marker. Its name and final contract remain undecided. Class enums could also use predicates and extraction if optimized efficiently.

A final fallback is exposing a discriminator and emitting an integral switch, using existing runtime optimizations directly. Classes would need a case-to-discriminator mapping too. We would prefer to avoid this: the mapping would become a stable API and ABI contract. Reordering cases or inserting a case anywhere could no longer simply renumber them in declaration order. Case numbering and library evolution would need a durable design. Extraction must still follow the specified conversions, including reconstruction from shared storage.

## Possible Connections to Type Unions

<details>
<summary>Exploratory integration with the union pattern and IUnion</summary>

Integration between data-carrying enums and type unions remains exploratory, with no requirements for the feature. Neither representation is required to implement the union pattern, carry `[Union]`, or implement `IUnion`. LDM could choose integration, reuse selected machinery, or keep the features independent. Any integration must preserve the enum construction and hierarchy rules through its metadata.

### Union Pattern and Runtime Access

The [type-unions proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) permits custom union classes or structs recognized through `[Union]` and creation/access members. Its `union` declaration selects an object-backed struct implementation. The custom pattern would let data-carrying enums retain their own representation.

[`IUnion`](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/union-patterns-update.md#marking-union-types-with-an-attribute) separately exposes contents to runtime consumers. An `enum struct` implementing it could participate in libraries and frameworks that add runtime union support, without requiring them to know its storage layout. Implementing it alone does not supply compiler union behavior.

### Possible Integration for `enum class`

A [union member provider](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-member-providers) could supply `Create` methods returning the case reference unchanged and `Value` returning `this`, optionally exposed through `IUnion`. This needs no wrapper allocation, though recursive runtime inspection must account for the self-reference.

The benefit is interoperability with other unions. The base/case relationship already supplies most language behavior, so integration adds APIs or provider metadata.

### Possible Integration for `enum struct`

The generated `enum struct` construction operations, `HasValue`, and `TryGetValue` fit much of the custom-union pattern. Integration would additionally need `[Union]` and an object-valued `Value`. `IUnion` could expose the same view at runtime.

Benefits include reusing union conversion, case-pattern, deconstruction, and nullable-matching machinery while retaining inline storage and non-boxing `TryGetValue`.

The costs and choices are:

- `Value` boxes struct cases and requires object-convertible cases.
- Public protocol names can conflict with domain meanings: `Option<T>.Value` might mean its payload and `HasValue` might mean `Some`, whereas the protocol exposes the active case and recognizes `None` too. A member provider avoids name conflicts but adds interface metadata and would require deciding where the proposed public `HasValue` belongs.
- Union type patterns inspect the contained case, while untyped property/positional patterns inspect the enum. Integration must preserve that distinction, following the [LDM decision on pattern targets](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-08-12.md#union-pattern-matching-targets).
- Union null patterns would select the open [uninitialized-null matching option](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#uninitialized-values-in-matching), with nullable `Value`. A designated `default` case could use non-nullable `Value`. Annotations and access members must agree.
- Union case-type exhaustiveness must accommodate the enum's separate open/closed evolution policy.

`[Union]` permits arbitrary implementations and identifies no discriminator mapping. It would not by itself provide the stronger guarantees needed for the proposed JIT optimization contract.

</details>

## Open Questions

The remaining choices below affect source behavior or compatibility. The rules above describe the current design where an alternative has not yet been selected.

### Uninitialized Values in Matching

Should a switch covering every declared case of a `closed enum struct` count as exhaustive even when the type has an uninitialized `default` value?

One option is to use the declared case set for exhaustiveness, as other language features use static information even when runtime values can differ. A switch expression without a matching arm would then throw if an uninitialized value reaches it. Another option is to require explicit handling through `HasValue` or a fallback. This is separate from handling future cases of an open enum.

Should the `null` pattern match the uninitialized state, or should it remain specific to null references and nullable values? A designated `default` case already has an ordinary case pattern.

### Always-Readonly `enum struct` Types

Should `enum struct` types and their generated case records always be readonly, or should they follow the [modifier-based rules](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#readonly-copying-and-with) in this draft?

### Independent Generic Case Parameters

Should a case be able to introduce its own type parameters, for example `X<T>(T Value)` in a nongeneric enum?

An `enum class` could represent this with a nested generic record deriving from the enum base: the enclosing value still holds a reference. However, `X` would describe a family of runtime types. The design would need to explain how to match or deconstruct all `X<T>` values without knowing `T`, infer the arguments, enforce constraints, and determine exhaustiveness. The [closed-hierarchy proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/closed-hierarchies.md#type-parameter-restriction) currently restricts independent type parameters for this reason.

An `enum struct` cannot directly store an arbitrary independent `T` inline: its size and GC layout must be determined without that type argument. Boxing or another form of indirection would introduce a different storage tradeoff. We should establish the need for generic cases before adding that complexity.

### Empty Parameter Lists

We recommend disallowing empty parameter lists in case declarations and requiring cases declared without parameters to be used without parentheses. This keeps the syntax consistent with ordinary enums: data-free cases have no parameters or parentheses, while data-carrying cases declare parameters and use parentheses for construction. Parentheses remain required when optional or `params` parameters allow zero arguments, so such calls still use `()`.

### Additional Members

Should data-carrying enums permit additional instance state? We recommend keeping instance state in the cases, even when all cases carry some common data. The enclosing enum can expose a computed property that switches on the active case to return that data. Callers can then access it through the enum without introducing fields that need separate initialization, copying, equality, and `with` rules.

Should custom instance constructors be permitted? A custom constructor would need to select a valid case and preserve the case-construction model.

Parameterized cases use `N(...)` or `N(...) = default`. The former shares its prefix with an instance constructor. That overlap would need consideration if constructors were allowed and the [comma-only separator alternative](https://github.com/dotnet/csharplang/blob/main/proposals/enum-members.md#separating-values-from-members) were selected.

### Deriving `enum class` from a Record

An `enum class` could derive from another record with an accessible parameterless constructor, or provide arguments to its base constructor directly in the base list. A further option is an enum primary constructor whose parameters are all forwarded to the base record, with cases supplying arguments to that constructor. For example, possible future syntax could be:

```csharp
record BaseRecord(int X);

enum class Outer(int X) : BaseRecord(X)
{
    A(int X, int Y) : base(X)
}
```

These options add substantial design surface around constructor binding, inherited state, and record copying and equality. We recommend deferring record inheritance and enum primary constructors from the initial feature. The current design does not preclude adding them later. They are included here for completeness.

### Inference from Case Arguments

Should `Option.Some(42)` infer the containing type `Option<int>` without a target type? [Constructor-call inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md), tracked by [#9627](https://github.com/dotnet/csharplang/issues/9627), and the [updated proposal](https://github.com/dotnet/csharplang/pull/10312) with [type-group lookup](https://github.com/dotnet/csharplang/pull/10310) offer related machinery. Applying it to enum case syntax needs an explicit design. Leading-dot access already works when context supplies the constructed enum type.

We would also like `Use(.Some(42))`, for `Use<T>(Option<T> value)`, to infer `T` from the case argument. The [current nested-inference model](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md#nested-inference) requires outer inference to complete before supplying a target to an inner expression that cannot bind independently. Supporting this scenario would require case lookup on a partially inferred enum type and gathering bounds from its arguments.

### Unmanaged Classification

An `enum struct`'s unmanaged classification is observable through constraints, pointers, and `stackalloc`, even though its physical representation is an implementation detail. What compatibility guarantee should apply when a compiler changes that representation?

LDM could allow classification to change in either direction, accepting potential source breaks, or require an unmanaged enum to remain unmanaged when its cases, payload types, and constraints are unchanged. The latter still allows managed-to-unmanaged changes while restricting future storage choices. Changes to declared cases or payload dependencies are separate library-evolution concerns.

### Metadata Encoding

What marker should identify data-carrying enums and their cases, and how should it encode the construction operations and hierarchy restrictions for supporting compilers? The public source behavior is specified above. The attribute or other metadata form and final generated API names remain to be designed.
