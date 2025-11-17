# Async main codegen update

## Summary

We update the codegen for `async Task Main` to allow the compiler to use a separate helper from the runtime when present.

## Motivation

In some scenarios, the runtime needs to be able to hook the real `Main` method, not just the `void` or `int` returning method that is generated to
wrap the user-written `async Task` method. To facilitate this, we allow codegen to use a different method to call the real user-written `Main` method.

## Detailed Design

We update section [§7.1](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/basic-concepts.md#71-application-startup) of the spec as
follows:

```diff
- Otherwise, the synthesized entry point waits for the returned task to complete, calling `GetAwaiter().GetResult()` on the task, using either the parameterless instance method or the extension method described by [§C.3](standard-library.md#c3-standard-library-types-not-defined-in-isoiec-23271). If the task fails, `GetResult()` will throw an exception, and this exception is propagated by the synthesized method.
+ Otherwise, the synthesized entry point waits for the returned task to complete, either passing the task to `System.Runtime.CompilerServices.AsyncHelpers.HandleAsyncEntryPoint` (if it exists) or calling `GetAwaiter().GetResult()` on the task, using either the parameterless instance method or the extension method described by [§C.3](standard-library.md#c3-standard-library-types-not-defined-in-isoiec-23271). If the task fails, the calling method form will throw an exception, and this exception is propagated by the synthesized method.
```

The compiler will look for the following APIs from the core libraries. We do not look at implementations defined outside the core library:

```cs
namespace System.Runtime.CompilerServices;

public class AsyncHelpers
{
    public static void HandleAsyncEntryPoint(System.Threading.Tasks.Task task);
    public static void HandleAsyncEntryPoint(System.Threading.Tasks.Task<int> task);
}
```

When present the resulting transformation looks like this:

```cs
// Original code
public class C
{
    public static async Task Main()
    {
        await System.Threading.Tasks.Task.Yield();
        Console.WriteLine("Hello world");
    }
}

// Lowered psuedocode, not including async state machine if present
public class C
{
    public static void $<Main>()
    {
        System.Runtime.CompilerServices.AsyncHelpers.HandleAsyncEntryPoint(Main());
    }

    public static async Task Main()
    {
        await System.Threading.Tasks.Task.Yield();
        Console.WriteLine("Hello world");
    }
}
```
