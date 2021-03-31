# Static virtual members in interfaces

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

# Summary

An interface is allowed to specify virtual static members that implementing classes and structs are then required to provide an explicit or implicit implementation of. The members can be accessed off of type parameters that are constrained by the interface.

# Motivation
[motivation]: #motivation

There is currently no way to abstract over static members and write generalized code that applies across types that define those static members. This is particularly problematic for member kinds that *only* exist in a static form, notably operators.

This feature allows generic algorithms over numeric types, represented by interface constraints that specify the presence of given operators. The algorithms can therefore be expressed in terms of such operators:

``` c#
// Interface specifies static properties and operators
interface IAddable<T> where T : IAddable<T>
{
    static virtual T Zero { get; } => default(T);
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

The feature would allow static interface members to be declared `abstract` or `virtual`. 

### Today's rules
Today, instance members in interfaces are implicitly abstract (or virtual if they have a default implementation), but can optionally have an `abstract` (or `virtual`) modifier. Non-virtual instance members must be explicitly marked as `sealed`. 

Static interface members today are implicitly non-virtual, and do not allow `abstract`, `virtual` or `sealed` modifiers.

### Proposal

#### Static virtual members
Static interface members other than fields are allowed to have an `abstract` or `virtual` modifier.

Static virtual members declared `abstract` are not allowed to have a body (or in the case of properties, the accessors are not allowed to have a body). 

Static virtual members declared `virtual` must provide a default implementation in the form of a member body (or in the case of properties, accessor bodies).

``` c#
interface I1<T> where T : I1<T>
{
    static abstract void M1();
    static virtual void M2() => Console.WriteLine("Default behavior");
    
    static abstract T P1 { get; set; }
    static virtual T P2 { get => T.P1; set => T.P1 = value; }
    
    static abstract event Action E1;
    static virtual event Action E2 { add => T.E1 += value; remove => T.E1 -= value; }
    
    static abstract T operator +(T l, T r);
    static virtual T operator -(T l, T r) => l;
    static abstract bool operator ==(T l, T r);
    static virtual bool operator !=(T l, T r) => !(l == r);
    static abstract implicit operator T(string s);
    static virtual explicit operator string(T t) => t.ToString();
}
```

Default implementations cannot be in the form of auto-properties or field-like events.

***Open question:** Non-virtual operators `==` and `!=` as well as the implicit and explicit conversion operators are disallowed in interfaces today. Should they be disallowed as virtual members?*

#### Static virtual member overrides
A derived interface can override an inherited static virtual member by declaring it with a qualified name, and providing either a default implementation or an `abstract` modifier.

``` c#
interface I2<T> : I1<T> where T : I2<T>
{
    static void I1<T>.M1() => Console.WriteLine("Default behavior");
    static abstract void I1<T>.M2();
    
    static T I1<T>.P1 { get => T.P2; set => T.P2 = value; }
    static abstract T I1<T>.P2 { get; set; }
    
    static event Action I1<T>.E1 { add => T.E2 += value; remove => T.E2 -= value; }
    static abstract event Action I1<T>.E2;
    
    static T I1<T>.operator +(T l, T r) => r;
    static abstract T I1<T>.operator -(T l, T r);
}
```

***Open question:** Should the qualifying `I1<T>.` go before the `operator` keyword or the operator symbol `+` and `-` itself?* I've chosen the former in the examples, as the latter doesn't work well with conversion operators.

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

Classes and structs can implement virtual instance members of interfaces either implicitly or explicitly. An implicitly implemented interface member is a normal (virtual or non-virtual) member declaration of the class or struct that just "happens" to also implement the interface member. The member can even be inherited from a base class and thus not even be present in the class declaration.

An explicitly implemented interface member uses a qualified name to identify the interface member in question. The implementation is not directly accessible as a member on the class or struct, but only through the interface.

### Proposal

No new syntax is needed in classes and structs to facilitate implicit implementation of static virtual interface members. Existing static member declarations serve that purpose.

Explicit implementations of static virtual interface members use a qualified name along with the `static` modifier.

``` c#
class C : I2<C>
{
    string _s;
    public C(string s) => _s = s;
    static void I2<C>.M() => Console.WriteLine("Implementation");
    static C I2<C>.P { get; set; }
    static event Action I2<C>.E;
    static C I2<C>.operator +(C l, C r) => new C($"{l._s} {r._s}");
    static bool I2<C>.operator ==(C l, C r) => l._s == r._s;
    static bool I2<C>.operator !=(C l, C r) => l._s != r._s;
    static implicit I2<C>.operator C(string s) => new C(s);
    static explicit I2<C>.operator string(C c) => c._s;
}
```

If a static virtual member of the interface has a most specific default implementation, then the implementing class or struct does not need to implement the member.

***Open question:** Should the qualifying `I2<C>.` go before the `operator` keyword or the operator symbol (e.g. `+`) itself?* I've chosen the former here, as it also works for the conversion operators.

# Semantics

## Operator restrictions

Today all unary and binary operator declarations have some requirement involving at least one of their operands to be of type `T` or `T?`, where `T` is the instance type of the enclosing type.

These requirements need to be relaxed so that a restricted operand is allowed to be of a type parameter that is constrained to `T`.

***Open question:** Should we relax this further so that the restricted operand can be of any type that derives from, or has one of some set of implicit conversions to `T`?*

## Implementing static virtual members

The rules for when a static member declaration in a class or struct is considered to implement a static virtual interface member, and for what requirements apply when it does, are the same as for instance members.

***TBD:** There may be additional or different rules necessary here that we haven't yet thought of.*

## Interface constraints with static virtual members

Today, when an interface `I` is used as a generic constraint, any type `T` with an implicit reference or boxing conversion to `I` is considered to satisfy that constraint.

When `I` has static virtual members this needs to be further restricted so that `T` cannot itself be an interface.

For instance:

``` c#
// I2<T> and C as above
void M<T>() where T : I2<T> { ... }
M<C>();  // Allowed: C is not an interface
M<I2<C>>(); // Disallowed: I is an interface
```

## Accessing static virtual interface members

A static virtual interface member `M` may be accessed on a type parameter `T` using the expression `T.M` when `T` is constrained (directly or indirectly) by an interface `I` and `M` is an accessible static virtual member of `I`.

``` c#
T M<T>() where T : I2<T>
{
    T.M1();
    T t = T.P1;
    T.E1 += () => { };
    return t + T.P2;
}
```

At runtime, the actual member implementation used is the one that exists on the actual type provided as a type argument.

``` c#
C c = M<C>(); // The static members of C get called
```

# Drawbacks
[drawbacks]: #drawbacks

- "static virtual" is a new concept and will meaningfully add to the conceptual load of C#.
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

Another *additional* feature would be to allow static members to be declared abstract and virtual in classes as well. This runs into complicating factors around which types can satisfy constraints that contain static virtual members. It seems like it can be saved for later, if and when the need and the design insights occur.

# Unresolved questions
[unresolved]: #unresolved-questions

Called out above, but here's a list:

- Operators `==` and `!=` as well as the implicit and explicit conversion operators are disallowed in interfaces today. Should they be disallowed as static virtual members as well?
- Should the qualifying `I.` in an explicit operator implementation go before the `operator` keyword or the operator symbol (e.g. `+`) itself?
- Should we relax the operator restrictions further so that the restricted operand can be of any type that derives from, or has one of some set of implicit conversions to the enclosing type?

# Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2021/LDM-2021-02-08.md
- https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-29.md
