# Closed Hierarchies

Champion issue: https://github.com/dotnet/csharplang/issues/9499  

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

Allow `closed` as a modifier on classes. A `closed` class is implicitly abstract. Thus, it cannot also have a `sealed` or `static` modifier. 

It is an error to explicitly use an `abstract` modifier on a `closed` class.

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

The same restriction applies to modules. A subtype of a `closed` type must be located within the same module as the base type.

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

#### Exhaustiveness when a subtype can't be used

If a subtype is not valid at a particular use site, due to constraint violations, accessibility violations, or other reasons, then, it's not possible to exhaust the switch via subtypes.

```cs
closed class C;
class D1 : C;
class Container
{
    protected class D2 : C;
}

class Program
{
    int M(C c)
        => c switch
        {
            D1 => 1,
            // warning: switch is non-exhaustive. Pattern 'C' is not handled.
        };
}
```

This also applies when a generic subtype is not *speakable*, and its applicability may depend on the final type argument substitution.

```csharp
closed class C<T> { ... }
class D1<U> : C<U> { ... }
class D2<V> : C<V[]> { ... }

class Program
{
    int M<X>(C<X> c)
        => c switch
        {
            D1<X> => 1,
            // warning: switch is non-exhaustive. Pattern 'C' is not handled.
        };
}
```

### Determining subtypes of a closed class

Exhaustiveness of switches over closed class types, is determined by checking if the switch is exhaustive over the *set of subtypes* of the input closed class type.

The set of subtypes `S` of a closed class is determined in the following way:
1) For a given closed type `C`, let `C₀` be its original definition.
2) For each subtype declaration `S₀` whose base type has original definition `C₀`, determine if a construction `S` exists which has base type `C`.
    - See also [§19.6.3 Uniqueness of implemented interfaces](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/interfaces.md#1963-uniqueness-of-implemented-interfaces) in the standard.
3) If such an `S` exists, it is included in the *set of subtypes*.

### Interface convertibility of closed classes

A closed class is said to have a *sealed hierarchy*, if all its subtypes are either *sealed* or have a *sealed hierarchy*. That is, all the classes in the expanded hierarchy are either sealed or closed.

When a closed class has a *sealed hierarchy*, then an *interface convertibility* restriction is introduced. This prevents attempting a conversion to interface type, which could never possibly succeed.

This restriction is similar in nature to *explicit reference conversion* from a sealed class type to interface type. See [§10.3.5 Explicit reference conversions](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#1035-explicit-reference-conversions).

```cs
var c = new C();
var i = (I)c; // error

closed class C { }
sealed class D1 : C { }
sealed class D2 : C { }
interface I { }
```

We determine whether the explicit reference conversion from `C` to `I` exists, by recursively gathering the set of interfaces implemented by `C` and its subtypes. If the set of interfaces includes `I`, and `C` does not implement `I`, then the explicit reference conversion exists from `C` to `I`. (In the case that `C` implements `I`, then an implicit reference conversion is available instead.)

### Lowering

Closed classes are generated with a `Closed` attribute, to allow them to be recognized by a consuming compiler.

```cs
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ClosedAttribute : Attribute { }
}
```

#### Blocking subtyping from other languages/compilers

Closed classes shall not be inherited from languages that do not support closed classes. This is accomplished by adding `[CompilerFeatureRequired("ClosedClasses")]` to all constructors of closed classes.

```cs
// Authoring assembly, built with .NET 10 SDK
closed class C1
{
    public C1() { }
    public C1(int param) { }
}

// Consuming assembly, built with .NET 8 SDK
class C2 : C1
{
    public C2() { } // error: 'C1.C1()' requires compiler feature "ClosedClasses"
    public C2() : base(42) { } // error: 'C1.C1(int)' requires compiler feature "ClosedClasses"
}
```

Metadata "view" of `C1`:
```cs
[Closed]
class C1
{
    [CompilerFeatureRequired("ClosedClasses")]
    public C1() { }
    [CompilerFeatureRequired("ClosedClasses")]
    public C1(int param) { }
}
```

Note that unlike for the "required members" feature, an ObsoleteAttribute is not emitted in addition to the CompilerFeatureRequiredAttribute. Only the latter is emitted.

#### Multiple CompilerFeatureRequiredAttributes

In a scenario like the following, the compiler will emit a separate `CompilerFeatureRequired`, for every required feature that is relevant to the symbol:

```cs
closed class C1
{
    public C() { }
    public required string P { get; set; }
}

// Metadata:
class C1
{
    [Obsolete("Types with required members are not supported in this version of your compiler")]
    [CompilerFeatureRequired("RequiredMembers")]
    [CompilerFeatureRequired("ClosedClasses")]
    public C1() { }
}
```

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

### Exhaustiveness when no subtypes exist

Should an exhaustiveness warning be reported for an empty switch, when the input is a closed type with no subtypes?

Recommendation: Require handling the closed class itself in this case. Essentially, pattern matching should treat it the same as a non-closed class.

```cs
closed class C;

class Program
{
    int M1(C c)
        // warning: switch is not exhaustive.
        => c switch
        {
        };

    int M2(C c)
        => c switch
        {
            C => 1, // ok
        };
}
```

### Allow matching the base type after matching all subtypes

Should it be permitted to match a closed base type in a switch, after all its subtypes have been exhausted?

Recommendation: No, report an error in this scenario.

```cs
closed class C;
class D1 : C;
class D2 : C;

class Program
{
    int M1(C c)
        => c switch
        {
            D1 => 1,
            D2 => 2,
            C => 3, // error: switch arm is impossible to match.
        };
}
```

### Exhaustiveness of type parameters constrained to closed type

Should it be permitted to exhaust a type parameter constrained to a closed class type, by matching the subtypes of the closed class?

```cs
closed class C;
class D1 : C;
class D2 : C;

class Program
{
    int M1<X>(X x) where X : C
        // warning: switch is not exhaustive. 'C' is not handled.
        => x switch
        {
            D1 => 1,
            D2 => 2,
        };

    int M2<X>(X x) where X : C
        => x switch
        {
            D1 => 1,
            D2 => 2,
            C => 3, // ok
        };
}
```

### Ruling out generic closed subtypes based on constraints

"Ruling out" a subtype of a closed class, requires determining that the subtype's base type, could never possibly unify with the switch input type.

Today, the CanUnify check does not consider constraints. In other words, a program like the following, requires explicitly matching the input closed type, because while the subtype cannot be used, we don't observe that it's impossible for the subtype to arise via generic substitution.

```cs
closed class C<T>;
class D1<U1> : C<U1>;
class D2<U2> : C<U2> where U2 : struct;

class Program
{
    int M1<X>(C<X> c) where X : class
    {
        // warning: switch is not exhaustive. Pattern 'C<X>' is not handled.
        return c switch
        {
            D1<X> => 1,
        };
    }

    int M2<X>(C<X> c) where X : class
    {
        return c switch
        {
            D1<X> => 1,
            C<X> => 2, // ok
        };
    }
}
```

The question is: should we try to detect when "mutually exclusive" sets of constraints are present, and filter out subtypes accordingly? This would remove the warning reported in 'M1' in the above sample.

Recommendation: No, don't introduce any new such detection. This will require writing "unnecessary" patterns which won't actually match in practice, in complex constraint scenarios like the above sample. However, the harm of such "unnecessary" patterns, seems less than the cost of implementing a precisely correct "mutually exclusive" check for constraints.
