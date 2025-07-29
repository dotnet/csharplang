# Non-boxing Access Pattern for Custom Unions

## Summary

A custom union can provide an alternative, non-boxing means to access its value by implementing a `TryGetValue` method overload for each case type, as well as a `HasValue` property to check for null.

## Motivation

A motivating scenario to manually implement a [custom union type](https://github.com/dotnet/csharplang/tree/main/meetings/working-groups/discriminated-unions/Custom%20Unions.md) is to customize how the value is stored to hopefully either match an existing interop layout or to avoid allocations.

However, this goal is hampered by the limitation of the compiler only understanding how to access the value via the `Value` property, resulting in struct values being boxed regardless of layout.

This proposal allows custom union types that support non-allocation scenarios, opening the way to possible future first-class syntax for non-allocating unions and to the development of special union types like `Option` and `Result` that would benefit from minimizing or eliminating extra allocations.

## Specification

### Pattern

A custom union may offer non-boxing access to its value by implementing `TryGetValue` methods that accept each of its case types, plus a `HasValue` property to check for null:

```csharp
public struct MyUnion : IUnion
{
    public bool HasValue => ...;
    public bool TryGetValue(out Case1 value) {...}
    public bool TryGetValue(out Case2 value) {...}
    
    object? IUnion.Value => ...;
}
```

### Lowering

When the compiler lowers a type pattern match, and the type involved corresponds to a `TryGetValue` overload, the compiler uses this overload instead of the `Value` property to implement the pattern match.

```csharp
if (u is Case1 c1) {...}
```

lowers to:

```csharp
if (u.TryGetValue(out Case1 c1)) {...}
```

If multiple `TryGetValue` overloads apply, and overload resolution fails to pick a unique best overload, the compiler will pick one arbitrarily rather than yield an ambiguity error.

When the compiler lowers a `null` constant pattern match, and a `HasValue` property is available, the compiler uses this property instead of the `Value` property to implement the pattern match:

```csharp
if (u is null) {...}
```

lowers to:

```csharp
if (!u.HasValue) {...}
```

### Well-formedness

It is up to the author of a custom union with non-boxing access to ensure that the behavior of the access methods is functionally equivalent to the behavior of using the `Value` property:

- `u.HasValue` yields true if and only if `u.Value is not null` would yield true
- `u.TryGetValue(out T value1)` yields true if and only if `u.Value is T value2` would yield true, and `value1` is equal to `value2`.

## Example

Here is an example of a custom union employing a strategy of using separate fields for each case, and an additional field acting as a discriminator.

```csharp
public record struct Point(double X, double Y);
public record struct Rectangle(Point TopLeft, Point BottomRight);

public struct PointOrRectangle : IUnion
{
    private enum Kind { Null = 0, Point, Rectangle }

    private readonly Kind _kind;
    private readonly Point _value1;
    private readonly Rectangle _value2;

    public PointOrRectangle(Point value) =>
        (_kind, _value1, _value2) = (Kind.Point, value, default);

    public PointOrRectangle(Rectangle value) =>
        (_kind, _value1, _value2) = (Kind.Rectangle, default, value);

    object? IUnion.Value => 
        _kind switch 
        {
            Kind.Point => _value1, // boxes
            Kind.Rectangle => _value2, // boxes
            _ => null
        };

    public bool HasValue => _kind != Null;
        
    public bool TryGetValue(out Point value)
    {
        if (_kind == Kind.Point)
        {
            value = _value1;
            return true;
        }
        else 
        {
            value = default;
            return false;
        }
    }

    public bool TryGetValue(out Rectangle value)
    {
        if (_kind == Kind.Rectangle)
        {
            value = _value2;
            return true;
        }
        else 
        {
            value = default;
            return false;
        }
    }
}
```