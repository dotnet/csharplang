# List patterns on enumerables

## Summary

Lets you to match an enumerable with a sequence of patterns e.g. `enumerable is { 1, 2, 3 }` will match a sequence of the length three with 1, 2, 3 as its elements.

## Detailed design

The pattern syntax is unchanged:

```antlr
primary_pattern
  : list_pattern
  | length_pattern
  | slice_pattern
  | // all of the pattern forms previously defined
  ;
```

### Pattern compatibility

A *length_pattern* is now also compatible with any type that is not *countable* but is *enumerable* — it can be used in `foreach`.

A *list_pattern* is now also compatible with any type that is not *indexable* but is *enumerable*.

A *slice_pattern* without a subpattern is compatible with any type that is compatible with a *list_pattern*.

```
enumerable is { 1, 2, .. } // okay
enumerable is { 1, 2, ..var x } // error
```

### Semantics

If the input type is *enumerable* but not *countable*, then the *length_pattern* is checked on the number of elements obtained from enumerating the collection.

If the input type is *enumerable* but not *indexable*, then the *list_pattern* enumerates elements from the collection and checks them against the listed patterns:  
Patterns at the start of the *list_pattern* — that are before the `..` *slice_pattern* if one is present, or all otherwise — are matched against the elements produced at the start of the enumeration.  
If the collection does not produce enough elements to get a value corresponding to a starting pattern, the match fails. So the *constant_pattern* `3` in `{ 1, 2, 3, .. }` doesn't match when the collection has fewer than 3 elements.  
Patterns at the end of the *list_pattern* (that are following the `..` *slice_pattern* if one is present) are matched against the elements produced at the end of the enumeration.  
If the collection does not produce enough elements to get values corresponding to the ending patterns, the *slice_pattern* does not match. So the *slice_pattern* in `{ 1, .., 3 }` doesn't match when the collection has fewer than 2 elements.  
A *list_pattern* without a *slice_pattern* only matches if the number of elements produced by complete enumeration and the number of patterns are equals. So `{ _, _, _ }` only matches when the collection produces exactly 3 elements.

Note that those implicit checks for number of elements in the collection are unaffected by the collection type being *countable*. So `{ _, _, _ }` will not make use of `Length` or `Count` even if one is available.

When multiple *list_patterns* are applied to one input value the collection will be enumerated once at most:  
```
_ = collection switch
{
  { 1 } => ...,
  { 2 } => ...,
  { .., 3 } => ...,
};

_ = collectionContainer switch
{
  { Collection: { 1 } } => ...,
  { Collection: { 2 } } => ...,
  { Collection: { .., 3 } } => ...,
};
```

It is possible that the collection will not be completely enumerated. For example, if one of the patterns in the *list_pattern* doesn't match or when there are no ending patterns in a *list_pattern* (e.g. `collection is { 1, 2, .. }`).

If an enumerator is produced when a *list_pattern* is applied to an enumerable type and that enumerator is disposable it will be disposed when a top-level pattern containing the *list_pattern* successfully matches, or when none of the patterns match (in the case of a `switch` statement or expression). It is possible for an enumerator to be disposed more than once and the enumerator must ignore all calls to `Dispose` after the first one.
```
// any enumerator used to evaluate this switch statement is disposed at the indicated locations
_ = collection switch
{
  { 1 } => /* here */  ...,
  _ => /* here */ ...,
};
/* here too, with a spilled try/finally around the switch expression */
```

### Lowering on enumerable type

> **Open question**: Need to investigate how to reduce allocation for the end circular buffer. `stackalloc` is bad in loops. Maybe we'll just have to fall back to locals and a `switch`.  (see [`params` feature discussion](https://github.com/dotnet/csharplang/blob/main/proposals/format.md#extending-params) also)

Although a helper type is not necessary, it helps simplify and illustrate the logic.

