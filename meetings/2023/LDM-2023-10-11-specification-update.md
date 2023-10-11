# Status of C# 7 standard

In our last meeting, the ECMA committee voted to submit the C# 7.3 standard to ECMA for final approval. This is the first version we've created in the open, on GitHub.Now that we've completed the work on the standard, the `draft-v7` branch has been renamed `standard-v7`.

> GitHub redirects any links to the `draft-v7` branch to the updated name. There's no need to updated any existing proposals due to that change. The default branch has already changed to the `draft-v8` branch. Merging v7 into the v8 branch is in progress.

## Nomenclature changes, and how to handle them going forward

The committee changed some of the nomenclature used in the C# 7 specs. The original terms were either confusing, or some words in a multi-word term were already used in the standard with a different meaning.

Notably, *safe-to-escape-scope* and *ref-safe-to-escape-scope* became *safe-context* and *ref-safe-context* respectively. The possible values are now *declaration-block*, *function-member*, and *caller-context*. (The speclet didn't use standard terms).

Question: Should we update the speclets from C# 7.2 and later to use the standard terms? Or wait until the standard incorporates all these versions? Or add a note in each relevant speclet of the changed terms? 

The first provides consistency now. The second preserves the historical nature of the speclets. The third is a compromise position.

## A look at C# 8 efforts and beyond

Rex has been creating draft PRs for C# 8 (and later) features. None of these are completed. We will start assigning work to committee members at our next meeting. We're making some [process changes](https://github.com/dotnet/csharpstandard/issues/960) that we believe will help us move faster.

You can see the mapping from any speclet to the corresponding standard text in the [admin folder](https://github.com/dotnet/csharpstandard/blob/draft-v8/admin/v8-feature-tracker.md) on the csharpstandard repo.

## Looking back

C# 7.3 was likely the longest cycle the committee has or will have. There were a number of reasons:

1. *New process*: This was the first version using Markdown and GitHub. We were finding issues with the conversion from Word, in addition to fixing existing spec bugs and adding new features.
1. *No Microsoft standard*: This was the first release where the Microsoft version of the C# spec wasn't updated. That's a positive because we're not trying to reconcile different descriptions, and we don't introduce regressions when fixes didn't make both version of the spec. On the other hand, it was our first experience working with feature specs and writing the standard language from the feature specs.
1. *Long release and point releases*: The committee decided to work up through 7.3 instead of 7.0 for two reasons:
   1. C# 7.3 is the last version compatible with .NET Framework.
   1. Small fixes introduced in point releases were easier.
1. *Spelunking LDM notes and roslyn repo*: The C# 7 timeframe also including creating the `csharplang` repository, and building the culture to update the specifications. Some notes were in `roslyn`, in issues or docs. Some in LDM notes, in either repo, and some in the speclets.
1. *Smaller committee*: The committee has room for more members. (Microsoft is under-represented at this point).
 
The newer feature specs have more language directly tied to the specification language. Other ongoing improvements have been noted above.

## Other questions for LDM

1. *Branch cleanup in csharplang*: Rex and I had used a [branch in csharplang](https://github.com/dotnet/csharplang/tree/standard-proposals) for some initial C# 7 integration with the Microsoft spec. That was abandoned some time ago. Is anyone using it? If not, I'll delete branch.
2. *Do we want to direct customer issues in speclets directly to csharplang?*: I sent mail about this a week ago. We have a new feedback mechanism in docs where I can configure a link to use a YML template for new issues in the C# lang repo. Currently, they go to the docs repo, and I triage them before moving or just fixing them.
 