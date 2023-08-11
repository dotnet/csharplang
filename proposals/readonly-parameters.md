# Readonly Parameters

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

We allow parameters to be marked with `readonly`. This disallows them from being assigned to or being passed by `ref` or `out`.

## Motivation
[motivation]: #motivation

C# users have [long requested](https://github.com/dotnet/csharplang/issues/188) the ability to mark both locals and parameters as `readonly`. The design team has somewhat resisted this for two reasons:

* The view that `readonly` on parameters and locals would be an attractive nuisance more than a helpful addition.
* Indecision on what a succinct syntax for locals would be to minimize the nuisance part of the first objection.

However, the addition of primary constructor parameters changes that calculus, at least for parameters. A significant piece of feedback from the initial preview is that users would like to be able to
ensure that primary constructor parameters are not modified. The scope of such parameters is much larger, so the danger of accidental modification is much higher. We therefore propose allowing `readonly`
as a parameter modifier.

This proposal makes a few assumptions about `readonly` locals as part of it:

* A future design would allow `readonly` as a local modifier.
* That future design might allow a shorthand for `readonly var`, or may say that `readonly var` is not allowed, and the separate shorthand is required for a `readonly` type-inferred local. What that shorthand
  is (`val`, `let`, `const`, or some other keyword) is beyond the scope of this proposal.

These assumptions allow us to presume that to fully spell out readonlyness for a parameter or local requires a modifier, and that modifier is `readonly`. In places where types can be inferred, we offer a
shorthand that combines the meanings, but otherwise `readonly` is required.

## Detailed design
[design]: #detailed-design

### Syntax

We modify [section 15.6.2.1](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#15621-general) of the C# specification with the following new definition for `parameter_modifier`:

```antlr
parameter_modifier
    : parameter_mode_modifier
    | 'this'
    | 'readonly'
    ;
```

The spec is not yet updated to include the draft C# 12 specification for [`ref readonly` parameters](https://github.com/dotnet/csharplang/blob/main/proposals/ref-readonly-parameters.md), but we will restrict
`readonly` such that if a parameter is both `readonly` and `ref readonly`, the `readonly` must appear on the left side of the `ref readonly`, so that `readonly ref readonly` is permitted, but `ref readonly readonly`
is not.

### Semantics

For a `readonly` parameter, the compiler will issue an error when it is assigned to or taken as a mutable `lvalue`. This means that a `readonly` parameter cannot be passed by `out` or `ref`, but can be passed
by value, `ref readonly`, or `in`.

#### `partial` methods

For `partial` methods, we allow the implementing `partial` method declaration to add the `readonly` modifier to a parameter if the defining `partial` method did not. If the defining partial `method` included the
`readonly` modifier, the implementing `partial` method must also include it.

#### Signature-only locations

`abstract` members, `interface` members, delegate types, and function pointer types are not permitted to specify `readonly` on their parameters

#### Overriding

Overriding members are not required to match the `readonly`ness of overridden member's parameters. `readonly` may be added or removed with no effect on the program.

#### Lambda parameters

Unresolved question

### Emit

The presence of `readonly` on a parameter has no impact to the generated code. It is not possible to determine from metadata whether a parameter is `readonly` or not.

## Drawbacks
[drawbacks]: #drawbacks

Earlier the proposal alluded to this feature being an attractive nuisance. To spell it out more clearly, we are worried that by introducing a verbose modifier that many people would like to be the default (including ourselves!),
it will become the new "thing to do" on every method definition, even in cases when it provides no real safety benefits.

## Alternatives
[alternatives]: #alternatives

The [championed issue](https://github.com/dotnet/csharplang/blob/main/proposals/ref-readonly-parameters.md) has a number of alternative designs, but most center around the axis of: should we introduce a new, shorter modifier, or
a shorthand that can apply to both locals and parameters? For example, `val int i` as a parameter definition would be what this proposal calls `readonly int i`. This shorthand is very inconsistent with standard C# behavior, so
this proposal takes the position that we would only want to introduce a shorthand for the `readonly` + type inference case.

## Unresolved questions
[unresolved]: #unresolved-questions

### Restrict to just primary ctor parameters

This general feature has historically been pushed back on due to the attractive nuisanceness of the feature. We could artificially restrict this to just primary constructor parameters to avoid introducing that in general.

### Lambda parameters

Today, when applying a modifier to a lambda parameter, the type must also be spelled out. For example, this is not permitted:

```cs
delegate void D(ref int i);
D d = (ref i) => {};
```

The LDM has long thought about allowing the type here to be omitted, but has not yet done so. Should we make that change as part of this proposal? Or should we say that `readonly`, like `ref`, means that the type of the lambda
parameter must be spelled out?

### Emit consequences

This proposal states that `readonly` on parameters has no effect on the emitted code, and that it will not be possible to tell from metadata (including things that read metadata, such as reflection) that a parameter was declared as
`readonly`. Are there use cases for reflecting this information in metadata, and if so, what should the emit strategy we use to convey this information be? And, if we do emit to metadata, should that change how overriding
carries through that information?
