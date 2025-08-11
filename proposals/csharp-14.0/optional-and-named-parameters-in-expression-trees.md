# Support optional and named arguments in Expression trees

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Champion issue: https://github.com/dotnet/csharplang/issues/9246

## Summary
[summary]: #summary

Support optional and named arguments in method calls in `Expression` trees

## Motivation
[motivation]: #motivation

Errors are reported for calls in `Expression` trees when the call is missing an argument for an optional parameter, or when arguments are named.

This results in unnecessary code and differences for expressions within `Expression` trees. And lack of support for optional arguments can lead to breaking changes when a new overload with an optional parameter is applicable at an existing call site.

The compiler restrictions should be removed if not needed.

For example, compiling the following with the .NET 10 preview SDK results in errors currently.

```csharp
namespace System
{
    public static class MemoryExtensions
    {
        public static bool Contains<T>(this ReadOnlySpan<T> span, T value, IEqualityComparer<T>? comparer = null);
    }
}

Expression<Func<int?[], int, bool>> e;
        
e = (a, i) => a.Contains(i); // error CS0854: expression tree may not contain a call that uses optional arguments
e = (a, i) => a.Contains(i, comparer: null); // error CS0853: expression tree may not contain a named argument specification
```

## Detailed design
[design]: #design

Remove the error reporting for these cases in `Expression` trees, and allow the existing method call rewriting to handle optional and named arguments.

## Drawbacks
[drawbacks]: #drawbacks

## Alternatives
[alternatives]: #alternatives

## Unresolved questions
[unresolved]: #unresolved-questions

It's unclear why the restrictions were added originally. An initial investigation hasn't revealed an issue supporting these cases.

## Design meetings
[meetings]: #meetings

*Based on a suggestion from @roji to support optional parameters.*
