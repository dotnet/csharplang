# covariant return types

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Support _covariant return types_. Specifically, allow an overriding method to have a more derived reference type than the method it overrides.

## Motivation
[motivation]: #motivation

It is a common pattern in code that different method names have to be invented to work around the language constraint that overrides must return the same type as the overridden method. See below for an example from the Roslyn code base.

## Detailed design
[design]: #detailed-design

Support _covariant return types_. Specifically, allow an overriding method to have a more derived reference type than the method it overrides. This would apply to methods and properties, and be supported in classes and interfaces.

This would be useful in the factory pattern. For example, in the Roslyn code base we would have

``` cs
class Compilation ...
{
    virtual Compilation WithOptions(Options options)...
}
```

``` cs
class CSharpCompilation : Compilation
{
    override CSharpCompilation WithOptions(Options options)...
}
```

The implementation of this would be for the compiler to emit the overriding method as a "new" virtual method that hides the base class method, along with a _bridge method_ that implements the base class method with a call to the derived class method.

## Drawbacks
[drawbacks]: #drawbacks

- [ ] Every language change must pay for itself.
- [ ] We should ensure that the performance is reasonable, even in the case of deep inheritance hierarchies
- [ ] We should ensure that artifacts of the translation strategy do not affect language semantics, even when consuming new IL from old compilers.

## Alternatives
[alternatives]: #alternatives

We could relax the language rules slightly to allow, in source,

```csharp
abstract class Cloneable
{
    public abstract Cloneable Clone();
}

class Digit : Cloneable
{
    public override Cloneable Clone()
    {
        return this.Clone();
    }

    public new Digit Clone() // Error: 'Digit' already defines a member called 'Clone' with the same parameter types
    {
        return this;
    }
}
```

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] How will APIs that have been compiled to use this feature work in older versions of the language?

## Design meetings

None yet. There has been some discussion at <https://github.com/dotnet/roslyn/issues/357>.