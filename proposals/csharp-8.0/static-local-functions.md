# Static local functions

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary

Support local functions that disallow capturing state from the enclosing scope.

## Motivation

Avoid unintentionally capturing state from the enclosing context.
Allow local functions to be used in scenarios where a `static` method is required.

## Detailed design

A local function declared `static` cannot capture state from the enclosing scope.
As a result, locals, parameters, and `this` from the enclosing scope are not available within a `static` local function.

A `static` local function cannot reference instance members from an implicit or explicit `this` or `base` reference.

A `static` local function may reference `static` members from the enclosing scope.

A `static` local function may reference `constant` definitions from the enclosing scope.

`nameof()` in a `static` local function may reference locals, parameters, or `this` or `base` from the enclosing scope.

Accessibility rules for `private` members in the enclosing scope are the same for `static` and non-`static` local functions.

A `static` local function definition is emitted as a `static` method in metadata, even if only used in a delegate.

A non-`static` local function or lambda can capture state from an enclosing `static` local function but cannot capture state outside the enclosing `static` local function.

A `static` local function cannot be invoked in an expression tree.

A call to a local function is emitted as `call` rather than `callvirt`, regardless of whether the local function is `static`.

Overload resolution of a call within a local function not affected by whether the local function is `static`.

Removing the `static` modifier from a local function in a valid program does not change the meaning of the program.

## Design meetings

https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-09-10.md#static-local-functions
