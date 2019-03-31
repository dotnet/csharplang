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

- Index / Range have known implementations

### Indexable types

### Detailed Decision Point 1

## Open Issues

### open Issue 1

### Detect on Count or Length
The property name `Count` was chosen to identify Indexable types as it's the standard over `Length` in collection 
types. The design could be extended to use `Count` or `Length` (preferring `Count` if both are present). This 
complicates the design a bit though and given the overwelming majority of collections use `Count`, typically via
`ICollection.Count`, the simpler design was chosen.

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

After consideration the design was normalized to say that any type which has a property `Count` with a return type of
`int` is Indexable. That still requires array and `string` to be special cased. That is more palatable though as it's
a fixed set of type rather than an entire class.

### Use Slice in

### Consideration 2

## Related Issues
- https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.cs
- https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md

## Design Meetings
