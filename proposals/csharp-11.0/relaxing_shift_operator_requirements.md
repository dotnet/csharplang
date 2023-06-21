# Relaxing shift operator requirements

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

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

### Shift operators

[§11.10](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1110-shift-operators) should be reworded as follows:
```diff
- When declaring an overloaded shift operator, the type of the first operand must always be the class or struct containing the operator declaration,
and the type of the second operand must always be int.
+ When declaring an overloaded shift operator, the type of the first operand must always be the class or struct containing the operator declaration.
```

That is, the restriction that the first operand be the class or struct containing the operator declaration remains.
While the restriction that the second operand must be `int` is removed.

### Binary operators

[§14.10.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/classes.md#14103-binary-operators) should be reworded as follows:
```diff
-*  A binary `<<` or `>>` operator must take two parameters, the first of which must have type `T` or `T?` and the second of which must have type `int` or `int?`, and can return any type.
+*  A binary `<<` or `>>` operator must take two parameters, the first of which must have type `T` or `T?`, and can return any type.
```

That is, the restriction that the first parameter be `T` or `T?` remains.
While the restriction that the second operand must be `int` or `int?` is removed.

### Binary operator overload resolution

The first bullet point at [§11.4.5](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1145-binary-operator-overload-resolution)
should be reworded as follows:

*  The set of candidate user-defined operators provided by `X` and `Y` for the operation `operator op(x,y)` is determined. The set consists of the union of the candidate operators provided by `X` and **, unless the operator is a shift operator,** the candidate operators provided by `Y`, each determined using the rules of Candidate user-defined operators [§11.4.6](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1146-candidate-user-defined-operators). If `X` and `Y` are the same type, or if `X` and `Y` are derived from a common base type, then shared candidate operators only occur in the combined set once.

That is, for shift operators, candidate operators are only those provided by type `X`.

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

https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-09.md
