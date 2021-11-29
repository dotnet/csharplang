# `ref` assignment on `switch` expressions

* [x] Proposed
* [ ] Prototype: None
* [ ] Implementation: None
* [ ] Specification: Started, below

## Summary
[summary]: #summary

Provide the ability to return `ref` values in `switch` expressions.

## Motivation
[motivation]: #motivation

The `switch` expression is meant to provide a quicker way to return values based on a given expression, but does not support returning `ref` expressions. This limitation doesn't prevent any harm, and can be lifted.

## Detailed design
[design]: #detailed-design

A `switch` expression may return a `ref` value. The returning expression, if not a `throw` expression, must be a `ref` expression.

A `switch` expression that returns a `ref` value **does not** need an extra `ref` in front of it when assigned/returned to a `ref` local. The examples below cover this case too. This decision is made since the `ref` is essentially included in the cases.

`throw` expressions can still be used normally.

## Examples
[examples]: #examples

```csharp
private ref int GetDirectionField(Direction direction) => direction switch
{
     Direction.North => ref NorthField,
     Direction.South => ref SouthField,
     Direction.East => ref EastField,
     Direction.West => ref WestField,
     _ => throw new NotImplementedException(),
};

private void RefSwitchAssignLocal()
{
    ref int dimension = axis switch
    {
        Axis.X => ref X,
        Axis.Y => ref Y,
    };
}
```

## Drawbacks
[drawbacks]: #drawbacks

None.

## Alternatives
[alternatives]: #alternatives

Currently, this may only be achieved with a `switch` statement, or a sequence of `if`-`else` statements.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Requires LDM review
- [ ] Should there be a quicker way to denote returning a `ref` to the provided expression by not requiring copying it over for each case?

## Design meetings
[meetings]: #design-meetings

None.
