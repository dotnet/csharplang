# C# Language Design

Welcome to the official repo for C# language design.

* Full Language Specification: [Markdown](spec)
* List of [Active](proposals), [Adopted](proposals/adopted), and [Rejected](proposals/rejected) proposals can be found in the [proposals folder](proposals).
* Archives of mailing lists discussions can be found [here](http://lists.dot.net/pipermail/vblang/).
* Archives of notes from design meetings, etc., can be found in the [notes folder](notes).

## Design Process

C# is designed by the C# Language Design Team (LDT).

1. To submit, support, and discuss ideas please subscribe to the [language design mailing list](https://lists.dot.net/mailman/listinfo/csharplang).

2. Ideas that the LDT feel could potentially make it into the language should be turned into [proposals](proposals), based on this [template](proposals/proposal-template.md), either by members of the LDT or by community members by invitation from the LDT. The lifetime of a proposal is described in [proposals/README.md](proposals/README.md). A good proposal should:
    * Fit with the general theme and aesthetic of the language.
    * Not introduce subtly alternate syntax for existing features.
    * Add a lot of value for a clear set of users.
    * Not add significantly to the complexity of the language, especially for new users.  

3. A prototype owner (who may or may not be proposal owner) should implement a prototype in their own fork of the [Roslyn repo](https://github.com/dotnet/roslyn) and share it with the design team and community for feedback. A prototype must meet the following bar:
	* Parsing (if applicable) should be resilient to experimentation--typing should not cause crashes.
	* Include minimal tests demonstrating the feature at work end-to-end.
	* Include minimal IDE support (keyword coloring, formatting, completion).

4. Once a prototype has proven out the proposal and the proposal has been _approved-in-principle_ by the design team, a feature owner (who may or may not be proposal or prototype owner(s)) implemented in a feature branch of the [Roslyn repo](https://github.com/dotnet/roslyn). The bar for implementation quality can be found [here](https://github.com/dotnet/roslyn).

5. Design changes during the proposal or feature implementation phase should be fed back into the original proposal as a PR describing the nature of the change and the rationale.

6. A PR should be submitted amending the formal language specification with the new feature or behavior.

7. Once a feature is implemented and merged into shipping branch of Roslyn and the appropriate changes merged into the language specification, the proposal should be archived under a folder corresponding to the version of the language in which it was included, e.g. [C# 7.1 proposals](proposals/csharp-7.1)). Rejected proposals are archived under the [rejected folder](proposals/rejected).

## Language Design Meetings

Language Design Meetings (LDMs) are held by the LDT and occasional invited guests, and are documented in Design Meeting Notes in the [meetings](meetings) folder, organized in folders by year. The lifetime of a design meeting note is described in [meetings/README.md](meetings/README.md). LDMs are where decisions about future C# versions are made, including which proposals do work on, how to evolve the proposals, and whether and when to adopt them.

## Language Specification

It is our plan to move the C# Language Specification into Markdown, and draft it in the [spec](spec) folder. The spec drafts will eventually be standardized and published by ECMA. The folder currently contains an unofficial Markdown version of the C# 6.0 specification for convenience.

## Implementation

The reference implementation of the C# language can be found in the [Roslyn repository](https://github.com/dotnet/roslyn). Until recently, that was also where language design artifacts were tracked. Please allow a little time as we move over active proposals.

**DISCLAIMER**: An active proposal is under active consideration for inclusion into a future version of the C# programming language but is not in any way guaranteed to ultimately be included in the next or any version of the language. A proposal may be postponed or rejected at any time during any phase of the above process based on feedback from the design team, community, code reviewers, or testing.
