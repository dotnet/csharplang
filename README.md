# C# Language Design

[![Join the chat at https://gitter.im/dotnet/csharplang](https://badges.gitter.im/dotnet/csharplang.svg)](https://gitter.im/dotnet/csharplang?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Welcome to the official repo for C# language design. This is where new C# language features are developed, adopted and specified.

C# is designed by the C# Language Design Team (LDT) in close coordination with the [Roslyn](https://github.com/dotnet/roslyn) project, which implements the language.

You can find:

- Active C# language feature proposals in the [proposals folder](proposals)
- Notes from C# language design meetings in the [meetings folder](meetings)
- Full C# 6 language specification (draft) in the [spec folder](spec)
- Summary of the [language version history here](Language-Version-History.md).

If you discover bugs or deficiencies in the above, please leave an issue to raise them, or even better: a pull request to fix them.

For *new feature proposals*, however, please raise them for [discussion](https://github.com/dotnet/csharplang/labels/Discussion), and *only* submit a proposal as a pull request if invited to do so by a member of the Language Design Team (a "champion").

## Discussion

Discussion pertaining to language features takes place in the form of issues in this repo, under the [Discussion label](https://github.com/dotnet/csharplang/labels/Discussion).

If you want to suggest a feature, discuss current design notes or proposals, etc., please [open a new issue](https://github.com/dotnet/csharplang/issues/new), and it will be tagged Discussion.

GitHub is not ideal for discussions, but it is beneficial to have language features discussed nearby to where the design artifacts are. Comment threads that are short and stay on topic are much more likely to be read. If you leave comment number fifty, chances are that only a few people will read it. To make discussions easier to navigate and benefit from, please observe a few rules of thumb:

- Discussion should be relevant to C# language design. Issues that are not will be summarily closed.
- Choose a descriptive title for the issue, that clearly communicates the scope of discussion.
- Stick to the topic of the issue title. If a comment is tangential, start a new issue and link back.
- If a comment goes into detail on a subtopic, also consider starting a new issue and linking back.
- Is your comment useful for others to read, or can it be adequately expressed with an emoji reaction to an existing comment?

## Design Process

[Proposals](proposals) are raised by, or on invitation from, "champions" on the LDT. They evolve as a result of decisions in [Language Design Meetings](meetings), which are informed by [discussion](https://github.com/dotnet/csharplang/labels/Discussion), experiments, and offline design work.

In many cases it will be necessary to implement and share a prototype of a feature in order to land on the right design, and ultimately decide whether to adopt the feature. Prototypes help discover both implementation and usability issues of a feature. A prototype should be implemented in a fork of the [Roslyn repo](https://github.com/dotnet/roslyn) and meet the following bar:

- Parsing (if applicable) should be resilient to experimentation: typing should not cause crashes.
- Include minimal tests demonstrating the feature at work end-to-end.
- Include minimal IDE support (keyword coloring, formatting, completion).

Once approved, a feature should be fully implemented in [Roslyn](https://github.com/dotnet/roslyn), and fully specified in the [language specification](spec), whereupon the proposal is moved into the appropriate folder for a completed feature, e.g. [C# 7.1 proposals](proposals/csharp-7.1).

**DISCLAIMER**: An active proposal is under active consideration for inclusion into a future version of the C# programming language but is not in any way guaranteed to ultimately be included in the next or any version of the language. A proposal may be postponed or rejected at any time during any phase of the above process based on feedback from the design team, community, code reviewers, or testing.

## Language Design Meetings

Language Design Meetings (LDMs) are held by the LDT and occasional invited guests, and are documented in Design Meeting Notes in the [meetings](meetings) folder, organized in folders by year. The lifetime of a design meeting note is described in [meetings/README.md](meetings/README.md). LDMs are where decisions about future C# versions are made, including which proposals to work on, how to evolve the proposals, and whether and when to adopt them.

## Language Specification

It is our plan to move the C# Language Specification into Markdown, and draft it in the [spec](spec) folder. The spec drafts will eventually be standardized and published by ECMA. The folder currently contains an unofficial Markdown version of the C# 6.0 specification for convenience.

## Implementation

The reference implementation of the C# language can be found in the [Roslyn repository](https://github.com/dotnet/roslyn). This repository also tracks the [implementation status for language features](https://github.com/dotnet/roslyn/blob/master/docs/Language%20Feature%20Status.md). Until recently, that was also where language design artifacts were tracked. Please allow a little time as we move over active proposals.
