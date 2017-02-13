# null coalescing assignment

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Below

## Summary
[summary]: #summary

Simplifies a common coding pattern where a variable is assigned a value if it is null.

## Motivation
[motivation]: #motivation

It is common to see code of the form

``` c#
if (variable == null)
{
    variable = expression;
}
```

This proposal adds a non-overloadable binary operator to the language that performs this function.

There have been at least eight separate community requests for this feature.

## Detailed design
[design]: #detailed-design

We add a new form of assignment operator

``` antlr
assignment_operator
    : '??='
    ;
```

Which follows the [existing semantic rules for compound assignment operators](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#compound-assignment). What that means is that an operation of the form `x ??= y` is processed as if written `x = (x ?? y)` (except `x` shall be computed once). However, we permit the assignment to `x` (of its existing value) to be elided in the case that `x` is not null.

## Drawbacks
[drawbacks]: #drawbacks

As with any language feature, we must question whether the additional complexity to the language is repaid in the additional clarity offered to the body of C# programs that would benefit from the feature.

## Alternatives
[alternatives]: #alternatives

The programmer can write `(x = x ?? y)` or `if (x == null) x = y;` by hand.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Requires LDM review
- [ ] Should we also support `&&=` and `||=` operators?

## Design meetings

None.
