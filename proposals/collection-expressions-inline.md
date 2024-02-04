# Collection expressions: inline collections

## Summary

Support collection expressions *inline* in expression contexts where the *collection type* is not observable.

## Motivation

Inline collection expressions could be used with spreads to allow adding elements *conditionally* to the containing collection:
```csharp
int[] items = [x, y, .. b ? [z] : []];
```

Inline collection expressions could be used directly in `foreach`:
```csharp
foreach (var b in [false, true]) { }
```
In these cases, the *collection type* of the inline collection expression is unspecified. The choice of how or whether to instantiate the collection is left to the compiler.

## Detailed design

### Conversions

To support conversions for inline collection expressions, an abstract *collection_type* is introduced.

A *collection_type* represents an enumerable type with a specific element type and an *unspecified* collection type.
A *collection_type* with element type `E` is referred to here as *`col<E>`*.

A *collection_type* exists at compile time only; a *collection_type* cannot be referenced in source or metadata.

The [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) of a *collection_type* `col<E>` is `E`.

An implicit *collection_type conversion* exists from a type with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ` to the *collection_type* `col<Tₑ>`.

The collection expression [*conversions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#conversions) section is updated as follows:

> An implicit *collection expression conversion* exists from a collection expression to the following types:
> * **A *collection_type* `col<T>`**
> * A single dimensional *array type* `T[]`
> * ...
> 
> The implicit *collection expression conversion* exists if the type has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ` where for each *element* `Eᵢ` in the collection expression:
> * If `Eᵢ` is an *expression element*, there is an implicit conversion from `Eᵢ` to `Tₑ`.
> * If `Eᵢ` is a *spread element* `..Sᵢ`, there is an implicit conversion **from `Sᵢ` to the *collection_type* `col<Tₑ>`**.

### Construction

The elements of a nested collection expression within a spread can be added to the containing collection instance directly, without instantiating an intermediate collection.

The elements of the nested collection expression are evaluated in order within the nested collection. There is no implied evaluation order between the elements of the nested collection and other elements within the containing collection expression.

### Type inference

No changes are made for [*type inference*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference).
Since *type inference* from a spread element relies on the *iteration type* of the spread element expression, and since collection expressions do not have a *type* or an *iteration type*, there is no type inference from a collection expression nested within a spread element.

The relevant part of the [*type inference*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference) section is included here for reference:

> An *input type inference* is made *from* an expression `E` *to* a type `T` in the following way:
>
> * If `E` is a *collection expression* with elements `Eᵢ`, and `T` is a type with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ` or `T` is a *nullable value type* `T0?` and `T0` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ`, then for each `Eᵢ`:
>   * If `Eᵢ` is an *expression element*, then an *input type inference* is made *from* `Eᵢ` *to* `Tₑ`.
>   * If `Eᵢ` is a *spread element* `..Sᵢ`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from*  the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) of `Sᵢ` *to* `Tₑ`.
> * *[existing rules from first phase]* ...

### Foreach

The collection in a `foreach` statement may be a *collection expression*.

If the `foreach` statement has an *explicitly typed iteration variable* of type `Tₑ`, the compiler verifies the following for each element `Eᵢ` in the collection expression and reports an error otherwise:
* If `Eᵢ` is an *expression element*, there is an implicit conversion from `Eᵢ` to `Tₑ`.
* If `Eᵢ` is a *spread element* `..Sᵢ`, there is an implicit conversion from `Sᵢ` to the *collection_type* `col<Tₑ>`.

If the `foreach` statement has an *implicitly typed iteration variable*, the type of the *iteration variable* is the [*best common type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116315-finding-the-best-common-type-of-a-set-of-expressions) of the types of the elements. If there is no *best common type*, an error is reported.

For each element `Eᵢ` in the collection expression, the type if any contributing to the *best common type* is the following:
* If `Eᵢ` is an *expression element*, the type of `Eᵢ`.
* If `Eᵢ` is a *spread element* `..Sᵢ`, the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) of `Sᵢ`.

Since collection expressions do not have a *type* or an *iteration type*, the *best common type* does not consider elements from nested collection expressions.

For a collection expression used as the collection in a `foreach` statement, the compiler may use any conforming representation for the collection instance, including eliding the collection.

The elements of the collection are evaluated in order, and the loop body is executed for each element in order. The compiler *may* evaluate subsequent elements before executing the loop body for preceding elements.

## Alternatives

### Natural type

Add collection expression *natural type*.

The natural type would be a combination of the *best common type* for the element type (see [foreach](#foreach) above), and a choice of collection type, such as one of the following:

|Collection type|Mutable|Allocations|Async code|Returnable
|:---:|:---:|:---:|:---:|:---:|
|T[]|Items only|1|Yes|Yes|
|List&lt;T&gt;|Yes|2|Yes|Yes|
|Span&lt;T&gt;|Items only|0/1|No|No/Yes|
|ReadOnlySpan&lt;T&gt;|No|0/1|No|No/Yes|
|Memory&lt;T&gt;|Items only|0/1|Yes|No/Yes|
|ReadOnlyMemory&lt;T&gt;|No|0/1|Yes|No/Yes|

Natural type would allow use of collection expressions in cases where there is no target type:
```csharp
var a = [1, 2, 3];                    // var
var b = [x, y].Where(e => e != null); // extension methods
var c = Identity([x, y]);             // type inference

static T Identity<T>(T t) => t;
```

Natural type would likely only apply when there is a *best common type* for the elements.
```csharp
var d = [];        // error
var e = [default]; // error: no type for default
var f = [1, null]; // error: no common type for int and <null>
```

Natural type would also allow a subset of the scenarios that are supported above. But since the above proposal relies on target typing and natural type relies on *best common type* of the elements within the collection expression, there will be some limitations if we don't also support target typing with nested collection expressions.
```csharp
byte[] x = [1, .. b ? [2] : []];       // error: cannot convert int to byte
int[]  y = [1, .. b ? [default] : []]; // error: no type for [default]
int?[] z = [1, .. b ? [2, null] : []]; // error: no common type for int and <null>
```
