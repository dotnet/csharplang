# Efficent Formatting

## Summary
This combination of features will allow for effecient and customizable formatting of `string` values.

## Motivation
The allocation of `string` and `params` values dominates the performance of many text based applications like MSBuild. 
In order to maintain efficiency such applications often need to abandon popular features like `params`, `string` 
interpolation, etc ... This set of language features will enable applications to avoid the extra allocation overhead
while continuing to use these features. 

## Detailed Design 
There are a set of features that will be used here to achieve these results:

- Expanding `params` to support more effecient types than array. 
- Allowing for developers to customize how `string` interpolation is achieved. 
- Allowing for interpolated `string` to bind to more efficient `string.Format` overloads.

### Extending params
The language will allow for `params` in a method signature to have the types `Span<T>`, `ReadOnlySpan<T>` and 
`IEnumerable<T>`. The same rules will apply to these new types that apply to `params T[]`:

- Can't overload where the only difference is a `params` keyword.
- Can invoke by passing a series of arguments that are implicitly convertible to `T` or a single `Span<T>` / 
`ReadOnlySpan<T>` / `IEnumerable<T>` argument.
- Must be the last parameter in a method signature.
- Etc ... 

The `Span<T>` and `ReadOnlySpa<T>` variants will be referred to as `Span<T>` below for simplicity. In cases where the 
behavior of `ReadOnlySpan<T>` differs it will be called out. 

The advantage the `Span<T>` variants of `params` provides is it gives the compiler great flexbility in how it allocates
the backing storage for the `Span<T>` value. With a `params T[]` the compiler must allocate a new `T[]` for every 
invocation of a `params` method because it must assume the callee stored and reused the parameter. This can lead to 
a large inefficiency in methods with lots of `params` invocations.

Given `Span<T>` variants are `ref struct` the callee cannot store the argument. Hence the compiler can optimize the 
call sites by taking actions like re-using the argument. This can make repeated invocations very efficient. The 
langauge though will make no specific guarantees about how such callsites are optimized. Only note that the compiler 
is free to use values other than `T[]` when invoking a `Span<T>` method. 

The `IEnumerable<T>` variant is a merely a covenience overload. It's useful in scenarios which have frequent uses of
`IEnumeralbe<T>` but also have lots of `params` usage. When invoked in `T` argument form the backing storage will 
be allocated as a `T[]` just as `params T[]` is done today.

One such potential implementation is the following. Consider all `params` invocation of a given type in a method 
body. The compiler could allocate an array which has a size equal to the largest `params` invocation and use that for
all of the invocations by creating appropriately sized `Span<T>` instances over the array. For example:

``` csharp
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

``` csharp
    static void Go() {
        var args = new string[3];
        args[0] = "jaredpar";
        Use(new Span<int>(args, start: 0, length: 1));

        args[0] = "hello";
        args[1] = "world";
        Use(new Span<int>(args, start: 0, length: 2));

        args[0] = "a";
        args[1] = "longer";
        args[2] = "set";
        Use(new Span<int>(args, start: 0, length: 3));
   }
```

This can siginficantly reduce the number of arrays allocated in an application. Allocations can be even further 
reduced if the runtime provides utilities for smarter stack allocation of arrays.

This optimization cannot always be applied though. Even though the callee cannot capture the `params` argument it can 
still be captured in the caller when there is a `ref` or a `out / ref` parameter that is itself a `ref struct`
type. 

``` csharp
static class SneakyCapture {
    static ref int M(params Span<T> span) => ref span[0];

    static void Oops() {
        // This now holds onto the memory backing the Span<T> 
        ref int r = ref M(42);
    }
}
```

These cases are statically dectable though. It potentially occurs whenever there is a `ref` return or a `ref struct`
parameter passed by `out` or `ref`. In such a case the compiler must allocate a fresh `T[]` for every invocation. 

Several other potential optimization strategies are discussed at the end of this document.

### params overload resolution changes
This proposal means the language now has four variants of `params` where before it had one. It is also sensible for 
methods to define overloads of methods that differ only on `params` declarations. 

Consider that `StringBuilder.AppendFormat` would certainly add a `params VariantCollection` overload in addition to the
`params object[]`. This would allow it to substantially improve performance by reducing boxing and collection 
allocations without requiring any changes to the calling code. 

To facilitate this the language will introduce the following overload resolution tie breaking rule. When the candidate
methods differ only by the `params` parameter then the canditates will be preferred in the following order:

1. `VariantCollection`
1. `ReadOnlySpan<T>`
1. `Span<T>`
1. `T[]`
1. `IEnumerable<T>`

This order is the most to the least effecient for the general case.

### Efficient interpolated strings
Interpolated strings are a popular yet innefecient feature in C#. The most common syntax, using an interpolated string
as a `string`, translates into a `string.Format` call. That will inccur boxing allocations for all value types, 
intermediate `string` allocations as the implementation largely uses `object.ToString` for formatting as well as
array allocations once the number of arguments exceeds the amount of parameters on the "fast" overloads of 
`string.Format`. 

Developers are able to customize the behavior of interpolated strings with `FormattableString`. This contains the data
which goes into an interpolated string: the format strings and the arguments as an array. This though still has the 
boxing and argument array allocation as well as the allocation for `FormattableString` (it's an `abstract class`). Hence
it's of little use to applications which are allocation heavy in `string` formatting.

To make interopolated string formatting efficient the language will recognize a new type: 
`System.ValueFormattableString`. All interpolated strings will have a target type conversion to this type. This will 
be implemented by translating the interpolated string into the call `ValueFormattableString.Create` exactly as is done
for `FormattableString.Create` today. The language will support all `params` options described in this document when
looking for the most suitable `ValueFormattableString.Create` method. 

``` csharp
readonly struct ValueFormattableString {
    public static ValueFormattableString Create(Variant v) { ... } 
    public static ValueFormattableString Create(string s) { ... } 
    public static ValueFormattableString Create(string s, params VariantCollection collection) { ... } 
    public static ValueFormattableString Create(string s, Variant v) { ... }
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
            VariantCollection.Create((Variant)DateTime.UtcNow));
    }
}
```

Overload resolution rules will be changed to prefer `ValueFormattableString` over `string` when the argument is an 
interpolated string. This means it will be valuable to have overloads which differ only on `string` and 
`ValueFormattableString`. Such an overload today with `FormattableString` is not valauble as the compiler will always
prefer the `string` version (unless the developer uses an explicit cast). 

### More efficient string.format

Allow for interpolated strings to bind to more efficient `string.Format` overloads.

## Open Issuess

### ValuableFormattableString breaking change
The change to prefer `ValueFormattableString` during overload resolution over `string` is a breaking change. It is
possible for a developer to have defined a type called `ValueFormattableString` today and use it in method overloads
with `string`. This proposed change would cause the compiler to pick a different overload once this set of features
was implemented. 

The possibility of this seems reasonably low. The type would need the full name `System.ValueFormattableString` and it 
would need to have `static` methods named `Create`. Given that developers are strongly discouraged from defining any
type in the `System` namespace this break seems like a reasonable compromise.

### open issue1

## Considerations

### consideration 1
## Future Considerations

CLR helper for stack allocating arrays 
Lambdas and re-using arrays
varargs won't work because of JIT and GC
VariantCollection make it a ref struct and Span<Variant>
Calling `Span<Variant>` more efficiently

## Misc
Related issues
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/issues/179
- https://github.com/dotnet/corefxlab/pull/2595

