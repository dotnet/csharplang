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

[Methods](https://github.com/dotnet/csharplang/blob/master/spec/classes.md#methods)

The method's *type_parameters* are in scope throughout the *method_declaration*, and can be used to form types throughout that scope in *return_type*, *method_body*, and *type_parameter_constraints_clauses* but not in *attributes*, except within a `nameof` expression in *attributes*.

[Method parameters](https://github.com/dotnet/csharplang/blob/master/spec/classes.md#method-parameters)

Names are introduced into this declaration space by the type parameter list and the formal parameter list of the method in `nameof` expressions in attributes placed on the method or its parameters.

Within a `nameof` expression in attributes placed on the method or its parameters, formal parameters can be referenced by their identifiers in *simple_name* expressions.

[Simple names](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#simple-names)

TODO

[Scopes](https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#scopes)

## Related spec sections
- [Corresponding parameters](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#corresponding-parameters)
