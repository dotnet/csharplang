# C# Language Proposals

Language proposals are living documents describing the current thinking about a given language feature.

Proposals can be either *active*, *inactive*, *rejected* or *done*. *Active* proposals are stored directly in the proposals folder, *inactive* and *rejected* proposals are stored in the [inactive](inactive) and [rejected](rejected) subfolders, and *done* proposals are archived in a folder corresponding to the language version they are part of.

## Lifetime of a proposal

A proposal starts its life when the language design team decides that it might make a good addition to the language some day. Typically it will start out being *active*, but if we want to capture an idea without wanting to work on it right now, a proposal can also start out in the *inactive* subfolder. Proposals may even start out directly in the *rejected* state, if we want to make a record of something we don't intend to do. For instance, if a popular and recurring request is not possible to implement, we can capture that as a rejected proposal.

The proposal may start out as an idea in a [discussion issue](https://github.com/dotnet/csharplang/labels/Discussion), or it may come from discussions in the Language Design Meeting, or arrive from many other fronts. The main thing is that the design team feels that it should be done, and that there's someone who is willing to write it up. Typically a member of the Language Design Team will assign themselves as a champion for the feature, tracked by a [Champion issue](https://github.com/dotnet/csharplang/labels/Proposal%20champion). The champion is responsible for moving the proposal through the design process.

A proposal is *active* if it is moving forward through design and implementation toward an upcoming release. Once it is completely *done*, i.e. an implementation has been merged into a release and the feature has been specified, it is moved into a subdirectory corresponding to its release.

If a feature turns out not to be likely to make it into the language at all, e.g. because it proves unfeasible, does not seem to add enough value or just isn't right for the language, it will be [rejected](rejected). If a feature has reasonable promise but is not currently being prioritized to work on, it may be declared [inactive](inactive) to avoid cluttering the main folder. It is perfectly fine for work to happen on inactive or rejected proposals, and for them to be resurrected later. The categories are there to reflect current design intent.

## Nature of a proposal

A proposal should follow the [proposal template](proposal-template.md). A good proposal should:

- Fit with the general spirit and aesthetic of the language.
- Not introduce subtly alternate syntax for existing features.
- Add a lot of value for a clear set of users.
- Not add significantly to the complexity of the language, especially for new users.  

## Discussion of proposals

Feedback and discussion happens in [discussion issues](https://github.com/dotnet/csharplang/labels/Discussion). When a new proposal is added to the proposals folder, it should be announced in a discussion issue by the champion or proposal author. 

 
