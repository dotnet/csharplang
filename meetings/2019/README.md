# Upcoming meetings for 2019

## Schedule ASAP
- Triage [#2152](https://github.com/dotnet/csharplang/issues/2152): "Allow Obsolete attribute on getters and setters"

## Schedule when convenient

- Discussion of refreshing language spec (Neal)
- Nullability flow through conditional access when unconstrained generic type parameters are involved. (Aleksey)
- Allowing pattern-based `foreach` and `await foreach` to bind to an extension `GetEnumerator`/`GetAsyncEnumerator` and `MoveNext`/`MoveNextAsync` (Julien/Chris)
- Making a `CancellationToken` available in async-iterator method bodies; possibly reserving a keyword (Julien/Stephen) 
- Confirm whether reachability should affect nullability analysis. (Fred/Chuck/Aleksey)
- Syntax of positional records/primary constructors (Andy)

## Recurring topics

- *Triage championed features*
- *Triage milestones*
- *Design review*

## Feb 13, 2019

## Feb 11, 2019

## Jan 23, 2019

- Function pointers (Jared, Alexandre)
- Any urgent outstanding topics before two week break

## Jan 16, 2019

- Confirm shadowing rules for local functions (Chuck/Julien)
- pattern-based disposal in `await foreach` (Julien/Stephen)

# C# Language Design Notes for 2019

Overview of meetings and agendas for 2019

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

