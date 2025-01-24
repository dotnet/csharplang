# Out variable declarations

Champion issue: <https://github.com/dotnet/csharplang/issues/60>

The *out variable declaration* feature enables a variable to be declared at the location that it is being passed as an `out` argument.

```antlr
argument_value
    : 'out' type identifier
    | ...
    ;
```

A variable declared this way is called an *out variable*. You may use the contextual keyword `var` for the variable's type. The scope will be the same as for a *pattern-variable* introduced via pattern-matching.

According to Language Specification (section 7.6.7 Element access) the argument-list of an element-access (indexing expression) does not contain ref or out arguments. However, they are permitted by the compiler for various scenarios, for example indexers declared in metadata that accept `out`.

Within the scope of a local variable introduced by an argument_value, it is a compile-time error to refer to that local variable in a textual position that precedes its declaration.

It is also an error to reference an implicitly-typed (§8.5.1) out variable in the same argument list that immediately contains its declaration.

Overload resolution is modified as follows:

We add a new conversion:

> There is a *conversion from expression* from an implicitly-typed out variable declaration to every type.

Also

> The type of an explicitly-typed out variable argument is the declared type.

and

> An implicitly-typed out variable argument has no type.

The *conversion from expression* from an implicitly-typed out variable declaration is not considered better than any other *conversion from expression*.

The type of an implicitly-typed out variable is the type of the corresponding parameter in the signature of the method selected by overload resolution.

The new syntax node `DeclarationExpressionSyntax` is added to represent the declaration in an out var argument.
