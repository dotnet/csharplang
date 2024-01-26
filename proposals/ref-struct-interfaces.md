# Ref Struct Interfaces

## Summary

This proposal will expand the capabilities of `ref struct` such that they can implement interfaces and participate as generic type arguments.

## Motivation

The inability for `ref struct` to implement interfaces means they cannot participate in fairly fundamental abstraction techniques of .NET. A `Span<T>`, even though it has all the attributes of a sequential list cannot participate in methods that take `IReadOnlyList<T>`, `IEnumerable<T>`, etc ... Instead specific methods must be coded for `Span<T>` that have virtually the same implementation. Allowing `ref struct` to implement interfaces will allow operations to be abstracted over them as they are for other types.

## Detailed Design

### ref struct interfaces

The language will allow for `ref struct` types to implement interfaces. The syntax and rules are the same as for normal `struct` with a few exceptions to account for the limitations of `ref struct` types.

The ability to implement interfaces does not impact the existing limitations against boxing `ref struct` instances. That means even if a `ref struct` implements a particular interface,  it cannot be directly cast to it as that represents a boxing action.

```csharp
ref struct File : IDisposable
{
    private SafeHandle _handle;
    public void Dispose()
    {
        _handle.Dispose();
    }
}

File f = ...;
// Error: cannot box `ref struct` type `File`
IDisposable d = f;
```

The ability to implement interfaces is only useful when combined with the ability for `ref struct` to participate in generic arguments (as [laid out later][ref-struct-generics]).

To allow for interfaces to cover the full expressiveness of a `ref struct` and the lifetime issues they can present, the language will allow `[UnscopedRef]` to appear on interface methods and properties. This is necessary as it allows for interfaces that abstract over `struct` to have the same flexibility as using a `struct` directly. Consider the following example:

```csharp
interface I1
{
    [UnscopedRef]
    ref int P1 { get; }
    ref int P2 { get; }
}

struct S1
{
    [UnscopedRef]
    ref int P1 { get; }
    ref int P2 { get; }
}

int M<T>(T t, S1 s)
    where T : allows ref struct, I1
{
    // Error: may return ref to t
    return t.P1;

    // Error: may return ref to t
    return s.P1;

    // Okay
    return t.P2;

    // Okay
    return s.p2;
}
```

When a `struct` / `ref struct` member implements an interface member with a `[UnscopedRef]` attribute, the implementing member may also be decorated with `[UnscopedRef]` but it is not required. However a member with `[UnscopedRef]` may not be used to implement a member that lacks the attribute ([details][unscoped-ref-impl]).

```csharp
interface I1
{
    [UnscopedRef]
    ref int P1 { get; }
    ref int P2 { get; }
}

struct S1 : I1
{
    ref int P1 { get; }
    ref int P2 { get; }
}

struct S2 : I1
{
    [UnscopedRef]
    ref int P1 { get; }
    ref int P2 { get; }
}

struct S3 : I1
{
    ref int P1 { get; }
    // Error: P2 is marked with [UnscopedRef] and cannot implement I1.P2 as is not marked 
    // with [UnscopedRef]
    [UnscopedRef]
    ref int P2 { get; }
}

class C1 : I1
{
    ref int P1 { get; }
    ref int P2 { get; }
}
```

Default interface methods pose a problem for `ref struct` as there are no protections against the default implementation boxing the `this` member.

```csharp
interface I1
{
    void M()
    {
        // Error: both of these box if I1 is implemented by a ref struct
        I1 local1 = this;
        object local2 = this;
    }
}

ref struct S = I1 { }
```

To handle this a `ref struct` will be forced to implement all members of an interface, even if they have default implementations. The runtime will also be updated to throw an exception if a default interface member is called on a `ref struct` type.

Detailed Notes:

- A `ref struct` can implement an interface
- A `ref struct` cannot participate in default interface members
- A `ref struct` cannot be cast to interfaces it implements as that is a boxing operation

### ref struct Generic Parameters

The language will allow for generic parameters to opt into supporting `ref struct` as arguments by using the `allows ref struct` syntax inside a `where` clause:

```csharp
T Identity<T>(T p)
    where T : allows ref struct
    => p;

// Okay
Span<int> local = Identity(new Span<int>(new int[10]));
```

This is similar to other items in a `where` clause in that it specifies the capabilities of the generic parameter. The difference is other syntax items limit the set of types that can fulfill a generic parameter while `allows ref struct` expands the set of types. This is effectively an anti-constraint as it removes the implicit constraint that `ref struct` cannot satisfy a generic parameter. As such this is given a new syntax prefix, `allows`, to make that clearer.

