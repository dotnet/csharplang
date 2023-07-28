# Method group improvements

[!INCLUDE[Specletdisclaimer](speclet-disclaimer.md)]

## Summary
[summary]: #summary

This proposal refines the determination of the natural type of a method group in a few ways:
1. Consider candidate methods scope-by-scope (instance methods first, then each scope subsequent scope of extension methods)
2. Prune candidates that have no chance of succeeding, so they don't interfere with determining a unique signature:
    - Prune generic instance methods when no type arguments are provided (`var x = M;`)
    - Prune generic extension methods based on being able to reduce the extension and on constraints

It also allows the natural type of a method group to contribute to method type inference

## Context on method group natural type

In C# 10, method groups gained a weak natural type.  
That type is a "weak type" in that it only comes into play when the method group is not target-typed (ie. it plays no role in `System.Action a = MethodGroup;`).  
That weak natural type allows scenarios like `var x = MethodGroup;`.

For reference:
https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/lambda-improvements.md#natural-function-type

> A method group has a natural type if all candidate methods in the method group have a common signature. (If the method group may include extension methods, the candidates include the containing type and all extension method scopes.)

In practice, this means that we:
1. Construct the set of all candidate methods:
  - methods on the relevant type are in the set if they are static and the receiver is a type, or if they are non-static and the receiver is a value
  - extension methods (across all scopes) that can be reduced are in the set
3. If the signatures of all the candidates do not match, then the method group doesn't have a natural type
4. If the arity of the resulting signature doesn't match the number of provided type arguments, then the method group doesn't have a natural type
5. Otherwise, the resulting signature is used as the natural type

## Context on method type inference

The current [type inference rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1263-type-inference) allow 
- method groups to contribute to output type inference
- infering bounds for the `Ui` type parameters in `C<U1 ... Uk>` when given a `C<V1...Vk>` and `C` is a class, struct, interface or delegate type.  
But they don't allow method groups to contribute to bounds.

```
Test(IsEven); // Error CS0411	The type arguments for method 'Program.Test<T>(Func<T, bool>)' cannot be inferred from the usage. Try specifying the type arguments explicitly.

partial class Program
{
    public static bool IsEven(int x) => x % 2 == 0;
    public static void Test<T>(Func<T, bool> predicate) { }
}
```

## Proposal

### Method group natural type

1. For each scope, we construct the set of all candidate methods:
  - for the initial scope, methods on the relevant type with arity matching the provided type arguments and satisfying constraints with the provided type arguments are in the set if they are static and the receiver is a type, or if they are non-static and the receiver is a value
  - for subsequent scopes, extension methods in that scope that can be substituted with the provided type arguments and reduced using the value of the receiver while satisfying contstraints are in the set
  1. If we have no candidates in the given scope, proceed to the next scope.
  2. If the signatures of all the candidates do not match, then the method group doesn't have a natural type
  3. Otherwise, resulting signature is used as the natural type
2. If the scopes are exhausted, then the method group doesn't have a natural type

### Type inference
[inference]: #inference

We modify the [explicit parameter type inference rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12638-explicit-parameter-type-inferences) to not just apply to explicitly-typed lambdas, but also to method groups:

An *explicit parameter type inference* is made *from* an expression `E` *to* a type `T` in the following way:

- If `E` is an explicitly typed anonymous function \***or method group with a unique signature** with parameter types `U₁...Uᵥ` and `T` is a delegate type or expression tree type with parameter types `V₁...Vᵥ` then for each `Uᵢ` an *exact inference* is made *from* `Uᵢ` *to* the corresponding `Vᵢ`.

----

Relates to scope-by-scope proposal: https://github.com/dotnet/csharplang/issues/7364
Relates to proposal to better handle generic extension methods: https://github.com/dotnet/roslyn/issues/69222
Relates to issue for method groups contributing to method type inference: https://github.com/dotnet/csharplang/discussions/129