```
class ListPatternHelper
{
  // Notes: 
  // We could inline this logic to avoid creating a new type and to handle the pattern-based enumeration scenarios.
  // We may only need one element in start buffer, or maybe none at all, if we can control the order of checks in the patterns DAG.
  // We could emit a count check for a non-terminal `..` and economize on count checks a bit.
  private EnumeratorType enumerator;
  private int count;
  private ElementType[] startBuffer;
  private ElementType[] endCircularBuffer;

  public ListPatternHelper(EnumerableType enumerable, int startPatternsCount, int endPatternsCount)
  {
    count = 0;
    enumerator = enumerable.GetEnumerator();
    startBuffer = startPatternsCount == 0 ? null : new ElementType[startPatternsCount];
    endCircularBuffer = endPatternsCount == 0 ? null : new ElementType[endPatternsCount];
  }

  // targetIndex = -1 means we want to enumerate completely
  private int MoveNextIfNeeded(int targetIndex)
  {
    int startSize = startBuffer?.Length ?? 0;
    int endSize = endCircularBuffer?.Length ?? 0;
    Debug.Assert(targetIndex == -1 || (targetIndex >= 0 && targetIndex < startSize));

    while ((targetIndex == -1 || count <= targetIndex) && enumerator.MoveNext())
    {
      if (count < startSize)
        startBuffer[count] = enumerator.Current;

      if (endSize > 0)
        endCircularBuffer[count % endSize] = enumerator.Current;

      count++;
    }

    return count;
  }

  public bool Last()
  {
    return !enumerator.MoveNext();
  }

  public int Count()
  {
    return MoveNextIfNeeded(-1);
  }

  // fulfills the role of `[index]` for start elements when enough elements are available
  public bool TryGetStartElement(int index, out ElementType value)
  {
    Debug.Assert(startBuffer is not null && index >= 0 && index < startBuffer.Length);
    MoveNextIfNeeded(index);
    if (count > index)
    {
      value = startBuffer[index];
      return true;
    }
    value = default;
    return false;
  }

  // fulfills the role of `[^hatIndex]` for end elements when enough elements are available
  public ElementType GetEndElement(int hatIndex)
  {
    Debug.Assert(endCircularBuffer is not null && hatIndex > 0 && hatIndex <= endCircularBuffer.Length);
    int endSize = endCircularBuffer.Length;
    Debug.Assert(endSize > 0);
    return endCircularBuffer[(count - hatIndex) % endSize];
  }
}
```

`collection is [3]` is lowered to
```
@{
  var helper = new ListPatternHelper(collection, 0, 0);

  helper.Count() == 3
}
```

`collection is { 0, 1 }` is lowered to
```
@{
  var helper = new ListPatternHelper(collection, 2, 0);

  helper.TryGetStartElement(index: 0, out var element0) && element0 is 0 &&
  helper.TryGetStartElement(1, out var element1) && element1 is 1 &&
  helper.Last()
}
```

`collection is { 0, 1, .. }` is lowered to
```
@{
  var helper = new ListPatternHelper(collection, 2, 0);

  helper.TryGetStartElement(index: 0, out var element0) && element0 is 0 &&
  helper.TryGetStartElement(1, out var element1) && element1 is 1
}
```

`collection is { .., 3, 4 }` is lowered to
```
@{
  var helper = new ListPatternHelper(collection, 0, 2);

  helper.Count() >= 2 && // `..` with 2 ending patterns
  helper.GetEndElement(hatIndex: 2) is 3 && // [^2] is 3
  helper.GetEndElement(1) is 4 // [^1] is 4
}
```

`collection is { 1, 2, .., 3, 4 }` is lowered to
```
@{
  var helper = new ListPatternHelper(collection, 2, 2);

  helper.TryGetStartElement(index: 0, out var element0) && element0 is 1 &&
  helper.TryGetStartElement(1, out var element1) && element1 is 2 &&
  helper.Count() >= 4 && // `..` with 2 starting patterns and 2 ending patterns
  helper.GetEndElement(hatIndex: 2) is 3 &&
  helper.GetEndElement(1) is 4
}
```

The same way that a `Type { name: pattern }` *property_pattern* checks that the input has the expected type and isn't null before using that as receiver for the property checks, so can we have the `{ ..., ... }` *list_pattern* initialize a helper and use that as the pseudo-receiver for element accesses.  
This should allow merging branches of the patterns DAG, thus avoiding creating multiple enumerators.

Note: async enumerables are out-of-scope for C# 10. (Confirmed in LDM 4/12/2021)
Note: sub-patterns are disallowed in slice-patterns on enumerables for now despite some desirable uses: `e is { 1, 2, ..[var count] }` (LDM 4/12/2021)

## Unresolved questions

1. Should we limit the list-pattern to `IEnumerable` types? Then we could allow `{ 1, 2, ..var x }` (`x` would be an `IEnumerable` we would cook up) (answer [LDM 4/12/2021]: no, we'll disallow sub-pattern in slice pattern on enumerable for now)
2. Should we try and optimize list-patterns like `{ 1, _, _ }` on a countable enumerable type? We could just check the first enumerated element then check `Length`/`Count`. Can we assume that `Count` agrees with enumerated count?
3. Should we try to cut the enumeration short for length-patterns on enumerables in some cases? (computing min/max acceptable count and checking partial count against that)
  What if the enumerable type has some sort of `TryGetNonEnumeratedCount` API?  
4. Can we detect at runtime that the input type is sliceable, so as to avoid enumeration? .NET 6 may be adding some LINQ methods/extensions that would help. 
