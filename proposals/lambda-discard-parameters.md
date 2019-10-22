# Lambda discard parameters

## Summary

Allow discards (`_`) to be used as lambda parameters.
For example:
- `(_, _) => 0`
- `(int _, int _) => 0`
- `delegate(int _, int _) { return 0; }`

## Motivation

Unused lambda parameters do not need to be named. The intent of discards is clear, i.e. they are unused/discarded.

## Detailed design

In a lambda or delegate declaration with more than one parameter named `_`, those parameters are discard parameters.

Discard parameters are not in scope in the body of the lambda or delegate. So they cannot be accessed.

Note: if a single parameter is named `_` then it is a regular parameter for backwards compatibility reasons.
