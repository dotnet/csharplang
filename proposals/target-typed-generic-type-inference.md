# Target-typed generic type inference

## Summary

Generic type inference may take a target type into account. For instance, given:

```csharp
public class MyCollection
{
    public MyCollection<T> Create<T>() { ... }
}
```

We would allow the `Create` method to be called without type argument when it can be inferred from a target type:

```csharp
MyCollection<string> c = MyCollection.Create(); // 'T' = 'string' inferred from target type
```

## Motivation

Generic factory methods often need explicit type arguments, even when the information is clear from context; i.e. from the target type. That's because generic type inference only takes arguments into account, not target types.

If we were to add generic type inference in non-method situations, such as constructor calls or type patterns, target typing would be particularly useful in those scenarios.

## Detailed design

We add the following to [the first phase of type inference](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12632-the-first-phase):

> Additionally, if the invocation has a target type `T`, then an *upper bound inference* ([§12.6.3.11](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#126311-upper-bound-inferences)) is made from `T` to `Tₑ`.

(Here `Tₑ` refers to the return type of the generic method being invoked.)

Note that, unlike arguments, which introduce a *lower bound* on a parameter type, a target type introduces an *upper bound* on the return type.

## Example

In the `MyCollection` example above, an upper bound inference will be made from the target type `MyCollection<string>` to the return type of the method, `MyCollection<T>`. That would lead recursively to an exact inference from `string` to `T`, which would put an exact bound `string` on `T`. When `T` is "fixed", it would thus be inferred to be `string`.

## Open questions

- This is probably a breaking change! Certainly, like all improvements to type inference, it may cause new candidates to succeed, leading to ambiguity or to the new candidate to be picked. Additionally it may change what is inferred for already-successful candidates, or even thwart the inference completely. However, consider that if a target type causes such a change in the inference of a type parameter, it is likely because the current inference result - without the target type - would cause a subsequent type error in assignment to the target! So perhaps the break isn't as bad, but of course it needs to be investigated!