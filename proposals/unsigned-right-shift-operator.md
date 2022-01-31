# Unsigned right shift operator

## Summary
[summary]: #summary

An unsigned right shift operator will be supported by C# as a built-in operator (for primitive integral types) and as a user-defined operator. 

## Motivation
[motivation]: #motivation

When working with signed integral value, it is not uncommon that you need to shift bits right without replicating
the high order bit on each shift. While this can be achieved for primitive integral types with a regular shift
operator, a cast to an unsigned type before the shift operation and a cast back after it is required. Within the
context of the generic math interfaces the libraries are planning to expose, this is potentially more problematic
as the type might not necessary have an unsigned counterpart defined or known upfront by the generic math code,
yet an algorithm might rely on ability to perform an unsigned right shift operation.

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement, and include examples of how the feature is used. Please include syntax and desired semantics for the change, including linking to the relevant parts of the existing C# spec to describe the changes necessary to implement this feature. An initial proposal does not need to cover all cases, but it should have enough detail to enable a language team member to bring this proposal to design if they so choose. -->

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
