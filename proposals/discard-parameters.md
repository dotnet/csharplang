# Discard parameters

## Summary

Allow discards (`_`) to be used as parameters.
For example:
- lambdas: `(_, _) => 0`, `(int _, int _) => 0`
- anonymous methods: `delegate(int _, int _) { return 0; }`
- local functions: `void M() { void local(int _, int _) => ...; ... }`
- methods: `override void M(int _, int _) { ... }`
- indexers, constructors
- delegate types: `delegate void MyDelegate(int _, int _);`

## Motivation

Unused parameters do not need to be named. The intent of discards is clear, i.e. they are unused/discarded.

## Detailed design

In a parameter list with more than one parameter named `_`, such parameters are discard parameters.
Note: if a single parameter is named `_` then it is a regular parameter for backwards compatibility reasons.

Discard parameters are not in scope in the body of the method, they do not introduce any names.
In a method invocation, a named argument never corresponds to a discard parameter.

