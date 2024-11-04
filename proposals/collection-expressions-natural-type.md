# Collection expressions: natural type

## Summary

Infer a natural type for collection expressions based on the elements in the collection to allow using collection expressions in non-target-typed locations.

## Motivation

Collection expressions with a natural type could be used in implicitly typed locations.

```csharp
var a = [x, y];                       // var
var b = [x, y].Where(e => e != null); // extension methods
var c = Identity([x, y]);             // type inference: T Identity<T>(T)
```

Collection expressions could be used in spreads with `? :` to add elements conditionally to the containing collection.

```csharp
int[] items = [x, y, .. b ? [z] : []];
```

Collection expressions could be used in `foreach`.

```csharp
foreach (var i in [1, 2, 3]) { }
```

With spreads and `foreach`, the *collection type* of the collection expression is not directly observable, only the *element type* is observable.

## Natural type

The collection type to use for collection expressions is an open question. For now, we'll call that type *`col<E>`* where `E` is the *element type*.

The *element type* `E` is the [*best common type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116315-finding-the-best-common-type-of-a-set-of-expressions) of the elements `Eᵢ`:
* If `Eᵢ` is an *expression element*, the contribution is the *type* of `Eᵢ`. If `Eᵢ` does not have a type, there is no contribution.
* If `Eᵢ` is a *spread element* `..Sᵢ`, the contribution is the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) of `Sᵢ`. If `Sᵢ` does not have a type, there is no contribution.

If there is no *best common type*, the collection expression has no type.

```csharp
foreach (var i in [1, .. b ? [2, 3] : []]) { } // ok: col<int>
foreach (var i in []) { }                      // error: cannot determine type
foreach (var i in [1, null]) { }               // error: no common type for int, <null>
```

The compiler will use the same *collection type* for all collection expressions, although the *element type* `E` depends on the specific collection expression.

The choice of collection type has a few implications:
- **Mutability**: Can the collection instance or the elements be modified?
- **Allocations**: How many allocations are required to create the instance?
- **`IEnumerable<T>`**: Does the collection type implement `IEnumerable<T>`, and perhaps other collection interfaces?
- **Non-type arguments**: Does the collection type support elements that are not valid as type arguments, such as pointers or `ref struct`?
- **Async code**: Can the collection be used in `async` code or an iterator?

The table below includes some possible collection types, and implications for each type.

|Collection type|Mutable|Allocs|`IEnumerable<T>`|Non-type args|Async|Details|
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
|`T[]`|elements only|1|Yes|pointers|Yes| |
|`List<T>`|Yes|2|Yes|No|Yes| |
|`ReadOnlySpan<T>`|No|0/1|No|No|No|stack/heap allocated buffer|
|`ReadOnlyMemory<T>`|No|1|Yes|No|Yes|heap allocated buffer|
|`IEnumerable<T>`|No|1+|Yes|No|Yes|context-dependent implementation|
|*Anonymous type*|?|1+|Yes|Yes|Yes|compiler-generated type|

If the collection type of a collection expression is *not observable*, the compiler may use any conforming representation for the collection, including eliding the collection instance altogether.

## Breaking changes

Previously, collection expressions [*conversions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#conversions) relied on *conversion from expression* to a target type.
And previously, collection expression [*type inference*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference) relied on input and output type inference for the elements.
For scenarios where the natural type of the collection could be used instead, there is a potential breaking change.

If the natural type is `IEnumerable<T>`, the following is a breaking change due to the **conversion** from `List<int>` to `IEnumerable<int>`

```csharp
bool b = true;
List<int> x = [1, 2, 3];
var y = b ? x : [4]; // y: previously List<int>, now IEnumerable<int>
```

The following is a breaking change in overload resolution due to **type inference** from the natural type to `T`:

```csharp
Log([1, 2, 3]); // previously Log<T>(IEnumerable<T>), now ambiguous

void Log<T>(T item) { ... }
void Log<T>(IEnumerable<T> items) { ... }
```

## Open questions

### Collection type?

Which collection type should we use?

### Conversions from type?

Is the natural type considered for *conversion from type*?

For instance, can a collection expression with natural type be assigned to `object`?

```csharp
object obj = [1, 2, 3]; // convert col<int> to object?

[Value([1, 2, 3])]      // convert col<int> to object?
static void F() { }

class ValueAttribute : Attribute
{
    public ValueAttribute(object value) { }
}
```

### Target-type `foreach` collection?

Should the collection expression be target-typed when used in `foreach` with an *explicitly typed iteration variable*?

In short, if the iteration variable type is explicitly typed as `E`, should we use `col<E>` as the target type of the `foreach` expression?

```csharp
foreach (bool? b in [false, true, null]) { } // target type: col<bool?>?
foreach (byte b in [1, 2, 3]) { }            // target type: col<byte>?
```

### Target-type spread collection?

If a spread element is contained in a *target-typed* collection expression, should the spread element expression be target-typed?

In short, if the containing collection expression has a target type with *element type* `E`, should we use `col<E>` as the target type for any spread element expressions?

```csharp
int[] x = [1, ..[]];           // spread target type: col<int>?
object[] y = [2, ..[default]]; // spread target type: col<object>?
```

### Limit natural type to `foreach` and conditional expressions only?

Should we defer the decision on *collection type* and only support natural type scenarios where the collection expression type is not observable?