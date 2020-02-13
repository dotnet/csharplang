# Attributes on local functions

## Attributes

Local function declarations are now permitted to have [attributes](https://github.com/dotnet/csharplang/blob/bb997294540bd02f939d60514634ccaa7abc659c/spec/attributes.md). Parameters and type parameters on local functions are also allowed to have attributes.

Attributes with a specified meaning when applied to a method, its parameters, or its type parameters will have the same meaning when applied to a local function, its parameters, or its type parameters, respectively.

A local function can be made conditional in the same sense as a [conditional method](https://github.com/dotnet/csharplang/blob/98043cdc889303d956d540d7ab3bc4f5044a9d3b/spec/attributes.md#the-conditional-attribute) by decorating it with a `[ConditionalAttribute]`. A conditional local function must also be `static`.

## Extern

The `extern` modifier is now permitted on local functions. This makes the local function external in the same sense as an [external method](https://github.com/dotnet/csharplang/blob/892af9016b3317a8fae12d195014dc38ba51cf16/spec/classes.md#external-methods).

Similarly to an external method, the *local-function-body* of an external local function must be a semicolon. A semicolon *local-function-body* is only permitted on an external local function. 

An external local function must also be `static`.

## Syntax

The [local functions grammar](https://github.com/dotnet/csharplang/blob/bb997294540bd02f939d60514634ccaa7abc659c/proposals/csharp-7.0/local-functions.md#syntax-grammar) is modified as follows:
```
local-function-header
    : attributes? local-function-modifiers? return-type identifier type-parameter-list?
        ( formal-parameter-list? ) type-parameter-constraints-clauses
    ;

local-function-modifiers
    : (async | unsafe | static | extern)*
    ;

local-function-body
    : block
    | arrow-expression-body
    | ';'
    ;
```
