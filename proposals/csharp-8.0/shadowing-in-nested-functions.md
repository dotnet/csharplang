# Name shadowing in nested functions

## Summary

Permit variable names in lambdas and local functions to reuse (and shadow) names from the enclosing scope.

## Detailed design

With `-langversion:8`, names of locals, local functions, parameters, type parameters, and range variables within a lambda or local function can reuse names of locals, local functions, parameters, type parameters, and range variables from an enclosing scope. The name in the nested scope hides the symbol of the same name from the enclosing scope within the nested scope.

Shadowing is supported for `static` and non-`static` local functions and lambdas.

There is no change in behavior using `-langversion:7.3` or earlier: names in nested functions that shadow names from the enclosing scope are reported as errors in those cases.

Any shadowing previously permitted is still supported with `-langversion:8`. For instance: variable names may shadow type and member names; and variable names may shadow enclosing method or local function names.

A warning is reported for a type parameter in a local function that shadows a type parameter in an enclosing scope.

## Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-09-10.md#static-local-functions
- https://github.com/dotnet/csharplang/blob/master/meetings/2019/LDM-2019-01-16.md#shadowing-in-nested-functions
