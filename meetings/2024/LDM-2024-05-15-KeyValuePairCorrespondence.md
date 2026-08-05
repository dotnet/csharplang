# KeyValuePair correspondence

In .NET, dictionary types and the `KeyValuePair<TKey, TValue>` (aka KVP, or KeyValuePair) types are intertwined.  A dictionary is commonly defined as a collection of elements of that KVP type, not just a mapping from some `TKey` to some `TValue`.  Indeed, this duality allows one to treat the two spaces mostly uniformly.  For example:

```c#
var dictionary = new Dictionary<string, int>();
var collection = (ICollection<KeyValuePair<string, int>>)dictionary;
collection.Add(new KeyValuePair<string, int>("mads", 21));
```

What is special about dictionaries, over standard element-based collection expressions, is that the dictionary types have a general view that any particular key will only be contained once, and can be used to then more efficiently map to its associated value over doing a linear scan.  Put more intuitively: A "dictionary type" is a "collection type" whose "element type" is some `KeyValuePair<K, V>` and which has an available `V this[K key] { get; }` indexer.

Because of this correspondence, we believe that dictionary expressions should not be considered very special and distinct from existing collection expressions.  Rather, the "dictionary expression" language feature is actually a feature that allows KeyValuePairs to be naturally expressed within collection expressions, along with a sensible and uniform set of rules to allow KeyValuePairs to naturally initialize collection types.  This "natural expression" happens both syntactically and semantically.

Specifically:

1. There is a new special syntax for declaring a KeyValuePair within a collection expression:

    ```c#
    X x = [k: v];
    ```

1. It can be used with dictionary types:

    ```c#
    Dictionary<string, int> nameToAge = ["mads": 21];
    ```

1. And also with existing collection types:

    ```c#
    List<KeyValuePair<string, int>> pairs = ["mads": 21];
    ```

1. And, while the syntax allows for easy specification of the particular key and value, usage of that syntax is optional.  Semantically, the feature works equally with normal KeyValuePair instances:

    ```c#
    KeyValuePair<string, int> kvp = new("mads", 21);
    Dictionary<string, int> nameToAge = [kvp];
    ```

1. The above allows for *uniformity* of processing KeyValuePair values, which we consider desirable so that users can expect them to work for all collection expressions elements:

    ```c#
    // Both 'spread' elements and 'expression' elements that evaluate 
    // to KeyValuePair values work with dictionary types
    Dictionary<string, int> nameToAge = [.. defaultValues, otherMap.Single(predicate)];
    ```

    Here, being able to 'spread' in another collection (which would normally be some `IEnumerable<KeyValuePair<,>>`) is desirable.  Similarly, being able to add individual pairs found through some means, without having to decompose into `k: v` syntax, is equally preferable.

## KeyValuePair transparency

The existing "Collection Expression" feature has a guiding principle that elements and spreads can be thought of as being lowered to `Add` calls. This enables things to be included or spread into the final collection that have a more specific type than the collection's element type itself.  For example:

```c#
// The collection expression can be comprised of `int` values
// despite the element type being `int?`.
List<int?> ages = [18, .. Enumerable.Range(21, 10)];
```

This allowance is implied by the lowered representation, where implicit conversions enable a straightforward scenario to appear equally straightforward in code without onerous explicit casts:

```c#
var ages = new List<int?>();
ages.Add(18);
foreach (var value in Enumerable.Range(21, 10))
    ages.Add(value);
```

Dictionary expressions have a corollary. Both the key and the value can be more specific types than the key and value types of the dictionary being built, when lowered in the same manner:

```c#
var map1 = new Dictionary<object, int?>();
map1["mads"] = 21;
// Etc
```

To achieve this principle in dictionary expressions, we expect the exact type of the KeyValuePair values to be generally transparent.  Rather than being strictly that type, the language will generally see *through* it to be a pair of some `TKey` and `TValue` types.  This transparency is in line with how tuples behave and serves as a strong intuition for how we want users to intuit KeyValuePairs in the context of collection expressions.

How does this transparency manifest?  Consider the following scenario:

```c#
Dictionary<object, int?> map1 = ["mads": 21];
```

The above expression would certainly be expected to work.  While `"mads"` is a string, and `21` an `int`, the target-typed nature of collection expressions would push the `object` and `int?` types through the constituent key and value expressions to type them properly.  We would *not* disallow this, despite `KeyValuePair<string, int>` and `KeyValuePair<object, int?>` being incompatible.

This would also be expected to work in the following case:

