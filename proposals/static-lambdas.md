# Static lambdas

## Summary

Support lambdas that disallow capturing state from the enclosing scope.

## Motivation

Avoid unintentionally capturing state from the enclosing context.

## Detailed design

A lambdas with `static` cannot capture state from the enclosing scope.
As a result, locals, parameters, and `this` from the enclosing scope are not available within a `static` lambda.

A `static` lambda cannot reference instance members from an implicit or explicit `this` or `base` reference.

A `static` lambda may reference `static` members from the enclosing scope.

A `static` lambda may reference `constant` definitions from the enclosing scope.

`nameof()` in a `static` lambda may reference locals, parameters, or `this` or `base` from the enclosing scope.

Accessibility rules for `private` members in the enclosing scope are the same for `static` and non-`static` lambdas.

No guarantee is made as to whether a `static` lambda definition is emitted as a `static` method in metadata. This is left up to the compiler implementation to optimize.

A non-`static` local function or lambda can capture state from an enclosing `static` lambda but cannot capture state outside the enclosing `static` lambda.

A `static` lambda can be used in an expression tree.

Removing the `static` modifier from a lambda in a valid program does not change the meaning of the program.
