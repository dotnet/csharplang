# C# Language Design Notes for 2018

Overview of meetings and agendas for 2018


## Jan 3, 2018
[C# Language Design Notes for Jan 3, 2018](LDM-2018-01-03.md)

1. Scoping of expression variables in constructor initializer
2. Scoping of expression variables in field initializer
3. Scoping of expression variables in query clauses
4. Caller argument expression attribute
5. Other caller attributes
6. New constraints


## Jan 10, 2018
[C# Language Design Notes for Jan 10, 2018](LDM-2018-01-10.md)

1. Ranges and endpoint types


## Jan 18, 2018 
[C# Language Design Notes for Jan 18, 2018](LDM-2018-01-18.md)

We discussed the range operator in C# and the underlying types for it.

1. Scope of the feature
2. Range types
3. Type name
4. Open-ended ranges
5. Empty ranges
6. Enumerability
7. Language questions


## Jan 22, 2018
[C# Language Design Notes for Jan 22, 2018](LDM-2018-01-22.md)

We continued to discuss the range operator in C# and the underlying types for it.

1. Inclusive or exclusive?
2. Natural type of range expressions
3. Start/length notation


## Jan 24, 2018
[C# Language Design Notes for Jan 24, 2018](LDM-2018-01-24.md)

1. Ref reassignment
2. New constraints
3. Target typed stackalloc initializers
4. Deconstruct as ref extension method

## July 9, 2018
[C# Language Design Notes for July 9, 2018](LDM-2018-07-09.md)

1. `using var` feature
   1. Overview
   2. Tuple deconstruction grammar form
   3. `using expr;` grammar form
   4. Flow control safety
2. Pattern-based Dispose in the `using` statement
3. Relax Multiline interpolated string syntax (`$@`)

## July 11, 2018
[C# Language Design Notes for July 11, 2018](LDM-2018-07-11.md)

1. Controlling nullable reference types with feature flags
1. Interaction with NonNullTypesAttribute
1. Feature flag and 'warning waves'
1. How 'oblivious' null types interact with generics
1. Nullable and interface generic constraints

## July 16, 2018
[C# Language Design Notes for July 16, 2018](LDM-2018-07-16.md)

1. Null-coalescing assignment
   1. User-defined operators
   1. Unconstrained type parameters
   1. Throw expression the right-hand side
1. Nullable await
1. Nullable pointer access
1. Non-nullable reference types feature flag follow-up

##  August 20, 2018

[C# Language Design Notes for August 20, 2018](LDM-2018-08-20.md)

1. Remaining questions on [suppression operator](https://na01.safelinks.protection.outlook.com/?url=https%3A%2F%2Fgithub.com%2Fdotnet%2Froslyn%2Fissues%2F28271&data=02%7C01%7C%7C6defe1e21ab54cce8d0008d606be5d23%7C72f988bf86f141af91ab2d7cd011db47%7C1%7C0%7C636703812006445395&sdata=DAdh5dev1mnr%2F5zxtvuJVcHP%2Bzewrzz4z9iuGkl%2BUHg%3D&reserved=0) (and possibly cast)
2. Does a dereference update the null-state?
3. Null contract attributes
4. Expanding the feature
5. Is T? where T : class? allowed or meaningful?
6. Confirm that oblivious should be ephemeral
7. Unconstrained T in List<T> then `FirstOrDefault()`. What attribute to annotate `FirstOrDefault`?

## August 22, 2018

[C# Language Design Notes for August 22, 2018](LDM-2018-08-22.md)

1. Target-typed new
1. Clarification on constraints with nullable reference types enabled

## September 5, 2018

[C# Language Design Notes for September 5, 2018](LDM-2018-09-05.md)

1. Index operator: is it a unary operator?
1. Compiler intrinsics


# Upcoming meetings

## Sep 19, 2018

- *Triage championed features*
- *Triage milestones*

## Sep 24, 2018

- C#/F# interactions (Don Syme, Phillip Carter)

## Sep 26, 2018

- Nullable type inference with respect to null-oblivious types (Chuck, Fred)

## Oct 1, 2018


## Oct 3, 2018


## Oct 10, 2018


## Schedule ASAP

- Pattern matching open questions (Neal)

## Schedule when convenient

- Readonly methods in structs (Jared)
- Discussion of refreshing language spec (Neal)
- [Open issues](https://github.com/dotnet/csharplang/issues/406) with default interface methods (Aleksey, David) 
- Discussion of records (Andy)

## Recurring topics

- *Triage championed features*
- *Triage milestones*
- *Design review*
