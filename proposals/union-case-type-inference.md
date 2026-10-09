# Generic type inference through union case types

*This proposal builds on the [unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md), [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md), and [target-typed inference for type patterns](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns.md) proposals.*

## Summary
[summary]: #summary

Lower-bound and upper-bound inference can proceed through a uniquely matching case type of a union. The matching case is selected and ordinary inference then recurses through the actual case type.

This allows, for example, `Some<int>` to contribute an `int` bound when the parameter type is `Option<T>`, and allows `Some<T>` to infer `T` from an `Option<int>` target or pattern input.

## Motivation
[motivation]: #motivation

The union conversion and matching features connect a union type to its case types, but generic type inference does not otherwise see that relationship. As a result, type arguments which are clear from the selected case must be written explicitly, or each consumer of inference must introduce its own special rule.

Inference should follow the same type relationship that makes the case relevant: select the unique matching case, then recurse through that case using the existing bound-inference rules. In particular, inference should not project through the type arguments of the union itself. Class and struct type parameters are invariant, so such a projection would incorrectly turn covariance or contravariance in a case type into exact inference.

Putting this behavior in lower-bound and upper-bound inference also makes it available to any inference consumer that supplies the relevant source and target types. Target-typed invocation inference and type-pattern inference can use the shared behavior directly, without specifying pretend or "as if" method calls for unions.

## Detailed design
[design]: #detailed-design

When lower-bound inference targets a union type, it selects a unique case type which can be made to match the source type by substituting types for unfixed type variables. Inference then continues from the source type to that case type.

Conversely, when upper-bound inference starts from a union type, it selects a unique case type which the target type can be made to match by substituting types for unfixed type variables. Inference then continues from that case type to the target type.

The case selection test admits identity and the inheritance or interface relationships already used by bound inference. Exactly one *case type* must satisfy the test. Multiple substitutions which make the same case type satisfy the test do not by themselves make case selection ambiguous; the recursive inference rules determine which bounds, if any, result.

### Lower-bound inference

