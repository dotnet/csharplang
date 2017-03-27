# C# Language Design Notes for 2017

Overview of meetings and agendas for 2017

## Jan 10, 2017

[C# Language Design Notes for Jan 10, 2017](LDM-2017-01-10.md)

1. Discriminated unions via "closed" types

## Jan 11, 2017

[C# Language Design Notes for Jan 11, 2017](LDM-2017-01-11.md)

1. Language aspects of [compiler intrinsics](https://github.com/dotnet/roslyn/issues/11475)

## Jan 17, 2017

[C# Language Design Notes for Jan 17, 2017](LDM-2017-01-17.md)

1. Constant pattern semantics: which equality exactly?
2. Extension methods on tuples: should tuple conversions apply?


## Jan 18, 2017

[C# Language Design Notes for Jan 18, 2017](LDM-2017-01-18.md)

1. Async streams (visit from Oren Novotny)

## Feb 21, 2017

[C# Language Design Notes for Feb 21, 2017](LDM-2017-02-21.md)

We triaged some of the [championed features](https://github.com/dotnet/csharplang/issues?q=is%3Aopen+is%3Aissue+label%3A%22Proposal+champion%22), to give them a tentative milestone and ensure they had a champion.

As part of this we revisited potential 7.1 features and pushed several out.

1. Implicit interface implementation in Visual Basic *(VB 16)*
2. Delegate and enum constraints *(C# X.X)*
3. Generic attributes *(C# X.0 if even practical)*
4. Replace/original *(C# X.0 if and when relevant)*
5. Bestest betterness *(C# 7.X)*
6. Null-coalescing assignments and awaits *(C# 7.X)*
7. Deconstruction in from and let clauses *(C# 7.X)*
8. Target-typed `new` expressions *(C# 7.X)*
9. Mixing fresh and existing variables in deconstruction *(C# 7.1)*
10. Implementing `==` and `!=` on tuple types *(C# 7.X)*
11. Declarations in embedded statements *(No)*
12. Field targeted attributes on auto-properties *(C# 7.1)*

## Feb 22, 2017

[C# Language Design Notes for Feb 22, 2017](LDM-2017-02-22.md)

We went over the proposal for `ref readonly`: [Champion "Readonly ref"](https://github.com/dotnet/csharplang/issues/38).

## Feb 28, 2017

[C# Language Design Notes for Feb 28, 2017](LDM-2017-02-28.md)

1. Conditional operator over refs (*Yes, but no decision on syntax*)
2. Async Main (*Allow Task-returning Main methods*)

