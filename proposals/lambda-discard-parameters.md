# Lambda discard parameters

## Summary

Allow discards (`_`) to be used as parameters of lambdas and anonymous methods.
For example:
- lambdas: `(_, _) => 0`, `(int _, int _) => 0`
- anonymous methods: `delegate(int _, int _) { return 0; }`

## Motivation

Unused parameters do not need to be named. The intent of discards is clear, i.e. they are unused/discarded.

## Detailed design

[Method parameters](https://github.com/dotnet/csharplang/blob/master/spec/classes.md#method-parameters)
In the parameter list of a lambda or anonymous method with more than one parameter named `_`, such parameters are discard parameters.
Note: if a single parameter is named `_` then it is a regular parameter for backwards compatibility reasons.

Discard parameters do not introduce any names to any scopes.
Note this implies they do not cause any `_` (underscore) names to be hidden.

[Simple names](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#simple-names)
If `K` is zero and the *simple_name* appears within a *block* and if the *block*'s (or an enclosing *block*'s) local variable declaration space ([Declarations](basic-concepts.md#declarations)) contains a local variable, parameter (with the exception of discard parameters) or constant with name `I`, then the *simple_name* refers to that local variable, parameter or constant and is classified as a variable or value.

## Related spec sections
[Scopes](https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#scopes)
[Corresponding parameters](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#corresponding-parameters)
