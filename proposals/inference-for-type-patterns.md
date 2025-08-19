# Target-typed inference for type patterns

*This proposal builds on the [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md) proposal.

## Summary

Generic type inference is extended to type patterns, which may omit a type argument list when it can be inferred from the pattern input value. For instance, given a declaration `Option<int> intOption`, instead of:

```csharp
if (intOption is Some<int> some) ...
```

You can simply write:

```csharp
if (intOption is Some some) ... // 'Some<int>' inferred from the type of 'intOption'
```

## Motivation

Type patterns can get unwieldy when the types are generic, which seems especially grating when the information to infer the type arguments is already available in context. 

For [closed hierarchies](https://github.com/dotnet/csharplang/blob/main/proposals/closed-hierarchies.md) and [unions](https://github.com/dotnet/csharplang/blob/main/proposals/nominal-type-unions.md) in particular, there are already rules in place that ensure that type arguments for a case type depend functionally on those of the closed class or union. This means that those type arguments are almost *guaranteed* to be inferrable when the input value is of a closed class or union type, and the type pattern is for a case type.

## Detailed specification

The proposal is specified by treating the type pattern "as if" it were a generic method, and the pattern application to the incoming value "as if" it were an invocation of that generic method with a target type of the incoming value.

In a type pattern `T ...` where `T` has no type arguments, if a non-generic `T` does not exist, or is not allowed in a pattern (e.g. it is static), or is not [pattern compatible](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/patterns.md#1122-declaration-pattern) with the input type `I`, then type inference is attempted:

For each generic type `T<X₁...Xᵥ>` with the same type name `T`, [generic type inference](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1263-type-inference) is performed as if the type pattern were a generic method with the same type parameter list as `T<X₁...Xᵥ>`, with an empty parameter list, and with T<X₁...Xᵥ> as its return type:

```csharp
T<X₁...Xᵥ> M<X₁...Xᵥ>()
```

and as if the pattern application were an invocation with no type arguments, with an empty argument list and with `I` as a target type:

```csharp
I i = M()
```

(Where the names `i` and `M` are otherwise invisible and not in conflict with any other names in scope.)

For instance, in the following example:

```csharp
public record None();
public record Some<T>(T value);
public union Option<T>(None, Some<T>);

void M(Option<int> intOption) => intOption switch
{
    None => ...,
    Some some => ..., // 'Some<int>' inferred for 'some'
}
```

Type inference proceeds as if with this method and invocation:

```csharp
Some<T> M<T>();

Option<int> i = M();
```

Which leads to `int` being inferred as the type argument corresponding to `T`.

If type inference succeeds and the inferred type arguments satisfy their constraints, then the type `T<X₁...Xᵥ>` is a candidate. If exactly one such candidate type is found, then that is the one inferred for use in the type pattern. Otherwise, inference fails and an error occurs. The type in the pattern must then be specified in full.