The following bullet is added to [§12.6.3.11 Lower-bound inferences](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#126311-lower-bound-inferences) in `standard/expressions.md`, before the existing bullet which determines sets of type arguments.

Throughout this section, **bold** indicates text being added to the existing specification.

> A *lower-bound inference from* a type `U` *to* a type `V` is made as follows:
>
> - If `V` is one of the *unfixed* `Xᵢ` then `U` is added to the set of lower bounds for `Xᵢ`.
> - Otherwise, if `V` is the type `V₁?` and `U` is the type `U₁?` then a lower bound inference is made from `U₁` to `V₁`.
> - **Otherwise, if `V` is a union type with a unique case type `K` that can, by substitution of types for unfixed type variables, be made a type such that `U` (or, if `U` is a type parameter, its effective base class or any member of its effective interface set) is identical to, inherits from (directly or indirectly), or implements (directly or indirectly) that type, then a lower-bound inference is made from `U` to `K`.**
> - Otherwise, sets `U₁...Uₑ` and `V₁...Vₑ` are determined by checking if any of the following cases apply:

### Upper-bound inference

The following bullet is added to [§12.6.3.12 Upper-bound inferences](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#126312-upper-bound-inferences) in `standard/expressions.md`, before the existing bullet which determines sets of type arguments.

> An *upper-bound inference from* a type `U` *to* a type `V` is made as follows:
>
> - If `V` is one of the *unfixed* `Xᵢ` then `U` is added to the set of upper bounds for `Xᵢ`.
> - **Otherwise, if `U` is a union type with a unique case type `K` such that `V` can, by substitution of types for unfixed type variables, be made identical to `K` or made a type that inherits from or implements `K`, then an upper-bound inference is made from `K` to `V`.**
> - Otherwise, sets `V₁...Vₑ` and `U₁...Uₑ` are determined by checking if any of the following cases apply:

## Examples

The examples use these declarations:

```csharp
public record None();
public record Some<T>(T Value);
public union Option<T>(None, Some<T>);
```

### Lower-bound inference

An argument of case type can contribute bounds to a parameter of union type:

```csharp
static void Consume<T>(Option<T> option) { }

Consume(new Some<int>(42)); // T is inferred as int
```

Input type inference makes a lower-bound inference from `Some<int>` to `Option<T>`. The unique matching case is `Some<T>`, so lower-bound inference recurses from `Some<int>` to `Some<T>` and infers `T` as `int`.

### Upper-bound inference

A union target can contribute bounds to a result of case type:

```csharp
static Some<T> CreateSome<T>() => ...;

Option<int> option = CreateSome(); // T is inferred as int
```

Target-typed generic type inference makes an upper-bound inference from `Option<int>` to `Some<T>`. The unique matching case is `Some<int>`, so upper-bound inference recurses from `Some<int>` to `Some<T>` and infers `T` as `int`.

The same relationship is available to type-pattern inference:

```csharp
if (option is Some some)
{
    // The pattern type is Some<int>.
}
```

Here the pattern input type is `Option<int>` and the prospective pattern type is `Some<T>`, leading to the same upper-bound inference.

### Preserve variance in the case type

Inference recurses through the case type rather than through the union's generic arguments:

```csharp
public union Seq<T>(IEnumerable<T>);

static IEnumerable<T> Singleton<T>(ref T value) => [value];

string item = "item";
Seq<object> sequence = Singleton(ref item); // T is inferred as string
```

Inference from the `ref` argument contributes an exact bound of `string`. Upper-bound inference from `Seq<object>` to `IEnumerable<T>` selects the case `IEnumerable<object>` and recurses through `IEnumerable<out T>`, contributing an upper bound of `object`. Fixing therefore retains the exact bound and fixes `T` as `string`.

Projecting the inference through `Seq<T>` instead would treat the union's class or struct type parameter as invariant and contribute an exact bound of `object`. That would conflict with the exact `string` bound and cause inference to fail, losing the covariance expressed by the actual case type.

### Case type shapes

Reordered and repeated type parameters are handled by ordinary inference after the case is selected:

```csharp
public record Pair<TFirst, TSecond>(TFirst First, TSecond Second);
public union Reordered<T, U>(Pair<U, T>);
public union Repeated<T>(Pair<T, T>);
```

Lower-bound inference from `Pair<string, int>` to `Reordered<T, U>` selects `Pair<U, T>` and infers `T` as `int` and `U` as `string`. A source of `Pair<int, int>` can similarly match `Repeated<T>`, while `Pair<int, string>` cannot make that case type match through a single substitution for `T`.

A case type need not mention every union type parameter:

```csharp
public union Wrapped<TTag, T>(None, Some<T>);

static void Use<TTag, T>(TTag tag, Wrapped<TTag, T> value) { }

Use("tag", new Some<int>(42)); // TTag is string; T is int
```

The `Some<T>` case contributes a bound only for `T`; the first argument determines `TTag`. The rules also require neither the union nor every case to be generic. A non-generic union can still contribute its fixed case type when inference compares it with a generic target shape, and a direct type-parameter case is handled by the ordinary recursive rule when selected.

## Ambiguity and no inference

Case selection is deliberately conservative. If more than one case type satisfies the substitution and type-relationship test, the union bullet does not apply and no union-derived inference is made:

```csharp
public union Sequence<T>(IEnumerable<T>, IReadOnlyCollection<T>);

static void Consume<T>(Sequence<T> sequence) { }

Consume(new List<int>()); // Both cases match; the union adds no bound for T.
```

This rule does not attempt to predict which case a later union conversion or overload-resolution step might prefer. Those steps can depend on information and betterness rules outside bound inference.

Multiple possible substitutions for one case type do not count as multiple matching cases. After that case is selected, existing recursive inference rules apply, including their own uniqueness requirements, and may still produce no bounds.

## Interactions and limitations

This proposal assumes the definitions of *union type* and *case type* from the current unions proposal. It changes only lower-bound and upper-bound inference. It does not specify union declarations, representation, conversions, matching or exhaustiveness, case construction or type-pattern syntax, or compiler implementation.

The proposal does not itself introduce a new inference consumer. Consumers such as target-typed generic type inference and type-pattern inference benefit when they initiate the relevant upper-bound or lower-bound inference. Existing consumers benefit in the same way without needing union-specific mappings to synthetic method signatures.

## Compatibility impact

This is an improvement to inference and can therefore change binding of existing source code. New bounds can make a previously inapplicable overload candidate applicable, change which inferred type is selected, introduce an overload-resolution ambiguity, or cause an inference which previously succeeded to fail. The frequency and practical impact of those changes require investigation.

## Alternatives

### Infer through union type arguments

The compiler could map a case back to the type arguments of its containing union and infer through those arguments. This is rejected because generic class and struct parameters are invariant. It would replace the variance and parameter arrangement of the case type with the unrelated shape of the union type, producing exact inference in cases such as `Seq<T>(IEnumerable<T>)` and mishandling reordered, repeated, or omitted parameters.

### Add rules to each inference consumer

Target-typed invocations, type patterns, and future consumers could each specify union-aware inference independently, potentially through pretend method calls. This is more complex and risks differences between consumers. Extending the shared bound-inference operations expresses the behavior once at the point where source and target types are already compared.
