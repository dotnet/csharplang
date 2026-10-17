# Common Design for Discriminated Unions and Data-Carrying Enums

This document is not intended to be read on its own. It holds design material referenced by the [union-based discriminated-union proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md) and the [data-carrying enum proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md) to avoid repetition.

This file is for design material that is identical or very nearly identical in both proposals. Any differences are called out explicitly.

## Contextual Construction and Inference

Case construction and patterns that name the enclosing type, such as `Option<int>.Some(42)`, work without target-typed access or improved generic inference. The examples below use these alternative declarations:

```csharp
enum struct Option<T>
{
    None = default,
    Some(T Value)
}

union Option<T>
{
    case None = default;
    case Some(T Value);
}
```

Their construction and pattern syntax is the same, so the use-site examples apply to both designs. The steps below identify which rules each shortened form needs, so compatibility can be checked as the related proposals evolve.

The analysis follows the [revised target-typed generic inference draft](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md): successful ordinary binding takes precedence over rebinding with a target. The related proposals remain in progress.

Inference starting from case arguments, including `Use(.Some(42))` without another source for the outer type argument, remains an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#inference-from-case-arguments).

### Case Access with a Known Target

We expect these forms to work with [target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md) and either proposal's case-construction rules:

```csharp
Option<int> none = .None;
Option<int> some = .Some(42);
```

