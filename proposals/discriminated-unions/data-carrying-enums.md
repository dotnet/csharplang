# Data-Carrying Enums

Champion issue: <https://github.com/dotnet/csharplang/issues/10378>
Discussion: <https://github.com/dotnet/csharplang/discussions/10379>

This proposal brings discriminated unions to C# through the familiar enum model: a value is one of several named cases, and each case can carry data. The new `enum class` and `enum struct` forms declare nested [`record class`](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md) and [`record struct`](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md) types, respectively, as their cases.

It is a follow-up to the [type unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) proposal. Type unions combine existing types, while data-carrying enums declare a type and its cases together. An [alternative design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md) builds discriminated unions directly on the `union` feature. The [design comparison](comparison.md) examines their tradeoffs.

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

The [`closed` modifier](#exhaustiveness-and-evolution) fixes the set of cases. Designating `None` as the [`default` case](#default-values) also accounts for zero-initialized values, so that switches can cover just `Some` and `None`.

This example uses `enum struct` for value-type semantics. [`enum class`](#enum-class-translation) provides reference-type semantics with the same case construction and pattern syntax.

`Parse("42")` relies on [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md), which is not specific to data-carrying enums, to infer `T` from `Option<int>`. The `.Some(...)` and `.None` forms depend on the [contextual syntax extensions](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#contextual-construction-and-inference), avoiding the need to repeat the enclosing enum type.

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

The `readonly` modifier applies only to `enum struct` types. The `ref` modifier is excluded from this proposal. A [separate proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-ref-like-types.md) explores `ref enum struct` and its open design questions.

The body grammar is subject to these rules: a cases-only body may have a trailing comma, and a members-only body needs no leading separator. A semicolon is required between cases and members and is permitted only when both are present. No comma is permitted immediately before it.

The grammar uses the ordinary `parameter_list`. Whether to permit an [empty parameter list](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#empty-parameter-lists) remains open.

Whether the semicolon between cases and members is required remains an [open question in Members in Enum Declarations](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/enum-members.md#separating-values-from-members).

### Declaration Rules

A case declaration introduces a public nested record. Its parameter list, if present, declares that record's primary constructor. Cases are sealed `record class` types in an `enum class` and `record struct` types in an `enum struct`. Allowing authors to supply [record bodies and case-specific interfaces](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#customizing-case-records) is a possible future extension.

For user-declared functionality (like `GetValueOrDefault` in the walkthrough), this proposal builds on [Members in Enum Declarations](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/enum-members.md). That proposal defines the syntax and rules for adding members. It is not required for the members that the compiler generates under this proposal.

Cases in a data-carrying enum use their enclosing enum's type parameters and constraints, which limits [case reuse between different instantiations](union-enum-common.md#generic-case-identity). Whether a case can declare [independent type parameters](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#independent-generic-case-parameters) is an open question.

Data-carrying enum declarations may list interfaces, but cannot specify a base class.

Cases cannot be assigned constant values.

User-declared members may customize generated members where ordinary record rules allow it. Other conflicts with generated members or case type names are errors. Whether to permit additional instance state or custom constructors is discussed under [Additional Members](#additional-members).

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

Cases use `E.Case` for data-free cases and `E.Case(arguments)` for cases with parameters, where `E` is the enclosing type. Both forms produce a value of the case type:

```csharp
Option<int>.Some some = Option<int>.Some(42);
Option<int> option = some;
option = Option<int>.None;
```

Arguments bind under the corresponding record constructor's rules. [Detailed lookup rules](union-enum-common.md#binding-case-access), including chained member access, remain to be specified.

Object-creation expressions targeting case types are errors, including explicit and target-typed `new`:

```csharp
var invalid = new Option<int>.Some(42); // error
Option<int>.Some alsoInvalid = new(42); // error
```

These restrictions also apply to the enclosing enum:

```csharp
Option<int> stillInvalid = new(); // error
```

### Generated APIs and Conversions

Case-to-enum conversions use existing C# conversion rules. `enum class` cases have implicit reference conversions to their base enum and explicit reference conversions back. For `enum struct`, the compiler generates operators governed by the existing user-defined conversion rules. The proposed minimum API adds:

- An `implicit operator` from each case type to the enum.
- A boolean `IsCase` property for each case, such as `IsSome`, indicating whether the enum represents that case.
- `HasValue`, which returns `true` when the enum represents a case, including a designated `default` case, and `false` otherwise.
- A `TryGetValue(out Case value)` overload for each case. When the enum represents that case, it returns `true` and assigns the case value to the out parameter. Otherwise it returns `false` and assigns `default(Case)`.
- An `explicit operator` from the enum to each case type, returning the case value if the enum represents that case or throwing `InvalidCastException` otherwise.

<details>
<summary>Example: generated access API for Option&lt;T&gt;</summary>

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

For an `enum struct` without a designated `default` case, `default(E)` represents none of its cases and `HasValue` is `false`. For example, if the walkthrough's `Option<T>` omitted `= default` on `None`:

```csharp
Option<int> uninitialized = default;   // HasValue is false
Option<int> none = Option<int>.None;   // represents None
Option<int> some = Option<int>.Some(0); // represents Some with Value = 0
```

An `enum struct` may designate at most one case with `= default` across all partial declarations. The designated case may have payload parameters. A zero-initialized enum then represents that case, with `HasValue` equal to `true` and each payload property holding its type's default value:

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

Nullability warnings and `default!` suppression follow the [shared rules for default cases](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#nullability-of-default-cases).

### Case Patterns

`enum class` cases use reference type patterns and record deconstruction. For an `enum struct`, a pattern naming a declared case tests whether the enum represents that case and extracts the case value. Property and positional patterns then inspect that value:

```csharp
if (option is Option<int>.Some(var value))
{
    Console.WriteLine(value);
}
```

Payloads keep their declared types and nullability. For `Option<string?>.Some(null)`, the pattern `Option<string?>.Some(var value)` succeeds and binds `value` to null.

Patterns that extract a case from an `enum struct` require an input statically known to be that enum type. They do not establish CLR inheritance. A boxed `enum struct` is still an enum, so testing an `object` input against a case type does not extract that case. Patterns on the enum's own members inspect the enum itself.

### Exhaustiveness and Evolution

Data-carrying enums follow the rules for [open and closed enums](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md). A `closed` enum promises a fixed case set, allowing exhaustive matching without a future-case fallback. An enum without `closed` may gain cases, so consumers need a fallback for them. This uses that proposal's evolution policy, rather than its rules for integral enum values and conversions.

Whether exhaustiveness must account for a struct value that represents no case, and whether `null` should match it, remain [open questions](#uninitialized-values-in-matching).

## Type Model

### `enum class` Translation

An `enum class` is an abstract record base with a private parameterless constructor and no copy constructor. A non-null value is the case instance itself. Data-free cases use shared instances. For example:

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

The base has no instance state, so case copy constructors call `base()`. Only the declared cases may derive from the enum. Its private constructor prevents external derivation.

### `enum struct` Translation

The `struct` keyword selects an [opinionated inline representation](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md). For example:

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

The emitted code uses ordinary nested record constructors.

### Readonly

`enum struct` types follow ordinary [`readonly struct` rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1622-struct-modifiers). Their case types have the enum's readonlyness and follow [`record struct` property rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#properties). Whether to make `enum struct` types [always readonly](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#always-readonly-struct-types) remains open.

Generated queries, extraction methods, and instance equality members are `readonly`.

## Common Behavior

The [common design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md) covers equality and hashing, recursive payloads, concurrent access, metadata and compatibility, library evolution, and contextual case syntax and inference.

## Switch Dispatch

The [shared switch emission discussion](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-switch-emit.md) covers type tests for `enum class`, non-boxing case access for `enum struct`, and possible optimizations.

## Possible Connections to Type Unions

<details>
<summary>Exploratory integration with the union pattern and IUnion</summary>

Integration between data-carrying enums and type unions remains exploratory, with no requirements for the feature. Neither representation is required to implement the union pattern, carry `[Union]`, or implement `IUnion`. Any integration must preserve the enum construction and hierarchy rules through its metadata.

### Union Pattern and Runtime Access

The [type-unions proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) permits custom union classes or structs recognized through `[Union]` and creation/access members. Its `union` declaration selects an object-backed struct implementation. The custom pattern would let data-carrying enums retain their own representation.

[`IUnion`](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/union-patterns-update.md#marking-union-types-with-an-attribute) separately exposes contents to runtime consumers. An `enum struct` implementing it could participate in libraries and frameworks that add runtime union support, without requiring them to know its storage layout. Implementing it alone does not supply compiler union behavior.

### Possible Integration for `enum class`

A [union member provider](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-member-providers) could supply `Create` methods returning the case reference unchanged and `Value` returning `this`, optionally exposed through `IUnion`. This needs no wrapper allocation, though recursive runtime inspection must account for the self-reference.

### Possible Integration for `enum struct`

The generated `enum struct` construction operations, `HasValue`, and `TryGetValue` fit much of the custom-union pattern. Integration would additionally need `[Union]` and an object-valued `Value`. `IUnion` could expose the same view at runtime.

Benefits include reusing union conversions, case patterns, deconstruction, and rules for `null` patterns while retaining inline storage and non-boxing `TryGetValue`.

The costs and choices are:

- `Value` boxes struct cases and requires case types implicitly convertible to `object`.
- Public union member names can conflict with domain meanings: `Option<T>.Value` might mean its payload and `HasValue` might mean `Some`, whereas the union access members expose the case instance and recognize `None` too. A member provider avoids name conflicts but adds interface metadata and would require deciding where the proposed public `HasValue` belongs.
- Union type patterns inspect the contained case, while untyped property/positional patterns inspect the enum. Integration must preserve that distinction, following the [LDM decision on pattern targets](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-08-12.md#union-pattern-matching-targets).
- Union null patterns would select the open [uninitialized-null matching option](#uninitialized-values-in-matching), with nullable `Value`. A designated `default` case could use non-nullable `Value`. Annotations and access members must agree.
- Union exhaustiveness checking must accommodate the enum's separate open/closed evolution policy.

</details>

## Open Questions

Questions common to both proposals are listed in the [shared design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#open-questions).

### Uninitialized Values in Matching

Should a switch covering every declared case of a `closed enum struct` count as exhaustive even when the type has an uninitialized `default` value?

One option is to use the declared case set for exhaustiveness, as other language features use static information even when runtime values can differ. A switch expression without a matching arm would then throw if an uninitialized value reaches it. Another option is to require explicit handling through `HasValue` or a fallback. This is separate from handling future cases of an open enum.

Should the `null` pattern match the uninitialized state, or should it remain specific to null references and nullable values? A designated `default` case already has an ordinary case pattern.

### Additional Members

Should data-carrying enums permit additional instance state? We recommend keeping instance state in the cases, even when all cases carry some common data. The enclosing enum can expose a computed property that switches on the case it represents to return that data. Callers can then access it through the enum without introducing fields that need separate initialization, copying, equality, and `with` rules.

Should custom instance constructors be permitted? A custom constructor would need to select a valid case.

Parameterized cases use `N(...)` or `N(...) = default`. The former shares its prefix with an instance constructor. That overlap would need consideration if constructors were allowed and the [comma-only separator alternative](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/enum-members.md#separating-values-from-members) were selected.

### Inference from a Known Case Type

We would likely want to extend [union-case type inference](https://github.com/dotnet/csharplang/pull/10355) to cover `enum struct` cases. Existing method inference does not follow the compiler-generated user-defined conversions, so `Use(Option<int>.Some(42))` cannot infer `T` for a method `Use<T>(Option<T> value)`.

Until that extension is designed, `enum struct` callers can write `Use<int>(Option<int>.Some(42))` or pass a value already typed as `Option<int>`.
