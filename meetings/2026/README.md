# Upcoming meetings for 2026

All schedule items must have a public issue or checked-in proposal that can be linked from the notes.

## Schedule ASAP

## Schedule when convenient

- [Anonymous using declarations](https://github.com/dotnet/csharplang/blob/665a9392e172e6f4f16347c502d9f80220a6e7a4/proposals/anonymous-using-declarations.md) (jnm2, 333fred, CyrusNajmabadi)
- Triage (working set)

## Recurring topics

- *Triage championed features and milestones*
- *Design review*

## Schedule

### Wed Apr 29, 2026

### Mon Apr 27, 2026

### Wed Apr 22, 2026

- [Final initializers](https://github.com/dotnet/csharplang/blob/5055b97eee8c10d12f822f6d4db9464329615947/proposals/final-initializers.md)
  - [LDM in 2020](https://github.com/dotnet/csharplang/blob/main/meetings/2020/LDM-2020-04-27.md#primary-constructor-bodies-and-validators) approved the syntax. Next is discussing semantics.

### Mon Apr 20, 2026

### Wed Apr 15, 2026

- [Deconstruction in lambda parameters](https://github.com/dotnet/csharplang/blob/c4ec6fb60c2e174b1abb6c019f22bb15b9b13f6c/proposals/deconstruction-in-lambda-parameters.md) (CyrusNajmabadi, jnm2)
  - [Last conclusion](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-01-12.md#deconstruction-in-lambda-parameters): take a closer look in LDM.

### Mon Apr 13, 2026

### Wed Apr 8, 2026

- [Target-typed static member access](https://github.com/dotnet/csharplang/blob/c2465a0605180e9624ee5ea9d6e0eab7e93a7c5b/proposals/target-typed-static-member-access.md) (jnm2, CyrusNajmabadi)
  - Continue discussing scope and open questions
- [Labeled `break` and `continue` Statements](https://github.com/dotnet/csharplang/blob/c4ec6fb60c2e174b1abb6c019f22bb15b9b13f6c/proposals/labeled-break-continue.md) (CyrusNajmabadi)
  - [Last conclusion](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-01-12.md#labeled-break-and-continue-statements): delve deeper in a full session.

### Mon Apr 6, 2026

- Unsafe evolution, continued (Andy)

## C# Language Design Notes for 2026

### Wed Apr 1, 2026

[C# Language Design Meeting for April 1st, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-04-01.md)

- Unsafe evolution migration and explicit safety markers
    - Marking `extern` members as explicitly safe
    - Avoiding easy but incorrect unsafe evolution migration paths
    - Scheduled topic: [`Unsafe evolution extern`](https://github.com/dotnet/csharplang/pull/10051) and [`RequiresUnsafe(bool)`](https://github.com/dotnet/runtime/issues/125904) (jjonescz, runtime team)

### Wed Mar 11, 2026

[C# Language Design Meeting for March 11th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-03-11.md)

- Target-typed static member access
    - Overview and smorgasbord of parts
    - Factory containers
    - Ambiguity with parenthesized expression
    - Ambiguity with conditional expression

### Mon Mar 9, 2026

[C# Language Design Meeting for March 9th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-03-09.md)

- Extension indexers
    - Ordering for implicit indexers and list patterns
    - Slice extensions for range access
    - Spread optimization in collection expressions

### Wed Feb 11, 2026

[C# Language Design Meeting for February 11th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-02-11.md)

- Union patterns update

### Mon Feb 9, 2026

[C# Language Design Meeting for February 9th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-02-09.md)

- Closed hierarchies open questions
    - Confirming API shape
    - Blocking subtyping from other languages
    - Multiple `CompilerFeatureRequired` attributes
    - Same module restriction
    - Permit explicit use of `abstract` modifier
    - Subtype metadata

### Wed Feb 4, 2026

[C# Language Design Meeting for February 4th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-02-04.md)

- Discriminated unions patterns
    - Null ambiguity in constructor selection
    - Marking unions with an attribute instead of IUnion interface
    - Factory method support
    - Union member providers

### Mon Feb 2, 2026

[C# Language Design Meeting for February 2nd, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-02-02.md)

- Extension indexers

### Mon Jan 26, 2026

[C# Language Design Meeting for January 26th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-01-26.md)

- Alternative syntax for caller-unsafe

### Wed Jan 21, 2026

[C# Language Design Meeting for January 21st, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-01-21.md)

- Unsafe evolution

### Mon Jan 12, 2026

[C# Language Design Meeting for January 12, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-01-12.md)

- Triage
    - Relaxed ordering for `partial` and `ref` modifiers
    - Deconstruction in lambda parameters
    - Unsigned sizeof
    - Labeled `break` and `continue` Statements
    - Extra accessor in property override
    - Immediately Enumerated Collection Expressions
    - Allow arrays as CollectionBuilder Create parameter type
