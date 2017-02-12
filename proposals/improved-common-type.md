# improved common type

* [x] Proposed
* [ ] Prototype: None
* [ ] Implementation: None
* [ ] Specification: See below

## Summary
[summary]: #summary

There are two circumstances in which the current common-type algorithm results are counter-intuitive, and result in the programmer adding what feels like a redundant cast to the code. With this change, an expression such as `condition ? 1 : null` would result in a value of type `int?`, and an expression of the form `condition ? new Dog() : new Cat()` would result in a value of their common base type `Animal`.

## Motivation
[motivation]: #motivation

This is a common cause of what feels to the programmer like needless boilerplate code.

## Detailed design
[design]: #detailed-design

We modify the specification for [finding the best common type of a set of expressions](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#finding-the-best-common-type-of-a-set-of-expressions) to affect the following two circumstances:

- If one expression is of a non-nullable value type `T` and the other is a null literal, the result is of type `T?`.
- If the two operands are class types and have a common base type other than `object`, the most specific such type is the result.

This is expected to affect the following aspects of the language:

- the [ternary expression](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#conditional-operator)
- the [null coalescing expression](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#the-null-coalescing-operator)
- implicitly types [array creation expression](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#array-creation-expressions)
- inferring the [return type of a lambda](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#inferred-return-type) for type inference

## Drawbacks
[drawbacks]: #drawbacks

There may be some incompatibilities introduced by this proposal.

## Alternatives
[alternatives]: #alternatives

None.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] What is the severity of incompatibility introduced by this proposal, and how can it be moderated?
- [ ] Should we handle reference types other than class types?

## Design meetings

None.