```c#
Dictionary<object?, int?> map2 = [null: null];
```

KeyValuePair transparency means that just as we expect the code for `map1` to be legal, we should consider the following legal as well:

```c#
KeyValuePair<string, int> kvp = new("mads", 21);
Dictionary<object, int?> map1 = [kvp];
```

After all, why would that be illegal, while the following became legal?

```c#
KeyValuePair<string, int> kvp = new("mads", 21);
Dictionary<object, int?> map1 = [kvp.Key: kvp.Value];
```

Requiring explicit deconstruction of the constituent key and value portions of a KVP, just to satisfy the compiler so it could target-type them, adds extra, painful steps.  It would become doubly worse once all collection element expressions are considered. We would like users to be able to write:

```c#
 Dictionary<object, int?> map = [.. nameToAge, otherMap.Single(predicate)];

// Not:

var singleElement = otherMap.Single(predicate);
Dictionary<object, int?> map = [.. nameToAge.Select(kvp => new KeyValuePair<object, int?>(kvp.Key, kvp.Value), singleElement.Key: singleElement.Value];
```

## Tuple analogy

It turns out that this sort of behavior is *exactly* what already exists in the language today for tuples.  Consider the following:

```c#
List<(object? key, int? value)> map = [("mads", 21)];
```

This already works today.  The language transparently sees through into the tuple expression to ensure that the above is legal.  This is also not a conversion applied to some `(string, int)` tuple type.  That can be seen here which is also legal:

```c#
List<(object? key, int? value)> map = [(null, null)];
```

Here, the types of the destination flow all the way through (including recursively through nested tuple types) into the tuple expression in the initializer.  This transparency is not limited to *tuple expressions* either.  All of the following are legal as well, despite non-matching *ValueTuple types*:

```c#
(string x, int y) kvp = ("mads", 21);

// (string, int) and (object?, int?) are not compatible at the runtime
// level.  The language enables this at the C# level.
List<(object? key, int? value)> map = [kvp];
```

And

```c#
(string? x, int? y) kvp = (null, null);
List<(object? key, int? value)> map = [kvp];
```

The language always permissively views tuples as a loose aggregation of constituent elements, each with their own type.  Conversions and compatibility are all performed on those constituent element types, not on the top level `ValueTuple<>` type which would normally not be compatible based on .NET type system rules.

## KeyValuePair inference

The tuple analogy above serves as an analogous system we can look to in order to see how we would like KeyValuePair to behave in collection expressions.  For example:

```c#
void M<TKey, TValue>(List<(TKey key, TValue value)> list1, List<(TKey key, TValue value)> list2);

// Note: neither tuple1 nor tuple2 are assignable/implicitly convertible
// to each other. Each has an element that has a wider type than the 
// corresponding element in the other.
(string x, int? y) tuple1 = ("mads", 21);
(object x, int y) tuple2 = ("cyrus", 22);

// Infers `M<object, int?>`
M([tuple1], [tuple2]);
```

This works today and correctly infers `M<object, int?>`.  Given the above, we would then desire the following to work:

```c#
void M<TKey, TValue>(Dictionary<TKey, TValue> d1, Dictionary<TKey, TValue> d2);

// Note: neither kvp1 nor kvp2 would ever be assignable/implicitly convertible to each other.
KeyValuePair<string, int?> kvp1 = new("mads", 21);
KeyValuePair<object, int> kvp2 = new("cyrus", 22);

// Would like this to infer `M<object,int?>` as well.
M([kvp1], [kvp2]);
```

## Tuple analogy (cont.)

The analogous tuple behavior serves as a good *bedrock* for our intuitions on what we want for KeyValuePairs.  However, how far we want to take this analogy is up to us, and we can consider several levels of increasing transparency support.  Those levels are:

1. No transparency support.  Do not treat KVPs like tuples.  Force users to explicitly convert between KVP types to satisfy type safety at the KVP level itself.  For example:

    ```c#
    KeyValuePair<string, int> kvp = new("mads", 21);
    Dictionary<object, int?> map1 = [kvp]; // illegal.  user must write:
    Dictionary<object, int?> map1 = [kvp.Key: kvp.Value];

    Dictionary<object, int?> map1 = [.. nameToAge, otherMap.Single(predicate)]; // illegal.  user must write:

    var temp = otherMap.Single(predicate);
    Dictionary<object, int?> map1 = [.. nameToAge.Select(kvp => new KeyValuePair<object, int?>(kvp.Key, kvp.Value)), temp.Key: temp.Value];
    ```