A type parameter bound by `allows ref struct` has all of the behaviors of a `ref struct` type:

1. Instances of it cannot be boxed
2. Instances participate in lifetime rules like a normal `ref struct`
3. The type parameter cannot be used in `static` fields, elements of an array, etc ...
4. Instances can be marked with `scoped`

Examples of these rules in action:

```csharp
interface I1 { }
I1 M1<T>(T p)
    where T : allows ref struct, I1
{
    // Error: cannot box potential ref struct
    return p;
}

T M2<T>(T p)
    where T : allows ref struct
{
    Span<int> span = stackalloc int[42];

    // The safe-to-escape of the return is current method because one of the inputs is
    // current method
    T t = M3<T>(span);

    // Error: the safe-to-escape is current method.
    return t;

    // Okay
    return default;
    return p;
}

T M3<T>(Span<T> span)
    where T : allows ref struct
{
    return default;
}
```

These parameters will be encoded in metadata as described in the [byref-like generics doc][byref-like-generics]. Specifically by using the `gpAcceptByRefLike(0x0020)` attribute value.

Detailed notes:

- A `where T : allows ref struct` generic parameter cannot
  - Have `where T : U` where `U` is a known reference type
  - Have `where T : class` constraint
  - Cannot be used as a generic argument unless the corresponding parameter is also `where T: allows ref struct`
- The `allows ref struct` can appear anywhere in the `where` clause
- A type parameter `T` which has `allows ref struct` has all the same limitations as a `ref struct` type.

## Soundness

We would like to verify the soundness of both the `ref struct` anti-constraint in particular and the anti-constraint concept in general. To do so we'd like to take advantage of the existing soundness proofs provided for the C# type system. This task is made easier by defining a new language that is similar to C#, but more regular in construction. We will verify the safety of that model, and then specify a sound translation to this language. Because this new language is centered around constraints, we'll call this language "constraint-C#".

The primary ref struct safety invariant that must be preserved is that variables of ref struct type must not appear on the heap. We can encode this restriction via a constraint. Because constraints permit substitution, not forbid it, we will technically define the inverse constraint: `heap`. The `heap` constraint specifies that a type may appear on the heap. In "constraint-C#" all types satisfy the `heap` constraint except for ref-structs. Moreover, all existing type parameters in C# will be lowered to type parameters with the `heap` constraint in "constraint-C#".

Now, assuming that existing C# is safe, we can transfer the C# ref-struct rules to "constraint-C#".

  1. Fields of classes cannot have a ref-struct type.
  2. Static fields cannot have a ref-struct type.
  3. Variables of ref-struct type cannot be converted to non-ref structs.
  4. Variables of ref-struct type cannot be substituted as type arguments.
  5. Variables of ref-struct type cannot implement interfaces.

The new rules apply to the `heap` constraint:

  1. Fields of classes must have types that satisfy the `heap` constraint.
  2. Static fields must have types that satisfy the `heap` constraint.
  3. Types with the `heap` constraint have only the identity conversion.
  4. Variables of ref-struct type can only be substituted for type parameters without the `heap` constraint.
  5. Ref-struct types may only implement interfaces without default-interface-members.

Rules (4) and (5) are slightly altered. Note that rule (4) does not need to be transferred exactly because we have a notion of type parameters without the `heap` contraint. Rule (5) is complicated. Implementing interfaces is not universally unsound, but default interface methods imply a receiver of interface type, which is a non-value type and violates rule (3). Thus, default-interface-members are disallowed.

With these rules, "constraint-C#" is ref-struct safe, supports type substitution, and supports interface implementation. The next step is to translate the language defined in this proposal, which we may call "allow-C#", into "constraint-C#". Fortunately, this is trivial. The lowering is a straightforward syntactic transformation. The syntax `where T : allows ref struct` in "allow-C#" is equivalent in "constraint-C#" to no constraint and the absence of "allow clauses" is equivalent to the `heap` constraint. Since the abstract semantics and typing are equivalent, "allow-C#" is also sound.

There is one last property which we might consider: whether all typed terms in C# are also typed in "constraint-C#". In other words, we want to know if, for all terms `t` in C#, whether the corresponding term `t'` after lowering to "constraint-C#" is well-typed. This is not a soundness constraint -- making terms ill-typed in our target language would never allow unsafety -- rather, it concerns backwards-compatibility. If we decide to use the typing of "constraint-C#" to validate "allow-C#", we would like to confirm that we are not making any existing C# code illegal.

