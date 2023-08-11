# Collection expressions - next

## Summary
[summary]: #summary

Additions to [*collection expressions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md).

## Motivation
[motivation]: #motivation

A form for dictionary-like collections is also supported where the elements of the literal are written as `k: v` like `[k1: v1, ..d1]`.  A future pattern form that has a corresponding syntax (like `x is [k1: var v1]`) would be desirable.

## Detailed design
[design]: #detailed-design

```diff
collection_literal_element
  : expression_element
+ | dictionary_element
  | spread_element
  ;

+ dictionary_element
  : expression ':' expression
  ;
```

### Spec clarifications
[spec-clarifications]: #spec-clarifications

* `dictionary_element` instances will commonly be referred to as `k1: v1`, `k_n: v_n`, etc.

* While a collection literal has a *natural type* of `List<T>`, it is permissible to avoid such an allocation if the result would not be observable.  For example, `foreach (var toggle in [true, false])`.  Because the elements are all that the user's code can refer to, the above could be optimized away into a direct stack allocation.

## Conversions
[conversions]: #conversions

The following implicit *collection literal conversions* exist from a collection literal expression:

* ...

* To a *type* that implements `System.Collections.IDictionary` where:
  * The *type* contains an applicable instance constructor that can be invoked with no arguments or invoked with a single argument for the 0-th parameter where the parameter has type `System.Int32` and name `capacity`.
  * For each *expression element* `Ei`:
    * the type of `Ei` is `dynamic` and there is an [applicable](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1265-compile-time-checking-of-dynamic-member-invocation) indexer setter that can be invoked with two `dynamic` arguments, or
    * the type of `Ei` is a type `System.Collections.Generic.KeyValuePair<Ki, Vi>` and there is an applicable indexer setter that can be invoked with two arguments of types `Ki` and `Vi`.
  * For each *dictionary element* `Ki:Vi`, there is an applicable indexer setter that can be invoked with two arguments of types `Ki` and `Vi`.
  * For each *spread element* `Si`:
    * the *iteration type* of `Si` is `dynamic` and there is an [applicable](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1265-compile-time-checking-of-dynamic-member-invocation) indexer setter that can be invoked with two `dynamic` arguments, or
    * the *iteration type* is `System.Collections.Generic.KeyValuePair<Ki, Vi>` and there is an applicable indexer setter that can be invoked with two arguments of types `Ki` and `Vi`.

* To an *interface type* `I<K, V>` where `System.Collections.Generic.Dictionary<TKey, TValue>` implements `I<TKey, TValue>` and where:
  * For each *expression element* `Ei`, the type of `Ei` is `dynamic`, or the type of `Ei` is a type `System.Collections.Generic.KeyValuePair<Ki, Vi>` and there is an implicit conversion from `Ki` to `K` and from `Vi` to `V`.
  * For each *dictionary element* `Ki:Vi` there is an implicit conversion from `Ki` to `K` and from `Vi` to `V`.
  * For each *spread element* `Si`, the *iteration type* of `Si` is `dynamic`, or the *iteration type* is `System.Collections.Generic.KeyValuePair<Ki, Vi>` and there is an implicit conversion from `Ki` to `K` and from `Vi` to `V`.

## Natural type
[natural-type]: #natural-type

In the absence of a *constructible collection target type*, a non-empty literal can have a *natural type*.

