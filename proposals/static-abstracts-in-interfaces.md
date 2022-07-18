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

#### Abstract static members
Static interface members other than fields are allowed to also have the `abstract` modifier. Abstract static members are not allowed to have a body (or in the case of properties, the accessors are not allowed to have a body). 

``` c#
interface I<T> where T : I<T>
{
    static abstract void M();
    static abstract T P { get; set; }
    static abstract event Action E;
    static abstract T operator +(T l, T r);
    static abstract bool operator ==(T l, T r);
    static abstract bool operator !=(T l, T r);
    static abstract implicit operator T(string s);
    static abstract explicit operator string(T t);
}
```

#### Virtual static members
Static interface members other than fields are allowed to also have the `virtual` modifier. Virtual static members are required to have a body. 

``` c#
interface I<T> where T : I<T>
{
    static virtual void M() {}
    static virtual T P { get; set; }
    static virtual event Action E;
    static virtual T operator +(T l, T r) { throw new NotImplementedException(); }
}
```

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
    string _s;
    public C(string s) => _s = s;
    static void I<C>.M() => Console.WriteLine("Implementation");
    static C I<C>.P { get; set; }
    static event Action I<C>.E;
    static C I<C>.operator +(C l, C r) => new C($"{l._s} {r._s}");
    static bool I<C>.operator ==(C l, C r) => l._s == r._s;
    static bool I<C>.operator !=(C l, C r) => l._s != r._s;
    static implicit I<C>.operator C(string s) => new C(s);
    static explicit I<C>.operator string(C c) => c._s;
}
```

# Semantics

## Operator restrictions

Today all unary and binary operator declarations have some requirement involving at least one of their operands to be of type `T` or `T?`, where `T` is the instance type of the enclosing type.

These requirements need to be relaxed so that a restricted operand is allowed to be of a type parameter that counts as "the instance type of the enclosing type".

In order for a type parameter `T` to count as " the instance type of the enclosing type", it must meet the following requirements:
- `T` is a direct type parameter on the interface in which the operator declaration occurs, and
- `T` is *directly* constrained by what the spec calls the "instance type" - i.e. the surrounding interface with its own type parameters used as type arguments.

## Equality operators and conversions

Abstract/virtual declarations of `==` and `!=` operators, as well as abstract/virtual declarations of implicit and explicit conversion operators will be allowed in interfaces. Derived interfaces will be allowed to implement them too.

For `==` and `!=` operators, at least one parameter type must be a type parameter that counts as "the instance type of the enclosing type", as defined in the previous section. 

## Implementing static abstract members

The rules for when a static member declaration in a class or struct is considered to implement a static abstract interface member, and for what requirements apply when it does, are the same as for instance members.

***TBD:** There may be additional or different rules necessary here that we haven't yet thought of.*

## Interfaces as type arguments

We discussed the issue raised by https://github.com/dotnet/csharplang/issues/5955 and decided to add a restriction around usage of an interface as a type argument (https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-28.md#type-hole-in-static-abstracts). Here is the restriction as it was proposed by https://github.com/dotnet/csharplang/issues/5955 and approved by the LDM.

An interface containing or inheriting a static abstract/virtual member that does not have most specific implementation in the interface cannot be used as a type argument. If all static abstract/virtual members have most specific implementation, the interface can be used as a type argument.

## Accessing static abstract interface members

A static abstract interface member `M` may be accessed on a type parameter `T` using the expression `T.M` when `T` is constrained by an interface `I` and `M` is an accessible static abstract member of `I`.

``` c#
T M<T>() where T : I<T>
{
    T.M();
    T t = T.P;
    T.E += () => { };
    return t + T.P;
}
```

At runtime, the actual member implementation used is the one that exists on the actual type provided as a type argument.

``` c#
C c = M<C>(); // The static members of C get called
```

Since query expressions are spec'ed as a syntactic rewrite, C# actually lets you use a *type* as the query source, as long as it has static members for the query operators you use! In other words, if the *syntax* fits, we allow it!
We think this behavior was not intentional or important in the original LINQ, and we don't want to do the work to support it on type parameters. If there are scenarios out there we will hear about them, and can choose to embrace this later.

## Variance safety [§17.2.3.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/interfaces.md#17232-variance-safety)

Variance safety rules should apply to signatures of static abstract members. The addition proposed in
https://github.com/dotnet/csharplang/blob/main/proposals/variance-safety-for-static-interface-members.md#variance-safety
should be adjusted from

*These restrictions do not apply to occurrences of types within declarations of static members.* 

to

*These restrictions do not apply to occurrences of types within declarations of **non-virtual, non-abstract** static members.*

## [§10.5.4](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#1054-user-defined-implicit-conversions) User defined implicit conversions

The following bullet points

- Determine the types `S`, `S₀` and `T₀`.
  - If `E` has a type, let `S` be that type.
  - If `S` or `T` are nullable value types, let `Sᵢ` and `Tᵢ` be their underlying types, otherwise let `Sᵢ` and `Tᵢ` be `S` and `T`, respectively.
  - If `Sᵢ` or `Tᵢ` are type parameters, let `S₀` and `T₀` be their effective base classes, otherwise let `S₀` and `T₀` be `Sₓ` and `Tᵢ`, respectively.
- Find the set of types, `D`, from which user-defined conversion operators will be considered. This set consists of `S0` (if `S0` is a class or struct), the base classes of `S0` (if `S0` is a class), and `T0` (if `T0` is a class or struct).
- Find the set of applicable user-defined and lifted conversion operators, `U`. This set consists of the user-defined and lifted implicit conversion operators declared by the classes or structs in `D` that convert from a type encompassing `S` to a type encompassed by `T`. If `U` is empty, the conversion is undefined and a compile-time error occurs.

are adjusted as follows:

- Determine the types `S`, `S₀` and `T₀`.
  - If `E` has a type, let `S` be that type.
  - If `S` or `T` are nullable value types, let `Sᵢ` and `Tᵢ` be their underlying types, otherwise let `Sᵢ` and `Tᵢ` be `S` and `T`, respectively.
  - If `Sᵢ` or `Tᵢ` are type parameters, let `S₀` and `T₀` be their effective base classes, otherwise let `S₀` and `T₀` be `Sₓ` and `Tᵢ`, respectively.
- Find the set of applicable user-defined and lifted conversion operators, `U`. 
  - Find the set of types, `D1`, from which user-defined conversion operators will be considered. This set consists of `S0` (if `S0` is a class or struct), the base classes of `S0` (if `S0` is a class), and `T0` (if `T0` is a class or struct).
  - Find the set of applicable user-defined and lifted conversion operators, `U1`. This set consists of the user-defined and lifted implicit conversion operators declared by the classes or structs in `D1` that convert from a type encompassing `S` to a type encompassed by `T`.
  - If `U1` is not empty, then `U` is `U1`. Otherwise,
    - Find the set of types, `D2`, from which user-defined conversion operators will be considered. This set consists of `Sᵢ` *effective interface set* and their base interfaces (if `Sᵢ` is a type parameter), and `Tᵢ` *effective interface set* (if `Tᵢ` is a type parameter).
    - Find the set of applicable user-defined and lifted conversion operators, `U2`. This set consists of the user-defined and lifted implicit conversion operators declared by the interfaces in `D2` that convert from a type encompassing `S` to a type encompassed by `T`.
    - If `U2` is not empty, then `U` is `U2`
- If `U` is empty, the conversion is undefined and a compile-time error occurs.

## [§10.5.5](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#1055-user-defined-explicit-conversions)  User-defined explicit conversions

The following bullet points

- Determine the types `S`, `S₀` and `T₀`.
  - If `E` has a type, let `S` be that type.
  - If `S` or `T` are nullable value types, let `Sᵢ` and `Tᵢ` be their underlying types, otherwise let `Sᵢ` and `Tᵢ` be `S` and `T`, respectively.
  - If `Sᵢ` or `Tᵢ` are type parameters, let `S₀` and `T₀` be their effective base classes, otherwise let `S₀` and `T₀` be `Sᵢ` and `Tᵢ`, respectively.
- Find the set of types, `D`, from which user-defined conversion operators will be considered. This set consists of `S0` (if `S0` is a class or struct), the base classes of `S0` (if `S0` is a class), `T0` (if `T0` is a class or struct), and the base classes of `T0` (if `T0` is a class).
- Find the set of applicable user-defined and lifted conversion operators, `U`. This set consists of the user-defined and lifted implicit or explicit conversion operators declared by the classes or structs in `D` that convert from a type encompassing or encompassed by `S` to a type encompassing or encompassed by `T`. If `U` is empty, the conversion is undefined and a compile-time error occurs.

are adjusted as follows:

- Determine the types `S`, `S₀` and `T₀`.
  - If `E` has a type, let `S` be that type.
  - If `S` or `T` are nullable value types, let `Sᵢ` and `Tᵢ` be their underlying types, otherwise let `Sᵢ` and `Tᵢ` be `S` and `T`, respectively.
  - If `Sᵢ` or `Tᵢ` are type parameters, let `S₀` and `T₀` be their effective base classes, otherwise let `S₀` and `T₀` be `Sᵢ` and `Tᵢ`, respectively.
- Find the set of applicable user-defined and lifted conversion operators, `U`.
  - Find the set of types, `D1`, from which user-defined conversion operators will be considered. This set consists of `S0` (if `S0` is a class or struct), the base classes of `S0` (if `S0` is a class), `T0` (if `T0` is a class or struct), and the base classes of `T0` (if `T0` is a class).
  - Find the set of applicable user-defined and lifted conversion operators, `U1`. This set consists of the user-defined and lifted implicit or explicit conversion operators declared by the classes or structs in `D1` that convert from a type encompassing or encompassed by `S` to a type encompassing or encompassed by `T`.
  - If `U1` is not empty, then `U` is `U1`. Otherwise,
    - Find the set of types, `D2`, from which user-defined conversion operators will be considered. This set consists of `Sᵢ` *effective interface set* and their base interfaces (if `Sᵢ` is a type parameter), and `Tᵢ` *effective interface set* and their base interfaces (if `Tᵢ` is a type parameter).
    - Find the set of applicable user-defined and lifted conversion operators, `U2`. This set consists of the user-defined and lifted implicit or explicit conversion operators declared by the interfaces in `D2` that convert from a type encompassing or encompassed by `S` to a type encompassing or encompassed by `T`.
    - If `U2` is not empty, then `U` is `U2`
- If `U` is empty, the conversion is undefined and a compile-time error occurs.

## Default implementations

An *additional* feature to this proposal is to allow static virtual members in interfaces to have default implementations, just as instance virtual/abstract members do. 

One complication here is that default implementations would want to call other static virtual members "virtually". Allowing static virtual members to be called directly on the interface would require flowing a hidden type parameter representing the "self" type that the current static method really got invoked on. This seems complicated, expensive and potentially confusing.

We discussed a simpler version which maintains the limitations of the current proposal that static virtual members can *only* be invoked on type parameters. Since interfaces with static virtual members will often have an explicit type parameter representing a "self" type, this wouldn't be a big loss: other static virtual members could just be called on that self type. This version is a lot simpler, and seems quite doable.

At https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-24.md#default-implementations-of-abstract-statics we decided to support Default Implementations of static members following/expanding the rules established in https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/default-interface-methods.md accordingly.  

## Pattern matching

Given the following code, a user might reasonably expect it to print True (as it would if the constant pattern was written inline):

```cs
M(1.0);

