# !?. operator

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: short form of (b==null)?null:b.c would become b!?.c

More or less in analogy with ther double question mark

## Motivation
[motivation]: handy notation for frequently occurring expression



## Detailed design
[design]: #detailed-design

Because of the equivalent expansion of b!?.c  to (b==null)?null:b.c , the design is evident.

## Drawbacks
[drawbacks]: #drawbacks

No real drawbacks

## Alternatives
[alternatives]: An alternative notation could be "???.",  or "!!." but that last one doesn't have a question mark, which is confusing as it doesn't refer to the ternary operator at all.



## Unresolved questions
none



## Design meetings

none yet.


