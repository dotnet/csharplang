# Upcoming meetings for 2023

All schedule items must have a public issue or checked-in proposal that can be linked from the notes.

## Schedule ASAP

- [nullability analysis of collection expressions](https://github.com/dotnet/csharplang/issues/7626) (Julien)

## Schedule when convenient

## Recurring topics

- *Triage championed features and milestones*
- *Design review*

## Mon Dec 18, 2023

## Wed Dec 13, 2023

## Mon Dec 4, 2023

## Wed Nov 29, 2023

## Wed Nov 15, 2023

## Wed Oct 18, 2023

- C# 13 planning (Jared and Mads - no notes)

# C# Language Design Notes for 2023

## Mon Oct 16, 2023

[C# Language Design Meeting for October 16th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-16.md)

- Triage
    - Breaking change warnings
    - Determine natural type of method group by looking scope-by-scope
    - u8 string interpolation
    - Lock statement pattern
    - String/Character escape sequence \\e as a short-hand for \\u001b 
    - New operator %% for canonical Modulus operations

## Wed Oct 11, 2023

[C# Language Design Meeting for October 11th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-11.md)

- C# spec update
- Collection expressions

## Mon Oct 9, 2023

[C# Language Design Meeting for October 9th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-09.md)

- Triage
    - ReadOnlySpan initialization from static data
    - Embedded Language Indicators for raw string literals
    - list-patterns on enumerables
    - Make generated \`Program\`\` for top-level statements public by default
    - CallerCharacterNumberAttribute
    - Add private and namespace accessibility modifiers for top-level types
    - Require await to apply nullable postconditions to task-returning calls
    - `is` expression evaluating `const` expression should be considered constant

## Wed Oct 4, 2023

[C# Language Design Meeting for October 4th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-04.md)

- Trimming and AOT

## Mon Oct 2, 2023

[C# Language Design Meeting for October 2nd, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-02.md)

- Collection expressions

## Wed Sept 27, 2023

[C# Language Design Meeting for September 27th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-09-27.md)

- Collection expressions

## Mon Sept 25, 2023

[C# Language Design Meeting for September 25th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-09-25.md)

- Primary constructors
- Defining well-defined behavior for collection expression types

## Wed Sept 20, 2023

[C# Language Design Meeting for September 20th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-09-20.md)

- Collection expressions
    - Type inference from spreads
    - Overload resolution fallbacks

## Mon Sept 18, 2023

[C# Language Design Meeting for September 18th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-09-18.md)

- Collection expression questions
    - Optimizing non-pattern collection construction
    - Avoiding intermediate buffers for known-length cases

## Wed Aug 16 2023

[C# Language Design Meeting for August 16th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-08-16.md)

- Ref-safety scope for collection expressions
- Experimental attribute

## Mon Aug 14, 2023

[C# Language Design Meeting for August 14th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-08-14.md)

- Betterness for collection expressions and span types
- Type inference from collection expression elements
- Collection expression conversions

## Wed Aug 9, 2023

[C# Language Design Meeting for August 9th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-08-09.md)

- Lambdas with explicit return types
- Target typing of collection expressions to core interfaces
- Loosening requirements for collection builder methods

## Mon Aug 7, 2023

[C# Language Design Meeting for August 7th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-08-07.md)

- Improvements to method group natural types

## Mon Jul 31, 2023

[C# Language Design Meeting for July 31st, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-31.md)

- Primary constructor parameters and `readonly`

## Wed Jul 26, 2023

[C# Language Design Meeting for July 26th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-26.md)

- Primary constructor parameters and `readonly`

## Mon Jul 24, 2023

[C# Language Design Meeting for July 24th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-24.md)

- Method group natural types with extension members
- Interceptors

## Mon Jul 17, 2023

[C# Language Design Meeting for July 17th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-17.md)

- Compiler Check-in
- `readonly` parameters

## Wed Jul 12, 2023

[C# Language Design Meeting for July 12th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-12.md)

- Collection Literals
    - `Create` methods
    - Extension methods
- Interceptors

## Mon Jun 19, 2023

[C# Language Design Meeting for June 19th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-06-19.md)

- Prefer spans over interfaces in overload resolution
- Collection literals

## Mon Jun 5, 2023

[C# Language Design Meeting for June 5th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-06-05.md)

- Collection literals

## Wed May 31, 2023

[C# Language Design Meeting for May 31st, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-05-31.md)

- Collection literals

## Wed May 17, 2023

[C# Language Design Meeting for May 17th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-05-17.md)

- Inline arrays

## Mon May 15, 2023

[C# Language Design Meeting for May 15th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-05-15.md)

- Breaking Change Warnings
- Primary Constructors

## Mon May 8, 2023

[C# Language Design Meeting for May 8th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-05-08.md)

- Primary Constructors

## Wed May 3, 2023

[C# Language Design Meeting for May 3rd, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-05-03.md)

- Inline Arrays
- Primary constructors
    - Attributes on captured parameters
    - Warning for shadowing base members
- Collection literal natural type

## Mon May 1, 2023

[C# Language Design Meeting for May 1st, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-05-01.md)

- Fixed Size Buffers
- `lock` statement improvements

## Wed Apr 26, 2023

[C# Language Design Meeting for April 26th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-04-26.md)

- Collection literals

## Mon Apr 10, 2023

[C# Language Design Meeting for April 10th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-04-10.md)

* Fixed Size Buffers

## Mon Apr 3, 2023

[C# Language Design Meeting for April 3rd, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-04-03.md)

- Collection Literals
- Fixed-size buffers

## Wed Mar 15, 2023 (No notes)

- Discriminated Unions
- Interceptors

## Mon Mar 13, 2023 (Shorter meeting)

[C# Language Design Meeting for March 13th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-03-13.md)

- Unsafe in aliases hole
- Attributes on primary ctors

## Wed Mar 8, 2023

[C# Language Design Meeting for March 8th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-03-08.md)

- Discriminated Unions
- Limited Breaking Changes in C#

## Wed Mar 1, 2023 (Shorter meeting)

[C# Language Design Meeting for March 1st, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-03-01.md)

- Discriminated Unions Summary

## Mon Feb 27, 2023

[C# Language Design Meeting for February 27th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-02-27.md)

- Interceptors

## Wed Feb 22, 2023

[C# Language Design Meeting for February 22nd, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-02-22.md)

- Primary Constructors
- Extensions

## Wed Feb 15, 2023

[C# Language Design Meeting for February 15th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-02-15.md)

- Open questions in primary constructors
    - Capturing parameters in lambdas
    - Assigning to `this` in a `struct`

## Wed Feb 1, 2023

[C# Language Design Meeting for February 1st, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-02-01.md)

- Position of `unsafe` in aliases
- Roles and extensions

## Wed Jan 18, 2023

[C# Language Design Meeting for January 18th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-01-18.md)

- Nullable post-conditions and `async`/`await`
- Semi-colon bodies for type declarations

## Wed Jan 11, 2023

[C# Language Design Meeting for January 11th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-01-11.md)

- `using` aliases for any types
    - Pointer types in aliases
    - Reference nullability in aliases
    - Value nullability in aliases

## Mon Jan 9, 2023

[C# Language Design Meeting for January 9th, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-01-09.md)

- Working group re-evaluation
