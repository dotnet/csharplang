# Self constraint for generic type parameters

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

A single generic type parameter on a given interface declaration can be specified to be the `self type` and the corresponding type argument must be a type that implements the interface. In addition, a type implementing any constructed form of the interface directly (not through its base class), must use itself as a type argument for the self type type parameter in that constructed form.

## Motivation
[motivation]: #motivation

When dealing with static abstracts in interfaces for operator declarations, the interface must currently declare one of the generic type parameters to have a recursive constraint:
```csharp
interface IAdditionOperators<TSelf, TOther, TResult>
    where TSelf : IAdditionOperators<TSelf, TOther, TResult>
{
    static abstract TResult operator +(TSelf left, TOther right);
}
```

This recursive constraint allows the language to infer that the constrainted type parameter is the implementing type and therefore that the `operator` will meet the C# requirement that at least one of the types participating in the operator signature be the declaring type.

However, this rule breaks down somewhat when going outside operators:
```csharp
interface IAdditiveIdentity<TSelf>
    where TSelf : IAdditiveIdentity<TSelf>
{
    static abstract TSelf AdditiveIdentity { get; }
}
```

In the above example, even though the intent here is the same as for operators, it is perfectly legal for a user to define and implement `struct S : IAdditiveIdentity<int> { ... }` assuming that `int` also implements `IAdditiveIdentity<int>`.

This also hinders various usages of `static abstracts in interfaces` in that there is no way to provide the underlying `constraint` token required for the emitted call and so users in the C# 10 preview must declare their own wrapper methods to handle the transition.

For example, say a user explicitly implements an interface:
```csharp
interface IAdditiveIdentity<TSelf>
    where TSelf : IAdditiveIdentity<TSelf>
{
    static abstract TSelf AdditiveIdentity { get; }
}

public struct Half : IAdditiveIdentity<Half>
{
    public Half IAdditiveIdentity<Half>.AdditiveIdentity { get; }
}
```

For non-static abstract members, this can be invoked simply by utilizing the interface `((ISomeInterface)value).SomeMethod()`. While for `static abstracts` there is no current equivalent and users must instead declare a wrapper method:
```csharp
internal static T GetAdditiveIdentity<T>()
    where T : IAdditiveIdentity<T>
{
    return T.AdditiveIdentity;
}
```

This allows them to then call `GetAdditiveIdentity<Half>()` to achieve the desired result. However, this is verbose and becomes unnecessary with a proper `self constraint` which would allow `IAdditiveIdentity<Half>.AdditiveIdentity` to correctly resolve to the implementation of `Half.AdditiveIdentity`, whether implicitly or explicitly implemented.

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

This would cause the constraint for the `TSelf` generic type parameter to have a `modreq` emitted (similarly to how the `modreq` for the `unmanaged` constraint works) that indicates it is the "self type" as well as the standard recursive constraint that was already being declared beforehand. The language can then depend on this information when interacting with the interface and know, for example, that `IAdditionOperators<int, TOther, TResult>` should resolve to the corresponding APIs exposed on `int`.

Only a single generic-type parameter would be allowed to be annotated as the `self type` as there is no reason for two types representing the same contract.

The self-constrained type argument must still be specified at the use site. That is the user must still fully specify `struct Half : IAdditiveIdentity<Half>` and could not choose to elide or otherwise not specify `<Half>`.

** TODO: Insert list of actual spec changes required here **

## Drawbacks
[drawbacks]: #drawbacks

The new syntax may be confusing to some users as the constraint is now specified in a different location.

Utilizing the new syntax would be a breaking change for existing types and so you could not, for example, modify `System.IEquatable<T>` to be `System.IEquatable<this T>`.

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

Should you be able to place additional constraints on the `self` type? For example, would `this TSelf` and `where TSelf : this, struct` be valid if you only wanted the interface to be implementable by struct types?

Does this need to be a `modopt` (or `modreq`?) or can it be expressed in some way such that existing types can "move forward" if they have existing scenarios where this was the intended contract?

How does this play into variance for classes and possible eventual support for static abstracts in classes? Would variance conversions between interface types lead to a loss of information in some way or another? Etc.

Would a proper self constraint allow a simpler model for encoding the `call` in IL? That is, if the runtime also supports a proper self constraint then rather than requiring ``constrained. !!T call !0 class IAdditiveIdentity`1<!!T>.get_AdditiveIdentity()`` it could simply support ``call !0 class IAdditiveIdentity`1<!!T>.get_AdditiveIdentity()``.

Could the self type be represented as an associated type rather than as a type parameter to an interface?

Obligatory general syntax questions: should we use a keyword other than `this`? Should the keyword go where the proposal puts it, or with the rest of the type constraints?

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
