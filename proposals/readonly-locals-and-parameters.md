In the name of Allah

# Readonly locals and parameters

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

The "readonly locals and parameters" feature is actually a group or features that declare local variables and method parameters without exposing the state to modifications.

# Prevoius work

There is an existing proposal that touches this topics https://github.com/dotnet/roslyn/issues/115 and https://github.com/dotnet/csharplang/issues/188.
Here I just want to acknowledge that the idea by itself is not new anyway.

## Motivation

Maximizing data and state immutability in any program has value that cause to improve readability, predictability and maintainability. So many languages have some concepts and features to provide some facilities about it and because above benefits these features are many uses in programs and master programmers recommended to use them, for example [Scott Meyers in Effective C++ book third edition](https://en.wikipedia.org/wiki/Scott_Meyers) says:
> Use const whenever possible

And when he wants to describe it says:

>The wonderful thing about const is that it allows you to specify a semantic constraint — a particular object should not be modified — and compilers will enforce that constraint. It allows you to communicate to both compilers and other programmers that a value should remain invariant. Whenever that is true, you should be sure to say so, because that way you enlist your compilers’ aid in making sure the
constraint isn’t violated.

In this scope C# has some features like readonly/const member, but has many missing features to help programmer to gain maximum immutability in program easily. One of these missing features has ability to declare local variables and method parameters without permit any modification after initialization. 

## Solution

## Syntax

## Drawbacks
[drawbacks]: #drawbacks

## Alternatives
[alternatives]: #alternatives

The main competing design is really "do nothing".

## Unresolved questions
[unresolved]: #unresolved-questions

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.
