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
string[] items2 = ["a", "b", .. includeRest ? ["c", "d"] : []];
```

See also https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/collection-expressions-inferred-type.md.

## Motivation
[motivation]: #motivation

Choosing a single "natural type" for collection expressions in the general case, e.g. `var coll = [1, 2, 3];`, has proven to be a difficult question and represents a major commitment. Should `coll` be a `List<int>`, `ReadOnlySpan<int>`, or something else? It's not obvious, and some of the possible answers represent a major engineering cost, and in any case a major statement about the defaults/preferences of the ecosystem.

However, we do see a significant amount of demand for the ability to use collection expressions in contexts where the collection is immediately enumerated, and the collection value itself is not directly observable in user code. In this case, we should be able to define a solution which is convenient, optimal, and relatively low risk.

Today, we believe that users are using `new[] { ... }` or `(T[])[...]` instead of `[...]`, when possible for "immediately enumerated"/"conditional element inclusion" cases. Today this results in sub-optimal allocation behavior, or verbosity resulting from the limitations of type inference.

## Detailed design
[design]: #detailed-design

### Method type argument inference

[Type inference](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference) is enriched by searching subexpressions of `?:`/`switch` and applying the natural type information we find about collection elements as inference bounds. This causes us to permit scenarios like the following:

```cs
bool flag = true;
M(flag ? [1] : [2]); // ambiguous before, now permitted
M([1]); // permitted before and now

void M<T>(T[] items) { }
```

#### Specific rules

See also https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference

Copying and augmenting the *type inference first phase* rules from [collection-expressions.md](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference) (new text in **bold**, deleted text in ~~strikethough~~):

> An *input type inference* is made *from* an expression `E` *to* a type `T` in the following way:
>
> * If `E` is a *collection expression* with elements `Eᵢ`, and `T` is a type with an *element type* `Tₑ` or `T` is a *nullable value type* `T0?` and `T0` has an *element type* `Tₑ`, then for each `Eᵢ`:
>   * If `Eᵢ` is an *expression element*, then an *input type inference* is made *from* `Eᵢ` *to* `Tₑ`.
>   * If `Eᵢ` is a *spread element* with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md#1395-the-foreach-statement) `Sᵢ`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#126310-lower-bound-inferences) is made *from* `Sᵢ` *to* `Tₑ`.
>   * **If `Eᵢ` is a *spread element* with no *iteration type*, then an *input type inference* is made *from* `Eᵢ` *to* `Tₑ`.**
> * **If `E` is a *conditional expression* of the form `cond ? a : b`, and does not have a natural type, then:**
>     * **An *input type inference* is made from `a` to `T`, and**
>     * **An *input type inference* is made from `b` to `T`.**
> * **If `E` is a *switch expression* of the form `input switch { P₁ => E₁, P₂ => E₂, ..., Pₙ => Eₙ }`, and does not have a natural type, then for each `Eᵢ`:**
>     * **An *input type inference* is made from `Eᵢ` to `T`.**
> * *[existing rules from first phase]* ...

---

> An *output type inference* is made *from* an expression `E` *to* a type `T` in the following way:
>
> * If `E` is a *collection expression* with elements `Eᵢ`, and `T` is a type with an *element type* `Tₑ` or `T` is a *nullable value type* `T0?` and `T0` has an *element type* `Tₑ`, then for each `Eᵢ`:
>   * If `Eᵢ` is an *expression element*, then an *output type inference* is made *from* `Eᵢ` *to* `Tₑ`.
>   * ~~If `Eᵢ` is a *spread element*, no inference is made from `Eᵢ`.~~
>   * **If `Eᵢ` is a *spread element*, an *output type inference* is made from `Eᵢ` to `T`.**
> * **If `E` is a *conditional expression* of the form `cond ? a : b`, and does not have a natural type, then:**
>     * **An *output type inference* is made from `a` to `T`, and**
>     * **An *output type inference* is made from `b` to `T`.**
> * **If `E` is a *switch expression* of the form `input switch { P₁ => E₁, P₂ => E₂, ..., Pₙ => Eₙ }`, and does not have a natural type, then for each `Eᵢ`:**
>     * **An *output type inference* is made from `Eᵢ` to `T`.**
> * *[existing rules from output type inferences]* ...

#### Remarks

**Why limit this to when the conditional-exprs/switches lack natural type?** In the case that the expressions *do* have natural type, we speculate that the existing rule for *inference from an expression with a type* will be adequate. Collection-expressions within such expressions will be converted to the *best common type* which we know the expression has by virtue of having a natural type. So, things will already work, e.g.:

```cs
bool flag = false;
List<Base> list = [];
var list2 = flag ? [new Derived()] : list; // 'list2' is of type 'List<Base>'
foreach (var item in list2) // the cond-expr could also be inlined here.
{
}

class Base;
class Derived : Base;
```

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

The *best common element type* of an expression is determined similarly to the *best common type of a set of expressions* ([§12.6.3.16](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#126316-finding-the-best-common-type-of-a-set-of-expressions)):

- A new *unfixed* type variable `X` is introduced.
- For each expression `Ei` an *output type inference* ([§12.6.3.8](expressions.md#12638-output-type-inferences)) is performed from it to `X[]`.
- `X` is *fixed* ([§12.6.3.13](expressions.md#126313-fixing)), if possible, and the resulting type is the best common element type.
- Otherwise inference fails.

When combined to the above changes to type inference rules, it effectively means that certain subexpressions are searched recursively in order to come up with an element type for the containing expression, which is then "pushed back down" into the elements via target-typed conversion.

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

Note that this feature is intentionally specified in such a way, that an element type is determined similarly for the `foreach` collection above, as it is for a generic method call with a `T[]` parameter:

```cs
M([
    .. flag1 ? [a] : [b, c],
    .. (flag2, flag3) switch
    {
        (true, true) => [d, e],
        (_, false) => [],
        _ => [f],
    },
    .. [g],
    h
]);

void M<T>(T[] items) { }
```

---

### Implementation flexibility

Similar to [collection-expressions-in-foreach.md](https://github.com/dotnet/csharplang/blob/9d618b5eacaca9721550fb9a153a291087c10dae/proposals/collection-expressions-in-foreach.md), the implementation is encouraged to optimize based on the fact that user code can't observe the array which is created for a foreach-collection or spread-value under these new rules. So, it is free to use different strategies to allocate space for the collection elements such as an InlineArray on the stack, or not creating a collection instance at all and instead "inlining" the enumeration of elements.

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

### Use of "special language-level collection type" for immediately enumerated collections

Instead of `T[]`, we could choose to define the feature in terms of a new type kind defined at the language level. We would call it something like an *iteration type `T` with element type `Tₑ`.

This could potentially make it easier to define things in such a way that `foreach (Span<int> span in [span1, span2, span3]) { ... }` could work. However, we don't expect to have support for ref struct element type in the short term. Since ref structs don't work with so many things, that specific support requires careful design and possibly evolution of features like *ref fields* in order to permit types such as `ReadOnlySpan<Span<int>>`.

### Optimization of immediately enumerated `new[] { }` expressions

Since we are already discussing optimization of `foreach (var i in [1, 2, 3])`, to avoid realizing the `int[]`, we may wish to also permit the compiler to optimize `foreach (var i in new[] { 1, 2, 3 })`.
