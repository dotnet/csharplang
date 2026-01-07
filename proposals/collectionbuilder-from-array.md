# Allow arrays as CollectionBuilder Create parameter type

Champion issue: TBD

## Summary

Types may now opt into support for collection expressions by using CollectionBuilderAttribute with a Create method that takes an array rather than a readonly span of the elements.

This will enable types such as `ReadOnlyMemory<T>` and `ArraySegment<T>` to support collection expressions without incurring the performance penalty of copying from a span to a second array to use as their backing storage.

```cs
[CollectionBuilder(typeof(ReadOnlyMemory), nameof(ReadOnlyMemory.Create))]
public readonly struct ReadOnlyMemory<T> : IEquatable<ReadOnlyMemory<T>> { ... }

// (Or MemoryMarshal, or some other holder)
public static class ReadOnlyMemory
{
    // Now allowed: T[] instead of ReadOnlySpan<T> parameter
    public static ReadOnlyMemory<T> Create<T>(T[] array) => array;
}
```

## Motivation

This proposal addresses two pain points with core types, while also opening the door for collection expressions to be used efficiently with user-defined array wrapper types.

One pain point is an unfortunate collision between collection expressions and `ArraySegment<T>`: reports have been regularly coming in that `ArraySegment<T> x = [];` compiles without errors or warnings but produces a default instance which throws NullReferenceException when interacted with, rather than the empty collection that was intended. If the collection expression is not empty, the code fails to compile.

The other pain point is that collection expressions do not support `Memory<T>` and `ReadOnlyMemory<T>`. `ReadOnlyMemory<T>` appears in common APIs such as [`Stream.WriteAsync`](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream.writeasync):

```cs
await stream.WriteAsync([.. Header, .. message], cancellationToken);
```

`Memory<T>` and `ReadOnlyMemory<T>` are fundamental types in the BCL. They parallel the `Span<T>` and `ReadOnlySpan<T>` types and perform the same roles as those types whenever the data must live on the heap, such as in async code or callbacks. It would bring consistency to API consumption for collection expressions to work for the heap-enabled "memory window" types as well as for the stack-only "memory window" types.

Without this feature, the only way for these core types or user-defined array wrapper types to support collection expressions is by using a CollectionBuilder Create method with a `ReadOnlySpan<T>` parameter. The `ReadOnlySpan<T>` parameter forces a copy of all elements from this span to a newly allocated backing array for the collection type. This is despite the fact that the compiler can already efficiently produce arrays for collection expressions, and ironically, the `ReadOnlySpan<T>` may be pointing to an array that the compiler allocated for that collection expression. The Create method cannot assume that the caller is the compiler. Thus, it cannot assume that it can use [`MemoryMarshal.TryGetArray`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.memorymarshal.trygetarray) to avoid the performance hit of a full copy when setting its internal array.

## Detailed design

The *[create method](csharp-12.0/collection-expressions.md#create-methods)* is now permitted to have a single parameter of either `System.ReadOnlySpan<E>` or `E[]`. If the parameter type is `E[]`, the compiler will construct the collection expression as though targeting the `E[]` type directly, then pass the constructed array to the *create method*.

If there are two *create methods* which are identical except that one takes `System.ReadOnlySpan<E>` and the other takes `E[]`, the collection type will not have a *create method*. This falls out of existing text in the *[create methods](csharp-12.0/collection-expressions.md#create-methods)* section:

> If only one method among those in the `CM` set has an [*identity conversion*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#1022-identity-conversion) from `E` to the *element type* of the *collection type*, that is the *create method* for the *collection type*. Otherwise, the *collection type* doesn't have a *create method*.

## Specification

Insertions are in **bold**.

The *[Create methods](csharp-12.0/collection-expressions.md#create-methods)* section of the collection expressions spec is updated:

> The method must have a single parameter of type `System.ReadOnlySpan<E>` **or `E[]`**, passed by value.

The *[Construction](csharp-12.0/collection-expressions.md#construction)* section of the collection expressions spec is updated:

> * An *initialization instance* is created as follows:
>   * If the target type is an *array* **or has a *create method* taking an *array*,** the collection expression has a *known length*, an array is allocated with the expected length.

And:

> * If the target type has a *create method*, the create method is invoked with the span **or array** instance.

## Alternatives

### Use implicit conversions from array

Instead of supporting `T[]` in the Create method, the collection expressions feature could be given a rule that any type without CollectionBuilderAttribute which has an implicit conversion from an array type can be targeted by a collection expression. The compiler would build an array and call the implicit conversion operator instead of calling a CollectionBuilder Create method. An upside of this approach is that it would remove the requirement for the BCL to add public Create methods for these types. A downside is that a user-defined type may wish to be efficiently targeted by a collection expression without having an implicit conversion from array, so the solution is less general.

### Do nothing except warn when using `[]` with `ArraySegment<T>`

This has turned out to be a common enough pitfall that some warning would save users time in tracking down the inevitable exception when the internal array is dereferenced possibly much later in the program, possibly not even in the user's own code. One option could be to ship an analyzer in the runtime so that the compiler does not have special knowledge of `ArraySegment<T>`.

## Future expansions

It may also become useful to support `IEnumerable<T>` as an additional option for the values parameter of the Create method. This would allow the existing [`ImmutableDictionary.CreateRange`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutabledictionary.createrange) BCL method to be used directly by the CollectionBuilder attribute, rather than requiring the runtime to add a new `ReadOnlySpan<KeyValuePair<TKey, TValue>>` overload of that method.
