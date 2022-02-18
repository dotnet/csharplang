# `params Span<T>`

## Summary
Avoid heap allocation for implicit allocation of arrays in specific scenarios with `params` arguments.

## Motivation
`params` array parameters provide a convenient way to call a method that takes an arbitrary length list of arguments.
However, using an array type for the parameter means the compiler must implicitly allocate an array on the heap at each call site.

If we extend `params` types to include the `ref struct` types `Span<T>` and `ReadOnlySpan<T>`, where values of those types cannot escape the call stack, the array at the call site may be created on the stack instead.

And if we're extending `params` to other types, we  could also allow `params IEnumerable<T>` to avoid allocating and copying collections at call sites that have an `IEnumerable<T>` rather than `T[]`.

The benefits of `params ReadOnlySpan<T>` and `params Span<T>` are primarily for new APIs. Existing commonly used APIs such as `Console.WriteLine()` and `StringBuilder.AppendFormat()` already have overloads that avoid array allocations for common cases and those overloads would need to be retained for backward compatibility.
```csharp
public static class Console
{
    public static void WriteLine(string value);
    public static void WriteLine(string format, object arg0);
    public static void WriteLine(string format, object arg0, object arg1);
    public static void WriteLine(string format, object arg0, object arg1, object arg2);
    public static void WriteLine(string format, params object[] arg);
}
```

## Detailed design

### Extending `params`
`params` parameters will be supported with types `Span<T>`, `ReadOnlySpan<T>`, and `IEnumerable<T>`.

A call in _expanded form_ ([§11.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member)) to a method with a `params T[]` or `params IEnumerable<T>` parameter will result in an array `T[]` allocated on the heap.

A call in _expanded form_ ([§11.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member)) to a method with a `params ReadOnlySpan<T>` or `params Span<T>` parameter will result in an array `T[]` created on the stack _if the `params` array is within limits (if any) set by the compiler_.
Otherwise the array will be allocated on the heap.

```csharp
Console.WriteLine(fmt, x, y, z); // WriteLine(string format, params ReadOnlySpan<object?> arg)
```

The compiler will report an error when compiling the method declaring the `params` parameter if the `ReadOnlySpan<T>` or `Span<T>` parameter value is returned from the method or assigned to an `out` parameter.
That ensures call-sites can create the underlying array on the stack and reuse the array across call-sites without concern for aliases.

A `params` parameter must be last parameter in the method signature.

Two overloads cannot differ by `params` modifier alone.

`params` parameters will be marked in metadata with a `System.ParamArrayAttribute` regardless of type.

### Overload resolution
Overload resolution will continue to prefer overloads that are applicable in _normal form_ ([§11.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member)) rather than _expanded form_ ([§11.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member)).

For overloads that are applicable in _expanded form_, better function member ([§11.6.4.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11643-better-function-member)) will be updated to prefer `params` types in a specific order:

> When performing this evaluation, if `Mp` or `Mq` is applicable in its expanded form, then `Px` or `Qx` refers to a parameter in the expanded form of the parameter list.
> 
> In case the parameter type sequences `{P1, P2, ..., Pn}` and `{Q1, Q2, ..., Qn}` are equivalent (i.e. each `Pi` has an identity conversion to the corresponding `Qi`), the following tie-breaking rules are applied, in order, to determine the better function member.
> 
> *  If `Mp` is a non-generic method and `Mq` is a generic method, then `Mp` is better than `Mq`.
> *  ...
> *  **Otherwise, if both methods have `params` parameters and are applicable only in their expanded forms, and the `params` types are distinct types with equivalent element type (there is an identity conversion between element types), the more specific `params` type is the first of:**
>    *  **`ReadOnlySpan<T>`**
>    *  **`Span<T>`**
>    *  **`T[]`**
>    *  **`IEnumerable<T>`**
> *  Otherwise if one member is a non-lifted operator and  the other is a lifted operator, the non-lifted one is better.
> *  Otherwise, neither function member is better.

### Array creation expressions
Array creation expressions that are target-typed to `ReadOnlySpan<T>` or `Span<T>` will be created on the stack _if the length of the array is a constant value within limits (if any) set by the compiler_.
Otherwise the array will be allocated on the heap.

