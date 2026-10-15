# Discriminated Unions

This proposal brings discriminated unions to C# by extending the [type-union feature](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) with nested case declarations. Each `case` introduces a fresh type and adds it to the union's case set. Existing [union conversions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-conversions), matching, members, and exhaustiveness then apply.

An [alternative approach](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md) builds on familiar C# enums. Both proposals can use the [shared non-boxed layout](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md) when declared with `struct`.

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

The `struct` keyword selects inline storage and value-type cases. Bare [`union`](#union-translation) keeps the existing object-backed struct representation and uses reference-type cases.

`Parse("42")` relies on [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md). The `.Some(...)` and `.None` forms depend on the [contextual syntax extensions](#contextual-construction-and-inference), avoiding the need to repeat the enclosing union type.

## Declarations

### Grammar

The following extends the [union-declaration grammar](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#syntax):

```antlr
union_declaration
    : attributes? union_modifier* 'union' 'struct'? identifier type_parameter_list?
      ('(' case_types ')')? struct_interfaces? type_parameter_constraints_clause*
      ('{' union_member_declaration* '}' | ';')
    ;

union_modifier
    : struct_modifier
    | 'partial'
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

The compiler declares the nested case types and adds them to the union's case set. The equivalent type-list declaration is:

```csharp
union Result(Result.Success, Result.Failure)
{
    public sealed record class Success(string Value);
    public sealed record class Failure(int Code);
}
```

The generated types follow the [case-construction rules](#construction-and-case-access).

Each case declares a public nested type, with effective accessibility bounded by its containing union. Cases use the containing union's type parameters and constraints. Independent case type parameters are an [open question](#independent-generic-case-parameters).

Case names must be unique and must not conflict with other members.

### User-Declared Constructors

Under the [union constructor rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-declarations), user-declared instance constructors must use a `this(...)` initializer to delegate, directly or indirectly, to a generated case constructor. Public single-parameter constructors are reserved for those generated constructors, which identify the union's case types. For example, the earlier `Result` could declare a parameterless constructor that selects `Failure(-1)`:

```csharp
public Result() : this(Result.Failure(-1)) { }
```

For declarations with generated cases and for `union struct` declarations, user code cannot assign to the generated `Value` property, including inside constructors. This keeps initialization within the declared case set and independent of the storage representation.

## Construction and Case Access

Generated cases use the syntactic forms `U.Case` and `U.Case(arguments)` to produce values of the case type:

```csharp
var success = Result.Success("Complete"); // Result.Success
Result result = success;

void UseSuccess(Result.Success value) { /* ... */ }
UseSuccess(Result.Success("Complete"));
```

A case without a parameter list is a value expression in a value context. Arguments for a parameterized case bind under the corresponding record constructor's rules. A data-free reference-type case can use a shared instance, but matching and equality depend on its case type rather than reference identity.

Explicit and target-typed object-creation expressions for generated case types are errors:

```csharp
var invalid = new Result.Success("Complete"); // error
Result.Success alsoInvalid = new("Complete"); // error
```

Whether unions with declared cases should also [restrict direct construction](#direct-construction-of-the-enclosing-union) remains open.

### Access APIs for `union struct`

A `union struct` generates the [non-boxing access pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#non-boxing-access-members): `HasValue` and a `TryGetValue(out Case value)` overload for each case. These queries are `readonly` instance members.

For generated struct cases, object-valued `Value` returns a boxed copy. Repeated reads may return different boxes, and mutating a returned box does not update the union. Bare `union` instead retains its stored object. `union struct` declarations with only a type list may need reference-backed storage to preserve existing union semantics, as discussed in the [shared layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#scope).

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

`Value` exposes the case instance. For this declaration, `default(Option<int>).Value` returns a boxed `Option<int>.None`, so the property's return type is non-nullable. Without a designated `default` case, zero-initialization leaves no active case and `Value` returns `null`.

</details>

## Defaults and Evolution

### Default Values

A `union struct` may designate at most one case with `= default` across all partial declarations. A zero-initialized union value, such as `default(U)`, behaves as follows:

- If a case is designated, the value represents that case, with each payload property holding its type's default value. `HasValue` is true, and `TryGetValue` for that case returns true.
- Otherwise, the value represents no case. `HasValue` is false, and `TryGetValue` returns false for every declared case.

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

Nullable analysis checks the designation as if each payload property were initialized with `default` of its declared type. `Reading(double Value, string Unit) = default` therefore produces a warning. Writing `= default!` suppresses these warnings without changing the payload defaults. The designation is permitted only on `union struct`.

### Library Evolution

Whether unions should support an explicitly open case set is an [open question](#open-unions).

Renaming or removing public cases, changing their payloads, or changing between bare `union` and `union struct` affects public APIs. Changing the designated `default` case changes the meaning of zero-initialized storage. Adding a case to a `union struct` can increase its storage or alignment requirements. Previously compiled switches retain their normal fallback or unmatched-switch behavior if they encounter a new case.

## Type Model

Generated case types provide record-like payload properties, deconstruction, copying, and equality.

### `union` Translation

Bare `union` generates sealed record class cases and uses the [existing object-backed representation](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#lowering) for the containing union.

### `union struct` Translation

The `struct` keyword selects an opinionated inline representation from the [shared layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md), which also discusses possible configuration. Generated cases are separate record structs:

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

Type-list-only declarations can also use `union struct`. Their existing case types keep their declared class or struct kind.

### Metadata and Compatibility

The [shared design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#metadata-and-compatibility) defines the required metadata and compatibility guards.

## Value Semantics

### Readonly, Copying, and `with`

Generated reference-type cases follow record-class rules. Generated value-type cases follow record-struct rules and the containing union's readonlyness: mutable cases have `get`/`set` payload properties, while readonly cases have `get`/`init`. Whether `union struct` types should always be readonly remains an [open question](#always-readonly-union-struct-types).

Assignment copies the enclosing struct shallowly. Bare unions copy a case reference. `union struct` values copy their stored payload. Extracting a struct case gives an independent value, and changing it does not update the union. Case values support the corresponding record `with` expressions.

### Equality and Hashing

Current union declarations inherit `Equals(object)` and `GetHashCode()` from `System.ValueType`. Because they contain one object field, equality already compares that field using the contained value's equality. They do not generate a strongly typed `Equals` or implement `IEquatable<U>`.

We propose generating `IEquatable<U>`, a strongly typed `Equals(U)`, and overrides of `Equals(object)` and `GetHashCode()` for union declarations, including existing type-list declarations. The synthesized `Equals(object)` checks the union type and delegates to the typed implementation. This gives an efficient implementation without relying on runtime field inspection or boxing the union for typed equality. The [public shape of the typed member](#equality-api-compatibility) remains a compatibility question.

Bare unions with user-defined equality or hashing members, or a user-provided `IEquatable<U>` implementation, retain their existing behavior without default equality synthesis. A `union struct` that customizes equality must provide `Equals(U)`, `Equals(object)`, and `GetHashCode()` together. An explicit `IEquatable<U>.Equals` implementation can supply the typed member. Incomplete customization is an error, so equality never falls back to comparing implementation storage.

For declarations with generated cases, synthesized equality requires the same case and equal contents under that case's record rules. Two values with no active case are equal and are unequal to active cases. A designated `default` case participates as its ordinary case. Unused storage and padding never participate. Hashing follows the active case's equality, with equal values producing equal hashes. Hash values and combining algorithms remain implementation details.

For type-list-only unions, synthesis preserves the existing contained-value equality semantics, including user-defined equality between different runtime types. A storage tag must not introduce a new inequality. This proposal does not add equality operators to the enclosing union.

### Recursive Payloads and Concurrent Access

Bare unions can represent recursion through their reference-type cases. `union struct` declarations follow the ordinary value-type layout rules. A case cannot recursively contain its enclosing `union struct` by value when that would require infinite storage.

Torn accesses follow the [shared concurrency and safety requirements](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#concurrent-access-and-safety).

## Contextual Construction and Inference

The [shared design](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#contextual-construction-and-inference) describes how case construction and patterns compose with target-typed access and generic inference.

## Open Questions

### Direct Construction of the Enclosing Union

Should a union with declared cases prohibit explicit and target-typed object creation of the enclosing union, so users construct a case and use its union conversion? If so, the design would need to preserve constructors for union conversions and specify how user-declared constructors could still be invoked. Zero-initialization through `default` and arrays would remain possible.

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

Current unions treat their declared type set as exhaustive. Should `open union` indicate that cases may be added in later versions, requiring consumers to account for future cases? The intent would be library evolution, not permission to store arbitrary unlisted types. The contract must be available to downstream compilers through metadata. The design should consider alignment with the [open/closed enum proposal](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md).

### Explicit `union class` Spelling

Should `union class` be an explicit synonym for bare `union`, as `record class` is for `record`? We would like this symmetry when adding `union struct`. However, bare `union` is itself an object-backed struct, so the word `class` would describe neither the containing CLR type nor the kind of every explicitly listed case. That potential confusion remains a reason to omit the synonym.

### Always-Readonly `union struct` Types

Should `union struct` and its generated case records always be readonly, or follow the modifier-based rules in this draft?

### Equality API Compatibility

A public `Equals(U)` overload can change source binding through union conversions. For `union U(int)`, `value.Equals(42)` currently calls `Equals(object)` and compares the union with a boxed integer, yielding false. With the typed overload, it can convert `42` to `U` and compare two unions, yielding true.

Should the strongly typed method instead be an explicit `IEquatable<U>` implementation? That would retain efficient generic comparisons without adding a public overload. Synthesis is an API change whose source compatibility needs evaluation, beyond optimizing the comparison itself.

### Independent Generic Case Parameters

Should a case introduce independent type parameters, such as `case X<T>(T Value)` in a nongeneric union? Reference-type cases could represent this family of types, but matching, inference, constraints, and exhaustiveness would need further design. Inline storage cannot directly reserve space for an arbitrary independent `T` without boxing or other indirection. We should establish the need before adding that complexity.

### Empty Parameter Lists

We recommend disallowing empty parameter lists in case declarations and requiring data-free cases to be constructed without parentheses. A parameterized case still requires `()` when optional or `params` parameters allow zero supplied arguments.

### Inference from Case Arguments

We do not expect `Option.Some(42)` or `Use(.Some(42))`, for `Use<T>(Option<T> value)`, to work under the current drafts. The former needs lookup and inference of the containing type's omitted arguments. The latter needs case lookup on a partially inferred target and bounds from the case arguments. Related work includes [constructor-call inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md), [#9627](https://github.com/dotnet/csharplang/issues/9627), and the [revised nested-inference model](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md#nested-inference).

These forms differ from [inference from a known case type](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#inference-from-a-known-case-type): the case type is not yet available when inference needs to select it.

### Metadata Encoding

How should metadata identify generated cases and their construction operations? The design must preserve the case types as ordinary usable types while preventing older compilers from accepting construction syntax that a supporting compiler rejects.
