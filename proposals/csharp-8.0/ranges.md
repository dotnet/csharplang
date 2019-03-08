# Ranges

## Summary

This feature is about delivering two new operators that allow constructing `System.Index` and `System.Range` objects, and using them to index/slice collections at runtime.

## Overview

### Well-known types and members

To use the new syntactic forms for System.Index and System.Range, new well-known
types and members may be necessary, depending on which syntactic forms are used.

To use the "hat" operator (`^`), the following is required

```csharp
namespace System
{
    public readonly struct Index
    {
        public Index(int value, bool fromEnd);
    }
}
```

To use the `System.Index` type as an argument in an array element access, the following
member is required:

```csharp
int System.Index.GetOffset(int length);
```

The `..` syntax for `System.Range` will require the `System.Range` type, as well as one
or more of the following members:

```csharp
namespace System
{
    public readonly struct Range
    {
        public Range(System.Index start, System.Index end);
        public static Range StartAt(System.Index start);
        public static Range EndAt(System.Index end);
        public static Range All { get; }
    }
}
```

The `..` syntax allows for either, both, or none of it's arguments to be absent. Regardless
of the number of arguments, the `Range` constructor is always sufficient for using the
`Range` syntax. However, if any of the other members are present and one or more of the
`..` arguments are missing, the appropriate member may be substituted.

Finally, for a value of type `System.Range` to be used in an array element access expression,
the following member must be present:

```csharp
namespace System.Runtime.CompilerServices
{
    public static class RuntimeHelpers
    {
        public static T[] GetSubArray<T>(T[] array, System.Range range);
    }
}
```

### System.Index

C# has no way of indexing a collection from the end, but rather most indexers use the "from start" notion, or do a "length - i" expression. We introduce a new Index expression that means "from the end". The feature will introduce a new unary prefix "hat" operator. Its single operand must be convertible to `System.Int32`. It will be lowered into the appropriate `System.Index` factory method call.

We augment the grammar for *unary_expression* with the following additional syntax form:

```antlr
unary_expression
    : '^' unary_expression
    ;
```

We call this the *index from end* operator. The predefined *index from end* operators are as follows:

```csharp
    System.Index operator ^(int fromEnd);
```

The behavior of this operator is only defined for input values greater than or equal to zero.

Examples:

```csharp
var thirdItem = list[2];                // list[2]
var lastItem = list[^1];                // list[Index.CreateFromEnd(1)]

var multiDimensional = list[3, ^2]      // list[3, Index.CreateFromEnd(2)]
```

#### System.Range

C# has no syntactic way to access "ranges" or "slices" of collections. Usually users are forced to implement complex structures to filter/operate on slices of memory, or resort to LINQ methods like `list.Skip(5).Take(2)`. With the addition of `System.Span<T>` and other similar types, it becomes more important to have this kind of operation supported on a deeper level in the language/runtime, and have the interface unified.

The language will introduce a new range operator `x..y`. It is a binary infix operator that accepts two expressions. Either operand can be omitted (examples below), and they have to be convertible to `System.Index`. It will be lowered to the appropriate `System.Range` factory method call.

We replace the C# grammar rules for *shift_expression* with the following (in order to introduce a new precedence level):

```antlr
shift_expression
    : range_expression
    | shift_expression '<<' range_expression
    | shift_expression right_shift range_expression
    ;

range_expression
    : additive_expression
    | range_expression? '..' additive_expression?
    ;
```

We call the `..` operator the *range operator*. The built-in range operator can roughly be understood to correspond to the invocation of a built-in operator of this form:

```csharp
    System.Range operator ..(Index start = 0, Index end = ^0);
```

Examples:

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

## Runtime behavior

* Compiler can optimize indexers for built-in types like arrays and strings, and lower the indexing to the appropriate existing methods.
* `System.Index` will throw if constructed with a negative value.
* `^0` does not throw, but it translates to the length of the collection/enumerable it is supplied to.
* `Range.All` is semantically equivalent to `0..^0`, and can be deconstructed to these indices.

## Questions
