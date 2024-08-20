# Better conversion from collection expression element

## Summary

Updates to the better conversion rules to be more consistent with `params`, and better handle current ambiguity scenarios. For example, `ReadOnlySpan<string>` vs `ReadOnlySpan<object>` can currently
cause ambiguities during overload resolution for `[""]`.

## Detailed Design

The following are the better conversion from expression rules. These replace the rules in https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#overload-resolution.

These rules are:

> Given an implicit conversion `C₁` that converts from an expression `E` to a type `T₁`, and an implicit conversion `C₂` that converts from an expression `E` to a type `T₂`, `C₁` is a ***better conversion*** than `C₂` if one of the following holds:
> **`E` is a *collection expression*, and `C₁` is a ***better collection conversion from expression***, or**
> * **`E` is not a *collection expression* and one of the following holds:**
>   * `E` exactly matches `T₁` and `E` does not exactly match `T₂`
>   * `E` exactly matches both or neither of `T₁` and `T₂`, and `T₁` is a [*better conversion target*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11646-better-conversion-target) than `T₂`
> * `E` is a method group, ...

We add a new definition for ***better collection conversion from expression***, as follows:

Given:
- `E` is a collection expression with element expressions `[EL₁, EL₂, ..., ELₙ]`
- `T₁` and `T₂` are collection types
- `T'₁`:
    - If `T₁` is a generic type, `T₁` with all type parameters unsubstituted
    - If `T₁` is an array type, an unsubstituted array type
    - Otherwise, `T₁`
- `T'₂` is the same as `T'₁`, except for using `T₂` instead of `T₁`
- `E₁` is the element type of `T₁`
- `E₂` is the element type of `T₂`
- `CE₁ᵢ` are the series of conversions from `ELᵢ` to `E₁`
- `CE₂ᵢ` are the series of conversions from `ELᵢ` to `E₂`

`T'₁` is at least as good as `T'₂` if there is an identity conversion between them.  
`T'₁` is better than `T'₂` if:
- `T'₁` is ReadOnlySpan<T>, and `T'₂` is not ReadOnlySpan<T>
- `T'₁` is Span<T>, and `T'₂` is an *array_or_array_interface*
- `T'₁` is implicitly convertible to `T'₂`, and `T'₂` is not implicitly convertible to `T'₁`.
  
Otherwise, `T'₁` neither as good as nor better than `T'₂`.

If `E₁` and `E₂` have an identity conversion, then the element conversions are as good as each other. Otherwise, the element conversions to `E₁` are better than the element conversions to `E₂` if:
- For every `ELᵢ`, `CE₁ᵢ` is at least as good as `CE₂ᵢ`, and
- There is at least one i where `CE₁ᵢ` is better than `CE₂ᵢ`
  
Otherwise, neither set of element conversions is better than the other, and they are also not as good as each other.  
Conversion comparisons are made using better conversion from expression if `ELᵢ` is not a spread element. If `ELᵢ` is a spread element, we use better conversion from the element type of the spread collection to `E₁` or `E₂`, respectively.

`T₁` is a ***better collection conversion from expression*** than `T₂` if:
- `T₁` is implicitly convertible to `T₂`, and `T₂` is not implicitly convertible to `T₁`, or
- `T'₁` is better than `T'₂`, and the element conversions to `E₁` are at least as good as the element conversions to `E₂`, or
- `T'₁` is at least as good as `T'₂`, and the element conversions to `E₁` are better than the element conversions to `E₂`.

> [!NOTE]
> We include the `T₁` to `T₂` conversion as a first step because otherwise, this scenario would fail:
> ```cs
> public void C<TUnrelated> : IEnumerable<int> { public void Add(int i) {} }
>
> void M(IEnumerable<int> e) {}
> void M(C<string> c) {}
> M([1, 2, 3]);
> ```
> There is an open question below on whether it needs to go further.

### Scenarios:

