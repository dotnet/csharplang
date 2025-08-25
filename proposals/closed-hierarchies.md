# Closed Hierarchies

## Summary

Allow a class to be declared `closed`. This prevents directly derived classes from being declared in a different assembly:

``` c#
// Assembly 1
public closed record class GateState;
public record class Closed : GateState;
public record class Open(float Percent) : GateState;

// Assembly 2
public record class Locked : GateState; // ERROR - 'GateState' is a closed class
```

Since all derived classes are declared in the closed class' assembly, a consuming `switch` expression that covers all of them can be concluded to "exhaust" the closed class - it does not need to provide a default case to avoid warnings.

``` c#
// Assembly 3
GateState state = ...;
string description = state switch
{
    Closed => "closed",
    Open(var percent) => $"{percent}% open"
    // No warning about missing cases
}; 
```

## Motivation

Many class types are not intended to be extended by anyone but their authors, but the language provides no way to express that intent, let alone guard against it happening. For consumers of the class this means that no set of derived classes will be considered to "exhaust" the base class, and a switch expression needs to include a catch-all case to avoid warnings.

Closed classes provide a way to indicate that a set of derived classes is complete, and allow consuming code to rely on that for exhaustiveness in switch expressions.

## Detailed design

### Syntax

Allow `closed` as a modifier on classes. A `closed` class is implicitly abstract whether or not the `abstract` modifier is specified. Thus, it cannot also have a `sealed` or `static` modifier. 

A class deriving from a closed class is *not* itself closed unless explicitly declared to be.

### Same-assembly restriction

If a class in one assembly is declared `closed` then it is an error to directly derive from it in another assembly:

``` c#
// Assembly 1
public closed class CC { ... } 
public class CO : CC { ... }     // Ok, same assembly

// Assembly 2
public class C1 : CC { ... }     // Error, 'CC' is closed and in a different assembly
public class C2 : CO { ... }     // Ok, 'CO' is not closed
```

### Type parameter restriction

If a generic class directly derives from a closed class, then all of its type parameters must be used in the base class specification:

```csharp
closed class C<T> { ... }
class D1<U> : C<U> { ... }   // Ok, 'U' is used in base class
class D2<V> : C<V[]> { ... } // Ok, 'V' is used in base class
class D3<W> : C<int> { ... } // Error, 'W' is not used in base class
```

This rule is to ensure that there is a single generic instantiation of the derived type that "exhausts" a given generic instantiation of the closed base type.

*Note:* This rule may not be sufficient if we allow closed interfaces at some point, because a) classes can implement multiple generic instantiations of the same interface, and b) interface type parameters can be co- or contravariant. At such point we'd need to refine the rule to continue to ensure that there's only ever one generic instantiation of a given derived type per generic instantiation of a closed base type.

### Exhaustiveness in switches

A `switch` expression that handles all of the direct descendants of a closed class will be considered to have exhausted that class. That means that some non-exhaustiveness warnings will no longer be given:

``` c#
CC cc = ...;
_ = cc switch
{
    CO co => ...,
    // No warning about non-exhaustive switch
};
```

On the other hand this also means that it can be an error for the closed base class to occur as a case after all its direct descendants:

``` c#
_ = cc switch
{
    CO co => ...,
    CC cc => ..., // Error, case cannot be reached
};
```

*Note:* There may not exist valid derived classes for certain generic instantiations of a closed base class. An exhaustive switch only needs to specify cases for derived types that are actually possible. 

For example:

```csharp
closed class C<T> { ... }
class D1<U> : C<U> { ... }
class D2<V> : C<V[]> { ... }
```

For `C<string>`, for instance, there is no corresponding instantiation of `D2<...>`, and no case for `D2<...>` needs to be given in a switch:

```csharp
C<string> cs = ...;
_ = cs switch
{
    D1<string> d1 => ...,
    // No need for a 'D2<...>' case - no instantiation corresponds to 'C<string>'
}
```

### Lowering

Closed classes are generated with a `Closed` attribute, to allow them to be recognized by a consuming compiler.

## Drawbacks

- It can be a breaking change to add a `closed` modifier to an existing class, or to add an additional derived class from a closed class. Before publishing a closed class, the author needs to consider the long term contract it implies with its consumers.
- Unless we find a way to prevent it, "unauthorized" derived classes may be allowed by unwitting other compilers, leading to the risk that the set of cases is not in fact closed at runtime.

## Alternatives

- Instead of a new `closed` modifier, a closed class could be designated with a `[Closed]` attribute.
- The scope of where descendants are allowed could be narrowed further to a file (although that would not have a lot of precedent in C#) or to inside the body of the closed class as nested classes.
- The closed set of allowed descendants could be given as a list instead of implied by where declarations occur. This would allow inclusion of classes in other assemblies.

## Optional features

- Interfaces could also be allowed to be closed. The rules would be very similar.

## Open questions

- Can closed classes be generated into IL in a way that prevents other languages and compilers from deriving from them even if they do not implement the feature?
