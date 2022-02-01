# Relaxing shift operator requirements

## Summary
[summary]: #summary

The shift operator requirements will be relaxed so that the right-hand side operand is no longer restricted to only be `int`.

## Motivation
[motivation]: #motivation

When working with types other than `int`, it is not uncommon that you shift using the result of another computation,
such as shifting based on the `leading zero count`. The natural type of something like a `leading zero count` is the
same as the input type (`TSelf`) and so in many cases, this requires you to convert that result to `int` before shifting,
even if that result is already within range.

Within the context of the generic math interfaces the libraries are planning to expose, this is potentially problematic
as the type is not well known and so the conversion to `int` may not be possible or even well-defined.

## Detailed design
[design]: #detailed-design

https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#shift-operators should be reworded as follows:
```diff
- When declaring an overloaded shift operator, the type of the first operand must always be the class or struct containing the operator declaration,
and the type of the second operand must always be int.
+ When declaring an overloaded shift operator, the type of the first operand must always be the class or struct containing the operator declaration.
```

That is, the restriction that the first operand be the class or struct containing the operator declaration remains.
While the restriction that the second operand must be `int` is removed.

## Drawbacks
[drawbacks]: #drawbacks

Users will be able to define operators that do not follow the recommended guidelines, such as implementing `cout << "string"` in C#.

## Alternatives
[alternatives]: #alternatives

The generic math interfaces being exposed by the libraries could expose explicitly named methods instead.
This may make code more difficult to read/maintain. 

The generic math interfaces could require the shift take `int` and that a conversion be performed.
This conversion may be expensive or may be not possible depending on the type in question.

## Unresolved questions
[unresolved]: #unresolved-questions

Is there concern around preserving the "intent" around why the second operand was restricted to `int`?

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
