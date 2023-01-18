# C# Language Design Notes for 2021

Overview of meetings and agendas for 2021

## Dec 15, 2021

[C# Language Design Notes for December 15th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-12-15.md)

1. Required parsing
2. Warnings for parameterless struct constructor

## Dec 1, 2021

[C# Language Design Notes for December 1st, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-12-01.md)

1. Roles and extensions

## Nov 10, 2021

[C# Language Design Notes for November 10th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-11-10.md)

1. Self types

## Nov 3, 2021

[C# Language Design Notes for November 3rd, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-11-03.md)

1. Name shadowing in local functions
2. `params Span<T>`

## Nov 1, 2021

[C# Language Design Notes for November 1st, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-11-01.md)

1. Order of evaluation for Index and Range
2. Collection literals

## Oct 27, 2021

[C# Language Design Notes for October 27th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-10-27.md)

1. UTF-8 String Literals
2. Readonly modifiers for primary constructors

## Oct 25, 2021

[C# Language Design Notes for October 25th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-10-25.md)

1. Required members
2. Delegate type argument improvements

## Oct 20, 2021

[C# Language Design Notes for October 20th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-10-20.md)

1. Open questions in list patterns
    1. Types that define both Length and Count
    2. Slices that return null
2. Primary constructors

## Oct 13, 2021

[C# Language Design Notes for October 13th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-10-13.md)

1. Revisiting DoesNotReturn
2. Warning on lowercase type names
3. Length pattern backcompat

## Sep 22, 2021

[C# Language Design Notes for September 22nd, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-09-22.md)

1. Open questions in list patterns
    1. Breaking change confirmation
    2. Positional patterns on ITuple
    3. Slicing rules
    4. Slice syntax recommendations
    5. Other list pattern features
2. Nested members in `with` and object creation
3. CallerIdentityAttribute
4. Attributes on `Main` for top level programs

## Sep 20, 2021

[C# Language Design Notes for September 20th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-09-20.md)

1. Lambda breaking changes
2. Newlines in non-verbatim interpolated strings
3. Object initializer event hookup
4. Type alias improvements

## Sep 15, 2021

[C# Language Design Notes for September 15th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-09-15.md)

* Feedback from the C# standardization committee
* Permit pattern variables under disjunctive patterns

## Sep 13, 2021

[C# Language Design Notes for September 13th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-09-13.md)

1. Feedback on static abstracts in interfaces

## Sep 1, 2021

[C# Language Design Notes for September 1st, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-09-01.md)

1. Lambda expression conversions to `Delegate`
2. C# 11 Initialization Triage
    1. Required properties
    2. Primary constructors
    3. Immutable collection initializers

## Aug 30, 2021

[C# Language Design Notes for August 30th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-08-30.md)

1. C# 11 Initial Triage
    1. Generic attributes
    2. List patterns
    3. Static abstracts in interfaces
    4. Declarations under `or` patterns
    5. Records and initialization
    6. Discriminated unions
    7. Params `Span<T>`
    8. Statements as expressions
    9. Expression trees
    10. Type system extensions

## Aug 25, 2021

[C# Language Design Notes for August 25th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-08-25.md)

1. Interpolated string handler user-defined conversion recommendations
2. Interpolated string handler additive expressions

## Aug 23, 2021

[C# Language Design Notes for August 23rd, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-08-23.md)

1. Nullability differences in partial type base clauses
2. Top-level statements default type accessibility
3. Lambda expression and method group type inference issues
    1. Better function member now ambiguous in some cases
    2. Conversions from method group to `object`
4. Interpolated string betterness in older language versions

## Jul 26, 2021

[C# Language Design Notes for July 26th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-07-26.md)

1. Lambda conversion to System.Delegate
2. Direct invocation of lambdas
3. Speakable names for top-level statements

## Jul 19, 2021

[C# Language Design Notes for July 19th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-07-19.md)

1. Global using scoping revisited

## Jul 12, 2021

[C# Language Design Notes for July 12th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-07-12.md)

1. C# 10 Feature Status
2. Speakable names for top-level statements

## Jun 21, 2021

[C# Language Design Notes for June 21st, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-06-21.md)

1. Open questions for lambda return types
2. List patterns in recursive patterns
3. Open questions in async method builder
4. Email Decision: Duplicate global using warnings

## Jun 14, 2021

[C# Language Design Notes for June 14th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-06-14.md)

1. Open questions in CallerArgumentExpressionAttribute
2. List pattern syntax

## Jun 7, 2021

[C# Language Design Notes for June 7th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-06-07.md)

1. Runtime checks for parameterless struct constructors
2. List patterns
    a. Exhaustiveness
    b. Length pattern feedback

## Jun 2, 2021

[C# Language Design Notes for June 2nd, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-06-02.md)

1. Enhanced #line directives
2. Lambda return type parsing
3. Records with circular references

## May 26, 2021

[C# Language Design Notes for May 26th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-05-26.md)

1. Open questions in list patterns

## May 19, 2021

[C# Language Design Notes for May 19th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-05-19.md)

1. Triage
    1. Checked operators
    2. Relaxing shift operator requirements
    3. Unsigned right shift operator
    4. Opaque parameters
    5. Column mapping directive
    6. Only allow lexical keywords
    7. Allow nullable types in declaration patterns
2. Protected interface methods

## May 17, 2021

[C# Language Design Notes for May 17th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-05-17.md)

1. Raw string literals

## May 12, 2021

[C# Language Design Notes for May 12th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-05-12.md)

1. Experimental attribute
2. Simple C# programs

## May 10, 2021

[C# Language Design Notes for May 10th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-05-10.md)

- Lambda improvements

## May 3, 2021

[C# Language Design Notes for May 3rd, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-05-03.md)

1. Improved interpolated strings
2. Open questions in record structs

## Apr 28, 2021

[C# Language Design Notes for April 28th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-28.md)

1. Open questions in record and parameterless structs
2. Improved interpolated strings

## Apr 21, 2021

[C# Language Design Notes for April 21st, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-21.md)

1. Inferred types for lambdas and method groups
2. Improved interpolated strings

## Apr 19, 2021

[C# Language Design Notes for April 19th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-19.md)

1. Improved interpolated strings

## Apr 14, 2021

[C# Language Design Notes for April 14th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-14.md)

1. Shadowing in record types
2. `field` keyword
3. Improved interpolated strings

## Apr 12, 2021

[C# Language Design Notes for April 12th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-12.md)

1. List patterns
2. Lambda improvements

## Apr 7, 2021

[C# Language Design Notes for April 7th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-07.md)

- MVP session

## Apr 5, 2021

[C# Language Design Notes for April 5th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-05.md)

1. Interpolated string improvements
2. Abstract statics in interfaces

## Mar 29, 2021

[C# Language Design Notes for March 29th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-29.md)

1. Parameterless struct constructors
2. AsyncMethodBuilder

## Mar 24, 2021

[C# Language Design Notes for March 24th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-24.md)

1. Improved interpolated strings
2. `field` keyword

## Mar 22, 2021

- *Design review* (No notes published)

## Mar 15, 2021

[C# Language Design Notes for March 15th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-15.md)

1. Interpolated string improvements
2. Global usings

## Mar 10, 2021

[C# Language Design Notes for March 10th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-10.md)

1. Property improvements
    1. `field` keyword
    2. Property scoped fields
2. Parameterless struct constructors

## Mar 3, 2021

[C# Language Design Notes for March 3rd, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-03.md)

1. Natural type for lambdas
    1. Attributes
    2. Return types
    3. Natural delegate types
2. Required members

## Mar 1, 2021

[C# Language Design Notes for March 1st, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-01.md)

1. Async method builder override
2. Async exception filters
3. Interpolated string improvements

## Feb 24, 2021

[C# Language Design Notes for February 24th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-02-24.md)

1. Static abstract members in interfaces

## Feb 22, 2021

[C# Language Design Notes for February 22nd, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-02-22.md)

1. Global `using`s
2. `using` alias improvements

## Feb 10, 2021

[C# Language Design Notes for February 10th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-02-10.md)

1. Follow up on record equality
2. Namespace directives in top-level programs
3. Global usings
4. Triage
    1. Nominal And Collection Deconstruction
    2. Sealed record ToString
    3. `using` aliases for tuple syntax
    4. Raw string literals
    5. Allow `var` variables to be used in a `nameof` in their initializers
    6. First-class native integer support
    7. Extended property patterns

## Feb 8, 2021

[C# Language Design Notes for February 8th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-02-08.md)

1. Virtual statics in interfaces
    1. Syntax Clashes
    2. Self-applicability as a constraint
    3. Relaxed operator operand types
    4. Constructors

## Feb 3, 2021

[C# Language Design Notes for February 3rd, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-02-03.md)

1. List patterns on `IEnumerable`
2. Global usings

## Jan 27, 2021

[C# Language Design Notes for January 27th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-01-27.md)

1. Init-only access on conversion on `this`
2. Record structs
    1. Copy constructors and Clone methods
    2. `PrintMembers`
    3. Implemented equality algorithms
    4. Field initializers
    5. GetHashcode determinism

## Jan 13, 2021

[C# Language Design Notes for January 13th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-01-13.md)

- Global usings
- File-scoped namespaces

## Jan 11, 2021

[C# Language Design Notes for January 11th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-01-11.md)

- Required properties simple form

## Jan 6, 2021

[C# Language Design Notes for January 5th, 2021](https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-01-05.md)

- File scoped namespaces
