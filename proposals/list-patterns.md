
# List patterns

## Summary

Lets you to match an array or a list with a sequence of patterns e.g. `array is [1, 2, 3]` will match an integer array of the length three with 1, 2, 3 as its elements, respectively.

## Detailed Design

### Fixed-length list patterns

The syntax will be modified to include a *list_pattern* defined as below:

```antlr
primary_pattern
	: list_pattern
list_pattern
	: '[' (pattern (',' pattern)* ','?)? ']'
```

**Pattern compatibility:** A *list_pattern* is compatible with any type that conforms to the ***range indexer pattern***:

1. Has an accessible property getter that returns an `int` and has the name `Length` or `Count`
2. Has an accessible indexer with a single `int` parameter
3. Has an accessible `Slice` method that takes two `int` parameters (for slice subpatterns)

This rule includes `T[]`,  `string`,  `Span<T>`, `ImmutableArray<T>` and more.

**Subsumption checking:**  This construct will be built entirely on top of the existing DAG nodes. The subsumption is checked just like recursive patterns with `ITuple` - corresponding subpatterns are matched by position plus an additional node for testing length.

**Lowering:** A pattern of the form `expr is [1, 2, 3]` is equivalent to the following code:
```cs
expr.Length is 3
&& expr[0] is 1
&& expr[1] is 2
&& expr[2] is 3
```

### Variable-length list patterns

The syntax will be modified to include a *slice_pattern* defined as below:
```antlr
primary_pattern
	: slice_pattern
slice_pattern
	: '..'
```

A *slice_pattern* is only permitted once and only directly in a *list_pattern* and discards ***zero or more*** elements. Note that it's possible to use a *slice_pattern* in a nested *list_pattern* e.g. `[.., [.., 1]]` will match `new int[][]{new[]{1}}`.

A *slice_pattern* acts like a proper discard i.e. no tests will be emitted for such pattern, rather it only affects other nodes, namely the length and indexer. For instance, a pattern of the form `expr is [1, .., 3]`  is equivalent to the following code: 
```cs
expr.Length is >= 2
&& expr[0]  is 1
&& expr[^1] is 3
```
Note: the lowering is presented in the pattern form here to show how subsumption checking works, for example, the following code produces an error because both patterns yield the same DAG:

```cs
case [_, .., 1]: // expr.Length is >= 2 && expr[^1] is 1
case [.., _, 1]: // expr.Length is >= 2 && expr[^1] is 1
```
Unlike:
```cs
case [_, 1, ..]: // expr.Length is >= 2 && expr[1] is 1
case [.., 1, _]: // expr.Length is >= 2 && expr[^2] is 1
```

Note: the pattern `[..]` lowers to `expr.Length >= 0` so it would not be considered as a catch-all.

### Slice subpatterns

We can further extend the *slice_pattern* to be able to capture the skipped sequence:


```antlr
slice_pattern
	: '..' unary_pattern?
```

A pattern of the form `expr is [1, ..var s, 3]` would be equivalent to the following code:

```cs
expr.Length    is >= 2
&& expr[0]     is 1
&& expr[1..^1] is var s
&& expr[^1]    is 3
```

### Additional types

Beyond the pattern-based mechanism outlined above, there are an additional two set of types we can cover as a special case.

#### Multi-dimensional arrays


```cs
array is [[1]]

array.GetLength(0) == 1 &&
array.GetLength(1) == 1 &&
array[0, 0] is 1
```
All multi-dimensional arrays can be non-zero-based. We can either cut this support or either:

1. Add a runtime helper to check if the array is zero-based across all dimensions.
2. Call `GetLowerBound` and add it to each indexer access to pass the *correct* index.
3. Assume all arrays are zero-based since that's the default for arrays created by `new` expressions.


The following rules determine if a pattern is valid for a multi-dimensional array:
- For an array of rank N, only N-1 level of nested list-patterns are accepted.
- Except for that last level, all subpatterns must be either a slice pattern without a subpattern (`..`) or a list-pattern.
- For slice patterns, the usual rules apply - only permitted once and only directly inside the pattern.
- All nested list-patterns must be of an exact or minimum size. Given X = the minimum or exact required length so far and Y = the new length from current nested list pattern, the expected size is calculated as follow:
  ```
  AtLeast(X) + AtLeast(Y) = AtLeast(Max(X, Y))
  Exactly(X) + Exactly(Y) = Exactly(X) only if X==Y
  Exactly(X) + AtLeast(Y) = Exactly(X) only if X>=Y
  ```
  Note: The presence of a slice pattern implies a minimum required length.
#### Foreach-able types
We can reuse `foreach` rules to determine if a type is viable for the match, so this includes pattern-based and extension `GetEnumerator`.

```cs
switch (expr)
{
    case [0]:    // e.MoveNext() && e.Current is 0 && !e.MoveNext()
        break;
    case [0, 1]: // e.MoveNext() && e.Current is 0 && e.MoveNext() && e.Current is 1 && !e.MoveNext()
        break;
}

using (var e = expr.GetEnumerator())
{
    if (e.MoveNext())
    {
        var t0 = e.Current;
        if (t0 is 0)
        {
            if (!e.MoveNext())
            {
                goto case0;
            }
            else
            {
                var t1 = e.Current;
                if (t1 is 1)
                {
                    if (!e.MoveNext())
                    {
                        goto case1;
                    }
                }
            }
        }
    }

    goto @default;
}
```
Like multi-dimensional arrays, we cannot support slice subpatterns, but we do permit `..` only as the last element in which case we simply omit the last call to `MoveNext`.

Note: Unlike other types, `[..]` is actually considered as a catch-all here, since no tests will be emitted for such pattern.

## Questions

- Should we support a trailing designator to capture the input? e.g. `[] v`
- Should we support `this[System.Index]` and `this[System.Range]` indexers?
- Should we support matching an `object` with a type check for `IEnumerable`?
