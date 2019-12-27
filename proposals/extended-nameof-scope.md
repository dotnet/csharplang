# Extended `nameof` scope

## Summary

Allow `nameof(parameter)` inside an attribute on a method or parameter.
For example:
- `[MyAttribute(nameof(parameter))] void M(int parameter) { }`
- `[MyAttribute(nameof(TParameter))] void M<TParameter>() { }`
- `void M(int parameter, [MyAttribute(nameof(parameter))] int other) { }`

## Motivation

Attributes like `NotNullWhen` or `CallerExpression` need to refer to parameters, but those parameters are currently not in scope.

## Detailed design

TODO

[Method parameters](https://github.com/dotnet/csharplang/blob/master/spec/classes.md#method-parameters)

[Simple names](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#simple-names)

[Scopes](https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#scopes)

## Related spec sections
- [Corresponding parameters](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#corresponding-parameters)
