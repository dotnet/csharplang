# Lambda discard parameters

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary

Allow discards (`_`) to be used as parameters of lambdas and anonymous methods.
For example:
- lambdas: `(_, _) => 0`, `(int _, int _) => 0`
- anonymous methods: `delegate(int _, int _) { return 0; }`

## Motivation

Unused parameters do not need to be named. The intent of discards is clear, i.e. they are unused/discarded.

## Detailed design

Method parameters - [§14.6.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/classes.md#1462-method-parameters)
In the parameter list of a lambda or anonymous method with more than one parameter named `_`, such parameters are discard parameters.
Note: if a single parameter is named `_` then it is a regular parameter for backwards compatibility reasons.

Discard parameters do not introduce any names to any scopes.
Note this implies they do not cause any `_` (underscore) names to be hidden.

Simple names ([§11.7.4](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1174-simple-names))
If `K` is zero and the *simple_name* appears within a *block* and if the *block*'s (or an enclosing *block*'s) local variable declaration space (Declarations - [§7.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/basic-concepts.md#73-declarations)) contains a local variable, parameter (with the exception of discard parameters) or constant with name `I`, then the *simple_name* refers to that local variable, parameter or constant and is classified as a variable or value.

Scopes - [§7.7](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/basic-concepts.md#77-scopes)
With the exception of discard parameters, the scope of a parameter declared in a *lambda_expression* ([§11.16](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1116-anonymous-function-expressions)) is the *anonymous_function_body* of that *lambda_expression*
With the exception of discard parameters, the scope of a parameter declared in an *anonymous_method_expression* ([§11.16](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1116-anonymous-function-expressions)) is the *block* of that *anonymous_method_expression*.

## Related spec sections
- Corresponding parameters - [§11.6.2.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11622-corresponding-parameters)
