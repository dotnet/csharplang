# null coalescing assignment

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Below

## Summary
[summary]: #summary

Simplifies a common coding pattern where a variable is assigned a value if it is null.

As part of this proposal, we will also loosen the type requirements on `??` to allow an expression whose type is an unconstrained type parameter to be used on the left-hand side.

## Motivation
[motivation]: #motivation

It is common to see code of the form

```csharp
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

Which follows the [existing semantic rules for compound assignment operators](../../spec/expressions.md#compound-assignment), except that we elide the assignment if the left-hand side is non-null. The rules for this feature are as follows.

Given `a ??= b`, where `A` is the type of `a`, `B` is the type of `b`:

1. If `A` does not exist or is a non-nullable value type, a compile-time error occurs.
2. If `B` is not implicitly convertible to `A`, a compile-time error occurs.
3. The type of `a ??= b` is `A`.
4. `a ??= b` is evaluated at runtime as `a ?? (a = b)`, except that `a` is only evaluated once.

For the relaxation of the type requirements of `??`, we update the spec where it currently states that, given `a ?? b`, where `A` is the type of `a`:

> 1. If A exists and is not a nullable type or a reference type, a compile-time error occurs.

We relax this requirement to:

1. If A exists and is a non-nullable value type, a compile-time error occurs.

## Drawbacks
[drawbacks]: #drawbacks

As with any language feature, we must question whether the additional complexity to the language is repaid in the additional clarity offered to the body of C# programs that would benefit from the feature.

## Alternatives
[alternatives]: #alternatives

The programmer can write `(x = x ?? y)`, `if (x == null) x = y;`, or `x ?? (x = y)` by hand.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Requires LDM review
- [ ] Should we also support `&&=` and `||=` operators?

## Design meetings

None.
