# Upcoming meetings for 2022

All schedule items must have a public issue or checked in proposal that can be linked from the notes.

## Schedule ASAP


## Schedule when convenient

* Variable declarations under disjunctive patterns (Fred/Julien): https://github.com/dotnet/csharplang/blob/main/proposals/pattern-variables.md

## Recurring topics

- *Triage championed features and milestones*
- *Design review*

## May 23, 2022

## May 11, 2022

## May 9, 2022

- Numeric IntPtr (Julien): https://github.com/dotnet/csharplang/issues/6065

# C# Language Design Notes for 2022

Overview of meetings and agendas for 2022

## May 2, 2022

[C# Language Design Meeting for May 2nd, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-05-02.md)

- Effect of `SetsRequiredMembers` on nullable analysis
- `field` questions
    - Partial overrides of virtual properties
    - Definite assignment of manually implemented setters

## Apr 27, 2022

[C# Language Design Meeting for April 27th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-04-27.md)

- Default parameter values in lambdas
- Null-conditional assignment

## Apr 25, 2022

[C# Language Design Meeting for April 25th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-04-25.md)

- `ref readonly` method parameters
- Inconsistencies around accessibility checks for interface implementations

## Apr 18, 2022

[C# Language Design Meeting for April 18th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-04-18.md)

1. Issues with Utf8 string literals
2. Ref and ref struct scoping modifiers

## Apr 13, 2022

[C# Language Design Meeting for April 13th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-04-13.md)

1. Parameter null checking
2. File-scoped types

## Apr 11, 2022

[C# Language Design Meeting for April 11th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-04-11.md)

1. Relax restrictions on braces on raw interpolated strings
2. Self-type stopgap attribute

## Apr 6, 2022

[C# Language Design Meeting for April 6th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-04-06.md)

1. Unresolved questions for static virtual members
2. Parameter null checking

## Mar 30, 2022

[C# Language Design Meeting for March 30th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-30.md)

1. Definite assignment in struct constructors calling `: this()`
2. `file private` accessibility

## Mar 28, 2022

[C# Language Design Meeting for March 28th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-28.md)

1. Variable declarations under disjunctive patterns
2. Type hole in static abstracts
3. Self types

## Mar 23, 2022

[C# Language Design Meeting for March 23rd, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-23.md)

1. Open questions in required members
    1. Emitting `SetsRequiredMembers` for record copy constructors
    2. Should `SetsRequiredMembers` suppress errors?
    3. Unsettable members
    4. Ref returning properties
    5. Obsolete members

## Mar 21, 2022

[C# Language Design Meeting for March 21st, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-21.md)

1. file private visibility
2. Open question in semi-auto properties
3. Open question in required members

## Mar 14, 2022

[C# Language Design Meeting for March 14th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-14.md)

1. file private visibility

## Mar 9, 2022

[C# Language Design Meeting for March 9th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-09.md)

1. Ambiguity of `..` in collection expressions
2. `main` attributes
3. `nameof(param)`

## Mar 7, 2022

- Design review. No published notes.

## Mar 2, 2022

[C# Language Design Meeting for March 2nd, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-02.md)

1. Open questions in `field`
    1. Initializers
    2. Property assignment in structs

## Feb 28, 2022

[C# Language Design Meeting for February 28th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-28.md)

1. Ref fields
    1. Encoding strategy
    2. Keywords vs Attributes
    3. Breaking existing lifetime rules

## Feb 23, 2022

[C# Language Design Meeting for February 23rd, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-23.md)

1. Pattern matching over `Span<char>`
2. Checked operators

## Feb 16, 2022

[C# Language Design Meeting for February 16th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-16.md)

1. Open questions in `field`
2. Triage
    1. User-defined positional patterns
    2. Delegate type arguments improvements
    3. Practical existential types for interfaces
    4. Static abstract interfaces and static classes

## Feb 14, 2022

[C# Language Design Meeting for February 14th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-14.md)

1. Definite assignment in structs
2. Checked operators

## Feb 9, 2022

[C# Language Design Meeting for February 9th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-09.md)

1. Continue discussion of checked user-defined operators
2. Review proposal for unsigned right shift operator
3. Review proposal for relaxing shift operator requirements
4. Triage champion features

## Feb 7, 2022

[C# Language Design Meeting for February 7th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-07.md)

1. Checked user-defined operators

## Jan 26, 2022

[C# Language Design Notes for January 26th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md)

1. Open questions in UTF-8 string literals

## Jan 24, 2022

[C# Language Design Notes for January 24th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-24.md)

1. Required members metadata representation
2. Default implementations of abstract statics
3. Triage
    1. Nested members in with and object creation
    2. Binary Compat Only
    3. Attribute for passing caller identity implicitly
    4. Attributes on Main for top level programs

## Jan 12, 2022

[C# Language Design Notes for January 12th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-12.md)

1. Open questions for `field`
    1. Initializers for semi-auto properties
    2. Definite assignment for struct types
2. Generic Math Operator Enhancements

## Jan 5, 2022

[C# Language Design Notes for January 5th, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-05.md)

1. Required Members

## Jan 3, 2022

[C# Language Design Notes for January 3rd, 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-03.md)

1. Slicing assumptions in list patterns, revisited
2. Parameterless struct constructors, revisited
