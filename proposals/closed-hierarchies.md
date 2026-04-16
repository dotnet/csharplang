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

### Generics and exhaustiveness

Use of generic closed classes means that the number and set of subtypes can depend on the particular construction of the closed class.

```cs
closed class C<T>;
class D1 : C<int>;
class D2 : C<int>;
class D3 : C<string>;
class D4<T> : C<T>;
class D5<T> : C<ImmutableArray<T>>;

class Program
{
    public int M1(C<int> c)
    {
        return c switch // exhaustive
        {
            D1 => 1,
            D2 => 2,
            // D3 and D5 is not permitted
            D4<int> => 4,
        };
    }

    public int M2(C<string> c)
    {
        return c switch // exhaustive
        {
            // D1, D2, and D5 is not permitted
            D3 => 3,
            D4<string> => 4,
        };
    }

    public int M3<T>(C<T> c)
    {
        return c switch // exhaustive. Note that D1, D2, D3 are all valid here.
        {
            D1 => 1,
            D2 => 2,
            D3 => 3,
            D4<T> => 4,
            D5<T> => 5,
        };
    }

    public class E;

    public int M4<T>(C<T> c) where T : E
    {
        return c switch // exhaustive. The language doesn't stop us today from using D1, D2, D3, but, they're not necessary.
        {
            D4<E> => 1,
        };
    }
}
```

It feels like the following determinations should be made:

1) Do we want to support generic closed classes?

**Recommendation**: Leaning toward no to start with. Let's focus on getting support out for the important core scenarios, and leave space to add this in response to user feedback.

2) If yes to (1), what rule should be used to determine the set of closed subtypes?

**Recommendation**: Use the following rule.

The set of subtype declarations `D` of a closed type `C` is determined in the following way:

If `C` is not generic (`C` and its containing type(s) do not have any type parameters), then the set of subtypes includes all class declarations (i.e. original definitions) whose base type is `C`.  
**Note:** the [finiteness rule](#type-parameter-restriction) indicates that the subtype of a non-generic closed type is never generic. So, this set will be complete.

If `C` is a generic type (Note: this could perhaps be cleaned up/simplified):
- Let `C₀` be the original definition of `C`.
- Let `D₀` be the set of all subtype declarations in the same module as `C₀` whose base type is a construction of `C₀`.
- For each subtype `S₀` in `D₀`, determine a subtype `S` which may be a member of the set of subtypes of `C`:
  - If `S₀` is not generic, and `S₀`'s base type is `C`, then `S₀` is a member of `D`.
  - If `S₀` is generic, then perform a [*generic subtype inference*](#generic-subtype-inference) from `S₀` to `C`. If the inference succeeds, then the resulting inferred type `S` is a member of `D`.

#### Generic subtype inference

In this scenario we have a constructed closed type `C`, and a "candidate subtype definition" `S₀`.  
Let the base type of `S₀` be `Cₛ`. Let `T1, T2, ..., Tn` be the set of type parameters of `S₀`.
Perform an inference like the following:

```cs
// - type parameter list has the same arity as `S₀`
// - the finiteness rule says that every type parameter of `S₀` must be used in `Cₛ`
void M<T1, T2, ..., Tn>(Cₛ s) { }

// The following method type argument inference, if it succeeds,
// results in a set of type arguments which can be substituted into `S₀` to yield the subtype `S` of `C`.
M(new C());
```
