# Dictionary Expressions

## Summary

*Dictionary Expressions* are a continuation of the C# 12 *Collection Expressions* feature.  They extend that system with a new terse syntax, `["mads": 21, "dustin": 22]`, for creating common dictionary values.  Like with collection expressions, merging other dictionaries into these values is possible using the existing spread operator `..` like so: `[.. currentStudents, "mads": 21, "dustin": 22]`

Several dictionary-like types can created without external BCL support.  These types are:

1. Dictionary-like types, containing an indexer `TValue this[TKey] { get; set; }`, like `Dictionary<TKey, TValue>` and `ConcurrentDictionary<TKey, TValue>`.
1. The well-known BCL dictionary interface types: `IDictionary<TKey, TValue>` and `IReadOnlyDictionary<TKey, TValue>`.

Further support is present for dictionary-like types not covered above through the `CollectionBuilderAttribute` and a similar API pattern to the corresponding *create method* pattern introduced for collection expressions.

## Motivation

While dictionaries are similar to standard sequential collections in that they can be interpreted as a sequence of key/value pairs, they differ in that they are often used for their more fundamental capability of efficient looking up of values based on a provided key.  In an analysis of the BCL and the NuGet package ecosystem, sequential collection types and values make up the lion's share of collections used.  However, dictionary types were still used a significant amount, with appearances in APIs occurring at between 5 and 10% the amount of sequential collections, and with dictionary values appearing universally in all programs.

Currently, all C# programs must use many different and unfortunately verbose approaches to create instances of such values. Some approaches also have performance drawbacks. Here are some common examples:

1. Collection-initializer types, which require syntax like `new Dictionary<X, Y> { ... }` (lacking inference of possibly verbose TKey and TValue) prior to their values, and which can cause multiple reallocations of memory because they use `N` `.Add` invocations without supplying an initial capacity.
1. Immutable collections, which require syntax like `ImmutableDictionary.CreateRange(...)`, but which are also unpleasant due to the need to provide values as an `IEnumerable<KeyValuePair>`.  Builders are even more unwieldy.
1. Read-only dictionaries, which require first making a normal dictionary, then wrapping it.
1. Concurrent dictionaries, which lack an `.Add` method, and thus cannot be used with normal collection initializers.

Looking at the surrounding ecosystem, we also find examples everywhere of dictionary creation being more convenient and pleasant to use. Swift, TypeScript, Dart, Ruby, Python, and more, opt for a succinct syntax for this purpose, with widespread usage, and to great effect. Cursory investigations have revealed no substantive problems arising in those ecosystems with having these built-in syntax forms.

Unlike with *collection expressions*, C# does not have an existing pattern serving as the corresponding deconstruction form.  Designs here should be made with a consideration for being complementary with deconstruction work. 

An inclusive solution is needed for C#. It should meet the vast majority of case for customers in terms of the dictionary-like types and values they already have. It should also feel pleasant in the language, complement the work done with collection expressions, and naturally extend to pattern matching in the future.

## Detailed Design

The following grammar productions are added:

```diff
collection_element
  : expression_element
  | spread_element
+ | key_value_pair_element
+ | comparer_element 
  ;

+ key_value_pair_element
+  : expression ':' expression
+  ;

+ comparer_element
+  : 'comparer': expression
+  ;
```

Alternative syntaxes are available for consideration, but should be considered later due to the bike-shedding cost involved.  Picking the above syntax allows for the compiler team to move quickly at implementing the semantic side of the feature, allowing earlier previews to be made available.  These syntaxes include, but are not limited to:

1. Using braces instead of brackets.  `{ k1: v1, k2: v2 }`.
2. Using brackets for keys: `[k1] = v1, [k2] = v2`
3. Using arrows for elements: `k1 => v1, k2 => v2`.

Choices here would have implications regarding potential syntactic ambiguities, collisions with potential future language features, and concerns around corresponding pattern forms.  However, all of those should not generally affect the semantics of the feature and can be considered at a later point dedicated to determining the most desirable syntax.

## Design Intuition

There are two aspects to the design of dictionary expressions. 

