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

At first glance, handling generic closed types seems manageable. One key task the language needs to define, in order to check exhaustiveness,
is how to determine the "set of *possible subtypes*" of a generic type. For example:

```cs
closed class C<T>;
class D1 : C<int>;
class D2 : C<string>;
class D3<T> : C<T>;

class Program
{
    static int Match1(C<int> c)
        => c switch
        {
            D1 => 1,
            D3<int> => 3
        };

    static int Match2(C<string> c)
        => c switch
        {
            D2 => 2,
            D3<string> => 3
        };

    static int Match3<X>(C<X> c)
        => c switch
        {
            D1 => 1,
            D2 => 2,
            D3<X> => 3
        };
}
```

So far, so good. When a concrete `C<int>` is used, for example, we can require matching just the types which could possibly have `C<int>` as a base type.
Even when a generic `C<X>` is used, we can include all the subtypes whose base type could possibly *unify* with `C<X>`.

However, it seems like when a more complex generic subtype is used, then, the user can get into a situation where it's not possible to name all the types needed in order to exhaust the base type.
We previously introduced a [type parameter restriction](#type-parameter-restriction) to try and prevent similar kinds of situations.

```cs
closed class C<T>;
class D1<U1> : C<U1>;
class D2<U2> : C<ImmutableArray<U2>>;
class D3<U3> : C<U3> where U3 : IEnumerable<int>;

class Program
{
    static int Match<X>(C<X> c)
        => c switch
        {
            D1<X> => 1,
            // We know that if 'X' is some 'ImmutableArray<?>', then, it's possible that 'c' is a 'D2'.
            // But, we don't have the ability to speak that 'D2' in this context.

            // Similarly, 'D3<X>' could flow in, but we can't refer to it, because 'X' doesn't meet constraints of 'U3'.
            D3<X> => 3,
        };
}
```

It feels unclear whether this situation can be avoided, except by ensuring that all the type parameters on the subtype, are passed *directly* as type arguments to the base type, and for a base type parameter which has equivalent constraints.

This still may be not be enough to ensure that "the subtype can always be used where the base type is used", though.

Otherwise we can still get into even *further* cases where a subtype can't be used in the context of the base type usage, such as with accessibility:

```cs
class C;

class Container
{
    protected class D1 : C;
}

class Program
{
    static int Match<X>(C c)
        => c switch
        {
            // error CS0122: 'Container.D1' is inaccessible due to its protection level
            Container.D1 => 1
        };
}
```

This seems to lead to the following questions:

1) Do we want to further restrict the shape of generic subclasses of closed classes, in order to reduce the number of "inexhaustibility" situations?
2) Given the inability to keep a "complete" guardrail, do we still want the [type parameter restriction](#type-parameter-restriction)?

One other way of ensuring "the subtype can always be spoken", might be to require the subtype to be listed at the declaration with a `permits` clause or similar:
```cs
closed class C<T>
    permits D1<T>, D2<T>, D3<T>;

class D1<U1> : C<T>;

// The below declarations are essentially invalid as the 'permits' clause contradicts the base clause, and, there isn't an ability for permits clause to introduce new type parameters or constraints.
class D2<U2> : C<ImmutableArray<U2>>;
class D3<U3> : C<U3> where U3 : IEnumerable<int>;
```

### Generic subtype inference

Assuming that we will support generic closed classes, it feels like there is a need to specify how the language decides what the set of *possible subtypes* are of such types.

The following is a starting point for such a rule:

1) For a given closed type `C`, let `C₀` be its original definition.
2) For each subtype declaration `S₀` whose base type has original definition `C₀`, determine if a construction `S` exists which has base type `C`.
    - See also [§19.6.3 Uniqueness of implemented interfaces](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/interfaces.md#1963-uniqueness-of-implemented-interfaces) in the standard.
    - Question: do we need to specify *how* we determine if the construction exists? It doesn't look like we did this for interfaces.

### Interface convertibility of sealed classes

[§10.3.5 Explicit reference conversions](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#1035-explicit-reference-conversions) implies that the following cast is valid when a type is not `sealed`, and invalid when it is `sealed`:

```cs
var c = new C();
var i = (I)c; // error

sealed class C { }
interface I { }
```

It feels like we could possibly make the same determination about the implemented interfaces in a closed hierarchy:

```cs
var c = new C();
var i = (I)c; // error

closed class C { }
sealed class D1 : C { }
sealed class D2 : C { }
interface I { }
```

The question is: should we attempt to make this determination and introduce errors in certain scenarios accordingly?

If we did `closed interface`, a similar determination could perhaps be made that way also. In that case, the types which implement the interface would be known, regardless of the `sealed`-ness of the implementing types:

```cs
var c = new C();
var i = (I)c; // error: `C` isn't one of the types that we statically know implements `I`, so there's no conversion.

class C { }
closed interface I { }
```
