# Collection expressions: inline collections

## Summary

Support collection expressions *inline* within spread elements and `foreach`.

## Detailed design

The following changes are made to the corresponding sections of the [*collection expressions proposal*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md).

### Conversions

The implicit *collection expression conversion* exists if the type has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ` and for each *element* `Eᵢ` in the collection expression one of the following holds:
* `Eᵢ` is an *expression element* and there is an implicit conversion from `Eᵢ` to `Tₑ`.
* `Eᵢ` is a *spread element* `..s` and `s` is *spreadable* as values of type `Tₑ`.

An expression `E` is *spreadable* as values of type `Tₑ` if one of the following holds:
* `E` is a *collection expression* and for each element `Eᵢ` in the collection expression there is an implicit conversion to `Tₑ`.
* `E` is a [*conditional expression*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1115-conditional-operator) `b ? x : y`, and `x` and `y` are *spreadable* as values of type `Tₑ`.
* `E` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Eₑ` and there is an implicit conversion from `Eₑ` to `Tₑ`.

*Should `switch` expressions be supported in spread elements?*

### Construction

For construction of a *collection expression*, ...

* For each element in order:
  * If the element is an *expression element* ...
  * If the element is a *spread element* then one of the following is used:
    * If the spread element expression is a *collection expression*, then the collection expression elements are evaluated in order as if the elements were in the containing collection expression.
    * If the spread element expression is a *conditional expression*, then the condition is evaluated and if `true` the second-, otherwise the third-operand is evaluated as if the operand was an element in the containing collection expression.
    * ...

### Type inference

> An *input type inference* is made *from* an expression `E` *to* a type `T` in the following way:
>
> * If `E` is a *collection expression* with elements `Eᵢ`, and `T` is a type with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ` or `T` is a *nullable value type* `T0?` and `T0` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ`, then for each `Eᵢ` a *collection element inference* is made *from* `Eᵢ` *to* `Tₑ`.
> * *[existing rules from first phase]* ...
>
> A *collection element inference* is made *from* a collection expression element `Eᵢ` *to* an *iteration type* `Tₑ` as follows:
> * If `Eᵢ` is an *expression element*, then an *input type inference* is made *from* `Eᵢ` *to* `Tₑ`.
> * If `Eᵢ` is a *spread element* `..s`, then a *spread element inference* is made *from* `s` *to* `Tₑ`.
>
> A *spread element inference* is made *from* an expression `E` to a collection expression *iteration type* `Tₑ` as follows:
> * If `E` is a *collection expression* with elements `Eᵢ`, then for each `Eᵢ` a *collection element inference* is made *from* `Eᵢ` *to* `Tₑ`.
> * If `E` is a [*conditional expression*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1115-conditional-operator) `b ? x : y`, then a *spread element inference* is made *from* `x` *to* `Tₑ` and a *spread element inference* is made *from* `y` *to* `Tₑ`.
> * If `E` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Eₑ`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Eₑ` *to* `Tₑ`.

