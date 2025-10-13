# Immediately Enumerated Collection Expressions

Champion issue: https://github.com/dotnet/csharplang/issues/9754

## Summary
[summary]: #summary

Permit collection expressions in "immediately enumerated" contexts, without requiring a target type.

```cs
foreach (bool b in [true, false])
{
    doMyLogicWith(b);
}

string[] items1 = ["a", "b", .. ["c"]];
```

See also https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/collection-expressions-inferred-type.md.

## Motivation
[motivation]: #motivation

Choosing a single "natural type" for collection expressions in the general case, e.g. `var coll = [1, 2, 3];`, has proven to be a difficult question and represents a major commitment. Should `coll` be a `List<int>`, `ReadOnlySpan<int>`, or something else? It's not obvious, and some of the possible answers represent a major engineering cost, and in any case a major statement about the defaults/preferences of the ecosystem.

However, we do see a significant amount of demand for the ability to use collection expressions in contexts where the collection is immediately enumerated, and the collection value itself is not directly observable in user code. In this case, we should be able to define a solution which is convenient, optimal, and relatively low risk.

## Detailed design
[design]: #detailed-design

### foreach

Given a foreach statement of the form:
```cs
foreach (iteration_type iteration_variable in collection)
    embedded_stmt
```
- When `collection` lacks a natural type, we determine if a *collection expression iteration conversion* exists, from `collection` to type `IEnumerable<TElem>`, and apply the conversion if it exists.
- `TElem` is determined in the following way:
    - If `iteration_type` is explicitly typed (i.e. not `var`), then `TElem` is `iteration_type`.
    - Otherwise, `TElem` is the *best common element type* of `collection`.
- If the type of `TElem` can be determined, then the *collection expression iteration conversion* exists. Otherwise, the conversion does not exist.
- As in [collection-expressions.md](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md), `IEnumerable<T>` refers to `System.Collections.Generic.IEnumerable<T>` throughout this specification.

### spreads

Given a *spread element* of the form:
```cs
.. collection
```
- When `collection` lacks a natural type, we determine if a *collection expression iteration conversion* exists, from `collection` to type `IEnumerable<TElem>`, and apply the conversion if it exists.
- `TElem` is determined in the following way:
    - If the collection-expression containing the spread element `.. collection` is subject to a *collection expression conversion* to a type with an *element type*, then `TElem` is that *element type*.
    - Otherwise, `TElem` is the *best common element type* of `collection`.
- If the type of `TElem` can be determined, then the *collection expression iteration conversion* exists. Otherwise, the conversion does not exist.

#### Remarks

We intend for the following cases, which push element type information down from a target type to just work:
- `foreach (string? x in [null]) { }`
- `string?[] items = [.. [null]];`
- `List<string?> items = [.. [null]];`

When no target element type is available, such as when `foreach (var x ...` form is being used, or when the element type of the containing collection-expression of a spread is not known, then, the *best common element type* mechanism is used to propagate the nested element type information outward.

### Best common element type

