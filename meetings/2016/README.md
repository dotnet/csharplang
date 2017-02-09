# C# Language Design Notes for 2016

Overview of meetings and agendas for 2016

## Feb 29, 2016

[C# Language Design Notes for Feb 29, 2016](LDM-2016-02-29.md)

*Catch up edition (deconstruction and immutable object creation)*

Over the past couple of months various design activities took place that weren't documented in design notes. This a summary of the state of design regarding positional deconstruction, with-expressions and object initializers for immutable types.

## Apr 6, 2016

[C# Language Design Notes for Apr 6, 2016](LDM-2016-04-06.md)

We settled several open design questions concerning tuples and pattern matching.


## Apr 12-22, 2016

[C# Language Design Notes for Apr 12-22, 2016](LDM-2016-04-12-22.md)

These notes summarize discussions across a series of design meetings in April on several topics related to tuples and patterns:
- Tuple syntax for non-tuple types
- Tuple deconstruction
- Tuple conversions
- Deconstruction and patterns
- Out vars and their scope

## May 3-4, 2016

[C# Language Design Notes for May 3-4, 2016](LDM-2016-05-03-04.md)

This pair of meetings further explored the space around tuple syntax, pattern matching and deconstruction. 
1. Deconstructors - how to specify them
2. Switch conversions - how to deal with them
3. Tuple conversions - how to do them
4. Tuple-like types - how to construct them

## May 10, 2016

[C# Language Design Notes for May 10, 2016](LDM-2016-05-10.md)

In this meeting we took a look at the possibility of adding new kinds of extension members, beyond extension methods.


## Jul 12, 2016

[C# Language Design Notes for Jul 12, 2016](LDM-2016-07-12.md)

Several design details pertaining to tuples and deconstruction resolved.

## Jul 13, 2016

[C# Language Design Notes for Jul 13, 2016](LDM-2016-07-13.md)

We resolved a number of questions related to tuples and deconstruction, and one around equality of floating point values in pattern matching.


## Jul 15, 2016

[C# Design Language Notes for Jul 15, 2016](LDM-2016-07-15.md)

In this meeting we took a look at what the scope rules should be for variables introduced by patterns and out vars.


## Aug 24, 2016

[C# Design Language Notes for Aug 24, 2016](LDM-2016-08-24.md)

After a meeting-free period of implementation work on C# 7.0, we had a few issues come up for resolution.

1. What does it take to be task-like?
2. Scope of expression variables in initializers

## Sep 6, 2016

[C# Language Design Notes for Sep 6, 2016](LDM-2016-09-06.md)

1. How do we select `Deconstruct` methods?

## Oct 18, 2016

[C# Language Design Meeting Notes, Oct 18, 2016](LDM-2016-10-18.md)

1. Wildcard syntax
2. Design "room" between tuples and patterns
3. Local functions
4. Digit separators
5. Throw expressions
6. Tuple name mismatch warnings
7. Tuple types in `new` expressions


## Oct 25-26, 2016

[C# Language Design Meeting Notes, Oct 25-26, 2016](LDM-2016-10-25-26.md)

1. Declaration expressions as a generalizing concept
2. Irrefutable patterns and definite assignment
3. Allowing tuple-returning deconstructors
4. Avoiding accidental reuse of out variables
5. Allowing underbar as wildcard character

## Nov 1, 2016

[C# Language Design Meeting Notes, Nov 1, 2016](LDM-2016-11-01.md)

1. Abstracting over memory with `Span<T>`

## Nov 15, 2016

[C# Language Design Meeting Notes, Nov 15, 2016](LDM-2016-11-15.md)

1. Tuple name warnings
2. "Discards"

## Nov 16, 2016

[C# Language Design Meeting Notes, Nov 16, 2016](LDM-2016-11-16.md)


1. Nullable reference types

## Nov 30, 2016

[C# Language Design Meeting Notes, Nov 30, 2016](LDM-2016-11-30.md)


1. Scope of while condition expression variables
2. Mixed deconstruction
3. Unused expression variables
4. Declarations in embedded statements
5. Not-null pattern

## Dec 7 and 14, 2016

[C# Language Design Meeting Notes, Dec 7 and 14, 2016](LDM-2016-12-07-14.md)

1. Expression variables in query expressions
2. Irrefutable patterns and reachability
3. Do-while loop scope
