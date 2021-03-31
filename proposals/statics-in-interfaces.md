# Static abstract members in interfaces

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

# Summary

An interface is allowed to specify abstract static members that implementing classes and structs are then required to provide an explicit or implicit implementation of. The members can be accessed off of type parameters that are constrained by the interface.

# Motivation
[motivation]: #motivation

There is currently no way to abstract over static members and write generalized code that applies across types that define those static members. This is particularly problematic for member kinds that *only* exist in a static form, notably operators.

This feature allows generic algorithms over numeric types, represented by interface constraints that specify the presence of given operators. The algorithms can therefore be expressed in terms of such operators:

``` c#
// Interface specifies static properties and operators
interface IAddable<T> where T : IAddable<T>
{
    static abstract T Zero { get; }
    static abstract T operator +(T t1, T t2);
}

// Classes and structs (including built-ins) can implement interface
struct Int32 : …, IAddable<Int32>
{
    static Int32 I.operator +(Int32 x, Int32 y) => x + y; // Explicit
    public static int Zero => 0;                          // Implicit
}

// Generic algorithms can use static members on T
public static T AddAll<T>(T[] ts) where T : IAddable<T>
{
    T result = T.Zero;                   // Call static operator
    foreach (T t in ts) { result += t; } // Use `+`
    return result;
}

// Generic method can be applied to built-in and user-defined types
int sixtyThree = AddAll(new [] { 1, 2, 4, 8, 16, 32 });
```

# Syntax

## Interface members

The feature would allow static interface members to be declared virtual. 

### Today's rules
Today, instance members in interfaces are implicitly abstract (or virtual if they have a default implementation), but can optionally have an `abstract` (or `virtual`) modifier. Non-virtual instance members must be explicitly marked as `sealed`. 

Static interface members today are implicitly non-virtual, and do not allow `abstract`, `virtual` or `sealed` modifiers.

### Proposal

#### Abstract virtual members
Static interface members other than fields are allowed to also have the `abstract` modifier. Abstract static members are not allowed to have a body (or in the case of properties, the accessors are not allowed to have a body). 

``` c#
interface I<T> where T : I<T>
{
    static abstract void M();
    static abstract T P { get; set; }
    static abstract event Action E;
    static abstract T operator +(T l, T r);
}
```

***Open question:** Operators `==` and `!=` as well as the implicit and explicit conversion operators are disallowed in interfaces today. Should they be allowed?*

#### Explicitly non-virtual static members
For symmetry with non-virtual instance members, static members should be allowed an optional `sealed` modifier, even though they are non-virtual by default:

``` c#
interface I0
{
    static sealed void M() => Console.WriteLine("Default behavior");
    
    static sealed int f = 0;
    
    static sealed int P1 { get; set; }
    static sealed int P2 { get => f; set => f = value; }
    
    static sealed event Action E1;
    static sealed event Action E2 { add => E1 += value; remove => E1 -= value; }
    
    static sealed I0 operator +(I0 l, I0 r) => l;
}
```

## Implementation of interface members

### Today's rules

Classes and structs can implement abstract instance members of interfaces either implicitly or explicitly. An implicitly implemented interface member is a normal (virtual or non-virtual) member declaration of the class or struct that just "happens" to also implement the interface member. The member can even be inherited from a base class and thus not even be present in the class declaration.

An explicitly implemented interface member uses a qualified name to identify the interface member in question. The implementation is not directly accessible as a member on the class or struct, but only through the interface.

### Proposal

No new syntax is needed in classes and structs to facilitate implicit implementation of static abstract interface members. Existing static member declarations serve that purpose.

Explicit implementations of static abstract interface members use a qualified name along with the `static` modifier.

``` c#
class C : I<C>
{
    static void I.M() => Console.WriteLine("Implementation");
    static C I.P { get; set; }
    static event Action I.E;
    static C I.operator +(C l, C r) => r;
}
```

***Open question:** Should the qualifying `I.` go before the `operator` keyword or the operator symbol `+` itself?* I've chosen the former here. The latter may clash if we choose to allow conversion operators.