Since all C# terms start as valid "constraint-C#" terms, we can validate preservation by examining each of our new "constraint-C#" restrictions. First, the addition of the `heap` constraint. Since all type parameters in C# would acquire the `heap` constraint, all existing terms must satisfy said constraint. This is true for all concrete types except ref structs, which is appropriate since ref structs may not appear as type arguments today. It is also true for all type parameters, since they would all themselves acquire the `heap` constraint. Moreover, since the `heap` constraint is a valid combination with all other constraints, this would not present any problems. Rules (1-5) would not present any problems since they directly correspond to existing C# rules, or are relaxations thereof. Therefore, all typeable terms in C# should be typeable in "constraint-C#" and we should not introduce any typing breaking changes.

## Open Issues

### Anti-Constraint syntax

**Decision**: use `where T: allows ref struct`

This proposal chose to expose the `ref struct` anti-constraint by augmenting the existing `where` syntax to include `allows ref struct`. This both succinctly describes the feature and is also expandable to include other anti-constraints in the future like pointers. There are other solutions considered that are worth discussing.

The first is simply picking another syntax to use within the `where` clause. Other proposed options included:

- `~ref struct`: the `~` serves as a marker that the syntax that follows is an anti-constraint.
- `include ref struct`: using `includes` instead of `allows`

```csharp
void M<T>(T p)
    where T : IDisposable, ~ref struct
{
    p.Dispose();
}
```

The second is to use a new clause entirely to make it clear that what follows is expanding the set of allowed types. Proponents of this feel that using syntax within `where` could lead to confusion when reading. The initial proposal used the following syntax: `allow T: ref struct`:

```csharp
void M<T>(T p)
    where T : IDisposable
    allow T : ref struct
{
    p.Dispose();
}
```

The `where T: allows ref struct` syntax had a slightly stronger preference in LDM discussions.

### Co and contra variance

**Decision**: no new issues

To be maximally useful type parameters that are `allows ref struct` must be compatible with generic variance. Specifically it must be legal for a parameter to be both co/contravariant and also `allows ref struct`. Lacking that they would not be usable in many of the most popular `delegate` and `interface` types in .NET like `Func<T>`, `Action<T>`, `IEnumerable<T>`, etc ...

After discussion it was concluded this is a non-issue. The `allows ref struct` constraint is just another way that `struct` can be used as generic arguments. Just as a normal `struct` argument removes the variance of an API so will a `ref struct`. 

### Auto-applying to delegate members

**Decision**: do not auto-apply

For many generic `delegate` members the language could automatically apply `allows ref struct` as it's purely an upside change. Consider that for `Func<> / Action<>` style delegates and most interface definitions there is no downside to expanding to allowing `ref struct`. The language can outline rules where it is safe to automatically apply this anti-constraint. This removes the manual process and would speed up the adoption of this feature.

This auto application of `allows ref struct` poses a few problems though. The first is in multi-targeted scenarios. Code would compile in one target framework but fail in another and there is no syntactic indicator of why the APIs should behave differently.

```csharp
// Works in net9.0 but fails in all other TF
Func<Span<char>> func;
```

This is likely to lead to customer confusion and looking at changes in `Func<T>` in the `net9.0` source wouldn't give customers any clue as to what changed.

The other issue is that very subtle changes in code can cause _spooky action at a distance_ problems. Consider the following code:

```csharp
interface I1<T>
{
}
```

This interface would be eligible for auto-application of `allows ref struct`. If a developer comes around later though and adds a default interface method then suddenly it would not be and it would break any consumers that had already created invocations like `I1<Span<char>>`. This is a very subtle change that would be hard to track down.

### Binary breaking change

Adding `allows ref struct` to an existing API is not a source breaking change. It is purely expanding the set of allowed types for an API. Need to track down if this is a binary breaking change or not. Unclear if updating the attributes of a generic parameter constitute a binary breaking change.

## Considerations

### Runtime support

This feature requires several pieces of support from the runtime / libraries team:

- Preventing default interface methods from applying to `ref struct`
- API in `System.Reflection.Metadata` for encoding the `gpAcceptByRefLike` value
- Support for generic parameters being a `ref struct`

Most of this support is likely already in place. The general `ref struct` as generic parameter support is already implemented as described [here][byref-like-generics]. It's possible the DIM implementation already account for `ref struct`. But each of these items needs to be tracked down.

### API versioning

#### allows ref struct anti-constraint

