# Collection expressions: inline collections

## Summary

Support collection expressions *inline* in expression contexts where the collection type is not important or not observable.

## Motivation

Collection expressions could be used with spreads to allow adding elements *conditionally* to the containing collection:
```csharp
int[] array = [x, y, .. b ? [z] : []];
```

Collection expressions could be used directly in `foreach`:
```csharp
foreach (bool value in [false, true]) { }
```
In these cases, the *collection type* of the inline collection expression is unspecified and the choice of how or whether to instantiate the collection is left to the compiler.

## Detailed design

### Conversions

A *collection_type* is introduced to represent an enumerable type with a specific element type and an *unspecified* collection type.
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

The elements of a nested collection expression within a spread can be added to the containing collection instance without requiring an intermediate collection.

The [*construction*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#construction) section is updated as follows:

> * For each element in order:
>   * If the element is an *expression element* ...
>   * If the element is a *spread element* then one of the following is used:
>     * **If the spread element expression is a *collection expression*, then the collection expression elements are evaluated in order as if the elements were in the containing collection expression.**
>     * **If the spread element expression is a *conditional expression*, then the condition is evaluated and if `true` the second-, otherwise the third-operand is evaluated as if the operand was an element in the containing collection expression.**
>     * ...

*Is there any implied order of evaluation of the elements in the nested collection expression with respect to the following elements in the containing collection expression?*

### Type inference

No changes are made to the [*type inference*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference) section.
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

If the `foreach` statement has an *implicitly typed iteration variable*, the type of the *iteration variable* is the [*best common type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116315-finding-the-best-common-type-of-a-set-of-expressions) of the collection expression *elements*. If there is no best common type, an error is reported.

For a collection expression used as the collection in a `foreach` statement, the compiler may use any conforming representation for the collection instance, including eliding the collection.

*What are the order of operations? Particularly, what do we guarantee with respect to evaluating the loop body before evaluating subsequent elements?*
