# Common Design for Discriminated Unions and Data-Carrying Enums

This document is not intended to be read on its own. It holds design material referenced by the [union-based discriminated-union proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md) and the [data-carrying enum proposal](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md) to avoid repetition.

This file is for design material that is identical or very nearly identical in both proposals. Any differences are called out explicitly.

## Contextual Construction and Inference

Case construction and patterns that name the enclosing type, such as `Option<int>.Some(42)`, work without target-typed access or improved generic inference. The examples below use these alternative declarations:

```csharp
enum struct Option<T>
{
    None = default,
    Some(T Value)
}

union struct Option<T>
{
    case None = default;
    case Some(T Value);
}
```

Their construction and pattern syntax is the same, so the use-site examples apply to both designs. The steps below identify which rules each shortened form needs, so compatibility can be checked as the related proposals evolve.

[Target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md) defines leading-dot lookup and its conversions. [Generalized generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-generalized-type-inference/proposals/generalized-type-inference.md) describes target-derived bounds. The [revised target-typed generic inference draft](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md) specifies when binding retries with a target and how nested inference proceeds. These drafts remain in progress. The analysis below uses the revised draft's rule that successful ordinary binding takes precedence.

Inference starting from case arguments, including `Use(.Some(42))` without another source for the outer type argument, remains an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#inference-from-case-arguments).

### Case Access with a Known Target

We expect these forms to work with target-typed static member access and either proposal's case-construction rules:

```csharp
Option<int> none = .None;
Option<int> some = .Some(42);
```

