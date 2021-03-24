# Expression variables in initializers

## Summary
[summary]: #summary

We extend the features introduced in C# 7 to permit expressions containing expression variables (out variable declarations and declaration patterns) in field initializers, property initializers, ctor-initializers, and query clauses.

## Motivation
[motivation]: #motivation

This completes a couple of the rough edges left in the C# language due to lack of time.

## Detailed design
[design]: #detailed-design

We remove the restriction preventing the declaration of expression variables (out variable declarations and declaration patterns) in a ctor-initializer. Such a declared variable is in scope throughout the body of the constructor.

We remove the restriction preventing the declaration of expression variables (out variable declarations and declaration patterns) in a field or property initializer. Such a declared variable is in scope throughout the initializing expression.

We remove the restriction preventing the declaration of expression variables (out variable declarations and declaration patterns) in a query expression clause that is translated into the body of a lambda. Such a declared variable is in scope throughout that expression of the query clause.

## Drawbacks
[drawbacks]: #drawbacks

None.

## Alternatives
[alternatives]: #alternatives

The appropriate scope for expression variables declared in these contexts is not obvious, and deserves further LDM discussion.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] What is the appropriate scope for these variables?

## Design meetings

None.
