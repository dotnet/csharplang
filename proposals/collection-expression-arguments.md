# Collection expression arguments

Champion issue: <https://github.com/dotnet/csharplang/issues/8887>

## Motivation

The [*dictionary expression*](https://github.com/dotnet/csharplang/blob/main/proposals/dictionary-expressions.md)
feature has identified a need for collection expressions to pass along user-specified data in order to configure
the behavior of the final collection.  Specifically, dictionaries allow users to customize how their keys compare,
using them to define equality between keys, and sorting or hashing (in the case of sorted or hashed collections
respectively).  This need applies when creating any sort of dictionary type (like `D d = new D(...)`,
`D d = D.CreateRange(...)` and even `IDictionary<...> d = <synthesized dict>`)

To support this, a new `with(...arguments...)` element is proposed as the first element of a collection expression
like so:

```c#
Dictionary<string, int> nameToAge = [with(comparer), .. d1, .. d2, .. d3];
```

1. When translating to a `new CollectionType(...)` call, these `...arguments...` are used to determine the appropriate
constructor and are passed along accordingly.
2. When translating to a `CollectionFactory.Create` call, these 
`...arguments...` are passed before with the `ReadOnlySpan<ElementType>` elements argument, all of which are 
used to determine the appropriate `Create` overload, and are passed along accordingly.
3. When translating to an interface (like `IDictionary<,>`) only a single argument is allowed.  It implements one of
   the well-known BCL comparer interfaces, and will be used to control the key comparing semantics of the final instance.

This syntax was chosen as it:

1. Keeps all information within the `[...]` syntax.  Ensuring that the code still clearly indicates a collection being created.
2. Does not imply calling a `new` constructor (when that isn't how all collections are created).
3. Does not imply creating/copying the values of the collection multiple times (like a postfix `with { ... }` might.
4. Is both not subtle, while also not being excessively verbose.  For example, using `;` instead of `,` to indicate
   arguments is a very easy piece of syntax to miss.  `with()` only adds 6 characters, and will easily stand out,
   especially with syntax coloring of the `with` keyword.
6. Reads nicely.  "This is a collection expression 'with' these arguments, consisting of these elements."
7. Solves the need for comparers for both dictionaries and sets.
8. Ensures any user need for passing arguments, or any needs we ourselves have beyond comparers in the future are already handled.
9. Does not conflict with any existing code (using https://grep.app/ to search).

A minor question exists if the preferred form would be `args(...)` or `init(...)` instead of `with(...)`.  But the forms are
otherwise identical.

Open question: Should any support for passing a comparer be provided at all?  Yes/No.

Working group recommendation: Yes.  Comparers are critical for proper behaving of collections, and examination of many packages
indicates usage of them to customize collection behavior.

Open question: If support for comparers is desired, should it be through a feature specific to *only* comparers?  Or should it
handle arbitrary arguments?

Working group recommendation: Support arbitrary arguments.  This solves both the 'comparer' issue, while nipping all present and
future argument concerns in the bud.  For example, users who need to customize performance-oriented arguments (like 'capacity')
now have a solution beyond waiting for the compiler to support the patterns they are using with the codegen they desire.

## Design Philosophy

The below section covers prior design philosophy discussions.  Including why certain forms were rejected. 

There are two main directions we can go in to supply this user-defined data.  The first is to special case *only*
 values in the *comparer* space (which we define as types inheriting from the BCL's `IComparer<T>` or
  `IEqualityComparer<T>` types).  The second is to provide a generalized mechanism to supply arbitrary arguments
   to the final invoked API when creating collection expressions.  The primary *dictionary expression* specification
    shows how we could do the former, while this specification seeks to do the latter.

Examinations of the solutions for just passing *comparers* have revealed weaknesses in their approach if we wanted
 to expand them to *arbitrary arguments*.  For example:

1. Reusing *element* syntax, like we do with the form: `[StringComparer.OrdinalIgnoreCase, "mads": 21]`. This works
   well in a space where `KeyValuePair<,>` and comparers do not inherit from common types.  But it breaks down in a
   world where one might do: `HashSet<object> h = [StringComparer.OrdinalIgnoreCase, "v"]`.  Is this passing along
   a comparer?  Or attempting to put two object values into the set?

2. Separating out arguments versus elements with subtle syntax (like using a semicolon instead of a comma to
   separate them in `[comparer; v1]`). This risks very confusing situations where a user accidentally writes `[1; 2]`
   (and gets a collection that passes '1' as, say, the 'capacity' argument for a `List<>`, and only contains the
   single value '2'), when they intended `[1, 2]` (a collection with two elements).

Because of this, in order to support arbitrary arguments, we believe a more obvious syntax is needed to more
clearly demarcate these values. Several other design concerns have also come up with in this space.  In no
particular order, these are:

1. That the solution not be ambiguous and cause breaks with code that people are likely using with collection
 expressions today.  For example:

    ```c#
    List<Widget> c = [new(...), w1, w2, w3];
    ```
    
    This is legal today, with the `new(...)` expression being a 'implicit object creation' that creates a new
    widget.  We cannot repurpose this to pass along arguments to `List<>`'s constructor as it would *certainly*
    break existing code.

1. That the syntax not extend to outside of the `[...]` construct.  For example:

    ```c#
    HashSet<string> s = [...] with ...;
    ```
    
    These syntaxes can be construed to mean that the collection is created first, and then recreated into a
    differing form, implying multiple transformations of the data, and potentially unwanted higher costs
    (even if that's not what is emitted).

1. That `new` as a potential keyword to use *at all* in this space is undesirably confusing.  Both because
   `[...]` *already* indicates that a *new* object is created, and because translations of the collection
   expression may go through non-constructor APIs (for example, the *Create method* pattern).

1. That the solution not be excessively verbose.  A core value proposition of collection expressions is
   *brevity*. So if the form adds a large amount of syntactic scaffolding, it will feel like a step backwards,
   and will undercut the value proposition of using collection-expressions, versus calling into the existing
   APIs to make the collection.

Note that a syntax like `new([...], ...)` runs afoul of both '2' and '3' above.  It makes it appear as if we
are calling into a constructor (when we may not be) *and* it implies that a created collection expression is
passed to that constructor, which is definitely is not.

Based on all of the above, a small handful of options have come up that are felt to solve the needs of passing
arguments, without stepping out of bounds of the goals of collection expressions.

## Option 1: `[with(...arguments...)]`

The design of this form would be as follows:

```diff
collection_element
   : expression_element
   | spread_element
   | key_value_pair_element
+  | with_element
   ;

+with_element
+  : 'with' argument_list
+  ;
```

Examples of how this would look are:

```c#
// With an existing type:

// Initialize to twice the capacity since we'll have to add
// more values later.
List<string> names = [with(capacity: values.Count * 2), .. values];

// With the dictionary types.
Dictionary<string, int> nameToAge1 = [with(comparer)];
Dictionary<string, int> nameToAge2 = [with(comparer), kvp1, kvp2, kvp3];
Dictionary<string, int> nameToAge3 = [with(comparer), k1:v1, k2:v2, k3:v4];
Dictionary<string, int> nameToAge4 = [with(comparer), .. d1, .. d2, .. d3];

Dictionary<string, int> nameToAge = [with(comparer), kvp1, k1: v2, .. d1];
```

These forms seem to "read" reasonable well.  In all those cases, the code is "creating a collection expression,
'with' the following arguments to pass along to control the final instance, and then the subsequent elements
used to populate it.  For example, the first line "creates a list of strings 'with' a capacity of two times the
count of the values about to be spread into it"

Importantly, this code has little chance of being overlooked like with forms such as: `[arg; element]`, while
also adding minimal verbosity, with a large amount of flexibility to pass any desired arguments along.

This would *technically* be a breaking change as `with(...)` *could* have been a call to a pre-existing method
called `with`.  However, unlike `new(...)` which is a *known* and recommended way to create implicitly-typed
values, `with(...)` is far less likely as a method name, running afoul of .Net naming for methods.  In the
unlikely event that a user did have such a method, they would certainly be able to continue calling into the
existing method by using `@with(...)`.

We would translate this `with(...)` element like so:

```c#
Dictionary<string, int> nameToAge1 = [with(StringComparer.OrdinalIgnoreCase), ...]; // translates to:

// argument_list *becomes* the argument list for the
// constructor call. 
__result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // followed by normal initialization

// or:

ImmutableDictionary<string, int> nameToAge2 = [with(StringComparer.OrdinalIgnoreCase), ...]; // translates to:

// argument_list arguments are passed initially to the
// 'create method'.
__result = ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase, /* key/values to initialize dictionary with */);

// or

IReadOnlyDictionary<string, int> nameToAge2 = [with(StringComparer.OrdinalIgnoreCase), ...]; // translates to:

// create synthesized dictionary with hashing/equality
// behavior determined by StringComparer.OrdinalIgnoreCase.
```

In other words, the argument_list arguments would be passed to the appropriate constructor if we are calling
a constructor, or to the appropriate 'create method' if we are calling such a method.  We would also allow a
single argument inheriting from the BCL *comparer* types to be provided when instantiating one of the destination
dictionary interface types to control its behavior.

## Option 2: `[args(...arguments...)]`

This form is effectively identical to the `with(...)` form, just using a slightly different identifier.  The
benefit here would primarily be around clearer identification of what is in the `(...)` section.  They are
clearly 'arguments' as 'args' states.

Of note: 'args' is *already* a contextual keyword in C#.  It was added as part of "top-level statements" to
allow top-level code to refer to the `string[]` arguments passed into the program.  So this form is effectively
identical to the `with(...)` just with a subjective preference on a different keyword.

This form seems to be even *less* likely to have any breaks versus `with(...)`.  A method called `with(...)`
and used in a collection expression is at least conceivable.  A method called `args(...)` feels like an even
lower realm of chance, making it even more acceptable to take the break.

Examples of this form are:

```c#
// With an existing type:

// Initialize to twice the capacity since we'll have to add
// more values later.
List<string> names = [args(capacity: values.Count * 2), .. values];

// With the dictionary types.
Dictionary<string, int> nameToAge1 = [args(comparer)];
Dictionary<string, int> nameToAge2 = [args(comparer), kvp1, kvp2, kvp3];
Dictionary<string, int> nameToAge3 = [args(comparer), k1:v1, k2:v2, k3:v4];
Dictionary<string, int> nameToAge4 = [args(comparer), .. d1, .. d2, .. d3];

Dictionary<string, int> nameToAge = [args(comparer), kvp1, k1: v2, .. d1];
```

These forms seem to "read" reasonable well.  In all those cases, the code is "creating a collection expression,
with the following 'args' to pass along to control the final instance, and then the subsequent elements used to
populate it.  For example, the first line "creates a list of strings with a capacity 'arg' of two times the count
of the values about to be spread into it"

## Option3: `[init(...arguments...)]`

Same as option 2, just with 'init' as the keyword chosen:

```c#
// With an existing type:

// Initialize to twice the capacity since we'll have to add
// more values later.
List<string> names = [init(capacity: values.Count * 2), .. values];

// With the dictionary types.
Dictionary<string, int> nameToAge1 = [init(comparer)];
Dictionary<string, int> nameToAge2 = [init(comparer), kvp1, kvp2, kvp3];
Dictionary<string, int> nameToAge3 = [init(comparer), k1:v1, k2:v2, k3:v4];
Dictionary<string, int> nameToAge4 = [init(comparer), .. d1, .. d2, .. d3];

Dictionary<string, int> nameToAge = [init(comparer), kvp1, k1: v2, .. d1];
```

## Option 4: `new(...arguments...) [...elements...]`

The design here would play off of how `new(...) { v1, v2, ... }` can already instantiate a target collection type
and supply initial collection values.  The arguments in the `new(...)` clause would be passed to the constructor if
creating a new instance, or as the initial arguments if calling a *create method*.  We would allow a single *comparer*
argument if creating a new `IDictionary<,>` or `IReadOnlyDictionary<,>`.

There are several downsides to this idea, as enumerated in the initial *weaknesses* section.  First, there is a
general concern around syntax appearing outside of the `[...]` section.  We want the `[...]` to be instantly 
recognizable, which is not the case if there is a `new(...)` appearing first.  Second, seeing the `new(...)` 
strongly triggers the view that this is simply an implicit-object-creation.  And, while somewhat true for the
case where a constructor *is* actually called (like for `Dictionary<,>`) it is misleading when calling a *create
method*, or creating an interface.  Finally, there is general apprehension around using `new` at all as there
is a feeling of redundancy around both the `new` indicating a new instance, *and* `[...]` indicating a new instance.

## Conversions

Collection arguments are *not* considered when determining *collection expression* conversions.

## Construction

Construction is updated as follows.

The elements of a collection expression are evaluated in order, left to right.
Within *collection arguments*, the arguments are evaluated in order, left to right.
Each element or argument is evaluated exactly once, and any further references refer to the results of this initial evaluation.

If *collection_arguments* is included and is not the first element in the collection expression, a compile-time error is reported.

If the *argument list* contains any values with *dynamic* type, a compile-time error is reported ([LDM-2025-01-22](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-01-22.md#conclusion-1)).

If the target type is a *struct* or *class type* that implements `System.Collections.IEnumerable`, and the target type does not have a *create method*, and the target type is not a *generic parameter type* then:
* [*Overload resolution*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1264-overload-resolution) is used to determine the best instance constructor from the *argument list*.
* If a best instance constructor is found, the constructor is invoked with the *argument list*.
  * If the constructor has a `params` parameter, the invocation may be in expanded form.
* Otherwise, a binding error is reported.

```csharp
// List<T> candidates:
//   List<T>()
//   List<T>(IEnumerable<T> collection)
//   List<T>(int capacity)
List<int> l;
l = [with(capacity: 3), 1, 2]; // new List<int>(capacity: 3)
l = [with([1, 2]), 3];         // new List<int>(IEnumerable<int> collection)
l = [with(default)];           // error: ambiguous constructor
```

If the target type is a type with a *create method*, then:
* The *argument list* is the `with()` *argument list* followed by a *collection expression* containing the elements only (no arguments).
* [*Overload resolution*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1264-overload-resolution) is used to determine the best factory method from the *argument list* from the [*create method candidates*](#create-method-candidates):
* If a best factory method is found, the method is invoked with the *argument list*.
  * If the factory method has a `params` parameter, the invocation may be in expanded form.
* Otherwise, a binding error is reported.

```csharp
MyCollection<string> c = [with(GetComparer()), "1", "2"];
// IEqualityComparer<string> _tmp1 = GetComparer();
// ReadOnlySpan<string> _tmp2 = ["1", "2"];
// c = MyBuilder.Create<string>(_tmp1, _tmp2);

[CollectionBuilder(typeof(MyBuilder), "Create")]
class MyCollection<T> { ... }

class MyBuilder
{
    public static MyCollection<T> Create<T>(ReadOnlySpan<T> elements);
    public static MyCollection<T> Create<T>(IEqualityComparer<T> comparer, ReadOnlySpan<T> elements);
}
```

If the target type is an *interface type*, then:
* [*Overload resolution*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1264-overload-resolution) is used to determine the best instance constructor from the *argument list* from the following candidate signatures:
  * If the target type is `IEnumerable<E>`, `IReadOnlyCollection<E>`, or `IReadOnlyList<E>`, the candidates are:
    * `new()`
  * If the target type is `ICollection<E>`, or `IList<E>`, the candidates are:
    * `new()`
    * `new(int capacity)`
  * If the target type is `IReadOnlyDictionary<K, V>`, the candidates are:
    * `new()`
    * `new(IEqualityComparer<K> comparer)`
  * If the target type is `IDictionary<K, V>`, the candidates are:
    * `new()`
    * `new(int capacity)`
    * `new(IEqualityComparer<K> comparer)`
    * `new(int capacity, IEqualityComparer<K> comparer)`
* If a best factory method is found, the method is invoked with the *argument list*.
* Otherwise, a binding error is reported.

```csharp
IDictionary<string, int> d;
IReadOnlyDictionary<string, int> r;

d = [with(StringComparer.Ordinal)]; // new Dictionary<string, int>(StringComparer.Ordinal)
r = [with(StringComparer.Ordinal)]; // new $PrivateImpl<string, int>(StringComparer.Ordinal)

d = [with(capacity: 2)]; // new Dictionary<string, int>(capacity: 2)
r = [with(capacity: 2)]; // error: 'capacity' parameter not recognized
```

If the target type is any other type, and the *argument list* is not empty, a binding error is reported.

```csharp
Span<int> a = [with(), 1, 2, 3]; // ok
Span<int> b = [with([1, 2]), 3]; // error: arguments not supported
```

### Create method candidates

For a collection expression where the target type *definition* has a `[CollectionBuilder]` attribute, the *create method candidates* for overload resolution are the following, **updated** from [*collection expressions: create methods*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#create-methods).

> A `[CollectionBuilder(...)]` attribute specifies the *builder type* and *method name* of a method to be invoked to construct an instance of the collection type.
> 
> The *builder type* must be a non-generic `class` or `struct`.
> 
> First, the set of applicable *create methods* `CM` is determined.
> It consists of methods that meet the following requirements:
> 
> * The method must have the name specified in the `[CollectionBuilder(...)]` attribute.
> * The method must be defined on the *builder type* directly.
> * The method must be `static`.
> * The method must be accessible where the collection expression is used.
> * The *arity* of the method must match the *arity* of the collection type.
> * The method must have a **last** parameter of type `System.ReadOnlySpan<E>`, passed by value.
> * There is an [*identity conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1022-identity-conversion), [*implicit reference conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1028-implicit-reference-conversions), or [*boxing conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1029-boxing-conversions) from the method return type to the *collection type*.
> 
> Methods declared on base types or interfaces are ignored and not part of the `CM` set.

> For a *collection expression* with a target type <code>C&lt;S<sub>0</sub>, S<sub>1</sub>, &mldr;&gt;</code> where the *type declaration* <code>C&lt;T<sub>0</sub>, T<sub>1</sub>, &mldr;&gt;</code> has an associated *builder method* <code>B.M&lt;U<sub>0</sub>, U<sub>1</sub>, &mldr;&gt;()</code>, the *generic type arguments* from the target type are applied in order &mdash; and from outermost containing type to innermost &mdash; to the *builder method*.

The key differences from the earlier algorithm are:
* Candidate methods may have additional parameters *before* the `ReadOnlySpan<E>` parameter.
* Multiple candidate methods are supported.

## Answered questions

### `dynamic` arguments

Should arguments with `dynamic` type be allowed? That might require using the runtime binder for overload resolution, which would make it difficult to limit the set of candidates, for instance for [collection builder cases](#construction-overloads-for-collection-builder-types).

**Resolution:** Disallowed. [LDM-2025-01-22](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-01-22.md#conclusion-1)

### `with()` breaking change

The proposed `with()` element is a breaking change.
```csharp
object x, y, z = ...;
object[] items = [with(x, y), z]; // C#13: ok; C#14: error args not supported for object[]

object with(object x, object y) { ... }
```

Confirm the breaking change is acceptable, and whether breaking change should be tied to language version.

**Resolution:** Keep previous behavior (no breaking change) when compiling with earlier language version. [LDM-2025-03-17](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-17)

### Should arguments affect collection expression conversion?

Should collection arguments and the applicable methods affect convertibility of the collection expression?
```csharp
Print([with(comparer: null), 1, 2, 3]); // ambiguous or Print<int>(HashSet<int>)?

static void Print<T>(List<T> list) { ... }
static void Print<T>(HashSet<T> set) { ... }
```

If the arguments affect convertibility based on the applicable methods, arguments should probably affect type inference as well.
```csharp
Print([with(comparer: StringComparer.Ordinal)]); // Print<string>(HashSet<string>)?
```

For reference, similar cases with target-typed `new()` result in errors.
```csharp
Print<int>(new(comparer: null));              // error: ambiguous
Print(new(comparer: StringComparer.Ordinal)); // error: type arguments cannot be inferred
```

**Resolution:** Collection arguments should be ignored in conversions and type inference. [LDM-2025-03-17](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-17)

### Collection builder method parameter order

For *collection builder* methods, should the span parameter be before or after any parameters for collection arguments?

Elements first would allow the arguments to be declared as optional.
```csharp
class MySetBuilder
{
    public static MySet<T> Create<T>(ReadOnlySpan<T> items, IEqualityComparer<T> comparer = null) { ... }
}
```

Arguments first would allow the span to be a `params` parameter, to support calling directly in expanded form.
```csharp
var s = MySetBuilder.Create(StringComparer.Ordinal, x, y, z);

class MySetBuilder
{
    public static MySet<T> Create<T>(IEqualityComparer<T> comparer, params ReadOnlySpan<T> items) { ... }
}
```

**Resolution:** The span parameter for elements should be the last parameter. [LDM-2025-03-12](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-12)

## Open questions

### Target types where arguments are *required*

Should collection expression conversions be supported to target types where arguments must be supplied because all of the constructors or factory methods require at least one argument?

Such types could be used with collection expressions that include explicit `with()` arguments but the types could not be used for `params` parameters.

For example, consider the following type constructed from a factory method:
```csharp
MyCollection<object> c;
c = [];                  // error: no arguments
c = [with(capacity: 1)]; // ok

[CollectionBuilder(typeof(MyBuilder), "Create")]
class MyCollection<T> : IEnumerable<T> { ... }

class MyBuilder
{
    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items, int capacity) { ... }
}
```

The same question applies for when the constructor is called directly as in the example below.
However, for the target types where the constructor is called directly, the collection expression *conversion* currently requires a constructor callable with no arguments. We would need to remove that conversion requirement to support such types.

```csharp
c = [];                  // error: no arguments
c = [with(capacity: 1)]; // error: no constructor callable with no arguments?

class MyCollection<T> : IEnumerable<T>
{
    public MyCollection(int capacity) { ... }
    public void Add(T t) { ... }
    // ...
}
```

### Arguments for *interface types*

Should arguments be supported for interface target types?

```csharp
ICollection<int> c = [with(capacity: 4)];
IReadOnlyDictionary<string, int> d = [with(comparer: StringComparer.Ordinal), ..values];
```

If so, which method signatures are used when binding the arguments?

For `ICollection<T>` and `IList<T>` should we use the accessible constructors from `List<T>`, or specific signatures independent from `List<T>`, say `new()` and `new(int capacity)`?

For `IDictionary<TKey, TValue>` should we use the accessible constructors from `Dictionary<TKey, TValue>`, or specific signatures, say `new()`, `new(int capacity)`, `new(IEqualityComparer<K> comparer)`, and `new(int capacity, IEqualityComparer<K> comparer)`?

What about `IReadOnlyDictionary<TKey, TValue>` which may be implemented by a synthesized type?

### Allow empty argument list for any target type

For target types such as *arrays* and *span types* that do not allow arguments, should an explicit empty argument list, `with()`, be allowed?

### Arguments with earlier language version

Is an error reported for `with()` when compiling with an earlier language version, or does `with` bind to another symbol in scope?

### `__arglist`

Should `__arglist` be supported in `with()` elements?

```csharp
class MyCollection : IEnumerable
{
    public MyCollection(__arglist) { ... }
    public void Add(object o) { }
}

MyCollection c;
c = [with(__arglist())];    // ok
c = [with(__arglist(x, y)]; // ok
```