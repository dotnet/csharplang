# Solution wide define constants

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

Being able to compile an entire solution with specific define constants, instead of "each project has its own independant define constants"

## Motivation
[motivation]: #motivation

This avoid duplicate work that can lead to missings / typo issues etc

## Detailed design
[design]: #detailed-design

MySolution /
     Lib.A/
     Lib.B/
     Program.A
     
     
Being able to add some define constants in MySolution.sln

Each projects will be able to grab define constants like that:

```xml
<DefineConstants>$(SolutionDefineConstants)</DefineConstants>
```


## Drawbacks
[drawbacks]: #drawbacks

I see no reasons to not have this

## Alternatives
[alternatives]: #alternatives

Having each projects have its own define constants, but as i said above it can lead to lot of issues/repetitive/duplicate/error prone work

## Unresolved questions
[unresolved]: #unresolved-questions

- nothing

## Design meetings

- none
