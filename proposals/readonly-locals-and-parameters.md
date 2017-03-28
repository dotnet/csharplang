In the name of Allah

# Readonly locals and parameters

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary
eThe "readonly locals and parameters" feature is actually a group or features that declare local variables and method parameters without permit the state (objects or primitives) to modifications.

# Related work

There is an existing proposal that touches this topics https://github.com/dotnet/roslyn/issues/115 and https://github.com/dotnet/csharplang/issues/188.
Here I just want to acknowledge that the idea by itself is not new anyway.

## Motivation

Maximizing data and state immutability in any program has value that cause to improve readability, predictability and maintainability. So many languages have some concepts and features to provide some facilities about it and because above benefits these features are many uses in programs and master programmers recommended to use them, for example [Scott Meyers in Effective C++ book third edition](https://en.wikipedia.org/wiki/Scott_Meyers) says:
> Use const whenever possible

And when he wants to describe it says:

>The wonderful thing about const is that it allows you to specify a semantic constraint — a particular object should not be modified — and compilers will enforce that constraint. It allows you to communicate to both compilers and other programmers that a value should remain invariant. Whenever that is true, you should be sure to say so, because that way you enlist your compilers’ aid in making sure the
constraint isn’t violated.

In this scope C# has some features like readonly/const member, but has many missing features to help programmer to gain maximum immutability in program easily. One of these missing features has ability to declare local variables and method parameters without permit any modification after initialization. 

## Detailed design
[design]: #detailed-design

### Readonly locals
We add new part to declaration statements:

``` antlr
declaration_statement:
    local-variable-declaration
    | local-constant-declaration
    | local-readonly-declaration
    ;
```

#### Local readonly declarations
A *local-readonly-declaration* declares one or more local readonly variables.

``` antlr
local-readonly-declaration:
    readonly   type   readonly-declarators
    ;
    
readonly-declarators:
    readonly-declarator
    | readonly-declarators   ','   readonly-declarator
    ;

readonly-declarator:
    identifier   '='   local-variable-initializer
    ;
```
Direct assignments to a local readonly variable are permitted only in `readonly-declarator` with `local-variable-initializer`. Attempting to assign to a local readonly variable is a compile-time error elsewhere.

### Readonly parameters
We add new parts to parameter modifier:

``` antlr
parameter_modifier:
    'ref'
    | 'out'
    | 'this'
    | 'readonly'    // new
    | 'readonly ref'    // new
    | 'readonly this'   // new
    ;
```
`readonly` modifier for parameter means that this parameter's state does not permit to change. So attempting to assign to a this parameter in method body is a compile-time error.

`readonly` parameter can have *default-argument*.

The `readonly`, `readonly ref` and `readonly this` are part of a method's signature.

**Note:** `readonly ref` and `readonly this` are describe at [Readonly references proposal](readonly-ref.md).

## Drawbacks
[drawbacks]: #drawbacks

## Alternatives
[alternatives]: #alternatives

The main competing design is really "do nothing".

## Unresolved questions
[unresolved]: #unresolved-questions

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.
