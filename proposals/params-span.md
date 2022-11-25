# `params ReadOnlySpan<T>`

## Summary
Avoid heap allocation of implicitly allocated `params` arrays.

## Motivation
`params` array parameters provide a convenient way to call a method that takes an arbitrary length list of arguments.
However, using an array type for the parameter means the compiler must implicitly allocate an array on the heap at each call site.

If we extend `params` support to `ReadOnlySpan<T>`, where the implicitly created span cannot escape the calling method, the underlying array at the call site may be created on the stack instead.

If overload resolution prefers `params ReadOnlySpan<T>` over `params T[]`, then adding a `params ReadOnlySpan<T>` overload to an existing API would reduce allocations when recompiling callers.

The benefit of `params ReadOnlySpan<T>` is primarily for APIs that don't already include optimized overloads. Commonly used APIs such as `Console.WriteLine()` and `StringBuilder.AppendFormat()` that already have non-`params` overloads for callers with few arguments would benefit less.
```csharp
// Existing API with params and non-params overloads
public static class Console
{
    public static void WriteLine(string value);
    public static void WriteLine(string format, object arg0);
    public static void WriteLine(string format, object arg0, object arg1);
    public static void WriteLine(string format, object arg0, object arg1, object arg2);
    public static void WriteLine(string format, params object[] arg);
}

// New API with single overload
abstract class Logger
{
    public abstract void Log(string format, params ReadOnlySpan<object> args);
}
```

## Detailed design

### Extending `params`
A `params` parameter type may be `System.ReadOnlySpan<T>` for a valid type argument `T`.

A call in [_expanded form_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member) to a method with a `params ReadOnlySpan<T>` parameter will result in a `ReadOnlySpan<T>` instance implicit created by the compiler.

```csharp
log.Log("({0}, {1}, {2})", x, y, z);

// Potentially emitted as:
log.Log("({0}, {1}, {2})",
    new System.ReadOnlySpan<object>(new object[] { x, y, z }));
```

A `params` parameter must be the last parameter in the method signature and cannot include a `ref`, `out`, or `in` modifier.

Two overloads cannot differ by `params` modifier alone.

`params` parameters are marked in metadata with a `System.ParamArrayAttribute`.

A `params ReadOnlySpan<T>` is implicitly `scoped` unless explicitly annotated with `[UnscopedRef]`.
Within the `params` method, the compiler will use escape analysis to report diagnostics if the `scoped` span is captured or returned.

### Overload resolution
Overload resolution prefers overloads that are applicable in [_normal form_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member) over [_expanded form_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member).

[_Better function member_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11643-better-function-member) will prefer `params ReadOnlySpan<T>` over `params T[]` for overloads applicable in [_expanded form_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member):

> In case the parameter type sequences `{P1, P2, ..., Pn}` and `{Q1, Q2, ..., Qn}` are equivalent (i.e. each `Pi` has an identity conversion to the corresponding `Qi`), the following tie-breaking rules are applied, in order, to determine the better function member.
> 
> *  If `Mp` is a non-generic method and `Mq` is a generic method, then `Mp` is better than `Mq`.
> *  ...
> *  **Otherwise, if both methods have `params` parameters and are applicable only in their expanded forms, and the `params` types are distinct types with equivalent element type (there is an identity conversion between element types), the more specific `params` type is the first of:**
>    *  **`ReadOnlySpan<T>`**
>    *  **`T[]`**
> *  Otherwise if one member is a non-lifted operator and  the other is a lifted operator, the non-lifted one is better.
> *  Otherwise, neither function member is better.

### Array allocation and reuse
The compiler will include the following optimizations for implicitly allocated `params` arrays. Additional optimizations may be added in future for cases where the compiler can determine there are _no reachable aliases_ to the array.