1. Transparent only when targeting some dictionary type, but not non-dictionary types:

    ```c#
    KeyValuePair<string, int> kvp = new("mads", 21);
    Dictionary<object, int?> map1 = [kvp]; // legal.

    List<KeyValuePair<object, int?>> map1 = [kvp]; // not legal.  User must write:
    List<KeyValuePair<object, int?>> map1 = [kvp.Key: kvp.Value]; // or
    List<KeyValuePair<object, int?>> map1 = [new KeyValuePair<object, int?>(kvp.Key, kvp.Value)];
    ```

1. Transparent in any collection expression, but no further:

    ```c#
    KeyValuePair<string, int> kvp = new("mads", 21);
    Dictionary<object, int?> map1 = [kvp]; // legal.
    List<KeyValuePair<object, int?>> map1 = [kvp]; // legal.

    KeyValuePair<object, int?> kvp2 = kvp1; // not legal.  User must write:
    KeyValuePair<object, int?> kvp2 = new KeyValuePair<object, int?>(kvp1.Key, kvp.Value);
    ```

1. Transparent everywhere:

    ```c#
    KeyValuePair<string, int> kvp = new("mads", 21);
    Dictionary<object, int?> map1 = [kvp]; // legal.
    List<KeyValuePair<object, int?>> map1 = [kvp]; // legal.
    KeyValuePair<object, int?> kvp2 = kvp1; // legal.
    ```

These four options form a spectrum, starting with doing nothing special, then only handling dictionaries, then handling any collection, all the way to the maximum support which effectively puts KeyValuePair handling at the same level as tuples for the language.

Open question 1: How far would we like to take this transparency?  All the way to full analogy with tuples?  No transparency at all?  Somewhere in the middle?

## Deconstruction

All of the above so far has been about how the language would enable working more conveniently with the KeyValuePair type.  And, there are good arguments to be made that KeyValuePair needs to allow these important scenarios to light up, due to how integral it is to the dictionary-type space to begin with.  However, fundamentally, all of the above could be reformulated, enabling the same scenarios without specializing KeyValuePair at all.  Specifically, all of the above works by stating that KeyValuePair can be seen transparently as a pair of two typed values (the `TKey Key` and the `TValue Value`).  Fundamentally, as that's all that is truly required, a relaxation could be performed that restates all of the above as:

> Any type that is *constructible* and *deconstructible* into two elements would be transparently supported in the context of collection expressions and the `k: v` element.

That relaxation would consume all the KeyValuePair support.  But would also then enable tuples to be used in all those cases *as well as* any appropriate type supporting two-element construction/deconstruction.  As such, all of the below would be legal:

```c#
Dictionary<string, int> nameToAge1 = [("mads", 21)];

List<(string, int)> pairs = ...;
Dictionary<string, int> nameToAge2 = [.. pairs];

record struct NameAndAge(string Name, int Age);
Dictionary<string, int> nameToAge3 = [nameToAge1, nameToAge2];

List<NameToAge> pairs = ["mads": 21, "cyrus": 22, "joseph": 23];
// etc.
```

Open question 2: How far would we like to take this? 

1. Only support KeyValuePair.  2-element tuples and other 2-element deconstructible types have no special meaning in a collection expression.

    ```c#
    Dictionary<string, int> nameToAge = [kvp]; // legal

    Dictionary<string, int> nameToAge = [("mads", 21)]; // not legal

    record NameAndAge(string Name, int Age);
    NameAndAge nameAndAge = new("mads", 21);
    Dictionary<string, int> nameToAge = [nameAndAge] // not legal
    ```

1. Support KeyValuePair and 2-element tuples, but not other 2-element deconstructible types.

    ```c#
    Dictionary<string, int> nameToAge = [kvp]; // legal

    Dictionary<string, int> nameToAge = [("mads", 21)]; // now legal!

    record NameAndAge(string Name, int Age);
    NameAndAge nameAndAge = new("mads", 21);
    Dictionary<string, int> nameToAge = [nameAndAge] // not legal
    ```

1. Support any 2-element deconstructible types?

    ```c#
    Dictionary<string, int> nameToAge = [kvp]; // legal

    Dictionary<string, int> nameToAge = [("mads", 21)]; // legal

    record NameAndAge(string Name, int Age);
    NameAndAge nameAndAge = new("mads", 21);
    Dictionary<string, int> nameToAge = [nameAndAge] // now legal!
    ```
