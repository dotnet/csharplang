# Index and Range Changes

## Summary
This proposes to make several changes to the 
[index and range design](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md) based on 
customer feedback: particularly from the CoreFX team and their experiences adding Index / Range support to .NET Core.
The change is not to the syntax but rather how the langauge maps the syntax to APIs.

## Motivation
The level of API churn necessary to adopt `Index` and `Range` is quite high today. Every collection type must have at
least two new indexers added for `Index` and `Range` respectively. The implementation of `Index` is exactly the same 
for virtually every collection in .NET. The implementation for `Range` is likewise often similar by simply deferring to 
the underlying collection type.

This work must be done by the collection author. The reliance on indexers as the primary use case means developers can't
update existing collections using extension methods. This means the adoption of the feature will be slowed by waiting
for library authors to update their types.

The use of `Index` does come with a small performance hit. Typically a consumer of `Index` will pay an extra branch
check (`IsFromEnd` and bounds) as they would with an `int` parameter (bounds only). Moving to `Range` doubles this cost
as it occurs on each `Index`.

This penalty is small and insignificant in the vast majority of code. For highly optimized code though, this extra 
branching can be unacceptable. It simply can't be thought of as a syntactic transformation from a call to 
`span.Slice(start, end)` to `span[start..end]`. Instead, the performance implications must be evaluated for each
usage. This will likely lead to the feature being banned in certain portions of code bases.

This proposals seeks to remove the abstraction penalties around `Index` and `Range`: both at an API and performance 
layer. The goal being that most collection types just work today with minimal change and the feature can be adopted
in existing code without fear of lost performance.

This proposal specifically does not want to change how `Index` and `Range` type binding occurs. The index and range
expressions continue to have the same syntax and types.

## Detailed Design 
### Countable Types
Any type which has a property named `Length` or `Count` with an accessible getter and a return type of `int` is 
considered Countable. The language can make use of this property to convert an expression of type `Index` into an `int`
at the point of the expression without the need to use the type `Index` at all. In case both `Length` and `Count`
are present, `Length` will be preferred. 

For simplicity going forward, the proposal will use the name `Length` to represent `Count` or `Length`.

### Index and Range implementations are known
The implementations of `Index` and `Range` are considered to be known and side effect free. Much like 
`ValueTuple<T1, T2>`, the language can assume a standard implementation and emit code inline which represents the 
implementation of methods on `Index` and `Range`. This implementation includes methods like `GetOffset` or conversions
like the implicit conversion from `int` to `Index`.

All arithmetic operations will be emitted using an `unchecked` context. That matches the context
in which `Index` and `Range` are compiled in.

### Implicit Index support
The language will provide an instance indexer member with a single parameter of type `Index` for types which meet the 
following criteria:

- The type is Countable.
- The type has an accessible instance indexer which takes a single `int` as the argument.
- The type does not have an accessible instance indexer which takes an `Index` as the first parameter. The `Index` 
must be the only parameter or the remaining parameters must be optional.

For such types, the language will act as if there is an index member of the form `T this[Index index]` where `T` is 
the return type of the `int` based indexer including any `ref` style annotations. The new member will have the same 
`get` and `set` members with matching accessibilty as the `int` indexer. 

The new indexer will be implemented by converting the argument of type `Index` into an `int` and emitting a call 
to the `int` based indexer. For discussion purposes, lets use the example of `receiver[expr]`. The conversion of 
`expr` to `int` will occur as follows:

- When the argument is of the form `^expr2` and the type of `expr2` is `int`, it will be translated to 
`receiver.Length - expr2`.
- Otherwise, it will be translated as `expr.GetOffset(receiver.Length)`.

This allows for developers to use the `Index` feature on existing types without the need for modification. For example:

``` csharp
List<char> list = ...;
var value = list[^1]; 

// Gets translated to 
var value = list[list.Count - 1]; 
```

The `receiver` and `Length` expressions will be spilled as appropriate to ensure any side effects are only executed 
once. For example:

``` csharp
class Collection {
    private int[] _array = new[] { 1, 2, 3 };

    int Length {
        get {
            Console.Write("Length ");
            return _array.Length;
        }
    }

    int this[int index] => _array[index];
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
The language will provide an instance indexer member with a single parameter of type `Range` for types which meet the 
following criteria:

- The type is Countable.
- The type has an accessible member named `Slice` which has two parameters of type `int`.
- The type does not have an instance indexer which takes a single `Range` as the first parameter. The `Range` 
must be the only parameter or the remaining parameters must be optional.

For such types, the language will bind as if there is an index member of the form `T this[Range range]` where `T` is 
the return type of the `Slice` method including any `ref` style annotations. The new member will also have matching
accessibility with `Slice`. 

When the `Range` based indexer is bound on an expression named `receiver`, it will be lowered by converting the `Range` 
expression into two values that are then passed to the `Slice` method. For discussion purposes, lets use the example of 
`receiver[expr]`.

The first argument of `Slice` will be obtained by converting the typed expression in the following way:

- When `expr` is of the form `expr1..expr2` (where `expr2` can be omitted) and `expr1` has type `int`, then it will be
emitted as `expr1`.
- When `expr` is of the form `^expr1..expr2` (where `expr2` can be omitted), then it will be emitted as 
`receiver.Length - expr1`.
- When `expr` is of the form `..expr2` (where `expr2` can be omitted), then it will be emitted as `0`.
- Otherwise, it will be emitted as `expr.Start.GetOffset(receiver.Length)`.

This value will be re-used in the calculation of the second `Slice` argument. When doing so it will be referred to as
`start`. The second argument of `Slice` will be obtained by converting the range typed expression in the following way:

- When `expr` is of the form `expr1..expr2` (where `expr1` can be omitted) and `expr2` has type `int`, then it will be 
emitted as `expr2 - start`.
- When `expr` is of the form `expr1..^expr2` (where `expr1` can be omitted), then it will be emitted as 
`(receiver.Length - expr2) - start`.
- When `expr` is of the form `expr1..` (where `expr1` can be omitted), then it will be emitted as `receiver.Length`.
- Otherwise, it will be emitted as `expr.End.GetOffset(receiver.Length) - start`.

The `receiver`, `Length` and `expr` expressions will be spilled as appropriate to ensure any side effects are only
executed once. For example:

``` csharp
class Collection {
    private int[] _array = new[] { 1, 2, 3 };

