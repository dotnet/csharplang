# Discriminated Unions

This proposal brings discriminated unions to C# by extending the [type-union feature](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) with nested case declarations. Each `case` introduces a fresh type and adds it to the union's case set. Existing [union conversions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-conversions), matching, members, and exhaustiveness then apply.

An [alternative approach](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md) builds on familiar C# enums.

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

The [`struct` keyword](#union-struct-translation) selects inline storage and value-type cases. Bare [`union`](#union-translation) keeps the existing object-backed struct representation and uses reference-type cases.

`Parse("42")` relies on [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md). The `.Some(...)` and `.None` forms depend on the [contextual syntax extensions](#contextual-construction-and-inference), avoiding the need to repeat the enclosing union type.

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

Given a union declaration such as:

```csharp
union Result
{
    case Success(string Value);
    case Failure(int Code);
}
```

The compiler declares nested record types and adds them to the union's set of types, as illustrated using existing union syntax:

```csharp
union Result(Result.Success, Result.Failure)
{
    public sealed record class Success(string Value);
    public sealed record class Failure(int Code);
}
```

Types introduced by `case` declarations are not instantiated with `new`. See [Construction and Case Access](#construction-and-case-access).

Each case declares a public nested type. Cases use the containing union's type parameters and constraints. Independent case type parameters are an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#independent-generic-case-parameters).

### User-Declared Constructors

User-declared constructors follow the [existing union constructor rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-declarations). For example, the earlier `Result` could declare:

```csharp
internal Result(string message) : this(Result.Success(message)) { }
```

For declarations with generated cases and for `union struct` declarations, user code cannot assign to the generated `Value` property, including inside constructors. This keeps initialization within the declared case set and independent of the storage representation.

## Construction and Case Access

Cases declared with `case` use `U.Case` for data-free cases and `U.Case(arguments)` for cases with parameters. Both forms produce a value of the case type:

```csharp
var success = Result.Success("Complete"); // Result.Success
Result result = success;

void UseSuccess(Result.Success value) { /* ... */ }
UseSuccess(Result.Success("Complete"));
```

Arguments bind under the corresponding record constructor's rules.

Explicit and target-typed object-creation expressions for generated case types are errors:

```csharp
var invalid = new Result.Success("Complete"); // error
Result.Success alsoInvalid = new("Complete"); // error
```

Whether unions with declared cases should also [restrict direct construction](#direct-construction-of-the-enclosing-union) remains open.

### Access APIs for `union struct`

A `union struct` generates the [non-boxing access pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#non-boxing-access-members): `HasValue` and a `TryGetValue(out Case value)` overload for each case. All of these are `readonly` instance members.

When a `union struct` declares cases with `case`, `Value` returns a boxed copy of the case instance. Repeated reads may return different boxes, and mutating a returned box does not update the union. Bare `union` instead retains its stored object. A `union struct` combining existing types may need reference-backed storage to preserve existing union semantics, as discussed in the [shared layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#scope).

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

## Defaults and Evolution

### Default Values

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

### Library Evolution

Whether unions should support an explicitly open case set is an [open question](#open-unions).

The [shared library-evolution rules](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#library-evolution) cover compatibility when declarations change.

## Type Model

### `union` Translation

For bare `union`, each `case` declares a sealed record class. The containing union uses the [existing object-backed representation](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#lowering).

### `union struct` Translation

The `struct` keyword selects an opinionated inline representation from the [shared layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md), which also discusses possible configuration. Each `case` declares a separate record struct:

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

### Metadata and Compatibility

The [shared design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#metadata-and-compatibility) defines the required metadata and compatibility guards.

## Value Semantics

### Readonly, Copying, and `with`

Cases declared in a `union struct` have the union's readonlyness and follow [record-struct property rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#properties). Whether `union struct` types should always be readonly remains an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#always-readonly-struct-types).

### Equality and Hashing

Every declared case is a record, so we lift record equality and hashing to the union itself. This gives callers familiar record semantics when comparing union instances directly or using them as collection keys.

The union implements `IEquatable<U>` and provides `Equals(U)`, `Equals(object)`, `GetHashCode()`, `==`, and `!=`, with synthesis and customization following the [record-struct equality rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#equality-members).

Equality requires matching case types and delegates to the case value's equality implementation. A union whose `Value` is `null` compares equal only to another instance of the same union type whose `Value` is also `null`. Hashing delegates to the case value's `GetHashCode()` when present.

Unions of existing types retain their equality and hashing semantics when declared with `union struct`.

### Recursive Payloads and Concurrent Access

The [shared rules for recursive payloads](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#recursive-payloads) apply.

Torn accesses follow the [shared concurrency and safety requirements](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#concurrent-access-and-safety).

## Contextual Construction and Inference

The [shared design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#contextual-construction-and-inference) describes how case construction and patterns compose with target-typed access and generic inference.

## Implementation Details

The [shared switch-emission discussion](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-switch-emit.md) covers local performance measurements, candidate lowerings, and possible compiler and JIT optimizations.

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

Conceptually, this would add the fresh case types to the explicit list, yielding `union Result(AuthIssue, Result.Success, Result.Failure)`.

Allowing `object` in such a list would overlap every generated case. A naked type parameter could similarly become a generated case type or one of its supertypes when instantiated. This would undermine the expectation that the declared cases are distinct alternatives. We would need either restrictions that ensure disjointness or a deliberate choice to retain ordinary overlapping type-union behavior.

### Open Unions

Should unions have a way to indicate that their case set is non-exhaustive, for example `open union`? This would let authors add cases in later versions while requiring consumers to account for future cases. The indication must be available to downstream compilers through metadata. The design should consider alignment with the [open/closed enum proposal](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md).

### Explicit `union class` Spelling

Should `union class` be an explicit synonym for bare `union`, as `record class` is for `record`? We would like this symmetry when adding `union struct`. For cases declared in the body, `class` would describe the record-class cases, while the containing union would remain an object-backed struct. That difference could make the spelling confusing.
