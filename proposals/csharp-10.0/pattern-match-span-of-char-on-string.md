<!--
New language feature proposals should fully fill out this template. This should include a complete detailed design, which describes the syntax of the feature, what that syntax means, and how it affects current parts of the spec. Please make sure to point out specific spec sections that need to be updated for this feature. If you do not have the knowledge or experience to fill this out completely, that's perfectly alright: please open this proposal as a Discussion instead (https://github.com/dotnet/csharplang/discussions/new) and the community can generally discuss the proposal in less formal terms.
-->
# Pattern match `Span<char>` on a constant string

* [x] Proposed
* [x] Prototype: Completed
* [x] Implementation: In Progress
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Permit pattern matching a `Span<char>` and a `ReadOnlySpan<char>` on a constant string.

## Motivation
[motivation]: #motivation

For perfomance, usage of `Span<char>` and `ReadOnlySpan<char>` is preferred over string in many scenarios. The framework has added many new APIs to allow you to use `ReadOnlySpan<char>` in place of a `string`.

A common operation on strings is to use a switch to test if it is a particular value, and the compiler optimizes such a switch. However there is currently no way to do the same on a `ReadOnlySpan<char>` efficiently, other than implementing the switch and the optimization manually.

In order to encourage adoption of `ReadOnlySpan<char>` we allow pattern matching a `ReadOnlySpan<char>`, on a constant `string`, and optimize such switches on `ReadOnlySpan<char>` in the same way as switches on `string`.

## Detailed design
[design]: #detailed-design

We alter the [spec](../csharp-7.0/pattern-matching.md#constant-pattern) for constant patterns as follows (the proposed addition is shown in bold):

> A constant pattern tests the value of an expression against a constant value. The constant may be any constant expression, such as a literal, the name of a declared `const` variable, or an enumeration constant, or a `typeof` expression etc.
>
> If both *e* and *c* are of integral types, the pattern is considered matched if the result of the expression `e == c` is `true`.
>
> **If *e* is of type `System.Span<char>` or `System.ReadOnlySpan<char>`, and *c* is a constant string, and *c* does not have a constant value of `null`, then the pattern is considered matching if `System.MemoryExtensions.SequenceEqual<char>(e, System.MemoryExtensions.AsSpan(c))` returns `true`.**
> 
> Otherwise the pattern is considered matching if `object.Equals(e, c)` returns `true`. In this case it is a compile-time error if the static type of *e* is not *pattern compatible* with the type of the constant.

`System.Span<T>` and `System.ReadOnlySpan<T>` are matched by name, must be `ref struct`s, and can be defined outside corlib. `System.MemoryExtensions` is matched by name and can be defined outside corlib. The signature of `System.MemoryExtensions.SequenceEqual` must match `public static bool SequenceEqual<T>(System.Span<T>, System.ReadOnlySpan<T>)` and `public static bool SequenceEqual<T>(System.ReadOnlySpan<T>, System.ReadOnlySpan<T>)` respectively, and the signature of `System.MemoryExtensions.AsSpan` must match `public static System.ReadOnlySpan<char> AsSpan(string)`. Methods with optional parameters are excluded from consideration.

## Drawbacks
[drawbacks]: #drawbacks

None

## Alternatives
[alternatives]: #alternatives

None

## Unresolved questions
[unresolved]: #unresolved-questions

None

## Design meetings

https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-10-07.md#readonlyspanchar-patterns