1. Each initializer has the target type `Option<int>`.
2. The [target-typed member binding conversion](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#basic-expression) qualifies `.None` as `Option<int>.None`. The [invocation target-typing conversion](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#target-typing-with-invocations) similarly qualifies `.Some(42)` as `Option<int>.Some(42)`.
3. Lookup finds the nested case types on the already constructed `Option<int>`. Its type arguments are known, just as they would be for lookup of a static property on that type. No inference through the nested member is needed.
4. Case construction produces the corresponding case values without `new`. Each result then uses the [case-to-enum conversion](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#generated-apis-and-conversions) or the existing [union conversion](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-conversions) to satisfy the target.

Without a target, `var value = .None` is an error. The leading-dot rules need to recognize generated cases under the construction rules of the [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#construction-and-case-access) and [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md#construction-and-case-access) designs.

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

We expect this call to infer `T = int` for both `union` and `union struct` if [union-case type inference](https://github.com/dotnet/csharplang/pull/10355) is adopted. For `enum class`, ordinary inference through the case's `Option<int>` base type already supplies `T = int`.

For `enum struct`, the corresponding inference remains an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#inference-from-a-known-case-type) because existing method inference does not follow its generated user-defined conversion.

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

[Type-pattern inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns.md) separately addresses written type names with omitted generic arguments.

## Metadata and Compatibility

The compiler emits metadata identifying generated case types and their construction operations. For data-carrying enums, it also identifies the enclosing enum. Supporting C# compilers use it to enforce the [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#construction-and-case-access) and [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md#construction-and-case-access) construction rules across assembly boundaries. The [encoding](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#metadata-encoding) remains an open question.

The compiler adds a nonoptional [CompilerFeatureRequired](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.compilerfeaturerequiredattribute) attribute to:

- The data-carrying enum and its generated case types, with a feature name such as `"DataCarryingEnums"` (TBD).
- Generated union case types, with a feature name such as `"DiscriminatedUnionCases"` (TBD).

Compilers that recognize the attribute but lack feature support reject use of these types. This prevents acceptance of `new E.Case(...)` that would become invalid after a compiler upgrade. An error-level `ObsoleteAttribute` guard covers compilers predating `CompilerFeatureRequired`, following the [required-members compatibility pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/required-members.md#metadata-representation). Supporting compilers ignore that guard.

## Nullability of Default Cases

Nullable analysis checks a designated `default` case as if each payload property were initialized with `default` of its declared type. Both declarations below produce a warning at `= default` because zero-initialization leaves `Unit` null despite its non-nullable declaration:

```csharp
enum struct Measurement
{
    Reading(double Value, string Unit) = default // warning
}

union struct Measurement
{
    case Reading(double Value, string Unit) = default; // warning
}
```

Declaring `string? Unit` permits that value. Writing `= default!` suppresses these warnings without changing the default payload values.

## Recursive Payloads

Recursive payloads follow the normal [value-type layout rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1642-value-semantics). A value type cannot recursively contain itself by value when that would require infinite storage. Recursion through references is permitted.

For example, both declarations below would require infinite storage:

```csharp
enum struct Expr
{
    Number(int Value),
    Add(Expr Left, Expr Right) // error
}

union struct Expr
{
    case Number(int Value);
    case Add(Expr Left, Expr Right); // error
}
```

## Library Evolution

Renaming or removing public cases, changing their payload parameters or properties, or changing a declaration to or from its `struct` form affects existing APIs. Changing the designated `default` case changes the meaning of zero-initialized storage and is a behavioral breaking change. Adding a case to a declaration with inline storage can increase its storage or alignment requirements. Previously compiled switches retain their normal fallback or unmatched-switch behavior if they encounter a new case.

## Open Questions

### Always-Readonly Struct Types

Should declarations using the `struct` keyword and their generated case records always be readonly, or should authors opt in with the `readonly` modifier?

### Independent Generic Case Parameters

Should a case introduce its own type parameters, for example an enum case `X<T>(T Value)` or a union case `case X<T>(T Value);` in a nongeneric declaration?

Reference-type cases could represent this as a family of record types. The design would need to explain how to match or deconstruct all `X<T>` values without knowing `T`, infer the arguments, enforce constraints, and determine exhaustiveness. The [closed-hierarchy proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/closed-hierarchies.md#type-parameter-restriction) currently restricts independent type parameters for this reason.

Inline storage cannot directly hold an arbitrary independent `T`: the enclosing type's size and GC layout must be determined without that type argument. Boxing or another form of indirection would introduce a different storage tradeoff. We should establish the need for generic cases before adding that complexity.

### Empty Parameter Lists

We recommend disallowing empty parameter lists in case declarations and requiring cases declared without parameters to be used without parentheses. This keeps the syntax consistent with ordinary enums: data-free cases have no parameters or parentheses, while data-carrying cases declare parameters and use parentheses for construction. Parentheses remain required when optional or `params` parameters allow zero arguments, so such calls still use `()`.

For example, either declaration could provide these cases:

```csharp
enum struct Message
{
    Empty,
    Text(string Value = ""),
    Numbers(params int[] Values)
}

union struct Message
{
    case Empty;
    case Text(string Value = "");
    case Numbers(params int[] Values);
}
```

The same construction syntax applies to both:

```csharp
var empty = Message.Empty;
var text = Message.Text();       // Value is ""
var numbers = Message.Numbers(); // Values is an empty array
```

### Inference from Case Arguments

We do not expect the following forms to infer their omitted type arguments under the current drafts. Each needs additional rules:

- `Option.Some(42)` needs lookup and inference of the containing type's omitted arguments. [Constructor-call inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md), tracked by [#9627](https://github.com/dotnet/csharplang/issues/9627), and the [updated proposal](https://github.com/dotnet/csharplang/pull/10312) with [type-group lookup](https://github.com/dotnet/csharplang/pull/10310) offer related machinery. Applying it to generated case syntax needs an explicit design.
- `Use(.Some(42))`, for `Use<T>(Option<T> value)`, needs case lookup on a partially inferred target and gathering bounds from the case arguments. The [current nested-inference model](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md#nested-inference) instead requires outer inference to complete before supplying a target to an inner expression that cannot bind independently.

These forms differ from [inference from a known case type](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#inference-from-a-known-case-type): the case type is not yet available when inference needs to select it.

### Metadata Encoding

How should metadata identify the declaration, its generated cases, and their construction operations? The attribute or other metadata form and final generated API names remain to be designed. The enum-based encoding must also describe hierarchy restrictions.
