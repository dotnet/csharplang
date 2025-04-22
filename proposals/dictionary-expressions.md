# Dictionary Expressions

Champion issue: <https://github.com/dotnet/csharplang/issues/8659>

## Summary

*Dictionary Expressions* are a continuation of the C# 12 *Collection Expressions* feature.  They extend that system with a new terse syntax, `["mads": 21, "dustin": 22]`, for creating common dictionary values.  Like with collection expressions, merging other dictionaries into these values is possible using the existing spread operator `..` like so: `[.. currentStudents, "mads": 21, "dustin": 22]`

Several dictionary-like types can be created without external BCL support.  These types are:

1. Concrete dictionary-like types, containing an read/write indexer `TValue this[TKey] { get; set; }`, like `Dictionary<TKey, TValue>` and `ConcurrentDictionary<TKey, TValue>`.
1. The well-known generic BCL dictionary interface types: `IDictionary<TKey, TValue>` and `IReadOnlyDictionary<TKey, TValue>`.

Further support is present for dictionary-like types not covered above through the `CollectionBuilderAttribute` and a similar API pattern to the corresponding *create method* pattern introduced for collection expressions.  Types like `ImmutableDictionary<TKey, TValue>` and `FrozenDictionary<TKey, TValue>` will be updated to support this pattern.

## Motivation

While dictionaries are similar to standard sequential collections in that they can be interpreted as a sequence of key/value pairs, they differ in that they are often used for their more fundamental capability of efficient looking up of values based on a provided key.  In an analysis of the BCL and the NuGet package ecosystem, sequential collection types and values make up the lion's share of collections used.  However, dictionary types were still used a significant amount, with appearances in APIs occurring at between 5% and 10% the amount of sequential collections, and with dictionary values appearing universally in all programs.

Currently, all C# programs must use many different and unfortunately verbose approaches to create instances of such values. Some approaches also have performance drawbacks. Here are some common examples:

1. Collection-initializer types, which require syntax like `new Dictionary<X, Y> { ... }` (lacking inference of possibly verbose TKey and TValue) prior to their values, and which can cause multiple reallocations of memory because they use `N` `.Add` invocations without supplying an initial capacity.
1. Immutable collections, which require syntax like `ImmutableDictionary.CreateRange(...)`, but which are also unpleasant due to the need to provide values as an `IEnumerable<KeyValuePair>`.  Builders are even more unwieldy.
1. Read-only dictionaries, which require first making a normal dictionary, then wrapping it.
1. Concurrent dictionaries, which lack an `.Add` method, and thus cannot easily be used with collection initializers.

Looking at the surrounding ecosystem, we also find examples everywhere of dictionary creation being more convenient and pleasant to use. Swift, TypeScript, Dart, Ruby, Python, and more, opt for a succinct syntax for this purpose, with widespread usage, and to great effect. Cursory investigations have revealed no substantive problems arising in those ecosystems with having these built-in syntax forms.

Unlike with *collection expressions*, C# does not have an existing pattern serving as the corresponding deconstruction form.  Designs here should be made with a consideration for being complementary with future deconstruction work. 

An inclusive solution is needed for C#. It should meet the vast majority of case for customers in terms of the dictionary-like types and values they already have. It should also feel pleasant in the language, complement the work done with collection expressions, and naturally extend to pattern matching in the future.

## Detailed Design

The following grammar productions are added:

```diff

collection_element
  : expression_element
  | spread_element
+ | key_value_pair_element
  ;

+ key_value_pair_element
+  : expression ':' expression
+  ;
```

Alternative syntaxes are available for consideration, but should be considered later due to the bike-shedding cost involved.  Picking the above syntax allows the compiler team to move quickly at implementing the semantic side of the feature, allowing earlier previews to be made available.  These syntaxes include, but are not limited to:

1. Using braces instead of brackets.  `{ k1: v1, k2: v2 }`.
2. Using brackets for keys: `[k1] = v1, [k2] = v2`
3. Using arrows for elements: `k1 => v1, k2 => v2`.

Choices here would have implications regarding potential syntactic ambiguities, collisions with potential future language features, and concerns around corresponding pattern forms.  However, all of those should not generally affect the semantics of the feature and can be considered at a later point dedicated to determining the most desirable syntax.

## Design Intuition

There are three core aspects to the design of dictionary expressions. 

1. Collection expressions containing `KeyValuePair<,>` (coming from `expression_element`, `spread_element`, or `key_value_pair_element` elements) can now instantiate a normal *collection type* *or* a *dictionary type*.

    So, if the target type for a collection expression is some *collection type* (that is *not* a *dictionary type*) with an element of `KeyValuePair<,>` then it can be instantiated like so:

    ```c#
    List<KeyValuePair<string, int>> nameToAge = ["mads": 21];
    ```

    This is just a simple augmentation on top of the existing collection expression rules.  In the above example, the code will be emitted as:

    ```c#
    __result.Add(new KeyValuePair<string, int>("mads", 21));
    ```

2. Introduction of the *dictionary type*. *Dictionary types* are types that are similar to the existing *collection types*, with the additional requirements that they have an *element type* of some `KeyValuePair<TKey, TValue>` *and* have an indexer `TValue this[TKey] { ... }`. The former requirement ensures that `List<T>` is not considered a dictionary type, as its element type is `T` not `KeyValuePair<,>`. The latter requirement ensures that `List<KeyValuePair<int, string>>` is also not considered a dictionary type, with its `int`-to-`KeyValuePair<int, string>` indexer (instead of an `int`-to-`string` indexer). `Dictionary<TKey, TValue>` passes both requirements.

    As such, if the target type for the collection expression *is* a *dictionary* type, then all `KeyValuePair<,>` produced by `expression_element` or `spread_element` elements will be changed to use the indexer\* to assign into the resultant dictionary. Any `key_value_pair_element` will use that indexer\* directly as well.  For example:

    ```c#
    Dictionary<string, int> nameToAge = ["mads": 21, existingDict.MaxPair(), .. otherDict];

    // would be rewritten similar to:

    Dictionary<string, int> __result = new();
    __result["mads"] = 21;

    // Note: the below casts must be legal for the dictionary
    // expression to be legal
    var __t1 = existingDict.MaxPair();
    __result[(string)__t1.Key] = (int)__t1.Value;

    foreach (var __t2 in otherDict)
        __result[(string)__t2.Key] = (int)__t2.Value;
    ```

    \* The above holds for types with an available `set` accessor in their indexer. Similar semantics are provided for dictionary types without a writable indexer (like immutable dictionary types, or `IReadOnlyDictionary<,>`), and are explained later in the spec.

