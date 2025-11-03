# Interpolated string handler method names

Champion issue: <https://github.com/dotnet/csharplang/issues/9046>

## Summary
[summary]: #summary

We add support for interpolated string handlers to receive a new piece of information, the name of the method they are an argument
to, in order to solve a pain point in the creation of handler types and make them more useful in logging scenarios.

```cs
public void LogDebug(
    this ILogger logger,
    [InterpolatedStringHandlerArgument(nameof(logger), "Method Name")] LogInterpolatedStringHandler message);
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
be inefficient. We want to introduce a small addition to the possible values in `InterpolatedStringHandlerArgumentAttribute` to
allow the name of the method being called to be passed along to the interpolated string handler type; this would then permit
parameterization based on the method name, eliminating a large amount of duplication and making it viable for the BCL to adopt
interpolation handlers for `ILogger`. Some examples of this:

* [fedavorich/ISLE][isle] uses T4 to get around the bloat, by generating handlers for every log level.
* [This BCL proposal][ilogger-proposal] was immediately abandoned after it was realized that there would need to be a handler type
  for every log level.

## Detailed design
[design]: #detailed-design

We make one small change to how interpolated string handlers perform [constructor resolution][constructor-resolution]. The change
is bolded below:

> ...
> 2. The argument list `A` is constructed as follows:
>   1. ...
>   2. If `i` is used as an argument to some parameter `pi` in method `M1`, and parameter `pi` is attributed with `System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute`,
>   then for every name `Argx` in the `Arguments` array of that attribute the compiler matches it to a parameter `px` that has the same name. The empty string is matched to the receiver
>   of `M1`. **The string `"Method Name"` is matched to the name of `M1`.**
>       * If any `Argx` is not able to be matched to a parameter or the name of `M1`, or an `Argx` requests the receiver of `M1` and `M1` is a static method, an error is produced and no further
>       steps are taken.
>       * Otherwise, the type of every resolved `px` is added to the argument list, in the order specified by the `Arguments` array. Each `px` is passed with the same `ref` semantics as is specified in `M1`. **If `"Method Name"` was present in the `Arguments` array, then a type of `string` is added to the argument list in that position.**

### Example

```cs
// Original code
var someOperation = RunOperation();
ILogger logger = CreateLogger(LogLevel.Error, ...);
logger.LogWarn($"Operation was null: {operation is null}");

// Approximate translated code:
var someOperation = RunOperation();
ILogger logger = CreateLogger(LogLevel.Error, ...);
var loggingInterpolatedStringHandler = new LoggingInterpolatedStringHandler(20, 1, "LogWarn", logger, out bool continueBuilding);
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
        public LoggingInterpolatedStringHandler(int literalLength, int formattedCount, string methodName, ILogger logger, out bool continueBuilding)
        {
            var methodLogLevel = methodName switch
            {
                "LogDebug" => LogLevel.Debug,
                "LogInfo" => LogLevel.Information,
                "LogWarn" => LogLevel.Warn,
                "LogError" => LogLevel.Error,
                _ => throw new ArgumentOutOfRangeException(methodName),
            };

            if (methodLogLevel < logger.LogLevel)
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
        public static void LogWarn(this ILogger logger, [InterpolatedStringHandlerArgument("Method Name", nameof(logger))] ref LogInterpolatedStringHandler message);
    }
}
```

## Drawbacks
[drawbacks]: #drawbacks

Arguably, the magic empty string that we do is already a bit of magic; we risk further complicating the feature by adding in
more magic strings that users need to know.

## Alternatives
[alternatives]: #alternatives

We could design a more complicated system that allows for passing of arbitrary constants to the interpolated string handler
constructor; for example, it could be considered a bit of a hack that we use the name of the logging method, instead of a proper
`LogLevel` enum that the logging system likely already has. However, this would be a far more complicated language feature, would
need more BCL changes, and we don't know of any scenarios that actually need anything more than a string representing the method
name. Given this, we've opted for the simpler approach of just passing the method name.

## Open questions
[open]: #open-questions

None

[interpolated-string-spec]: https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/improved-interpolated-strings.md
[isle]: https://github.com/fedarovich/isle/blob/main/src/Isle/Isle.Extensions.Logging/LoggerExtensions.tt
[ilogger-proposal]: https://github.com/dotnet/runtime/issues/111283
[constructor-resolution]: https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/improved-interpolated-strings.md#constructor-resolution