The *natural type* is determined from the [*natural element type*](#natural-element-type).
If the *natural element type* `T` cannot be determined, the literal has no *natural type*. If `T` can be determined, the *natural type* of the collection is `List<T>`.

The choice of `List<T>` rather than `T[]` or `ImmutableArray<T>` is to allow mutation of `var` locals after initialization. `List<T>` is preferred over `Span<T>` because `Span<T>` cannot be used in `async` methods.

```c#
var values = [1, 2, 3];
values.Add(4); // ok
```

The *natural element type* may be inferred from `spread_element` enumerated element type.

```c#
var c = [..[1, 2, 3]]; // List<int>
```

Should `IEnumerable` contribute an *iteration type* of `object` or no contribution?

```c#
IEnumerable e1 = [1, 2, 3];
var e2 = [..e1];           // List<object> or error?
List<string> e3 = [..e1];  // error?
```

The *natural type* should not prevent conversions to other collection types in *best common type* or *type inference* scenarios.
```c#
var x = new[] { new int[0], [1, 2, 3] }; // ok: int[][]
var y = First(new int[0], [1, 2, 3]);    // ok: int[]

static T First<T>(T x, T y) => x;
```

---

* For example, given:

    ```c#
    string s = ...;
    object[] objects = ...;
    var x = [s, ..objects]; // List<object>
    ```

    The *natural type* of `x` is `List<T>` where `T` is the *best common type* of `s` and the *iteration type* of `objects`.  Respectively, that would be the *best common type* between `string` and `object`, which would be `object`.  As such, the type of `x` would be `List<object>`.

* Given:

    ```c#
    var values = x ? [1, 2, 3] : []; // List<int>
    ```

    The *best common type* between `[1, 2, 3]` and `[]` causes `[]` to take on the type `[1, 2, 3]`, which is `List<int>` as per the existing *natural type* rules. As this is a constructible collection type, `[]` is treated as target-typed to that collection type.

## Natural element type
[natural-element-type]: #natural-element-type

Computing the *natural element type* starts with three sets of types and expressions called *dictionary key set*, *dictionary value set*, and *remainder set*.

The *dictionary key/value sets* will either both be empty or both be non-empty.

Each element of the literal is examined in the following fashion:

* An element `e_n` has its *type* determined.  If that type is some `KeyValuePair<TKey, TValue>`, then `TKey` is added to *dictionary key set* and `TValue` is added to *dictionary value set*.  Otherwise, the `e_n` *expression* is added to *remainder set*.

* An element `..s_n` has its *iteration type* determined.  If that type is some `KeyValuePair<TKey, TValue>`, then `TKey` is added to *dictionary key set* and `TValue` is added to *dictionary value set*. Otherwise, the *iteration type* is added to *remainder set*.

* An element `k_n: v_n` adds the `k_n` and `v_n` *expressions* to *dictionary key set* and *dictionary value set* respectively.

* If the *dictionary key/value sets* are empty, then there were definitely no `k_n: v_n` elements. In that case, the *fallback case* runs below.

* If *dictionary key/value sets* are non-empty, then a first round of the *best common type* algorithm in performed on those sets to determine `BCT_Key` and `BCT_Value` respectively.

    * If the first round fails for either set, the *fallback case* runs below.

    * If the first round succeeds for both sets, there is a `KeyValuePair<BCT_Key, BCT_Value>` type produced.  This type is added to *remainder set*.  A second round of the *best common type* algorithm is performed on *remainder set* set to determine `BCT_Final`.

        * If the second round fails, the *fallback* case runs below.
        * Otherwise `BCT_Final` is the *natural element type* and the algorithm ends.

* The *fallback case*:

    * All `e_n` *expressions* are added to *remainder set*
    * All `..s_n` *iteration types* are added to *remainder set*
    * The *natural element type* is the *best common type* of the *remainder set* and the algorithm ends.

---

* Given:

    ```c#
    Dictionary<string, object> d1 = ...;
    Dictionary<object, string> d2 = ...;
    var d3 = [..d1, ..d2];
    ```

    The *natural type* of `d3` is `Dictionary<object, object>`.  This is because the `..d1` will have a *iteration type* of `KeyValuePair<string, object>` and `..d2` will have a *iteration type* of `KeyValuePair<object, string>`. These will contribute `{string, object}` to the determination of the `TKey` type and `{object, string}` to the determination of the `TValue` type.  In both cases, the *best common type* of each of these sets is `object`.

* Given:

    ```c#
    var d = [null: null, "a": "b"];
    ```

    The *natural type* of `d` is `Dictionary<string, string>`.  This is because the `k_n: v_n` elements will construct the set `{null, "a"}` for the determination of the `TKey` type and `{null, "b"}` to the determination of the `TValue` type.  In both cases, the *best common type* of each of these sets is `string`.

* Given:

    ```c#
    string s1, s2;
    object o1, o2;
    var d = [s1: o1, o2: s2];
    ```

    The *natural type* of `d3` is `Dictionary<object, object>`.  This is because the `k_n: v_n` elements will construct the set `{s1, o1}` for the determination of the `TKey` type and `{o2, s2}` to the determination of the `TValue` type.  In both cases, the *best common type* of each of these sets is `object`.

* Given:

    ```c#
    string s1, s2;
    object o1, o2;
    var d = [KeyValuePair.Create(s1, o1), KeyValuePair.Create(o2, s2)];
    ```

    The *natural type* of `d3` is `Dictionary<object, object>`.  This is because the `e_n` elements are `KeyValuePair<string, object>` and `KeyValuePair<object, string>` respectively.  These will construct the set `{string, object}`for the determination of the `TKey` type and `{object, string}` to the determination of the `TValue` type.  In both cases, the *best common type* of each of these sets is `object`.

### Interface translation
[interface-translation]: #interface-translation

Given a target type `T` for a literal:

* If `T` is some interface `I<TKey, TValue>` where that interface is implemented by `Dictionary<TKey, TValue>`, then the literal is translated as:

    ```c#
    Dictionary<TKey, TValue> __temp = [...]; /* standard translation */
    I<TKey, TValue> __result = __temp;
    ```

* If `T` is a dictionary collection initializer with key `K1` and value `V1`, the literal is translated as:

    ```c#
    T __result = new T(capacity: __len);

    __result[__e1.Key] = __e1.Value;
    __result[__k1] = __v1;
    foreach (var __t in __s1)
        __result[__t.Key] = __t.Value;

    // further additions of the remaining elements
    ```

    * In this translation, `expression_element` is only supported if the element type is some `KeyValuePair<,>` or `dynamic`, and `spread_element` is only supported if the enumerated element type is some `KeyValuePair<,>` or `dynamic`.

## Syntax ambiguities
[syntax-ambiguities]: #syntax-ambiguities

* `dictionary_element` can be ambiguous with a [`conditional_expression`](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1115-conditional-operator).  For example:

    ```c#
    var v = [ a ? [b] : c ];
    ```

    This could be interpreted as `expression_element` where the `expression` is a `conditional_expression` (e.g. `[ (a ? [b] : c) ]`).  Or it could be interpreted as a `dictionary_element` `"k: v"` where `a?[b]` is `k`, and `c` is `v`.

## Resolved questions
[resolved]: #resolved-questions

* Should a `collection_literal_expression` have a *natural type*?  In other words, should it be legal to write the following:

    ```c#
    var x = [1, 2, 3];
    ```

    Resolution: Yes, the *natural type* will be an appropriate instantiation of `List<T>`. The following text exists to record the original discussion of this topic.

    <details>

    It is virtually certain that users will want to do this.  However, there is much less certainty both on what users would want this mean and if there is even any sort of broad majority on some default.  There are numerous types we could pick, all of which have varying pros and cons.  Specifically, our options are *at least* any of the following:

    * Array types
    * Span types
    * `ImmutableArray<T>`
    * `List<T>`
    * [`ValueArray<T, N>`](https://github.com/dotnet/roslyn/pull/57286)

    Each of those options has varying benefits with respect to the following questions:

    * Will the literal cause a heap allocation (and, if so, how many), or can it live on the stack?
    * Are the values of the literal mutable after creation or are they fixed?
    * Is the resultant value itself mutable (e.g. can it be cleared, or can new elements be added to it)?
    * Can the value be used in all contexts (for example, async/non-async)?
    * Can be used for *all* literal forms (for example, a `spread_element` of an *unknown length*)?

    Note: for whatever type we pick as a *natural type*, the user can always target-type to the type they want with a simple cast, though that won't be pleasant.

    With all of that, we have a matrix like so:

    | type | heap allocs | mutable elements | mutable collection | async | all literal forms |
    |-|-|-|-|-|-|
    | `T[]` | 1 | Yes | No | Yes | No* |
    | `Span<T>` | 0 | Yes | No | No | No* |
    | `ReadOnlySpan<T>` | 0 | No | No | No | No* |
    | `List<T>` | 2 | Yes | Yes | Yes | Yes |
    | `ImmutableArray<T>` | 1 | No | No | Yes | No* |
    | `ValueArray<T, N>` | ? | ? | ? | ? | ? |

    \* `T[]`, `Span<T>` and `ImmutableArray<T>` might potentially work for 'all literal forms' if we extend this spec greatly with some sort of builder mechanism that allows us to tell it about all the pieces, with a final `T[]` or `Span<T>` obtained from the builder which can also then be passed to the `Construct` method used by *known length* translation in order to support `ImmutableArray<T>` and any other collection.

    Only `List<T>` gives us a `Yes` for all columns. However, getting `Yes` for everything is not necessarily what we desire.  For example, if we believe the future is one where immutable is the most desirable, the types like `T[]`, `Span<T>`, or `List<T>` may not complement that well.  Similarly if we believe that people will want to use these without paying for allocations, then `Span<T>` and `ReadOnlySpan<T>` seem the most viable.

    However, the likely crux of this is the following:

    * Mutation is part and parcel of .NET
    * `List<T>` is already heavily the lingua franca of lists.
    * `List<T>` is a viable final form for any potential list literal (including those with spreads of *unknown length*)
    * Span types and ValueArray are too esoteric, and the inability to use ref structs within async-contexts is likely a deal breaker for broad acceptance.

    As such, while it unfortunate that it has two allocations, `List<T>` seems be the most broadly applicable. This is likely what we would want from the *natural type*.

    I believe the only other reasonable alternative would be `ImmutableArray<T>`, but either with the caveat that that it cannot support `spread_elements` of *unknown length*, or that we will have to add a fair amount of complexity to this specification to allow for some API pattern to allow it to participate.  That said, we should strongly consider adding that complexity if we believe this will be the recommended collection type that we and the BCL will be encouraging people to use.

    Finally, we could consider having different *natural types* in different contexts (like in an async context, pick a type that isn't a ref struct), but that seems rather confusing and distasteful.

    </details>
