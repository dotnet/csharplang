# Unmanaged constant types

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

Expand the allowable types for compile-time constants (`const` declarations) to any value type that is fully blittable.

The current specification [§12.23](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1223-constant-expressions) limits constant expressions to an enumerated set of types, or default reference expressions (`default`, or `null`).

This proposal expands the types allowed. The allowed types should include any unmanaged type ([§8.8](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/types.md#88-unmanaged-types))

It could be expanded to include any `readonly struct` type.

We should consider if a `ref struct` type, such as `ReadOnlySpan<T>` could be a compile-time constant.

## Motivation
[motivation]: #motivation

Only constant expressions can be used in pattern matching expressions. This expands the types that can be used in patterns. For example, it's common to use GUIDs as identifiers. This feature enables matching on GUID values. Currently, the only way to do that is to convert a GUID to a ReadOnlySpan<byte> and use a list pattern on the list of bytes.

Similarly, any other type that holds multiple values, like record struct types, could be used to match patterns.  For example, a `Point` type could be matched agains a `const` for the origin.

Other uses for constant expressions include:

- default argument values.
- Attribute parameters

## Detailed design
[design]: #detailed-design

This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement,  and include examples of how the feature is used. This section can start out light before the prototyping phase but should get into specifics and corner-cases as the feature is iteratively designed and implemented.

## Drawbacks
[drawbacks]: #drawbacks

Why should we *not* do this?

## Alternatives
[alternatives]: #alternatives

What other designs have been considered? What is the impact of not doing this?

## Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still undecided?

- **What types should be allowed?**:  Preventing mutation for types whose members are all unmanaged types (or `struct` types whose members are all unmanaged types) is a manageable problem: a `const` instance can't be accessed as a writable `ref`. However, if any member is a reference type, that problem is harder to solve.
- **Are there implications for the runtime?** Depending on how constants are stored in memory, is there an impact on the runtime?
- **What are the implications of allowing constant ref structs? Are they even useful?**: Are the rules surrounding ref safety consistent with where constants are used?
- **How will down-level scenarios work?** Can these constants be accessed by code using previous compilers? What about using Reflection to access fields?
- **Does a type need to "opt in" to all constant definitions?** (Otherwise, changes later could introduce a field that disallows all existing `const` declarations):

   ```csharp
    public struct Point
    {
        public int X;
        public int Y;
    }

    // V2:
    
    public struct Point
    {
        public int X;
        public int Y;
        public object SomeReference;
    }
    ```

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.
