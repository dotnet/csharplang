# List patterns

## Summary

Lets you to match an array or a list with a sequence of patterns e.g. `array is [1, 2, 3]` will match an integer array of the length three with 1, 2, 3 as its elements, respectively.

## Detailed design

The pattern syntax is modified as follow:

```antlr
list_pattern_clause
  : '[' (pattern (',' pattern)* ','?)? ']'
  ;

list_pattern
  : list_pattern_clause simple_designation?
  ;

slice_pattern
  : '..' pattern?
  ;

primary_pattern
  : list_pattern
  | slice_pattern
  | // all of the pattern forms previously defined
  ;
```
There are two new patterns:

- The *list_pattern* is used to match elements.
- A *slice_pattern* is only permitted once and only directly in a *list_pattern_clause* and discards _**zero or more**_ elements.

#### Pattern compatibility

A *list_pattern* is compatible with any type that is *countable* as well as *indexable* — it has an accessible indexer that takes an `Index` as an argument or otherwise an accessible indexer with a single `int` parameter. If both indexers are present, the former is preferred.  

A *slice_pattern* with a subpattern is compatible with any type that is *countable* as well as *sliceable* — it has an accessible indexer that takes a `Range` as an argument or otherwise an accessible `Slice` method with two `int` parameters. If both are present, the former is preferred.

A *slice_pattern* without a subpattern is compatible with any type that is compatible with a *list_pattern*.

This set of rules is derived from the [***range indexer pattern***](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md#implicit-index-support).

#### Subsumption checking

Subsumption checking works just like [positional patterns with `ITuple`](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/patterns.md#positional-pattern) - corresponding subpatterns are matched by position plus an additional node for testing length.

For example, the following code produces an error because both patterns yield the same DAG:

```cs
case [_, .., 1]: // expr.Length is >= 2 && expr[^1] is 1
case [.., _, 1]: // expr.Length is >= 2 && expr[^1] is 1
```
Unlike:
```cs
case [_, 1, ..]: // expr.Length is >= 2 && expr[1] is 1
case [.., 1, _]: // expr.Length is >= 2 && expr[^2] is 1
```

The order in which subpatterns are matched at runtime is unspecified, and a failed match may not attempt to match all subpatterns.

Given a specific length, it's possible that two subpatterns refer to the same element, in which case a test for this value is inserted into the decision DAG.

- For instance, `[_, >0, ..] or [.., <=0, _]` becomes `length >= 2 && ([1] > 0 || length == 3 || [^2] <= 0)` where the length value of 3 implies the other test.
- Conversely, `[_, >0, ..] and [.., <=0, _]` becomes `length >= 2 && [1] > 0 && length != 3 && [^2] <= 0` where the length value of 3 disallows the other test.

As a result, an error is produced for something like `case [.., p]: case [p]:` because at runtime, we're matching the same element in the second case.

If a slice subpattern matches a list or a length value, subpatterns are treated as if they were a direct subpattern of the containing list. For instance, `[..[1, 2, 3]]` subsumes a pattern of the form `[1, 2, 3]`.

The following assumptions are made on the members being used:

- The property that makes the type *countable* is assumed to always return a non-negative value, if and only if the type is *indexable*. For instance, the pattern `{ Length: -1 }` can never match an array.
- The member that makes the type *sliceable* is assumed to be well-behaved, that is, the return value is never null and that it is a proper subslice of the containing list. 

The behavior of a pattern-matching operation is undefined if any of the above assumptions doesn't hold.

#### Lowering

A pattern of the form `expr is [1, 2, 3]` is equivalent to the following code:
```cs
expr.Length is 3
&& expr[new Index(0, fromEnd: false)] is 1
&& expr[new Index(1, fromEnd: false)] is 2
&& expr[new Index(2, fromEnd: false)] is 3
```
A *slice_pattern* acts like a proper discard i.e. no tests will be emitted for such pattern, rather it only affects other nodes, namely the length and indexer. For instance, a pattern of the form `expr is [1, .. var s, 3]`  is equivalent to the following code (if compatible via explicit `Index` and `Range` support):
```cs
expr.Length is >= 2
&& expr[new Index(0, fromEnd: false)] is 1
&& expr[new Range(new Index(1, fromEnd: false), new Index(1, fromEnd: true))] is var s
&& expr[new Index(1, fromEnd: true)] is 3
```
The *input type* for the *slice_pattern* is the return type of the underlying `this[Range]` or `Slice` method with two exceptions: For `string` and arrays, `string.Substring` and `RuntimeHelpers.GetSubArray` will be used, respectively.

## Unresolved questions

1. Should we support multi-dimensional arrays? (answer [LDM 2021-05-26]: Not supported. If we want to make a general MD-array focused release, we would want to revisit all the areas they're currently lacking, not just list patterns.)
2. Should we accept a general *pattern* following `..` in a *slice_pattern*? (answer [LDM 2021-05-26]: Yes, any pattern is allowed after a slice.)
3. By this definition, the pattern `[..]` tests for `expr.Length >= 0`. Should we omit such test, assuming `Length` is always non-negative? (answer [LDM 2021-05-26]: `[..]` will not emit a Length check)
