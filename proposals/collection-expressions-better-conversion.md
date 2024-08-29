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
- `E₁` is the element type of `T₁`
- `E₂` is the element type of `T₂`
- `CE₁ᵢ` are the series of conversions from `ELᵢ` to `E₁`
- `CE₂ᵢ` are the series of conversions from `ELᵢ` to `E₂`

If there is an identity conversion from `E₁` to `E₂`, then the element conversions are as good as each other. Otherwise, the element conversions to `E₁` are better than the element conversions to `E₂` if:
- For every `ELᵢ`, `CE₁ᵢ` is at least as good as `CE₂ᵢ`, and
- There is at least one i where `CE₁ᵢ` is better than `CE₂ᵢ`
Otherwise, neither set of element conversions is better than the other, and they are also not as good as each other.  
Conversion comparisons are made using better conversion from expression if `ELᵢ` is not a spread element. If `ELᵢ` is a spread element, we use better conversion from the element type of the spread collection to `E₁` or `E₂`, respectively.

`C₁` is a ***better collection conversion from expression*** than `C₂` if:
- `T₁` or `T₂` is not a *span type*, and `T₁` is implicitly convertible to `T₂`, and `T₂` is not implicitly convertible to `T₁`, or
- `E₁` does not have an identity conversion to `E₂`, and the element conversions to `E₁` are better than the element conversions to `E₂`, or
- `E₁` has an identity conversion to `E₂`, and one of the following holds:
   - `T₁` is `System.ReadOnlySpan<E₁>`, and `T₂` is `System.Span<E₂>`, or
   - `T₁` is `System.ReadOnlySpan<E₁>` or `System.Span<E₁>`, and `T₂` is an *array_or_array_interface* with *element type* `E₂`

Otherwise, neither collection type is better, and the result is ambiguous.

> [!NOTE]
> These rules mean that methods that expose overloads that take different element types and without a conversion between the collection types are ambiguous for empty collection expressions. As an example:
> ```cs
> public void M(ReadOnlySpan<int> ros) { ... }
> public void M(Span<int?> span) { ... }
>
> M([]); // Ambiguous
> ```

### Scenarios:

