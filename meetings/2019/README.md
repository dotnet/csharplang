# C# Language Design Notes for 2019

Overview of meetings and agendas for 2019

## No meetings yet

# Upcoming meetings

## Jan 7, 2019

- Nullable: never null warning 
- Nullable: re-discuss uninitialized field warnings
- Nullable: What warnings if any should be reported about nullable mismatch for overrides/implements. What constitutes a mismatch. (https://github.com/dotnet/roslyn/issues/23268, https://github.com/dotnet/roslyn/issues/30958) (Aleksey)
- Nullable: Breaking change in parsing array specifiers (https://github.com/dotnet/roslyn/issues/32141) (Neal)

## Jan 9, 2019

- Async-streams: re-discuss pattern-based `await foreach` (Stephen)
- Recursive Patterns Open Language Issues https://github.com/dotnet/csharplang/issues/2095

## Jan 14, 2019

- Generating null-check for `parameter!` (Jared)

## Jan 16, 2019

## Jan 23, 2019

## Feb 11, 2019

## Feb 13, 2019

## Schedule ASAP

## Schedule when convenient

- Discussion of refreshing language spec (Neal)
- Nullability flow through conditional access when unconstrained generic type parameters are involved. (Aleksey)
- Allowing pattern-based `foreach` and `await foreach` to bind to an extension `GetEnumerator`/`GetAsyncEnumerator` and `MoveNext`/`MoveNextAsync` (Julien/Chris)
- Confirm shadowing rules for local functions (Chuck/Julien)
- Making a `CancellationToken` available in async-iterator method bodies; possibly reserving a keyword (Julien/Stephen) 
- Confirm whether reachability should affect nullability analysis. (Fred/Chuck/Aleksey)

## Recurring topics

- *Triage championed features*
- *Triage milestones*
- *Design review*