3. Alignment of the rules for assigning to *dictionary types* with the rules for assigning to *collection types*, just requiring aspects such as *element* and *iteration types* to be some `KeyValuePair<,>`.  *However*, with the rules extended such that the `KeyValuePair<,>` type itself is relatively transparent, and instead the rule is updated to work on the underlying `TKey` and `TValue` types.

    This view allows for very natural processing of what would otherwise be thought of as disparate `KeyValuePair<,>` types.  For example:

    ```c#
    Dictionary<string, int> d1 = ...;

    // Assignment possible, even though KeyValuePair<string, int>` is not itself assignable to KeyValuePair<object, long>
    Dictionary<object, long> d2 = [.. d1]; 
    ```

Note: Many rules in this spec will refer to types needing to be the same `KeyValuePair<,>` type.  This is an informal way of saying the types must have an identity conversion between them.  As such, `KeyValuePair<(int X, int Y), object>` would be considered the same type a `KeyValuePair<(int, int), object?>` for the purpose of these rules.

With a broad interpretation of these rules, all of the following would be legal:

```c#
// Assigning to dictionary types:
Dictionary<string, int> nameToAge1 = ["mads": 21, existingKvp];     // as would
Dictionary<string, int> nameToAge2 = ["mads": 21, .. existingDict]; // as would
Dictionary<string, int> nameToAge3 = ["mads": 21, .. existingListOfKVPS];

// Assigning to collection types:
List<string, int> nameToAge1 = ["mads": 21, existingKvp];     // as would
List<string, int> nameToAge2 = ["mads": 21, .. existingDict]; // as would
List<string, int> nameToAge3 = ["mads": 21, .. existingListOfKVPS];
```

## Comparer support

A dictionary expression can also provide a custom *comparer* to control its behavior just by including such a value as the first `expression_element` in the expression. For example:

```c#
Dictionary<string, int> caseInsensitiveMap = [StringComparer.CaseInsensitive, .. existingMap];

// Or even:
Dictionary<string, int> caseInsensitiveMap = [StringComparer.CaseInsensitive];
```

While this approach does reuse `expression_element` both for specifying individual `KeyValuePair<,>` as well as a comparer for the dictionary, there is no ambiguity here as no type could satisfy both types.  

The motivation for this is due to the high number of cases of dictionaries found in real world code with custom comparers.  Support for any further customization is not provided.  This is in line with the lack of support for customization for normal collection expressions (like setting initial capacity). Other designs were explored which attempted to generalize this concept (for example, passing arbitrary arguments along).  These designs never landed on a satisfactory syntax.  And the concept of passing an arbitrary argument along doesn't supply a satisfactory answer on how that would control instantiating an `IDictionary<,>` or `IReadOnlyDictionary<,>`.

### Question: Comparers for *collection types*

Should support for the key comparer be available for normal *collection types*, not just *dictionary types*.  This would be useful for set-like types like `HashSet<>`.  For example:

```c#
HashSet<string> values = [StringComparer.CaseInsensitive, .. names];
```

### Question: Specialized comparer syntax.

Should there be more distinctive syntax for the comparer?  Simply starting with a comparer could be difficult to tease out.  Having a syntax like so could make things clearer:

```c#
// `comparer: ...` to indicate the purpose of this value
Dictionary<string, int> caseInsensitiveMap = [comparer: StringComparer.CaseInsensitive, .. existingMap];

// Semicolon to more clearly delineate the comparer
Dictionary<string, int> caseInsensitiveMap = [StringComparer.CaseInsensitive; .. existingMap];

