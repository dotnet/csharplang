# Checked user-defined operators

## Summary
[summary]: #summary

C# should support defining `checked` variants of the following user-defined operators so that users can opt into or out of overflow behavior as appropriate:
*  The `-` unary operator (https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#unary-minus-operator).
*  The `+`, `-`, `*`, and `/` binary operators (https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#arithmetic-operators).

## Motivation
[motivation]: #motivation

There is no way for a user to declare a type and support both checked and unchecked versions of an operator. This will make it hard to port various algorithms to use the proposed `generic math` interfaces exposed by the libraries team. Likewise, this makes it impossible to expose a type such as `Int128` or `UInt128` without the language simultaneously shipping its own support to avoid breaking changes.

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement, and include examples of how the feature is used. Please include syntax and desired semantics for the change, including linking to the relevant parts of the existing C# spec to describe the changes necessary to implement this feature. An initial proposal does not need to cover all cases, but it should have enough detail to enable a language team member to bring this proposal to design if they so choose. -->

Expression trees? 

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

Conversion operators?
Dynamic?

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
