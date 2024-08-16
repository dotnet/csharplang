# Upcoming meetings for 2024

All schedule items must have a public issue or checked-in proposal that can be linked from the notes.

## Schedule ASAP

- Extension [conversions](https://github.com/dotnet/csharplang/pull/8340) (Julien/Mads)

## Schedule when convenient

- Consider [Block-bodied switch expression arms](https://github.com/dotnet/csharplang/issues/3037) (Fred)

## Recurring topics

- *Triage championed features and milestones*
- *Design review*

## Schedule

### Wed Aug 28, 2024

### Mon Aug 26, 2024

### Wed Aug 21, 2024

- Open questions in `field` (Chuck/Cyrus) - https://github.com/dotnet/csharplang/blob/main/proposals/field-keyword.md#open-ldm-questions

### Mon Aug 19, 2024

- [Better conversion from collection expression](https://github.com/dotnet/csharplang/pull/8348) (Fred)

### Wed Aug 14, 2024

- Field access in auto properties and nullable-ref-types (Cyrus/Jared)

## C# Language Design Notes for 2024

### Wed Jul 24, 2024

[C# Language Design Meeting for July 24th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-07-24.md)

- Discriminated Unions
- Better conversion from collection expression with `ReadOnlySpan<T>` overloads

### Mon Jul 22, 2024

[C# Language Design Meeting for July 22nd, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-07-22.md)

- Extensions
- Ref structs implementing interfaces

### Wed Jul 17, 2024

[C# Language Design Meeting for July 17th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-07-17.md)

- Overload resolution priority open questions
- Better conversion from collection expression with `ReadOnlySpan<T>` overloads

### Mon Jul 15, 2024

[C# Language Design Meeting for July 15th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-07-15.md)

- `field` keyword
- First-Class Spans Open Question

### Wed Jun 26, 2024

[C# Language Design Meeting for June 26th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-26.md)

- Extensions

### Mon Jun 24, 2024

[C# Language Design Meeting for June 24th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-24.md)

- First-Class Spans
- `field` questions
    - Mixing auto-accessors
    - Nullability

### Mon Jun 17, 2024

[C# Language Design Meeting for June 17th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-17.md)

- `params` Span breaks
- Overload resolution priority questions
    - Application error or warning on `override`s
    - Implicit interface implementation
- Inline arrays as `record struct`s

### Wed Jun 12, 2024

[C# Language Design Meeting for June 12th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-12.md)

- `params Span` breaks
- Extensions

### Mon Jun 10, 2024

[C# Language Design Meeting for June 10th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-10.md)

- `ref struct`s implementing interfaces and in generics

### Mon Jun 3, 2024

[C# Language Design Meeting for June 3rd, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-03.md)

- Params collections and dynamic
- Allow ref and unsafe in iterators and async

### Wed May 15, 2024

[C# Language Design Meeting for May 15th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-05-15.md)

- `field` and `value` as contextual keywords
  - Usage in `nameof`
  - Should `value` be a keyword in a property or indexer get? Should `field` be a keyword in an indexer?
  - Should `field` and `value` be considered keywords in lambdas and local functions within property accessors?
  - Should `field` and `value` be keywords in property or accessor signatures? What about `nameof` in those spaces?
- Dictionary expressions

### Mon May 13, 2024

[C# Language Design Meeting for May 13th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-05-13.md)

- First-class span types questions
    - Delegate conversions
    - Variant conversion existence
- Overload resolution priority questions
    - Attribute shape and inheritance
    - Extension overload resolution

### Wed May 8, 2024

[C# Language Design Meeting for May 8th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-05-08.md)

- `readonly` for primary constructor parameters

### Wed May 1, 2024

[C# Language Design Meeting for May 1st, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-05-01.md)

- Adjust binding rules in the presence of a single candidate

### Wed Apr 24, 2024

[C# Language Design Meeting for Apr 24th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-04-24.md)

- Adjust dynamic binding rules for a situation of a single applicable candidate

### Mon Apr 22, 2024

[C# Language Design Meeting for Apr 22nd, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-04-22.md)

- Effect of language version on overload resolution in presence of `params` collections
- Partial type inference: '_' in method and object creation type argument lists

### Wed Apr 17, 2024

[C# Language Design Meeting for Apr 17th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-04-17.md)

- Relax `Add` requirement for collection expression conversions to types implementing `IEnumerable`
- Extensions

### Mon Apr 15, 2024

[C# Language Design Meeting for Apr 15th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-04-15.md)

- Non-enumerable collection types
- Interceptors
- Relax `Add` requirement for collection expression conversions to types implementing `IEnumerable`

### Mon Apr 8, 2024

[C# Language Design Meeting for Apr 8th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-04-08.md)

- Implementation specific documentation

### Mon Apr 1, 2024

[C# Language Design Meeting for Apr 1st, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-04-01.md)

- Async improvements (Async2)

### Wed Mar 27, 2024

[C# Language Design Meeting for Mar 27th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-27.md)

- Discriminated Unions

### Mon Mar 11, 2024

[C# Language Design Meeting for Mar 11th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-11.md)

- Dictionary expressions

### Mon Mar 4, 2024

[C# Language Design Meeting for Mar 4th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-04.md)

- Breaking changes: making `field` and `value` contextual keywords
- Overload resolution priority

### Wed Feb 28, 2024

[C# Language Design Meeting for Feb 28th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-02-28.md)

- Extensions

### Mon Feb 26, 2024

[C# Language Design Meeting for Feb 26th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-02-26.md)

- `ref struct`s in generics
- Collection expressions

### Wed Feb 21, 2024

[C# Language Design Meeting for Feb 21st, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-02-21.md)

- Declaration of ref/out parameters in lambdas without typename
- `params` collections
    - Metadata format
    - `params` and `scoped` across `override`s
    - `required` members and `params` parameters

### Wed Feb 7, 2024

[C# Language Design Meeting for Feb 7th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-02-07.md)

- Partial type inference
- Breaking change warnings

### Mon Feb 5, 2024

[C# Language Design Meeting for Feb 5th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-02-05.md)

- First-class span types
- Collection expressions: inline collections

### Wed Jan 31, 2024

[C# Language Design Meeting for January 31st, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-31.md)

- Relax "enumerable" requirement for collection expressions
- `params` collections evaluation orders

### Mon Jan 29, 2024

[C# Language Design Meeting for January 29th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-29.md)

- `params` collections
  - Better function member changes
  - `dynamic` support
- `dynamic` and `ref` local function bugfixing

### Mon Jan 22, 2024

[C# Language Design Meeting for January 22nd, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-22.md)

### Wed Jan 10, 2024

[C# Language Design Meeting for January 10th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-10.md)

- Collection expressions: conversion vs construction

### Mon Jan 8, 2024

[C# Language Design Meeting for January 8th, 2024](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-08.md)

- Collection expressions
    - Iteration type of `CollectionBuilderAttribute` collections
    - Iteration type in conversions
