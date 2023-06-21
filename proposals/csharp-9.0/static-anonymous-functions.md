# Static anonymous functions

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary

Allow a 'static' modifier on lambdas and anonymous methods, which disallows capture of locals or instance state from containing scopes.

## Motivation

Avoid unintentionally capturing state from the enclosing context, which can result in unexpected retention of captured objects or unexpected additional allocations.

## Detailed design

A lambda or anonymous method may have a `static` modifier. The `static` modifier indicates that the lambda or anonymous method is a *static anonymous function*.

A *static anonymous function* cannot capture state from the enclosing scope.
As a result, locals, parameters, and `this` from the enclosing scope are not available within a *static anonymous function*.

A *static anonymous function* cannot reference instance members from an implicit or explicit `this` or `base` reference.

A *static anonymous function* may reference `static` members from the enclosing scope.

A *static anonymous function* may reference `constant` definitions from the enclosing scope.

`nameof()` in a *static anonymous function* may reference locals, parameters, or `this` or `base` from the enclosing scope.

Accessibility rules for `private` members in the enclosing scope are the same for `static` and non-`static` anonymous functions.

No guarantee is made as to whether a *static anonymous function* definition is emitted as a `static` method in metadata. This is left up to the compiler implementation to optimize.

A non-`static` local function or anonymous function can capture state from an enclosing *static anonymous function* but cannot capture state outside the enclosing *static anonymous function*.

Removing the `static` modifier from an anonymous function in a valid program does not change the meaning of the program.
