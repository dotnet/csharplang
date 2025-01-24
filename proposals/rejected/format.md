# Efficient Params and String Formatting

## Summary
This combination of features will increase the efficiency of formatting `string` values and passing of `params` style
arguments.

## Motivation
The allocation overhead of formatting `string` values can dominate the performance of many text based applications: 
from the boxing penalty of `struct` types, the `object[]` allocation for `params` and the intermediate `string` 
allocations during `string.Format` calls. In order to maintain efficiency such applications often need to abandon
productivity features such as `params` and `string` interpolation and move to non-standard, hand coded solutions. 

Consider MSBuild as an example. This is written using a lot of modern C# features by developers who are conscious of 
performance. Yet in one representative build sample MSBuild will generate 262MB of `string` allocation
using minimal verbosity. Of that 1/2 of the allocations are short lived allocations inside `string.Format`. These 
features would remove much of that on .NET Desktop and get it down to nearly zero on .NET Core due to the availability of `Span<T>`

The set of language features described here will enable applications to continue using these features, with very
little or no churn to their application code base, while removing the unintended allocation overhead in the majority of 
cases.

## Detailed Design 
There are a set of features that will be used here to achieve these results:

- Expanding `params` to support a broader set of collection types.
- Allowing for developers to customize how `string` interpolation is achieved. 
- Allowing for interpolated `string` to bind to more efficient `string.Format` overloads.

### Extending params
The language will allow for `params` in a method signature to have the types `Span<T>`, `ReadOnlySpan<T>` and 
`IEnumerable<T>`. The same rules for invocation will apply to these new types that apply to `params T[]`:

- Can't overload where the only difference is a `params` keyword.
- Can invoke by passing a series of arguments that are implicitly convertible to `T` or a single `Span<T>` / 
`ReadOnlySpan<T>` / `IEnumerable<T>` argument.
- Must be the last parameter in a method signature.
- Etc ... 

The `Span<T>` and `ReadOnlySpan<T>` variants will be referred to as `Span<T>` below for simplicity. In cases where the 
behavior of `ReadOnlySpan<T>` differs it will be explicitly called out. 

The advantage the `Span<T>` variants of `params` provides is it gives the compiler great flexibility in how it allocates
the backing storage for the `Span<T>` value. With a `params T[]` the compiler must allocate a new `T[]` for every 
invocation of a `params` method. Re-use is not possible because it must assume the callee stored and reused the 
parameter. This can lead to a large inefficiency in methods with lots of `params` invocations.

Given `Span<T>` variants are `ref struct` the callee cannot store the argument. Hence the compiler can optimize the 
call sites by taking actions like re-using the argument. This can make repeated invocations very efficient as compared
to `T[]`. The language though will make no specific guarantees about how such callsites are optimized. Only note that 
the compiler is free to use values other than `T[]` when invoking a `params Span<T>` method. 

One such potential implementation is the following. Consider all `params` invocation in a method body. The compiler 
could allocate an array which has a size equal to the largest `params` invocation and use that for all of the 
invocations by creating appropriately sized `Span<T>` instances over the array. For example:

```csharp
static class OneAllocation {
    static void Use(params Span<string> spans) {
        ...
    }

    static void Go() {
        Use("jaredpar");
        Use("hello", "world");
        Use("a", "longer", "set");
    }
}
```

The compiler could choose to emit the body of `Go` as follows:

```csharp
    static void Go() {
        var args = new string[3];
        args[0] = "jaredpar";
        Use(new Span<string>(args, start: 0, length: 1));

        args[0] = "hello";
        args[1] = "world";
        Use(new Span<string>(args, start: 0, length: 2));

        args[0] = "a";
        args[1] = "longer";
        args[2] = "set";
        Use(new Span<string>(args, start: 0, length: 3));
   }
```

This can significantly reduce the number of arrays allocated in an application. Allocations can be even further 
reduced if the runtime provides utilities for smarter stack allocation of arrays.

This optimization cannot always be applied though. Even though the callee cannot capture the `params` argument it can 
still be captured in the caller when there is a `ref` or a `out / ref` parameter that is itself a `ref struct`
type. 

```csharp
static class SneakyCapture {
    static ref int M(params Span<T> span) => ref span[0];

    static void Oops() {
        // This now holds onto the memory backing the Span<T> 
        ref int r = ref M(42);
    }
}
```

These cases are statically detectable though. It potentially occurs whenever there is a `ref` return or a `ref struct`
parameter passed by `out` or `ref`. In such a case the compiler must allocate a fresh `T[]` for every invocation. 

Several other potential optimization strategies are discussed at the end of this document.

