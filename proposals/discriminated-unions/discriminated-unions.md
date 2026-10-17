# Discriminated Unions

This proposal brings discriminated unions to C# by extending the [type-union feature](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) with nested case declarations. Each `case` declares a nested [record](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md) and adds it to the union's set of case types. Existing union conversions, matching, members, and exhaustiveness then apply.

An [alternative approach](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md) builds on familiar C# enums. The [design comparison](comparison.md) examines their tradeoffs.

## Example Walkthrough

An `Option<T>` represents either a value (`Some`) or its absence (`None`). The following example declares those cases, adds a helper method, and shows how to create and use values of the union:

```csharp
using System;

public union struct Option<T>
{
    case None = default;
    case Some(T Value);

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

Designating `None` as the [`default` case](#default-values) accounts for zero-initialized values.

The [`struct` keyword](#union-struct-translation) selects inline storage and declares each case as a nested [`record struct`](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md). Without `struct`, a [`union`](#union-translation) keeps the existing object-backed struct representation and declares each case as a nested `record class`.

`Parse("42")` relies on [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md), which is not specific to unions. The `.Some(...)` and `.None` forms depend on the [contextual syntax extensions](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#contextual-construction-and-inference), avoiding the need to repeat the enclosing union type.

## Declarations

### Grammar

The following extends the [union-declaration grammar](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#syntax):

```antlr
union_declaration
    : attributes? struct_modifier* 'partial'? 'union' 'struct'? identifier type_parameter_list?
      ('(' case_types ')')? struct_interfaces? type_parameter_constraints_clause*
      ('{' union_member_declaration* '}' | ';')
    ;

union_member_declaration
    : union_case_declaration
    | struct_member_declaration
    ;

union_case_declaration
    : attributes? 'case' identifier parameter_list? ('=' 'default' '!'?)? ';'
    ;
```

A union with `case` declarations must omit the parenthesized type list in every partial declaration. Allowing a type list to add existing types alongside declared cases is an [open question](#mixing-existing-types-and-declared-cases). When no type list is supplied, at least one `case` declaration is required across all partial declarations.

The `ref` modifier is excluded from this proposal. A [separate proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-ref-like-types.md) explores `ref union struct` and its open design questions. Whether to permit an explicit `union class` synonym is an [open question](#explicit-union-class-spelling).

### Case Declarations

A case declaration introduces a public nested record. Its parameter list, if present, declares that record's primary constructor. Without `struct`, cases are sealed `record class` types. With `struct`, they are `record struct` types. Allowing authors to supply [record bodies and case-specific interfaces](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#customizing-case-records) is a possible future extension.

Given a union declaration such as:

```csharp
union Result
{
    case Success(string Value);
    case Failure(int Code);
}
```

The compiler adds the declared types to the union's set of types. Using existing union syntax, this corresponds to:

```csharp
union Result(Result.Success, Result.Failure)
{
    public sealed record class Success(string Value);
    public sealed record class Failure(int Code);
}
```

Types introduced by `case` declarations are not instantiated with `new`. See [Construction and Case Access](#construction-and-case-access).

Cases use the containing union's type parameters and constraints, which limits [case reuse between different instantiations](union-enum-common.md#generic-case-identity). Independent case type parameters are an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#independent-generic-case-parameters).

### User-Declared Constructors

User-declared constructors follow the [existing union constructor rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-declarations). For example, the earlier `Result` could declare:

```csharp
internal Result(string message) : this(Result.Success(message)) { }
```

For unions with case declarations and for all `union struct` types, user code cannot assign to the compiler-generated `Value` property, including inside constructors. This keeps initialization independent of the storage representation.

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

Whether unions with declared cases should also [restrict direct construction](#direct-construction-of-the-enclosing-union) remains open.

### Access APIs for `union struct`

A `union struct` generates the [non-boxing access pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#non-boxing-access-members).

For a `union struct` with case declarations, `Value` returns a boxed copy of the selected case. Unions of existing types may need to preserve incoming boxes, as discussed in the [layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#scope).

<details>
<summary>Example: generated access API for Option&lt;T&gt;</summary>

Public signatures for the walkthrough's `Option<T>`, with storage and ordinary record members omitted:

```csharp
[Union]
public struct Option<T> : IUnion
{
    public record struct None;
    public record struct Some(T Value);

