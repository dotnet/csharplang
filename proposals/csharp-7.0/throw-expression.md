# Throw expression

We extend the set of expression forms to include

```antlr
throw_expression
    : 'throw' null_coalescing_expression
    ;

null_coalescing_expression
    : throw_expression
    ;
```

The type rules are as follows:

- A *throw_expression* has no type.
- A *throw_expression* is convertible to every type by an implicit conversion.

A *throw expression* throws the value produced by evaluating the *null_coalescing_expression*, which must denote a value of the class type `System.Exception`, of a class type that derives from `System.Exception` or of a type parameter type that has `System.Exception` (or a subclass thereof) as its effective base class. If evaluation of the expression produces `null`, a `System.NullReferenceException` is thrown instead.

The behavior at runtime of the evaluation of a *throw expression* is the same [as specified for a *throw statement*](../../spec/statements.md#the-throw-statement).

The flow-analysis rules are as follows:

- For every variable *v*, *v* is definitely assigned before the *null_coalescing_expression* of a *throw_expression* iff it is definitely assigned before the *throw_expression*.
- For every variable *v*, *v* is definitely assigned after *throw_expression*.

A *throw expression* is permitted in only the following syntactic contexts:
- As the second or third operand of a ternary conditional operator `?:`
- As the second operand of a null coalescing operator `??`
- As the body of an expression-bodied lambda or method.
