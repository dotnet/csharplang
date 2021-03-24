# null-conditional await

* [x] Proposed
* [ ] Prototype: None
* [ ] Implementation: None
* [ ] Specification: Started, below

## Summary
[summary]: #summary

Support an expression of the form `await? e`, which awaits `e` if it is non-null, otherwise it results in `null`.

## Motivation
[motivation]: #motivation

This is a common coding pattern, and this feature would have nice synergy with the existing null-propagating and null-coalescing operators.

## Detailed design
[design]: #detailed-design

We add a new form of the *await_expression*:

```antlr
await_expression
    : 'await' '?' unary_expression
    ;
```

The null-conditional `await` operator awaits its operand only if that operand is non-null. Otherwise the result of applying the operator is null.

The type of the result is computed using the [rules for the null-conditional operator](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#null-conditional-operator).

> **NOTE:**
> If `e` is of type `Task`, then `await? e;` would do nothing if `e` is `null`, and await `e` if it is not `null`.
>
> If `e` is of type `Task<K>` where `K` is a value type, then `await? e` would yield a value of type `K?`.

## Drawbacks
[drawbacks]: #drawbacks

As with any language feature, we must question whether the additional complexity to the language is repaid in the additional clarity offered to the body of C# programs that would benefit from the feature.

## Alternatives
[alternatives]: #alternatives

Although it requires some boilerplate code, uses of this operator can often be replaced by an expression something like `(e == null) ? null : await e` or a statement like `if (e != null) await e`.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Requires LDM review

## Design meetings

None.
