# Upcoming meetings for 2019

## Schedule ASAP
- Triage [#2152](https://github.com/dotnet/csharplang/issues/2152): "Allow Obsolete attribute on getters and setters"

## Schedule when convenient

- Discussion of refreshing language spec (Neal)
- Allowing pattern-based `foreach` and `await foreach` to bind to an extension `GetEnumerator`/`GetAsyncEnumerator` and `MoveNext`/`MoveNextAsync` (Julien/Chris)
- Making a `CancellationToken` available in async-iterator method bodies; possibly reserving a keyword (Julien/Stephen) 
- Syntax of positional records/primary constructors (Andy)

## Recurring topics

- *Triage championed features*
- *Triage milestones*
- *Design review*

## Feb 13, 2019

## Feb 11, 2019

Nullable:
1. Tracking assignments through refs and ref expressions (e.g. `(q ? ref x : ref y) = null;` or  `(q ? ref x : ref y) = "";`), and the state of a variable to which a ref has been taken.
2. Nullability flow through conditional access when unconstrained generic type parameters are involved. (Aleksey)
3. `!` on L-values https://github.com/dotnet/roslyn/issues/27522
4. reset state with `is` https://github.com/dotnet/roslyn/issues/30297
5. Do we want to have an analysis that can tell when a test against null would have a known result, so we can produce a hidden diagnostic?  [roslyn#29868](https://github.com/dotnet/roslyn/issues/29868) (Aleksey/Neal)
6. Should reachability affect nullable analysis? [roslyn#28798](https://github.com/dotnet/roslyn/issues/28798) [roslyn#30949](https://github.com/dotnet/roslyn/issues/30949) [roslyn#32047](https://github.com/dotnet/roslyn/issues/32047) (Fred/Chuck/Aleksey)
7. element-wise analysis of tuple conversions https://github.com/dotnet/roslyn/issues/33035
8. Should `throw null` warn?
9. `MaybeNull` and other attributes, and the relation to unspeakable types. https://github.com/dotnet/roslyn/issues/30953 (Chuck)
10. `var?` https://github.com/dotnet/roslyn/issues/31874

# C# Language Design Notes for 2019

Overview of meetings and agendas for 2019

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

