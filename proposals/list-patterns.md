# List patterns

## Summary

Lets you to match an array or a list with a sequence of patterns e.g. `array is {1, 2, 3}` will match an integer array of the length three with 1, 2, 3 as its elements, respectively.

## Detailed Design

The pattern syntax is modified as follow:

```antlr
positional_pattern
  : type? positional_pattern_clause length_pattern_clause? property_or_list_pattern_clause? simple_designation?
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

length_pattern_clause
  : '[' pattern ']'
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

- The *list_pattern* is used to match elements and the *length_pattern* is used to match the length.
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

A *length_pattern* is compatible with any type that is *countable* - it has an accessible property getter that returns an `int` and has the name `Length` or `Count`. If both properties are present, the former is preferred.

A *list_pattern* is compatible with any type that is *countable* as well as *indexable* - it has an accessible indexer that takes an `Index` or `int` argument. If both indexers are present, the former is preferred.

A *slice_pattern* is compatible with any type that is *countable* as well as *sliceable* - it has an accessible indexer that takes a `Range` argument or otherwise an accessible `Slice` method that takes two `int` arguments. If both are present, the former is preferred.

This set of rules is derived from the [***range indexer pattern***](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md#implicit-index-support) but relaxed to ignore optional or `params` parameters, if any.

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
> 
#### Lowering

A pattern of the form `expr is {1, 2, 3}` is equivalent to the following code:
```cs
expr.Length is 3
&& expr[0] is 1
&& expr[1] is 2
&& expr[2] is 3
```
A *slice_pattern* acts like a proper discard i.e. no tests will be emitted for such pattern, rather it only affects other nodes, namely the length and indexer. For instance, a pattern of the form `expr is {1, .. var s, 3}`  is equivalent to the following code:
```cs
expr.Length    is >= 2
&& expr[0]     is 1
&& expr[1..^1] is var s
&& expr[^1]    is 3
```
The *input type* for the *slice_pattern* is the return type of the underlying `this[Range]` or `Slice` method with two exceptions: For `string` and arrays, `string.Substring` and `RuntimeHelpers.GetSubArray` will be used, respectively.

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
