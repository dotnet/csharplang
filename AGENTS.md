# Agent instructions

When editing C# language proposals, preserve the repository's existing proposal style and terminology.

## Proposal template

When drafting a new C# language proposal, start from `proposals/proposal-template.md` and retain its required structure and reference anchors. Include the champion issue, Summary, Motivation, Detailed design, Drawbacks, Alternatives, and Open questions sections. If a section is intentionally empty, say so rather than omitting it.

## Spec baseline diffs

When a proposal shows changes against the C# specification baseline in `dotnet/csharpstandard`:

- State which csharpstandard section or file is being updated, preferably with a link to the relevant section.
- For prose changes, quote enough unchanged text for context (use blockquote) and mark changes inline:
  - Use `**bold**` for additions.
  - Use `~~strikethrough~~` for deletions.
- For ANTLR grammar productions, use a fenced markdown `diff` block with `+` and `-` lines. This is the preferred form for grammar changes.
- When adding a new spec-diff section, include a short note such as: "Throughout this section, ~~strikethrough~~ indicates text being removed from the existing specification, and **bold** indicates text being added."