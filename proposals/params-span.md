# `params ReadOnlySpan<T>`

## Summary
Avoid heap allocation of implicitly allocated `params` arrays.

## Motivation
`params` array parameters provide a convenient way to call a method that takes an arbitrary length list of arguments.
However, using an array type for the parameter means the compiler must implicitly allocate an array on the heap at each call site.

If we extend `params` support to `ReadOnlySpan<T>`, where the implicitly created span cannot escape the calling method, the underlying buffer at the call site may be created on the stack instead.

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

A call in [_expanded form_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member) to a method with a `params ReadOnlySpan<T>` parameter will result in a `ReadOnlySpan<T>` instance implicitly created by the compiler.

```csharp
log.Log("({0}, {1}, {2})", x, y, z);

// Potentially emitted as:
log.Log("({0}, {1}, {2})",
    new System.ReadOnlySpan<object>(new object[] { x, y, z }));
```

A `params` parameter must be the last parameter in the method signature and cannot include a `ref`, `out`, or `in` modifier.

Two overloads cannot differ by `params` modifier alone.

`params` parameters are marked in metadata with a `System.ParamArrayAttribute`.

A `params ReadOnlySpan<T>` is implicitly `scoped`.
The parameter _cannot_ be annotated with `[UnscopedRef]` and _cannot_ be declared `scoped` explicitly.
Within the `params` method, the compiler will use escape analysis to report diagnostics if the span is captured or returned.

### Overload resolution
Overload resolution prefers overloads that are applicable in [_normal form_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member) over [_expanded form_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member).

[_Better function member_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11643-better-function-member) will prefer `params ReadOnlySpan<T>` over `params T[]` for overloads applicable in [_expanded form_](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member):

> In case the parameter type sequences `{P1, P2, ..., Pn}` and `{Q1, Q2, ..., Qn}` are equivalent (i.e. each `Pi` has an identity conversion to the corresponding `Qi`), the following tie-breaking rules are applied, in order, to determine the better function member.
> 
> *  If `Mp` is a non-generic method and `Mq` is a generic method, then `Mp` is better than `Mq`.
> *  ...
> *  Otherwise if one member is a non-lifted operator and the other is a lifted operator, the non-lifted one is better.
> *  **Otherwise, if both methods have `params` parameters and are applicable only in their expanded forms, and the `params` types are distinct types with equivalent element type (there is an identity conversion between element types), the more specific `params` type is the first of:**
>    *  **`ReadOnlySpan<T>`**
>    *  **`T[]`**
> *  Otherwise, neither function member is better.

Overload resolution will prefer `params T[]` at callsites where the type substituted for `T` is not a valid generic type argument since a `params ReadOnlySpan<T>` will not be applicable in those cases. The types that are valid as array elements but not as generic type arguments are:
- _pointers_ and
- _function pointers_.

### Allocation and reuse
The compiler will include the following optimizations for implicitly allocated `params` buffers. Additional optimizations may be added in future for cases where the compiler can determine there are _no reachable aliases_ to the buffer.