First is the concept of a `dictionary type`. *Dictionary types* are types that are similar to the existing `collection type` concept, with the additional requirements that they have an *element type* of some `KeyValuePair<TKey, TValue>>` *and* have an indexer `TValue this[TKey] { ... }`.  The latter requirement ensures that `List<KeyValuePair<int, string>>` is not considered a dictionary type (as its indexer is from `int` to `KeyValuePair<int, string>`).

Second is that collection expressions containing `KeyValuePair<,>`s (coming from `expression_element`s, `spread_element`s, or `key_value_pair_element`s) can now instantiate a normal `collection type` or a `dictionary type`.  

So, if the target type for a collection expression is some *collection type* (that is *not* a *dictionary type*) with an element of `KeyValuePair<,>` then it can be instantiated like so:

```c#
List<KeyValuePair<string, int>> nameToAge = ["mads": 21];
```

This is just a simple augmentation on top of the existing collection expression rules.  In the above example, the code will be emitted as:

```c#
__result.Add(KeyValuePair.Create("mads", 21));
```

However, if the target type for the collection expression *is* a *dictionary* type, then all `KeyValuePair<,>`s produced by `expression_element` or `spread_element` elements will be changed to use the indexer to assign into the resultant dictionary, and any `key_value_pair_element` will use that indexer directly as well.  For example:

```c#
Dictionary<string, int> nameToAge = ["mads": 21, existingDict.MaxPair(), .. otherDict];

// would be rewritten similar to:

Dictionary<string, int> __result = new();
__result["mads"] = 21;

var __t1 = existingDict.MaxPair();
__result[(string)__t1.Key] = (int)__t1.Value;

foreach (var __t2 in otherDict)
    __result[(string)__t2.Key] = (int)__t2.Value;
```

Many rules for *dictionary expressions* will correspond to existing rules for *collection expressions*, just requiring aspects such as *element* and *iteration types* to be some `KeyValuePair<,>`.

With a broad interpretation of these rules, all of the following would be legal:

```c#
Dictionary<string, int> nameToAge1 = ["mads": 21, existingKvp]; // as would
Dictionary<string, int> nameToAge2 = ["mads": 21, .. existingDict]; // as would
Dictionary<string, int> nameToAge3 = ["mads": 21, .. existingListOfKVPS];
```

A dictionary can also have a custom comparer provided through the use of a special `comparer: ...` element provided in the expression.  For example:

```c#
Dictionary<string, int> caseInsensitiveMap = [comparer: StringComparer.CaseInsensitive, .. existingMap];
```

The motivation for this is due to the high number of cases of dictionaries found in real world code with custom comparers.  Support for any further customization is not provided.  In line with the lack of support for customization for normal collection expressions (for example, for setting initial capacity).

### Q&A 1

Can a dictionary type value be created without using a key_value_pair_element?  For example are the following legal:

```c#
Dictionary<string, int> d1 = [existingKvp];
Dictionary<string, int> d2 = [.. otherDict];
```

Note: the element `KeyValuePair<K1,V1>` types need not be identical to the `KeyValuePair<K2,V2>` type of the destination dictionary type.  They simply must be convertible to the `V1 this[K1 key] { ... }` indexer provided by the dictionary.

