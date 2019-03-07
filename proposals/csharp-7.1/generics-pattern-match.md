# pattern-matching with generics

* [x] Proposed
* [ ] Prototype:
* [ ] Implementation:
* [ ] Specification:

## Summary
[summary]: #summary

The specification for the [existing C# as operator](../../spec/expressions.md#the-as-operator) permits there to be no conversion between the type of the operand and the specified type when either is an open type. However, in C# 7 the `Type identifier` pattern requires there be a conversion between the type of the input and the given type.

We propose to relax this and change `expression is Type identifier`, in addition to being permitted in the conditions when it is permitted in C# 7, to also be permitted when `expression as Type` would be allowed. Specifically, the new cases are cases where the type of the expression or the specified type is an open type. 

## Motivation
[motivation]: #motivation

Cases where pattern-matching should "obviously" be permitted currently fail to compile. See, for example, https://github.com/dotnet/roslyn/issues/16195.

## Detailed design
[design]: #detailed-design

We change the paragraph in the pattern-matching specification (the proposed addition is shown in bold):

> Certain combinations of static type of the left-hand-side and the given type are considered incompatible and result in compile-time error. A value of static type `E` is said to be *pattern compatible* with the type `T` if there exists an identity conversion, an implicit reference conversion, a boxing conversion, an explicit reference conversion, or an unboxing conversion from `E` to `T`**, or if either `E` or `T` is an open type**. It is a compile-time error if an expression of type `E` is not pattern compatible with the type in a type pattern that it is matched with.

## Drawbacks
[drawbacks]: #drawbacks

None.

## Alternatives
[alternatives]: #alternatives

None.

## Unresolved questions
[unresolved]: #unresolved-questions

None.

## Design meetings

LDM considered this question and felt it was a bug-fix level change. We are treating it as a separate language feature because just making the change after the language has been released would introduce a forward incompatibility. Using the proposed change requires that the programmer specify language version 7.1.
