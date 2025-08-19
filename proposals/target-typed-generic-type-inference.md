# Target-typed generic type inference

## Summary

Generic type inference may take a target type into account. For instance, given:

```csharp
public class MyCollection
{
    public MyCollection<T> Create<T>() { ... }
}
public class MyCollection<T> : IEnumerable<T> { ... }
```

We would allow the `Create` method to be called without type argument when it can be inferred from a target type:

```csharp
IEnumerable<string> c = MyCollection.Create(); // 'T' = 'string' inferred from target type
```

## Motivation

Generic factory methods often need explicit type arguments, even when the information is clear from context; i.e. from the target type. That's because generic type inference only takes arguments into account, not target types.

This would also make generic type inference a more helpful addition in non-method situations, as proposed for [constructor calls](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md) and [type patterns](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns.md). Specifically, [union types](https://github.com/dotnet/csharplang/blob/main/proposals/nominal-type-unions.md) and [closed classes](https://github.com/dotnet/csharplang/blob/main/proposals/closed-hierarchies.md) would benefit from this, as they are frequently a target type when case types are constructed or matched.

## Detailed design

Generic type inference currently has a [first phase](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12637-input-type-inferences) that consists of collecting "bounds" on type parameters based on comparing each parameter type with its corresponding incoming argument. A second phase then uses the collected bounds for each type parameter to infer the corresponding type argument, if possible.

This proposal adds to the first phase a facility for also collecting bounds by comparing the generic method's *return type* with the *target type* for the invocation, if one exists (new text in **bold**):

> For each of the method arguments `Eᵢ`, an input type inference ([§12.6.3.7](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12637-input-type-inferences)) is made from `Eᵢ` to the corresponding parameter type `Tᵢ`.
>
> **Additionally, if the invocation has a target type `T`, then an *upper-bound inference* ([§12.6.3.12](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#126312-upper-bound-inferences)) is made from `T` to `Tₑ`.**

Here `Tₑ`, introduced [earlier in the section](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12631-general), refers to the return type of the generic method being invoked.

Note that, unlike arguments, which generally introduce a *lower bound* on a parameter type (or an *exact bound* when the parameter is e.g. a `ref` parameter), a target type introduces an *upper bound* on the return type. Intuitively, type inference must pick type arguments to make sure that each parameter type is "big enough" for its argument to fit, while (with this proposal) the return type is "small enough" to fit the target type.

## Example

In the `MyCollection` example above, an *upper-bound inference* will be made from the target type `IEnumerable<string>` to the return type of the method, `MyCollection<T>`. Because `IEnumerable<out T>` is "covariant" in `T`, this recursively leads to an *upper-bound inference* from `string` to `T`, which puts an *upper bound* `string` on the type parameter `T` itself. When `T` is "fixed" in the second phase, `string` is the only bound on it, and becomes the inferred type argument for `T`.

## Open questions

- This is probably a breaking change! Certainly, like all improvements to type inference, it may cause new candidates to succeed, leading to ambiguity or to the new candidate to be picked. Additionally it may change what is inferred for already-successful candidates, or even thwart the inference completely. However, consider that if a target type causes such a change in the inference of a type parameter, it is likely because the current inference result - without the target type - would cause a subsequent type error in assignment to the target! So perhaps the break isn't as bad, but of course it needs to be investigated!