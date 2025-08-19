# Target-typed generic type inference

## Summary

Generic type inference may take a target type into account. For instance, given:

```csharp
public class MyCollection
{
    public static MyCollection<T> Create<T>() { ... }
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

## Implementation considerations

The point of this section is to argue that the proposal is probably not expensive from an implementation point of view. The argument is somewhat gnarly, and can be safely skipped for the purposes of just understanding the proposal at the language level.

The type inference machinery required for this feature is all already there in the compiler. To see this, we can systematically "map" examples such as `MyCollection<T>` to code for which the inference works today. 

We're going to rewrite the `Create` method - the one that we want to do type inference for - into one that doesn't return its result, but instead passes it to a `Receptacle<T>` delegate: an extra optional parameter that can receive the result:

```csharp
    //public static MyCollection<T> Create<T>() { ... ; return result; }
    public static void Create<T>(Receptacle<MyCollection<T>>? use = null!) { ... ; use?.Invoke(result); }
```

`Receptacle<T>` is a delegate type that is *contravariant* in `T`:

```csharp
public delegate void Receptacle<in T>(T value);
```

(Incidentally, `Receptacle<T>` is identical to `Action<T>`.)

At the point of inference, instead of assigning the result of `Create` to the fresh variable `c`, we need to create a `Receptacle` for the variable:

```csharp
// IEnumerable<string> c = MyCollection.Create();
IEnumerable<string> c;
Receptacle<IEnumerable<string>> set_c = value => c = value;
```

Now we're ready to call our modified `Create` method, passing in the `Receptacle` for `c`:

```csharp
MyCollection.Create(set_c); // 'T' = 'string' inferred from receptacle type
```

Type inference succeeds with `T` = `string`!

To see that this is isomorphic to the inference in the proposed feature, let's follow type inference through the first couple of steps:

1. The [first phase](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12632-the-first-phase) performs an *input type inference* from the argument `set_c` to the parameter type `Receptacle<MyCollection<T>>`.
2. The [input type inference](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12637-input-type-inferences) performs a *lower-bound inference* from `set_c`'s type `Receptacle<IEnumerable<string>>` to the parameter type `Receptacle<MyCollection<T>>`.
3. The [lower-bound inference](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#126311-lower-bound-inferences) matches up the `Receptacle<...>` on each side, and, because the type parameter of `Receptacle` is contravariant, performs an *upper-bound inference* from `IEnumerable<string>` to `MyCollection<T>`.

And now we're at the point where this proposal begins: Performing an *upper-bound inference* from the target type to the return type. 

In other words, the existence of contravariant type parameters in C# and their ability to "flip the sign" in type inference is how this expressiveness is already there. In fact, an alternative and equivalent (but more convoluted) way of specifying the proposal would be through such a mapping of the code in terms of the existing type inference machinery.