    public Option(None value) { ... }
    public Option(Some value) { ... }

    public readonly object Value { get; }
    public readonly bool HasValue { get; }
    public readonly bool TryGetValue(out None value) { ... }
    public readonly bool TryGetValue(out Some value) { ... }
}
```

For this declaration, `Value` has a non-nullable return type because `None` is the designated `default` case.

</details>

## Default Values

A `union struct` may designate at most one case with `= default` across all partial declarations. A zero-initialized union value, such as `default(U)`, behaves as follows:

- If a case is designated, `Value` returns a boxed instance of that case, with each payload property holding its type's default value. `HasValue` is `true`, and `TryGetValue` for that case returns `true`.
- Otherwise, `Value` returns `null`. `HasValue` is `false`, and `TryGetValue` returns `false` for every declared case.

For example:

```csharp
union struct Measurement
{
    case Missing;
    case Reading(double Value, string? Unit) = default;
}

Measurement measurement = default;
bool hasValue = measurement.HasValue; // true
bool isReading = measurement is Measurement.Reading(0, null); // true
bool gotReading = measurement.TryGetValue(out Measurement.Reading reading); // true
```

Nullability warnings and `default!` suppression follow the [shared rules for default cases](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#nullability-of-default-cases). The designation is permitted only on `union struct`.

## Type Model

### `union` Translation

A `union` without the `struct` modifier uses the [existing object-backed representation](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#lowering). Data-free cases use shared instances.

### `union struct` Translation

The `struct` keyword selects an opinionated inline representation from the [shared layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md), which also discusses possible configuration. For example:

```csharp
// Conceptual type model and simple storage for Option<T>:
[Union]
public struct Option<T> : IUnion
{
    public record struct None;
    public record struct Some(T Value);

    private byte _discriminator;
    private T _value;

    // Generated construction, access, and equality members omitted.
}
```

Unions of existing types can also select this storage strategy by adding `struct`, as in `union struct X(T1, T2, T3)`.

An [alternative design](union-case-types-and-layout.md) makes layout an extension point, with implementations supplied by authors or generators. Attributes select a strategy without making its storage policy part of the language.

### Readonly

Cases declared in a `union struct` have the union's readonlyness and follow [record-struct property rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#properties). Whether `union struct` types should always be readonly remains an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#always-readonly-struct-types).

## Common Behavior

The [common design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md) covers equality and hashing, recursive payloads, concurrent access, metadata and compatibility, library evolution, and contextual case syntax and inference.

Unions of existing types retain their equality and hashing semantics when declared with `union struct`.

## Switch Dispatch

A `union` without the `struct` modifier can use existing union switch dispatch. `union struct` may warrant [specialized dispatch](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-switch-emit.md) for its non-boxed storage.

## Open Questions

Questions common to both proposals are listed in the [shared design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#open-questions).

### Direct Construction of the Enclosing Union

Should a union with declared cases prohibit explicit and target-typed object creation of the enclosing union, so users construct a case and use its union conversion? If so, the design would need to preserve constructors for union conversions and specify how user-declared constructors could still be invoked.

### Mixing Existing Types and Declared Cases

A future extension could allow a parenthesized list of additional types alongside declared cases:

```csharp
union Result(AuthIssue)
{
    case Success(string Value);
    case Failure(int Code);
}
```

Conceptually, this would add the declared case types to the explicit list, yielding `union Result(AuthIssue, Result.Success, Result.Failure)`.

Allowing `object` in such a list would overlap every declared case type. A type parameter listed directly, as in `union Result<T>(T)`, could similarly become a declared case type or one of its supertypes when instantiated. This would undermine the expectation that the declared cases are distinct alternatives. We would need either restrictions that ensure disjointness or a deliberate choice to retain ordinary overlapping type-union behavior.

### Open Unions

Should unions have a way to indicate that their case set is non-exhaustive, for example `open union`? This would let authors add cases in later versions while requiring consumers to account for future cases. The indication must be available to downstream compilers through metadata. The design should consider alignment with the [open/closed enum proposal](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md).

### Explicit `union class` Spelling

Should `union class` be an explicit synonym for `union` without the `struct` modifier, as `record class` is for `record`? We would like this symmetry when adding `union struct`. For cases declared in the body, `class` would describe the record-class cases, while the containing union would remain an object-backed struct. That difference could make the spelling confusing.