static void M<T>(T t) where T : INumberBase<T>
{
    Console.WriteLine(t is 1);
}
```

However, because the input type of the pattern is not `double`, the constant `1` pattern will first type check the incoming `T` against `int`. This is unintuitive, so we will block it until a future C# version adds better handling for numeric matching against types derived from `INumberBase<T>`. To do so, we will say that, we will explicitly recognize `INumberBase<T>` as the type that all "numbers" will derive from, and block the pattern if we're trying to match a numeric constant pattern against a number type that we can't represent the pattern in (ie, a type parameter constrained to `INumberBase<T>`, or a user-defined number type that inherits from `INumberBase<T>`).

Formally, we add an exception to the definition of *pattern-compatible* for constant patterns:

> A constant pattern tests the value of an expression against a constant value. The constant may be any constant expression, such as a literal, the name of a declared `const` variable, or an enumeration constant. When the input value is not an open type, the constant expression is implicitly converted to the type of the matched expression; if the type of the input value is not *pattern-compatible* with the type of the constant expression, the pattern-matching operation is an error. **If the constant expression being matched against is a numeric value, the input value is a type that inherits from `System.Numerics.INumberBase<T>`, and there is no constant conversion from the constant expression to the type of the input value, the pattern-matching operation is an error.**

We also add a similar exception for relational patterns:

> When the input is a type for which a suitable built-in binary relational operator is defined that is applicable with the input as its left operand and the given constant as its right operand, the evaluation of that operator is taken as the meaning of the relational pattern. Otherwise we convert the input to the type of the expression using an explicit nullable or unboxing conversion. It is a compile-time error if no such conversion exists. **It is a compile-time error if the input type is a type parameter constrained to or a type inheriting from `System.Numerics.INumberBase<T>` and the input type has no suitable built-in binary relational operator defined.** The pattern is considered not to match if the conversion fails. If the conversion succeeds then the result of the pattern-matching operation is the result of evaluating the expression e OP v where e is the converted input, OP is the relational operator, and v is the constant expression.


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

# Unresolved questions
[unresolved]: #unresolved-questions

## Static abstract interfaces and static classes

See https://github.com/dotnet/csharplang/issues/5783 and https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-16.md#static-abstract-interfaces-and-static-classes for more information.

# Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2021/LDM-2021-02-08.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-05.md
- https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-29.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-24.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-16.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-28.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-04-06.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-06-06.md
