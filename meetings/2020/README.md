# Upcoming meetings for 2020

## Schedule ASAP



## Schedule when convenient

- How can we improve the capture of unfinished business, both in terms of making sure the LDM comes back to remaining open issues, and in terms of keeping the feature specs in sync with LDM decisions?

## Recurring topics

- *Triage championed features and milestones*
- *Design review*

## Sep 30, 2020

- *Design review*

## Sep 28, 2020

## Sep 23, 2020

- General improvements to the `struct` experience (Jared) 
    - `ref` fields
    - returning fields by ref
    - safe fixed arrays
    - `where T : ref struct`
    - ref structs implementing interfaces.

## Sep 21, 2020

- https://github.com/dotnet/roslyn/issues/15936 (Fred)
    - Should we keep the existing CA warning or move it to the compiler?
    - What about float.NaN, Half.NaN?
- *Triage championed features and milestones*

## Jun 3, 2020

- allow suppression on `return someBoolValue!;` (issue https://github.com/dotnet/roslyn/issues/44080, Julien)
- record decision on side-effect of `M(someMaybeNullValue); // warns` and effect of suppression `expr!` (issue https://github.com/dotnet/roslyn/issues/43383, Julien)
- improving suppression in the middle of null-coalescing (https://github.com/dotnet/csharplang/issues/3393, Neal/Julien)
- init-only: should `_ = new C() { readonlyField = null };` be allowed in a method on type `C`? (Jared/Julien)
- init-only: confirm metadata encoding (`IsExternalInit` modreq) with compat implications (Jared/Julien)
- init-only: init-only methods ? `init void Init()` (Jared/Julien)

## May 18, 2020

- Record Monday (Andy, Jared, Mads)

## April 29, 2020

- Design review

## April 22, 2020

- https://github.com/dotnet/csharplang/projects/4 Triage for C# 10.0 (Mads)

## March 18, 2020

- https://github.com/jaredpar/csharplang/blob/record/proposals/recordsv3.md clone-style records (Jared)

## Jan 13, 2020

- Records: Paging back in the previous proposal (Andy)

# C# Language Design Notes for 2020

Overview of meetings and agendas for 2020

## Sep 16, 2020

[C# Language Design Notes for September 16th, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-09-16.md)

- Required Properties
- Triage

## Sep 14, 2020

[C# Language Design Notes for September 14th, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-09-14.md)

- Partial method signature matching
- Null-conditional handling of the nullable suppression operator
- Annotating IEnumerable.Cast
- Nullability warnings in user-written record code
- Tuple deconstruction mixed assignment and declaration

## Sep 9, 2020

[C# Language Design Notes for September 9th, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-09-09.md)

- Triage issues still in C# 9.0 candidate
- Triage issues in C# 10.0 candidate

## Aug 24, 2020

[C# Language Design Notes for August 24th, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-08-24.md)

- Warnings on types named `record`
- `base` calls on parameterless `record`s
- Omitting unnecessary synthesized `record` members
- [`record` `ToString` behavior review](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/records.md#printing-members-printmembers-and-tostring-methods)
    - Behavior of trailing commas
    - Handling stack overflows
    - Should we omit the implementation of `ToString` on `abstract` records
    - Should we call `ToString` prior to `StringBuilder.Append` on value types
    - Should we try and avoid the double-space in an empty record
    - Should we try and make the typename header print more economic
- Reference equality short circuiting

## Jul 27, 2020

[C# Language Design Notes for July 27th, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-07-27.md)

- [Improved nullable analysis in constructors](https://github.com/RikkiGibson/csharplang/blob/nullable-ctor/proposals/nullable-constructor-analysis.md) (Rikki)
- [Equality operators (`==` and `!=`) in records](https://github.com/dotnet/csharplang/issues/3707#issuecomment-661800278) (Fred)
- `.ToString()` or `GetDebuggerDisplay()` on records? (Julien)
- Restore W-warning to `T t = default;` for generic `T`, now you can write `T?`? (Julien) 

## Jul 20, 2020

[C# Language Design Notes for July 20th, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-07-20.md)

- [struct private fields in definite assignment](https://github.com/dotnet/csharplang/issues/3431) (Neal/Julien)
    - [Proposal 1](https://github.com/dotnet/roslyn/issues/30194#issuecomment-657858716)
    - [Proposal 2](https://github.com/dotnet/roslyn/issues/30194#issuecomment-657900257)
- Finish [Triage](https://github.com/dotnet/csharplang/issues?q=is%3Aopen+is%3Aissue+label%3A%22Proposal+champion%22+no%3Amilestone)
- Records-related features to pick up in the next version of C# (Mads)


## Jul 13, 2020

[C# Language Design Notes for July 13th, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-07-13.md)

- Triage open issues

## Jul 6, 2020

[C# Language Design Notes for July 6, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-07-06.md)

- [Repeat Attributes in Partial Members](https://github.com/RikkiGibson/csharplang/blob/repeated-attributes/proposals/repeat-attributes.md) (Rikki)
- `sealed` on `data` members
- [Required properties](https://github.com/dotnet/csharplang/issues/3630) (Fred)


## Jul 1, 2020

[C# Language Design Notes for July 1, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-07-01.md)

- [Non-defaultable struct types](https://github.com/dotnet/csharplang/issues/99#issuecomment-601792573) (Sam, Chuck)
- Confirm unspeakable `Clone` method and long-term implications (Jared/Julien)

## Jun 29, 2020

[C# Language Design Notes for June 29, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-29.md)

- [Static interface members](https://github.com/Partydonk/partydonk/issues/1)  (Miguel, Aaron, Mads, Carol)

## Jun 24, 2020

[C# Language Design Notes for June 24, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-24.md)

- Parameter null checking: finalize syntax
- https://github.com/dotnet/csharplang/issues/3275 Variance on static interface members (Aleksey)
- [Function pointer question](https://github.com/dotnet/roslyn/issues/39865#issuecomment-647692516) (Fred)


## Jun 22, 2020

[C# Language Design Notes for June 22, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-22.md)

1. Data properties

1. Clarifying what's supported in records for C# 9

    - Structs

    - Inheritance with records and classes

## Jun 17, 2020

[C# Language Design Notes for June 17, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-17.md)

1. Null-suppression & null-conditional operator
1. `parameter!` syntax
1. `T??`

## Jun 15, 2020

[C# Language Design Notes for June 15, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-15.md)

Record:

1. `modreq` for init accessors

1. Initializing `readonly` fields in same type

1. `init` methods

1. Equality dispatch

1. Confirming some previous design decisions

1. `IEnumerable.Current`

## Jun 10, 2020

[C# Language Design Notes for June 10, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-10.md)

- https://github.com/dotnet/csharplang/issues/1711 Roles and extensions

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

## May 11, 2020

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
