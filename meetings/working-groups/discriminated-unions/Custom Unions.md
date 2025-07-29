# Custom Unions

## Summary

[Nominal Type Unions](https://github.com/dotnet/csharplang/blob/main/proposals/nominal-type-unions.md) allow the compiler to generate union types that have special behavior when consumed.

This proposal specifies a pattern that a class or struct declaration can follow in order to get the same special behavior when consumed.

## Motivation

The declaration syntax for nominal unions is intended to cover most green-field situations where people want to specify a union. However, for some scenarios the generated outcome is not optimal or even usable:

- There is already an existing widely consumed library type representing a "union", and the author wants to imbue it with "union powers" without breaking existing usage.
- For e.g. performance, architectural or interop reasons, the contents of the union need to be stored differently than the default boxed object field used in compiler-generated unions.

## Specification

A type is considered a "custom union type" if it implements the [`IUnion` interface](Union%20Interfaces.md). Every constructor on the type that is at least as accessible as the type and takes exactly one parameter contributes the type of that parameter as a case type of the custom union type.

The consumption of such a type as a custom union type is enabled in the following ways:

- **Implicit conversion**: There is an implicit union conversion from each case type to the custom union type. It is implemented by calling the corresponding constructor.
- **Pattern matching**: Patterns applied to a value of the custom union type (other than always-succeeding patterns such as `_` and `var`) are instead applied to the `IUnion.Value` property.
- **Exhaustiveness**: Switch expressions covering all the case types of the custom union type are considered exhaustive.

## Example

Say the following type already exists:

```csharp
public sealed class Result<T>
{
    internal Result(object? outcome) => (Value, Error) = outcome switch
    {
        Exception error => (default!, error),
        T value => (value, null),
        null when default(T) is null => (default!, null);
        _ => throw new InvalidOperationException(...);
    };
    
    public Result(T value) => (Value, Error) = (value, null);
    public Result(Exception error) => (Value, Error) = (default!, error);
    
    public T Value { get; }
    public Exception? Error { get; }
    public bool Succeeded => Error is null;
}
```

It can be made a union type simply by implementing the `IUnion` interface:

```csharp
public sealed class Result<T> : IUnion
{
    object? IUnion.Value => Error ?? Value;
    ... // Existing members
}
```

Note that in this example the existing type already has a public `Value` property with a different meaning than the one on the `IUnion` interface, so `IUnion.Value` gets implemented explicitly, and that's the one the compiler will consume for pattern matching purposes.

Note also that the type is only considered to have two case types, `T` and `Exception`, even though it has a third single-parameter constructor. That's because the `object?` constructor is less accessible than the type itself and doesn't count.

## Drawbacks

Not every existing type may be enhanced in a non-breaking way to become a custom union type using these rules. For instance, it may not be able to expose the right set of constructors to establish the desired set of case types - e.g. it relies on factory methods for creating values. It is possible that we need to refine or enhance the mechanism by which a type is interpreted as a custom union type.