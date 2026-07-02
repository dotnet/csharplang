# `cref` instantiated types

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

<!-- One paragraph explanation of the feature. -->

This feature allows users to reference closed generic types in `cref` attributes of documentation comments.

## Motivation
[motivation]: #motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

Currently documentation comments cannot reference instantiated generic types. This is especially problematic for cases such as asynchronous programming, where documentation for return values cannot easily express both the task-like return type (e.g. `Task<TResult>`) and the type of value held by the task (e.g. `int` for `ValueTask<int>`).

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement,  and include examples of how the feature is used. This section can start out light before the prototyping phase but should get into specifics and corner-cases as the feature is iteratively designed and implemented. -->

`cref` attributes are updated to allow generic type references to provide type arguments:

```xml
<see cref="Dictionary{int, string}"/>
```

The documentation comment ID for instantiated generic types is given the following form:

```
T:System.Collections.Generic.Dictionary`2[[System.Int32, System.String]]
```

Post-processing tools for documentation comments SHOULD use the type instantiation when rendering the documentation.

> [Dictionary](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2)&lt;[int](https://docs.microsoft.com/en-us/dotnet/api/system.int32), [string](https://docs.microsoft.com/en-us/dotnet/api/system.string)&gt;

Post-processing tools MAY ignore the type instantiation provided in double-brackets, and render the reference in a form like the following:

> [Dictionary&lt;TKey, TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2)

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

The current C# compiler ignores the specific identifiers passed in references to generic types. In many cases, the new behavior more closely matches the behavior users expect, but it still produces a change in the output files. In some cases, new warnings would be reported.

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

* How should the open generic type be referenced if a name matching the type parameter appears in the scope of the reference?
    * One option is to allow `<see cref="Dictionary{,}"/>`

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->

