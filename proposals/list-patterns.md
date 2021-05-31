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
  : '..' pattern?
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

Notes:

- Due to the ambiguity with *property_pattern*, a *list_pattern* cannot be empty and a *length_pattern* should be used instead to match a list with the length of zero, e.g. `[0]`. 
- The *length_pattern_clause* must be in agreement with the inferred length from the *list_pattern_clause* (if any), e.g. `[0] {1}` is an error.
	- However, `[1] {}` is **not** an error due to the length mismatch, rather, `{}` would be always parsed as an empty *property_pattern_clause*. We may want to add a warning for it so it would not be confused that way.
- If the *type* is an *array_type*, the *length_pattern_clause* is disambiguated so that `int[] [0]` would match an empty integer array.
- All other combinations are valid, for instance `T (p0, p1) [p2] { name: p3 } v` or `T (p0, p1) [p2] { p3 } v` where each clause can be omitted.

#### Pattern compatibility

A *length_pattern* is compatible with any type that is *countable* — it has an accessible property getter that returns an `int` and has the name `Length` or `Count`. If both properties are present, the former is preferred.  

A *list_pattern* is compatible with any type that is *countable* as well as *indexable* — it has an accessible indexer that takes an `Index` as an argument or otherwise an accessible indexer with a single `int` parameter. If both indexers are present, the former is preferred.  

A *slice_pattern* with a subpattern is compatible with any type that is *countable* as well as *sliceable* — it has an accessible indexer that takes a `Range` as an argument or otherwise an accessible `Slice` method with two `int` parameters. If both are present, the former is preferred.
A *slice_pattern* without a subpattern is compatible with any type that is compatible with a *list_pattern*.

This set of rules is derived from the [***range indexer pattern***](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md#implicit-index-support).

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
 
#### Lowering

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

## Unresolved questions

1. Should we support multi-dimensional arrays? (answer [LDM 2021-05-26]: Not supported. If we want to make a general MD-array focused release, we would want to revisit all the areas they're currently lacking, not just list patterns.)
2. Should we accept a general *pattern* following `..` in a *slice_pattern*? (answer [LDM 2021-05-26]: Yes, any pattern is allowed after a slice.)
3. By this definition, the pattern `{..}` tests for `expr.Length >= 0`. Should we omit such test, assuming `Length` is always non-negative? (answer [LDM 2021-05-26]: `{..}` will not emit a Length check)