    int Length {
        get {
            Console.Write("Length ");
            return _array.Length;
        }
    }

    int[] Slice(int start, int length) { 
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
        Console.WriteLine(array.length);
    }
}
```

This code will print "Get Length 2". 

The language will special case the following known types: 

- `string`: the method `Substring` will be used instead of `Slice`.
- `array`: the method `System.Reflection.CompilerServices.GetSubArray` will be used instead of `Slice`.

## Open Issues

### Declared vs. Instantiated type

## Considerations

### Detect Indexable based on ICollection
The inspiration for this proposal was collection initializers. Using the structure of a type to convey that it had
opted into a feature. In the case of collection initializers types can opt into the feature by implementing the 
interface `IEnumerable` (non generic).

This proposal initially required that types implement `ICollection` in order to qualify as Indexable. That required a number of special cases though:

- `ref struct`: these cannot implement interfaces yet types like `Span<T>` are ideal for index / range support. 
- `string`: does not implement `ICollection` and adding that `interface` has a large cost.

This means to support key types special casing is already needed. The special casing of `string` is less interesting 
as the language does this in other areas (`foreach` lowering, constants, etc ...). The special casing of `ref struct`
is more concerning as it's special casing an entire class of types. They get labeled as Indexable if they simply have
a property named `Count` with a return type of `int`. 

After consideration the design was normalized to say that any type which has a property `Count` / `Length` with a 
return type of `int` is Indexable. That removes all special casing, even for `string` and arrays.

### Detect just Count
Detecting on the property names `Count` or `Length` does complicate the design a bit. Picking just one to standardize
though is not sufficient as it ends up excluding a large number of types:

- Use `Length`: excludes pretty much every collection in System.Collections and sub-namespaces. Those tend to derive 
from `ICollection` and hence prefer `Count` over length.
- Use `Count`: excludes `string`, arrays, `Span<T>` and most `ref struct` based types

The extra complication on the initial detection of Indexable types is outweighed by its simplification in other 
aspects.

### Choice of Slice as a name
The name `Slice` was chosen as it's the de-facto standard name for slice style operations in .NET. Starting with 
netcoreapp2.1 all span style types use the name `Slice` for slicing operations. Prior to netcoreapp2.1 there really
aren't any examples of slicing to look to for an example. Types like `List<T>`, `ArraySegment<T>`, `SortedList<T>`
would've been ideal for slicing but the concept didn't exist when types were added. 

Thus, `Slice` being the sole example, it was chosen as the name.

### Index target type conversion
Another way to view the `Index` transformation in an indexer expression is as a target type conversion. Instead
of binding as if there is a member of the form `return_type this[Index]`, the language instead assigns a target
typed conversion to `int`. 

This concept could be generalized to all member access on Countable types. Whenever an expression with type `Index` is 
used as an argument to an instance member invocation and the receiver is Countable then the expression will have a 
target type conversion to `int`. The member invocations applicable for this conversion include methods, indexers, 
properties, extension methods, etc ... Only constructors are excluded as they have no receiver. 

The target type conversion will be implemented as follows for any expression which has a type of `Index`. For
discussion purposes lets use the example of `receiver[expr]`:

- When `expr` is of the form `^expr2` and the type of `expr2` is `int`, it will be translated to 
`receiver.Length - expr2`.
- Otherwise, it will be translated as `expr.GetOffset(receiver.Length)`.

The `receiver` and `Length` expressions will be spilled as appropriate to ensure any side effects are only executed
once. For example:

``` csharp
class Collection {
    private int[] _array = new[] { 1, 2, 3 };

    int Length {
        get {
            Console.Write("Length ");
            return _array.Length;
        }
    }

    int GetAt(int index) => _array[index];
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

This feature would be beneficial to any member which had a parameter that represented an index. For example 
`List<T>.InsertAt`. This also has the potential for confusion as the language can't give any guidance as to whether
or not an expression is meant for indexing. All it can do is convert any `Index` expression to `int` when invoking
a member on a Countable type. 

Restrictions:

- This conversion is only applicable when the expression with type `Index` is directly an argument to the member. It 
would not apply to any nested expressions.

## Related Issues
- https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.cs
- https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md

## Design Meetings
- https://github.com/dotnet/csharplang/blob/master/meetings/2019/LDM-2019-04-01.md