The compiler _will allocate the buffer on the stack_ for a `params ReadOnlySpan<T>` argument when
- the parameter is implicitly or explicitly `scoped` _which is required from source_,
- the argument is implicitly allocated, and
- the runtime supports [_fixed size buffers of managed types_](#runtime-stack-allocation).

The compiler _will reuse the buffer_ allocated on the stack for implicit arguments to `params ReadOnlySpan<T>` and `params ReadOnlySpan<U>` when there is an identity conversion between element types `T` and `U`.

For target frameworks that do not support _fixed size buffers_, implicitly allocated buffers will be allocated on the heap. 

The parameter must be `scoped` to ensure the implicitly allocated buffer is not returned or aliased which might prevent allocating on the stack or reusing the buffer.

The buffer is allocated on the stack regardless of argument length or element size.
The buffer is allocated to the length of the longest `params` argument across all applicable uses for matching `T`.

To _opt out_ of compiler optimizations at a call site, the calling code should allocate the span explicitly (directly or indirectly using `new ReadOnlySpan<T>(new[] { ... })`).

The span for a particular `params` argument will be a slice of the buffer matching the argument length at that call site.

At runtime, the stack space for the buffer is reserved for the lifetime of the method, regardless of where in the method the buffer is used.

Reuse is within the same method and thread of execution only and may be across distinct call sites _or_ repeated calls from the same call site.

Before exiting a C# scope, the compiler ensures the buffer contains no references from the scope.

### Runtime stack allocation
There is a runtime request to support fields of [_fixed size buffers of managed types_](https://github.com/dotnet/runtime/issues/61135).
With _fixed size buffer_ fields, we can define `ref struct` types that allow using locals for stack allocated buffers.

For example, consider a `FixedSizeBuffer3<T>` type defined below which includes an inline buffer with 3 items:
```csharp
ref struct FixedSizeBuffer3<T>
{
    public fixed T Items[3]; // pseudo-code for inline fixed size buffer
}
```

With that type, a call to `log.Log("({0}, {1}, {2})", x, y, z)` could be emitted as:
```csharp
var _tmp = new FixedSizeBuffer3<object>();
_tmp.Items[0] = x;
_tmp.Items[1] = y;
_tmp.Items[2] = z;

// Logger.Log(string format, params ReadOnlySpan<object> args);
log.Log("({0}, {1}, {2})",
    MemoryMarshal.CreateReadOnlySpan<object>(ref _tmp.Items, 3));
```

Ideally the base class library will provide types such as `FixedSizeBuffer1<T>`, `FixedSizeBuffer2<T>`, etc. for a limited number of span lengths.
And if the compilation requires buffers for other span lengths, the compiler will generate and emit the additional types.

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
    FixedSizeBuffer2<object> _tmp = new FixedSizeBuffer2<object>();

    log.Log("Dictionary",
        new ReadOnlySpan<object>(Array.Empty<object>()); // no reuse

    foreach (var (k, v) in dictionary)
    {
        _tmp.Items[0] = k;
        _tmp.Items[1] = v;
        log.Log("{0}, {1}",
            MemoryMarshal.CreateReadOnlySpan<object>(ref _tmp.Items, 2)); // reuse
        MemoryMarshal.CreateSpan<object>(ref _tmp.Items, 2).Clear();      // clear
    }

    _tmp.Items[0] = dictionary.Count;
    log.Log("Count = {0}",
        MemoryMarshal.CreateReadOnlySpan<object>(ref _tmp.Items, 1)); // reuse slice
}
```

## Open issues
### Support `params scoped T[]`?
Allow a `params T[]` to be marked as `scoped` and allocate argument arrays on the stack at call sites? That would avoid heap allocation at each call site, but allocations could only be reused at call sites with matching argument type _and length_.

### Support `params Span<T>`?
Support `params Span<T>` to allow the `params` method to modify the span contents, even though the effects are only observable at call sites that explicitly allocate the span?

### Support `params IEnumerable<T>`, etc.?
If we're extending `params` to support `ReadOnlySpan<T>`, should we also support `params` parameters of other collection types, including interfaces and concrete types?

The reason to support `params ReadOnlySpan<T>` is to improve performance of existing callers by allowing stack allocation of `params` buffers.
The reason to extend `params` to other collection types is not performance but to support implicit collections at call sites while _also_ supporting APIs or call sites that use collections other than arrays.

For APIs, supporting `params` and other collection types is already possible through overloads:
```csharp
abstract class Logger
{
    public abstract void Log(string format, IEnumerable<object> args);

    public void Log(string format, params object[] args)
    {
        Log(format, (IEnumerable<object>)args);
    }
}
```

And for callers where the API takes an explicit collection type rather than `params`, [_collection literals_](https://github.com/dotnet/csharplang/issues/5354) provide a simple syntax that reduces the need for `params`.
```csharp
log.Log("({0}, {1}, {2})", [x, y, z]);

abstract class Logger
{
    public abstract void Log(string format, IEnumerable<object> args);
}
```

That said, this proposal doesn't prevent extending `params` to other types in the future.

### Opting out
Should we allow opt-ing out of _implicit allocation_ on the call stack?
Perhaps an attribute that can be applied to a method, type, or assembly.

## Related proposals
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/blob/main/proposals/format.md#extending-params
