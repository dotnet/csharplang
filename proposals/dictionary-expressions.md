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

## Dictionary types

A type is considered a *dictionary type* if the following hold:
* The *element type* is `KeyValuePair<TKey, TValue>`.
* The *type* has an instance *indexer*, with a `get` accessor where:
  * The indexer has a single parameter with an identity conversion from the parameter type to `TKey`.\*
  * There is an identity conversion from the indexer type to `TValue`.\*
  * The `get` accessor returns by value.
  * The `get` accessor is as accessible as the declaring type.

\* *Identity conversions are used rather than exact matches to allow type differences in the signature that are ignored by the runtime: `object` vs. `dynamic`; tuple element names; nullable reference types; etc.*

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

*Collection expression conversions* are updated to include conversions to *dictionary types*.

An implicit *collection expression conversion* exists from a *collection expression* to the following *dictionary types*:
* A *dictionary type* with an appropriate *[create method](#create-methods)*.

* A *struct* or *class* *dictionary type* that implements `System.Collections.IEnumerable` where:
  * The *element type* is determined from a `GetEnumerator` instance method or enumerable interface.
  * The *type* has an *[applicable](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11642-applicable-function-member)* constructor that can be invoked with no arguments (*or* a constructor with a single [*comparer*](#Comparer-support) parameter), and the constructor is accessible at the location of the collection expression.
  * The *indexer* has a `set` accessor that is as accessible as the declaring type.

* An *interface type*:
  * `System.Collections.Generic.IDictionary<TKey, TValue>`
  * `System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>`

*Collection expression conversions* require implicit conversions for each element.
The element conversion rules are updated as follows.

> The implicit conversion exists if the type has an *element type* `T` where for each *element* `Eᵢ` in the collection expression:
> * If `Eᵢ` is an *expression element*, there is an implicit conversion from `Eᵢ` to `T`.
> * If `Eᵢ` is a *spread element* `..Sᵢ`, there is an implicit conversion from the *iteration type* of `Sᵢ` to `T`.
> * **If `Eᵢ` is a *key-value pair element* `Kᵢ:Vᵢ` and `T` is a type `KeyValuePair<K, V>`, there is an implicit conversion from `Kᵢ` to `K` and an implicit conversion from `Vᵢ` to `V`.**
> * **Otherwise there is *no conversion* from the collection expression to the target type.**

### Key-value pair conversions

A *key-value pair conversion* is introduced.

An implicit *key-value pair conversion* exists from an *expression element* to the *element type* of the containing *collection expression* if all of the following hold:
- the expression element has *type* `KeyValuePair<Kᵢ, Vᵢ>`
- the collection expression has *element type* `KeyValuePair<K, V>`
- there is an implicit conversion from `Kᵢ` to `K`
- there is an implicit conversion from `Vᵢ` to `V`

An implicit *key-value pair conversion* exists from the *iteration type* of a *spread element* to the *element type* of the containing *collection expression* if all of the following hold:
- the spread element has *iteration type* `KeyValuePair<Kᵢ, Vᵢ>`
- the collection expression has *element type* `KeyValuePair<K, V>`
- there is an implicit conversion from `Kᵢ` to `K`
- there is an implicit conversion from `Vᵢ` to `V`

Key-value pair conversions are useful for *expression elements* and *spread elements* where the key or value types do not match the collection element type exactly.
Despite the name, key-value pair conversions *do not* apply to *key-value elements*.

```csharp
Dictionary<int, string>  x = ...;
Dictionary<long, object> y = [..x]; // key-value pair conversion from KVP<int, string> to KVP<long, object>
```

Implicit key-value pair conversions are similar to [*implicit tuple conversions*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#10213-implicit-tuple-conversions) that allow converting between distinct tuple types.

```csharp
List<(int, string)>  x = ...;
List<(long, object)> y = [..x]; // tuple conversion from (int, string) to (long, object)
```

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

The elements of a collection expression are evaluated in order, left to right. Each element is evaluated exactly once, and any further references to the elements refer to the results of this initial evaluation.

If the target is a *dictionary type*, and collection expression's first element is an `expression_element`, and the type of that element is some [*comparer*](#Comparer-support), then:

- If using a constructor to instantiate the value, the constructor must take a single parameter whose type is some [*comparer*](#Comparer-support) type.  The first `element_expression` value will be passed to this parameter.
- If using a *[create method](#create-methods)*, the method's first parameter's type is some [*comparer*](#Comparer-support) type. The first `element_expression` value will be passed to this parameter.
- If creating an interface, this [*comparer*](#Comparer-support) must be some `IEqualityComparer<TKey>` type. That comparer will be used to control the behavior of the final type (synthesized or otherwise).  This means that instantiating interfaces only supports hashing semantics, not ordered semantics.

For each element `Eᵢ` in order:
- If the target is a *dictionary type* then:
  - If `Eᵢ` is a *key value pair element* `Kᵢ:Vᵢ`, first `Kᵢ` is evaluated, then `Vᵢ` is evaluated, and the applicable indexer is invoked on the dictionary instance with the converted values of `Kᵢ` and `Vᵢ`.
  - If `Eᵢ` is an *expression element* of type `KeyValuePair<Kᵢ:Vᵢ>`, then `Eᵢ` is evaluated, and the applicable indexer is invoked on the dictionary instance with the converted values of `.Key` and `.Value` from the value of `Eᵢ`.
  - If `Eᵢ` is an *spread element* `..Sᵢ` where `Sᵢ` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `KeyValuePair<Kᵢ, Vᵢ>`, then `Sᵢ` is evaluated and an applicable `GetEnumerator` instance or extension method is invoked on the value of `Sᵢ`, and for each item `Sₑ` from the enumerator, the applicable indexer is invoked on the dictionary instance with the converted values of `.Key` and `.Value` from the value of `Sₑ`. If the enumerator implements `IDisposable`, then `Dispose` will be called after enumeration, regardless of exceptions.
- If the target is a *collection type* that has an *element type* of `KeyValuePair<Kₑ, Vₑ>` then:
  - If `Eᵢ` is a *key value pair element* `Kᵢ:Vᵢ`, first `Kᵢ` is evaluated, then `Vᵢ` is evaluated, then a `KeyValuePair<Kₑ, Vₑ>` value is constructed with the converted values of `Kᵢ` and `Vᵢ`, and the value is added to the collection instance *using existing steps for *collection types**.
  - If `Eᵢ` is an *expression element* of type `KeyValuePair<Kᵢ:Vᵢ>`, then `Eᵢ` is evaluated, then a `KeyValuePair<Kₑ, Vₑ>` value is constructed with the converted values of `.Key` and `.Value` from the value of `Eᵢ`, and the value is added to the collection instance *using existing steps for *collection types**.
  - If `Eᵢ` is an *spread element* `..Sᵢ` where `Sᵢ` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `KeyValuePair<Kᵢ, Vᵢ>`, then `Sᵢ` is evaluated and an applicable `GetEnumerator` instance or extension method is invoked on the value of `Sᵢ`, and for each item `Sₑ` from the enumerator, then a `KeyValuePair<Kₑ, Vₑ>` value is constructed with the converted values of `.Key` and `.Value` from the value of `Sₑ`, and the value is added to the collection instance *using existing steps for *collection types**. If the enumerator implements `IDisposable`, then `Dispose` will be called after enumeration, regardless of exceptions.
- If the target is a *collection type* that has an *element type* other than `KeyValuePair<,>` then:
  - *[Use existing steps for construction]*

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

### Mutable interface translation

Given the target type `IDictionary<TKey, TValue>`, the type used will be `Dictionary<TKey, TValue>`.  Using the normal translation mechanics defined already (including handling of an initially provided [*comparer*](#Comparer-support)). This follows the originating intuition around `IList<T>` and `List<T>` in *collection expressions*. 

### Non-mutable interface translation

Given a target type `IReadOnlyDictionary<TKey, TValue>`, a compliant implementation is only required to produce a value that implements that interface. A compliant implementation is free to:

1. Use an existing type that implements that interface.
1. Synthesize a type that implements the interface.

In either case, the type used is allowed to implement a larger set of interfaces than those strictly required.

Synthesized types are free to employ any strategy they want to implement the required interfaces properly.  The value generated is allowed to implement more interfaces than required. For example, implementing the mutable interfaces as well (specifically, implementing `IDictionary<TKey, TValue>` or the non-generic `IDictionary`). However, in that case:

1. The value must return true when queried for `.IsReadOnly`. This ensures consumers can appropriately tell that the collection is non-mutable, despite implementing the mutable views.
1. The value must throw on any call to a mutation method. This ensures safety, preventing a non-mutable collection from being accidentally mutated.

This follows the originating intuition around `IReadOnlyList<T>` and the synthesized type for it in *collection expressions*. 


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

### Binding to indexer

For concrete dictionary types that do not use `CollectionBuilderAttribute`, where the compiler constructs the resulting instance using a constructor and repeated calls to an indexer, how should the compiler resolve the appropriate indexer for each element?

Options include:
1. For each element individually, use normal lookup rules and overload resolution to determine the resulting indexer based on the element expression (for an expression element) or type (for a spread or key-value pair element). *This corresponds to the binding behavior for `Add()` methods for non-dictionary collection expressions.*
2. Use the target type implementation of `IDictionary<K, V>.this[K] { get; set; }`.
3. Use the accessible indexer that matches the signature `V this[K] { get; set; }`.

### Key-value pair conversions

Should the compiler support a new [*key-value pair conversion*](#key-value-pair-conversions) within collection expressions to allow implicit conversions from an expression element of type `KeyValuePair<K1, V1>`, or a spread element with an iteration type of `KeyValuePair<K1, V1>` to the collection expression iteration type `KeyValuePair<K2, V2>`?

### Concrete type for `I{ReadOnly}Dictionary<K, V>`

What concrete type should be used for a dictionary expression with target type `IDictionary<K, V>`?

Options include:
1. Use `Dictionary<K, V>`, and state that as a requirement.
2. Use `Dictionary<K, V>` for now, and state the compiler is free to use any conforming implementation.
3. Synthesize an internal type and use that.

What concrete type should be used for `IReadOnlyDictionary<K, V>`?

Options include:
1. Use a BCL type such as `ReadOnlyDictionary<K, V>`, and state that as a requirement.
2. Use a BCL type such as `ReadOnlyDictionary<K, V>` for now, and state the compiler is free to use any conforming implementation.
3. Synthesize an internal type and use that.

We should consider aligning the decisions with the concrete types provided for collection expressions targeting mutable and immutable non-dictionary interfaces.

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

Resolution: TBD.  Working group recommendation: Use applicable instance indexer only.  This ensures that everything dictionary-like is initialized in a consistent fashion.  This would be a break in behavior when recompiling.  The view is that these types would be rare.  And if they exist, it would be nonsensical for them to behave differently using the indexer versus the `.Add` (outside of potentially throwing behavior).

### Question: Parsing ambiguity

Parsing ambiguity around: `[a ? [b] : c]`

Working group recommendation: Use normal parsing here.  So this would be the same as `[a ? ([b]) : (c)]` (a collection expression containing a conditional expression).  If the user wants a `key_value_pair_element` here, they can write: `[(a?[b]) : c]`
