# Constant Interpolated Strings

* [x] Proposed
* [ ] Prototype: [Not Started](https://github.com/kevinsun-dev/roslyn/BRANCH_NAME)
* [ ] Implementation: [Not Started](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

Enables constants to be generated from interpolated strings of type string constant.

## Motivation
[motivation]: #motivation

The following code is already legal:
```
public class C
{
    const string S1 = "Hello world";
    const string S2 = "Hello" + " " + "World";
    const string S3 = S1 + " Kevin, welcome to the team!";
}
```
However, there have been many community requests to make the following also legal:
```
public class C
{
    const string S1 = $"Hello world";
    const string S2 = $"Hello{" "}World";
    const string S3 = $"{S1} Kevin, welcome to the team!";
}
```
This proposal represents the next logical step for constant string generation, where existing string syntax that works in other situations is made to work for constants.

## Detailed design
[design]: #detailed-design

The [specifications](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#interpolated-strings) for interpolated strings remain the same, with the restriction that all operations must be completed in compile time. We permit the interpolated strings construct to be used in constants in the [spec](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#constant-expressions).

These interpolated strings are restricted in that their consituent components also must be constant and of type string.

## Drawbacks
[drawbacks]: #drawbacks

This proposal adds additional complexity to the compiler in exchange for broader applicability of interpolated strings. As these strings are fully evaluated at compile time, the valuable automatic formatting features of interpolated strings are less neccesary. Most use cases can be largely replicated through the alternatives below.

## Alternatives
[alternatives]: #alternatives

The current `+` operator for string concatnation can combine strings in a similar manner to the current proposal.

## Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still undecided?

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.