// Both?
Dictionary<string, int> caseInsensitiveMap = [comparer : StringComparer.CaseInsensitive; .. existingMap];
```

### Question: Types of comparers supported.

`IEqualityComparer<T>` is not the only comparer type used in collections.  `SortedDictionary<,>` and `SortedSet<,>` both use an `IComparer<T>` instead (as they have ordering, not hashing semantics).  It seems unfortunate to leave out `SortedDictionary<,>` if we are supporting the rest.  As such, perhaps the rules should just be that the special value in the collection be typed as some `IComparer<T>` or some `IEqualityComparer<T>`.  

## Conversions

[*Collection expression conversions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#conversions) are **updated** to include conversions to *dictionary types*.

An implicit *collection expression conversion* exists from a collection expression to the following types:
* A single dimensional *array type* `T[]`, in which case the *element type* is `T`
* A *span type*:
  * `System.Span<T>`
  * `System.ReadOnlySpan<T>`  
  In which case the *element type* is `T`
* A *type* with an appropriate *[create method](#create-methods)*, in which case the *element type* is the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) determined from a `GetEnumerator` instance method or enumerable interface, not from an extension method
* A *struct* or *class type* that implements `System.Collections.IEnumerable` where:
  * The *type* has an *[applicable](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11642-applicable-function-member)* constructor that can be invoked with no arguments, and the constructor is accessible at the location of the collection expression.
  * **One of the following holds:**
    * **The [*iteration type*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md#1395-the-foreach-statement) of the *type* is `KeyValuePair<K, V>`, and the *type* has an instance *indexer*, with `get` and `set` accessors where:**
      * **The indexer has a single parameter passed by value or with `in`.**
      * **There is an identity conversion from the parameter type to `K` and an identity conversion from the indexer type to `V`.** *Identity conversions rather than exact matches allow type differences that are ignored by the runtime: `object` vs. `dynamic`; tuple element names; nullable reference types; etc.*
      * **The `get` accessor returns by value.**
      * **The `get` and `set` accessors are declared `public`.**
      * **The indexer is not [*hidden*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/basic-concepts.md#7723-hiding-through-inheritance).**
    * If the collection expression has any elements, the *type* has an instance or extension method `Add` where:
      * The method can be invoked with a single value argument.
      * If the method is generic, the type arguments can be inferred from the collection and argument.
      * The method is accessible at the location of the collection expression.

    In which case the *element type* is the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md#1395-the-foreach-statement) of the *type*.
* An *interface type*:
  * `System.Collections.Generic.IEnumerable<T>`
  * `System.Collections.Generic.IReadOnlyCollection<T>`
  * `System.Collections.Generic.IReadOnlyList<T>`
  * `System.Collections.Generic.ICollection<T>`
  * `System.Collections.Generic.IList<T>`  
    In which case the *element type* is `T`
* **An *interface type*:**
  * **`System.Collections.Generic.IDictionary<TKey, TValue>`**
  * **`System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>`**  
  **In which case the *element type* is `KeyValuePair<TKey, TValue>`**

*Collection expression conversions* require implicit conversions for each element.
The element conversion rules are **updated** as follows.

The implicit conversion exists if the type has an *element type* `T` where for each *element* `Eᵢ` in the collection expression:
* If `Eᵢ` is an *expression element* then:
  * There is an implicit conversion from `Eᵢ` to `T`, **or**
  * **There is no implicit conversion from `Eᵢ` to `T`, and `T` is a type `KeyValuePair<K, V>`, and `Eᵢ` has a type `KeyValuePair<Kᵢ, Vᵢ>`, and there is an implicit conversion from `Kᵢ` to `K` and an implicit conversion from `Vᵢ` to `V`.**
* If `Eᵢ` is a *spread element* `..Sᵢ` then:
  * There is an implicit conversion from the *iteration type* of `Sᵢ` to `T`, **or**
  * **There is no implicit conversion from the *iteration type* of `Sᵢ` to `T`, and `T` is a type `KeyValuePair<K, V>`, and `Sᵢ` has an *iteration type* `KeyValuePair<Kᵢ, Vᵢ>`, and there is an implicit conversion from `Kᵢ` to `K` and an implicit conversion from `Vᵢ` to `V`.**
* **If `Eᵢ` is a *key-value pair element* `Kᵢ:Vᵢ`, then `T` is a type `KeyValuePair<K, V>`, and there is an implicit conversion from `Kᵢ` to `K` and an implicit conversion from `Vᵢ` to `V`.**

> Allowing implicit key and value conversions is useful for *expression elements* and *spread elements* where the key or value types do not match the collection element type exactly.
> 
> ```csharp
> Dictionary<int, string>  x = ...;
> Dictionary<long, object> y = [..x]; // key-value pair conversion from KVP<int, string> to KVP<long, object>
> ```

Collection arguments are *not* considered when determining *collection expression* conversions.

## Create methods

> A *create method* is indicated with a `[CollectionBuilder(...)]` attribute on the *collection type*.
> The attribute specifies the *builder type* and *method name* of a method to be invoked to construct an instance of the collection type.

> **A create method need not be called `Create`.  Instead, it may commonly use the name `CreateRange` in the dictionary domain.**
>
> For the create method:
>   - The method must have a single parameter of type System.ReadOnlySpan<E>, passed by value, and there is an identity conversion from E to the *iteration type* of the collection type. 
>
>    - **The method has two parameters, where the first is a [*comparer*](#Comparer-support) and the other follows the rules of the *single parameter* rule above. This method will be called if the collection expression's first element is an [*comparer*](#Comparer-support) that is convertible to that parameter type.**

*Dictionary type* authors who use `CollectionBuilderAttribute` should have the method that is pointed to have `overwrite` not `throw` semantics when encountering the same `.Key` multiple times in the span of `KeyValuePair<,>` they are processing.

The runtime has committed to supplying these new CollectionBuilder methods that take `ReadOnlySpan<>` for their immutable collections.

## Construction

[*Collection construction*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#construction) is **updated** as follows.

The elements of a collection expression are evaluated in order, left to right. Each element is evaluated exactly once, and any further references to the elements refer to the results of this initial evaluation.  
...

If the target type is a *struct* or *class type* that implements `System.Collections.IEnumerable`, and the target type does not have a *[create method](#create-methods)*, the construction of the collection instance is as follows:

* The elements are evaluated in order. Some or all elements may be evaluated *during* the steps below rather than before.

* The compiler *may* determine the *known length* of the collection expression by invoking [*countable*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md#adding-index-and-range-support-to-existing-library-types) properties &mdash; or equivalent properties from well-known interfaces or types &mdash; on each *spread element expression*.

* The constructor that is applicable with no arguments is invoked.

* **If the *iteration type* is a type `KeyValuePair<K, V>` and the [*collection expression conversion*](#conversions) involves an instance *indexer*, then:**
  * **For each element in order:**
    * **If the element is a *key value pair element* `Kᵢ:Vᵢ` then:**
      * **First `Kᵢ` is evaluated, then `Vᵢ` is evaluated.**
      * **The indexer is invoked on the collection instance with the converted values of `Kᵢ` and `Vᵢ`.**
    * **If the element is an *expression element* `Eᵢ`, then:**
      * **If `Eᵢ` is implicitly convertible to `KeyValuePair<K, V>`, then:**
        * **`Eᵢ` is evaluated and converted to a `KeyValuePair<K, V>`.**
        * **The indexer is invoked on the collection instance with `Key` and `Value` of the converted value.**
      * **Otherwise, `Eᵢ` has a type `KeyValuePair<Kᵢ, Vᵢ>`, in which case:**
        * **`Eᵢ` is evaluated.**
        * **The indexer is invoked on the collection instance with `Key` and `Value` of the value, converted to `K` and `V`.**
    * **If the element is a *spread element* where the spread element *expression* has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tᵢ` then:**
      * **An applicable `GetEnumerator` instance or extension method is invoked on the spread element *expression***.
      * **For each item from the enumerator:**
        * **If `Tᵢ` is implicitly convertible to `KeyValuePair<K, V>` then:**
          * **The item is converted to a `KeyValuePair<K, V>`.**
          * **The indexer is invoked on the collection instance with `Key` and `Value` of the converted item.**
        * **Otherwise, `Tᵢ` is a type `KeyValuePair<Kᵢ, Vᵢ>`, in which case:**
          * **The indexer is invoked on the collection instance with `Key` and `Value` of the item, converted to `K` and `V`.**
      * **If the enumerator implements `IDisposable`, then `Dispose` will be called after enumeration, regardless of exceptions.**

