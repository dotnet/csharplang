# Collection expressions: spread inline collections

## Summary

Support collection expressions *inline* within spread elements.

## Motivation

Allow collection expressions to contain conditional elements by using spreads of *conditional expressions with nested collection expressions*.

## Detailed design

To support spreads of conditional expressions with nested collection expressions, the following changes are made to the [*collection expressions specification*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md).

### Conversions

For an implicit *collection expression conversion* to a target type, the values yielded from any *spread elements* must be implicitly convertible to the *iteration type* of the target type.

The [*conversions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#conversions) section is updated as follows:

> The implicit *collection expression conversion* exists if the type has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ` and for each *element* `Eᵢ` in the collection expression one of the following holds:
> * `Eᵢ` is an *expression element* and there is an implicit conversion from `Eᵢ` to `Tₑ`.
> * **`Eᵢ` is a *spread element* `..s` and `s` is *spreadable* as values of type `Tₑ`.**
> 
> **An expression `E` is *spreadable* as values of type `Tₑ` if one of the following holds:**
> * **`E` is a *collection expression* and for each element `Eᵢ` in the collection expression there is an implicit conversion to `Tₑ`.**
> * **`E` is a [*conditional expression*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1115-conditional-operator) `b ? x : y`, and `x` and `y` are *spreadable* as values of type `Tₑ`.**
> * `E` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Eₑ` and there is an implicit conversion from `Eₑ` to `Tₑ`.

*Should `switch` expressions be supported in spread elements?*

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

Type inference sees through spreads, conditional expressions, and nested collection expressions to make inferences from the elements within the nested collection expressions.

The [*type inference*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#type-inference) section is updated as follows, with some refactoring in addition to **new rules**:

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
> * **If `E` is a *collection expression* with elements `Eᵢ`, then for each `Eᵢ` a *collection element inference* is made *from* `Eᵢ` *to* `Tₑ`.**
> * **If `E` is a [*conditional expression*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1115-conditional-operator) `b ? x : y`, then a *spread element inference* is made *from* `x` *to* `Tₑ` and a *spread element inference* is made *from* `y` *to* `Tₑ`.**
> * If `E` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Eₑ`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Eₑ` *to* `Tₑ`.

### Foreach

A *collection expression* may be used as the collection in a `foreach` statement.

If the `foreach` statement has an *explicitly typed iteration variable* of type `Tₑ`, the compiler verifies the following for each element `Eᵢ` and reports an error otherwise:
* `Eᵢ` is an *expression element* and there is an implicit conversion from `Eᵢ` to `Tₑ`.
* `Eᵢ` is a *spread element* `..s` and `s` is *spreadable* as values of type `Tₑ`.

*The verification above is exactly the requirement for *conversions*. We should re-use the verification statement rather than restating here.*

If the `foreach` statement has an *implicitly typed iteration variable*, the type of the *iteration variable* is the [*best common type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116315-finding-the-best-common-type-of-a-set-of-expressions) of the collection expression *elements*. If there is no best common type, an error is reported.

*State the rules for *best common type* using the recursive iteration of elements and any nested collection expressions.*

*The [*best common type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116315-finding-the-best-common-type-of-a-set-of-expressions) is defined using *output type inference* from each expression. Do we need to update *output type inference* to infer from spread elements that contain nested collection expressions? Why didn't we need inference from spread elements previously without nested collection expressions?*

For a collection expression used as the collection in a `foreach` statement, the compiler may use any conforming representation for the collection instance, including eliding the collection.

*What are the order of operations? Particularly, what do we guarantee with respect to evaluating the loop body before evaluating subsequent elements?*