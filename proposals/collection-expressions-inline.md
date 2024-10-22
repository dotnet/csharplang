# Collection expressions: inline collections

## Summary

Infer a natural type for collection expressions based on the elements in the collection to allow using collection expressions in non-target-typed locations.

## Motivation

Collection expressions with a natural type could be used in implicitly-typed scenarios.

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

In the last two cases, the *collection type* of the collection expression is not directly observable, only the *element type* is observable.

## Natural type

The natural type of a collection expression is a *collection type* with *element type* `E`, where `E` is the [*best common type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116315-finding-the-best-common-type-of-a-set-of-expressions) of the elements `Eᵢ`:
* If `Eᵢ` is an *expression element*, the contribution is the *type* of `Eᵢ`. If `Eᵢ` does not have a type, there is no contribution.
* If `Eᵢ` is a *spread element* `..Sᵢ`, the contribution is the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) of `Sᵢ`.

If there is no *best common type*, the collection expression has no type.

```csharp
foreach (var i in [1, .. b ? [2, 3] : []]) { } // ok: col<int>
foreach (var i in []) { }        // error: cannot determine type
foreach (var i in [1, null]) { } // error: no common type for int, <null>
```

The compiler will use the same *collection type* for all collection expressions, although the *element type* `E` depends on the specific collection expression.

The actual collection type to use is an open question.

The table includes some possible collection types, and some implications for each type.

|Collection type|Mutable|Allocs|`IEnumerable<T>`|Non-type args|Async code|Notes|
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
|`T[]`|Items only|1|Yes|pointers|Yes| |
|`List<T>`|Yes|2|Yes|No|Yes| |
|`ReadOnlySpan<T>`|No|0/1|No|No|No|stack/heap allocated buffer|
|`ReadOnlyMemory<T>`|No|1|Yes|No|Yes|heap allocated buffer|
|`IEnumerable<T>`|No|1+|Yes|No|Yes|context-dependent implementation|
|*Anonymous type*|?|1+|Yes|Yes|Yes|compiler-generated type|

## Breaking changes

### Breaking change: conversions

Previously, collection expressions [*conversions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#conversions) relied on *conversion from expression* to a target type.
For scenarios where the natural type of the collection could be used instead, there is a potential breaking change.

If collection expression natural type uses `IEnumerable<T>`:
```csharp
bool b = true;
List<int> x = [1, 2, 3];
var y = b ? x : [4]; // y: previously List<int>, now IEnumerable<int>
```

### Breaking change: type inference

Previously, collection expression [*type inference*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference) relied on input and output type inference for the elements.
For scenarios where the natural type of the collection could be used instead, there is a potential breaking change.

```csharp
Log([1, 2, 3]); // previously Log<T>(IEnumerable<T>), now ambiguous

void Log<T>(T item) { ... }
void Log<T>(IEnumerable<T> items) { ... }
```

## Code generation

If the type of a collection expression is not observable, the compiler may use any conforming representation for the collection, including eliding the collection instance altogether.

### Spreads

The elements of a collection expression within a spread are evaluated in order as if the spread elements were declared in the containing collection expression directly.

### Foreach

The elements of a collection expression in a `foreach` are evaluated in order, and the loop body is executed for each element in order. The compiler *may* evaluate subsequent elements before executing the loop body for preceding elements.

## Open questions

### Collection type?

Which collection type should we use?

### Target-type `foreach` collection?

If a `foreach` statement has an *explicitly typed iteration variable* of type `E`, the target type of the `foreach` collection is `col<E>`.
If the target type is used, an error is reported if the collection is not implicitly convertible to `col<E>`.

Should the collection expression be target-typed when used in `foreach` with an explicitly-typed iteration variable?
```csharp
foreach (bool? b in [false, true, null]) { } // target type: col<bool?>?
foreach (byte b in [1, 2, 3]) { }            // target type: col<byte>?
```

### Target-type spread collection?

If a spread element `..S` is contained in a collection expression with a target type with *iteration type* `E`, then the target type of the *expression* `S` is `col<E>`.
If the target type is used, an error is reported if the spread element expression is not implicitly convertible to `col<E>`.

*What if the containing collection type uses `Add()` overloads? Should th nested element be bound to the best overload.*

Should the target *element type* of the containing collection be used as a target-type for nested spread collection expressions?

```csharp
int[] x = [1, ..[]];           // spread target type: col<int>?
object[] y = [2, ..[default]]; // spread target type: col<object>?
```

### Limit natural type to `foreach` and conditional expressions only?

Should we defer the decision on collection type for now and only support natural type scenarios where the collection expression type is not observable? Those cases, specifically `foreach` collections and spread elements that use conditional expressions, are arguably the most interesting, and those cases side-step the decision on which collection type to use.