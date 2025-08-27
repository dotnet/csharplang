# Unsigned `sizeof`

Champion issue: <https://github.com/dotnet/csharplang/issues/9633>

## Summary

This proposal enables `sizeof` expressions without a constant value to implicitly convert to unsigned integer types the same size as `uint` or larger:

```cs
uint size = sizeof(SomeStruct);
```

## Motivation

This is a minute language change which alleviates a paper cut within native interop domains.

A common pattern for native Windows APIs involves assigning the size of a struct to one of its own fields, or passing the struct's size as an additional argument to a function along with a pointer to the struct. The language provides the required value easily using `sizeof(NativeStruct)`. This is only permitted in unsafe code, and it is safe so long as the struct is authored to be blittable—to have a memory layout identical to what the native API expects.

These same native APIs make heavy use of unsigned integer types. The C# struct or method representing the native API will often use `uint` or other unsigned types in order to faithfully preserve the native API's distinction between signed and unsigned values. For example, `Microsoft.Windows.CsWin32` is a source generator which autogenerates such structs and methods based on Windows header files.

A size is never negative. Thus, invariably, the native API is defined to take an unsigned integer, and the automated C# projection of that API requires the language user to produce a `uint` value for the size. Since the C# `sizeof` operator produces a non-constant `int` value for a user-defined struct, this results in the user having to insert explicit `uint` casts most of the time that `sizeof` is used with a struct.

For example:

```cs
var buffer = default(PROCESS_BASIC_INFORMATION);

PInvoke.NtQueryInformationProcess(
    processHandle,
    PROCESSINFOCLASS.ProcessBasicInformation,
    &buffer,
    (uint)sizeof(PROCESS_BASIC_INFORMATION),
    null);
```

Or:

```cs
var header = default(BITMAPINFOHEADER);
header.biSize = (uint)sizeof(BITMAPINFOHEADER);

// Generated from native headers:
struct BITMAPINFOHEADER
{
    public uint biSize;
    public int biWidth;
    // ...
}
```

The frequent insertion of explicit uint casts is ironic: the compiler is emitting the `sizeof` CIL instruction which itself natively produces an `unsigned int32` as defined by ECMA-335, 6th edition, §III.2.45.

## Detailed design

A new implicit conversion is defined, an _unsigned sizeof conversion_, from a `sizeof` expression to `uint`.

## Specification

The [sizeof operator](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12819-the-sizeof-operator) section is adjusted as follows (additions in **bold**):

> ### 12.8.19 The sizeof operator
>
> The `sizeof` operator returns the number of 8-bit bytes occupied by a variable of a given type **as an `int` value**.

Then, a new conversion is added to the [Implicit conversions](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/conversions.md#102-implicit-conversions) section:

> ### Unsigned sizeof conversions
>
> An implicit conversion exists from a _sizeof_expression_ ([§12.8.19](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12819-the-sizeof-operator)) to `uint`.

## Drawbacks

No drawbacks are anticipated.

## Answered questions

## Open questions

1. Is a betterness rule needed in order to prevent this change from affecting overload resolution? For example:

   ```cs
   M(sizeof(SomeStruct));
   void M(long s) { } // Selected in C# 14
   void M(uint s) { }
   ```