See also [collection-expressions.md#type-inference](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference).

The *best common element type* of an expression `E` is determined similarly to the *best common type of a set of expressions* ([§12.6.3.16](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#126316-finding-the-best-common-type-of-a-set-of-expressions)):

- A new *unfixed* type variable `X` is introduced.
- An *output type inference* ([§12.6.3.8](expressions.md#12638-output-type-inferences)) is performed from `E` to `IEnumerable<X>`.
- `X` is *fixed* ([§12.6.3.13](expressions.md#126313-fixing)), if possible, and the resulting type is the best common element type.
- Otherwise inference fails.

For example, in the following statement, the element type of the collection being iterated, is same as the *best common type* of expressions `a, b, c`:

```cs
foreach (var item in [a, b, c])
{
}
```

Note that this feature is intentionally specified in such a way that an element type is determined similarly for the `foreach` collection above, as it is for a generic method call with an `IEnumerable<T>` parameter:

```cs
M([a, b, c]);
void M<T>(IEnumerable<T> items)
{
}
```

### Implementation flexibility

Similar to [collection-expressions-in-foreach.md](https://github.com/dotnet/csharplang/blob/9d618b5eacaca9721550fb9a153a291087c10dae/proposals/collection-expressions-in-foreach.md), the implementation is encouraged to optimize based on the fact that user code can't observe the array which is created for a foreach-collection or spread-value under these new rules. So, it is free to use different strategies to allocate space for the collection elements such as an InlineArray on the stack, or not creating a collection instance at all and instead "inlining" the enumeration of elements.

### Future considerations

This proposal doesn't get us 100% of the way there to "conditional element inclusion" scenarios like the following:

```cs
// The spread value is erroneous even after this proposal
string[] items2 = ["a", "b", .. includeRest ? ["c", "d"] : []];
```

We are interested in pursuing type inference improvements which we expect to improve things across the board--for calls, foreach, and spreads--all in a similar way.
```cs
// Make all of the following work using a future type inference improvement:
M1(cond ? [1] : [2]);
M2(cond ? [1] : [2]);
foreach (var item in cond ? [1] : [2]) { }
string[] array = ["a", "b", .. includeRest ? ["c", "d"] : []];

void M1<T>(T[] items) { }
void M2<T>(IEnumerable<T> items) { }
```

Permitting conditional element inclusion will also grow the optimization space, e.g.:

```cs
List<int> items = [a, b, .. includeMoreItems ? [c, d] : []];

// someday could emit as:
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

Ensuring high-quality code generation in a wide variety of usage scenarios may be a significant amount of work.

## Alternatives
[alternatives]: #alternatives

Take the [collection-expressions-in-foreach.md](https://github.com/dotnet/csharplang/blob/98d6837c32e8d0ab25a29001267be5be206a0f19/proposals/collection-expressions-in-foreach.md) proposal instead, which provides support for the "base case" `foreach (var item in [1, 2, 3])` only, and doesn't provide support in spreads.

## Open questions
[open]: #open-questions

### Output type inference for spreads

The type inference rules (see [Method type argument inference](#method-type-argument-inference)) state the following regarding *output type inference*:

> If `Eᵢ` is a *spread element*, no inference is made from `Eᵢ`.

It looks like this was added back in https://github.com/dotnet/csharplang/pull/7604 andthe significance of this decision isn't 100% clear. It's possibly because we were only making inferences from types at that time. In this proposal, we adjust this so that an output type inference can be made from expression, in the case of `[.. [() => expr1, () => expr2]]`, for example.

**Resolution:** Break out type inference changes into its own proposal, and pare this proposal down to only providing a target type for collections and spread values which lack natural type.

### Use of "special language-level collection type" for immediately enumerated collections

Instead of `T[]`, we could choose to define the feature in terms of a new type kind defined at the language level. We would call it something like an *iteration type `T` with element type `Tₑ`.

This could potentially make it easier to define things in such a way that `foreach (Span<int> span in [span1, span2, span3]) { ... }` could work. However, we don't expect to have support for ref struct element type in the short term. Since ref structs don't work with so many things, that specific support requires careful design and possibly evolution of features like *ref fields* in order to permit types such as `ReadOnlySpan<Span<int>>`.

**Resolution:** Use `IEnumerable<T>` as target type for the scenarios in this proposal. We think that using a special new type kind would add additional spec/implementation cost without adding significant value. Use of `IEnumerable<T>` permits ref structs as elements but not pointers. We'd like to investigate viability of actually generating code for the "collection of Spans" case, with caution, and understanding that the scenario may need to be blocked until separate, further language improvements are made.

### Optimization of immediately enumerated `new[] { }` expressions

Since we are already discussing optimization of `foreach (var i in [1, 2, 3])`, to avoid realizing the `int[]`, we may wish to also permit the compiler to optimize `foreach (var i in new[] { 1, 2, 3 })`.

**Resolution:** We don't think the value is worth the risk, because array literals have been around such a long time. If people want the nice new codegen, they should move to the new syntax form.
