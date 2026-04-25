# Extension members on typeless receivers

## Summary

Allow extension members to be invoked on a receiver expression that has no type:

```cs
// All errors today.
ImmutableArray<int> a = [1, 2, 3].ToImmutableArray();
var memoized = SomeMethod.Memoize();
var x = (cond ? null : GetInt()).SomeNullableExtension();
```

Resolution treats the receiver as the first argument of each candidate extension member, then picks the best applicable candidate by the existing overload-resolution rules. Receivers that already have a type bind exactly as today.

## Motivation

Collection expressions ([proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md)) are the primary motivating scenario. A collection expression has no type, so it cannot be the receiver of any extension member, even where the conversion from the collection expression to the extension's first parameter type is well-defined and the extension member is the canonical way to express the operation:

```cs
// All of these are binding-time errors today.
var a = [1, 2, 3].ToImmutableArray();
var b = [1, 2, 3].ToList();
var c = ["one", "two"].ToHashSet();
```

The workaround today is to spell out the target type with a cast (or with a typed declaration target):

```cs
var a = (ImmutableArray<int>)[1, 2, 3];
```

That works, but it forces the developer to write the full target type at the point of construction even when generic inference on the called method would otherwise determine it. The cost grows with the genericity of the type:

```cs
// Today.
var values = (ImmutableArray<Some<Complex, Type>>)[x, y, z];

// Proposed; the type argument of ToImmutableArray<T> is inferred from x, y, z.
var values = [x, y, z].ToImmutableArray();
```

The same shape arises for other expressions that have no type. These are not the headline driver, but the rule we propose is uniform across all such expressions, so they fall out at no additional spec cost. LDM can dial individual categories back via the [open question](#which-receiver-categories-are-supported) below if any prove unwanted.

```cs
// Lambdas whose parameter types are not specified.
var memoized1 = ((x, y) => Compute(x, y)).Memoize();

// Method groups.
var memoized2 = SomeMethod.Memoize();

// Conditional expressions whose arms do not have a common type.
var x = (cond ? null : GetInt()).SomeNullableExtension();
```

LDM has previously identified this as a separate track to explore, distinct from solving the collection-expression case in isolation ([LDM-2023-07-12](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-12.md)). This proposal is that track.

## Detailed design

The following updates are presented as a diff against [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) of the C# 7 standard, and against the [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) section of the [C# 14 extension members](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md) proposal.

Throughout this section, ~~strikethrough~~ indicates text being removed from the existing specification, and **bold** indicates text being added. Unchanged prose is quoted verbatim for context.

### Extension method invocations

The receiver-side eligibility constraint in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) is relaxed to admit a receiver expression that does not have a type. No new conversion form is introduced; the standard's existing expression-to-type implicit conversions ([§10.2.7 null literal](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1027-null-literal-conversions), [§10.2.13 tuple expression](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10213-implicit-tuple-conversions), [§10.2.15 anonymous function and method group](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10215-anonymous-function-conversions-and-method-group-conversions), [§10.2.16 default literal](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10216-default-literal-conversions), [§10.2.17 implicit throw](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10217-implicit-throw-conversions)) already cover every typeless expression form admitted today, and the applicability check on the third eligibility bullet (defined in [§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12642-applicable-function-member)) already invokes them. The only change is that the fourth eligibility bullet, which adds a separate "identity, reference, or boxing" requirement on the receiver, no longer applies when *expr* does not have a type.

The prose in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) is updated as follows.

> An extension method `Cᵢ.Mₑ` is ***eligible*** if:
>
> - `Cᵢ` is a non-generic, non-nested class
> - The name of `Mₑ` is *identifier*
> - `Mₑ` is accessible and applicable when applied to the arguments as a static method as shown above
> - ~~An implicit identity, reference or boxing conversion exists from *expr* to the type of the first parameter of `Mₑ`.~~ **One of the following holds:**
>   - ***expr* has a type, and an implicit identity, reference or boxing conversion exists from *expr* to the type of the first parameter of `Mₑ`.**
>   - ***expr* does not have a type.**

The remaining prose of [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) is unchanged. In particular, the rule that extension methods do not apply when *expr* or any of the *args* has compile-time type `dynamic` is unaffected, because an expression that does not have a type also does not have compile-time type `dynamic`.

> *Example*: Given the standard library declaration
>
> ```csharp
> public static class Enumerable
> {
>     public static ImmutableArray<T> ToImmutableArray<T>(this IEnumerable<T> source) { ... }
> }
> ```
>
> the expression
>
> ```csharp
> [1, 2, 3].ToImmutableArray()
> ```
>
> is processed as follows. Member access in [§12.8.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1287-member-access) falls through to extension method invocation ([§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations)), because `[1, 2, 3]` is not classified as a namespace, a type, or a value, variable, property access, or indexer access whose type is a *type*. In [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations), the candidate `Enumerable.ToImmutableArray<T>` is eligible:
>
> - `Enumerable` is a non-generic, non-nested class.
> - The name of the candidate is `ToImmutableArray`.
> - The candidate is accessible and applicable as the static call `Enumerable.ToImmutableArray([1, 2, 3])`: type inference yields `T` = `int`, and an implicit conversion from `[1, 2, 3]` to `IEnumerable<int>` exists.
> - `[1, 2, 3]` does not have a type, satisfying the second branch of the receiver bullet.
>
> Overload resolution then selects this candidate, and the result is a value of type `ImmutableArray<int>`.
>
> *end example*

### Extension member consumption

### Interactions with other features

## Back-compat analysis

## Drawbacks

## Alternatives

## Design decisions

## Open LDM questions

### Which receiver categories are supported?

## Related discussions

## Design meetings