In plain English, the collection types themselves must be either the same, or unambiguously better (ie, `List<T>` and `List<T>` are the same, `List<T>` is unambiguously better than `IEnumerable<T>`, and `List<T>` and `HashSet<T>` cannot be compared), and
the element conversions for the better collection type must also be the same or better (ie, we can't decide between `ReadOnlySpan<object>` and `Span<string>` for `[""]`, the user needs to make that decision). More examples of this are:

| `T₁` | `T₂` | `E` | `C₁` Conversions | `C₂` Conversions | `CE₁ᵢ` vs `CE₂ᵢ` | Outcome |
|--------|--------|------------|----------------|----------------|---------------------|---------|
| `List<int>` | `List<byte>` | `[1, 2, 3]` | `[Identity, Identity, Identity]` | `[Implicit Constant, Implicit Constant, Implicit Constant]` | `CE₁ᵢ` is better | `List<int>` is picked |
| `List<int>` | `List<byte>` | `[(int)1, (byte)2]` | `[Identity, Implicit Numeric]` | Not applicable | `T₂` is not applicable | `List<int>` is picked |
| `List<int>` | `List<byte>` | `[1, (byte)2]` | `[Identity, Implicit Numeric]` | `[Implicit Constant, Identity]` | Neither is better | Ambiguous |
| `List<int>` | `List<byte>` | `[(byte)1, (byte)2]` | `[Implicit Numeric, Implicit Numeric]` | `[Identity, Identity]` | `CE₂ᵢ` is better | `List<byte>` is picked |
| `List<int?>` | `List<long>` | `[1, 2, 3]` | `[Implicit Nullable, Implicit Nullable, Implicit Nullable]` | `[Implicit Numeric, Implicit Numeric, Implicit Numeric]` | Neither is better | Ambiguous |
| `List<int?>` | `List<ulong>` | `[1, 2, 3]` | `[Implicit Nullable, Implicit Nullable, Implicit Nullable]` | `[Implicit Numeric, Implicit Numeric, Implicit Numeric]` | `CE₁ᵢ` is better | `List<int?>` is picked |
| `List<short>` | `List<long>` | `[1, 2, 3]` | `[Implicit Numeric, Implicit Numeric, Implicit Numeric]` | `[Implicit Numeric, Implicit Numeric, Implicit Numeric]` | `CE₁ᵢ` is better | `List<short>` is picked |
| `IEnumerable<int>` | `List<byte>` | `[1, 2, 3]` | `[Identity, Identity, Identity]` | `[Implicit Constant, Implicit Constant, Implicit Constant]` | `CE₁ᵢ` is better | `IEnumerable<int>` is picked |
| `IEnumerable<int>` | `List<byte>` | `[(byte)1, (byte)2]` | `[Implicit Numeric, Implicit Numeric]` | `[Identity, Identity]` | `CE₂ᵢ` is better | `List<byte>` is picked |
| `int[]` | `List<byte>` | `[1, 2, 3]` | `[Identity, Identity, Identity]` | `[Implicit Constant, Implicit Constant, Implicit Constant]` | `CE₁ᵢ` is better | `int[]` is picked |
| `ReadOnlySpan<string>` | `ReadOnlySpan<object>` | `["", "", ""]` | `[Identity, Identity, Identity]` | `[Implicit Reference, Implicit Reference, Implicit Reference]` | `CE₁ᵢ` is better | `ReadOnlySpan<string>` is picked |
| `ReadOnlySpan<string>` | `ReadOnlySpan<object>` | `["", new object()]` | Not applicable | `[Implicit Reference, Identity]` | `T₁` is not applicable | `ReadOnlySpan<object>` is picked |
| `ReadOnlySpan<object>` | `Span<string>` | `["", ""]` | `[Implicit Reference]` | `[Identity]` | `CE₂ᵢ` is better | `Span<string>` is picked |
| `ReadOnlySpan<object>` | `Span<string>` | `[new object()]` | `[Identity]` | Not applicable | `T₁` is not applicable | `ReadOnlySpan<object>` is picked |
| `ReadOnlySpan<InterpolatedStringHandler>` | `ReadOnlySpan<string>` | `[$"{1}"]` | `[Interpolated String Handler]` | `[Identity]` | `CE₁ᵢ` is better | `ReadOnlySpan<InterpolatedStringHandler>` is picked |
| `ReadOnlySpan<InterpolatedStringHandler>` | `ReadOnlySpan<string>` | `[$"{"blah"}"]` | `[Interpolated String Handler]` | `[Identity]` - But constant | `CE₂ᵢ` is better | `ReadOnlySpan<string>` is picked |
| `ReadOnlySpan<string>` | `ReadOnlySpan<FormattableString>` | `[$"{1}"]` | `[Identity]` | `[Interpolated String]` | `CE₂ᵢ` is better | `ReadOnlySpan<string>` is picked |
| `ReadOnlySpan<string>` | `ReadOnlySpan<FormattableString>` | `[$"{1}", (FormattableString)null]` | Not applicable | `[Interpolated String, Identity]` | `T₁` isn't applicable | `ReadOnlySpan<FormattableString>` is picked |
| `HashSet<short>` | `Span<long>` | `[1, 2]` | `[Implicit Constant, Implicit Constant]` | `[Implicit Numeric, Implicit Numeric]` | `CE₁ᵢ` is better | `HashSet<short>` is picked |
| `HashSet<long>` | `Span<short>` | `[1, 2]` | `[Implicit Numeric, Implicit Numeric]` | `[Implicit Constant, Implicit Constant]` | `CE₂ᵢ` is better | `Span<short>` is picked |

## Open questions

### How far should we prioritize `ReadOnlySpan`/`Span` over other types?

As specified today, the following overloads would be ambiguous:

```cs
C.M1(["Hello world"]); // Ambiguous, no tiebreak between ROS and List
C.M2(["Hello world"]); // Ambiguous, no tiebreak between Span and List

C.M3(["Hello world"]); // Ambiguous, no tiebreak between ROS and MyList.

C.M4(["Hello", "Hello"]); // Ambiguous, no tiebreak between ROS and HashSet. Created collections have different contents

class C
{
    public static void M1(ReadOnlySpan<string> ros) {}
    public static void M1(List<string> list) {}

    public static void M2(Span<string> ros) {}
    public static void M2(List<string> list) {}

    public static void M3(ReadOnlySpan<string> ros) {}
    public static void M3(MyList<string> list) {}

    public static void M4(ReadOnlySpan<string> ros) {}
    public static void M4(HashSet<string> hashset) {}
}

class MyList<T> : List<T> {}
```

How far do we want to go here? The `List<T>` variant seems reasonable, and subtypes of `List<T>` exist aplenty. But the `HashSet` version has very different semantics, how sure are we that it's actually "worse"
than `ReadOnlySpan` in this API?