The compiler _will allocate the array on the stack_ for a `params ReadOnlySpan<T>` argument when
- the parameter is implicitly or explicitly `scoped`,
- the argument is implicitly allocated, and
- the runtime supports [_stack allocated arrays of managed types_](#runtime-stack-allocation).

The compiler _will reuse the array_ allocated on the stack for implicit arguments to `params ReadOnlySpan<T>` and `params ReadOnlySpan<U>` when there is an identity conversion between element types `T` and `U`.

The array is allocated on the stack regardless of argument length or array element size.
The array is allocated to the length of the longest `params` argument across all applicable uses for matching `T`.

The span for a particular `params` argument will be a slice of the array matching the argument length at that call site.

At runtime, the stack space for the array is reserved for the lifetime of the method, regardless of where in the method the array is used.

Reuse is within the same method and thread of execution only and may be across distinct call sites _or_ repeated calls from the same call site.

Before exiting a C# scope, the compiler ensures the array contains no references from the scope.

To _opt out_ of compiler optimizations at a call site, the calling code should allocate the span explicitly.

The code emitted for an implicitly allocated `params` span should be identical to the code emitted when using a [_collection literal_](https://github.com/dotnet/csharplang/issues/5354)) for the `params` argument.
```csharp
log.Log("({0}, {1}, {2})", x, y, z);
log.Log("({0}, {1}, {2})", [x, y, z]); // identical code gen
```

### Runtime stack allocation
There is a runtime request to support [_fixed size array_](https://github.com/dotnet/runtime/issues/61135) fields of managed types.
With _fixed size arrays_, we can define `struct` types with inline arrays and use locals for stack allocated arrays.

For example, consider a `FixedSizeArray3<T>` type defined below which includes an inline three element array:
```csharp
struct FixedSizeArray3<T>
{
    public T[3] Array; // pseudo-code for inline fixed size array
}
```

With that type, a call to `log.Log("({0}, {1}, {2})", x, y, z)` could be emitted as:
```csharp
var _tmp = new FixedSizeArray3<object>();
_tmp.Array[0] = x;
_tmp.Array[1] = y;
_tmp.Array[2] = z;

// Logger.Log(string format, params ReadOnlySpan<object> args);
log.Log("({0}, {1}, {2})",
    new ReadOnlySpan<object>(_tmp.Array));
```

Ideally the base class library will provide types such as `FixedSizeArray1<T>`, `FixedSizeArray2<T>`, etc. for a limited number of array lengths.
And if the compilation requires spans for other array lengths, the compiler will generate and emit the additional types.

### Example
Consider the following extension method for logging the contents of a dictionary:
```csharp
static void LogDictionary<K, V>(this Logger log, Dictionary<K, V> dictionary)
{
    log.Log("Dictionary");

    foreach (var (k, v) in dictionary)
        log.Log("{0}, {1}", k, v);

    log.Log("Count = {0}", dictionary.Count);
}
```

The method could be lowered to:
```csharp
static void LogDictionary<K, V>(this Logger log, Dictionary<K, V> dictionary)
{
    FixedSizeArray2<object> _tmp = new FixedSizeArray2<object>();

    log.Log("Dictionary",
        new ReadOnlySpan<object>(Array.Empty<object>()); // no reuse

    foreach (var (k, v) in dictionary)
    {
        _tmp.Array[0] = k;
        _tmp.Array[1] = v;
        log.Log("{0}, {1}",
            new ReadOnlySpan(_tmp.Array)); // reuse
        Array.Clear(_tmp.Array);           // clear
    }

    _tmp.Array[0] = dictionary.Count;
    log.Log("Count = {0}",
        new ReadOnlySpan(_tmp.Array, 0, 1)); // reuse slice
}
```

## Open issues
### Support `params scoped T[]`?
Allow a `params T[]` to be marked as `scoped` and allocate argument arrays on the stack at call sites? That would avoid heap allocation at each call site, but allocations could only be reused at call sites with matching argument type _and length_.

### Support `params Span<T>`?
Support `params Span<T>` to allow the `params` method to modify the span contents, even though the effects are only observable at call sites that explicitly allocate the argument array?

### Support `params IEnumerable<T>`, etc.?
If we're extending `params` to support `ReadOnlySpan<T>`, should we also support `params` parameters of other collection types, including interfaces and concrete types? The reason to support `params ReadOnlySpan<T>` is to improve the performance of existing callers. And other collection types are already well supported by having non-`params` overloads for the other types in addition to a `params T[]` overload. That said, this proposal doesn't prevent extending `params` to other types in the future.

### Opting out
Should we allow opt-ing out of _implicit allocation_ on the call stack?
Perhaps an attribute that can be applied to a method, type, or assembly.

## Related proposals
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/blob/main/proposals/format.md#extending-params
