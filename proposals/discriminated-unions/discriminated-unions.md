# Discriminated Unions

This proposal brings discriminated unions to C# by extending the [type-union feature](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) with nested case declarations. Each `case` introduces a fresh type and adds it to the union's case set. Existing [union conversions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-conversions), matching, members, and exhaustiveness then apply.

An [alternative approach](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md) builds on familiar C# enums. Both proposals can use the [shared non-boxed layout](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md) when declared with `struct`.

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

`Parse("42")` relies on [target-typed generic type inference](#inference-from-an-assignment-target). The `.Some(...)` and `.None` forms depend on the [contextual syntax extensions](#contextual-construction-and-inference), avoiding the need to repeat the enclosing union type.

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
    | 'ref'
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

The `ref` modifier requires `union struct`. Its [protocol implications](#ref-like-unions) remain unresolved. Whether to permit an explicit `union class` synonym is an [open question](#explicit-union-class-spelling).

### Case Declarations

For example:

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

A non-ref-like `union struct` generates the [non-boxing access pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#non-boxing-access-members): `HasValue` and a `TryGetValue(out Case value)` overload for each case. These queries are `readonly` instance members.

For generated struct cases, object-valued `Value` returns a boxed copy. Repeated reads may return different boxes, and mutating a returned box does not update the union. Bare `union` instead retains its stored object. `union struct` declarations with only a type list may need reference-backed storage to preserve existing union semantics, as discussed in the [shared layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#scope).

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

Without a designated `default` case, `default(U)` for a `union struct` has no active case. Designating a case with `= default` makes `default(U)` represent that case, with each payload field holding its type's default value. For example:

```csharp
union struct Measurement
{
    case Missing;
    case Reading(double Value, string? Unit) = default;
}

Measurement measurement = default;
bool isReading = measurement is Measurement.Reading(0, null); // true
```

At most one case may be designated, across all partial declarations. Zero-initialized fields and array elements follow the same rules. Zero-initialization does not execute case constructors or apply optional parameter defaults. Explicit construction preserves the supplied payload, including non-default values.

The designated `default` case is active even when its payload contains nulls.

Nullable analysis checks the designation as if each payload property were initialized with `default` of its declared type. `Reading(double Value, string Unit) = default` therefore produces a warning. Writing `= default!` suppresses these warnings without changing the payload defaults. The designation is permitted only on `union struct`.

### Library Evolution

Whether unions should support an explicitly open case set is an [open question](#open-unions).

Renaming or removing public cases, changing their payloads, or changing between bare `union` and `union struct` affects public APIs. Changing the designated `default` case changes the meaning of zero-initialized storage. Adding a case to a `union struct` can increase its storage or alignment requirements. Previously compiled switches retain their normal fallback or unmatched-switch behavior if they encounter a new case.

## Type Model

Generated case types provide record-like payload properties, deconstruction, copying, and equality, except in the ref-like form.

### `union` Translation

Bare `union` generates sealed record class cases and uses the [existing object-backed representation](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#lowering) for the containing union.

### `union struct` Translation

The `struct` keyword selects an opinionated inline representation from the [shared layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md), which also discusses possible configuration. Generated cases are separate record structs:

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

### Ref-Like Unions

The proposed `ref union struct` form makes both the enclosing union and every generated case a `ref struct`. This permits ref-like payloads and type parameters with `allows ref struct`:

```csharp
public ref union struct RefOption<T> where T : allows ref struct
{
    case None = default;
    case Some(T Value);
}
```

Its conceptual types would be ordinary ref structs with generated payload properties and deconstruction:

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

    // Typed construction and access members omitted.
}
```

The object-valued `Value` requirement is a fundamental unresolved problem: these case values cannot be boxed. The current union protocol also requires case types to be object-convertible. Merely implementing `IUnion` on a ref struct does not solve either issue. This form needs a protocol extension, potentially allowing typed creation and `HasValue`/`TryGetValue` without an object-valued view. Its construction, conversions, and matching remain contingent on that design.

Ordinary [ref-safety and anti-constraint rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/ref-struct-interfaces.md#ref-struct-generic-parameters) must apply throughout. Ref-like payloads stay in typed fields. Ref-like unions and their cases receive no synthesized record equality, hashing, or printing. As with `Span<T>`, default `Equals(object)` and `GetHashCode()` overrides can throw `NotSupportedException`. Authors provide any custom equality operators or formatting on the enclosing union rather than individual cases.

### Metadata and Compatibility

The compiler emits metadata identifying generated case types and their construction operations. Supporting compilers use it to enforce the restriction on `new` across assembly boundaries. The encoding remains an [open question](#metadata-encoding).

Generated case types carry nonoptional [CompilerFeatureRequired](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.compilerfeaturerequiredattribute) metadata with a feature name such as `"DiscriminatedUnionCases"` (TBD). An error-level `ObsoleteAttribute` guard covers older compilers, following the [required-members compatibility pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/required-members.md#metadata-representation). Supporting compilers ignore that guard.

## Value Semantics

### Readonly, Copying, and `with`

Generated reference-type cases follow record-class rules. Generated value-type cases follow record-struct rules and the containing union's readonlyness: mutable cases have `get`/`set` payload properties, while readonly cases have `get`/`init`. Whether `union struct` types should always be readonly remains an [open question](#always-readonly-union-struct-types).

Assignment copies the enclosing struct shallowly. Bare unions copy a case reference. `union struct` values copy their stored payload. Extracting a struct case gives an independent value, and changing it does not update the union. Case values support the corresponding record `with` expressions.

### Equality and Hashing

Current union declarations inherit `Equals(object)` and `GetHashCode()` from `System.ValueType`. Because they contain one object field, equality already compares that field using the contained value's equality. They do not generate a strongly typed `Equals` or implement `IEquatable<U>`.

We propose generating `IEquatable<U>`, a strongly typed `Equals(U)`, and overrides of `Equals(object)` and `GetHashCode()` for non-ref-like union declarations, including existing type-list declarations. The synthesized `Equals(object)` checks the union type and delegates to the typed implementation. This gives an efficient implementation without relying on runtime field inspection or boxing the union for typed equality. The [public shape of the typed member](#equality-api-compatibility) remains a compatibility question.

Bare unions with user-defined equality or hashing members, or a user-provided `IEquatable<U>` implementation, retain their existing behavior without default equality synthesis. A non-ref-like `union struct` that customizes equality must provide `Equals(U)`, `Equals(object)`, and `GetHashCode()` together. An explicit `IEquatable<U>.Equals` implementation can supply the typed member. Incomplete customization is an error, so equality never falls back to comparing implementation storage.

For declarations with generated cases, synthesized equality requires the same case and equal contents under that case's record rules. Two values with no active case are equal and are unequal to active cases. A designated `default` case participates as its ordinary case. Unused storage and padding never participate. Hashing follows the active case's equality, with equal values producing equal hashes. Hash values and combining algorithms remain implementation details.

For type-list-only unions, synthesis preserves the existing contained-value equality semantics, including user-defined equality between different runtime types. A storage tag must not introduce a new inequality. This proposal does not add equality operators or formatting members to the enclosing union.

### Recursive Payloads and Concurrent Access

Bare unions can represent recursion through their reference-type cases. `union struct` declarations follow the ordinary value-type layout and ref-safety rules. A case cannot recursively contain its enclosing `union struct` by value when that would require infinite storage.

Torn accesses follow the [shared concurrency and safety requirements](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#concurrent-access-and-safety).

## Contextual Construction and Inference

Qualified construction and patterns work without target-typed access or improved generic inference. Shorter forms depend on the linked proposals below. The traces identify the required steps so the proposals can be checked together as they evolve.

### Case Access with a Known Target

We expect these forms to work with [target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md) and this proposal's case-construction rules:

```csharp
Option<int> none = .None;
Option<int> some = .Some(42);
```

1. Each initializer has the target type `Option<int>`.
2. Leading-dot lookup qualifies the expression with that constructed type. No inference through the nested case is needed.
3. Case construction produces an `Option<int>.None` or `Option<int>.Some` value.
4. The existing union conversion supplies the enclosing union value.

Without a target, `var value = .None` is an error. The leading-dot rules need to recognize generated cases as described here, rather than requiring a static factory with the same name as the nested type.

### Inference from Another Argument

We expect this call to work with target-typed static member access and ordinary argument inference:

```csharp
void Use<T>(Option<T> value, T fallback) { /* ... */ }

Use(.None, 42);
```

1. `.None` has no independent type and contributes no input bound.
2. The second argument contributes a lower bound of `int` for `T`.
3. Fixing `T` supplies the first parameter's target type, `Option<int>`.
4. Binding `.None` proceeds as in the known-target example, followed by the union conversion.

This order agrees with the revised generic-inference draft's [nested-inference rules](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md#nested-inference).

### Inference from a Known Case Type

We expect this call to infer `T = int` if [union-case type inference](https://github.com/dotnet/csharplang/pull/10355) is adopted. It does not require target-typed member access:

```csharp
void Use<T>(Option<T> value) { /* ... */ }

Use(Option<int>.Some(42));
```

1. Qualified construction produces an `Option<int>.Some`, independently of the parameter's target type.
2. Lower-bound inference against `Option<T>` finds the unique matching case, `Option<T>.Some`.
3. The proposed rule recurses from `Option<int>.Some` to `Option<T>.Some`. Ordinary inference through the nested type's containing generic type supplies `T = int`.
4. The argument then has a union conversion to the fixed parameter type, `Option<int>`.

### Inference from an Assignment Target

We expect `Option<int> result = Parse("42")` in the walkthrough to work if [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md) is adopted. This is a general inference feature, independent of unions. Without it, callers must write `Parse<int>("42")`.

The broader [generalized type-inference proposal](https://github.com/dotnet/csharplang/blob/madstorgersen-generalized-type-inference/proposals/generalized-type-inference.md) also addresses information flowing from context. These drafts remain in progress.

### Target-Typed Patterns and Switch Arms

We expect this to work with target-typed static member access:

```csharp
union Result<T, E>
{
    case Ok(T Value);
    case Error(E Value);
}

Result<U, E> Map<T, U, E>(Result<T, E> result, Func<T, U> mapper)
    => result switch
    {
        .Ok(var value) => .Ok(mapper(value)),
        .Error(var error) => .Error(error)
    };
```

1. The input type `Result<T, E>` qualifies the pattern case names under the proposed [pattern rules](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#pattern-matching).
2. Union matching extracts a `T` or an `E` from the corresponding case.
3. The return type `Result<U, E>` target-types the construction in each arm.
4. The resulting case values use union conversions to satisfy that return type.

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

These forms differ from [inference from a known case type](#inference-from-a-known-case-type): the case type is not yet available when inference needs to select it.

### Metadata Encoding

How should metadata identify generated cases and their construction operations? The design must preserve the case types as ordinary usable types while preventing older compilers from accepting construction syntax that a supporting compiler rejects.
