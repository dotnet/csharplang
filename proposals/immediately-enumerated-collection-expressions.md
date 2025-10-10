# Immediately Enumerated Collection Expressions

Champion issue: `<link to the champion issue>`

## Summary
[summary]: #summary

Permit collection expressions in "immediately enumerated" contexts, without requiring a target type.

```cs
foreach (bool b in [true, false])
{
    doMyLogicWith(b);
}

string[] items1 = ["a", "b", .. ["c"]];
string[] items2 = ["a", "b", .. includeRest ? ["c", "d"] : []];
```

See also https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/collection-expressions-inferred-type.md.

## Motivation
[motivation]: #motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

Choosing a single "natural type" for collection expressions in the general case, e.g. `var coll = [1, 2, 3];`, has proven to be a difficult question and represents a major commitment. Should `coll` be a `List<int>`, `ReadOnlySpan<int>`, or something else? It's not obvious, and some of the possible answers represent a major engineering cost, and in any case a major statement about the defaults/preferences of the ecosystem.

However, we do see a significant amount of demand for the ability to use collection expressions in contexts where the collection is immediately enumerated, and the collection value itself is not directly observable in user code. In this case, we should be able to define a solution which is convenient, optimal, and relatively low risk.

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement,  and include examples of how the feature is used. This section can start out light before the prototyping phase but should get into specifics and corner-cases as the feature is iteratively designed and implemented. -->

### foreach

Given a foreach statement of the form:
```cs
foreach (iteration_type iteration_variable in collection)
    embedded_stmt
```
- When `collection` lacks a natural type, we determine if a *collection expression iteration conversion* exists, from `collection` to type `TElem[]`, and apply the conversion if it exists.
- `TElem` is determined in the following way:
    - If `iteration_type` is explicitly typed (i.e. not `var`), then `TElem` is `iteration_type`.
    - Otherwise, `TElem` is the *best common element type* of `collection`.
- If the type of `TElem` can be determined, then the *collection expression iteration conversion* exists. Otherwise, the conversion does not exist.

### spreads

Given a *spread element* of the form:
```cs
.. collection
```
- When `collection` lacks a natural type, we determine if a *collection expression iteration conversion* exists, from `collection` to type `TElem[]`, and apply the conversion if it exists.
- `TElem` is the *best common element type* of `collection`.
- If the type of `TElem` can be determined, then the *collection expression iteration conversion* exists. Otherwise, the conversion does not exist.

### Best common element type

- The *best common element type* is the *best common type* of the *element expression closure* of `collection`.
- The *element expression closure* of `collection` is a set of expressions determined by the following algorithm:
- Let `set` be an empty set.
- If `collection` is a collection-expression, then for each each element `elem`:
    - If `elem` is an expression element, add it to `set`.
    - If `elem` is a spread element `..S`, then determine the *element expression closure* of `S`, and add all its elements to `set`.
- If `collection` is a conditional expression of the form `cond ? a : b`:
    - Determine the *element expression closure* of `a` and `b` respectively and add all the elements of both sets into `set`.
- If `collection` is a switch expression of the form `cond switch { p_1 => e_1, p_2 => e_2, ..., p_n => e_n }`:
    - Determine the *element expression closure*s of all `e1..en`, and add all elements of the resulting sets into `set`.

This means that certain subexpressions are searched recursively in order to come up with an element type for the containing expression, which is then "pushed back down" into the elements via target-typed conversion.

For example, in the following statement, the element type of the collection being iterated, is the *best common type* of expressions `a, b, c, d, e, f, g, h`:

```cs
foreach (var item in [
    .. flag1 ? [a] : [b, c],
    .. (flag2, flag3) switch
    {
        (true, true) => [d, e],
        (_, false) => [],
        _ => [f],
    },
    .. [g],
    h
])
{
}
```

---

Note also that this element closure mechanism only comes in to play when certain subexpressions lack a natural type. For example, the following is permitted today, and the empty `[]` is converted to `T[]` due to type information provided by `items`:

```cs
bool useItems = false;
int[] items = [1, 2, 3];
foreach (var i in useItems ? items : [])
{
}
```

### Method type argument inference

[Type inference](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference) is enriched in an analogous way, searching subexpressions of `?:`/`switch` and applying the natural type information we find about them as inference bounds. This causes us to permit scenarios like the following:

```cs
bool flag = true;
M(flag ? [1] : [2]); // ambiguous before, now permitted
M([1]); // permitted before and now

void M<T>(T[] items) { }
```

### Implementation flexibility

Similar to [collection-expressions-in-foreach.md](https://github.com/dotnet/csharplang/blob/9d618b5eacaca9721550fb9a153a291087c10dae/proposals/collection-expressions-in-foreach.md), the implementation is encouraged to optimize based on the fact that user code can't observe the array which is created for a foreach-collection or spread-value under these new rules. So, it is free to use different strategies to allocate space for the collection elements such as an InlineArray on the stack.

Below is an example of an emit strategy for "conditional element inclusion":

```cs
List<int> items = [a, b, .. includeMoreItems ? [c, d] : []];

// emit as:
List<int> items = new List<int>(capacity: 2 + 2);
items.Add(a);
items.Add(b);
if (includeMoreItems)
{
    items.Add(c);
    items.Add(d);
}
```

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

Ensuring high-quality code generation in a wide variety of usage scenarios may be a significant amount of work.

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

If we do nothing, then, most likely users will insert casts in places where this implicit typing is desired, and accept sub-optimal codegen for creating the resulting collections.

## Open questions
[open]: #open-questions

<!-- What parts of the design are still undecided? -->

None specified.
