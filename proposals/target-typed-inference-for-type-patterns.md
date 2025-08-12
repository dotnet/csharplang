# Target-typed inference for type patterns

## Summary

Allow type patterns to omit a type argument list when it can be inferred from the pattern input value:

```csharp
void M(Option<int> option) => option switch
{
    Some(var i) => ..., // 'Some<int>' inferred
    ...
}
```

## Motivation

Specification of the whole type to be matched can be arduous. For [closed hierarchies](https://github.com/dotnet/csharplang/blob/main/proposals/closed-hierarchies.md) and [unions](https://github.com/dotnet/csharplang/blob/main/proposals/nominal-type-unions.md) in particular, there are already rules in place that ensure that type arguments for a case type depend functionally on those of the closed class or union, so those type arguments are almost guaranteed to be inferrable in the common scenario where the input value is of a closed class or union type, and the type pattern is for a case type.

## Detailed specification

In a type pattern `T ...` where `T` has no type arguments, if non-generic `T` does not exist, or is not allowed in a pattern (e.g. it is static), or is not [pattern compatible](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/patterns.md#1122-declaration-pattern) with the input type `I`, then type inference is attempted:

For each generic type with the same type name `T<X₁...Xᵥ>`, [generic type inference](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1263-type-inference) is is attempted, as if the type pattern were a generic method with the same type parameter list as `T<X₁...Xᵥ>`, with an empty parameter list, and with T<X₁...Xᵥ> as its return type:

```csharp
T<X₁...Xᵥ> M<X₁...Xᵥ>()
```

and as if the pattern application were an invocation with no type arguments, with an empty argument list and with `I` as a target type:

```csharp
I i = M()
```

Where the names `i` and `M` are otherwise invisible and not in conflict with any other names in scope.

For instance, for this declaration of and switch over the `Option<T>` type:

```csharp
public record None();
public record Some<T>(T value);
public union Option<T>(None, Some<T>);

void M(Option<int> option) => option switch
{
    None => ...,
    Some(var i) => ..., // 'Some<int>' inferred
}
```

Type inference would proceed as if with this method and invocation:

```csharp
Some<T> M<T>();

Option<int> i = M();
```

If type inference succeeds and the inferred type arguments satisfy their constraints, then the type `T<X₁...Xᵥ>` is a candidate with the inferred type arguments. If exactly one such type is found, then that is the one inferred for use in the type pattern. Otherwise, inference fails and an error occurs. The type in the pattern must then be specified in full.