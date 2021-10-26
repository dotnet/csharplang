# Self constraint for generic type parameters

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

A single generic type parameter on a given interface declaration can be specified to be the `self type` and must be the same as the deriving type that implements the interface.

## Motivation
[motivation]: #motivation

When dealing with static abstracts in interfaces for operator declarations, the interface must currently declare one of the generic type parameters to have a recursive constraint:
```csharp
interface IAdditionOperators<TSelf, TOther, TResult>
    where TSelf : INumber<TSelf>
{
    static abstract TResult operator +(TSelf left, TOther right);
}
```

This recursive constraint allows the language to infer that the constrainted type parameter is the implementing type and therefore that the `operator` will meet the C# requirement that at least one of the types participating in the operator signature be the declaring type.

However, this rule breaks down somewhat when going outside operators:
```csharp
interface IAdditiveIdentity<TSelf>
    where TSelf : INumber<TSelf>
{
    static abstract TSelf AdditiveIdentity { get; }
}
```

In the above example, even though the intent here is the same as for operators, it is perfectly legal for a user to define and implement `struct S : IAdditiveIdentity<int> { ... }` assuming that `int` also implements `IAdditiveIdentity<int>`.

This also hinders various usages of `static abstracts in interfaces` in that there is no way to provide the underlying `constraint` token required for the emitted call and so users in the C# 10 preview must declare their own wrapper methods to handle the transition.

## Detailed design
[design]: #detailed-design

The following syntax would become valid:
```csharp
interface IAdditionOperators<this TSelf, TOther, TResult>
{
    static abstract TResult operator +(TSelf left, TOther right);
}

interface IAdditiveIdentity<this TSelf>
{
    static abstract TSelf AdditiveIdentity { get; }
}
```

This would cause the `TSelf` generic type parameter to have a `modopt` emitted that indicates it is the "self type" as well as the standard recursive constraint that was already being declared beforehand. The language can then depend on this information when interacting with the interface and know, for example, that `IAdditionOperators<int, TOther, TResult>` should resolve to the corresponding APIs exposed on `int`.

Only a single generic-type parameter would be allowed to be annotated as the `self type` as there is no reason for two types representing the same contract.

** TODO: Insert list of actual spec changes required here **

## Drawbacks
[drawbacks]: #drawbacks

The new syntax may be confusing to some users as the constraint is now specified in a different location.

## Alternatives
[alternatives]: #alternatives

Consider exposing the constraint in a different manner such as:
```csharp
interface IAdditiveIdentity<TSelf>
    where TSelf : this
{
    static abstract TSelf AdditiveIdentity { get; }
}
```

## Unresolved questions
[unresolved]: #unresolved-questions

Should you be able to place additional constraints on the `self` type? For example, would `this TSelf` and `where TSelf : ISomeInterface` be valid?

Does this need to be a `modopt` (or `modreq`?) or can it be expressed in some way such that existing types can "move forward" if they have existing scenarios where this was the intended contract?

How does this play into variance for classes and possible eventual support for static abstracts in classes?

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
