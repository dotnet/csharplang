# Local functions

Champion issue: <https://github.com/dotnet/csharplang/issues/56>

We extend C# to support the declaration of functions in block scope. Local functions may use (capture) variables from the enclosing scope.

The compiler uses flow analysis to detect which variables a local function uses before assigning it a value. Every call of the function requires such variables to be definitely assigned. Similarly the compiler determines which variables are definitely assigned on return. Such variables are considered definitely assigned after the local function is invoked.

Local functions may be called from a lexical point before its definition. Local function declaration statements do not cause a warning when they are not reachable.

TODO: _WRITE SPEC_

## Syntax grammar

This grammar is represented as a diff from the current spec grammar.

```diff
declaration-statement
    : local-variable-declaration ';'
    | local-constant-declaration ';'
+   | local-function-declaration
    ;

+local-function-declaration
+   : local-function-header local-function-body
+   ;

+local-function-header
+   : local-function-modifiers? return-type identifier type-parameter-list?
+       ( formal-parameter-list? ) type-parameter-constraints-clauses
+   ;

+local-function-modifiers
+   : (async | unsafe)
+   ;

+local-function-body
+   : block
+   | arrow-expression-body
+   ;
```

Local functions may use variables defined in the enclosing scope. The current
implementation requires that every variable read inside a local function be
definitely assigned, as if executing the local function at its point of
definition. Also, the local function definition must have been "executed" at
any use point.

After experimenting with that a bit (for example, it is not possible to define
two mutually recursive local functions), we've since revised how we want the
definite assignment to work. The revision (not yet implemented) is that all
local variables read in a local function must be definitely assigned at each
invocation of the local function. That's actually more subtle than it sounds,
and there is a bunch of work remaining to make it work. Once it is done you'll
be able to move your local functions to the end of its enclosing block.

The new definite assignment rules are incompatible with inferring the return
type of a local function, so we'll likely be removing support for inferring the
return type.

Unless you convert a local function to a delegate, capturing is done into
frames that are value types. That means you don't get any GC pressure from
using local functions with capturing.

### Reachability

We add to the spec

> The body of a statement-bodied lambda expression or local function is considered reachable.
