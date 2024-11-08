# Language Design Process

The language design process is the steps that a proposal takes throughout its life, going from an initial seed of an idea, to a championed proposal that is being considered
for inclusion in the language, all the way to the final specification representing a feature that has been shipped as part of a .NET release. It is very important to the
language design team that we have a clear process and organization for this, for multiple reasons:

* Our community is very active and vocal on this repo, and we want to make sure that feedback can be heard and impact the design and direction of the language, as well as
  ensuring that the community can follow the state of designs.
* We want to make sure that we are using our design energy effectively, and that we can see the status of previous meetings as we drive a feature to completion.
* We want to be able to look back historically to use previous design decisions to inform new language features, as well as to ensure that when a feature is incorporated into
  the ECMA spec, it captures the full nuances of what was designed.

To achieve these goals, this repository covers the actual proposed text for new language features (often called speclets), notes from language design meetings (called LDM),
intermediate documents being worked on as part of the development of proposals, issues tracking features that we want to include in the C# language (champion issues), and
discussion topics for those features. In order to keep things organized, we keep discussion of proposals to actual discussions; issues are for tracking purposes only. This
policy is changed from previous history in the csharplang repo, so many (most) issues will have some historical discussion in them. However, threaded discussion topics are
better for the types of branching conversations that language features have, so all new discussion will happen in the Discussion forum, rather than on issues.

## Steps of the process

