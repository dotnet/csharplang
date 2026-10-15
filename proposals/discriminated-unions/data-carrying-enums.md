# Data-Carrying Enums

Champion issue: <https://github.com/dotnet/csharplang/issues/10378>
Discussion: <https://github.com/dotnet/csharplang/discussions/10379>

This proposal brings discriminated unions to C# through the familiar enum model: a value is one of several named cases, and each case can carry data. The new `enum class` and `enum struct` forms provide reference-type and value-type representations with record-like behavior for their cases.

It is a follow-up to the [type unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) proposal. Type unions combine existing types, while data-carrying enums declare a type and its cases together. An [alternative design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md) builds discriminated unions directly on the `union` feature.

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

An `Option<T>` represents either a value (`Some`) or its absence (`None`). The following example declares those cases, adds a helper method, and shows how to create and use values of the enum:

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

`Parse("42")` relies on [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#inference-from-an-assignment-target) to infer `T` from `Option<int>`. The `.Some(...)` and `.None` forms depend on the [contextual syntax extensions](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#contextual-construction-and-inference), avoiding the need to repeat the enclosing enum type.

## Declarations

### Grammar

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

The `readonly` and `ref` modifiers apply only to `enum struct` types.

The body grammar is subject to these rules: a cases-only body may have a trailing comma, and a members-only body needs no leading separator. A semicolon is required between cases and members and is permitted only when both are present. No comma is permitted immediately before it.

The grammar uses the ordinary `parameter_list`. Whether to permit an [empty parameter list](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#empty-parameter-lists) remains open.

Whether the semicolon between cases and members is required remains an [open question in Members in Enum Declarations](https://github.com/dotnet/csharplang/blob/main/proposals/enum-members.md#separating-values-from-members).

### Declaration Rules

For user-declared functionality (like `GetValueOrDefault` in the walkthrough), this proposal builds on [Members in Enum Declarations](https://github.com/dotnet/csharplang/blob/main/proposals/enum-members.md). That proposal defines the syntax and rules for adding members. It is not required for the members that the compiler generates under this proposal.

Data-carrying enum cases use their enclosing enum's type parameters and constraints. Whether a case can declare [independent type parameters](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#independent-generic-case-parameters) is an open question.

Data-carrying enum declarations may list interfaces, but cannot specify a base class. Whether to allow [record inheritance for `enum class`](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#deriving-enum-class-from-a-record) remains an open question. Interfaces are implemented by the enclosing type under ordinary interface-implementation rules.

Cases cannot be assigned constant values.

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

Data-carrying enums support `partial` under the ordinary partial-type rules. Cases from all parts form one case set, with unique case names.

Authors can separate cases and members into different parts. A members-only part can be written as `partial enum struct Option<T> { /* members */ }`.

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

For an `enum struct` without a designated `default` case, `default(E)` is an *uninitialized value* with no active case. For example, if the walkthrough's `Option<T>` omitted `= default` on `None`:

```csharp
Option<int> uninitialized = default;   // no active case
Option<int> none = Option<int>.None;   // active None
Option<int> some = Option<int>.Some(0); // active Some with payload 0
```

An `enum struct` may designate at most one case with `= default` across all partial declarations. The designated case may have payload parameters. A zero-initialized enum then represents that case, with each payload field holding its type's default value:

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

Constructing or converting the designated case preserves the supplied payload, including non-default values. Zero-initialization of fields and array elements follows the same rules and does not invoke case constructors or use optional parameter defaults.

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

### Exhaustiveness and Evolution

Data-carrying enums follow the case-set policy of [open/closed enums](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md). A `closed` enum promises a fixed case set, allowing exhaustive matching without a future-case fallback. An enum without `closed` may gain cases, so consumers need a fallback for them. This uses that proposal's evolution policy, rather than its rules for integral enum values and conversions.

Class null references follow the ordinary nullable-reference and switch rules. Whether exhaustiveness must account for an uninitialized struct, and whether `null` should match it, remain [open questions](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#uninitialized-values-in-matching).

Renaming or removing public cases, changing payload parameters or properties, or changing between class and struct representations affects existing APIs. Changing the designated `default` case changes the meaning of zero-initialized storage and is a behavioral breaking change. Adding a struct case can increase storage or alignment requirements. Previously compiled switches retain their normal fallback or unmatched-switch behavior for newly added cases.

## Type Model

Each data-carrying enum case declares a public nested type that can be used like any other type. Its effective accessibility is bounded by the enclosing enum. Case types are records except in the [ref-like form](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#ref-like-enums). Record behavior supplies payload properties, deconstruction, and case equality. See [Construction and Case Access](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#construction-and-case-access) for restrictions on `new`.

The type sketches below describe relationships between the enum and its cases. Storage and switch emit remain implementation details. Candidate strategies are discussed in the [shared layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md) and [Implementation Details](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#implementation-details).

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

The `struct` keyword selects an opinionated inline representation. Case types are separate `record struct` types, with conversions between the cases and the enum:

```csharp
// Conceptual type model and simple storage for Option<T>:
public struct Option<T>
{
    public record struct None;
    public record struct Some(T Value);

    private byte _discriminator;
    private T _value; // Some's payload, when active

    // Generated APIs omitted.
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

`enum class` cases use record equality, including case type identity. For active `enum struct` values, equality requires the same case and equality of the corresponding case records. Payload comparisons follow ordinary record rules, including `EqualityComparer<T>.Default`. Unused storage and padding do not participate.

Both enum forms expose strongly typed `Equals`, `IEquatable<E>`, `Equals(object)`, and `==`/`!=` for the enum type `E`. Struct hashing follows the active case's record hashing and may incorporate case identity. Hash values and combining algorithms are implementation details.

Two uninitialized values of the same `enum struct` are equal, have equal hashes, and are unequal to any active case. A designated `default` case participates as that active case.

### Recursive Payloads

An `enum struct` follows the normal [value-type layout rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1642-value-semantics), and a [`ref enum struct`](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#ref-like-enums) follows the corresponding rules for `ref struct` types. Recursive payloads are permitted wherever those rules allow them.

For example:

```csharp
enum struct Expr
{
    Number(int Value),
    Add(Expr Left, Expr Right) // error
}
```

### Concurrent Access

Inline enum values follow the shared layout proposal's [concurrency and safety requirements](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#concurrent-access-and-safety).

## Contextual Construction and Inference

The walkthrough combines data-carrying enums with target-typed access and inference improvements. The qualified forms in the core design work independently of those extensions. The traces below identify which rules each shortened form needs, so compatibility can be checked as the related proposals evolve.

[Target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md) defines leading-dot lookup and its conversions. [Generalized generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-generalized-type-inference/proposals/generalized-type-inference.md) describes target-derived bounds. The [revised target-typed generic inference draft](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md) specifies when binding retries with a target and how nested inference proceeds. These drafts remain in progress. The analysis below uses the revised draft's rule that successful ordinary binding takes precedence.

Inference starting from case arguments, including `Use(.Some(42))` without another source for the outer type argument, remains an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#inference-from-case-arguments).

### Case Access with a Known Target

We expect these forms to work with target-typed static member access and this proposal's case-construction rules:

```csharp
Option<int> none = .None;
Option<int> some = .Some(42);
```

1. Each initializer has the target type `Option<int>`.
2. The [target-typed member binding conversion](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#basic-expression) qualifies `.None` as `Option<int>.None`. The [invocation target-typing conversion](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#target-typing-with-invocations) similarly qualifies `.Some(42)` as `Option<int>.Some(42)`.
3. Lookup finds the nested case types on the already constructed `Option<int>`. Its type arguments are known, just as they would be for lookup of a static property on that type. No inference through the nested member is needed.
4. This proposal's [case-construction rules](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#construction-and-case-access) produce the corresponding case values without `new`. Each result then uses the [case-to-enum conversion](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#generated-apis-and-conversions) to satisfy the target.

Without a target, `var value = .None` is an error.

### Inference from Another Argument

We expect this call to work with target-typed static member access and ordinary argument inference. It needs no target-derived generic inference:

```csharp
void Use<T>(Option<T> value, T fallback) { /* ... */ }

Use(.None, 42);
```

1. Method lookup finds `Use<T>`. Type inference starts with `T` unfixed.
2. `.None` has no independent type, so [input type inference](https://github.com/dotnet/csharpstandard/blob/alpha-v12/standard/expressions.md#12637-input-type-inferences) gathers no bound from it.
3. `42` has type `int`. Input type inference against the second parameter, `T`, contributes a lower bound of `int`.
4. Fixing selects `T = int`. Substitution gives parameter types `Option<int>` and `int`.
5. The target-typed member binding conversion makes `.None` acceptable for the first parameter without using the member name to select an overload. The second argument has an identity conversion. With this sole overload, `Use<int>` is selected.
6. The first parameter supplies the target `Option<int>`. Binding `.None` then follows the known-target lookup above, producing the case value and converting it to the enum.

This ordering agrees with the revised draft's [nested-inference rules](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md#nested-inference).

### Inference from an Assignment Target

We expect this line from the [walkthrough](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#example-walkthrough) to work if [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md) is adopted:

```csharp
Option<int> result = Parse("42");
```

Inferring `T` from the assignment target is a general method-inference feature, independent of enums. Without that proposal, users must supply the type argument explicitly as `Parse<int>("42")`.

### Target-Typed Patterns and Switch Arms

We expect these patterns and case constructions to work with target-typed static member access. The type parameters are declared on `Map`, so its body needs no generic inference:

```csharp
closed enum class Result<T, E> { Ok(T Value), Error(E Value) }

Result<U, E> Map<T, U, E>(Result<T, E> result, Func<T, U> mapper)
    => result switch
    {
        .Ok(var value) => .Ok(mapper(value)),
        .Error(var error) => .Error(error)
    };
```

1. The [target-typed pattern rules](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#pattern-matching) use the input `Result<T, E>` to qualify the pattern case names.
2. The `Ok` pattern extracts a `T`, while the `Error` pattern extracts an `E`.
3. The method's return type `Result<U, E>` target-types the switch arms and qualifies their construction expressions.
4. `mapper(value)` supplies a `U` for `Result<U, E>.Ok`, while `error` supplies an `E` for `Result<U, E>.Error`. Each case value converts to the result enum type.

[Type-pattern inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns.md) separately addresses written type names with omitted generic arguments.

## Implementation Details

The following are candidate switch translations. They may change with compiler and runtime improvements while preserving the specified API and observable behavior.

Data-free `enum class` cases use shared instances.

### Switch Translation

Switches over data-carrying enums identify the active case before extracting its payload. The candidate translations below use a chain of `is` tests for `enum class` or case predicates for `enum struct`, which the runtime could optimize.

The input is evaluated once. Reordering tests must preserve null handling, guards, deconstruction, source ordering where observable, and the original fallback behavior. The sketches below omit those details and use distinct unguarded cases.

#### `enum class` Case Tests

For an `enum class` with cases `E.C1`, `E.C2`, and `E.C3`, use `is` to test each case type and bind the case value:

```csharp
var __case = e;
if (__case is E.C1 c1) { HandleC1(c1.Value); }
else if (__case is E.C2 c2) { HandleC2(c2.Value); }
else if (__case is E.C3 c3) { HandleC3(c3.Value); }
```

Because the case types are sealed, the runtime can reduce these `is` tests to method-table pointer comparisons. Distinct case identities could support a balanced tree or profile-guided ordering. Class enums could also use predicates and extraction if those optimize efficiently.

#### `enum struct` Case Tests

The shared layout proposal covers [inline dispatch](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#case-dispatch), [current measurements](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#performance-and-current-runtime-behavior), and [runtime-recognition alternatives](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#runtime-recognition-and-alternatives).

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

`[Union]` alone would not provide the stronger [JIT optimization contract](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#runtime-recognition-and-alternatives).

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

We do not expect the following forms to infer their omitted type arguments under the current drafts. Each needs additional rules:

- `Option.Some(42)` needs lookup and inference of the containing type's omitted arguments. [Constructor-call inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md), tracked by [#9627](https://github.com/dotnet/csharplang/issues/9627), and the [updated proposal](https://github.com/dotnet/csharplang/pull/10312) with [type-group lookup](https://github.com/dotnet/csharplang/pull/10310) offer related machinery. Applying it to enum case syntax needs an explicit design.
- `Use(.Some(42))`, for `Use<T>(Option<T> value)`, needs case lookup on a partially inferred enum type and gathering bounds from the case arguments. The [current nested-inference model](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md#nested-inference) instead requires outer inference to complete before supplying a target to an inner expression that cannot bind independently.
- For an `enum struct`, `Use(Option<int>.Some(42))` needs inference to recognize the generated case-to-enum relationship. Existing method inference does not follow user-defined conversions. `Use<int>(Option<int>.Some(42))` or passing a value already typed as `Option<int>` works without that addition.

### Metadata Encoding

What marker should identify data-carrying enums and their cases, and how should it encode the construction operations and hierarchy restrictions for supporting compilers? The public source behavior is specified above. The attribute or other metadata form and final generated API names remain to be designed.