# Semantics

## Operator restrictions

Today all unary and binary operator declarations have some requirement involving at least one of their operands to be of type `T` or `T?`, where `T` is the instance type of the enclosing type.

These requirements need to be relaxed so that a restricted operand is allowed to be of a type parameter that is constrained to `T`.

***Open question:** Should we relax this further so that the restricted operand can be of any type that derives from, or has one of some set of implicit conversions to `T`?*

## Implementing static abstract members

The rules for when a static member declaration in a class or struct is considered to implement a static abstract interface member, and for what requirements apply when it does, are the same as for instance members.

***TBD:** There may be additional or different rules necessary here that we haven't yet thought of.*

## Interface constraints with static abstract members

Today, when an interface `I` is used as a generic constraint, any type `T` with an implicit reference or boxing conversion to `I` is considered to satisfy that constraint.

When `I` has static abstract members this needs to be further restricted so that `T` cannot itself be an interface.

For instance:

``` c#
// I and C as above
void M<T>() where T : I<T> { ... }
M<C>();  // Allowed: C is not an interface
M<I<C>>(); // Disallowed: I is an interface
```

## Accessing static abstract interface members

A static abstract interface member `M` may be accessed on a type parameter `T` using the expression `T.M` when `T` is constrained by an interface `I` and `M` is an accessible static abstract member of `I`.

``` c#
T M<T>() where T : I<T>
{
    T.M();
    T t = T.P;
    T.E += () => { };
    return t1 + T.P;
}
```

At runtime, the actual member implementation used is the one that exists on the actual type provided as a type argument.

``` c#
C c = M<C>(); // The static members of C get called
```

## Variance safety
https://github.com/dotnet/csharplang/blob/main/spec/interfaces.md#variance-safety

Variance safety rules should apply to signatures of static abstract members. The addition proposed in
https://github.com/dotnet/csharplang/blob/main/proposals/variance-safety-for-static-interface-members.md#variance-safety
should be adjusted from

*These restrictions do not apply to ocurrances of types within declarations of static members.* 

to

*These restrictions do not apply to ocurrances of types within declarations of **non-virtual, non-abstract** static members.*


# Drawbacks
[drawbacks]: #drawbacks

- "static abstract" is a new concept and will meaningfully add to the conceptual load of C#.
- It's not a cheap feature to build. We should make sure it's worth it.

# Alternatives
[alternatives]: #alternatives

## Structural constraints

An alternative approach would be to have "structural constraints" directly and explicitly requiring the presence of specific operators on a type parameter. The drawbacks of that are:
    - This would have to be written out every time. Having a named constraint seems better.
    - This is a whole new kind of constraint, whereas the proposed feature utilizes the existing concept of interface constraints.
    - It would only work for operators, not (easily) other kinds of static members.
    
## Default implementations

An *additional* feature to this proposal is to allow static virtual members in interfaces to have default implementations, just as instance virtual members do. We're investigating this, but the semantics get very complicated: default implementations will want to call other static virtual members, but what syntax, semantics and implementation strategies should we use to ensure that those calls can in turn be virtual?

This seems like a further improvement that can be done independently later, if the need and the solutions arise.

## Virtual static members in classes

Another *additional* feature would be to allow static members to be abstract and virtual in classes as well. This runs into similar complicating factors as the default implementations, and again seems like it can be saved for later, if and when the need and the design insights occur.

# Unresolved questions
[unresolved]: #unresolved-questions

Called out above, but here's a list:

- Operators `==` and `!=` as well as the implicit and explicit conversion operators are disallowed in interfaces today. Should they be allowed?
- Should the qualifying `I.` in an explicit operator implenentation go before the `operator` keyword or the operator symbol (e.g. `+`) itself?
- Should we relax the operator restrictions further so that the restricted operand can be of any type that derives from, or has one of some set of implicit conversions to the enclosing type?

# Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2021/LDM-2021-02-08.md
- https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-29.md
