# Upcoming meetings for 2020

## Schedule ASAP

- Parameter null checking

## Schedule when convenient


## Recurring topics

- *Triage championed features*
- *Triage milestones*
- *Design review*

## Jul 1, 2020

## Jun 29, 2020

- Record Monday (Andy, Jared, Mads)

## Jun 24, 2020

## Jun 22, 2020

- Record Monday (Andy, Jared, Mads)

## Jun 17, 2020

## Jun 15, 2020

- Record Monday (Andy, Jared, Mads)

# Jun 10, 2020

- https://github.com/dotnet/csharplang/issues/1711 Roles, extensions and static interfaces (Mads)

## Jun 8, 2020

- Record Monday (Andy, Jared, Mads)

## Jun 3, 2020

- allow suppression on `return someBoolValue!;` (issue https://github.com/dotnet/roslyn/issues/44080, Julien)
- record decision on side-effect of `M(someMaybeNullValue); // warns` and effect of suppression `expr!` (issue https://github.com/dotnet/roslyn/issues/43383, Julien)
- improving suppression in the middle of null-coalescing (https://github.com/dotnet/csharplang/issues/3393, Neal/Julien)
- init-only: should `_ = new C() { readonlyField = null };` be allowed in a method on type `C`? (Jared/Julien)
- init-only: confirm metadata encoding (`IsExternalInit` modreq) with compat implications (Jared/Julien)
- init-only: init-only methods ? `init void Init()` (Jared/Julien)

## May 18, 2020

- Record Monday (Andy, Jared, Mads)

## May 11, 2020

- Record Monday (Andy, Jared, Mads)
    - https://gist.github.com/MadsTorgersen/3fb6b7461e211c8458044ad5115f2117 Primary constructors and records (Mads)

## April 29, 2020

- Design review

## April 22, 2020

- https://github.com/dotnet/csharplang/projects/4 Triage for C# 10.0 (Mads)

## March 18, 2020

- https://github.com/jaredpar/csharplang/blob/record/proposals/recordsv3.md clone-style records (Jared)

## Jan 29, 2020

- Records: drilling in to individual features (Mads)

## Jan 13, 2020

- Records: Paging back in the previous proposal (Andy)

# C# Language Design Notes for 2020

Overview of meetings and agendas for 2020

## Jun 1, 2020

[C# Language Design Notes for June 1, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-01.md)

Records:
    1. Base call syntax
    2. Synthesizing positional record members and assignments
    3. Record equality through inheritance

## May 27, 2020

[C# Language Design Notes for May 27, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-05-27.md)

Record syntax
    1. Record structs?
    2. Record syntax/keyword
    3. Details on property shorthand syntax

## March 11, 2020

[C# Language Design Notes for May 11, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-05-11.md)

Records

## May 6, 2020

[C# Language Design Notes for May 6, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-05-06.md)

1. Target-typing ?: when the natural type isn't convertible to the target type.
1. Allow `if (x is not string y)` pattern.
1. Open issues in extension `GetEnumerator`
1. Args in top-level programs

## May 4, 2020

[C# Language Design Notes for May 4, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-05-04.md)

1. Reviewing design review feedback

## April 27, 2020

[C# Language Design Notes for April 27, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-04-27.md)

Records: positional & primary constructors

## April 20, 2020

[C# Language Design Notes for April 20, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-04-20.md)

Records: Factories

## April 15, 2020

[C# Language Design Notes for April 15, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-04-15.md)

1. Non-void and non-private partial methods
2. Top-level programs

## April 13. 2020

[C# Language Design Notes for April 13, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-04-13.md)

1. Roadmap for records
2. Init-only properties

## April 8, 2020

[C# Language Design Notes for April 8, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-04-08.md)

1. `e is dynamic` pure null check
2. Target typing `?:`
3. Inferred type of an `or` pattern
4. Module initializers

## April 6, 2020

[C# Language Design Notes for April 6, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-04-06.md)

1. Record Monday: Init-only members

## April 1, 2020

[C# Language Design Notes for April 1, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-04-01.md)

1. Function pointer design adjustments

2. `field` keyword in properties

## March 30, 2020

1. Record Monday

[C# Language Design Notes for March 30, 2020](LDM-2020-03-30.md)

## March 25, 2020

[C# Language Design Notes for March 25, 2020](LDM-2020-03-25.md)

1. Open issues with native int

2. Open issues with target-typed new

## March 23, 2020

[C# Language Design Notes for March 23, 2020](LDM-2020-03-23.md)

1. Triage
2. Builder-based records

## March 9, 2020

[C# Language Design Notes for March 9, 2020](LDM-2020-03-09.md)

1. Simple programs

2. Records

## Feb 26, 2020

[C# Language Design Notes for Feb. 26, 2020](LDM-2020-02-26.md)

Design Review

## Feb 24

[C# Language Design Notes for Feb. 24, 2020](LDM-2020-02-24.md)

Taking another look at "nominal" records

## Feb 19

[C# Language Design Notes for Feb. 19, 2020](LDM-2020-02-19.md)

State-based value equality

## Feb 12

[C# Language Design Notes for Feb. 12, 2020](LDM-2020-02-12.md)

Records

## Feb 10

[C# Language Design Notes for Feb. 10, 2020](LDM-2020-02-10.md)

Records

## Feb 5

[C# Language Design Notes for Feb. 5, 2020](LDM-2020-02-05.md)

- Nullability of dependent calls (Chuck, Julien)
- https://github.com/dotnet/csharplang/issues/3137 Records as individual features (Mads)

## Feb 3

[C# Language Design Notes for Feb. 3, 2020](LDM-2020-02-03.md)

Value Equality

## Jan 29, 2020

[C# Language Design Notes for Jan. 29, 2020](LDM-2020-01-29.md)

Records: "With-ers"

## Jan 22, 2020

[C# Language Design Notes for Jan 22, 2020](LDM-2020-01-22.md)

1. Top-level statements and functions
2. Expression Blocks

## Jan 15, 2020

[C# Language Design Notes for Jan 15, 2020](LDM-2020-01-15.md)

Records

1. "programming with data"
1. Decomposing subfeatures of records

## Jan 8, 2020

[C# Language Design Notes for Jan 8, 2020](LDM-2020-01-08.md)

1. Unconstrained type parameter annotation
2. Covariant returns

## Jan 6, 2020

[C# Language Design Notes for Jan 6, 2020](LDM-2020-01-06.md)

1. Use attribute info inside method bodies
1. Making Task-like types covariant for nullability
1. Casting to non-nullable reference type
1. Triage
