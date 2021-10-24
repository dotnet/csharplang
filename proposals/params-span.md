# Stack allocation of arrays of managed types

## Summary
Allow explicit stack allocation of short-lived arrays regardless of type, similar to the current support for arrays of managed types using `stackalloc` and `Span<T>`.
And allow implicit stack allocation of arrays, including `params` arguments, in cases where the arrays are only used as `ReadOnlySpan<T>` or `Span<T>`.

## Explicit stack allocation
Explicit stack allocation will be supported for arrays of any type with `stackalloc`.

The expression `stackalloc T[length]` will allocate `T[]` on the call stack unconditionally, regardless of `length`.
The type of the expression is `Span<T>` unless `T` is an _unmanaged type_ target-typed to `T*`.

`length` must be a non-negative `int` but does not need to be compile-time constant.

```csharp
public static ImmutableArray<TResult> Select<TSource, TResult>(this ImmutableArray<TSource> source, Func<TSource, TResult> map)
{
    int n = source.Length;
    Span<TResult> result = n <= 16 ? stackalloc TResult[n] : new TResult[n];
    for (int i = 0; i < n; i++)
        result[i] = map(source[i]);
    return ImmutableArray.Create(result); // requires ImmutableArray.Create<T>(ReadOnlySpan<T>)
}
```

### Lowering `stackalloc`
The runtime will provide a `StackAlloc<T>(int length)` method that unconditionally allocates an array of `T[length]` on the call stack.
```csharp
namespace System.Runtime.CompilerServices
{
    public static class RuntimeHelpers
    {
        public static Span<T> StackAlloc<T>(int length);
    }
}
```

A call to `Console.WriteLine(fmt, stackalloc object[] { x, y, z });` could be emitted as:
```csharp
var span = RuntimeHelpers.StackAlloc<object>(3);
span[0] = x;
span[1] = y;
span[2] = z;
Console.WriteLine(fmt, (ReadOnlySpan<object>)span); // WriteLine(string format, params ReadOnlySpan<object?> arg)
```

_Can we use `localloc` for managed types?_

## Implicit stack allocation
Stack allocation of arrays is extended to implicit allocations as well.
In particular, array creation expressions that are target-typed to `ReadOnlySpan<T>` or `Span<T>` will be allocated on the stack if:
- The length of the array is a constant integer <= 8 (the actual limit is arbitrary), and
- The call-site has not explicitly _opted-out_ of implicit stack allocation.

```csharp
Span<int> s = new[] { i, j, k }; // stack allocation of int[]
WriteLine(fmt, new[] { x, y });  // stack allocation of object[] for WriteLine(string fmt, ReadOnlySpan<object> args);
```

### `params Span<T>`
The C# compiler will support parameters declared as `params ReadOnlySpan<T>` and `params Span<T>`.

_Include the restrictions from `params T[]`._

A call in _expanded_ form to a method with `params ReadOnlySpan<T>` or `params Span<T>` parameter will result in an array `T[]` allocated on the stack if the following hold; otherwise the array will be allocated on the heap.
- The number of arguments in the `params` array is <= 8 (the actual limit is arbitrary), and
- The call-site has not explicitly _opted-out_ of implicit stack allocation.

The compiler will report an error for the method declaring the `params` parameter if the `ReadOnlySpan<T>` or `Span<T>` parameter value is returned from the method or assigned to an `out` parameter.
That ensures call-sites can allocate the underlying array on the stack and reuse the array across call-sites without concern for aliases.

### Overload resolution
Overload resolution will continue to prefer overloads that are applicable in _normal_ form rather than _expanded_ form.

For overloads that are applicable in _expanded_ form, overload resolution will prefer `params` parameter types in the following order.
1. `params ReadOnlySpan<T>`
2. `params Span<T>`
3. `params T[]`

```csharp
Console.WriteLine(fmt, x, y);       // WriteLine(string format, object? arg0, object? arg1)
Console.WriteLine(fmt, x, y, z, w); // WriteLine(string format, params ReadOnlySpan<object?> arg)
```