Yes.  These are legal: [LDM-2024-03-11](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-11.md#conclusions)

### Q&A 2

Can you spread a *non dictionary type* when producing a dictionary type'd value.  For example:

```c#
Dictionary<string, int> nameToAge = ["mads": 21, .. existingListOfKVPS];
``` 

**Resolution:** *Spread elements* of key-value pair collections will be supported in dictionary expressions. [LDM-2024-03-11](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-11.md#conclusions)

### Q&A 3

How far do we want to take this KeyValuePair representation of things? Do we allow *key value pair elements* when producing normal collections? For example, should the following be allowed:

```c#
List<KeyValuePair<string, int>> = ["mads": 21];
```


**Resolution:** *Key value pair elements* will be supported in collection expressions for collection types that have a key-value pair element type. [LDM-2024-03-11](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-11.md#conclusions)

### Open question 4

Should we take a very restrictive view of `KeyValuePair<,>`?  Specifically, should we allow only that exact type?  Or should we allow any types with an implicit conversion to that type?  For example:

```c#
struct Pair<X, Y>
{
  public static implicit operator KeyValuePair<X, Y>(Pair<X, Y> pair) => ...;
}

Dictionary<int, string> map1 = [pair1, pair2]; // ?


List<Pair<int, string>> pairs = ...;
Dictionary<int, string> map2 = [.. pairs]; // ?
```

### Q&A 5

Dictionaries provide two ways of initializing their contents.  A restrictive `.Add`-oriented form that throws when a key is already present in the dictionary, used by collection initializers, and a permissive indexer-oriented form which does not, used by dictionary initializers.  The restrictive form is useful for catching mistakes ("oops, I didn't intend to add the same thing twice!"), but is limiting *especially* in the spread case.  For example:

```c#
Dictionary<string, Option> optionMap = [opt1Name: opt1Default, opt2Name: opt2Default, .. userProvidedOptions];
```

Or, conversely:

```c#
Dictionary<string, Option> optionMap = [.. Defaults.CoreOptions, feature1Name: feature1Override];
```

Which approach should we go with with our dictionary expressions? Options include:

1. Purely restrictive.  All elements use `.Add` to be added to the list.  Note: types like `ConcurrentDictionary` would then not work, not without adding support with something like the `CollectionBuilderAttribute`.
2. Purely permissive.  All elements are added using the indexer.  Perhaps with compiler warnings if the exact same key is given the same constant value twice.
3. Perhaps a hybrid model.  `.Add` if only using `k:v` and switching to indexers if using spread elements.  There is deep potential for confusion here.

**Resolution:** Use *indexer* as the lowering form. [LDM-2024-03-11](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-11.md#conclusions)

### Open question 6

From [Open Question 3](#open-question-3), we will support *dictionary elements* for C#12 collection expression target types. Which approach should we use for initialization for those types? Options include:

1. Use applicable instance indexer if available; otherwise use C#12 initialization.
2. Use applicable instance indexer if available; otherwise report an error during construction (or conversion?).
3. Use C#12 initialization always.

## Dictionary types

A type is considered a *dictionary type* if the following hold:
* The *element type* is `KeyValuePair<TKey, TValue>`.
* The *type* has an instance *indexer* where:
  * The indexer has a single parameter with an identity conversion from the parameter type to `TKey`.\*
  * There is an identity conversion from the indexer type to `TValue`.\*
  * The getter returns by value.
  * The getter is as accessible as the declaring type.

\* *Identity conversions are used rather than exact matches to allow type differences in the signature that are ignored by the runtime: `object` vs. `dynamic`; tuple element names; nullable reference types; etc.*

## Conversions

*Collection expression conversions* are updated to include conversions to *dictionary types*.

An implicit *collection expression conversion* exists from a collection expression to the following *dictionary types*:
* A *dictionary type* with an appropriate *[create method](#create-methods)*.
* A *struct* or *class* *dictionary type* that implements `System.Collections.IEnumerable` where:
  * The *element type* is determined from a `GetEnumerator` instance method or enumerable interface.
  * The *type* has an *[applicable](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11642-applicable-function-member)* constructor that can be invoked with no arguments, and the constructor is accessible at the location of the collection expression.
  * The *indexer* has a setter that is as accessible as the declaring type.
* An *interface type*:
  * `System.Collections.Generic.IDictionary<TKey, TValue>`
  * `System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>`

*Collection expression conversions* require implicit conversions for each element.
The element conversion rules are now differentiated based on whether the *element type* of the target type is `KeyValuePair<,>`.

If the *element type* is a type *other than* `KeyValuePair<,>`, the rules are *unchanged* from *language version 12* other than **clarifications**:

> An implicit *collection expression conversion* exists from a collection expression to a *type* with *element type* `T` **where `T` is not `KeyValuePair<,>` and** where for each *element* `Eᵢ` in the collection expression:
> * If `Eᵢ` is an *expression element*, there is an implicit conversion from `Eᵢ` to `T`.
> * If `Eᵢ` is a *spread element* `..Sᵢ`, there is an implicit conversion from the *iteration type* of `Sᵢ` to `T`.
> * **Otherwise there is *no implicit conversion* from the collection expression to the target type.**

If the *element type* is `KeyValuePair<,>`, the rules are *modified* for *language version 13* (this applies to any type with an *element type* of `KeyValuePair<,>`, not only *dictionary types*):

> An implicit *collection expression conversion* exists from a collection expression to a *type* with *element type* `KeyValuePair<K, V>` where for each *element* `Eᵢ` in the collection expression:
> * If `Eᵢ` is an *expression element*, then the type of `Eᵢ` is `KeyValuePair<Kᵢ:Vᵢ>` and there is an implicit conversion from `Kᵢ` to `K` and an implicit conversion from `Vᵢ` to `V`.
> * If `Eᵢ` is a *dictionary element* `Kᵢ:Vᵢ`, there is an implicit conversion from `Kᵢ` to `K` and an implicit conversion from `Vᵢ` to `V`.
> * If `Eᵢ` is a *spread element* `..Sᵢ`, then the *iteration type* of `Sᵢ` is `KeyValuePair<Kᵢ:Vᵢ>` and there is an implicit conversion from `Kᵢ` to `K` and an implicit conversion from `Vᵢ` to `V`.
> * Otherwise there is *no implicit conversion* from the collection expression to the target type.

The new rules above represent a breaking change: For types that are a valid conversion target in *language version 12* and have an *element type* of `KeyValuePair<,>`, the element conversion rules change between language versions 12 and 13.

## Create methods

> A create method is indicated with a [CollectionBuilder(...)] attribute on the collection type. The attribute specifies the builder type and method name of a method to be invoked to construct an instance of the collection type.
> ```diff
> + A create method will commonly use the name `CreateRange` in the dictionary domain.
> ```
> For the create method:
>   - The method must have a single parameter of type System.ReadOnlySpan<E>, passed by value, and there is an identity conversion from E to the iteration type of the collection type.
>    - ```diff
>      + Or, the method have a single parameter of
>      + `IEnumerable<KeyValuePair<TKey, TValue>>` and iteration type
>      + of the collection type is the same `KeyValuePair<,>` type. 
> ```

This would allow `ImmutableDictionary<TKey, TValue>` to be annotated with `[CollectionBuilder(typeof(ImmutableDictionary), "CreateRange")]` to light up support for creation.

### Open question 1

We could consider not adding this rule, and instead still require the create method to take a `ReadOnlySpan<E>`.  However, that would require the BCL to then add such a method to `ImmutableDictionary` as the existing `CreateRange` methods take an `IEnumerable`.

### Open question 2

It is common for dictionaries to take in a `comparer` value, to determine how keys are compared when adding, removing, and looking them up.  Use in the ecosystem is prevalent, and much discussion and feedback from the community is that being able to supply a comparer is important to them.  How could we accomplish this with the new dictionary-expression?  Options include:

1. Provide no support (or no support in C# 13), leaving such dictionaries as ones you could not use *dictionary expressions* for.

1. Provide a special syntactic form *solely* for the purpose of supplying that value.  For example: `[comparer: myComp, "mads": 21, .. ldmMembers]`.  Here `comparer` would be a contextual keyword.  Users wanting to use that as an actual key would say `@comparer: 1`

1. Provide a special syntactic form for the purpose of supplying data to constructors and create methods.  For example: `[new: (comparer: myComp, capacity: 50), "mads": 21, .. ldmMembers]`

## Construction

> The elements of a collection expression are evaluated in order, left to right. Each element is evaluated exactly once, and any further references to the elements refer to the results of this initial evaluation.
> 
> ```diff
> + A dictionary_element evaluates its interior expressions in order,
> + left to right.  In other words, the key is evaluated before the value. 
> ```
>
> For each element in order:
>
> - If the element is an expression element, the applicable Add instance or extension method is invoked with the element expression as the argument. (Unlike classic collection initializer behavior, element evaluation and Add calls are not necessarily interleaved.)
> 
> - ```diff
>   + If the target is a dictionary type, then the element must be a
>   + `KeyValuePair<,>`.  The applicable indexer is invoked with the
>   + `.Key` and `.Value` members of that pair.
>   ```
>
> - If the element is a spread element then one of the following is used:
>   - An applicable GetEnumerator instance or extension method is invoked on the spread element expression and for each item from the enumerator the applicable Add instance or extension method is invoked on the collection instance with the item as the argument. If the enumerator implements IDisposable, then Dispose will be called after enumeration, regardless of exceptions.
> 
>    - ```diff
>      + If the target is a dictionary-type, the enumerator's element
>      + type must be some `KeyValuePair<,>`, and for each of those
>      + elements the applicable indexer is invoked on the collection
>      + instance with the `.Key` and `.Value` members of that pair.
>      ```

## Type inference

```c#
var a = AsDictionary(["mads": 21, "dustin": 22]); // AsDictionary<string, int>(Dictionary<string, int> arg)

static Dictionary<TKey, TValue> AsDictionary<TKey, TValue>(Dictionary<TKey, TValue> arg) => arg;
```

Rules TBD.  Intuition though is to be inferring both a `TKey` and `TValue` type. `k:v` elements contribute input and output inferences respectively to those types.  Normal expression elements and spread elements must have associated `KeyValuePair<K_n, V_n>` types, where the `K_n` and `V_n` then contribute as well.

For example:

```c#
KeyValuePair<object, long> kvp = ...;
var a = AsDictionary(["mads": 21, "dustin": 22, kvp]); // AsDictionary<object, long>(Dictionary<object, long> arg)

static Dictionary<TKey, TValue> AsDictionary<TKey, TValue>(Dictionary<TKey, TValue> arg) => arg;
```

## Extension methods

No changes here.  Like with collection expressions, dictionary expressions do not have a natural type, so the existing conversions from type are not applicable. As a result, a dictionary expression cannot be used directly as the first parameter for an extension method invocation. 

## Overload resolution

No changes currently.  But open question if any *better conversion from expression* rules are needed.

Tentatively we think the answer is no.  The types that would appear in signatures would likely be:

```c#
void X(IDictionary<A, B> dict);
void X(Dictionary<A, B> dict);
```

In this case, standard betterness would pick the latter method.

Similarly for:

```c#
void X(IEnumerable<KeyValuePair<A, B>> dict);
void X(Dictionary<A, B> dict);
```

Similar to *collection expressions*, there is no betterness between disparate concrete dictionary types.  For example:

```c#
void X(Dictionary<A, B> dict);
void X(ImmutableDictionary<A, B> dict);

X([a, b]); // ambiguous
```

## Interface translation

### Mutable interface translation

Given the target type `IDictionary<TKey, TValue>`,  the type used will be `Dictionary<TKey, TValue>`.

### Non-mutable interface translation

Given a target type `IReadOnlyDictionary<TKey, TValue>`, a compliant implementation is only required to produce a value that implements that interface. A compliant implementation is free to:

1. Use an existing type that implements that interface.
1. Synthesize a type that implements the interface.

In either case, the type used is allowed to implement a larger set of interfaces than those strictly required.

Synthesized types are free to employ any strategy they want to implement the required interfaces properly.  The value generated is allowed to implement more interfaces than required. For example, implementing the mutable interfaces as well (specifically, implementing `IDictionary<TKey, TValue>` or the non-generic `IDictionary`). However, in that case:

1. The value must return true when queried for `.IsReadOnly`. This ensures consumers can appropriately tell that the collection is non-mutable, despite implementing the mutable views.
1. The value must throw on any call to a mutation method. This ensures safety, preventing a non-mutable collection from being accidentally mutated.

### Open question 1

There is a subtle concern around the following interface destinations:

```c#
// NOTE: These are not overloads.

void Xxx(IEnumerable<KeyValuePair<string, int>> pairs) ...
void Yyy(IDictionary<string, int> pairs) ...

Xxx(["mads": 21, .. ldm]);
Yyy(["mads": 21, .. ldm]);
```

When the destination is an IEnumerable, we tend to think we're producing a sequence (so "mads" could show up twice).  However, the use of the `k:v` syntax more strongly indicates production of a dictionary-value.

What should we do here when targeting `IEnumerable<...>` *and* using `k:v` elements? Produce an ordered sequence, with possibly duplicated values?  Or produce an unordered dictionary, with unique keys?

## Random open questions

### Question 1

If a user had a type that could be both initialized with a collection initializer *and* a dictionary initializer, what should happen?

For example:

```c#
HybridType h = [kvp1, kvp2];
```

In C# 12 this might have been calling `.Add(kvp)`.  Is it ok to call the indexer now?  If there is an applicable `.Add`, should we defer to this, and only fallback to the indexer? 

### Question 2

Parsing ambiguity around: `a ? [b] : c`

### Question 3

What are the rules when types have multiple indexers and multiple impls of `IEnumerable<KVP<,>>`

It would likely make sense to align with whatever comes out if you `foreach`ed the collection.
