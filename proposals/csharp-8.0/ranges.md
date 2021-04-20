# Ranges

## Summary

This feature is about delivering two new operators that allow constructing `System.Index` and `System.Range` objects, and using them to index/slice collections at runtime.

## Overview

### Well-known types and members

To use the new syntactic forms for `System.Index` and `System.Range`, new well-known
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

The `..` syntax allows for either, both, or none of its arguments to be absent. Regardless
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
var array = new int[] { 1, 2, 3, 4, 5 };
var thirdItem = array[2];    // array[2]
var lastItem = array[^1];    // array[new Index(1, fromEnd: true)]
```

#### System.Range

C# has no syntactic way to access "ranges" or "slices" of collections. Usually users are forced to implement complex structures to filter/operate on slices of memory, or resort to LINQ methods like `list.Skip(5).Take(2)`. With the addition of `System.Span<T>` and other similar types, it becomes more important to have this kind of operation supported on a deeper level in the language/runtime, and have the interface unified.

The language will introduce a new range operator `x..y`. It is a binary infix operator that accepts two expressions. Either operand can be omitted (examples below), and they have to be convertible to `System.Index`. It will be lowered to the appropriate `System.Range` factory method call.

We replace the C# grammar rules for *multiplicative_expression* with the following (in order to introduce a new precedence level):

```antlr
range_expression
    : unary_expression
    | range_expression? '..' range_expression?
    ;

multiplicative_expression
    : range_expression
    | multiplicative_expression '*' range_expression
    | multiplicative_expression '/' range_expression
    | multiplicative_expression '%' range_expression
    ;
```

All forms of the *range operator* have the same precedence. This new precedence group is lower than the *unary operators* and higher than the *multiplicative arithmetic operators*.

We call the `..` operator the *range operator*. The built-in range operator can roughly be understood to correspond to the invocation of a built-in operator of this form:

```csharp
System.Range operator ..(Index start = 0, Index end = ^0);
```

Examples:

```csharp
var array = new int[] { 1, 2, 3, 4, 5 };
var slice1 = array[2..^3];    // array[new Range(2, new Index(3, fromEnd: true))]
var slice2 = array[..^3];     // array[Range.EndAt(new Index(3, fromEnd: true))]
var slice3 = array[2..];      // array[Range.StartAt(2)]
var slice4 = array[..];       // array[Range.All]
```

Moreover, `System.Index` should have an implicit conversion from `System.Int32`, in order to avoid the need to overload mixing integers and indexes over multi-dimensional signatures.

## Adding Index and Range support to existing library types

### Implicit Index support

The language will provide an instance indexer member with a single parameter of type `Index` for types which meet the following criteria:

- The type is Countable.
- The type has an accessible instance indexer which takes a single `int` as the argument.
- The type does not have an accessible instance indexer which takes an `Index` as the first parameter. The `Index` must be the only parameter or the remaining parameters must be optional.

A type is ***Countable*** if it  has a property named `Length` or `Count` with an accessible getter and a return type of `int`. The language can make use of this property to convert an expression of type `Index` into an `int` at the point of the expression without the need to use the type `Index` at all. In case both `Length` and `Count`
are present, `Length` will be preferred. For simplicity going forward, the proposal will use the name `Length` to represent `Count` or `Length`.

For such types, the language will act as if there is an indexer member of the form `T this[Index index]` where `T` is the return type of the `int` based indexer including any `ref` style annotations. The new member will have the same `get` and `set` members with matching accessibility as the `int` indexer.

The new indexer will be implemented by converting the argument of type `Index` into an `int` and emitting a call to the `int` based indexer. For discussion purposes, let's use the example of `receiver[expr]`. The conversion of `expr` to `int` will occur as follows:

- When the argument is of the form `^expr2` and the type of `expr2` is `int`, it will be translated to `receiver.Length - expr2`.
- Otherwise, it will be translated as `expr.GetOffset(receiver.Length)`.

This allows for developers to use the `Index` feature on existing types without the need for modification. For example:

``` csharp
List<char> list = ...;
var value = list[^1];

// Gets translated to
var value = list[list.Count - 1];
```

The `receiver` and `Length` expressions will be spilled as appropriate to ensure any side effects are only executed once. For example:

``` csharp
class Collection {
    private int[] _array = new[] { 1, 2, 3 };

    public int Length {
        get {
            Console.Write("Length ");
            return _array.Length;
        }
    }