* **If the *iteration type* is a type `KeyValuePair<K, V>` and the [*collection expression conversion*](#conversions) involves an applicable `Add` method, then:**
  * **For each element in order:**
    * **If the element is a *key value pair element* `Kᵢ:Vᵢ` then:**
      * **First `Kᵢ` is evaluated, then `Vᵢ` is evaluated.**
      * **A `KeyValuePair<K, V>` instance is constructed from the values of `Kᵢ` and `Vᵢ` converted to `K` and `V`.**
      * **The applicable `Add` instance or extension method is invoked with the `KeyValuePair<K, V>` instance as the argument.**
    * **If the element is an *expression element* `Eᵢ`, then:**
      * **If `Eᵢ` is implicitly convertible to `KeyValuePair<K, V>`, then the applicable `Add` instance or extension method is invoked with `Eᵢ` as the argument.**
      * **Otherwise, `Eᵢ` has a type `KeyValuePair<Kᵢ, Vᵢ>`, in which case:**
        * **`Eᵢ` is evaluated.**
        * **A `KeyValuePair<K, V>` instance is constructed from the `Key` and `Value` of the value, converted to `K` and `V`.**
        * **The applicable `Add` instance or extension method is invoked with the `KeyValuePair<K, V>` instance as the argument.**
    * **If the element is a *spread element* where the spread element *expression* has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tᵢ` then:**
      * **An applicable `GetEnumerator` instance or extension method is invoked on the spread element *expression***.
      * **For each item from the enumerator:**
        * **If `Tᵢ` is implicitly convertible to `KeyValuePair<K, V>` then the applicable `Add` instance or extension method is invoked with item as the argument.**
        * **Otherwise, `Tᵢ` is a type `KeyValuePair<Kᵢ, Vᵢ>`, in which case:**
          * **A `KeyValuePair<K, V>` instance is constructed from the `Key` and `Value` of the item, converted to `K` and `V`.**
          * **The applicable `Add` instance or extension method is invoked with the `KeyValuePair<K, V>` instance as the argument.**
      * **If the enumerator implements `IDisposable`, then `Dispose` will be called after enumeration, regardless of exceptions.**

* Otherwise, the *iteration type* is *not* a `KeyValuePair<K, V>` type, in which case:
  * For each element in order:
    * If the element is an *expression element*, the applicable `Add` instance or extension method is invoked with the element *expression* as the argument.
    * If the element is a *spread element* then ...:
      * An applicable `GetEnumerator` instance or extension method is invoked on the *spread element expression*.
      * For each item from the enumerator:
        * The applicable `Add` instance or extension method is invoked on the *collection instance* with the item as the argument.
      * If the enumerator implements `IDisposable`, then `Dispose` will be called after enumeration, regardless of exceptions.
      * ...

If the target type is an *array*, a *span*, a type with a *[create method](#create-methods)*, or an *interface*, the construction of the collection instance is as follows:

* The elements are evaluated in order. Some or all elements may be evaluated *during* the steps below rather than before.

* The compiler *may* determine the *known length* of the collection expression by invoking [*countable*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md#adding-index-and-range-support-to-existing-library-types) properties &mdash; or equivalent properties from well-known interfaces or types &mdash; on each *spread element expression*.

* An *initialization instance* is created as follows:
  * If the target type is an *array* and the collection expression has a *known length*, an array is allocated with the expected length.
  * If the target type is a *span* or a type with a *create method*, and the collection has a *known length*, a span with the expected length is created referring to contiguous storage.
  * Otherwise intermediate storage is allocated. The intermediate storage has an indexer for element assignment.

* **If the *iteration type* is a type `KeyValuePair<K, V>`, then:**
  * **For each element in order:**
    * **If the element is a *key value pair element* `Kᵢ:Vᵢ` then:**
      * **First `Kᵢ` is evaluated, then `Vᵢ` is evaluated.**
      * **The initialization instance *indexer* is invoked on the collection instance with the converted values of `Kᵢ` and `Vᵢ`.**
    * **If the element is an *expression element* `Eᵢ`, then:**
      * **If `Eᵢ` is implicitly convertible to `KeyValuePair<K, V>`, then:**
        * **`Eᵢ` is evaluated and converted to a `KeyValuePair<K, V>`.**
        * **The initialization instance *indexer* is invoked on the collection instance with `Key` and `Value` of the converted value.**
      * **Otherwise, `Eᵢ` has a type `KeyValuePair<Kᵢ, Vᵢ>`, in which case:**
        * **`Eᵢ` is evaluated.**
        * **The initialization instance *indexer* is invoked on the collection instance with `Key` and `Value` of the value, converted to `K` and `V`.**
    * **If the element is a *spread element* where the spread element *expression* has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tᵢ` then:**
      * **An applicable `GetEnumerator` instance or extension method is invoked on the spread element *expression***.
      * **For each item from the enumerator:**
        * **If `Tᵢ` is implicitly convertible to `KeyValuePair<K, V>` then:**
          * **The item is converted to a `KeyValuePair<K, V>`.**
          * **The initialization instance *indexer* is invoked on the collection instance with `Key` and `Value` of the converted item.**
        * **Otherwise, `Tᵢ` is a type `KeyValuePair<Kᵢ, Vᵢ>`, in which case:**
          * **The initialization instance *indexer* is invoked on the collection instance with `Key` and `Value` of the item, converted to `K` and `V`.**
      * **If the enumerator implements `IDisposable`, then `Dispose` will be called after enumeration, regardless of exceptions.**

* Otherwise, the *iteration type* is *not* a `KeyValuePair<K, V>` type, in which case:
  * For each element in order:
    * If the element is an *expression element*, the initialization instance *indexer* is invoked to assign the evaluated expression at the current index.
    * If the element is a *spread element* then ...:
      * An applicable `GetEnumerator` instance or extension method is invoked on the *spread element expression*.
      * For each item from the enumerator:
        * The initialization instance *indexer* is invoked to assign the item at the current index.
        * If the enumerator implements `IDisposable`, then `Dispose` will be called after enumeration, regardless of exceptions.

* If intermediate storage was allocated for the collection, a collection instance is allocated with the actual collection length and the values from the initialization instance are copied to the collection instance, or if a span is required the compiler *may* use a span of the actual collection length from the intermediate storage. Otherwise the initialization instance is the collection instance.

* If the target type has a *create method*, the create method is invoked with the span instance.

## Type inference

`k:v` elements contribute input and output inferences respectively to those types.  Normal expression elements and spread elements must have associated `KeyValuePair<K_n, V_n>` types, where the `K_n` and `V_n` then contribute as well.

For example:

```c#
KeyValuePair<object, long> kvp = ...;
var a = AsDictionary(["mads": 21, "dustin": 22, kvp]); // AsDictionary<object, long>(Dictionary<object, long> arg)

static Dictionary<TKey, TValue> AsDictionary<TKey, TValue>(Dictionary<TKey, TValue> arg) => arg;
```

The [*type inference*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference) rules are updated as follows.

> 11.6.3.2 The first phase
>
> For each of the method arguments `Eᵢ`:
>
> * An *input type inference* is made *from* `Eᵢ` *to* the corresponding *parameter type* `Tᵢ`.
>
> An *input type inference* is made *from* an expression `E` *to* a type `T` in the following way:
>
> * If `E` is a *collection expression* with elements `Eᵢ`:
>   * **If `T` has an *element type* `KeyValuePair<Kₑ, Vₑ>`, or `T` is a *nullable value type* `T0?` and `T0` has an *element type* `KeyValuePair<Kₑ, Vₑ>`, then for each `Eᵢ`**:
>     * **If `Eᵢ` is a *key value pair element* `Kᵢ:Vᵢ`, then an *input type inference* is made *from* `Kᵢ` *to* `Kₑ` and an *input type inference* is made *from* `Vᵢ` *to* `Vₑ`.**
>     * **If `Eᵢ` is an *expression element* with type `KeyValuePair<Kᵢ, Vᵢ>`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Kᵢ` *to* `Kₑ` and a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Vᵢ` *to* `Vₑ`.**
>     * **If `Eᵢ` is a *spread element* with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `KeyValuePair<Kᵢ, Vᵢ>`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Kᵢ` *to* `Kₑ` and a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Vᵢ` *to* `Vₑ`.**
>   * If `T` has an *element type* `Tₑ`, or `T` is a *nullable value type* `T0?` and `T0` has an *element type* `Tₑ`, then for each `Eᵢ`:
>     * If `Eᵢ` is an *expression element*, then an *input type inference* is made *from* `Eᵢ` *to* `Tₑ`.
>     * If `Eᵢ` is a *spread element* with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Sᵢ`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Sᵢ` *to* `Tₑ`.
> * *[existing rules from first phase]* ...

> 11.6.3.7 Output type inferences
>
> An *output type inference* is made *from* an expression `E` *to* a type `T` in the following way:
>
> * If `E` is a *collection expression* with elements `Eᵢ`:
>   * **If `T` has an *element type* `KeyValuePair<Kₑ, Vₑ>`, or `T` is a *nullable value type* `T0?` and `T0` has an *element type* `KeyValuePair<Kₑ, Vₑ>`, then for each `Eᵢ`**:
>     * **If `Eᵢ` is a *key value pair element* `Kᵢ:Vᵢ`, then an *output type inference* is made *from* `Kᵢ` *to* `Kₑ` and an *output type inference* is made *from* `Vᵢ` *to* `Vₑ`.**
>     * **If `Eᵢ` is an *expression element*, no inference is made from `Eᵢ`.**
>     * **If `Eᵢ` is a *spread element*, no inference is made from `Eᵢ`.**
>   * If `T` has an *element type* `Tₑ` or `T` is a *nullable value type* `T0?` and `T0` has an *element type* `Tₑ`, then for each `Eᵢ`:
>     * If `Eᵢ` is an *expression element*, then an *output type inference* is made *from* `Eᵢ` *to* `Tₑ`.
>     * If `Eᵢ` is a *spread element*, no inference is made from `Eᵢ`.
> * *[existing rules from output type inferences]* ...

The *input type inference* change is necessary to infer `T` in `InputType<T>()` in the following; the *output type inference* change is necessary to infer `T` in `OutputType<T>()`.

```csharp
static void InputType<T>(Dictionary<string, T> d);
static void OutputType<T>(Dictionary<string, Func<T>> d);

InputType(["a":1]);
OutputType(["b":() => 2)]);
```

## Extension methods

No changes here.  Like with collection expressions, dictionary expressions do not have a natural type, so the existing conversions from type are not applicable. As a result, a dictionary expression cannot be used directly as the first parameter for an extension method invocation. 

## Overload resolution

For example, given:

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

[*Better collection conversion from expression*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/collection-expressions-better-conversion.md) is updated as follows.

> If there is an identity conversion from `E₁` to `E₂`, then the element conversions are as good as each other. Otherwise, the element conversions to `E₁` are ***better than the element conversions*** to `E₂` if:
> - For every `ELᵢ`, `CE₁ᵢ` is at least as good as `CE₂ᵢ`, and
> - There is at least one i where `CE₁ᵢ` is better than `CE₂ᵢ`
> Otherwise, neither set of element conversions is better than the other, and they are also not as good as each other.
>
> Conversion comparisons are made as follows:
> - **If the target is a type with an *element type* `KeyValuePair<Kₑ, Vₑ>`:**
>   - **If `ELᵢ` is a *key value pair element* `Kᵢ:Vᵢ`, conversion comparison uses better conversion from expression from `Kᵢ` to `Kₑ` and better conversion from expression from `Vᵢ` to `Vₑ`.**
>   - **If `ELᵢ` is an *expression element* with *element type* `KeyValuePair<Kᵢ, Vᵢ>`, conversion comparison uses better conversion from type `Kᵢ` to `Kₑ` and better conversion from type `Vᵢ` to `Vₑ`.**
>   - **If `ELᵢ` is an *spread element* with an expression with *element type* `KeyValuePair<Kᵢ, Vᵢ>`, conversion comparison uses better conversion from type `Kᵢ` to `Kₑ` and better conversion from type `Vᵢ` to `Vₑ`.**
> - **If the target is a type with an *element type* other than `KeyValuePair<,>`:**
>   - **If `ELᵢ` is a *key value pair element*, there is no conversion to the *element type*.**
>   - If `ELᵢ` is an *expression element*, conversion comparison uses better conversion from expression.
>   - If `ELᵢ` is a *spread element*, conversion conversion uses better conversion from the spread collection *element type*.
>
> `C₁` is a ***better collection conversion from expression*** than `C₂` if:
> - Both `T₁` and `T₂` are not *span types*, and `T₁` is implicitly convertible to `T₂`, and `T₂` is not implicitly convertible to `T₁`, or
> - **Both or neither of `T₁` and `T₂` have *element type* `KeyValuePair<,>`, and** `E₁` does not have an identity conversion to `E₂`, and both  and the element conversions to `E₁` are ***better than the element conversions*** to `E₂`, or
> - `E₁` has an identity conversion to `E₂`, and one of the following holds:
>    - `T₁` is `System.ReadOnlySpan<E₁>`, and `T₂` is `System.Span<E₂>`, or
>    - `T₁` is `System.ReadOnlySpan<E₁>` or `System.Span<E₁>`, and `T₂` is an *array_or_array_interface* with *element type* `E₂`
>
> Otherwise, neither collection type is better, and the result is ambiguous.

## Interface translation

### Non-mutable interface translation

Given a target type `IReadOnlyDictionary<TKey, TValue>`, a compliant implementation is required to produce a value that implements that interface. If a type is synthesized, it is recommended the synthesized type implements `IDictionary<TKey, TValue>` as well. This ensures maximal compatibility with existing libraries, including those that introspect the interfaces implemented by a value in order to light up performance optimizations.

In addition, the value must implement the nongeneric `IDictionary` interface. This enables collection expressions to support dynamic introspection in scenarios such as data binding.

A compliant implementation is free to:

1. Use an existing type that implements the required interfaces.
2. Synthesize a type that implements the required interfaces.

In either case, the type used is allowed to implement a larger set of interfaces than those strictly required.

Synthesized types are free to employ any strategy they want to implement the required interfaces properly. For example, returning a cached singleton for empty collections, or a synthesized type which inlines the keys/values directly within itself, avoiding the need for additional internal collection allocations.

1. The value must return `true` when queried for `ICollection<T>.IsReadOnly`. This ensures consumers can appropriately tell that the collection is non-mutable, despite implementing the mutable views.
1. The value must throw on any call to a mutation method (like `IDictionary<TKey, TValue>.Add`). This ensures safety, preventing a non-mutable collection from being accidentally mutated.

This follows the originating intuition around the `IEnumerable<T> / IReadOnlyCollection<T> / IReadOnlyList<T>` interfaces and the allowed flexibility the compiler has in using an existing type or synthesized type when creating an instance of those in [*collection expressions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#non-mutable-interface-translation). 


### Mutable interface translation

Given the target type `IDictionary<TKey, TValue>`:

1. The value must be an instance of `Dictionary<TKey, TValue>`

Translation mechanics will happen using the already defined rules that encompass the `Dictionary<TKey, TValue>` type (including handling of an initially provided [*comparer*](#Comparer-support)).

This follows the originating intuition around the `IList<T> / ICollection<T>` interfaces and the concrete `List<T>` destination type in [*collection expressions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#mutable-interface-translation). 


## Answered Questions

### Answered question 1

Can a dictionary type value be created without using a key_value_pair_element?  For example, are the following legal?

```c#
Dictionary<string, int> d1 = [existingKvp];
Dictionary<string, int> d2 = [.. otherDict];
```

Note: the element `KeyValuePair<K1,V1>` types need not be identical to the `KeyValuePair<K2,V2>` type of the destination dictionary type.  They simply must be convertible to the `V1 this[K1 key] { ... }` indexer provided by the dictionary.

Yes.  These are legal: [LDM-2024-03-11](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-11.md#conclusions)

### Answered question 2

Can you spread a *non dictionary type* when producing a dictionary type'd value.  For example:

```c#
Dictionary<string, int> nameToAge = ["mads": 21, .. existingListOfKVPS];
``` 

**Resolution:** *Spread elements* of key-value pair collections will be supported in dictionary expressions. [LDM-2024-03-11](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-11.md#conclusions)

### Answered question 3

How far do we want to take this KeyValuePair representation of things? Do we allow *key value pair elements* when producing normal collections? For example, should the following be allowed:

```c#
List<KeyValuePair<string, int>> = ["mads": 21];
```

**Resolution:** *Key value pair elements* will be supported in collection expressions for collection types that have a key-value pair element type. [LDM-2024-03-11](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-11.md#conclusions)

### Answered question 4

Dictionaries provide two ways of initializing their contents.  A restrictive `.Add`-oriented form that throws when a key is already present in the dictionary, and a permissive indexer-oriented form which does not.  The restrictive form is useful for catching mistakes ("oops, I didn't intend to add the same thing twice!"), but is limiting *especially* in the spread case.  For example:

```c#
Dictionary<string, Option> optionMap = [opt1Name: opt1Default, opt2Name: opt2Default, .. userProvidedOptions];
```

Or, conversely:

```c#
Dictionary<string, Option> optionMap = [.. Defaults.CoreOptions, feature1Name: feature1Override];
```

Which approach should we go with for dictionary expressions? Options include:

1. Purely restrictive.  All elements use `.Add` to be added to the list.  Note: types like `ConcurrentDictionary` would then not work, not without adding support with something like the `CollectionBuilderAttribute`.
2. Purely permissive.  All elements are added using the indexer.  Perhaps with compiler warnings if the exact same key is given the same constant value twice.
3. Perhaps a hybrid model.  `.Add` if only using `k:v` and switching to indexers if using spread elements.  There is deep potential for confusion here.

**Resolution:** Use *indexer* as the lowering form. [LDM-2024-03-11](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-03-11.md#conclusions)

### Answered question 5

What types and translation should be used when targeting dictionary interfaces (`IDictionary<TKey, TValue>` or `IReadOnlyDictionary<TKey, TValue>`)?

**Resolution:** Use the same rules used for mutable and non-mutable interfaces for normal
[*collection expressions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#interface-translation)
analogously translated to dictionaries.  Full details can be found in [interface-translation](#interface-translation).  

[LDM-2025-04-09](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-09.md#conclusion)

### Conversion from expression element for `KeyValuePair<K, V>` collections

Confirm the **allowed conversions** from an *expression element* when the target type is a `KeyValuePair<K, V>` collection.

```csharp
List<KeyValuePair<string, int>> list;
list = [default];             // ok
list = [new()];               // ok
list = [new StringIntPair()]; // error: UDC not supported
```

>  * If `Eᵢ` is an *expression element* then one of the following holds:
>     * **There is an implicit conversion from `Eᵢ` to `KeyValuePair<K:V>` where the conversion is one of:**
>       * ***default literal conversion***
>       * ***target-typed new conversion***
>     * **`Eᵢ` has type `KeyValuePair<Kᵢ:Vᵢ>` and there is an implicit conversion from `Kᵢ` to `K` and an implicit conversion from `Vᵢ` to `V`.**

**Resolution:** Existing conversions should continue to apply for *expression elements* and *spread elements* before considering co-variant conversions of `Key` and `Value` for distinct `KeyValuePair<,>` types. [LDM-2025-03-17](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-17.md#conclusion-2)

### Binding to indexer

For concrete dictionary types that do not use `CollectionBuilderAttribute`, where the compiler constructs the resulting instance using a constructor and repeated calls to an indexer, how should the compiler resolve the appropriate indexer for each element?

```csharp
MyDictionary<string, int> d =
  [
    (object)"one":1, // this[object] { set; }
    "two":2          // this[string] { set; }
  ];

class MyDictionary<K, V> : IEnumerable<KeyValuePair<object, object>>
{
  // ...
  public V this[K k] { ... }
  public object this[object o] { ... }
}
```

Options include:
1. For each element individually, use normal lookup rules and overload resolution to determine the resulting indexer based on the element expression (for an expression element) or type (for a spread or key-value pair element). *This corresponds to the binding behavior for `Add()` methods for non-dictionary collection expressions.*
2. Use the target type implementation of `IDictionary<K, V>.this[K] { get; set; }`.
3. Use the accessible indexer that matches the signature `V this[K] { get; set; }`.

**Resolution:** Option 3: Use the indexer that qualifies the type as a dictionary type. [LDM-2025-03-05](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-05.md#conclusion)

### Type inference for `KeyValuePair<K, V>` collections

Confirm the [*type inference*](#type-inference) rules for elements when the target type is a `KeyValuePair<K, V>` collection.

```csharp
string x; int y;
KeyValuePair<string, long> e;
Dictionary<object, int> d;
...
Print([x:y]);         // Print<string, int>
Print([e]);           // Print<string, long>
Print([..d]);         // Print<object, int>
Print([x:y, e, ..d]); // Print<object, long>

void Print<K, V>(List<KeyValuePair<K, V>> pairs) { ... }
```

>   * **If `T` has an *element type* `KeyValuePair<Kₑ, Vₑ>`, or `T` is a *nullable value type* `T0?` and `T0` has an *element type* `KeyValuePair<Kₑ, Vₑ>`, then for each `Eᵢ`**:
>     * **If `Eᵢ` is a *key value pair element* `Kᵢ:Vᵢ`, then an *input type inference* is made *from* `Kᵢ` *to* `Kₑ` and an *input type inference* is made *from* `Vᵢ` *to* `Vₑ`.**
>     * **If `Eᵢ` is an *expression element* with type `KeyValuePair<Kᵢ, Vᵢ>`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Kᵢ` *to* `Kₑ` and a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Vᵢ` *to* `Vₑ`.**
>     * **If `Eᵢ` is a *spread element* with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `KeyValuePair<Kᵢ, Vᵢ>`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Kᵢ` *to* `Kₑ` and a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Vᵢ` *to* `Vₑ`.**

**Resolution:** Rules accepted as written. [LDM-2025-03-24](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-24.md#conclusion-1)

### Overload resolution for `KeyValuePair<K, V>` collections

Confirm the *better conversion* rules for [*overload resolution*](#overload-resolution) when the target types are `KeyValuePair<K, V>` collections.

```csharp
KeyValuePair<byte, int> e;
Dictionary<byte, int> d;
...
Print([1:2]); // <int, int>
Print([e])    // ambiguous
Print([..d])  // ambiguous

void Print(List<KeyValuePair<int, int>> pairs) { ... }
void Print(List<KeyValuePair<byte, object>> pairs) { ... }
```

> Conversion comparisons are made as follows:
> - **If the target is a type with an *element type* `KeyValuePair<Kₑ, Vₑ>`:**
>   - **If `ELᵢ` is a *key value pair element* `Kᵢ:Vᵢ`, conversion comparison uses better conversion from expression from `Kᵢ` to `Kₑ` and better conversion from expression from `Vᵢ` to `Vₑ`.**
>   - **If `ELᵢ` is an *expression element* with *element type* `KeyValuePair<Kᵢ, Vᵢ>`, conversion comparison uses better conversion from type `Kᵢ` to `Kₑ` and better conversion from type `Vᵢ` to `Vₑ`.**
>   - **If `ELᵢ` is an *spread element* with an expression with *element type* `KeyValuePair<Kᵢ, Vᵢ>`, conversion comparison uses better conversion from type `Kᵢ` to `Kₑ` and better conversion from type `Vᵢ` to `Vₑ`.**

**Resolution:** Rules accepted as written. [LDM-2025-03-24](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-24.md#conclusion-2)

### Support dictionary types as `params` type

Should types with element type `KeyValuePair<K, V>`, that are not otherwise collection types, be supported as `params` parameter types?

```csharp
KeyValuePair<string, int> x, y;

ToDictionary(x, y);
ToReadOnlyDictionary(x, y);

static Dictionary<K, V> ToDictionary<K, V>(
    params Dictionary<K, V> elements) => elements;          // C#14: ok?

static IReadOnlyDictionary<K, V> ToReadOnlyDictionary<K, V>(
    params IReadOnlyDictionary<K, V> elements) => elements; // C#14: ok?
```

Note that regardless of whether we support dictionary types for `params`, or simply continue to support C#12 collection types with `KeyValuePair<K, V>` element type, it won't be possible to use `k:v` syntax when calling a `params` method with *expanded form*.

```csharp
ToList("one":1);   // error: syntax error ':'
ToList(["two":2]); // C#14: ok

static List<KeyValuePair<K, V>> ToList<K, V>(params List<KeyValuePair<K, V>> elements) => elements;
```

**Resolution:** Allow `params` on dictionary-like types that can be targeted with a collection expression, and constructing those types will prefer using indexers when available. [LDM-2025-03-24](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-24.md#conclusion)

### Question: Types that support both collection and dictionary initialization

C# 12 supports collection types where the element type is some `KeyValuePair<,>`, where the type has an applicable `Add()` method that takes a single argument. Which approach should we use for initialization if the type also includes an indexer?

For example, consider a type like so:

```c#
public class Hybrid<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
{
    public void Add(KeyValuePair<TKey, TValue> pair);
    public TValue this[TKey key] { ... }
}

// This would compile in C# 12:
// Translating to calls to .Add.
Hybrid<string, int> nameToAge = [someKvp];
```

Options include:

1. Use applicable instance indexer if available; otherwise use C#12 initialization.
2. Use applicable instance indexer if available; otherwise report an error during construction (or conversion?).
3. Use C#12 initialization always.

**Resolution:** If the target type is a struct or class type that implements `IEnumerable` and has an iteration type of `KeyValuePair<K, V>`, and the type has the expected instance indexer (see [*Conversions*](#conversions)), then the indexer is used for initialization rather than any `Add` methods. [LDM-2025-03-05](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-05.md#conclusion)

### Question: Parsing ambiguity

Parsing ambiguity around: `[a ? [b] : c]`

**Resolution:** Parse as `[a ? ([b]) : (c)]`. [LDM-2025-04-14](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-14.md#conclusion)

### Question: Implement non-generic `IDictionary` when targeting `IReadOnlyDictionary<,>`

Collection expressions specified explicitly:

> Given a target type which does not contain mutating members, namely `IEnumerable<T>`, `IReadOnlyCollection<T>`, and `IReadOnlyList<T>`, a compliant implementation is required to produce a value that implements that interface. ...
>
> In addition, the value must implement the nongeneric `ICollection` and `IList` interfaces. This enables collection expressions to support dynamic introspection in scenarios such as data binding.

Do we want a similar correspondance when the target type is `IReadOnlyDictionary<,>`?  Specifically, should the value be required to implement the non-generic `IDictionary` interface?

**Resolution:** The type used to implement `IReadOnlyDictionary<K, V>` should implement `System.Collections.IDictionary`. [LDM-2025-04-14](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-14.md#conclusion-1)

## Retracted Designs/Questions

### Question: Should `k:v` elements force dictionary semantics?

Is there a concern around the following interface destinations:

```c#
// NOTE: These are not overloads.

void AAA(IEnumerable<KeyValuePair<string, int>> pairs) ...
void BBB(IDictionary<string, int> pairs) ...

AAA(["mads": 21, .. ldm]);
BBB(["mads": 21, .. ldm]);
```

When the destination is an `IEnumerable<T>`, we tend to think we're producing a sequence (so "mads" could show up twice).  However, the use of the `k:v` syntax more strongly indicates production of a dictionary-value.

What should we do here when targeting `IEnumerable<...>` *and* using `k:v` elements? Produce an ordered sequence, with possibly duplicated values?  Or produce an unordered dictionary, with unique keys?

Resolution: `IEnumerable<KVP>` is not a dictionary type (as it lacks an indexer).  As such, it has sequential value semantics (and can include duplicates).  This would happen today anyways if someone did `[.. ldm]` and we do not think the presence of a `k:v` element changes how the semantics should work.

This is also similar to how passing to an `IEnumerable<T>` would differ from passing to some *set* type with normal collection expressions.  The target type *intentionally* affects semantics, and there is no expectation that across very different target types that one would receive the same resultant values with the same behaviors.  We do not view *dictionary types* or *key value pair elements* as changing the calculus here.

### Question: Allow deconstructible types?

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

Similarly, instead of `KeyValuePair<,>` we could allow *any* type deconstructible to two values? For example:

```c#
record struct Pair<X, Y>(X x, Y y);

Dictionary<int, string> map1 = [pair1, pair2]; // ?
```

Resolution: While cute, these capabilities are not needed for core scenarios to work.  They also raise concerns about where to draw the line wrt to what is the dictionary space and what is not.  As such, we will only allow `KeyValuePair<,>` for now.  And we will not do anything with tuples and/or other deconstructible types.  This is also something that could be relaxed in the future if there is sufficient feedback and motivation to warrant it.  This design space is withdrawn from dictionary expressions.

### Question: Semantics when a type implements the *dictionary type* shape in multiple ways.

What are the rules when types have multiple indexers and multiple implementations of `IEnumerable<KVP<,>>`?

This concern already exists with *collection types*.  For those types, the rule is that we must have an *element type* as per the existing language rules.  This follows for *dictionary types*, along with the rule that there must be a corresponding indexer for this *element type*.  If those hold, the type can be used as a *dictionary type*.  If these don't hold, it cannot be.

## Open Questions

### Question: Special case 'comparer' support for dictionaries (and regular collections)?

[Collection expression arguments](https://github.com/dotnet/csharplang/blob/main/proposals/collection-expression-arguments.md) proposes a generalized system for providing arguments for constructible (`new(...)`) collection types, collection builder types, and for a subset of interface types.  This solves the problem of how can a comparer be passed to a dictionary-like type, as well as for other collections that can benefit from customization (like hash sets and the like).  However, in the absense of an approved language change to support a generalized argument passing system, do we want to be able to have special support for passing *only* comparers along?

For example, a hypothetical syntax could be something like:

```c#
Dictionary<string, object> nameToOptions = [
    comparer: StringComparer.OrginalIgnoreCase,
    .. GetDefaultOptions(),
    .. GetHostSpecificOptions(),
    .. GetPlatformSpecificOptions(),
];
```

Pros: A specialized syntax can be clearer and more focused to the exact problem at hand, side stepping lots of complexity related to arbitrary argument passing (for example, out/ref arguments, named arguments, optional arguments, and the like).
Cons: Some user will still want to pass arbitrary arguments along (for cases like '.Capacity', and for collections that capture/wrap some other collection).  They will still be left out if we do not have a general system.  And, if we add a general system later, there would be multiple ways to support passing a comparer along.

Possible syntactic options here are:

1. `[comparer: StringComparer.OrginalIgnoreCase]`.  Simple and clear.  But a long contextual keyword for 'comparer'.
2. `[comp: StringComparer.OrginalIgnoreCase]`. A bit less clear, but generally readable in context.  Similar to `init` where we truncate a word to something reasonable in context.
3. `[...] with StringCompaer.OrdinalIgnoreCase`.  Not desirable.  Collections may be quite large, and having to get to the end to understand core behavior/semantics of how the collection operates is not great.  This especially clashes with all existing forms to make collections today, where the comparer will be at the start.
4. `[ == StringComparer.OrdinalIgnoreCase]`.  Cutesy syntax.  `==` represents 'equality', and thus this is a special element saying "equality is provided by this comparer"
5. `[ == : StringComparer.OrdinalIgnoreCase]`.  Similarly cutesy, just using a colon to indicate "provided by".
6. `[ <=> StringComparer.OrdinalIgnoreCase]`.  Cutesy, and in line with C++ (and potential future language changes) where the "spaceship operator" represents the way things compare against each other.

Note: Any solution should support both `IComparer<>` (for `SortedSet<>`, `SortedDictionary<,>`, and their immutable variants) and `IEqualityComparer<>` (for all the hashing based collections).  As such, a mild word like `comparer/comp` seems to fit the bill best.

If we do special case comparers, the rules would say something intuitively akin to the following:

> If a comparer element is provided, then:
> 1. If generating a `new()` type, the type must have a constructor that that comparer can be passed to.
> 2. If generating a collection builder type, their must be a factory method references that can take the comparer as the first argument, and the elements as the second.
> 3. If generating an interface, the only supported interfaces are `IDictionary<,>` and `IReadOnlyDictionary<,>`.  For the former, the comparer will be passed to the `new(IEqualityComparer<>)` constructor on `Dictionary<>`.  For the latter, the dictionary created by the compiler will be guaranteed to use the specified equality comparer to perform hashing and equality checks of the provided keys.

