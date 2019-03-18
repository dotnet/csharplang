# Upcoming meetings for 2019

## Schedule ASAP

## Schedule when convenient

- Discussion of refreshing language spec (Neal)
- Allowing pattern-based `foreach` and `await foreach` to bind to an extension `GetEnumerator`/`GetAsyncEnumerator` and `MoveNext`/`MoveNextAsync` (Julien/Chris)
- Making a `CancellationToken` available in async-iterator method bodies; possibly reserving a keyword (Julien/Stephen) 
- Syntax of positional records/primary constructors (Andy)
- Nullable sidecar files (Immo)
- Nullable Reference Types: Open LDM Issues https://github.com/dotnet/csharplang/issues/2201

## Recurring topics

- *Triage championed features*
- *Triage milestones*
- *Design review*

## Apr 17, 2019

## Apr 15, 2019

## Apr 3, 2019

## Apr 1, 2019

- Pattern-based index/range translation (Jared, Stephen)

## Mar 27, 2019

#### Default Interface Methods
See also https://github.com/dotnet/csharplang/issues/406

We now have a proposed runtime implementation for reabstraction.  See https://github.com/dotnet/coreclr/pull/23313

- Reabstraction (open)
- explicit interface abstract overrides in classes (open)

#### Nullable Reference Types

- What is the nullability of a dynamic value?  Oblivious?
  See also https://github.com/dotnet/roslyn/issues/29893

- When we compute an annotation in the walker (e.g. type inference), do we use the context?
  See also https://github.com/dotnet/roslyn/issues/33639

- Inferred nullable state from a finally block (open)
  See also https://github.com/dotnet/roslyn/issues/34018

## Mar 25, 2019

- *Design review*

## Mar 13, 2019

#### Default Interface Methods
See also https://github.com/dotnet/csharplang/issues/406

- Reabstraction (open)
- explicit interface abstract overrides in classes (open)

#### Pattern-Matching
See also https://github.com/dotnet/csharplang/issues/2095

- Propose to change precedence of switch expression to primary (open)

  The switch expression is currently at *relational* precedence. I propose to change it to *primary* precedence. See [#2331](https://github.com/dotnet/csharplang/issues/2331) for details.

- Reserve `and` and `or` in patterns (open)

  In anticipation of possibly permitting `and` and `or` as pattern combinators in the future, we should forbid (or at least warn) when these identifiers are used as the designator in a declaration or recursive pattern.  Otherwise it would be a breaking change.

- To where do null inferences flow from a pattern in a `switch`? (open)

  1. To the entry of the switch and all previous cases
  2. To that branch of the switch only
  3. To all code that follows the test in logical order

## Mar 6, 2019

- ~~Nullable sidecar files (Immo)~~
- Nullable Reference Types: Open LDM Issues https://github.com/dotnet/csharplang/issues/2201

# C# Language Design Notes for 2019

Overview of meetings and agendas for 2019

## Mar 4, 2019

[C# Language Design Notes for March 4, 2019](LDM-2019-03-04.md)

1. Nullable user studies
2. Interpolated string and string.Format optimizations

## Feb 27, 2019

[C# Language Design Notes for Feb 27, 2019](LDM-2019-02-27.md)

1. Allow ObsoleteAttribute on property accessors
2. More Default Interface Member questions

## Feb 25, 2019

[C# Language Design Notes for Feb 25, 2019](LDM-2019-02-25.md)

- Open issues in default interface methods (https://github.com/dotnet/csharplang/issues/406). 
    - Base calls
    - We currently have open issues around `protected`, `internal`, reabstraction, and `static` fields among others.

## Feb 20, 2019

[C# Language Design Notes for Feb 20, 2019](LDM-2019-02-20.md)

- Nullable Reference Types: Open LDM Issues https://github.com/dotnet/csharplang/issues/2201

## Feb 13, 2019

[C# Language Design Notes for Feb 13, 2019](LDM-2019-02-13.md)

- Nullable Reference Types: Open LDM Issues https://github.com/dotnet/csharplang/issues/2201

## Jan 23, 2019

[C# Language Design Notes for Jan 23, 2019](LDM-2019-01-23.md)

Function pointers ([Updated proposal](https://github.com/dotnet/csharplang/blob/master/proposals/function-pointers.md))

## Jan 16, 2019

[C# Language Design Notes for Jan 16, 2019](LDM-2019-01-16.md)

1. Shadowing in lambdas
2. Pattern-based disposal in `await foreach`

## Jan 14, 2019

[C# Language Design Notes for Jan 14, 2019](LDM-2019-01-14.md)

- Generating null-check for `parameter!`
https://github.com/dotnet/csharplang/pull/2144

## Jan 9, 2019

[C# Language Design Notes for Jan 9, 2019](LDM-2019-01-09.md)

1. GetAsyncEnumerator signature
2. Ambiguities in nullable array type syntax
2. Recursive Patterns Open Language Issues https://github.com/dotnet/csharplang/issues/2095

## Jan 7, 2019

[C# Language Design Notes for Jan 7, 2019](LDM-2019-01-07.md)

Nullable:

1. Variance in overriding/interface implementation
2. Breaking change in parsing array specifiers

