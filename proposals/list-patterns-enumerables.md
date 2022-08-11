# List patterns on enumerables

## Summary

Lets you to match an enumerable with a sequence of patterns e.g. `enumerable is [ 1, 2, 3 ]` will match a sequence of the length three with 1, 2, 3 as its elements.

## Detailed design

The pattern syntax is unchanged:

```antlr
primary_pattern
  : list_pattern
  | slice_pattern
  | // all of the pattern forms previously defined
  ;
```

### Pattern compatibility

When the type is not *indexable* or *countable*, pattern compatibility is defined as follows:

- A *list_pattern* is compatible with any type that is *enumerable* — it can be used in `foreach`.
- A *slice_pattern* is compatible with any type that is compatible with a *list_pattern* but no subpatterns are allowed and it can only appear as the last item in the list.

```cs
enumerable is [ 1, 2 ]           // okay
enumerable is [ 1, 2, .. ]       // okay
enumerable is [ 1, 2, .. var x ] // error
enumerable is [ 1, 2, .., 3 ]    // error
```

### Semantics on enumerable input

The *list_pattern* enumerates elements from the collection and checks them against the listed subpatterns. 

When multiple *list_patterns* are applied to one input value, the collection will be enumerated once at most.

It is possible that the collection will not be completely enumerated. For example, if one of the subpatterns in the list doesn't match or when there are no ending patterns in a *list_pattern* (e.g. `[ 1, 2, .. ]`).

If an enumerator is produced when a *list_pattern* is applied to an enumerable type and that enumerator is disposable it will be disposed when a top-level pattern containing the *list_pattern* successfully matches, or when none of the patterns match (in the case of a `switch` statement or expression). It is possible for an enumerator to be disposed more than once and the enumerator must ignore all calls to `Dispose` after the first one.
```cs
// any enumerator used to evaluate this switch statement is disposed at the indicated locations
_ = collection switch
{
  [ 1 ] => /* here */  ...,
  _ => /* here */ ...,
};
/* here too, with a spilled try/finally around the switch expression */
```

Note: async enumerables are out-of-scope. (Confirmed in LDM 4/12/2021) 

## Unresolved questions

1. Should we limit the list-pattern to `IEnumerable` types? Then we could allow [ 1, 2, ..var x ] (x would be an IEnumerable we would cook up) (answer [LDM 4/12/2021]: no, we'll disallow sub-pattern in slice pattern on enumerable for now)
2. Should we try and optimize list-patterns like `[ 1, _, _ ]` on a countable enumerable type? Can we assume that `Count` agrees with enumerated count?
3. What is the expected behavior if the input type is indexable but not countable? Should we fallback to enumerable semantics?
4. On the usage of `System.Linq.Enumerable.TryGetNonEnumeratedCount<T>` as previously discussed:
	  - Should we try to bind to any applicable instance or extension method with the same name?
	  - Should we fallback to `Enumerable.TryGetNonEnumeratedCount` even if `System.Linq` is not imported?
	  - Should we try to emit `Enumerable.TryGetNonEnumeratedCount` even if it causes boxing?
	  - Is it possible for runtime to provide a non-generic variation of this API?
