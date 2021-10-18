# Stack allocation of `params Span<T>`

## Summary
Allow calling a method with a `params` parameter without allocating an array on the heap.

## Motivation
A `params` array parameter allows callers to pass an arbitrary length list of arguments to the method without explicitly creating an array. Instead, the compiler is responsible for allocating and initializing an array for the trailing arguments.

For instance, a call to `Console.WriteLine(fmt, x, y, z, w);` is emitted as:
```csharp
// public static void WriteLine(string format, params object?[]? arg)
Console.WriteLine(fmt, new object[4] { x, y, z, w });
```
However, the implicit array is allocated on the heap.

To avoid the array allocation, some commonly used methods such as `Console.WriteLine()` include overloads with fixed numbers of arguments in addition to the `params` array overload.
```csharp
public static void WriteLine(string value);
public static void WriteLine(string format, object? arg0);
public static void WriteLine(string format, object? arg0, object? arg1);
public static void WriteLine(string format, params object?[]? arg);
```

Ideally, the compiler should allocate the `params` array on the callstack rather than the heap when the compiler can ensure the value does not escape the method being called.

## Detailed design
### Additional `params` types
The C# compiler will support parameters declared as `params ReadOnlySpan<T>` and `params Span<T>`.

_List the restrictions from `params T[]`._ 

The compiler will report an error if the `ReadOnlySpan<T>` or `Span<T>` parameter value is returned from the method or assigned to an `out` parameter.
That ensures call-sites can allocate the underlying _array_ for the span on the callstack and reuse the underlying _array_ without concern for aliases.

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

### Lowering
If a call in _expanded_ form binds to a method with `params ReadOnlySpan<T>` or `params Span<T>` parameter, the compiler will generate a `Span<T>` where the `ref` field of the span points to an _array_ of argument values of type `T` on the callstack.

The _array_ of `N` argument values on the callstack will be a `struct` with at least `N` fields of type `T` where the layout and alignment of the fields matches the layout and alignment of the equivalent `T[]`.

The `struct` type will be an `internal` synthesized type shared across call-sites in the compilation.
The synthesized type will have an unspeakable name.
If `T` is a valid type argument, the synthesized type will be a constructed generic type.

For instance, the call to `Console.WriteLine(fmt, x, y, z, w);` will be emitted as essentially:
```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct $Values4<T> { public T Item1, Item2, Item3, Item4; };

var values = new $Values4<object>() { Item1 = x, Item2 = y, Item3 = z, Item4 = w };
var span = MemoryMarshal.CreateSpan(ref values.Item1, 4);
Console.WriteLine(fmt, (ReadOnlySpan<object>)span); // WriteLine(string format, params ReadOnlySpan<object?> arg)
```

### Heap allocation
If the number of arguments in the `params` array is > 8 (an arbitrary limit), or if the compiler supports opting out of stack allocation, the compiler will allocate the array on the heap rather than the stack.
```csharp
var values = new object[] { x, y, ... };
Console.WriteLine(fmt, new ReadOnlySpan<object>(values)); // WriteLine(string format, params ReadOnlySpan<object?> arg)
```

No attempt will be made to share heap allocated array instances across calls to `params` methods.

### Opting out
The caller can opt out for a particular call by explicitly allocating the `params` argument:
```csharp
Console.WriteLine(fmt, new object[] { x, y, z, w }); // WriteLine(string, params object?[]?)
```

We could allow opting out of stack allocation for multiple calls by supporting an attribute such as `[MethodImpl(MethodImplOptions.NoStackAlloc)]` applied to the calling method, or a similar attribute applied to the containing type or assembly.
In those cases, any `params` arrays will be allocated on the heap.

### Alternative lowering
The runtime will provide a `StackAlloc<T>(int length)` method that allocates an array of `T[length]` on the call stack.
```csharp
namespace System.Runtime.CompilerServices
{
    public static class RuntimeHelpers
    {
        public static Span<T> StackAlloc<T>(int length);
    }
}
```

With `StackAlloc<T>(int)`, the call to `Console.WriteLine(fmt, x, y, z, w);` could be emitted as:
```csharp
var span = RuntimeHelpers.StackAlloc<object>(4);
span[0] = x;
span[1] = y;
span[2] = z;
span[3] = w;
Console.WriteLine(fmt, (ReadOnlySpan<object>)span); // WriteLine(string format, params ReadOnlySpan<object?> arg)
```

To avoid repeated calls within loops, values returned from `StackAlloc<T>(int)` will need to be shared across calls, potentially requiring moving calls to `StackAlloc<T>(int)` ahead of any loops.

## Future considerations: Explicit stack allocation
In addition to implicit stack allocation of `params` arrays, we could choose to support _explicit_ stack allocation of arrays of managed types.
```csharp
object[] args = stackalloc object[] { x, y, z, w };
Console.WriteLine(fmt, args);                           // error: 'args' may escape
Console.WriteLine(fmt, new ReadOnlySpan<object>(args)); // ok
```

Supporting explicit stack allocation is likely to overlap this proposal, but for now this proposal focuses on implicit allocations for `params` only.

## Related proposals
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/blob/main/proposals/format.md#extending-params
