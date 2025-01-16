# Collection expression arguments

Champion issue: https://github.com/dotnet/csharplang/issues/8887

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

If *collection_arguments* is not the first element in the collection expression, a compile-time error is reported.

If the target type is a *struct* or *class type* that implements `System.Collections.IEnumerable`, and the target type does not have a *create method*, and the target type is not a *generic parameter type* then:
* [*Overload resolution*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1264-overload-resolution) is used to determine the best instance constructor from the *argument list*.
  * If the *argument list* contains any values with *dynamic* type, the best instance constructor is determined at runtime.
* If a best instance constructor is found, the constructor is invoked with the *argument list*.
  * If the constructor has a `params` parameter, the invocation may be in expanded form.
* Otherwise, a binding error is reported.

If the target type is a type with a *create method*, then:
* The *argument list* is a concatenation of *some `ReadOnlySpan<T>` value (TBD)* and any explicit *argument list*.
* [*Overload resolution*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1264-overload-resolution) is used to determine the best factory method from the *argument list*.
  * If the *argument list* contains any values with *dynamic* type, the best factory method is determined at runtime.
* If a best factory method is found, the method is invoked with the *argument list*.
  * If the constructor has a `params` parameter, the invocation may be in expanded form.
* Otherwise, a binding error is reported.

If the target type is an *interface type*, then:
* [*Overload resolution*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1264-overload-resolution) is used to determine the best instance constructor from the *argument list* from the following candidate signatures:
  * If the target type is `IEnumerable<E>`, `ICollection<E>`, or `IList<E>`, the candidates are:
    * `new()`
    * `new(int capacity)`
  * If the target type is `IReadOnlyCollection<E>`, or `IReadOnlyList<E>`, the candidates are:
    * `new()`
  * If the target type is `IDictionary<K, V>`, the candidates are:
    * `new()`
    * `new(int capacity)`
    * `new(IEqualityComparer<K, V> comparer)`
    * `new(int capacity, IEqualityComparer<K, V> comparer)`
  * If the target type is `IReadOnlyDictionary<K, V>`, the candidates are:
    * `new()`
    * `new(IEqualityComparer<K, V> comparer)`
  * If the *argument list* contains any values with *dynamic* type, the best instance constructor is determined at runtime.
* If a best factory method is found, the method is invoked with the *argument list*.
* Otherwise, a binding error is reported.

If the target type is any other type, and the *argument list* is not empty, a binding error is reported.

*Describe breaking changes resulting from using overload resolution with collection builders vs. C#12.*

## Open questions

1. Is an error reported for `with()` when compiling with an earlier language version, or does `with` bind to another symbol in scope?
