# Collection expressions (updates)

## Summary
[summary]: #summary

Updates to [*collection expressions*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md) for C#13.

## Overload resolution
[overload-resolution]: #overload-resolution

[*Better conversion from expression*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11644-better-conversion-from-expression) is updated to prefer the target with the better conversion from **each element**.

> Given an implicit conversion `C₁` that converts from an expression `E` to a type `T₁`, and an implicit conversion `C₂` that converts from an expression `E` to a type `T₂`, `C₁` is a *better conversion* than `C₂` if one of the following holds:
>
> * `E` is a *collection expression*, and one of the following holds:
>   * **`T₁` and `T₂` have the same *collection type definition*, and `T₁` has *element type* `S₁`, and `T₂` has *element type* `S₂`, and both of the following hold:**
>     * **For each element `Eᵢ` in `E`:**
>       * **If `Eᵢ` is an expression element, the conversion from `Eᵢ` to `S₂` is not better than the conversion from `Eᵢ` to `S₁`**
>       * **If `Eᵢ` is a spread element with iteration type `Sᵢ`, the conversion from `Sᵢ` to `S₂` is not better than the conversion from `Sᵢ` to `S₁`**
>     * **For at least one element `Eᵢ` in `E`:**
>       * **If `Eᵢ` is an expression element, the conversion from `Eᵢ` to `S₁` is better than the conversion from `Eᵢ` to `S₂`**
>       * **If `Eᵢ` is a spread element with iteration type `Sᵢ`, the conversion from `Sᵢ` to `S₁` is better than the conversion from `Sᵢ` to `S₂`**
>
>   * `T₁` is `System.ReadOnlySpan<E₁>`, and `T₂` is `System.Span<E₂>`, and an ~~implicit~~ **identity** conversion exists from `E₁` to `E₂`
>   * `T₁` is `System.ReadOnlySpan<E₁>` or `System.Span<E₁>`, and `T₂` is an *array_or_array_interface* with *element type* `E₂`, and an ~~implicit~~ **identity** conversion exists from `E₁` to `E₂`
>   * `T₁` is not a *span_type*, and `T₂` is not a *span_type*, and an implicit conversion exists from `T₁` to `T₂`
> * `E` is not a *collection expression* and one of the following holds:
>   * `E` exactly matches `T₁` and `E` does not exactly match `T₂`
>   * `E` exactly matches both or neither of `T₁` and `T₂`, and `T₁` is a [*better conversion target*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11646-better-conversion-target) than `T₂`
> * `E` is a method group, ...

The updated rules apply with **language version 13+** only.

### Breaking changes

The existing rules, preferring `ReadOnlySpan<E₁>` over `System.Span<E₂>`, and preferring `{ReadOnly}Span<E₁>` over array or array interface of `E₂`, are now restricted to cases where there is identity conversion from `E₁` to `E₂`.