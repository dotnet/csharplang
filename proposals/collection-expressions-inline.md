# Collection expressions: inline collections

## Summary

Support collection expressions *inline* in expression contexts where the *collection type* is unspecified.

## Motivation

Inline collection expressions could be used with spreads to allow adding elements *conditionally* to the containing collection:
```csharp
int[] items = [x, y, .. b ? [z] : []];
```

Inline collection expressions could be used directly in `foreach`:
```csharp
foreach (bool b in [false, true]) { }
```
In these cases, the *collection type* of the inline collection expression is unspecified. The choice of how and whether to instantiate the collection is left to the compiler.

## Compile time *collection_type*
A *collection_type* is introduced that represents an enumerable type with a specific element type and an *unspecified* containing collection type.

A *collection_type* with element type `E` is referred to here as *`col<E>`*.

A *collection_type* exists at compile time only &mdash; a *collection_type* cannot be referenced in source or metadata.

The [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) of a *collection_type* `col<E>` is `E`.

A *collection_type* may be used as a target type or as a natural type for collection expressions.

## Target types

A *collection_type* can be used as a target type for collection expressions in spreads and `foreach`. For these scenarios, it is the target *iteration type* that is important; the target collection type is not observable.

### Conversions
[target-type-conversions]: #target-type-conversions

To enable a *collection_type* as a target type, a [*collection expression conversion*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#conversions) is defined for *collection_type*:

> An implicit *collection expression conversion* exists from a collection expression to the following types:
> * **A *collection_type* `col<T>`**
> * A single dimensional *array type* `T[]`
> * ...
> 
> The implicit conversion exists if the type has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ` where for each *element* `Eᵢ` in the collection expression:
> * If `Eᵢ` is an *expression element*, there is an implicit conversion from `Eᵢ` to `Tₑ`.
> * If `Eᵢ` is a *spread element* `..Sᵢ`, there is an implicit conversion from the *iteration type* of `Sᵢ` to `Tₑ`.

### Spreads
[target-type-spreads]: #target-type-spreads

If a spread element `..S` is contained in a collection expression with a target type with *iteration type* `E`, then the target type of the *expression* `S` is `col<E>`.
The target type is only used when the spread element expression does not have a type.
If the target type is used, an error is reported if the spread element expression is not implicitly convertible to `col<E>`.

For a collection expression within a spread, the compiler may use any conforming representation for the nested collection instance, including eliding the collection instance and instead adding the elements to the containing collection instance directly.

The elements of a collection expression within a spread are evaluated in order as if the spread elements were declared in the containing collection expression directly.

### Foreach
[target-type-foreach]: #target-type-foreach

If a `foreach` statement has an *explicitly typed iteration variable* of type `E`, the target type of the `foreach` collection is `col<E>`.
The target type is only used when the collection does not have a type.
If the target type is used, an error is reported if the collection is not implicitly convertible to `col<E>`.

For a collection expression in a `foreach` collection, the compiler may use any conforming representation for the collection instance, including eliding the collection instance.

The elements of a collection expression in a `foreach` are evaluated in order, and the loop body is executed for each element in order. The compiler *may* evaluate subsequent elements before executing the loop body for preceding elements.

## Natural type

A *collection_type* can be used as a natural type for collection expressions in scenarios where only the *iteration type* of the collection expression is used, and where the containing collection is not observable.

The *natural type* of a collection expression is the *collection_type* `col<E>` where `E` is the [*best common type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116315-finding-the-best-common-type-of-a-set-of-expressions) of the elements.
For each element `Eᵢ` in the collection expression, the type contributed to the *best common type* is the following:
* If `Eᵢ` is an *expression element*, the contribution is the *type* of `Eᵢ`. If `Eᵢ` does not have a type, there is no contribution.
* If `Eᵢ` is a *spread element* `..Sᵢ`, the contribution is the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) of `Sᵢ`. If `Sᵢ` does not have an *iteration type*, there is no contribution.

If there is no *best common type* of the elements, the collection expression has no natural type.

Natural type is not a replacement for target type &mdash; there are scenarios supported by target type but not natural type. That said, we could choose to support either target type or natural type, or both.
```csharp
// conditional expression:
// - target type col<byte>: ok
// - natural type col<int>: error: cannot convert int to byte
byte[] a = [1, 2, .. b ? [3] : []];
```

### Conversions
[natural-type-conversions]: #natural-type-conversions

To allow *collection_types* to be used in *best common type* &mdash; for example  `b ? [x] : [y]` &mdash; a conversion is defined between *collection_types*:

> An implicit *collection_type conversion* exists from the *collection_type* `col<T>` to the *collection_type* `col<U>` if there is an implicit conversion from `T` to `U`.

### Foreach
[natural-type-foreach]: #natural-type-foreach

The primary scenario for natural type may be `foreach`.

If the `foreach` statement has an *implicitly typed iteration variable*, the type of the *iteration variable* is the *iteration type* of the collection. If the iteration type of the collection cannot be determined, an error is reported.
```csharp
foreach (var i in [1, 2, 3]) { } // ok: col<int>
foreach (var i in []) { }        // error: cannot determine type
foreach (var i in [1, null]) { } // error: no common type for int, <null>
```

The `foreach` scenario could be extended to include conditional expressions, switch expressions, and spreads within collection expressions:
```csharp
foreach (var i in b ? [x] : [y, z]) { }
foreach (var i in b switch { true => [x], false => [y, z]}) { }
foreach (var i in [x, .. b ? [y, z] : []]) { }
```

## Observable collection instances

The discussion of natural type above covers scenarios where the collection instance is not observable.
We could go further and support scenarios where the collection type is unspecified but where the choice of collection type is observable.
For those cases, the compiler will need to instantiate a concrete collection type.

```csharp
var a = [x, y];                       // var
var b = [x, y].Where(e => e != null); // extension methods
var c = Identity([x, y]);             // type inference: T Identity<T>(T)
```

The following are several potential collection types:

|Collection type|Mutable|Allocations|Async code|Returnable
|:---:|:---:|:---:|:---:|:---:|
|T[]|Items only|1|Yes|Yes|
|List&lt;T&gt;|Yes|2|Yes|Yes|
|Span&lt;T&gt;|Items only|0/1|No|No/Yes|
|ReadOnlySpan&lt;T&gt;|No|0/1|No|No/Yes|
|Memory&lt;T&gt;|Items only|1|Yes|Yes|
|ReadOnlyMemory&lt;T&gt;|No|1|Yes|Yes|
