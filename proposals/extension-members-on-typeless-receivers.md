# Extension members on typeless receivers

## Summary

Allow extension members to be invoked on a receiver expression that has no type:

```cs
// All errors today.
ImmutableArray<int> a = [1, 2, 3].ToImmutableArray();
var memoized = SomeMethod.Memoize();
var x = (cond ? null : GetInt()).SomeNullableExtension();
```

Resolution treats the receiver as the first argument of each candidate extension member, then picks the best applicable candidate by the existing overload-resolution rules. Receivers that already have a type bind exactly as today.

## Motivation

Collection expressions ([proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md)) are the primary motivating scenario. A collection expression has no type, so it cannot be the receiver of any extension member, even where the conversion from the collection expression to the extension's first parameter type is well-defined and the extension member is the canonical way to express the operation:

```cs
// All of these are binding-time errors today.
var a = [1, 2, 3].ToImmutableArray();
var b = [1, 2, 3].ToList();
var c = ["one", "two"].ToHashSet();
```

The workaround today is to spell out the target type with a cast (or with a typed declaration target):

```cs
var a = (ImmutableArray<int>)[1, 2, 3];
```

That works, but it forces the developer to write the full target type at the point of construction even when generic inference on the called method would otherwise determine it. The cost grows with the genericity of the type:

```cs
// Today.
var values = (ImmutableArray<Some<Complex, Type>>)[x, y, z];

// Proposed; the type argument of ToImmutableArray<T> is inferred from x, y, z.
var values = [x, y, z].ToImmutableArray();
```

The same shape arises for other expressions that have no type. These are not the headline driver, but the rule we propose is uniform across all such expressions, so they fall out at no additional spec cost. LDM can dial individual categories back via the [open question](#which-receiver-categories-are-supported) below if any prove unwanted.

```cs
// Lambdas whose parameter types are not specified.
var memoized1 = ((x, y) => Compute(x, y)).Memoize();

// Method groups.
var memoized2 = SomeMethod.Memoize();

// Conditional expressions whose arms do not have a common type.
var x = (cond ? null : GetInt()).SomeNullableExtension();
```

LDM has previously identified this as a separate track to explore, distinct from solving the collection-expression case in isolation ([LDM-2023-07-12](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-12.md)). This proposal is that track.

## Detailed design

## Back-compat analysis

## Drawbacks

## Alternatives

## Design decisions

## Open LDM questions

### Which receiver categories are supported?

## Related discussions

## Design meetings
