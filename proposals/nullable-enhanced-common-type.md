# Nullable-Enhanced Common Type

* [x] Proposed
* [ ] Prototype: None
* [ ] Implementation: None
* [ ] Specification: See below

## Summary
[summary]: #summary

There is a situation in which the current common-type algorithm results are counter-intuitive, and results in the programmer adding what feels like a redundant cast to the code. With this change, an expression such as `condition ? 1 : null` would result in a value of type `int?`, and an expression such as `condition ? x : 1.0` where `x` is of type `int?` would result in a value of type `double?`.

## Motivation
[motivation]: #motivation

This is a common cause of what feels to the programmer like needless boilerplate code.

## Detailed design
[design]: #detailed-design

We modify the specification for [finding the best common type of a set of expressions](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#finding-the-best-common-type-of-a-set-of-expressions) to affect the following situations:

- If one expression is of a non-nullable value type `T` and the other is a null literal, the result is of type `T?`.
- If one expression is of a nullable value type `T?` and the other is of a value type `U`, and there is an implicit conversion from `T` to `U`, then the result is of type `U?`.

This is expected to affect the following aspects of the language:

- the [ternary expression](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#conditional-operator)
- implicitly typed [array creation expression](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#array-creation-expressions)
- inferring the [return type of a lambda](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#inferred-return-type) for type inference
- cases involving generics, such as invoking `M<T>(T a, T b)` as `M(1, null)`.

More precisely, we change the following sections of the specification (insertions in bold, deletions in strikethrough):

> #### Output type inferences
> 
> An *output type inference* is made *from* an expression `E` *to* a type `T` in the following way:
> 
> *  If `E` is an anonymous function with inferred return type  `U` ([Inferred return type](../spec/expressions.md#inferred-return-type)) and `T` is a delegate type or expression tree type with return type `Tb`, then a *lower-bound inference* ([Lower-bound inferences](../spec/expressions.md#lower-bound-inferences)) is made *from* `U` *to* `Tb`.
> *  Otherwise, if `E` is a method group and `T` is a delegate type or expression tree type with parameter types `T1...Tk` and return type `Tb`, and overload resolution of `E` with the types `T1...Tk` yields a single method with return type `U`, then a *lower-bound inference* is made *from* `U` *to* `Tb`.
> *  **Otherwise, if `E` is an expression with nullable value type `U?`, then a *lower-bound inference* is made *from* `U` *to* `T` and a *null bound* is added to `T`. **
> *  Otherwise, if `E` is an expression with type `U`, then a *lower-bound inference* is made *from* `U` *to* `T`.
> *  **Otherwise, if `E` is a constant expression with value `null`, then a *null bound* is added to `T`** 
> *  Otherwise, no inferences are made.

> #### Fixing
> 
> An *unfixed* type variable `Xi` with a set of bounds is *fixed* as follows:
> 
> *  The set of *candidate types* `Uj` starts out as the set of all types in the set of bounds for `Xi`.
> *  We then examine each bound for `Xi` in turn: For each exact bound `U` of `Xi` all types `Uj` which are not identical to `U` are removed from the candidate set. For each lower bound `U` of `Xi` all types `Uj` to which there is *not* an implicit conversion from `U` are removed from the candidate set. For each upper bound `U` of `Xi` all types `Uj` from which there is *not* an implicit conversion to `U` are removed from the candidate set.
> *  If among the remaining candidate types `Uj` there is a unique type `V` from which there is an implicit conversion to all the other candidate types, then ~~`Xi` is fixed to `V`.~~
>     -  **If `V` is a value type and there is a *null bound* for `Xi`, then `Xi` is fixed to `V?`**
>     -  **Otherwise   `Xi` is fixed to `V`**
> *  Otherwise, type inference fails.

## Drawbacks
[drawbacks]: #drawbacks

There may be some incompatibilities introduced by this proposal.

## Alternatives
[alternatives]: #alternatives

None.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] What is the severity of incompatibility introduced by this proposal, if any, and how can it be moderated?

## Design meetings

None.
