# Index and Range Changes

## Summary
This proposes to make several changes to the 
[index and range design](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md) based on 
customer feedback: particularly from the CoreFX team and their experiences adding Index / Range support to .NET Core.
The change is not to the syntax but rather how the langauge maps the syntax to APIs.

## Motivation
- Abstraction penalty
- Performance
- Reduce the API burden for making existing libraries Index / Range aware

## Detailed Design 
- Does not change the existing type logic. 

### Indexable types
Any type which has an accessible getter property named `Length` or `Count` with a return type of `int` is considered
Indexable. The language can use this property to efficiently convert an `Index` expression into an `int` at the 
point of the expression without relying on the implementation of receiver to do so. 

Note: For simplicity going forward the proposal will use the name `Length` to represent `Count` or `Length`.

For example it allows the following simplication:

``` csharp
Span<char> span = ...;
char c = span[^1];

// Can be translated to 
Span<char> span = ...;
char c = span[span.Length - 1];
```

Transforming an `Index` to an `int` at the call site significantly reduces the burden of frameworks to adopt `Index`. 
Vitrually any collection type will automatically work with `Index` now as the compiler can translate it to `int` in 
all cases.

Further this can improve performance by eliminating extra branching. The callee when accepting an `Index` parameter must
do both test to see if the value is from the end, `Index.IsFromEnd`, and if the value is inside the bounds of the 
collection. While a small check this can be important in performance sensitive areas.

Doing the translation to `int` at the call site means the `IsFromEnd` check can often be eliminated. For example when 
dealing with an `int` the compiler can pass the value through. Or in the cases where `^` is used the computation from
end can be done directly without the additional branching.

### Index target type conversion
Whenever an expression with type `Index` is used as an argument to an instance member invocation and the receiver is 
Indexable then the expression will have a target type conversion to `int`. The member invocations applicable for this
conversion include methods, indexers, properties, extension methods, etc ... Only constructors are excluded as they
have no receiver. 

The target type conversion will be implemented as follows:

- When the expression is `^expr` and the type is `int` it will be translated to `receiver.Length - expr`. This 
arithmetic will be implemented in an unchecked fashion.
- Else it will be translated as `expr.GetOffset(receiver.Length)` where `expr` is the expression typed as `Index`.

The receiver will be spilled as appropriate to ensure the side effects of obtaining the receiver are only executed
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


### Index and Range implementations are known
The implementations of `Index` and `Range` are considered to be known and side effect free. Much like 
`ValueTuple<T1, T2>` the language can assume a standard implementation and optimize around it.

### Detailed Decision Point 1

## Open Issues

### Extension method for Indexer

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

### Bind to a

### Choice of Slice as a anme

### Consideration 2

## Related Issues
- https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.cs
- https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md

## Design Meetings
