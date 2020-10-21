# Covariant returns

## Summary
[summary]: #summary

Support _covariant return types_. Specifically, permit the override of a method to declare a more derived return type than the method it overrides, and similarly to permit the override of a read-only property to declare a more derived type. Override declarations appearing in more derived types would be required to provide a return type at least as specific as that appearing in overrides in its base types. Callers of the method or property would statically receive the more refined return type from an invocation.

## Motivation
[motivation]: #motivation

It is a common pattern in code that different method names have to be invented to work around the language constraint that overrides must return the same type as the overridden method.

This would be useful in the factory pattern. For example, in the Roslyn code base we would have

``` cs
class Compilation ...
{
    public virtual Compilation WithOptions(Options options)...
}
```

``` cs
class CSharpCompilation : Compilation
{
    public override CSharpCompilation WithOptions(Options options)...
}
```

## Detailed design
[design]: #detailed-design

This is a specification for [covariant return types](https://github.com/dotnet/csharplang/issues/49) in C#.  Our intent is to permit the override of a method to return a more derived return type than the method it overrides, and similarly to permit the override of a read-only property to return a more derived return type.  Callers of the method or property would statically receive the more refined return type from an invocation, and overrides appearing in more derived types would be required to provide a return type at least as specific as that appearing in overrides in its base types.

--------------

### Class Method Override

The [existing constraint on class override](../../spec/classes.md#override-methods) methods

> - The override method and the overridden base method have the same return type.

is modified to

> - The override method must have a return type that is convertible by an identity conversion or (if the method has a value return - not a [ref return](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/ref-locals-returns.md)) implicit reference conversion to the return type of the overridden base method.

And the following additional requirements are appended to that list:

> - The override method must have a return type that is convertible by an identity conversion or (if the method has a value return - not a [ref return](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/ref-locals-returns.md)) implicit reference conversion to the return type of every override of the overridden base method that is declared in a (direct or indirect) base type of the override method.
> - The override method's return type must be at least as accessible as the override method  ([Accessibility domains](../../spec/basic-concepts.md#accessibility-domains)).

This constraint permits an override method in a `private` class to have a `private` return type.  However it requires a `public` override method in a `public` type to have a `public` return type.

### Class Property and Indexer Override

The [existing constraint on class override](../../spec/classes.md#virtual-sealed-override-and-abstract-property-accessors) properties

> An overriding property declaration shall specify the exact same accessibility modifiers and name as the inherited property, and there shall be an identity conversion ~~between the type of the overriding and the inherited property~~. If the inherited property has only a single accessor (i.e., if the inherited property is read-only or write-only), the overriding property shall include only that accessor. If the inherited property includes both accessors (i.e., if the inherited property is read-write), the overriding property can include either a single accessor or both accessors.

is modified to

> An overriding property declaration shall specify the exact same accessibility modifiers and name as the inherited property, and there shall be an identity conversion **or (if the inherited property is read-only and has a value return - not a [ref return](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/ref-locals-returns.md)) implicit reference conversion from the type of the overriding property to the type of the inherited property**. If the inherited property has only a single accessor (i.e., if the inherited property is read-only or write-only), the overriding property shall include only that accessor. If the inherited property includes both accessors (i.e., if the inherited property is read-write), the overriding property can include either a single accessor or both accessors. **The overriding property's type must be at least as accessible as the overriding property ([Accessibility domains](../../spec/basic-concepts.md#accessibility-domains)).**

-----------------

***The remainder of the draft specification below proposes a further extension to covariant returns of interface methods to be considered later.***

### Interface Method, Property, and Indexer Override

Adding to the kinds of members that are permitted in an interface with the addition of the DIM feature in C# 8.0, we further add support for `override` members along with covariant returns.  These follow the rules of `override` members as specified for classes, with the following differences:

The following text in classes:

> The method overridden by an override declaration is known as the ***overridden base method***. For an override method `M` declared in a class `C`, the overridden base method is determined by examining each base class of `C`, starting with the direct base class of `C` and continuing with each successive direct base class, until in a given base class type at least one accessible method is located which has the same signature as `M` after substitution of type arguments.

is given the corresponding specification for interfaces:

> The method overridden by an override declaration is known as the ***overridden base method***. For an override method `M` declared in an interface `I`, the overridden base method is determined by examining each direct or indirect base interface of `I`, collecting the set of interfaces declaring an accessible method which has the same signature as `M` after substitution of type arguments.  If this set of interfaces has a *most derived type*, to which there is an identity or implicit reference conversion from every type in this set, and that type contains a unique such method declaration, then that is the *overridden base method*.

We similarly permit `override` properties and indexers in interfaces as specified for classes in *15.7.6 Virtual, sealed, override, and abstract accessors*.

### Name Lookup

Name lookup in the presence of class `override` declarations currently modify the result of name lookup by imposing on the found member details from the most derived `override` declaration in the class hierarchy starting from the type of the identifier's qualifier (or `this` when there is no qualifier).  For example, in *12.6.2.2 Corresponding parameters* we have

> For virtual methods and indexers defined in classes, the parameter list is picked from the first  declaration or override of the function member found when starting with the static type of the receiver, and searching through its base classes.

to this we add

> For virtual methods and indexers defined in interfaces, the parameter list is picked from the declaration or override of the function member found in the most derived type among those types containing the declaration of override of the function member.  It is a compile-time error if no unique such type exists.

For the result type of a property or indexer access, the existing text

> - If I identifies an instance property, then the result is a property access with an associated instance expression of E and an associated type that is the type of the property. If T is a class type, the associated type is picked from the first declaration or override of the property found when starting with T, and searching through its base classes.

is augmented with

> If T is an interface type, the associated type is picked from the declaration or override of the property found in the most derived of T or its direct or indirect base interfaces.  It is a compile-time error if no unique such type exists.

A similar change should be made in *12.7.7.3 Indexer access*

In *12.7.6 Invocation expressions* we augment the existing text

> - Otherwise, the result is a value, with an associated type of the return type of the method or delegate. If the invocation is of an instance method, and the receiver is of a class type T, the associated type is picked from the first declaration or override of the method found when starting with T and searching through its base classes.

with

> If the invocation is of an instance method, and the receiver is of an interface type T, the associated type is picked from the declaration or override of the method found in the most derived interface from among T and its direct and indirect base interfaces.  It is a compile-time error if no unique such type exists.

### Implicit Interface Implementations

This section of the specification

> For purposes of interface mapping, a class member `A` matches an interface member `B` when:
> 
> - `A` and `B` are methods, and the name, type, and formal parameter lists of `A` and `B` are identical.
> - `A` and `B` are properties, the name and type of `A` and `B` are identical, and `A` has the same accessors as `B` (`A` is permitted to have additional accessors if it is not an explicit interface member implementation).
> - `A` and `B` are events, and the name and type of `A` and `B` are identical.
> - `A` and `B` are indexers, the type and formal parameter lists of `A` and `B` are identical, and `A` has the same accessors as `B` (`A` is permitted to have additional accessors if it is not an explicit interface member implementation).

is modified as follows:

> For purposes of interface mapping, a class member `A` matches an interface member `B` when:
> 
> - `A` and `B` are methods, and the name and formal parameter lists of `A` and `B` are identical, and the return type of `A` is convertible to the return type of `B` via an identity of implicit reference convertion to the return type of `B`.
> - `A` and `B` are properties, the name of `A` and `B` are identical, `A` has the same accessors as `B` (`A` is permitted to have additional accessors if it is not an explicit interface member implementation), and the type of `A` is convertible to the return type of `B` via an identity conversion or, if `A` is a readonly property, an implicit reference conversion.
> - `A` and `B` are events, and the name and type of `A` and `B` are identical.
> - `A` and `B` are indexers, the formal parameter lists of `A` and `B` are identical, `A` has the same accessors as `B` (`A` is permitted to have additional accessors if it is not an explicit interface member implementation), and the type of `A` is convertible to the return type of `B` via an identity conversion or, if `A` is a readonly indexer, an implicit reference conversion.

This is technically a breaking change, as the program below prints "C1.M" today, but would print "C2.M" under the proposed revision.

``` c#
using System;

interface I1 { object M(); }
class C1 : I1 { public object M() { return "C1.M"; } }
class C2 : C1, I1 { public new string M() { return "C2.M"; } }
class Program
{
    static void Main()
    {
        I1 i = new C2();
        Console.WriteLine(i.M());
    }
}
```

Due to this breaking change, we might consider not supporting covariant return types on implicit implementations.

### Constraints on Interface Implementation

**We will need a rule that an explicit interface implementation must declare a return type no less derived than the return type declared in any override in its base interfaces.**

### API Compatibility Implications

*TBD*

### Open Issues

The specification does not say how the caller gets the more refined return type. Presumably that would be done in a way similar to the way that callers get the most derived override's parameter specifications.

--------------

If we have the following interfaces:

```csharp
interface I1 { I1 M(); }
interface I2 { I2 M(); }
interface I3: I1, I2 { override I3 M(); }
```

Note that in `I3`, the methods `I1.M()` and `I2.M()` have been “merged”.  When implementing `I3`, it is necessary to implement them both together.

Generally, we require an explicit implementation to refer to the original method.  The question is, in a class

```csharp
class C : I1, I2, I3
{
    C IN.M();
}
```

What does that mean here?  What should *N* be?

I suggest that we permit implementing either `I1.M` or `I2.M` (but not both), and treat that as an implementation of both.

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

- some discussion at <https://github.com/dotnet/roslyn/issues/357>.
- https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-01-08.md
- Offline discussion toward a decision to support overriding of class methods only in C# 9.0.
