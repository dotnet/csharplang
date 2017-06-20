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

## Mar 1, 2017
[C# Language Design Notes for Mar 1, 2017](LDM-2017-03-01.md)

1. Shapes and extensions (*exploration*)
2. Conditional refs (*original design adopted*)

## Mar 7, 2017
[C# Language Design Notes for Mar 7, 2017](LDM-2017-03-07.md)

We continued to flesh out the designs for features currently considered for C# 7.1.

1. Default expressions (*design adopted*)
2. Field target on auto-properties (*yes*)
3. private protected (*yes, if things work as expected*)

## Mar 8, 2017
[C# Language Design Notes for Mar 8, 2017](LDM-2017-03-08.md)

We looked at default interface member implementations.

1. Xamarin interop scenario
2. Proposal
3. Inheritance from interface to class
4. Overriding and base calls
5. The diamond problem
6. Binary compatibility
7. Other semantic challenges

## Mar 15, 2017
[C# Language Design Notes for Mar 8, 2017](LDM-2017-03-15.md)

Triage of championed features

1. JSON literals
2. Fixing of captured locals
3. Allow shadowing of parameters
4. Weak delegates
5. Protocols/duck typing/concepts/type classes
6. Zero and one element tuples
7. Deconstruction in lambda parameters
8. Private protected

## Mar 21, 2017
[C# Language Design Notes for Mar 21, 2017](LDM-2017-03-21.md)

Discussion of default interface member implementations, based on [this guided tour](https://github.com/dotnet/csharplang/issues/288).

1. Concerns raised on GitHub and elsewhere
2. Inheritance?
3. Breaking name lookup on `this`
4. Events
5. Modifiers
6. Methods
7. Properties
8. Overrides
9. Reabstraction
10. Most specific override
11. Static non-virtual members
12. Accessibility levels
13. Existing programs

## Mar 28, 2017
[C# Language Design Notes for Mar 28, 2017](LDM-2017-03-28.md)

Design some remaining 7.1 features

1. Fix pattern matching restriction with generics
2. Better best common type

## Mar 29, 2017

[C# Language Design Notes for Mar 29, 2017](LDM-2017-03-29.md)

1. Nullable scenarios
2. `Span<T>` safety

## Apr 5, 2017
[C# Language Design Notes for Apr 5, 2017](LDM-2017-04-05.md)

1. Non-virtual members in interfaces
2. Inferred tuple element names
3. Tuple element names in generic constraints

## Apr 11, 2017
[C# Language Design Notes for Apr 11, 2017](LDM-2017-04-11.md)

1. Runtime behavior of ambiguous default implementation

## Apr 18, 2017
[C# Language Design Notes for Apr 18, 2017](LDM-2017-04-18.md)

1. Default implementations for event accessors in interfaces
2. Reabstraction in a class of default-implemented member
3. `sealed override` with default implementations
4. Use of `sealed` keyword for non-virtual interface members
5. Implementing inaccessible interface members
6. Implicitly implementing non-public interface members
7. Not quite implementing a member
8. asynchronous `Main`

## Apr 19, 2017
[C# Language Design Notes for Apr 19, 2017](LDM-2017-04-19.md)

1. Improved best common type
2. Diamonds with classes
3. Structs and default implementations
4. Base invocation

## May 16, 2017
[C# Language Design Notes for May 16, 2017](LDM-2017-05-16.md)

1. Triage C# 7.1 features that didn't make it
2. Look at C# 7.2 features
3. GitHub procedure around new design notes and proposals
4. Triage of championed features

## May 17, 2017
[C# Language Design Notes for May 17, 2017](LDM-2017-05-17.md)

More questions about default interface member implementations

1. Conflicting override of default implementations
2. Can the Main entry point method be in an interface?
3. Static constructors in interfaces?
4. Virtual properties with private accessors
5. Does an override introduce a member?
6. Parameter names

## May 26, 2017
[C# Language Design Notes for May 26, 2017](LDM-2017-05-26.md)

1. Native ints

## May 31, 2017
[C# Language Design Notes for May 31, 2017](LDM-2017-05-31.md)

1. Default interface members: overriding or implementing?
2. Downlevel poisoning of ref readonly in signatures
3. Extension methods with ref this and generics
4. Default in operators

## Jun 13, 2017
[C# Language Design Notes for Jun 13, 2017](LDM-2017-06-13.md)

1. Native-size ints
2. Native-size floats

## Jun 14, 2017
[C# Language Design Notes for Jun 14, 2017](LDM-2017-06-14.md)

Several issues related to default implementations of interface members

1. Virtual properties with private accessors
2. Requiring interfaces to have a most specific implementation of all members
3. Member declaration syntax revisited
4. Base calls

