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

The method's *type_parameters* are in scope throughout the *method_declaration*, and can be used to form types throughout that scope in *return_type*, *method_body*, and *type_parameter_constraints_clauses* but not in *attributes*, **except within a `nameof` expression in *attributes*.**

[Method parameters](https://github.com/dotnet/csharplang/blob/master/spec/classes.md#method-parameters)

A method declaration creates a separate declaration space for parameters, type parameters and local variables. Names are introduced into this declaration space by the type parameter list and the formal parameter list of the method and by local variable declarations in the block of the method.
**Names are introduced into this declaration space by the type parameter list and the formal parameter list of the method in `nameof` expressions in attributes placed on the method or its parameters.**

\[...]   
Within the block of a method, formal parameters can be referenced by their identifiers in simple_name expressions (Simple names).
**Within a `nameof` expression in attributes placed on the method or its parameters, formal parameters can be referenced by their identifiers in *simple_name* expressions.**

[Simple names](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#simple-names)

A *simple_name* is either of the form `I` or of the form `I<A1,...,Ak>`, where `I` is a single identifier and `<A1,...,Ak>` is an optional *type_argument_list*. When no *type_argument_list* is specified, consider `K` to be zero. The *simple_name* is evaluated and classified as follows:

- If `K` is zero and the *simple_name* appears within a block and if the block's (or an enclosing block's) local variable declaration space (Declarations) contains a local variable, parameter or constant with name `I`, then the *simple_name* refers to that local variable, parameter or constant and is classified as a variable or value.
- If `K` is zero and the *simple_name* appears within the body of a generic method declaration and if that declaration includes a type parameter with name `I`, then the *simple_name* refers to that type parameter.
- **If `K` is zero and the *simple_name* appears within a `nameof` expression in an attribute on the method declaration or its parameters and if that declaration includes a parameter or type parameter with name `I`, then the *simple_name* refers to that parameter or type parameter.**
- Otherwise, for each instance type `T` (The instance type), starting with the instance type of the immediately enclosing type declaration and continuing with the instance type of each enclosing class or struct declaration (if any):  
\[...]
- Otherwise, for each namespace `N`, starting with the namespace in which the *simple_name* occurs, continuing with each enclosing namespace (if any), and ending with the global namespace, the following steps are evaluated until an entity is located:  
\[...]
- Otherwise, the simple_name is undefined and a compile-time error occurs.

[Scopes](https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#scopes)

- The scope of a type parameter declared by a type_parameter_list on a method_declaration is \[...] **and `nameof` expressions in an attribute on the method declaration or its parameters.**
- The scope of a parameter declared in a method_declaration (Methods) is the *method_body* of that method_declaration **and `nameof` expressions in an attribute on the method declaration or its parameters.**

## Related spec sections
- [Declarations](https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#declarations)