    public int this[int index] => _array[index];
}

class SideEffect {
    Collection Get() {
        Console.Write("Get ");
        return new Collection();
    }

    void Use() {
        int i = Get()[^1];
        Console.WriteLine(i);
    }
}
```

This code will print "Get Length 3".

### Implicit Range support

The language will provide an instance indexer member with a single parameter of type `Range` for types which meet the following criteria:

- The type is Countable.
- The type has an accessible member named `Slice` which has two parameters of type `int`.
- The type does not have an instance indexer which takes a single `Range` as the first parameter. The `Range` must be the only parameter or the remaining parameters must be optional.

For such types, the language will bind as if there is an indexer member of the form `T this[Range range]` where `T` is the return type of the `Slice` method including any `ref` style annotations. The new member will also have matching accessibility with `Slice`.

When the `Range` based indexer is bound on an expression named `receiver`, it will be lowered by converting the `Range` expression into two values that are then passed to the `Slice` method. For discussion purposes, let's use the example of `receiver[expr]`.

The first argument of `Slice` will be obtained by converting the range typed expression in the following way:

- When `expr` is of the form `expr1..expr2` (where `expr2` can be omitted) and `expr1` has type `int`, then it will be emitted as `expr1`.
- When `expr` is of the form `^expr1..expr2` (where `expr2` can be omitted), then it will be emitted as `receiver.Length - expr1`.
- When `expr` is of the form `..expr2` (where `expr2` can be omitted), then it will be emitted as `0`.
- Otherwise, it will be emitted as `expr.Start.GetOffset(receiver.Length)`.

This value will be re-used in the calculation of the second `Slice` argument. When doing so it will be referred to as `start`. The second argument of `Slice` will be obtained by converting the range typed expression in the following way:

- When `expr` is of the form `expr1..expr2` (where `expr1` can be omitted) and `expr2` has type `int`, then it will be emitted as `expr2 - start`.
- When `expr` is of the form `expr1..^expr2` (where `expr1` can be omitted), then it will be emitted as `(receiver.Length - expr2) - start`.
- When `expr` is of the form `expr1..` (where `expr1` can be omitted), then it will be emitted as `receiver.Length - start`.
- Otherwise, it will be emitted as `expr.End.GetOffset(receiver.Length) - start`.

The `receiver`, `Length`, and `expr` expressions will be spilled as appropriate to ensure any side effects are only executed once. For example:

``` csharp
class Collection {
    private int[] _array = new[] { 1, 2, 3 };

    public int Length {
        get {
            Console.Write("Length ");
            return _array.Length;
        }
    }

    public int[] Slice(int start, int length) {
        var slice = new int[length];
        Array.Copy(_array, start, slice, 0, length);
        return slice;
    }
}

class SideEffect {
    Collection Get() {
        Console.Write("Get ");
        return new Collection();
    }

