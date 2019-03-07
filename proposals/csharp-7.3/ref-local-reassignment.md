# Ref Local Reassignment

In C# 7.3, we add support for rebinding the referent of a ref local variable or a ref parameter.

We add the following to the set of `assignment_operator`s.

```antlr
assignment_operator
    : '=' 'ref'
    ;
```

The `=ref` operator is called the ***ref assignment operator***. It is not a *compound assignment operator*. The left operand must be an expression that binds to a ref local variable, a ref parameter (other than `this`), or an out parameter. The right operand must be an expression that yields an lvalue designating a value of the same type as the left operand.

The right operand must be definitely assigned at the point of the ref assignment.

When the left operand binds to an `out` parameter, it is an error if that `out` parameter has not been definitely assigned at the beginning of the ref assignment operator.

If the left operand is a writeable ref (i.e. it designates anything other than a `ref readonly` local or  `in` parameter), then the right operand must be a writeable lvalue.

The ref assignment operator yields an lvalue of the assigned type. It is writeable if the left operand is writeable (i.e. not `ref readonly` or `in`).

The safety rules for this operator are:

- For a ref reassignment `e1 = ref e2`, the *ref-safe-to-escape* of `e2` must be at least as wide a scope as the *ref-safe-to-escape* of `e1`.

Where *ref-safe-to-escape* is defined in [Safety for ref-like types](../csharp-7.2/span-safety.md)
