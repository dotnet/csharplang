# Module Initializers

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
[summary]: #summary

Although the .NET platform has a [feature](https://github.com/dotnet/runtime/blob/master/docs/design/specs/Ecma-335-Augments.md#module-initializer) that directly supports writing initialization code for the assembly (technically, the module), it is not exposed in C#.  This is a rather niche scenario, but once you run into it the solutions appear to be pretty painful.  There are reports of [a number of customers](https://www.google.com/search?q=.net+module+constructor+c%23&oq=.net+module+constructor) (inside and outside Microsoft) struggling with the problem, and there are no doubt more undocumented cases.

## Motivation
[motivation]: #motivation

- Enable libraries to do eager, one-time initialization when loaded, with minimal overhead and without the user needing to explicitly call anything
- One particular pain point of current `static` constructor approaches is that the runtime must do additional checks on usage of a type with a static constructor, in order to decide whether the static constructor needs to be run or not. This adds measurable overhead.
- Enable source generators to run some global initialization logic without the user needing to explicitly call anything

## Detailed design
[design]: #detailed-design

A method can be designated as a module initializer by decorating it with a `[ModuleInitializer]` attribute.

```cs
using System;
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ModuleInitializerAttribute : Attribute { }
}
```

The attribute can be used like this:

```cs
using System.Runtime.CompilerServices;
class C
{
    [ModuleInitializer]
    internal static void M1()
    {
        // ...
    }
}
```

Some requirements are imposed on the method targeted with this attribute:
1. The method must be `static`.
1. The method must be parameterless.
1. The method must return `void`.
1. The method must not be generic or be contained in a generic type.
1. The method must be accessible from the containing module.
    - This means the method's effective accessibility must be `internal` or `public`.
    - This also means the method cannot be a local function.
    
When one or more valid methods with this attribute are found in a compilation, the compiler will emit a module initializer which calls each of the attributed methods. The calls will be emitted in a reserved, but deterministic order.

## Drawbacks
[drawbacks]: #drawbacks

Why should we *not* do this?

- Perhaps the existing third-party tooling for "injecting" module initializers is sufficient for users who have been asking for this feature.

## Design meetings

### [April 8th, 2020](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-04-08.md#module-initializers)
