# Final initializers

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Final initializers are a proposed new kind of member declaration that runs at the end of an object's initialization - after constructors, object initializers and collection initializers.

## Motivation
[motivation]: #motivation

With object and collection initializers in the language, there is not a place in a type declaration to write code that runs *after* the object is otherwise fully initialized - e.g. to do final validation, trim or clean up input, compute additional private state, register the object somewhere, etc. Final initializers provide a new kind of member declaration specifically for that purpose.

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement, and include examples of how the feature is used. Please include syntax and desired semantics for the change, including linking to the relevant parts of the existing C# spec to describe the changes necessary to implement this feature. An initial proposal does not need to cover all cases, but it should have enough detail to enable a language team member to bring this proposal to design if they so choose. -->

### Syntax

``` antlr
final_initializer_declaration
    : attributes? `init` method_body
    ;
```

No modifiers can be specified. In some ways the declaration is a counterpart to a finalizer [14.13](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#1413-finalizers), in that it has a very specific format with few points of decision for the programmer.

### Semantics

Unless one is specified, all types are considered to have an implicit, empty final initializer. A final initializer is considered a public virtual instance member. A final initializer declaration is considered an override, and will implicitly perform a call to the final initializer of the base class before executing the specified body, all the way up to the (empty) final initializer of the `object` class.

At the end of the execution of an object creation expression ([11.7.15.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#117152-object-creation-expressions)) or a `with` expression, the resulting object's final initializer is executed as a function member invocation [11.6.6](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1166-function-member-invocation) on the resulting object, which means that it will be a virtual call based on the runtime type of that object.

### Implementation strategies

Final initializers should likely be implemented as a public virtual method with an unspeakable name (same across all final initializers), no parameters and a `void` return type.

One option is for `object` itself to have such a virtual method, and for all final initializer declarations to be turned into overrides of this method with a `base` call prepended to the body. This would lead to every single object having such a method on it, which may or may not be an issue.

Other alternatives include introducing method declarations only when necessitated by final initializer declarations. Under such strategies, it is important that the presence of final initializer methods is dynamically discoverable (either through reflection, interface implementation or otherwise), so that even when the runtime type is not statically known, the final initializer can be found and called. This can be the case for `with` expressions.

The latter set of approaches can be vulnerable to binary breaks - if a base class introduces a final initializer, a derived class that is not recompiled may have the wrong semantics and not call the base.

## Drawbacks
[drawbacks]: #drawbacks

This is yet another mechanism related to object initialization. While it provides a means for behaviors and guarantees around object state that weren't available before, it needs to be weighed against the complexity it adds.

## Alternatives
[alternatives]: #alternatives

- Is the `init` syntax the best choice? Should it rather mirror finalizers in some way, with a glyph and an empty parameter list `()`?
- Is unconditionally calling the base final initializer before the body too inflexible? Should there be an explicit base call syntax instead?

## Unresolved questions
[unresolved]: #unresolved-questions

- Which implementation strategy best balances performance with binary compatibility?
- Should `extern` be supported, as it is for finalizers?

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
