# Target-typed inference for constructor calls

*This proposal builds on the [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md) proposal.

## Summary

Generic type inference is extended to 'new' expressions, which may infer type arguments for the newly created class or struct, including [from a target type](target-typed-generic-type-inference) if present. For instance, given:

```csharp
public class MyCollection<T> : IEnumerable<T>
{
    public MyCollection() { ... }
    ...
}
```

We would allow the constructor to be called without a type argument when it can be inferred from arguments or (in this case) a target type:

```csharp
IEnumerable<string> c = new MyCollection(); // 'T' = 'string' inferred from target type
```

## Motivation

Even without [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md) constructors are at a disadvantage to factory methods, because type arguments cannot be inferred on invocation. This frequently leads to trivial factory methods being declared simply for the benefit of type inference. If target-typed inference is added, this convenience gap is only going to grow.

For constructor calls we already have "target type new" for when a target type is exactly the type to be created, but even when that's not the case, arguments or the target type often have sufficient information between them that the type arguments for the constructed type could be inferred.

This is frequently the case for [closed hierarchies](https://github.com/dotnet/csharplang/blob/main/proposals/closed-hierarchies.md) and [unions](https://github.com/dotnet/csharplang/blob/main/proposals/nominal-type-unions.md). Most commonly, a target type will be the closed class or union type itself, whereas the constructed type will be one of the case types:

```csharp
Option<string> option = new Some<string>("Hello");
```

With this proposal, it could be written simply as

```csharp
Option<string> option = new Some("Hello"); // Infer 'string' from argument and target type
```

## Detailed specification

The proposal is specified by treating the constructor "as if" it were a generic method, and the constructor call "as if" it were an invocation of that generic method.

In an *object_creation_expression* of the form `new T(E₁ ...Eₓ)` where `T` has no type argument list, if there is an accessible non-generic type `T` and it has one or more accessible and applicable constructors, then overload resolution proceeds as today.

However, if no such type or constructors are found, then for each generic type `T<X₁...Xᵥ>` with the same type name `T`, and for each constructor of that generic type with the parameter list `(T₁ p₁ ... Tₓ pₓ)`, [generic type inference](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1263-type-inference) is attempted, as if the constructor were a generic method `M` with the same type parameter list as `T<X₁...Xᵥ>`, with the same parameter list `(T₁ p₁ ... Tₓ pₓ)` as the constructor, and with T<X₁...Xᵥ> as its return type:

```csharp
T<X₁...Xᵥ> M<X₁...Xᵥ>(T₁ p₁ ... Tₓ pₓ)
```

and as if the object creation expression were an invocation with no type arguments, and with the same argument list and target type `I` (if any) as the creation expression:

```csharp
M(E₁ ...Eₓ) // without target type
I i = M(E₁ ...Eₓ) // with target type
```

(Where the names `i` and `M` are otherwise invisible and not in conflict with any other names in scope.)

For instance, for this type and invocation:

```csharp
public class C<T1, T2> : IEnumerable<T1>
{
    public C(T2 t2) { ... }
}

IEnumerable<string> l = new C(5);
```

Type inference would proceed as if with this method and invocation:

```csharp
C<T1, T2> M<T1, T2>(T2 t2);

IEnumerable<string> l = M(5);
```

If type inference succeeds, and the inferred type arguments satisfy their constraints, and the constructor is applicable when the type arguments are applied to its containing generic type, then the constructor is a candidate.

Overload resolution then proceeds as normal between the resulting candidate constructors.
