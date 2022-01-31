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

### Shift operators

The https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#shift-operators section will be adjusted
to include `>>>` operator - the unsigned right shift operator.

### Operator overloading

Operator `>>>` will be added to the set of overloadable binary operators at https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#operator-overloading.

### Lifted operators

Operator `>>>` will be added to the set of binary operators permitting a lifted form at https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#lifted-operators.

### Operator precedence and associativity

The https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#operator-precedence-and-associativity section will be adjusted to add `>>>` operator to the "Shift" category and `>>>=` operator to the "Assignment and lambda expression" category.

### Grammar ambiguities

The `>>>` operator is subject to the same grammar ambiguities described at https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#grammar-ambiguities as a regular `>>` operator.

### Dynamic?

### Expression Tree?

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
