# Type Value Conversion

## Summary

A type expression specified in a value context can be converted to a value if the type supports a conversion to value.

```csharp
GateState value = GateState.Locked;
```

## Motivation

A nominal type union can declare cases that contain no corresponding values. Unions that are similar to enums will often have many of these no-value cases.

```csharp
public union GateState
{
    case Locked;
    case Closed;
    case Open(float amount);
}
```

Even though only a few of the cases contain data, all cases are separate types. In this example, the types declared when using case declarations are records. 

However unfortunate, a case without data must still be allocated.

```csharp
GateState state = new GateState.Locked()
```

It would be preferable if all uses of these non-value cases could share a single instance and avoid repeated allocation. A typical way of achieving this is to declare a static field or property on the type, using that member to access the same instance each time.

```csharp
record Locked { public static readonly Locked Instance = new Locked(); }
```

A case declaration could add this member implicitly, but a user would still need to refer to it explicitly. This may be unexpected at first and tedious always.

```csharp
GateState value = GateState.Locked.Instance;
```

If the type expression could be converted directly to a value, then code is clearer because it is always just referring to the case, and not an implementation artifact.

```csharp
GateState value = GateState.Locked;
```

* *Note: It is not possible to have a nested type and a property of the same name in the same declaration scope.*

### Other Uses

Singleton classes are quite common in the wild and would benefit from a type to value conversion.

For example, most custom equality comparers take no outside arguments and rarely ever require more than one instance to exist.

```csharp
public class MyEqualityComparer : IEqualityComparer<MyType>
{
    public bool Equals(MyType a, MyType b) => ...;
    public int GetHashCode(MyType a) => ...;

    public static readonly MyEqualityComparer Instance = new MyEqualityComparer();
}
```

## Detailed Design

The type expression to value conversion is declared via a conversion operator on the type.

```csharp
public record Locked
{
    public static readonly Locked Instance = new Locked();
    public static implicit operator this => Instance;
}
```

* The `this` operator converts a type expression to a value of that same type.
* The author declaring a conversion operator expresses clear intent for the conversion to exist.

## Alternative Designs

### Conversion by Pattern

A conversion exists between a type expression and a value when the type contains a static field or property with a recognized name that returns a value of the type's type.

```csharp
public record Locked
{
    public static readonly Locked Instance = new Locked();
}
```

* The pattern looks for a specific member name like `Instance`.

### Conversion by Attribute

A conversion exists between a type expression and a value when the type contains a static field or property decorated with the `Singleton` attribute.

```csharp
public record Locked
{
    [Singleton]
    public static readonly Locked Value = new Locked();
}
```

* This alternative allows existing types with fields/properties returning singleton or default values to support this conversion even if the member does not have the pattern recognized name.

### Conversion by Interface

A conversion exists between a type expression and a value when the type implements an interface with a known static property.

```csharp
public interface ISingleton<TSelf>
    where TSelf : ISingleton<TSelf>
{
    TSelf Singleton { get; }
}
```

```csharp
public record Locked : ISingleton<Locked>
{
    public static readonly Locked Instance = new Locked();
    static Locked ISingleton<Locked>.Singleton => _instance;
}
```

* The compiler uses the property from the interface.
