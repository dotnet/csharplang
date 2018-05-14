# Ranges

## Summary

This feature is about delivering two new operators that allow constructing `System.Index` and `System.Range` objects, and using them to index/slice collections at runtime.

## Detailed Design

#### System.Index

C# has no way of indexing a collection from the end, but rather most indexers use the "from start" notion, or do a "length - i" expression. We introduce a new Index expression that means "from the end". The feature will introduce a new unary prefix "hat" operator. Its single operand must be convertible to `System.Int32`. It will be lowered into the appropriate `System.Index` factory method call.

```csharp
var thirdItem = list[2];                // list[2]
var lastItem = list[^1];                // list[Index.CreateFromEnd(1)]

var multiDimensional = list[3, ^2]      // list[3, Index.CreateFromEnd(2)]
```

#### System.Range

C# has no syntactic way to access "ranges" or "slices" of collections. Usually users are forced to implement complex structures to filter/operate on slices of memory, or resort to LINQ methods like `list.Skip(5).Take(2)`. With the addition of `System.Span<T>` and other similar types, it becomes more important to have this kind of operation supported on a deeper level in the language/runtime, and have the interface unified.

The language will introduce a new range operator `x..y`. It is a binary infix operator that accepts two expressions. Either operands can be omitted (examples below), and they have to be convertible to `System.Index`. It will be lowered to the appropriate `System.Range` factory method call.

```csharp
var slice1 = list[2..^3];               // list[Range.Create(2, Index.CreateFromEnd(3))]
var slice2 = list[..^3];                // list[Range.ToEnd(Index.CreateFromEnd(3))]
var slice3 = list[2..];                 // list[Range.FromStart(2)]
var slice4 = list[..];                  // list[Range.All]

var multiDimensional = list[1..2, ..]   // list[Range.Create(1, 2), Range.All]
```

Moreover, `System.Index` should have an implicit conversion from `System.Int32`, in order not to need to overload for mixing integers and indexes over multi-dimensional signatures.

## Workarounds

For prototyping reasons, and since runtime/framework collections will not have support for such indexers, the compiler will finally look for the following extension method when doing overload resolution:

* `op_Indexer_Extension(this TCollection<TItem> collection, ...arguments supplied to the indexer)`

This workaround will be removed once contract with runtime/framework is finalized, and before the feature is declared complete.

## Alternatives

The new operators (`^` and `..`) are syntactic sugar. The functionality can be implemented by explicit calls to `System.Index` and `System.Range` factory methods, but it will result in a lot more boilerplate code, and the experience will be unintuitive.

## IL Representation

These two operators will be lowered to regular indexer/method calls, with no change in subsequent compiler layers.

## Runtime behaviour

* Compiler can optimize indexers for built-in types like arrays and strings, and lower the indexing to the appropriate existing methods.
* `System.Index` will throw if constructed with a negative value.
* `^0` does not throw, but it translates to the length of the collection/enumerable it is supplied to.
* `Range.All` is semantically equivalent to `0..^0`, and can be deconstructed to these indices.

## Questions
