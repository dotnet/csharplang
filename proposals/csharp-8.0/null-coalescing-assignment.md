# Null coalescing assignment

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

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

Which follows the existing semantic rules for compound assignment operators ([§11.18.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11183-compound-assignment)), except that we elide the assignment if the left-hand side is non-null. The rules for this feature are as follows.

Given `a ??= b`, where `A` is the type of `a`, `B` is the type of `b`, and `A0` is the underlying type of `A` if `A` is a nullable value type:

1. If `A` does not exist or is a non-nullable value type, a compile-time error occurs.
2. If `B` is not implicitly convertible to `A` or `A0` (if `A0` exists), a compile-time error occurs.
3. If `A0` exists and `B` is implicitly convertible to `A0`, and `B` is not dynamic, then the type of `a ??= b` is `A0`. `a ??= b` is evaluated at runtime as:
   ```C#
   var tmp = a.GetValueOrDefault();
   if (!a.HasValue) { tmp = b; a = tmp; }
   tmp
   ```
   Except that `a` is only evaluated once.
4. Otherwise, the type of `a ??= b` is `A`. `a ??= b` is evaluated at runtime as `a ?? (a = b)`, except that `a` is only evaluated once.


For the relaxation of the type requirements of `??`, we update the spec where it currently states that, given `a ?? b`, where `A` is the type of `a`:

> 1. If A exists and is not a nullable type or a reference type, a compile-time error occurs.

We relax this requirement to:

1. If A exists and is a non-nullable value type, a compile-time error occurs.

This allows the null coalescing operator to work on unconstrained type parameters, as the unconstrained type parameter T exists, is not a nullable type, and is not a reference type.

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