```csharp
Span<int> s = new[] { i, j, k };   // int[] on the stack
WriteLine(fmt, new[] { x, y, z }); // object[] on the stack for WriteLine(string fmt, ReadOnlySpan<object> args);
```

### Array re-use
The compiler _may_ reuse an implicitly allocated array across multiple uses within a single thread executing a method:
- At the same call-site (within a loop) or
- At distinct call-sites if the lifetime of the spans do not overlap, and the array length is sufficient, and
  - the element types are managed types that are considered identical by the runtime, or
  - the element types are unmanaged types of the same size.

An implicitly allocated array may be reused regardless of whether the array was created on the stack or the heap.

### Lowering implicit allocation
For the `params` and array creation cases above that are target typed to `Span<T>` or `ReadOnlySpan<T>`, the compiler will lower the creation of spans using an efficient approach, specifically avoiding heap allocations when possible.
The exact details are still to be determined and may differ based on the target framework and runtime.

The guarantee the compiler gives is the span will be the expected size and will contain the expected items at any point in user code.

## Open issues
### Is `params Span<T>` necessary?
Is there a reason to support `params` parameters of type `Span<T>` in addition to `ReadOnlySpan<T>`? Is allowing mutation within the `params` method useful?

### Is `params IEnumerable<T>` necessary?
If the compiler allows `params ReadOnlySpan<T>`, then new APIs that require `params` could use `params ReadOnlySpan<T>` instead of `params T[]` because `T[]` is implicitly convertible to `ReadOnlySpan<T>`. And existing APIs could add a `params ReadOnlySpan<T>` overload where the existing `params T[]` simply delegates to the new overload.

There is no conversion from `IEnumerable<T>` to `ReadOnlySpan<T>` however, so allowing `params IEnumerable<T>` is essentially asking APIs to provide two overloads for `params` methods: `params ReadOnlySpan<T>` and `params IEnumerable<T>`. 

Are scenarios for `params IEnumerable<T>` sufficiently compelling to justify that?

### Array limits
The compiler may use heuristics to determine when to fallback to heap allocation for the underlying data for spans.
If heuristics are necessary, experimentation should establish the limits we agree on.

### Lowering approach
We need to determine the particular approach used to lower `params` and array creation expressions to avoid heap allocation.

For instance, one potential approach to represent a `Span<T>` of constant length `N` is to synthesize a `struct` with `N` fields of type `T`
where the layout and alignment of the fields matches the alignment of elements in `T[]`, and create the `Span<T>` from a `ref` to the first field of the `struct`.

With that approach, `Console.WriteLine(fmt, x, y, z);` would be emitted as:
```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct __ValueArray3<T> { public T Item1, Item2, Item3; };

var values = new __ValueArray3<object>() { Item1 = x, Item2 = y, Item3 = z };
var span = MemoryMarshal.CreateSpan(ref values.Item1, 3);
Console.WriteLine(fmt, (ReadOnlySpan<object>)span); // WriteLine(string format, params ReadOnlySpan<object?> arg)
```

Alternative approaches may require runtime support.

### Explicit `stackalloc`
Should we allow explicit stack allocation of arrays of managed types with `stackalloc` as well?
```csharp
public static ImmutableArray<TResult> Select<TSource, TResult>(this ImmutableArray<TSource> source, Func<TSource, TResult> map)
{
    int n = source.Length;
    Span<TResult> result = n <= 16 ? stackalloc TResult[n] : new TResult[n];
    for (int i = 0; i < n; i++)
        result[i] = map(source[i]);
    return ImmutableArray.Create(result); // requires ImmutableArray.Create<T>([DoesNotEscape] ReadOnlySpan<T> items)
}
```

This would require runtime support for stack allocation of arrays of non-constant length and any type, and GC tracking of the elements.

Direct runtime support for stack allocation of arrays of managed types might be useful for lowering implicit allocation as well.

The GC does not currently track the lifetime of a `stackalloc` array so if the contents of the array have a shorter lifetime than the method, the compiler will need to zero the contents of the array so the lifetime of elements matches expectations.

### Opting out
Should we allow opt-ing out of _implicit allocation_ on the call stack?
Perhaps an attribute that can be applied to a method, type, or assembly.

## Related proposals
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/blob/main/proposals/format.md#extending-params