    void Use() {
        var array = Get()[0..2];
        Console.WriteLine(array.Length);
    }
}
```

This code will print "Get Length 2".

The language will special case the following known types:

- `string`: the method `Substring` will be used instead of `Slice`.
- `array`: the method `System.Reflection.CompilerServices.GetSubArray` will be used instead of `Slice`.

## Alternatives

The new operators (`^` and `..`) are syntactic sugar. The functionality can be implemented by explicit calls to `System.Index` and `System.Range` factory methods, but it will result in a lot more boilerplate code, and the experience will be unintuitive.

## IL Representation

These two operators will be lowered to regular indexer/method calls, with no change in subsequent compiler layers.

## Runtime behavior

- Compiler can optimize indexers for built-in types like arrays and strings, and lower the indexing to the appropriate existing methods.
- `System.Index` will throw if constructed with a negative value.
- `^0` does not throw, but it translates to the length of the collection/enumerable it is supplied to.
- `Range.All` is semantically equivalent to `0..^0`, and can be deconstructed to these indices.

## Considerations

### Detect Indexable based on ICollection

The inspiration for this behavior was collection initializers. Using the structure of a type to convey that it had opted into a feature. In the case of collection initializers types can opt into the feature by implementing the interface `IEnumerable` (non generic).

This proposal initially required that types implement `ICollection` in order to qualify as Indexable. That required a number of special cases though:

- `ref struct`: these cannot implement interfaces yet types like `Span<T>` are ideal for index / range support.
- `string`: does not implement `ICollection` and adding that `interface` has a large cost.

This means to support key types special casing is already needed. The special casing of `string` is less interesting as the language does this in other areas (`foreach` lowering, constants, etc ...). The special casing of `ref struct` is more concerning as it's special casing an entire class of types. They get labeled as Indexable if they simply have a property named `Count` with a return type of `int`.

After consideration the design was normalized to say that any type which has a property `Count` / `Length` with a return type of `int` is Indexable. That removes all special casing, even for `string` and arrays.

### Detect just Count

Detecting on the property names `Count` or `Length` does complicate the design a bit. Picking just one to standardize though is not sufficient as it ends up excluding a large number of types:

- Use `Length`: excludes pretty much every collection in System.Collections and sub-namespaces. Those tend to derive from `ICollection` and hence prefer `Count` over length.
- Use `Count`: excludes `string`, arrays, `Span<T>` and most `ref struct` based types

The extra complication on the initial detection of Indexable types is outweighed by its simplification in other aspects.

### Choice of Slice as a name

The name `Slice` was chosen as it's the de-facto standard name for slice style operations in .NET. Starting with netcoreapp2.1 all span style types use the name `Slice` for slicing operations. Prior to netcoreapp2.1 there really aren't any examples of slicing to look to for an example. Types like `List<T>`, `ArraySegment<T>`, `SortedList<T>` would've been ideal for slicing but the concept didn't exist when types were added.

Thus, `Slice` being the sole example, it was chosen as the name.

### Index target type conversion

Another way to view the `Index` transformation in an indexer expression is as a target type conversion. Instead of binding as if there is a member of the form `return_type this[Index]`, the language instead assigns a target typed conversion to `int`.

This concept could be generalized to all member access on Countable types. Whenever an expression with type `Index` is used as an argument to an instance member invocation and the receiver is Countable then the expression will have a target type conversion to `int`. The member invocations applicable for this conversion include methods, indexers, properties, extension methods, etc ... Only constructors are excluded as they have no receiver.

The target type conversion will be implemented as follows for any expression which has a type of `Index`. For discussion purposes lets use the example of `receiver[expr]`:

- When `expr` is of the form `^expr2` and the type of `expr2` is `int`, it will be translated to `receiver.Length - expr2`.
- Otherwise, it will be translated as `expr.GetOffset(receiver.Length)`.

The `receiver` and `Length` expressions will be spilled as appropriate to ensure any side effects are only executed once. For example:

``` csharp
class Collection {
    private int[] _array = new[] { 1, 2, 3 };

    public int Length {
        get {
            Console.Write("Length ");
            return _array.Length;
        }
    }

    public int GetAt(int index) => _array[index];
}

class SideEffect {
    Collection Get() {
        Console.Write("Get ");
        return new Collection();
    }

    void Use() {
        int i = Get().GetAt(^1);
        Console.WriteLine(i);
    }
}
```

This code will print "Get Length 3".

This feature would be beneficial to any member which had a parameter that represented an index. For example `List<T>.InsertAt`. This also has the potential for confusion as the language can't give any guidance as to whether or not an expression is meant for indexing. All it can do is convert any `Index` expression to `int` when invoking a member on a Countable type.

Restrictions:

- This conversion is only applicable when the expression with type `Index` is directly an argument to the member. It would not apply to any nested expressions.

## Decisions made during implementation

- All members in the pattern must be instance members
- If a Length method is found but it has the wrong return type, continue looking for Count
- The indexer used for the Index pattern must have exactly one int parameter
- The Slice method used for the Range pattern must have exactly two int parameters
- When looking for the pattern members, we look for original definitions, not constructed members

## Design meetings

- [Jan 10, 2018](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-01-10.md)
- [Jan 18, 2018](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-01-18.md)
- [Jan 22, 2018](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-01-22.md)
- [Dec 3, 2018](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-12-03.md)
- [Mar 25, 2019](https://github.com/dotnet/csharplang/blob/master/meetings/2019/LDM-2019-03-25.md#pattern-based-indexing-with-index-and-range)
- [April 1st, 2019](https://github.com/dotnet/csharplang/blob/master/meetings/2019/LDM-2019-04-01.md)
- [April 15, 2019](https://github.com/dotnet/csharplang/blob/master/meetings/2019/LDM-2019-04-15.md#follow-up-decisions-for-pattern-based-indexrange)
