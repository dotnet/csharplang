# Union patterns update

Union patterns matter for "custom unions", i.e., union types that are "handwritten" to get the language's [union behaviors](https://github.com/dotnet/csharplang/blob/main/proposals/unions.md#union-behaviors), rather than generated from [union declarations](https://github.com/dotnet/csharplang/blob/main/proposals/unions.md#union-declarations). 

We anticipate custom unions to include existing "union-like" types that are augmented to be recognized as unions by the language, as well as new types for which different characteristics (e.g. in terms of storage or performance) are desired compared to what's generated from union declarations. We expect both kinds to be rare in comparison to use of union declarations, and for most users never having to declare custom union types.

We propose a few changes to the current patterns in order to better serve more scenarios:

- Union types are marked with a `[Union]` attribute instead of the `IUnion` interface.
- Union members are either found on the union type itself or delegated to an interface that the union type implements.


In addition there are some potential scenarios that we don't currently have proposals for, but that might come up later. We list those at the end.

## Marking union types with an attribute

The current pattern of marking union types with the `IUnion` interface leads to a couple of problems, e.g.:

- A type cannot implement the `IUnion` interface *without* being a union.
- If a base class is a union then derived classes must also be unions.

We propose to use a `[Union]` attribute to mark types that are intended to have union behaviors:

```csharp
[Union] public record struct Pet
{
    public Pet(Dog value) => Value = value;
    public Pet(Cat value) => Value = value;
    public Pet(Bird? value) => Value = value;

    public object? Value { get; }
}
```

As part of this change, the `Value` property used by the compiler is no longer `IUnion.Value`, but just the property found on the type. Union patterns no longer rely on specific interfaces or interface members.

We don't anticipate allowing `[Union]` on type parameters, which therefore cannot be considered union types by the compiler.

## Union member providers

The current pattern looks for members on the union type itself. This has a few downsides:

- Existing types may have members that would unintentionally be recognized as union members (e.g. copy constructor).
- Types may not wish to expose additional union members as part of their public surface area.

We propose to allow a union type to optionally delegate all union members to a nested `IUnionMembers` interface that the union type implements. Since constructors for a type cannot occur on another type, the interface would instead use static factory methods called `Create`:

```csharp
[Union] public record struct Pet : Pet.IUnionMembers
{
    object? _value;

    Pet(object? value) => _value = value;
    
    // Look for union members here, not in 'Pet' itself
    public interface IUnionMembers
    {
        static Pet Create(Dog value) => new(value);
        static Pet Create(Cat value) => new(value);
        static Pet Create(Bird? value) => new(value);

        object? Value { get; }
    }

    object? IUnionMembers.Value => _value;
}
```

The compiler can use constrained calls where applicable to avoid the overhead of interface invocation.

### Alternatives

Instead of delegating to another type, it has been proposed to mark union members in the union type itself with attributes. This could allow marking members with different names, or different kinds of members (e.g. factory methods or user-defined implicit conversions) so as to make use of any suitable members the type already has.

One downside is that attributes can never be used to hide members from the surface area. Thus it is strictly less expressive than delegation. In addition, the set of attributes and the rules for their usage might get quite complicated. By comparison, the delegation approach puts all union members in one place with well-defined, unambiguous names and signatures.

## Other scenarios

We can imagine reasonable scenarios that are not expressible by the above changes. While we are not proposing solutions for those now, it is useful to imagine possible ways to address them in the future, if only to reassure ourselves that we are not accidentally blocking them by choices we make now.

### "Consume-only" unions

Not all union types may want to offer the ability for their users to create union values directly; perhaps instead they offer APIs that create them internally and hand them out.

Such unions wouldn't have creation members (constructors or factories), so they need another way of specifying their case types. We don't yet have specific proposals.

### More specific shared base types

In some union types, case types share a more specific base type than `object?`. We could probably allow the `Value` property to expose such a more specific type, and have the compiler take advantage of the additional knowledge (e.g. non-nullability) in its implementation of union behaviors.

### Non-object case types

The requirement for an `object`-returning `Value` property that gives access to the union's value no matter the case type, means that all case types have to be implicitly convertible to object.

That rules out e.g. ref structs and pointer types as case types of compiler-recognized unions.

There may be ways in which we can amend this, either by removing the requirement for a `Value` property to exist, or allowing certain case types not to be accessed through it. We don't yet have specific proposals.

### Non-boxing access pattern

The original proposal includes an optional non-boxing access pattern, and this proposal preserves that. Additional `HasValue` and `TryGetValue` members can be used by the compiler as strongly typed alternatives to matching through the weakly typed `Value` property. This can be used for efficiency purposes, e.g. when a union type stores value types directly instead of boxing them.

The current non-boxing access pattern is definitely worth revisiting before we lock it in, but we don't yet have specific alternate proposals.