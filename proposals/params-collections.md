# `params Collections`

## Summary

In C# 12 language added support for creating instances of collection types beyond just arrays.
See [collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md).
This proposal extends `params` support to all such collection types.

## Motivation

A `params` array parameter provides a convenient way to call a method that takes an arbitrary length list of arguments.
Today `params` parameter must be an array type. However, it might be beneficial for a developer to be able to have the same
convenience when calling APIs that take other collection types. For example, an `ImmutableArray<T>`, `ReadOnlySpan<T>`, or 
plain `IEnumerable`. Especially in cases when compiler is able to avoid an implicit array allocation for the purpose of
creating the collection (`ImmutableArray<T>`, `ReadOnlySpan<T>`, etc). Today, in situations when an API takes a collection type,
developers add `params` overload that takes an array, construct the target collection and call the original overload with that
collection, thus consumers of the API have to trade an extra array allocation for convenience.

## Detailed design

### Extending `params`

### Overload resolution

## Open questions

### Opting out of implicit allocation on the call stack

Should we allow opt-ing out of _implicit allocation_ on the call stack?
Perhaps an attribute that can be applied to a method, type, or assembly.

## Alternatives 

There is an alternative [proposal](https://github.com/dotnet/csharplang/blob/main/proposals/params-span.md) that extends
`params` only for `ReadOnlySpan<T>`.

Also, one might say, that with [collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md)
now in the language, there is no need to extend `params` support at all. Foe any collection type. To consume an API with collection type, a developer
simply needs to add two characters, `[` before the expanded list of arguments, and `]` after it. Given that, extending `params` support might be an overkill,
especially that other languages are unlikely to support consumption of non-array `params` parameters any time soon.

## Related proposals
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/blob/main/proposals/format.md#extending-params
