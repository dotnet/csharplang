# Upcoming meetings for 2026

All schedule items must have a public issue or checked-in proposal that can be linked from the notes.

## Schedule ASAP


## Schedule when convenient

- [Final initializers](https://github.com/dotnet/csharplang/blob/5055b97eee8c10d12f822f6d4db9464329615947/proposals/final-initializers.md)
  - [LDM in 2020](https://github.com/dotnet/csharplang/blob/main/meetings/2020/LDM-2020-04-27.md#primary-constructor-bodies-and-validators) approved the syntax. Next is discussing semantics.
- [Labeled `break` and `continue` Statements](https://github.com/dotnet/csharplang/blob/c4ec6fb60c2e174b1abb6c019f22bb15b9b13f6c/proposals/labeled-break-continue.md) (CyrusNajmabadi)
  - [Last conclusion](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-01-12.md#labeled-break-and-continue-statements): delve deeper in a full session.
- [Anonymous using declarations](https://github.com/dotnet/csharplang/blob/665a9392e172e6f4f16347c502d9f80220a6e7a4/proposals/anonymous-using-declarations.md) (jnm2, 333fred, CyrusNajmabadi)
- Triage (working set)

## Recurring topics

- *Triage championed features and milestones*
- *Design review*

## Schedule

### Wed Apr 29, 2026

### Mon Apr 27, 2026

### Wed Apr 22, 2026

- Unsafe evolution, continued - [Simple core model](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/unsafe-evolution/unsafe-simple-core-model.md) (Andy, Mads)

### Mon Apr 20, 2026

- [Closed class generics question](https://github.com/dotnet/csharplang/blob/03974b0deb3572569684aadcf146fe61a86fc09e/proposals/closed-hierarchies.md) (Rikki)

## C# Language Design Notes for 2026

### Wed Apr 15, 2026

[C# Language Design Meeting for April 15th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-04-15.md)

- Deconstruction in lambda parameters
- Labeled `break` and `continue`

### Mon Apr 13, 2026

[C# Language Design Meeting for April 13th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-04-13.md)

- Unsafe evolution, continued
    - Safe markers for members with internal `unsafe` code
    - Members with pointers in their signatures
    - Choosing a temporary spelling for `safe`

### Wed Apr 8, 2026

[C# Language Design Meeting for April 8th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-04-08.md)

- Target-typed static member access
    - Scope review and motivating examples
    - Dot or no dot
    - Initial rollout scope

### Mon Apr 6, 2026

[C# Language Design Meeting for April 6th, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-04-06.md)

- Unsafe evolution, continued
    - Simple core model
    - `extern` members and explicit safety markers

### Wed Apr 1, 2026

[C# Language Design Meeting for April 1st, 2026](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-04-01.md)

- Unsafe evolution migration and explicit safety markers
    - Marking `extern` members as explicitly safe
    - Avoiding easy but incorrect unsafe evolution migration paths

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
