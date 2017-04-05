# default interface methods

* [x] Proposed
* [ ] Prototype: [In progress](https://github.com/dotnet/roslyn/tree/features/DefaultInterfaceImplementation)
* [ ] Implementation: None
* [ ] Specification: In progress, below


## Summary
[summary]: #summary

Add support for _virtual extension methods_ - methods in interfaces with concrete implementations. A class or struct that implements such an interface is required to have a single _most specific_ implementation for the interface method, either implemented by the class or struct, or inherited from its base classes or interfaces. Virtual extension methods enable an API author to add methods to an interface in future versions without breaking source or binary compatibility with existing implementations of that interface.

These are similar to Java's ["Default Methods"](http://docs.oracle.com/javase/tutorial/java/IandI/defaultmethods.html).

(Based on the likely implementation technique) this feature requires corresponding support in the CLI/CLR. Programs that take advantage of this feature cannot run on earlier versions of the platform.


## Motivation
[motivation]: #motivation

The principal motivations for this feature are
- Default interface methods enable an API author to add methods to an interface in future versions without breaking source or binary compatibility with existing implementations of that interface. 
- The feature enables C# to interoperate with APIs targeting [Android (Java)](http://docs.oracle.com/javase/tutorial/java/IandI/defaultmethods.html) and [iOs (Swift)](https://developer.apple.com/library/content/documentation/Swift/Conceptual/Swift_Programming_Language/Protocols.html#//apple_ref/doc/uid/TP40014097-CH25-ID267), which support similar features.
- As it turns out, adding default interface implementations provides the elements of the "traits" language feature (https://en.wikipedia.org/wiki/Trait_(computer_programming)). Traits have proven to be a powerful programming technique (http://scg.unibe.ch/archive/papers/Scha03aTraits.pdf).


## Detailed design
[design]: #detailed-design

The syntax for an interface is extended to permit
- a *body* for a method or indexer, property, or event accessor (i.e. a "default" implementation)
- static methods, properties, indexers, and events.
- Explicit access modifiers (the default access is `public`)
- `override` modifiers

Members with bodies permit the interface to provide a "default" implementation for the method in classes and structs that do not provide an overriding implementation.

Interfaces may not contain instance state. While static fields are now permitted, instance fields are not permitted in interfaces. Instance auto-properties are not supported in interfaces, as they would implicitly declare a hidden field.

Static and private methods permit useful refactoring and organization of code used to implement the interface's public API.


### Concrete methods in interfaces

The simplest form of this feature is the ability to declare a *concrete method* in an interface, which is a method with a body.

``` c#
interface IA
{
    void M() { WriteLine("IA.M"); }
}
```

A class that implements this interface need not implement its concrete method.

``` c#
class C : IA { } // OK

IA i = new C();
i.M(); // prints "IA.M"
```

The final override for `IA.M` in class `C` is the concrete method `M` declared in `IA`. Note that a class does not inherit members from its interfaces; that is not changed by this feature:

``` c#
new C().M(); // error: class 'C' does not contain a member 'M'
```

Within an instance member of an interface, `this` has the type of the enclosing interface.


### Modifiers in interfaces

The syntax for an interface is relaxed to permit modifiers on its members. The following are permitted: `private`, `protected`, `internal`, `public`, `virtual`, `abstract`, `override`, `sealed`, `static`, `extern`.

> ***TODO***: check what other modifiers exist.

An interface member whose declaration includes a body is a `virtual` member unless the `sealed` modifier is used, whether or not the `virtual` modifier is present. Similarly, although `abstract` is the default on interface members without bodies, that modifier may be given explicitly.  A non-virtual member may be declared using the `sealed` keyword.

Access modifiers may be used on interface members. The access level `public` is the default but it may be given explicitly.

> ***Open Issue:*** We need to specify the precise meaning of the access modifiers such as `protected`.

Interfaces may declare `static` members, including nested types, methods, indexers, properties, and events. The default access level is `public`.

> ***Open Issue:*** Should operator declarations be permitted in an interface? Probably not conversion operators, but what about others?

> ***Open Issue:*** Should `new` be permitted on interface member declarations that hide members from base interfaces?

> ***Open Issue:*** Should `const` declarations be permitted in an interface?




### Overrides in interfaces

Override declarations (i.e. those containing the `override` modifier) allow the programmer to provide a most specific implementation of a virtual member in an interface where the compiler or runtime would not otherwise find one. It also allows turning an abstract member from a super-interface into a default member in a derived interface. An override declaration is permitted to *explicitly* override a particular base interface method by qualifying the declaration with the interface name (no access modifier is permitted in this case).


``` c#
interface IA
{
    void M() { WriteLine("IA.M"); }
}
interface IB : IA
{
    override void IA.M() { WriteLine("IB.M"); } // explicitly named
}
interface IC : IA
{
    override void M() { WriteLine("IC.M"); } // implicitly named
}
```

If the interface is not named in the override declaration, then all matching methods (from direct or indirect base interfaces) are overridden. There must be at least one such method or the override declaration is an error.

> ***Open issue***: should that "direct and indirect" be "direct" here?

Overrides in interfaces are useful to provide a more specific (e.g. more efficient) implementation of a base interface's method. For example, a new `First()` method on `IEnumerable` may have a much more efficient implementation on the interface `IList`.

A method declared in an interface is never treated as an `override` of another method unless it contains he `override` modifier. This is necessary for compatibility.

``` c#
interface IA
{
    void M();
}
interface IB : IA
{
    void M(); // not related to 'IA.M'; not an override
}
```

Override declarations in interfaces may not be declared `sealed`.


### Reabstraction

A virtual (concrete) method declared in an interface may be overridden to be abstract in a derived interface

``` c#
interface IA
{
    void M() { WriteLine("IA.M"); }
}
interface IB : IA
{
    override abstract void M();
}
class C : IB { } // error: class 'C' does not implement 'IA.M'.
```

The `abstract` modifier is not required in the declaration of `IB.M` (that is the default in interfaces), but it is probably good practice to be explicit in an override declaration.

This is useful in derived interfaces where the default implementation of a method is inappropriate and a more appropriate implementation should be provided by implementing classes.

> ***Open Issue:*** Should reabstraction be permitted?

### The most specific override rule

We require that every interface and class have a *most specific override* for every interface method among the overrides appearing in the type or its direct and indirect interfaces. The *most specific override* is a unique override that is more specific than every other override. If there is no override, the method itself is considered the most specific override.

One override `M1` is considered *more specific* than another override `M2` if `M1` is declared on type `T1`, `M2` is declared on type `T2`, and either
1. `T1` contains `T2` among its direct or indirect interfaces, or
2. `T2` is an interface type but `T1` is not an interface type.

``` c#
interface IA
{
    void M() { WriteLine("IA.M"); }
}
interface IB : IA
{
    override void IA.M() { WriteLine("IB.M"); }
}
interface IC : IA
{
    override void IA.M() { WriteLine("IC.M"); }
}
interface ID : IB, IC { } // error: no most specific override for 'IA.M'
abstract class C : IB, IC { } // error: no most specific override for 'IA.M'
abstract class D : IA, IB, IC // ok
{
    public abstract void M();
}

```

The most specific override rule ensures that a conflict (i.e. an ambiguity arising from diamond inheritance) is resolved explicitly by the programmer at the point where the conflict arises.

Because we support explicit abstract overrides in interfaces, we could do so in classes as well

``` c#
abstract class E : IA, IB, IC // ok
{
    abstract void IA.M();
}
```

> ***Open issue***: should we support explicit interface abstract overrides in classes?

In addition, it is an error if in a class declaration the most specific override of some interface method is an an abstract override that was declared in an interface. This is an existing rule restated using the new terminology.

``` c#
interface IF
{
    void M();
}
abstract class F : IF { } // error: 'F' does not implement 'IF.M'
```

### `static` and `private` methods

Because interfaces may now contain executable code, it is useful to abstract common code into private and static methods. We now permit these in interfaces.

> ***Open issue***: Should we support private methods? Should we support static methods? **YES**

> ***Open issue***: should we permit interface methods to be `protected` or `internal` or other access? If so, what are the semantics? Are they `virtual` by default? If so, is there a way to make them non-virtual?

> ***Open issue***: If we support static methods, should we support (static) operators?


### Base interface invocations

Code in a type that derives from an interface with a default method can explicitly invoke that interface's "base" implementation.


``` c#
interface I0
{
   void M() { Console.WriteLine("I0"); }
}
interface I1 : I0
{
   override void M() { Console.WriteLine("I1"); }
}
interface I2 : I0
{
   override void M() { Console.WriteLine("I2"); }
}
interface I3 : I1, I2
{
   // an explicit override that invoke's a base interface's default method
   void I0.M() { I2.base.M(); }
}

```


An instance (nonstatic) method is permitted to invoke an accessible instance method override in a direct base interface nonvirtually by naming it using the syntax `Type.base.M`. This is useful when an override that is required to be provided due to diamond inheritance is resolved by delegating to one particular base implementation.

``` c#
interface IA
{
    void M() { WriteLine("IA.M"); }
}
interface IB : IA
{
    override void IA.M() { WriteLine("IB.M"); }
}
interface IC : IA
{
    override void IA.M() { WriteLine("IC.M"); }
}

class D : IA, IB, IC
{
    void IA.M() { IB.base.M(); } 
}
```

> ***Open issue:*** what syntax should we use for base invocation? Alternatives:
> 1. Interface.base.M()
> 2. base<Interface>.M()

### Effect on existing programs

The rules presented here are intended to have no effect on the meaning of existing programs.

Example 1:

``` c#
interface IA
{
    void M();
}
class C: IA // Error: IA.M has no concrete most specific override in C
{
    public static void M() { } // method unrelated to 'IA.M' because static
}
```

Example 2:

``` c#
interface IA
{
    void M();
}
class Base: IA
{
    void IA.M() { }
}
class Derived: Base, IA // OK, all interface members have a concrete most specific override
{
    private void M() { } // method unrelated to 'IA.M' because private
}
```

The same rules give similar results to the analogous situation involving default interface methods:

``` c#
interface IA
{
    void M() { }
}
class Derived: IA // OK, all interface members have a concrete most specific override
{
    private void M() { } // method unrelated to 'IA.M' because private
}
```

> ***Open issue***: confirm that this is an intended consequence of the specification. **YES**

### Further areas to be specified

- [ ] It would be useful to catalog the kinds of source and binary compatibility effects caused by adding default interface methods and overrides to existing interfaces.



## Drawbacks
[drawbacks]: #drawbacks

This proposal requires a coordinated update to the CLR specification (to support concrete methods in interfaces and method resolution). It is therefore fairly "expensive" and it may be worth doing in combination with other features that we also anticipate would require CLR changes.

## Alternatives
[alternatives]: #alternatives

None.

## Unresolved questions
[unresolved]: #unresolved-questions

Open questions are called out throughout the proposal, above.

The detailed specification will describe the resolution mechanism used at runtime to select the precise method to be invoked.

## Design meetings

2017-03-08 LDM Meeting Notes (not published yet)
2017-03-20 LDM Meeting Notes (not published yet)
[2017-03-23 meeting "CLR Behavior for Default Interface Methods"](https://github.com/dotnet/csharplang/issues/404) (not published yet)

