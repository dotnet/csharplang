# Async Main

* [x] Proposed
* [ ] Prototype
* [ ] Implementation
* [ ] Specification

## Summary
[summary]: #summary

Allow `await` to be used in an application's Main / entrypoint method by allowing the entrypoint to return `Task` / `Task<int>` and be marked `async`.

## Motivation
[motivation]: #motivation

It is very common when learning C#, when writing console-based utilities, and when writing small test apps to want
to call and `await` `async` methods from Main.  Today we add a level of complexity here by forcing such `await`'ing to be
done in a separate async method, which causes developers to need to write boilerplate like the following just to get
started:

```csharp
public static void Main()
{
    MainAsync().GetAwaiter().GetResult();
}

private static async Task MainAsync()
{
    ... // Main body here
}
```

We can remove the need for this boilerplate and make it easier to get started simply by allowing Main itself to be
`async` such that `await`s can be used in it.

## Detailed design
[design]: #detailed-design

The following signatures are currently allowed entrypoints:

```csharp
static void Main()
static void Main(string[])
static int Main()
static int Main(string[])
```

We extend the list of allowed entrypoints to include:

```csharp
static Task Main()
static Task<int> Main()
static Task Main(string[])
static Task<int> Main(string[])
```

To avoid compatibility risks, these new signatures will only be considered as valid entrypoints if no overloads of the previous set are present.
The language / compiler will not require that the entrypoint be marked as `async`, though we expect the vast majority of uses will be marked as such.

When one of these is identified as the entrypoint, the compiler will synthesize an actual entrypoint method that calls one of these coded methods:
- ```static Task Main()``` will result in the compiler emitting the equivalent of ```private static void $GeneratedMain() => Main().GetAwaiter().GetResult();```
- ```static Task Main(string[])``` will result in the compiler emitting the equivalent of ```private static void $GeneratedMain(string[] args) => Main(args).GetAwaiter().GetResult();```
- ```static Task<int> Main()``` will result in the compiler emitting the equivalent of ```private static int $GeneratedMain() => Main().GetAwaiter().GetResult();```
- ```static Task<int> Main(string[])``` will result in the compiler emitting the equivalent of ```private static int $GeneratedMain(string[] args) => Main(args).GetAwaiter().GetResult();```

Example usage:

```csharp
using System;
using System.Net.Http;

class Test
{
    static async Task Main(string[] args) =>
	    Console.WriteLine(await new HttpClient().GetStringAsync(args[0]));
}
```

## Drawbacks
[drawbacks]: #drawbacks

The main drawback is simply the additional complexity of supporting additional entrypoint signatures.

## Alternatives
[alternatives]: #alternatives

Other variants considered:

Allowing `async void`.  We need to keep the semantics the same for code calling it directly, which would then make it difficult for a generated entrypoint to call it (no Task returned).  We could solve this by generating two other methods, e.g.

```csharp
public static async void Main()
{
   ... // await code
}
```

becomes

```csharp
public static async void Main() => await $MainTask();

private static void $EntrypointMain() => Main().GetAwaiter().GetResult();

private static async Task $MainTask()
{
    ... // await code
}
```

There are also concerns around encouraging usage of `async void`.

Using "MainAsync" instead of "Main" as the name.  While the async suffix is recommended for Task-returning methods, that's primarily about library functionality, which Main is not, and supporting additional entrypoint names beyond "Main" is not worth it.

## Unresolved questions
[unresolved]: #unresolved-questions

n/a

## Design meetings

n/a
