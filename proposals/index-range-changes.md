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
check (`IsFromEnd` and bounds) as they would with a `int` parameter (bounds only). Moving to `Range` doubles this cost
as it occurs on each `Index`.

This penalty is small and insignificant in the vast majority of code. For highly optimized code though this extra 
branching can be unacceptable. It simply can't be thought of as a syntactic transformation from a call to 
`span.Slice(start, end)` to `span[start..end]`. Instead the performance implications must be evaluated on each
usage. This will likely lead to the feature being banned in certain portions of code bases.

This proposals seeks to remove the abstraction penalties around `Index` and `Range`: both at an API and performance 
layer. The goal being that most collection types just work today with minimal change and the feature can be adopted
in existing code without fear of lost performance.

This proposal specifically does not want to change how `Index` and `Range` type binding occurs. The index and range
expressions continue to have the same syntax and types.

## Detailed Design 
### Indexable types
Any type which has an accessible getter property named `Length` or `Count` with a return type of `int` is considered
Indexable. The language can make use of this property to convert an index expression into an `int` at the point of 
the expression without the need to use the type `Index` at all. 

Note: For simplicity going forward the proposal will use the name `Length` to represent `Count` or `Length`.

For example it allows the following simplication:

``` csharp
Span<char> span = ...;
char c = span[^1];

// Can be translated to 
Span<char> span = ...;
char c = span[span.Length - 1];
```

Transforming an index expression to an `int` at the call site significantly reduces the burden of frameworks to adopt 
`Index`. Vitrually any collection type will automatically work with `Index` now as the compiler can translate it to 
`int` in all cases.

Further this can improve performance by eliminating extra branching. The callee when accepting an `Index` parameter must
do both test to see if the value is from the end, `Index.IsFromEnd`, and if the value is inside the bounds of the 
collection. While a small check this can be important in performance sensitive areas.

Doing the translation to `int` at the call site means the `IsFromEnd` check can often be eliminated. For example when 
dealing with an `int` the compiler can pass the value through. Or in the cases where `^` is used the computation from
end can be done directly without the additional branching.

### Index and Range implementations are known
The implementations of `Index` and `Range` are considered to be known and side effect free. Much like 
`ValueTuple<T1, T2>` the language can assume a standard implementation and emit code inline which represents the 
implementation of methods on `Index` and `Range`. This implementation includes methods like `GetOffset` or conversions
like the implicit conversion from `int` to `Index`.

All arithemtic operations which are emitted will be done so using an `unchecked` context. That matches the context
in which `Index` and `Range` are compiled in.

### Index target type conversion
Whenever an expression with type `Index` is used as an argument to an instance member invocation and the receiver is 
Indexable then the expression will have a target type conversion to `int`. The member invocations applicable for this
conversion include methods, indexers, properties, extension methods, etc ... Only constructors are excluded as they
have no receiver. 

The target type conversion will be implemented as follows on the index expression:

- When the expression is `^expr` and the type is `int` it will be translated to `receiver.Length - expr`.
- Else it will be translated as `expr.GetOffset(receiver.Length)` where `expr` is the expression typed as `Index`.

The receiver will be spilled as appropriate to ensure he side effects of obtaining the receiver are only executed
once. For example:

``` csharp
class SideEffect {
    int[] Get() {
        Console.Write("Get ");
        return new [] { 1, 2 , 3};
    }

    void Use() { 
        int i = Get()[^1]
        Console.WriteLine(i);
    }
}
```

This code will print "Get 3". 

When a range expression is used as an argument to an instance member invocation then the target type conversion to 
`int` extends to both `Index` operands. In the case either of the `Index` members are omitted then the the appropriate
start or end value will be inserted using `0` or `receiver.Length` as appropriate.

``` csharp
class RangeTargettype {
    void Example() { 
        var array = new[] { 1, 2, 3 };
        Console.WriteLine(array[1..]);

        // Becomes
        Console.WriteLine(array[new Range(1, array.Length));
    }
}
```

### Indexing on Range
When binding an index member on a Indexable type where the single argument is of type `Range` the language will 
attempt to translate it to a `Slice` call. The arguments to slice will be both `Index` values of the range converted
to `Index` using their target typed conversion described in the previous section. If this translation is not succesful
then normal index binding will occur.

``` csharp
class Collection {
    public int Length { get; }
    public int[] this[Range range] => ...;
}

class Slice {
    void Example(Span<int> span, Collection collection) {
        Span<int> slicedSpan = span[2..]
        int[] slicedCollection = collection[2..];

        // Translated to 
        Span<int> slicedSpan = span.Slice(2, span.Length);
        int[] slicedCollection = collection[new Range(2, collection.Count)v;
    }
}
```

The `Slice` method can instance or extension so long as it is accessible has types that are convertible from `int`.

The compiler will special case the following receiver types binding to `Slice`:

- `string`: instead of `Slice` the method `Substring` will be used. 
- array: the runtime helper for array slicing will be used.

## Open Issues


## Considerations

### Detect Indexable based on ICollection
The inspiration for this proposal was collection initializers. Using the structure of a type to convey that it had
opted into a feature. In the case of collection initializers types can opt into the feature by implementing the 
interface `IEnumerable` (non generic).

Initially this proposal required that types implement `ICollection` in order to qualify as Indexable. That though
required a number of special cases:

- `ref struct`: these cannot implement interfaces yet types like `Span<T>` are ideal for index / range support. 
- `string`: does not implement `ICollection` and adding that `interface` has a large cost.

This means to support key types special casing is already needed. The special casing of `string` is less interesting 
as the language does this in other areas (`foreach` lowering, constants, etc ...). The special casing of `ref struct`
is more concerning as it's special casing an entire class of types. The get labeled as Indexable if they simply have
a property named `Count` with a return type of `int`. 

After consideration the design was normalized to say that any type which has a property `Count` / `Length` with a 
return type of `int` is Indexable. That removes all special casing, even for `string` and arrays.

### Detect just Count
Detecting on the property names `Count` or `Length` does complicate the design a bit. Picking just one to standardize
though is not sufficient as it ends up excluding a large number of types:

- Use `Length`: excludes pretty much every collection in System.Collections and sub-namespaces. Those tend to derive 
from `ICollection` and hence prefer `Count` over length.
- Use `Count`: excludes `string`, arrays, `Span<T>` and most `ref struct` based types

The extra complication on the initial detection of Indexable types is outweighed by it's simplification in other 
aspects.

### Choice of Slice as a anme
The name `Slice` was chosen as it's the de-facto standard name for slice style operations in .NET. Starting with 
netcoreapp2.1 all span style types use the name `Slice` for slicing operations. Prior to netcoreapp2.1 there really
aren't any examples of slicing to look to for an example. Types like `List<T>`, `ArraySegment<T>`, `SortedList<T>`
would've been ideal for slicing but the concept didn't exist when types were added. 

Thus `Slice` being the sole example it was chosen as the name.


## Related Issues
- https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.cs
- https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md

## Design Meetings