If there are two applicable overload `M1` and `M2` with `params` element types `T1` and `T2` but otherwise identical signatures, the preferred overload is `M1` if the preferred type is `T1` regardless of whether the span type in `M1` is preferred over the span type in `M2`. _Quote from spec here._

### Opt-in and opt-out
To enforce stack allocation at a call-site, use `stackalloc` explicitly.
```csharp
Span<int> s = stackalloc[] { 1, 2, ..., 100 };
Console.WriteLine(fmt, stackalloc[] { x, y, z });
```

To opt-out of implicit stack allocation at all call-sites within a method, the compiler will support an attribute such as `[MethodImpl(MethodImplOptions.NoImplicitStackAlloc)]`, and a similar attribute for an entire type or assembly.

### Lowering implicit allocation
There are several approaches that have been discussed for lowering implicit stack allocations.

#### Approach 1: Value type array
Implicit stack allocation is limited to arrays where the length is known at compile-time. 
For an array of length `N`, the compiler can generate an equivalent `struct` with at least `N` fields of type `T` where the layout and alignment matches the alignment of an equivalent `T[]`, and a `Span<T>` can be created from the `ref` to the first field in the `struct`.

The _array_ of `N` argument values on the callstack will be a `struct` with at least `N` fields of type `T` where the layout and alignment of the fields matches the layout and alignment of the equivalent `T[]`.

The `struct` type could be an `internal` synthesized type shared across call-sites in the compilation with the same number of arguments.
The synthesized type will have an unspeakable name.
If `T` is a valid type argument, the synthesized type will be a constructed generic type.

A call to `Console.WriteLine(fmt, x, y, z);` could be emitted as:
```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct $Values3<T> { public T Item1, Item2, Item3; };

var values = new $Values3<object>() { Item1 = x, Item2 = y, Item3 = z };
var span = MemoryMarshal.CreateSpan(ref values.Item1, 3);
Console.WriteLine(fmt, (ReadOnlySpan<object>)span); // WriteLine(string format, params ReadOnlySpan<object?> arg)
```

#### Approach 2: `StackAlloc<T>(int length)`
The runtime will provide a `StackAlloc<T>(int length)` method that unconditionally allocates an array of `T[length]` on the call stack.

To avoid repeated calls within loops, values returned from `StackAlloc<T>(int)` will need to be shared across calls, potentially requiring moving calls to `StackAlloc<T>(int)` ahead of any loops.

And even without loops there may be many distinct call-sites within a method (for instance, many sequential calls to `Console.WriteLine()`), so ideally allocations will be shared across distinct call-sites as well.

The heuristics for sharing allocations may get tricky and will almost certainly be opaque to the C# author however.

A call to `Console.WriteLine(fmt, x, y, z);` could be emitted as:
```csharp
var span = RuntimeHelpers.StackAlloc<object>(3);
span[0] = x;
span[1] = y;
span[2] = z;
Console.WriteLine(fmt, (ReadOnlySpan<object>)span); // WriteLine(string format, params ReadOnlySpan<object?> arg)
```

#### Approach 3: `StackAlloc<T>(int)` and `StackFree<T>(Span<T>)`
The runtime might provide a `StackAlloc<T>(int length)` method _and also a corresponding `StackFree<T>(Span<T> span)`.

A pair of methods would avoid the need to share allocations from approach #2.

A call to `Console.WriteLine(fmt, x, y, z);` could be emitted as:
```csharp
var span = RuntimeHelpers.StackAlloc<object>(3);
span[0] = x;
span[1] = y;
span[2] = z;
Console.WriteLine(fmt, (ReadOnlySpan<object>)span); // WriteLine(string format, params ReadOnlySpan<object?> arg)
RuntimeHelpers.StackFree(span);
```

## Related proposals
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/blob/main/proposals/format.md#extending-params