There are a few steps along the path from the seed of an idea all the way to an implemented language feature that is in an official ECMA specification. While much of that
process takes place outside of this repository (https://github.com/dotnet/roslyn for the language feature implementation, https://github.com/dotnet/runtime for supporting
BCL APIs and runtime changes, https://github.com/dotnet/csharpstandard/ for the specification changes, just to name a few), we track the overall implementation of the feature
in this repository, and take the following steps to make understanding the current status easier.

### Proposed feature

New ideas are submitted as [discussions](https://github.com/dotnet/csharplang/discussions). These ideas can be very freeform, though we ask that you search for duplicates
before opening a new discussion, as the most common first comment on new discussions is one or more links to existing discussions that cover the idea. While ideas are welcome,
there is no guarantee that an idea will be adopted into the language; even among things that have been triaged for eventual inclusion in the language, there is more work
than can be done in a single lifetime. In order to move forward, a member of the language design team (LDT) has to decide to "champion" the idea. This is effectively the
LDT member deciding to sponsor the idea, and to bring it forward at a future LDM. Most features do not make it out of this stage.

In order to move to the next stage, there needs to be enough detail to fill out the [proposal template](proposals/proposal-template.md) with at least some amount of detail.
While we do not need exact spec language at this point, there should be enough information that other LDT members can get a general idea of the feature, what areas of the
language it will impact, and where the complicated aspects are likely to be. In order to be triaged as part of an LDM, this template will need to be checked into the repo.

#### Lifecycle

* Starts when a new discussion is opened
* Moves to [Championed feature](#championed-feature) when an LDT member decides to champion

### Championed feature

A championed feature is an idea for a C# language feature that an LDT member has decided to sponsor, or "champion", for possible inclusion into C#. You can identify issues
in this category by looking for issues with
[this query](https://github.com/dotnet/csharplang/issues?q=is%3Aissue%20state%3Aopen%20no%3Amilestone%20label%3A%22Proposal%20champion%22), issues with the `Proposal Champion`
label and no milestone. For these issues, one or more LDT members have indicated that they are interested in the idea, but the entire LDM has not met to discuss the idea and
give an official blessing. We try to triage these every few months, though when we start wrapping up a particular release and design time is needed for active questions on
features currently under development, we can lag behind here.

#### Lifecycle

* Starts when an LDT member decides to champion a [proposed feature](#proposed-feature)
* Moves to [rejected feature](#rejected-feature) if rejected at LDM
* Moves to [triaged feature](#implemented-feature) if approved at LDM and assigned to a development milestone

### Triaged feature

A triaged feature is a championed issue that has been approved at LDM for inclusion in a future release of C#. We have quite a few issues in this bucket; they are visible
by looking at any issues labeled `Proposal Champion` that have been assigned to one of the development milestones, `Any Time`, `Backlog`, `Needs More Work`, or `Working Set`.
[This query](https://github.com/dotnet/csharplang/issues?q=is%3Aissue%20state%3Aopen%20label%3A%22Proposal%20champion%22%20(milestone%3ABacklog%20OR%20milestone%3A%22Any%20Time%22%20OR%20milestone%3A%22Needs%20More%20Work%22%20OR%20milestone%3A%22Working%20Set%22%20))
shows these issues. The development milestones mean the following:

* `Working Set` - These are features that are being actively worked on by LDT members in some form; whether that's design work behind the scenes, active LDMs discussing the
  topics, or other actions.
* `Backlog` - These are features that have been approved for inclusion in C# at some LDM in the past, but are not currently being actively worked on. These are not open to
  community implementation; they are usually too large or involved to devote LDM time to unless we're willing to make an active effort to get them into the language.
* `Needs More Work` - These are features that have been approved for inclusion in C# at some LDM in the past, but there are currently design aspects or blocking issues that
  prevent active work from proceeding at this point.
* `Any Time` - These are features that have been approved for inclusion in C# at some LDM in the past that are open for community members to contribute to C#. Please do keep
  in mind that the C# compiler team is constrained by resource limits, and will need to devote significant time to helping get even the simplest of features into the language;
  please ask _before_ starting to work on one of these features to make sure the team is currently able to devote that time. Features in this category can be in one of two states,
  denoted by labels on the issue:
    * `Needs Approved Specification` - LDT has approved this in theory, but has not been presented with a precise specification for how the feature will work. Before implementation
      can proceed, a complete specification needs to be created and approved at an LDM.
    * `Needs Implementation` - A specification for this feature has been approved at a previous LDM, and needs to be implemented in the C# compiler.

This state is the one that will consume most of an approved feature's lifecycle, on average. It is not uncommon for a feature that is approved in theory to spend years in the
backlog and/or working set before being implemented.

#### Lifecycle

* Starts when a [championed feature](#championed-feature) is approved at LDM and assigned to a development milestone
* Ends when the feature is [implemented](#implemented-feature) as part of a C# release
* Ends if the feature is reconsidered at an LDM and then [rejected](#rejected-feature)

### Implemented feature

Once a feature has been implemented in the [Roslyn](https://github.com/dotnet/roslyn) C# compiler and been released as part of an official C# release, it is considered implemented.
At this point, it will have a complete speclet available in the [proposals/csharp-\<release version\>](proposals) folder (note that some older C# features, particularly the C# 7.X
and prior features, did not follow this, and have incomplete or non-existent speclets). At this point, the issue will be labeled `Implemented Needs ECMA Specification`, but it will
not be closed until the ECMA-334 specification is updated with the feature. This can take some time; the ECMA-334 committee is working on catching up as fast as they can, but is
several years behind the language implementation.

#### Lifecycle

* Starts when a [triaged feature](#triaged-feature) is shipped as part of a C# release
* Ends when the feature is fully incorporated into a version of the ECMA-334 specification

### ECMA-specified feature

At this point, the feature has been fully incorporated by ECMA-TC49-TG2, the C# standards committee, into the
[official C# ECMA specification](https://github.com/dotnet/csharpstandard/). When this happens, we close the issue as completed, and all development work on the feature is
complete.

#### Lifecycle

* Starts when an [implemented feature](#implemented-feature) is shipped as part of a C# release
* This is the final state for a feature that is included in C#, no further state changes occur

### Rejected feature

When a feature is explicitly considered during an LDM, and the LDT decides as a group to reject it, it moves to this state. At this point, close the champion issue as not planned
and set the milestone to `Likely Never`. It's not impossible for an issue to be pulled back out of this state and included in the language in the future, but generally, this state
means that the feature will never be part of C#.

#### Lifecycle

* Starts when a [championed feature](#championed-feature) is considered at LDM and rejected
* While it is possible that some rejected features end up getting reconsidered, this is generally the final state for language features that are explicitly considered and
  rejected during LDM

## Language Design Team processes

These are various processes and actions taken by LDT members during the development of a feature. Community members should not perform these actions unless invited to do so
by an LDT member.

### Steps to move a [Discussion](#proposed-feature) to a [Champion feature](#championed-feature)

When an LDT member decides to champion a discussion, they take the following steps:

1. Create a new proposal champion issue.
    * If preferred, the LDT member can ask the original proposer to create this issue.
    * Note: it can be easier to create the PR for step 6 first, get that into a ready-to-merge state, and then create the champion issue at that point, depending on the complexity
      of the feature.
    * The champion issue should have a short summary of the feature, but not the full proposal; there should be enough detail to jog the memory and/or get someone interested in reading the full
      specification, but should not have detail that will end up needing to be edited often as a proposal evolves.
2. Assign themselves to the champion issue.
3. Apply the `Proposal Champion` label to the new issue, as well as to the original discussion.
4. Link to the original discussion from the champion issue.
5. Lock the proposal champion issue for comments to ensure that discussion continues in the discussion area, rather than on the champion issue.
6. Fill out and check in a [proposal template](proposals/proposal-template.md) for the feature. Exact specese is not required, but there should be enough detail to have a
   meaningful triage session.
    * This is also something the LDT member can ask a community member to open a PR for, if they are willing.
    * The filled out proposal should include a link to the champion issue for easy navigation.

### Bringing open questions to LDM

During the course of development of a feature, there are several different types of questions that need to get brought to LDM for answers. The most important overriding factor
for any question is that there is a checked-in commit that contains the question. The document and commit will be linked as part of the notes so that future readers of the notes
can understand the full context in which the question was asked.

#### Alternative proposals, supplemental documentation

As part of the initial design of a feature, a number of different proposals may be brought as part of the design process, either as alternatives to an initial design, or as
supplemental materials to an existing design to help drive conversation in LDM. We want to keep these "supplemental" materials in one place, rather than scattered throughout
the repo as different issues, discussions, and other documents. For such material, they should go in the [working group folder](meetings/working-groups/) for that feature. Not
all features will have such a folder; indeed, most will not. For these documents, please check them in _before_ bringing them to an LDM. The LDM organizer should be able to
link to an exact document, not to a PR.

#### Specific implementation questions

During the implementation process, we will often come up with specific scenarios that need to be brought to an LDM and discussed. These questions should be placed in the proposal
specification, in an `Open Questions` section below the main specification text. Each question should have a _linkable_ header, such that the notes that go over the question can
link to the exact question being asked. For these questions, please check them in _before_ bringing them to an LDM. The LDM organizer should be able to link to a specific heading
in a specific document, not to a PR.

Once a question has been answered, the specification should be updated to include any changes required, and the question should be removed. We link to exact commits in the notes
to ensure that questions can still be found, while keep speclets neat and free of potentially confusing syntax examples that may be rejected at LDM.

#### Proposed specification updates

Sometimes during implementation, a specification needs to be updated. These updates are often best viewed by looking at a PR diff; however, PRs present a problem for historical
recording keeping. While GitHub does keep around commits that were only ever part of a PR (either because the PR was closed, or because it was squashed/rebased), reusing a PR
across multiple LDM sessions can make it difficult to understand the exact state of the PR when it was reviewed by LDM. Whenever possible, do not reuse PRs between multiple LDM
sessions. When a PR is reviewed by LDM, either close or merge it, and make a new PR for the next LDM to pick up where it left off. This is a guideline, not a rule; there will be
times this cannot happen for whatever reason. But the following rules _must_ be followed:

1. Do not force push over commits that have been reviewed by LDM.
2. When scheduling your topic for LDM, please use GitHub commit URL or commit range URL. The PR link can be included as well, but the commit (range) is required for inclusion in
   the notes. The LDM organizer should be able to link to exactly what will be reviewed in the LDM session.

### Steps to move a [triaged feature](#triaged-feature) to an [implemented feature](#implemented-feature)

Once a feature has been implemented and has or soon will be shipped, take the following steps (these are usually done in bulk when a release nears):

1. If a folder for the C# release does not exist yet, create it.
2. Move the specification for the feature into that folder.
3. Update the champion issue as follows:
   1. Update the specification link to point at the new location.
   2. Update the milestone of the issue to be the C# release it has shipped/will ship in, creating it if it doesn't exist.
   3. Add the version of .NET and VS it will/did ship in to the issue title.
        * As an example, `[Proposal]: Params Collections` became `[Proposal]: Params Collections (VS 17.10, .NET 9)`
   4. Add the `Implemented Needs ECMA Spec` label to the issue.
4. Add the feature to the [language version history](Language-Version-History.md) document.

### Publishing notes

When publishing a set of notes, take the following steps:

1. Put the notes in the appropriate `meetings/<year>` folder. Notes should follow the `LDM-<year>-<month>-<date>.md` format.
   1. Any supplemental documents for the meeting are also included here with the same prefix to ensure good sorting.
   2. Include an agenda at the top with document-relative links to each topic discussed during LDM.
2. Update the `meetings/<year>/README.md` to:
   1. Move the date into the `C# Language Design Notes for <year>` section
   2. Update the agenda to be the final agenda from the meeting notes. Remove document-relative links.
   3. Include a link to the notes. This format is usually `[C# Language Design Meeting for <month> <day>, <year>](absolute-note-link)`.
   4. If a topic was not discussed during LDM, or not fully finished, move the topic line back to `Schedule ASAP`.
3. Commit the updates. Prefer using spelled out dates (ie, January 1st, 1970), rather than abbreviations, to avoid confusion.
4. Update the champion issues of discussed topics with a link to the notes. Prefer using an exact link to the heading for that set of notes.
5. Create a discussion for the new notes. The title format is `LDM Notes for <month> <day>, <year>`. Set the category to `LDM Notes`.
   1. The discussion should link to the full notes, and copy the agenda from the README.
6. Post the discussion to various communities to let people know the notes are up; at a minimum, to the C# LDM teams chat. We often post to
   discord as well, but that is dependent on people being who are on discord not being on vacation.