The `IEnumerable<T>` variant is a merely a convenience overload. It's useful in scenarios which have frequent uses of
`IEnumerable<T>` but also have lots of `params` usage. When invoked in `T` argument form the backing storage will 
be allocated as a `T[]` just as `params T[]` is done today.

### params overload resolution changes
This proposal means the language now has four variants of `params` where before it had one. It is sensible for methods
to define overloads of methods that differ only on the type of a `params` declarations. 

Consider that `StringBuilder.AppendFormat` would certainly add a `params ReadOnlySpan<object>` overload in addition to
the `params object[]`. This would allow it to substantially improve performance by reducing collection allocations 
without requiring any changes to the calling code. 

To facilitate this the language will introduce the following overload resolution tie breaking rule. When the candidate
methods differ only by the `params` parameter then the candidates will be preferred in the following order:

1. `ReadOnlySpan<T>`
1. `Span<T>`
1. `T[]`
1. `IEnumerable<T>`

This order is the most to the least efficient for the general case.

### Variant
CoreFX is prototyping a new managed type named [Variant](https://github.com/dotnet/corefxlab/pull/2595). This type 
is meant to be used in APIs which expect heterogeneous values but don't want the overhead brought on by using `object`
as the parameter. The `Variant` type provides universal storage but avoids the boxing allocation for the most commonly
used types. Using this type in APIs like `string.Format` can eliminate the boxing overhead in the majority of cases.

This type itself is not necessarily special to the language. It is being introduced in this document separately though
as it becomes an implementation detail of other parts of the proposal. 

### Efficient interpolated strings
Interpolated strings are a popular yet inefficient feature in C#. The most common syntax, using an interpolated `string`
as a `string`, translates into a `string.Format(string, params object[])` call. That will incur boxing allocations for 
all value types, intermediate `string` allocations as the implementation largely uses `object.ToString` for formatting
as well as array allocations once the number of arguments exceeds the amount of parameters on the "fast" overloads of 
`string.Format`. 

The language will change its interpolation lowering to consider alternate overloads of `string.Format`. It will
consider all forms of `string.Format(string, params)` and pick the "best" overload which satisfies the argument types.
The "best" `params` overload will be determined by the rules discussed above. This means interpolated `string` can now
bind to very efficient overloads like `string.Format(string format, params ReadOnlySpan<Variant> args)`. In many cases
this will remove all intermediate allocations.

### Customizable interpolated strings
Developers are able to customize the behavior of interpolated strings with `FormattableString`. This contains the data
which goes into an interpolated string: the format `string` and the arguments as an array. This though still has the 
boxing and argument array allocation as well as the allocation for `FormattableString` (it's an `abstract class`). Hence
it's of little use to applications which are allocation heavy in `string` formatting.

To make interpolated string formatting efficient the language will recognize a new type: 
`System.ValueFormattableString`. All interpolated strings will have a target type conversion to this type. This will 
be implemented by translating the interpolated string into the call `ValueFormattableString.Create` exactly as is done
for `FormattableString.Create` today. The language will support all `params` options described in this document when
looking for the most suitable `ValueFormattableString.Create` method. 

```csharp
readonly struct ValueFormattableString {
    public static ValueFormattableString Create(Variant v) { ... } 
    public static ValueFormattableString Create(string s) { ... } 
    public static ValueFormattableString Create(string s, params ReadOnlySpan<Variant> collection) { ... } 
}

class ConsoleEx { 
    static void Write(ValueFormattableString f) { ... }
}

class Program { 
    static void Main() { 
        ConsoleEx.Write(42);
        ConsoleEx.Write($"hello {DateTime.UtcNow}");

        // Translates into 
        ConsoleEx.Write(ValueFormattableString.Create((Variant)42));
        ConsoleEx.Write(ValueFormattableString.Create(
            "hello {0}", 
            new Variant(DateTime.UtcNow)));
    }
}
```

Overload resolution rules will be changed to prefer `ValueFormattableString` over `string` when the argument is an 
interpolated string. This means it will be valuable to have overloads which differ only on `string` and 
`ValueFormattableString`. Such an overload today with `FormattableString` is not valuable as the compiler will always
prefer the `string` version (unless the developer uses an explicit cast). 

## Open Issues

### ValueFormattableString breaking change
The change to prefer `ValueFormattableString` during overload resolution over `string` is a breaking change. It is
possible for a developer to have defined a type called `ValueFormattableString` today and use it in method overloads
with `string`. This proposed change would cause the compiler to pick a different overload once this set of features
was implemented. 

The possibility of this seems reasonably low. The type would need the full name `System.ValueFormattableString` and it 
would need to have `static` methods named `Create`. Given that developers are strongly discouraged from defining any
type in the `System` namespace this break seems like a reasonable compromise.

### Expanding to more types
Given we're in the area we should consider adding `IList<T>`, `ICollection<T>` and `IReadOnlyList<T>` to the set of
collections for which `params` is supported. In terms of implementation it will cost a small amount over the other
work here.

LDM needs to decide if the complication to the language is worth it though. The addition of `IEnumerable<T>` removes 
a very specific friction point. Lacking this `params` solution many customers were forced to allocate `T[]` from an 
`IEnumerable<T>` when calling a `params` method. The addition of `IEnumerable<T>` fixes this though. There is no
specific friction point that the other interfaces fix here. 

## Considerations

### Variant2 and Variant3
The CoreFX team also has a non-allocating set of storage types for up to three `Variant` arguments. These are a single
`Variant`, `Variant2` and `Variant3`. All have a pair of methods for getting an allocation free `Span<Variant>` off of 
them: `CreateSpan` and `KeepAlive`. This means for a `params Span<Variant>` of up to three arguments the call site 
can be entirely allocation free.

```csharp
static class ZeroAllocation {
    static void Use(params Span<Variant> spans) {
        ...
    }

    static void Go() {
        Use("hello", "world");
    }
}
```

The `Go` method can be lowered to the following:

```csharp
static class ZeroAllocation {
    static void Go() {
        Variant2 _v;
        _v.Variant1 = new Variant("hello");
        _v.Variant2 = new Variant("word");
        Use(_v.CreateSpan());
        _v.KeepAlive();
    }
}
```

This requires very little work on top of the proposal to re-use `T[]` between `params Span<T>` calls. The compiler
already needs to manage a temporary per call and do clean up work after (even if in one case it's just marking 
an internal temp as free). 

Note: the `KeepAlive` function is only necessary on desktop. On .NET Core the method will not be available and hence
the compiler won't emit a call to it.

### CLR stack allocation helpers
The CLR only provides only 
[localloc](https://learn.microsoft.com/dotnet/api/system.reflection.emit.opcodes.localloc)
 for stack allocation of contiguous memory. This instruction is limited in that it only works for `unmanaged` types. 
 This means it can't be used as a universal solution for efficiently allocating the backing storage for `params 
 Span<T>`. 

This limitation is not some fundamental restriction though but instead more an artifact of history. The CLR could choose
to add new op codes / intrinsics which provide universal stack allocation. These could then be used to allocate the
backing storage for most `params Span<T>` calls.

```csharp
static class BetterAllocation {
    static void Use(params Span<string> spans) {
        ...
    }

    static void Go() {
        Use("hello", "world");
    }
}
```

The `Go` method can be lowered to the following:

```csharp
static class ZeroAllocation {
    static void Go() {
        Span<T> span = RuntimeIntrinsic.StackAlloc<string>(length: 2);
        span[0] = "hello";
        span[1] = "world";
        Use(span);
    }
}
```

While this approach is very heap efficient it does cause extra stack usage. In an algorithm which has a deep stack and
lots of `params` usage it's possible this could cause a `StackOverflowException` to be generated where a simple `T[]`
allocation would succeed. 

Unfortunately C# is not set up for the type of inter-method analysis where it could make an educated determination of
whether or not call should use stack or heap allocation of `params`. It can only really consider each call on its 
own.

The CLR is best setup for making this type of determination at runtime. Hence we'd likely have the runtime provide two
methods for universal stack allocation:

1. `Span<T> StackAlloc<T>(int length)`: this has the same behaviors and limitations of `localloc` except it can work on
any type `T`. 
1. `Span<T> MaybeStackAlloc<T>(int length)`: this runtime can choose to implement this by doing a stack or heap
allocation. The runtime can then use the execution context in which it's called to determine how the `Span<T>` is 
allocated. The caller though will always treat it as if it were stack allocated.

For very simple cases, like one to two arguments, the C# compiler could always use `StackAlloc<T>` variant. This is 
unlikely to significantly contribute to stack exhaustion in most cases. For other cases the compiler could choose to 
use `MaybeStackAlloc<T>` instead and let the runtime make the call.

How we choose will likely require a deeper investigation and examination of real world apps. But if these new intrinsics
are available then it will give us this type of flexibility.

### Why not varargs? 
The existing [varargs](https://docs.microsoft.com/cpp/windows/variable-argument-lists-dot-dot-dot-cpp-cli)
feature was considered here as a possible solution. This feature though is meant primarily for C++/CLI scenarios and
has known holes for other scenarios. Additionally there is significant cost in porting this to Unix. Hence it wasn't
seen as a viable solution.

## Related Issues
This spec is related to the following issues: 

- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/issues/179
- https://github.com/dotnet/corefxlab/pull/2595
