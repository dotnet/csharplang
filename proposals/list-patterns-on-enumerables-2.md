# List patterns on enumerables

Champion issue: <https://github.com/dotnet/csharplang/issues/9005>

## Summary

Extends list patterns to be able to be used with enumerables that are not countable or indexable. `items.Where(...) is [var singleItem]`, or `is []`, or `is [p1, .., p2]`.

The pattern will be evaluated without multiple enumeration. The slice pattern `..` is supported, but only without a subpattern.

## Motivation

[LDM 2023-10-09](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-09.md#list-patterns-on-enumerables) set the following direction:

> This is follow-up work from C# 11 that we did not have time in C# 12 to invest in. We intend to continue the work here now; collection expressions supporting more than just indexable and countable types show where our list pattern support falls short.

One example of where gap is noticed is when using LINQ methods. `.Where(...)` is the type of thing which is common to insert after a collection, but when you insert this in `items is [var item]` or similar, the list pattern is no longer permitted. This puts you in an awkward spot where a lot of rewriting is necessary. There's no built-in helper that recovers the behavior of `is [var item]`. With more complex list patterns, it only gets worse from there.

## Detailed design

Any list pattern will be supported for an enumerable type (a type supported by `foreach`) if the same pattern would be supported by a type that is countable and indexable, but not sliceable (even if the enumerable is sliceable). Thus, for the enumerable types gaining support through this proposal, it will be an error for a slice pattern to contain a subpattern.

The type being matched against for each element pattern inside the list pattern will be determined the same way the iteration variable type is inferred with the `foreach` statement. For the slice pattern `..` without a subpattern, the type that it is matching against is unspecified.

Async enumerables are not supported. So far in the language, consumption of async enumerables requires the `await` keyword which highlights the point where execution may be suspended.

No new syntax is involved in this proposal.

### Design rationale

#### No multiple enumeration

Enumerables cannot be assumed to represent a materialized collection. An enumerable may represent an in-memory collection or a generated sequence, but it may also represent a remote query or an iterator method. As such, an enumerable may return different results on each enumeration, and enumeration may have side effects. Multiple enumeration is considered both a performance smell and a correctness issue in cases where the enumerable is not known to be a materialized collection or a trivially generated, guaranteed-stable sequence. The .NET SDK and other popular tools produce warnings for multiple enumeration of the same enumerable.

#### Slice subpatterns would require buffering

With multiple enumeration off the table, then if slice subpatterns (for example `..var slice`) were permitted, they would have to buffer the sliced items in general. The subpattern would not be able to expose a Skip/Take-style enumerable composed over the original enumerable, because any consumption of the resulting sliced enumerable would execute the original enumerable a second time and thus would be multiple enumeration.

Even in the case where an enumerable type explicitly supports slicing by declaring its own range indexer, this does not free us from the concern about multiple enumeration. The range indexer might provide Skip/Take-style windowing on a remote query or an iterator method. The enumerable returned by such an indexer might be fine to use on its own, but we would not want to enumerate _both_ the sliced enumerable and the original enumerable, because this effectively enumerates the original enumerable twice.

#### No implicit buffering

This proposal does not enable slice subpatterns because of the general need to buffer the sliced elements into memory. Those who do want buffering can request it explicitly in their code by matching against `enumerable.ToList()` or `enumerable.ToArray().AsSpan()` or similar, where slice subpatterns are already available for use. Additionally, when matching against such a materialized collection, the type of the slice will be more specific than `IEnumerable`/`IEnumerable<T>`. This more specific type will permit strongly-typed access to the slice as a materialized collection itself.

This also avoids the need to worry about what the sliced type would be when the list pattern is matched against a more specific type than `IEnumerable`/`IEnumerable<T>` and that type doesn't have a range indexer.

[LDM 2022-10-19](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-10-19.md#allowing-slicing-to-capture) mentions that it would be strange to allow slice subpatterns for non-countable, non-indexable enumerables unless they are also allowed for countable, indexable, _non-sliceable_ types as well. The conclusion at the time was to wait and see about efficiency once the runtime evaluation is designed. In light of the rationale above, this proposal recommends not pursuing them.

### Evaluation

The pattern will be evaluated using the enumerator. The enumerator will be obtained and disposed in the same manner as the `foreach` statement. The order of operations will be the following:

- `GetEnumerator()` is called.
- For each list pattern element if any, up to and not including the `..` if present:
  - `MoveNext()` is called. If it returns false, evaluation ends, and the list pattern is not matched.
  - `Current` is accessed no more than once, and the element pattern is matched against the value it returns. A temporary variable may be introduced to avoid calling `Current` more than once. It is preferable to skip the `Current` call for any patterns which can match without reading an input value, such as discards or redundant patterns. If the element pattern fails to match, evaluation ends and the list pattern is not matched.
- If the end of the pattern has been reached:
  - `MoveNext()` is called. Evaluation ends, and the list pattern is matched if `MoveNext()` returned `false` and is not matched if it returned `true`.
- Otherwise, there is a discarding slice pattern (`..`). If there are no more element patterns following the slice pattern, evaluation ends and the list pattern is matched.
- Otherwise, there are patterns to match at the end of the enumerable:
  - A buffer is obtained, such as an array or inline array at the discretion of the implementation, with a size equal to the number of patterns following the slice pattern.
  - An attempt is made to fill the buffer. For each pattern following the slice pattern:
    - `MoveNext()` is called. If it returns false, evaluation ends, and the list pattern is not matched.
    - `Current` is accessed and its value is stored in the first available unwritten position in the buffer.
  - Once the buffer has been filled, enumeration continues and the buffer is used as a circular buffer:
    - `MoveNext()` is called. If it returns `false`, enumeration is finished and evaluation moves to the final step of evaluating the remaining patterns.
    - If it returns `true`, `Current` is accessed and its value is stored in the buffer, overwriting the oldest entry still in the buffer. Enumeration continues from the previous step.
  - Each pattern following the slice pattern is matched against the buffer entries in order, so that the oldest buffer entry is matched with the first pattern that follows the slice pattern, and the newest buffer entry is matched with the last pattern in the list pattern. Evaluation ends. If any pattern fails to match, the list pattern is not matched. Otherwise, the list pattern is matched.
- The enumerator is disposed, if applicable. This step is not skipped when evaluation ends.

If the patterns following the slice pattern consist only of patterns which can match without reading an input value, such as discards or redundant patterns, then an implementation may omit the buffer and the `Current` calls. Rather than enumerating all remaining items, enumeration is only done once for each pattern following the slice pattern. For each, `MoveNext()` is called. If it returns `false`, evaluation ends and the list pattern is not matched. If it returns `true` once for each remaining pattern, evaluation ends and the list pattern is matched.

## Answered questions

### Allowing patterns after slices

Should we allow patterns following the slice pattern, such as `enumerable is [1, 2, .., 3]`?

#### Answer

We should plan on having them eventually. ([LDM 2022-10-19](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-10-19.md#allowing-patterns-after-slices))

## Open questions

### Optimizing statically countable enumerables

Should the compiler be allowed to optimize patterns such as `[1, _, _]` by enumerating only one item, and assuming that `Length`/`Count` is well-behaved and can be checked rather than enumerating for the rest of the pattern?

### Optimizing runtime-countable enumerables

Similar to the previous question, should the compiler be allowed to use `TryGetNonEnumeratedCount` to avoid full enumeration for patterns such as `[1, _, _]`?
