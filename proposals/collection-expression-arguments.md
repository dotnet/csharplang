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
1. Does not imply calling a `new` constructor (when that isn't how all collections are created).
1. Does not imply creating/copying the values of the collection multiple times (like a postfix `with { ... }` might.
1. Does not contort order of operations, especially with C#'s consistent left-to-right expression evaluation ordering semantics.
   For example, it does not evaluate the arguments used to construct a collection *after* evaluating the expressions used to
   populate the collection.
1. Does not force a user to read to the end of a (potentially large) collection expression to determine core behavioral semantics.
   For example, having to see to the end of a hundred-line dictionary, only to find that, yes, it was using the right key comparer.
1. Is both not subtle, while also not being excessively verbose.  For example, using `;` instead of `,` to indicate
   arguments is a very easy piece of syntax to miss.  `with()` only adds 6 characters, and will easily stand out,
   especially with syntax coloring of the `with` keyword.
1. Reads nicely.  "This is a collection expression 'with' these arguments, consisting of these elements."
1. Solves the need for comparers for both dictionaries and sets.
1. Ensures any user need for passing arguments, or any needs we ourselves have beyond comparers in the future are already handled.
1. Does not conflict with any existing code (using https://grep.app/ to search).

A minor question exists if the preferred form would be `args(...)` or `init(...)` instead of `with(...)`.  But the forms are
otherwise identical.

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

These forms seem to "read" reasonably well.  In all those cases, the code is "creating a collection expression,
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

These forms seem to "read" reasonably well.  In all those cases, the code is "creating a collection expression,
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

#### Constructors

If the target type is a *struct* or *class type* that implements `System.Collections.IEnumerable`, and the target type does not have a *create method*, and the target type is not a *generic parameter type* then:
* [*Overload resolution*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1264-overload-resolution) is used to determine the best instance constructor from the candidates.
* The set of candidate constructors is all accessible instance constructors declared on the target type that are applicable with respect to the *argument list* as defined in [*applicable function member*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12642-applicable-function-member).
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

#### CollectionBuilderAttribute methods

If the target type is a type with a *create method*, then:
* [*Overload resolution*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1264-overload-resolution) is used to determine the best create method from the candidates.
* For each [*create method*](#create-methods) for the target type, we define a *projection method* with an identical signature to the create method but *without the last parameter*.
* The set of *candidate projection methods* is the projection methods that are applicable with respect to the *argument list* as defined in [*applicable function member*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12642-applicable-function-member).
* If a best projection method is found, the corresponding create method is invoked with the *argument list* appended with a `ReadOnlySpan<T>` containing the elements.
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

#### Interface target type

If the target type is an *interface type*, then:
* [*Overload resolution*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1264-overload-resolution) is used to determine the best candidate method signature.
* The set of candidate signatures is the signatures below for the target interface that are applicable with respect to the *argument list* as defined in [*applicable function member*](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12642-applicable-function-member).

  |Interfaces|Candidate signatures|
  |:---:|:---:|
  |`IEnumerable<E>`<br>`IReadOnlyCollection<E>`<br>`IReadOnlyList<E>`|*None*|
  |`ICollection<E>`<br>`IList<E>`|`(int capacity)`|
  |`IReadOnlyDictionary<K, V>`|`(IEqualityComparer<K>? comparer)`|
  |`IDictionary<K, V>`|`(int capacity)`<br>`(IEqualityComparer<K>? comparer)`<br>`(int capacity, IEqualityComparer<K>? comparer)`|

* If a best method signature is found, a method with that signature *or an equivalent initialization* is invoked with the *argument list*.
* Otherwise, a binding error is reported.

```csharp
IDictionary<string, int> d;
IReadOnlyDictionary<string, int> r;

d = [with(StringComparer.Ordinal)]; // new Dictionary<string, int>(StringComparer.Ordinal)
r = [with(StringComparer.Ordinal)]; // new $PrivateImpl<string, int>(StringComparer.Ordinal)

d = [with(capacity: 2)]; // new Dictionary<string, int>(capacity: 2)
r = [with(capacity: 2)]; // error: 'capacity' parameter not recognized
d = [with()];            // error: empty arguments not supported
```

#### Other target types

If the target type is any other type, then a binding error is reported for the *argument list*, even if empty.

```csharp
Span<int> a = [with(), 1, 2, 3]; // error: arguments not supported
Span<int> b = [with([1, 2]), 3]; // error: arguments not supported
```

### Create methods

For a collection expression where the target type *definition* has a `[CollectionBuilder]` attribute, the *create methods* are the following, **updated** from [*collection expressions: create methods*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#create-methods).

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
* Create methods may have additional parameters *before* the `ReadOnlySpan<E>` parameter.
* Multiple create methods are supported.

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

**Resolution:** Keep previous behavior (no breaking change) when compiling with earlier language version. [LDM-2025-03-17](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-17.md#conclusion)

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

**Resolution:** Collection arguments should be ignored in conversions and type inference. [LDM-2025-03-17](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-17.md#conclusion-1)

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

**Resolution:** The span parameter for elements should be the last parameter. [LDM-2025-03-12](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-12.md#conclusion-1)

### Arguments with earlier language version

Is an error reported for `with()` when compiling with an earlier language version, or does `with` bind to another symbol in scope?

**Resolution:** No breaking change for `with` inside a collection expression when compiling with earlier language versions. [LDM-2025-03-17](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-03-17.md#conclusion)

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

However, for the target types where the constructor is called directly, the collection expression *conversion* currently **requires a constructor callable with no arguments**, but the collection *arguments* are ignored when determining convertibility.

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

**Resolution:** Support conversions to target types where all constructors or factory methods require arguments, and require `with()` for the conversion. [LDM-2025-03-05](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-14.md#conclusion-2)

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

**Resolution:** No support for `__arglist` in collection arguments unless free. [LDM-2025-03-05](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-14.md#conclusion-3)

### Arguments for *interface types*

Should arguments be supported for interface target types?

```csharp
ICollection<int> c = [with(capacity: 4)];
IReadOnlyDictionary<string, int> d = [with(comparer: StringComparer.Ordinal), ..values];
```

<details>
If so, which method signatures are used when binding the arguments?

For **mutable** interface types, the options are:
1. Use the accessible constructors from the well-known type required for instantation: `List<T>` or `Dictionary<K, V>`.
1. Use signatures independent of specific type, for instance using `new()` and `new(int capacity)` for `ICollection<T>` and `IList<T>` (see [*Construction*](#construction) for potential signatures for each interface).

Using the accessible constructors from a well-known type has the following implications:
- Parameter names, optional-ness, `params`, are taken from the parameters directly.
- All accessible constructors are included, even though that may not be useful for collection expressions, such as `List(IEnumerable<T>)` which would allow `IList<int> list = [with(1, 2, 3)];`.
- The set of constructors may depend on the BCL version.

Recomendation: Use the accessible constructors from the well-known types.  We have guaranteed we would use these types, so this just 'falls out' and is the clearest and simplest path to constructing these values.  


For **non-mutable** interface types, the options are similar:
1. Do nothing.  This 
1. Use signatures independent of specific type, although the only scenario may be `new(IEqualityComparer<K> comparer)` for `IReadOnlyDictionary<K, V>` for C#14..


Using accessible constructors from some well known type (the strategy for mutable-interface-types) is not viable as there is no relation to any particular existing type, and the final type we may use and/or synthesize.  As such, there would have to be odd new requirements that the compiler be able to map any existing constructor of said type (even as it evolves) over to the non-mutable instance it actually generates.

Recomendation: Use signatures independent of a specific type.  And, for C# 14, only support `new(IEqualityComparer<K> comparer)` for `IReadOnlyDictionary<K, V>` as that is the only non-mutable interface where we feel it is critical for usability/semantics to allow users to provide this.  Future C# releases can consider expanding on this set based on solid justifications provided.
</details>

**Resolution:** https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-23.md

Arguments are supported for interface target types.  For both mutable and non-mutable interfaces the set of arguments will be curated.  

The expected list (which still needs to be LDM ratified) is [Interface target type](#Interface-target-type)


## Open questions

### Empty argument lists

Should we allow empty argument lists for some or all target types?

An empty `with()` would be equivalent to no `with()`. It might provide some consistency with non-empty cases, but it wouldn’t add any new capability.

The meaning of an empty `with()` might be clearer for some target types than others:
- For types where **constructors** are used, call the applicable constructor with no arguments.
- For types with **`CollectionBuilderAttribute`**, call the applicable factory method with elements only.
- For **interface types**, construct the well-known or implementation-defined type with no arguments.
- However, for **arrays** and **spans**, where collection arguments are not otherwise supported, `with()` may be confusing.

```csharp
List<int>           l = [with()]; // ok? new List<int>()
ImmutableArray<int> m = [with()]; // ok? ImmutableArray.Create<int>()

IList<int>       i = [with()]; // ok? new List<int>() or equivalent
IEnumerable<int> e = [with()]; // ok?

int[]     a = [with()]; // ok?
Span<int> s = [with()]; // ok?
```
