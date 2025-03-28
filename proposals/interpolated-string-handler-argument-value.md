# Interpolated string handler argument value

Champion issue: <https://github.com/dotnet/csharplang/issues/9046>

## Summary
[summary]: #summary

In order to solve a pain point in the creation of handler types and make them more useful in logging scenarios,
we add support for interpolated string handlers to receive a new piece of information,
a custom value supplied at the call site.

```cs
public void LogDebug(
    this ILogger logger,
    [InterpolatedStringHandlerArgument(nameof(logger))]
    [InterpolatedStringHandlerArgumentValue(LogLevel.Debug)]
    LogInterpolatedStringHandler message);
```

## Motivation
[motivation]: #motivation

C# 10 introduced [interpolated string handlers][interpolated-string-spec], which were intended to allow interpolated strings to
be used in high-performance and logging scenarios, using more efficient building techniques and avoiding work entirely when the
string does not need to be realized. However, a common pain point has arisen since then; for logging APIs, you will often want to
have APIs such as `LogTrace`, `LogDebug`, `LogWarn`, etc, for each of your logging levels. Today, there is no way to use a single
handler type for all of those methods. Instead, our guidance has been to prefer a single `Log` method that takes a `LogLevel` or
similar enum, and use `InterpolatedStringHandlerArgumentAttribute` to pass that value along. While this works for new APIs, the
simple truth is that we have many existing APIs that use the `LogTrace/Debug/Warn/etc` format instead. These APIs either must
introduce new handler types for each of the existing methods, which is a lot of overhead and code duplication, or let the calls
be inefficient.

We want to allow a custom value to be passed along to the interpolated string handler type.
The value would be specific to a particular method that uses interpolated string handler parameter.
This would then permit parameterization based on the value,
eliminating a large amount of duplication and making it viable to adopt
interpolation handlers for `ILogger` and similar scenarios.

Some examples of this:
* [fedavorich/ISLE][isle] uses T4 to get around the bloat, by generating handlers for every log level.
* [This BCL proposal][ilogger-proposal] was immediately abandoned after it was realized that there would need to be a handler type
  for every log level.

## Detailed design
[design]: #detailed-design

The compiler will recognizes the `System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentValueAttribute`:

```cs
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class InterpolatedStringHandlerArgumentValueAttribute : Attribute
    {
        public InterpolatedStringHandlerArgumentValueAttribute(object? value)
        {
            Value = value; 
        }

        public object? Value { get; }
    }
}
```

This attribute is used on parameters, to inform the compiler how to lower an interpolated string handler pattern used in a parameter position.
The attribute can be used on its own or in combination with `InterpolatedStringHandlerArgument` attribute.

We make one small change to how interpolated string handlers perform [constructor resolution][constructor-resolution]. The change
is bolded below:

>2. The argument list `A` is constructed as follows:
>    1. The first two arguments are integer constants, representing the literal length of `i`, and the number of _interpolation_ components in `i`, respectively.
>    2. If `i` is used as an argument to some parameter `pi` in method `M1`, and parameter `pi` is attributed with `System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute`,
>    then for every name `Argx` in the `Arguments` array of that attribute the compiler matches it to a parameter `px` that has the same name. The empty string is matched to the receiver
>    of `M1`.
>        * If any `Argx` is not able to be matched to a parameter of `M1`, or an `Argx` requests the receiver of `M1` and `M1` is a static method, an error is produced and no further
>        steps are taken.
>        * Otherwise, the type of every resolved `px` is added to the argument list, in the order specified by the `Arguments` array. Each `px` is passed with the same `ref` semantics as is specified in `M1`.
>    3. **If `i` is used as an argument to some parameter `pi` in method `M1`, and parameter `pi` is attributed with `System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentValueAttribute`,
>    the attribute value is added to the argument list.**
>    4. The final argument is a `bool`, passed as an `out` parameter.
>3. Traditional method invocation resolution is performed with method group `M` and argument list `A`. For the purposes of method invocation final validation, the context of `M` is treated
>as a _member\_access_ through type `T`.
>    * If a single-best constructor `F` was found, the result of overload resolution is `F`.
>    * If no applicable constructors were found, step 3 is retried, removing the final `bool` parameter from `A`. If this retry also finds no applicable members, an error is produced and
>    no further steps are taken.
>    * If no single-best method was found, the result of overload resolution is ambiguous, an error is produced, and no further steps are taken.

### Example

```cs
// Original code
var someOperation = RunOperation();
ILogger logger = CreateLogger(LogLevel.Error, ...);
logger.LogWarn($"Operation was null: {operation is null}");

// Approximate translated code:
var someOperation = RunOperation();
ILogger logger = CreateLogger(LogLevel.Error, ...);
var loggingInterpolatedStringHandler = new LoggingInterpolatedStringHandler(20, 1, logger, LogLevel.Warn, out bool continueBuilding);
if (continueBuilding)
{
    loggingInterpolatedStringHandler.AppendLiteral("Operation was null: ");
    loggingInterpolatedStringHandler.AppendFormatted(operation is null);
}
LoggingExtensions.LogWarn(logger, loggingInterpolatedStringHandler);


// Helper libraries
namespace Microsoft.Extensions.Logging;
{
    using System.Runtime.CompilerServices;

    [InterpolatedStringHandler]
    public struct LoggingInterpolatedStringHandler
    {
        public LoggingInterpolatedStringHandler(int literalLength, int formattedCount, ILogger logger, LogLevel logLevel, out bool continueBuilding)
        {
            if (logLevel < logger.LogLevel)
            {
                continueBuilding = false;
            }
            else
            {
                continueBuilding = true;
                // Set up the rest of the builder
            }
        }
    }
    public static class LoggerExtensions
    {
        public static void LogWarn(
            this ILogger logger,
            [InterpolatedStringHandlerArgument(nameof(logger))]
            [InterpolatedStringHandlerArgumentValue(LogLevel.Warn)]
            ref LogInterpolatedStringHandler message);
    }
}
```

## Drawbacks
[drawbacks]: #drawbacks

The extra attribute and an additional compiler complexity.

## Alternatives
[alternatives]: #alternatives

https://github.com/dotnet/csharplang/blob/main/proposals/interpolated-string-handler-method-names.md

## Open questions
[open]: #open-questions

None

[interpolated-string-spec]: https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/improved-interpolated-strings.md
[isle]: https://github.com/fedarovich/isle/blob/main/src/Isle/Isle.Extensions.Logging/LoggerExtensions.tt
[ilogger-proposal]: https://github.com/dotnet/runtime/issues/111283
[constructor-resolution]: https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/improved-interpolated-strings.md#constructor-resolution
