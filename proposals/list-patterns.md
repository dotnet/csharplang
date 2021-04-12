# List patterns

## Summary

Lets you to match an array or a list with a sequence of patterns e.g. `array is {1, 2, 3}` will match an integer array of the length three with 1, 2, 3 as its elements, respectively.

## Detailed design

The pattern syntax is modified as follow:

```antlr
positional_pattern
  : type? positional_pattern_clause length_pattern_clause? property_or_list_pattern_clause? simple_designation?
  ;

property_or_list_pattern_clause
  : list_pattern_clause
  | property_pattern_clause
  ;

property_pattern_clause
  : '{' (subpattern (',' subpattern)* ','?)? '}'
  ;

list_pattern_clause
  : '{' pattern (',' pattern)* ','? '}'
  ;

length_pattern_clause
  : '[' pattern ']'
  ;

length_pattern
  : type? length_pattern_clause property_or_list_pattern_clause? simple_designation?
  ;

list_pattern
  : type? list_pattern_clause simple_designation?
  ;

property_pattern
  : type? property_pattern_clause simple_designation?
  ;

slice_pattern
  : '..' negated_pattern?
  ;

primary_pattern
  : list_pattern
  | length_pattern
  | slice_pattern
  | // all of the pattern forms previously defined
  ;
```
There are three new patterns:

- The *list_pattern* is used to match elements.
- The *length_pattern* is used to match the length.
- A *slice_pattern* is only permitted once and only directly in a *list_pattern_clause* and discards _**zero or more**_ elements.

> **Open question**: Should we accept a general *pattern* following `..` in a *slice_pattern*?

Notes:

- Due to the ambiguity with *property_pattern*, a *list_pattern* cannot be empty and a *length_pattern* should be used instead to match a list with the length of zero, e.g. `[0]`. 
- The *length_pattern_clause* must be in agreement with the inferred length from the *list_pattern_clause* (if any), e.g. `[0] {1}` is an error.
	- However, `[1] {}` is **not** an error due to the length mismatch, rather, `{}` would be always parsed as an empty *property_pattern_clause*. We may want to add a warning for it so it would not be confused that way.
- If the *type* is an *array_type*, the *length_pattern_clause* is disambiguated so that `int[] [0]` would match an empty integer array.
- All other combinations are valid, for instance `T (p0, p1) [p2] { name: p3 } v` or `T (p0, p1) [p2] { p3 } v` where each clause can be omitted.

> **Open question**: Should we support all these combinations?

#### Pattern compatibility

A *length_pattern* is compatible with any type that is *countable* — it has an accessible property getter that returns an `int` and has the name `Length` or `Count`. If both properties are present, the former is preferred.  
A *length_pattern* is also compatible with any type that is *enumerable* — it can be used in `foreach`.

A *list_pattern* is compatible with any type that is *countable* as well as *indexable* — it has an accessible indexer that takes an `Index` or `int` argument. If both indexers are present, the former is preferred.  
A *list_pattern* is also compatible with any type that is *enumerable*.

A *slice_pattern* is compatible with any type that is *countable* as well as *sliceable* — it has an accessible indexer that takes a `Range` argument or otherwise an accessible `Slice` method that takes two `int` arguments. If both are present, the former is preferred.  
A *slice_pattern* without a subpattern is also compatible with any type that is *enumerable*.

```
enumerable is { 1, 2, .. } // okay
enumerable is { 1, 2, ..var x } // error
```

This set of rules is derived from the [***range indexer pattern***](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md#implicit-index-support) but relaxed to ignore optional or `params` parameters, if any.

> **Open question**: We should define the exact binding rules for any of these members and decide if we want to diverge from the range spec.  
> **Open question**: Should extension methods play a role in sliceability?  

#### Semantics on enumerable type

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

#### Subsumption checking

Subsumption checking works just like [positional patterns with `ITuple`](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/patterns.md#positional-pattern) - corresponding subpatterns are matched by position plus an additional node for testing length.

For example, the following code produces an error because both patterns yield the same DAG:

```cs
case {_, .., 1}: // expr.Length is >= 2 && expr[^1] is 1
case {.., _, 1}: // expr.Length is >= 2 && expr[^1] is 1
```
Unlike:
```cs
case {_, 1, ..}: // expr.Length is >= 2 && expr[1] is 1
case {.., 1, _}: // expr.Length is >= 2 && expr[^2] is 1
```

The order in which subpatterns are matched at runtime is unspecified, and a failed match may not attempt to match all subpatterns.

> **Open question**: The pattern `{..}` tests for  `expr.Length >= 0`. Should we omit such test (assuming `Length` is always non-negative)?
 
#### Lowering on countable/indexeable/sliceable type

A pattern of the form `expr is {1, 2, 3}` is equivalent to the following code (if compatible via implicit `Index` support):
```cs
expr.Length is 3
&& expr[0] is 1
&& expr[1] is 2
&& expr[2] is 3
```
A *slice_pattern* acts like a proper discard i.e. no tests will be emitted for such pattern, rather it only affects other nodes, namely the length and indexer. For instance, a pattern of the form `expr is {1, .. var s, 3}`  is equivalent to the following code (if compatible via explicit `Index` and `Range` support):
```cs
expr.Length is >= 2
&& expr[new Index(0)] is 1
&& expr[new Range(1, new Index(1, true))] is var s
&& expr[new Index(1, true)] is 3
```
The *input type* for the *slice_pattern* is the return type of the underlying `this[Range]` or `Slice` method with two exceptions: For `string` and arrays, `string.Substring` and `RuntimeHelpers.GetSubArray` will be used, respectively.

#### Lowering on enumerable type

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

Note: async enumerables are out-of-scope. (Confirmed in LDM 4/12/2021)
Note: sub-patterns are disallowed in list-patterns on enumerables for now despite some desirable uses: `e is { 1, 2, ..[var count] }` (LDM 4/12/2021)

### Additional types

Beyond the pattern-based mechanism outlined above, there are an additional two set of types that can be covered as a special case.

- **Multi-dimensional arrays**: All nested list patterns must agree to a length range.
- **Foreach-able types**: This includes pattern-based and extension `GetEnumerator`.

A slice subpattern (i.e. the pattern following `..` in a *slice_pattern*) is disallowed for either of the above.

## Unresolved questions

All multi-dimensional arrays can be non-zero-based. We can either:

1. Add a runtime helper to check if the array is zero-based across all dimensions.
2. Call `GetLowerBound` and add it to each indexer access to pass the *correct* index.
3. Assume all arrays are zero-based since that's the default for arrays created by `new` expressions.
4. Should we limit the list-pattern to `IEnumerable` types? Then we could allow `{ 1, 2, ..var x }` (`x` would be an `IEnumerable` we would cook up) (answer [LDM 4/12/2021]: no, we'll disallow sub-pattern in slice pattern on enumerable for now)
5. Should we try and optimize list-patterns like `{ 1, _, _ }` on a countable enumerable type? We could just check the first enumerated element then check `Length`/`Count`. Can we assume that `Count` agrees with enumerated count?
6. Should we try to cut the enumeration short for length-patterns on enumerables in some cases? (computing min/max acceptable count and checking partial count against that)
  What if the enumerable type has some sort of `TryGetNonEnumeratedCount` API?  
7. Can we detect at runtime that the input type is sliceable, so as to avoid enumeration? .NET 6 may be adding some LINQ methods/extensions that would help. 
