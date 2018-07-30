# see href

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

<!-- One paragraph explanation of the feature. -->

This proposal defines the preferred form for a documentation reference to an external resource addressable by a URI.

## Motivation
[motivation]: #motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

Currently, the C# language does not provide clear facilities for referencing external content addressable by a URI. This leads to confusion among developers regarding the best plan to include such content in documentation:

* https://github.com/dotnet/machinelearning/pull/529#discussion_r202773275
* https://stackoverflow.com/questions/6960426/c-sharp-xml-documentation-website-link

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement,  and include examples of how the feature is used. This section can start out light before the prototyping phase but should get into specifics and corner-cases as the feature is iteratively designed and implemented. -->

This proposal involves three components, each of which should be formalized for inclusion in the language specification.

* The `href` attribute of documentation comment elements is defined, with the value as a URI. The compiler MAY report a warning if the value is not a valid URI.

* The `see` and `seealso` elements are updated to allow for the use of `href` attribute. The compiler MAY report a warning if both the `cref` and `href` attributes are used for the same reference. The following shows examples of how this may appear:

    ```xml
    <see href="https://github.com/dotnet/csharplang/"/>
    <see href="https://github.com/dotnet/csharplang/">The official repo for the design of the C# programming language</see>
    ```

* The `see` and `seealso` elements are updated to indicate the content of the element, if provided, should be used as the display text of the reference.

This change is likely to not require any core changes in the behavior of the compiler. However, tools (including the IDE component of dotnet/roslyn) are likely to require updates to ensure correct presentation of these references.

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->