The `allows ref struct` anti-constraint can be safely applied to a large number of generic definitions that do not have implementations. That means most delegates, interfaces and `abstract` methods can safely apply `allows ref struct` to their parameters. These are just API definitions without implementations and hence expanding the set of allowed types is only going to result in errors if they're used as type arguments where `ref struct` are not allowed.

API owners can rely on a simple rule of "if it compiles, it's safe". The compiler will error on any unsafe uses of `allows ref struct`, just as it does for other `ref struct` uses.

At the same time though there are versioning considerations API authors should consider. Essentially API owners should avoid adding `allows ref struct` to type parameters where the owning type / member may change in the future to be incompatible with `allows ref struct`. For example:

- An `abstract` method which may later change to a `virtual` method
- An `abstract` type which may later add implementations

In such cases an API author should be careful about adding `allows ref struct` unless they are certain the type / member evolution will not using `T` in a way that breaks `ref struct` rules.

Removing the `allows ref struct` anti-constraint is always a breaking change: source and binary.

#### Default Interface Methods

API authors need to be aware that adding DIMS will break `ref struct` implementors until they are recompiled. This is similar to [existing DIM behavior][dim-diamond] where by adding a DIM to an interface will break existing implementations until they are recompiled. That means API authors need to consider the likelihood of `ref struct` implementations when adding DIMs. 

#### UnscopedRef

Adding or removing `[UnscopedRef]` from `interface` members is a source breaking change (and potentially creating runtime issues). The attribute should be applied when defining an interface member and not added or removed later.

### Span&lt;Span&lt;T&gt;&gt;

This combination of features does not allow for constructs such as `Span<Span<T>>`. This is made a bit clearer by looking at the definition of `Span<T>`:

```csharp
readonly ref struct Span<T>
{
    public readonly ref T _data;
    public readonly int _length;

    public Span(T[] array) { ... }

    public static implicit operator Span<T>(T[]? array) { }
 
    public static implicit operator Span<T>(ArraySegment<T> segment) { }
}
```

If this type definition were to include `allows ref struct` then all `T` instances in the definition would need be treated as if they were potentially a `ref struct` type. That presents two classes of problems.

The first is for APIs like `Span(T[] array)` and the implicit operators the `T` cannot be a `ref struct`: it's either used as an array element or as generic parameter which cannot be `allows ref struct`. There are a handful of public APIs on `Span<T>` that have uses of `T` that cannot be compatible with a `ref struct`. These are public API that cannot be deleted and hence must be rationalized by the language. The most likely path forward is the compiler will special case `Span<T>` and issue an error code ever bound to one of these APIs when the argument for `T` is _potentially_ a `ref struct`.

The second is that the language does not support `ref` fields that are `ref struct`. There is a [design proposal][ref-struct-ref-fields] for allowing that feature. It's unclear if that will be accepted into the language or if it's expressive enough to handle the full set of scenarios around `Span<T>`.

Both of these issues are beyond the scope of this proposal.

### UnscopedRef Implementation Logic

The rationale behind the `[UnscopedRef]` rules for interface implementation is easiest to understand when visualizing the `this` parameter as an explicit, rather than implicit, argument to the methods. Consider for example the following `struct` where `this` is visualized as an implicit parameter (similar to how Python handles it):

```csharp
struct S
{
    public void M(scoped ref S this) { }
}
```

The `[UnscopedRef]` on an interface member is specifying that `this` lacks `scoped` for lifetime purposes at the call site. Allowing `[UnscopedRef]` to be ommitted on the implementing member is effectively allowing a parameter that is `ref T` to be implemented by a parameter that is `scoped ref T`. The language already allows this:

```csharp
interface I1
{
    void M(ref Span<char> span);
}

struct S : I1
{
    public void M(scoped ref Span<char> span) { }
}
```

## Related Items

Related Items:

- https://github.com/dotnet/csharplang/issues/7608
- https://github.com/dotnet/csharplang/pull/7555
- https://github.com/dotnet/runtime/blob/main/docs/design/features/byreflike-generics.md
- https://github.com/dotnet/runtime/pull/67783
- https://github.com/dotnet/runtime/issues/27229#issuecomment-1537274804

[ref-struct-ref-fields]: https://github.com/dotnet/csharplang/blob/main/proposals/expand-ref.md
[ref-struct-generics]: #ref-struct-generic-parameters
[byref-like-generics]: https://github.com/dotnet/runtime/blob/main/docs/design/features/byreflike-generics.md
[dim-diamond]: https://github.com/dotnet/csharplang/blob/main/meetings/2018/LDM-2018-10-17.md#diamond-inheritance
[unscoped-ref-impl]: #unscopedref-implementation-logic
