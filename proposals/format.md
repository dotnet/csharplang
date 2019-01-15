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

### params Span
The language will allow for `params` in a method signature to have the types `Span<T>` and `ReadOnlySpan<T>`. The same
rules will apply to `params Span<T>` that apply to `params T[]`:

- Can't overload where the only difference is a `params` keyword.
- Can invoke by passing a series of `T` arguments or a single `Span<T>` argument.
- Must be the last parameter in a method signature.
- Etc ... 

The advantage this variant of `params` provides is it gives the compiler great flexbility in how it allocations the
backing storage for the `Span<T>` value. With a `params T[]` the compiler must allocate a new `T[]` for every 
invocation of a `params` method because it must assume the callee stored and reused the parameter. Given the 
`Span<T>` and `ReadOnlySpan<T>` types are `ref struct` the callee cannot store the argument. Hence the compiler can
safely re-use the value. 

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

### params IEnumerable
The language will allow for `params` in a method signature to have the type `IEnumerable<T>`. The same rules will apply 
to `params IEnumerable<T>` that apply to `params T[]`:

- Can't overload where the only difference is a `params` keyword.
- Can invoke by passing a series of `T` arguments or a single `Span<T>` argument.
- Must be the last parameter in a method signature.
- Etc ... 

The compiler will invoke a `params IEnumerable<T>` method exactly as it invokes a `params T[]` method. A new array will
be allocated for every call site and passed to the callee.

### params VariantCollection


### params overload resolution changes
Prefer Variant, span, array, ienumerable. Efficiency here is more important. 

### Customize interopolated strings
existing behavior: interpolated strings have natural type of string but can target type to formattablestring

change to ValueFormattableString

### Variant type

## Open Issuess

### open issue1

## Considerations

### consideration 1
## Future Considerations

CLR helper for stack allocating arrays 

## Misc
Related issues
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/issues/179
- https://github.com/dotnet/corefxlab/pull/2595

