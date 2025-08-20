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

If a generic class `D` directly derives from a closed class `C`, then all of its type parameters must be "inferrable" from the base class specification. Informally, this rule is to ensure that for every generic instantiation of `C` there is at most one generic instantiation of `D` that derives from it. If that were not the case, it would not be possible to do an exhaustive switch over `C` with just one case per derived class.

Formally, given the following declarations of `C` and `D`:

```csharp
class C<...> { ... }
class D<X₁...Xᵥ> : C<T₁...Tₓ> { ... }
```

And these two function declarations:

```csharp
D<X₁...Xᵥ> Make_D<X₁...Xᵥ>() => default!;
C<T₁...Tₓ> Make_C<X₁...Xᵥ>() => Make_D();
```

We say that the type parameters of `D` are *inferrable* from `D`'s base class if and only if type inference succeeds for the call to `Make_D` in the body of `Make_C`.

*Note:* For closed classes, this rule seems equivalent to demanding that each type parameter of `D` occurs in the base class specification. However, by expressing it in terms of generic type inference, it is more directly evident that it satisfies the motivation for the rule. Also if we were to add closed *interfaces* the rule would be more easily adaptable to accommodate that.

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

For a given instantiation of a generic closed base class `C<...>`, there may not exist a valid generic instantiation of a given derived class `D<...>`. In this situation, no case for `D` needs to provided in order for a switch to be exhaustive.

For example:

```csharp
closed class C<T> { ... }
class D1<U> : C<U> { ... }
class D2<V> : C<V[]> { ... }
```

For the generic instantiation `C<string>` there is no corresponding instantiation of `D2<...>`, and no case for `D2<...>` therefore needs to be given in a switch:

```csharp
C<string> cs = ...;
_ = cs switch
{
    D1<string> d1 => ...,
    // No need for a 'D2<...>' case - no instantiation corresponds to 'C<string>'
}
```

Formally, given the following declarations of `C` and `D`:

```csharp
class C<...> { ... }
class D<X₁...Xᵥ> : C<T₁...Tₓ> { ... }
```

A concrete generic instantiation `C<S₁...Sₓ>`, and these two function declarations:

```csharp
D<X₁...Xᵥ> Make_D<X₁...Xᵥ>() => default!;
C<S₁...Sₓ> Make_Concrete_C() => Make_D();
```

A valid corresponding instantiation of `D<...>` exists and must be included in an exhaustive switch if and only if type inference succeeds and produces valid type arguments for the invocation of `Make_D` in the body of `Make_Concrete_C`. The type arguments for `D<...>` are those produced by the successful type inference.

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
