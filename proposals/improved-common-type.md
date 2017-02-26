# improved common type

* [x] Proposed
* [ ] Prototype: None
* [ ] Implementation: None
* [ ] Specification: See below

## Summary
[summary]: #summary

There is a situation in which the current common-type algorithm results are counter-intuitive, and results in the programmer adding what feels like a redundant cast to the code. With this change, an expression such as `condition ? 1 : null` would result in a value of type `int?`.

## Motivation
[motivation]: #motivation

This is a common cause of what feels to the programmer like needless boilerplate code.

## Detailed design
[design]: #detailed-design

We modify the specification for [finding the best common type of a set of expressions](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#finding-the-best-common-type-of-a-set-of-expressions) to affect the following situation:

- If one expression is of a non-nullable value type `T` and the other is a null literal, the result is of type `T?`.

This is expected to affect the following aspects of the language:

- the [ternary expression](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#conditional-operator)
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

## Design meetings

None.