In plain English, the collection types themselves must be either the same, or unambiguously better (ie, `List<T>` and `List<T>` are the same, `List<T>` is unambiguously better than `IEnumerable<T>`, and `List<T>` and `HashSet<T>` cannot be compared), and
the element conversions for the better collection type must also be the same or better (ie, we can't decide between `ReadOnlySpan<object>` and `Span<string>` for `[""]`, the user needs to make that decision). More examples of this are:


| `T₁` | `T₂` | `E` | `C₁` Conversions | `C₂` Conversions | `T'₁` vs `T'₂` | `CE₁ᵢ` vs `CE₂ᵢ` | Outcome |
|--------|--------|------------|----------------|----------------|---------------------|---------------------|---------|
| `List<int>` | `List<byte>` | `[1, 2, 3]` | `[Identity, Identity, Identity]` | `[Implicit Constant, Implicit Constant, Implicit Constant]` | `T'₁` is as good as `T'₂` | `CE₁ᵢ` is better | `List<int>` is picked |
| `List<int>` | `List<byte>` | `[(int)1, (byte)2]` | `[Identity, Implicit Numeric]` | Not applicable | `T'₁` is as good as `T'₂` | `T₂` is not applicable | `List<int>` is picked |
| `List<int>` | `List<byte>` | `[1, (byte)2]` | `[Identity, Implicit Numeric]` | `[Implicit Constant, Identity]` | `T'₁` is as good as `T'₂` | Neither is better | Ambiguous |
| `List<int>` | `List<byte>` | `[(byte)1, (byte)2]` | `[Implicit Numeric, Implicit Numeric]` | `[Identity, Identity]` | `T'₁` is as good as `T'₂` | `CE₂ᵢ` is better | `List<byte>` is picked |
| `List<int?>` | `List<long>` | `[1, 2, 3]` | `[Implicit Nullable, Implicit Nullable, Implicit Nullable]` | `[Implicit Numeric, Implicit Nullable, Implicit Nullable]` | `T'₁` is as good as `T'₂` | Neither is better | Ambiguous |
| `List<int?>` | `List<ulong>` | `[1, 2, 3]` | `[Implicit Nullable, Implicit Nullable, Implicit Nullable]` | `[Implicit Numeric, Implicit Numeric, Implicit Numeric]` | `T'₁` is as good as `T'₂` | `CE₁ᵢ` is better | `List<int?>` is picked |
| `List<short>` | `List<long>` | `[1, 2, 3]` | `[Implicit Numeric, Implicit Numeric, Implicit Numeric]` | `[Implicit Numeric, Implicit Numeric, Implicit Numeric]` | `T'₁` is as good as `T'₂` | `CE₁ᵢ` is better | `List<short>` is picked |
| `IEnumerable<int>` | `List<byte>` | `[1, 2, 3]` | `[Identity, Identity, Identity]` | `[Implicit Constant, Implicit Constant, Implicit Constant]` | `T'₂` is better than `T'₁` | `CE₁ᵢ` is better | Ambiguous |
| `IEnumerable<int>` | `List<byte>` | `[(byte)1, (byte)2]` | `[Implicit Numeric, Implicit Numeric]` | `[Identity, Identity]` | `T'₂` is better than `T'₁` | `CE₂ᵢ` is better | `List<byte>` is picked |
| `int[]` | `List<byte>` | - | - | - | Neither is better | - | Ambiguous |
| `ReadOnlySpan<string>` | `ReadOnlySpan<object>` | `["", "", ""]` | `[Identity, Identity, Identity]` | `[Implicit Reference, Implicit Reference, Implicit Reference]` | `T'₁` is as good as `T'₂` | `CE₁ᵢ` is better | `ReadOnlySpan<string>` is picked |
| `ReadOnlySpan<string>` | `ReadOnlySpan<object>` | `["", new object()]` | Not applicable | `[Implicit Reference, Identity]` | `T'₁` is as good as `T'₂` | `T₁` is not applicable | `ReadOnlySpan<object>` is picked |
| `ReadOnlySpan<object>` | `Span<string>` | `["", ""]` | `[Implicit Reference]` | `[Identity]` | `T'₁` is better than `T'₂` | `CE₂ᵢ` is better | Ambiguous |
| `ReadOnlySpan<object>` | `Span<string>` | `[new object()]` | `[Identity]` | Not applicable | $`T'₁` is better than `T'₂` | `T₁` is not applicable | `ReadOnlySpan<object>` is picked |
| `ReadOnlySpan<InterpolatedStringHandler>` | `ReadOnlySpan<string>` | `[${1}]` | `[Interpolated String Handler]` | `[Identity]` | `T'₁` is as good as `T'₂` | `CE₁ᵢ` is better | `ReadOnlySpan<InterpolatedStringHandler>` is picked |
| `ReadOnlySpan<InterpolatedStringHandler>` | `ReadOnlySpan<string>` | `[${"blah"}]` | `[Interpolated String Handler]` | `[Identity]` - But constant | `T'₁` is as good as `T'₂` | `CE₂ᵢ` is better | `ReadOnlySpan<string>` is picked |
| `ReadOnlySpan<string>` | `ReadOnlySpan<FormattableString>` | `[${1}]` | `[Identity]` | `[Interpolated String]` | `T'₁` is as good as `T'₂` | `CE₂ᵢ` is better | `ReadOnlySpan<string>` is picked |
| `ReadOnlySpan<string>` | `ReadOnlySpan<FormattableString>` | `[${1}, (FormattableString)null]` | Not applicable | `[Interpolated String, Identity]` | `T'₁` is as good as `T'₂` | `T₁` isn't applicable | `ReadOnlySpan<FormattableString>` is picked |
| `HashSet<short>` | `Span<long>` | `[1, 2]` | `[Implicit Constant, Implicit Constant]` | `[Implicit Numeric, Implicit Numeric]` | Neither is better | `CE₁ᵢ` is better | Ambiguous |
| `HashSet<long>` | `Span<short>` | `[1, 2]` | `[Implicit Numeric, Implicit Numeric]` | `[Implicit Constant, Implicit Constant]` | Neither is better | `CE₂ᵢ` is better | Ambiguous |

## Open questions

### Should we include user-defined conversions in the compatibility check between `T'₁` and `T'₂`?

For example, should this work?

```cs
C<int>.M([1, 2]);

class C<T> : IEnumerable<T>
{
    public static implicit operator Span<T>(C<T> t) => new T[];

    public static void M(C<int> c) {}
    public static void M(Span<int> c) {}

    // Implementation of IEnumerable<T> and Add(T)
}
```

The element types are the same, so it's a simply a question of whether we recognize that there's user-defined relationship between `C<T>` and `Span<T>`; if we include the conversion, then `C<T>` is more specific than `Span<T>`, and
that's the one we choose. Otherwise, we say this is ambiguous.

### How far should we go in comparing base types

Consider this scenario:

```cs
class C<T> : IEnumerable<int> { public void Add(int i) { } }

static void M(IEnumerable<byte>) { }
static void M(C<string>) { }
M([1, 2, 3]);
```

By the current rules, `C<T>` and `IEnumerable<byte>` are not as good as each other, and neither is better.

* `T₁` - `C<string>`
* `T'₁` - `C<T>`
* `T₂` - `IEnumerable<int>`
* `T'₂` - `IEnumerable<T>`

`T₁` and `T₂` aren't related, and neither are `T'₁` or `T'₂`. One possibly way to solve this is to also go through every unsubstituted interface of `T'₁` and compare them with `T'₂`, but is this complexity worth it in this scenario?