1. Each initializer has the target type `Option<int>`.
2. The [target-typed member binding conversion](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#basic-expression) qualifies `.None` as `Option<int>.None`. The [invocation target-typing conversion](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#target-typing-with-invocations) similarly qualifies `.Some(42)` as `Option<int>.Some(42)`.
3. Lookup finds the nested case types on the already constructed `Option<int>`. Its type arguments are known, just as they would be for lookup of a static property on that type. No inference through the nested member is needed.
4. Case construction produces the corresponding case values without `new`. Each result then uses the [case-to-enum conversion](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md#generated-apis-and-conversions) or the existing [union conversion](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-conversions) to satisfy the target.

Without a target, `var value = .None` is an error. The leading-dot rules need to recognize case types under the construction rules of the [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md#construction-and-case-access) and [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md#construction-and-case-access) designs.

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
6. The first parameter supplies the target `Option<int>`. Binding `.None` then follows the known-target lookup above, producing the case value and converting it to the enclosing type.

This ordering agrees with the revised draft's [nested-inference rules](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md#nested-inference).

### Inference from a Known Case Type

We expect this call to infer `T = int` for `union` if [union-case type inference](https://github.com/dotnet/csharplang/pull/10355) is adopted. For `enum class`, ordinary inference through the case's `Option<int>` base type already supplies `T = int`.

For `enum struct`, the corresponding inference remains an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md#inference-from-a-known-case-type) because existing method inference does not follow its generated user-defined conversion.

```csharp
void Use<T>(Option<T> value) { /* ... */ }

Use(Option<int>.Some(42));
```

For the union-based design, inference proceeds as follows:

1. Qualified construction produces an `Option<int>.Some`, independently of the parameter's target type.
2. Lower-bound inference against `Option<T>` finds the unique matching case, `Option<T>.Some`.
3. The proposed rule recurses from `Option<int>.Some` to `Option<T>.Some`. Ordinary inference through the nested type's containing generic type supplies `T = int`.
4. The argument then has a union conversion to the fixed parameter type, `Option<int>`.

### Target-Typed Patterns and Switch Arms

We expect these patterns and case constructions to work with target-typed static member access. The type parameters are declared on `Map`, so its body needs no generic inference. The example applies to either declaration:

```csharp
closed enum class Result<T, E>
{
    Ok(T Value),
    Error(E Value)
}

union Result<T, E>
{
    case Ok(T Value);
    case Error(E Value);
}
```

```csharp
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
4. `mapper(value)` supplies a `U` for `Result<U, E>.Ok`, while `error` supplies an `E` for `Result<U, E>.Error`. Each case value converts to the enclosing result type using that proposal's conversion rules.

## Generic Case Identity

In both designs, a nested case type includes every type argument of its enclosing type, even when its payload does not use that argument. For either `Result<T, E>` declaration above:

```csharp
Result<int, string>.Error error = Result<int, string>.Error("Failed");
Result<string, string> result = error; // error: the nested case types differ
Result<string, string> reconstructed = Result<string, string>.Error(error.Value);
```

The incoming error cannot be propagated directly when a pipeline changes only the success type. The `Map` example reconstructs its `Error` case for this reason. The same restriction applies to `record struct` cases. Improved inference can shorten construction, but cannot change type identity.

Ordinary unions can instead use separately declared records whose type parameters describe only their own payloads:

```csharp
public record Ok<T>(T Value);
public record Error<E>(E Value);
public union Result<T, E>(Ok<T>, Error<E>);
```

Here, `Error<E>` can be reused across different success types. Both shorthand designs choose nesting and therefore give up this reuse, as explored in the working group's [analysis of case nesting](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/to-nest-or-not-to-nest.md#too-many-type-parameters).

## Metadata and Compatibility

The compiler emits metadata identifying case types and their construction operations. For data-carrying enums, it also identifies the enclosing enum. Supporting C# compilers use it to enforce the [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md#construction-and-case-access) and [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md#construction-and-case-access) construction rules across assembly boundaries. The [encoding](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#metadata-encoding) remains an open question.

The compiler adds a nonoptional [CompilerFeatureRequired](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.compilerfeaturerequiredattribute) attribute to:

- The data-carrying enum and its case types, with a feature name such as `"DataCarryingEnums"` (TBD).
- Types introduced by union `case` declarations, with a feature name such as `"DiscriminatedUnionCases"` (TBD).

Compilers that recognize the attribute but lack feature support reject use of these types. This prevents acceptance of `new E.Case(...)` that would become invalid after a compiler upgrade. An error-level `ObsoleteAttribute` guard covers compilers predating `CompilerFeatureRequired`, following the [required-members compatibility pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/required-members.md#metadata-representation). Supporting compilers ignore that guard.

## Default Values and Nullability

### Nullability of Default Cases

Nullable analysis checks a designated `default` case as if each payload property were initialized with `default` of its declared type. Both declarations below produce a warning at `= default` because the designated case has a null `Unit` despite its non-nullable declaration:

```csharp
enum struct Measurement
{
    Reading(double Value, string Unit) = default // warning
}

union Measurement
{
    case Reading(double Value, string Unit) = default; // warning
}
```

Declaring `string? Unit` permits that value. Writing `= default!` suppresses these warnings without changing the default payload values.

### Nullable Values and Case Payloads

A null payload, a struct representing no case, and a nullable struct with no value are distinct states. Consider either declaration, with no designated `default` case:

```csharp
enum struct Message
{
    Empty,
    Text(string? Value)
}

union Message
{
    case struct Empty;
    case struct Text(string? Value);
}
```

```csharp
Message textCase = Message.Text(null);
Message uninitialized = default;
Message? noUnion = null;
Message? present = default(Message);
```

The represented cases below apply to both designs. The last column follows the existing [union null-pattern rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#constant-pattern), including [matching nullable unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#resolved-union-matching-for-nullable-of-a-union-value-type). Null-pattern matching for `enum struct` remains an [open question](data-carrying-enums.md#uninitialized-values-in-matching).

| Variable | Represented case | Nullable wrapper's `HasValue` | `is null` for `union` |
| --- | --- | --- | --- |
| `textCase` | `Text` with a null payload | N/A | `false` |
| `uninitialized` | No case | N/A | `true` |
| `noUnion` | No union value | `false` | `true` |
| `present` | The contained union represents no case | `true` | `true` |

For `union`, `textCase.Value` returns a boxed `Text` record, while `uninitialized.Value` returns null. Designating `Empty` as the `default` case makes the second and fourth rows represent `Empty` instead. The union's `Value` is then a boxed `Empty` record, and neither matches `null`. The null nullable wrapper in the third row remains null.

## Equality and Hashing

Each case declared in a union or data-carrying enum is a record representing data. We expect users to treat the enclosing union or enum as a data value too, so we propose giving it value equality and hashing.

For a union or enum named `Result`, we propose:

- `IEquatable<Result>`.
- A strongly typed `Equals(Result)` method (`Equals(Result?)` for `enum class`).
- Overrides of `Equals(object?)` and `GetHashCode()`.
- `==` and `!=` operators for `Result`.

We could omit these members, but we believe that would be surprising and undesirable. Users would have to unwrap cases for comparisons or write the boilerplate that records already eliminate. Combining records into a discriminated union should preserve that convenience.

Customization of synthesized equality members follows the [record-class](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#equality-members) or [record-struct](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#equality-members) rules, according to the enclosing type's class or struct kind.

Note that a record's synthesized `==` and `!=` operators delegate to `Equals`. We follow the same approach here.

Instances are equal when they represent the same case and their contents are equal under that case record's equality rules.

For a struct with no designated `default` case, default-initialized instances compare equal to each other and unequal to instances representing a case. With a designated `default` case, equality uses that case with default payload values.

Hashing uses the selected case and its contents.

For example, these declarations have class cases:

```csharp
enum class Result
{
    Success(string Value),
    Failure(int Code)
}

union Result
{
    case Success(string Value);
    case Failure(int Code);
}
```

The object-backed `union Result` can synthesize the following equality members. Other union members and interfaces are omitted:

```csharp
public struct Result : IEquatable<Result>
{
    public object? Value { get; }

    public readonly bool Equals(Result other)
        => object.Equals(Value, other.Value);

    public override readonly bool Equals(object? obj)
        => obj is Result other && Equals(other);

    public override readonly int GetHashCode()
        => Value?.GetHashCode() ?? 0;

    public static bool operator ==(Result left, Result right)
        => left.Equals(right);

    public static bool operator !=(Result left, Result right)
        => !(left == right);
}
```

The `enum class` form gets these behaviors from its record hierarchy. For `enum struct` and unions using non-boxed storage, the [non-boxed layout proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#equality-and-hashing) shows how equality and hashing operate on decomposed storage without boxing the case.

## Recursive Payloads

Recursive payloads follow the normal [value-type layout rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1642-value-semantics). A value type cannot recursively contain itself by value when that would require infinite storage. Recursion through references is permitted.

For example, the enum declaration below requires infinite storage. The union declaration works with its default object-backed storage because `Add` is boxed:

```csharp
enum struct Expr
{
    Number(int Value),
    Add(Expr Left, Expr Right) // error
}

union Expr
{
    case struct Number(int Value);
    case struct Add(Expr Left, Expr Right); // valid with object-backed storage
}
```

A non-boxed union layout must retain reference indirection for that recursive case, or reject the declaration. A `record class` `Add` case provides that indirection without boxing.

## Concurrent Access

Concurrent access to `enum struct` and unions using non-boxed storage follows the [layout proposal's concurrency and safety requirements](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md#concurrent-access-and-safety).

## Library Evolution

Changing the designated `default` case changes what `default` represents in either design and is a behavioral breaking change. Adding a case can increase storage or alignment requirements when the enclosing type uses a non-boxed layout. Previously compiled switches use any existing fallback if they encounter a new case. A switch expression with no matching arm throws.

## Open Questions

### Binding Case Access

The intended binding distinguishes type and value contexts and supports chained member access. Using either `Option<T>` declaration above:

```csharp
Option<int>.None none = Option<int>.None;
Option<int>.Some some = Option<int>.Some(42);
bool equal = Option<int>.None.Equals(none);
```

The declared types name the nested records. The first two initializers produce case instances, and the last invokes the `None` instance's `Equals` method.

Precise lookup rules remain to be specified, including how to choose between a case type's static members and its instance's members in chained accesses. [LDM previously explored a general type-as-value mechanism](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-08-27.md#type-value-conversion) for data-free cases. We need to decide whether to use that mechanism or define case-specific binding rules. This work is required even for fully qualified case access and is independent of target-typed access and generic inference.

### Always-Readonly Struct Types

Should `enum struct` types and unions with case declarations, together with their `record struct` cases, always be readonly, or should authors opt in with the `readonly` modifier?

### Customizing Case Records

Both designs could let authors supply the record body as well as its name and primary constructor parameters. For example, these are possible future forms:

```csharp
enum class Message
{
    Text(string Value)
    {
        public int Length => Value.Length;
    },
    Empty
}

union Message
{
    case Text(string Value)
    {
        public int Length => Value.Length;
    }
    case Empty;
}
```

The body could declare members or customize synthesized record members under ordinary record rules. Cases could also specify additional interfaces. Whether these extensions are useful enough to support, and their exact syntax and restrictions, remain open. Both designs leave room to add them later.

Allowing mutable case state would require revisiting shared class instances used for data-free cases and union defaults.

### Independent Generic Case Parameters

Should a case introduce its own type parameters, for example an enum case `X<T>(T Value)` or a union case `case X<T>(T Value);` in a nongeneric declaration?

Reference-type cases could represent this as a family of record types. The design would need to explain how to match or deconstruct all `X<T>` values without knowing `T`, infer the arguments, enforce constraints, and determine exhaustiveness. The [closed-hierarchy proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/closed-hierarchies.md#type-parameter-restriction) currently restricts independent type parameters for this reason.

Inline storage cannot directly hold an arbitrary independent `T`: the enclosing type's size and GC layout must be determined without that type argument. Boxing or another form of indirection would introduce a different storage tradeoff. We should establish the need for generic cases before adding that complexity.

### Empty Parameter Lists

We recommend rejecting empty parameter lists in case declarations and using data-free cases without parentheses, as with ordinary enums. Cases with optional or `params` parameters still require `()` when no arguments are supplied.

For example, either declaration could provide these cases:

```csharp
enum struct Message
{
    Empty,
    Text(string Value = ""),
    Numbers(params int[] Values)
}

union Message
{
    case struct Empty;
    case struct Text(string Value = "");
    case struct Numbers(params int[] Values);
}
```

The same construction syntax applies to both:

```csharp
var empty = Message.Empty;
var text = Message.Text();       // Value is ""
var numbers = Message.Numbers(); // Values is an empty array
```

### Inference from Case Arguments

We do not expect the following forms to infer their omitted type arguments under [generalized generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-generalized-type-inference/proposals/generalized-type-inference.md) or the revised target-typed inference draft. Each needs additional rules:

- `Option.Some(42)` needs lookup and inference of the containing type's omitted arguments. [Constructor-call inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md), tracked by [#9627](https://github.com/dotnet/csharplang/issues/9627), and the [updated proposal](https://github.com/dotnet/csharplang/pull/10312) with [type-group lookup](https://github.com/dotnet/csharplang/pull/10310) offer related machinery. Applying it to case construction syntax needs an explicit design.
- `Use(.Some(42))`, for `Use<T>(Option<T> value)`, needs case lookup on a partially inferred target and gathering bounds from the case arguments. The [current nested-inference model](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md#nested-inference) instead requires outer inference to complete before supplying a target to an inner expression that cannot bind independently.

These forms differ from [inference from a known case type](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#inference-from-a-known-case-type): the case type is not yet available when inference needs to select it.

### Metadata Encoding

How should metadata identify the declaration, its case types, and their construction operations? The attribute or other metadata form and final generated API names remain to be designed.
