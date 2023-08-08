# Method group natural type improvements

[!INCLUDE[Specletdisclaimer](speclet-disclaimer.md)]

## Summary
[summary]: #summary

This proposal refines the determination of the natural type of a method group in a few ways:
1. Consider candidate methods scope-by-scope (instance methods first, then each scope subsequent scope of extension methods)
2. Prune candidates that have no chance of succeeding, so they don't interfere with determining a unique signature:
    - Prune generic instance methods when no type arguments are provided (`var x = M;`)
    - Prune generic extension methods based on being able to reduce the extension and on constraints

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

## Proposal

The principle is to go scope-by-scope and prune candidates that we know cannot succeed as early as possible (same principle used in overload resolution).

1. For each scope, we construct the set of all candidate methods:
  - for the initial scope, methods on the relevant type with arity matching the provided type arguments and satisfying constraints with the provided type arguments are in the set if they are static and the receiver is a type, or if they are non-static and the receiver is a value
  - for subsequent scopes, extension methods in that scope that can be substituted with the provided type arguments and reduced using the value of the receiver while satisfying contstraints are in the set
  1. If we have no candidates in the given scope, proceed to the next scope.
  2. If the signatures of all the candidates do not match, then the method group doesn't have a natural type
  3. Otherwise, resulting signature is used as the natural type
2. If the scopes are exhausted, then the method group doesn't have a natural type

----

Relates to scope-by-scope proposal: https://github.com/dotnet/csharplang/issues/7364
Relates to proposal to better handle generic extension methods: https://github.com/dotnet/roslyn/issues/69222

