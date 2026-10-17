# Generic type inference through union case types

Champion issue: <https://github.com/dotnet/csharplang/issues/9662>

*This proposal builds on the [unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) proposal and generalized type inference.*

## Summary
[summary]: #summary

Lower-bound and upper-bound inference can proceed through a uniquely matching case type of a union. The matching case is selected and ordinary inference then recurses through the actual case type.

This allows, for example, `Some<int>` to contribute an `int` bound when the parameter type is `Option<T>`, and allows `Some<T>` to infer `T` from an `Option<int>` target or pattern input.

## Motivation
[motivation]: #motivation

The union conversion and matching features connect a union type to its case types, but generic type inference does not otherwise see that relationship.

Inference should follow the same type relationship that makes the case relevant: select the unique matching case, then recurse through that case using the existing bound-inference rules.

## Detailed design
[design]: #detailed-design

When lower-bound inference targets a union type, it selects a unique case type which can be made to match the source type by substituting types for unfixed type variables. Inference then continues from the source type to that case type.

Conversely, when upper-bound inference starts from a union type, it selects a unique case type which the target type can be made to match by substituting types for unfixed type variables. Inference then continues from that case type to the target type.

The case selection test admits identity and the inheritance or interface relationships already used by bound inference. Exactly one *case type* must satisfy the test. Multiple substitutions which make the same case type satisfy the test do not by themselves make case selection ambiguous; the recursive inference rules determine which bounds, if any, result.

### Lower-bound inference

The following bullet is added to [§12.6.3.11 Lower-bound inferences](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#126311-lower-bound-inferences).

**Bold** indicates text being added to the existing specification.

> A *lower-bound inference from* a type `U` *to* a type `V` is made as follows:
>
> - If `V` is one of the *unfixed* `Xᵢ` then `U` is added to the set of lower bounds for `Xᵢ`.
> - Otherwise, if `V` is the type `V₁?` and `U` is the type `U₁?` then a lower bound inference is made from `U₁` to `V₁`.
> - **Otherwise, if `V` is a union type with a unique case type `K` that can, by substitution of types for unfixed type variables, be made a type such that `U` (or, if `U` is a type parameter, its effective base class or any member of its effective interface set) is identical to, inherits from (directly or indirectly), or implements (directly or indirectly) that type, then a lower-bound inference is made from `U` to `K`.**
> - Otherwise, sets `U₁...Uₑ` and `V₁...Vₑ` are determined by checking if any of the following cases apply:

### Upper-bound inference

The following bullet is added to [§12.6.3.12 Upper-bound inferences](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#126312-upper-bound-inferences).

> An *upper-bound inference from* a type `U` *to* a type `V` is made as follows:
>
> - If `V` is one of the *unfixed* `Xᵢ` then `U` is added to the set of upper bounds for `Xᵢ`.
> - **Otherwise, if `U` is a union type with a unique case type `K` such that `V` can, by substitution of types for unfixed type variables, be made identical to `K` or made a type that inherits from or implements `K`, then an upper-bound inference is made from `K` to `V`.**
> - Otherwise, sets `V₁...Vₑ` and `U₁...Uₑ` are determined by checking if any of the following cases apply:

### Examples

The examples use these declarations:

```csharp
public record None();
public record Some<T>(T Value);
public union Option<T>(None, Some<T>);
```

#### Lower-bound inference

An argument of a case type can contribute bounds to a parameter of a union type:

```csharp
static void Consume<T>(Option<T> option) { }

Consume(new Some<int>(42)); // T is inferred as int
```

Input type inference makes a lower-bound inference from `Some<int>` to `Option<T>`. The unique matching case is `Some<T>`, so lower-bound inference recurses from `Some<int>` to `Some<T>` and infers `T` as `int`.

#### Upper-bound inference

In combination with [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md), a union target can contribute bounds to a result of a case type:

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

#### Preserve variance in the case type

Inference recurses through the case type rather than through the union's generic arguments:

```csharp
public union Seq<T>(IEnumerable<T>);

static IEnumerable<T> Singleton<T>(ref T value) => [value];

string item = "item";
Seq<object> sequence = Singleton(ref item); // T is inferred as string
```

Inference from the `ref` argument contributes an exact bound of `string`. Upper-bound inference from `Seq<object>` to `IEnumerable<T>` selects the case `IEnumerable<object>` and recurses through `IEnumerable<out T>`, contributing an upper bound of `object`. Fixing therefore retains the exact bound and fixes `T` as `string`.

Projecting the inference through `Seq<T>` instead would treat the union's class or struct type parameter as invariant and contribute an exact bound of `object`. That would conflict with the exact `string` bound and cause inference to fail, losing the covariance expressed by the actual case type.

### Ambiguity and no inference

Case selection is deliberately conservative. If more than one case type satisfies the substitution and type-relationship test, the union bullet does not apply and no union-derived inference is made:

```csharp
public union Sequence<T>(IEnumerable<T>, IReadOnlyCollection<T>);

static void Consume<T>(Sequence<T> sequence) { }

Consume(new List<int>()); // Both cases match; the union adds no bound for T.
```

This rule does not attempt to predict which case a later union conversion or overload-resolution step might prefer. Those steps can depend on information and betterness rules outside bound inference.

Multiple possible substitutions for one case type do not count as multiple matching cases. After that case is selected, existing recursive inference rules apply, including their own uniqueness requirements, and may still produce no bounds.

## Drawbacks
[drawbacks]: #drawbacks

### Compatibility impact

This is an improvement to inference and can therefore change binding of existing source code. New bounds can make a previously inapplicable overload candidate applicable, change which inferred type is selected, introduce an overload-resolution ambiguity, or cause an inference which previously succeeded to fail. The frequency and practical impact of those changes require investigation.

### Conversion precedence

The proposed inference mirrors union conversions, and if successful would often be followed by a case type being successfully converted to a union type. However, user defined conversions can shadow union conversions and take precedence, even where union/case type inference has been applied.

We could accept this, try to limit inference to when a union conversion would not be shadowed (very gnarly!) or perhaps rethink the precedence order between user-defined conversions and union conversions.

## Alternatives
[alternatives]: #alternatives

None.

## Open questions
[open]: #open-questions

The practical compatibility impact on existing source code remains to be measured.
