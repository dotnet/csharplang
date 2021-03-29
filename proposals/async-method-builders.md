# AsyncMethodBuilder override

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Allow per-method override of the async method builder to use.
For some async methods we want to customize the invocation of `Builder.Create()` to use a different _builder type_.

```C#
[AsyncMethodBuilderAttribute(typeof(PoolingAsyncValueTaskMethodBuilder<int>))] // new usage of AsyncMethodBuilderAttribute type
static async ValueTask<int> ExampleAsync() { ... }
```

## Motivation
[motivation]: #motivation

Today, async method builders are tied to a given type used as a return type of an async method.  For example, any method that's declared as `async Task` uses `AsyncTaskMethodBuilder`, and any method that's declared as `async ValueTask<T>` uses `AsyncValueTaskMethodBuilder<T>`.  This is due to the `[AsyncMethodBuilder(Type)]` attribute on the type used as a return type, e.g. `ValueTask<T>` is attributed as `[AsyncMethodBuilder(typeof(AsyncValueTaskMethodBuilder<>))]`. This addresses the majority common case, but it leaves a few notable holes for advanced scenarios.

In .NET 5, an experimental feature was shipped that provides two modes in which `AsyncValueTaskMethodBuilder` and `AsyncValueTaskMethodBuilder<T>` operate.  The on-by-default mode is the same as has been there since the functionality was introduced: when the state machine needs to be lifted to the heap, an object is allocated to store the state, and the async method returns a `ValueTask{<T>}` backed by a `Task{<T>}`.  However, if an environment variable is set, all builders in the process switch to a mode where, instead, the `ValueTask{<T>}` instances are backed by reusable `IValueTaskSource{<T>}` implementations that are pooled.  Each async method has its own pool with a fixed maximum number of instances allowed to be pooled, and as long as no more than that number are ever returned to the pool to be pooled at the same time, `async ValueTask<{T}>` methods effectively become free of any GC allocation overhead.

There are several problems with this experimental mode, however, which is both why a) it's off by default and b) we're likely to remove it in a future release unless very compelling new information emerges (https://github.com/dotnet/runtime/issues/13633).
- It introduces a behavioral difference for consumers of the returned `ValueTask{<T>}` if that `ValueTask` isn't being consumed according to spec.  When it's backed by a `Task`, you can do with the `ValueTask` things you can do with a `Task`, like await it multiple times, await it concurrently, block waiting for it to complete, etc.  But when it's backed by an arbitrary `IValueTaskSource`, such operations are prohibited, and automatically switching from the former to the latter can lead to bugs.  With the switch at the process level and affecting all `async ValueTask` methods in the process, whether you control them or not, it's too big a hammer.
- It's not necessarily a performance win, and could represent a regression in some situations.  The implementation is trading the cost of pooling (accessing a pool isn't free) with the cost of GC, and in various situations the GC can win.  Again, applying the pooling to all `async ValueTask` methods in the process rather than being selective about the ones it would most benefit is too big a hammer.
- It adds to the IL size of a trimmed application, even if the flag isn't set, and then to the resulting asm size.  It's possible that can be worked around with improvements to the implementation to teach it that for a given deployment the environment variable will always be false, but as it stands today, every `async ValueTask` method saw for example an ~2K binary footprint increase in aot images due to this option, and, again, that applies to all `async ValueTask` methods in the whole application closure.
- Different methods may benefit from differing levels of control, e.g. the size of the pool employed because of knowledge of the method and how it's used, but the same setting is applied to all uses of the builder.  One could imagine working around that by having the builder code use reflection at runtime to look for some attribute, but that adds significant run-time expense, and likely on the startup path.

On top of all of these issues with the existing pooling, it's also the case that developers are prevented from writing their own customized builders for types they don't own.  If, for example, a developer wants to implement their own pooling support, they also have to introduce a brand new task-like type, rather than just being able to use `{Value}Task{<T>}`, because the attribute specifying the builder is only specifiable on the type declaration of the return type.

We need a way to have an individual async method opt-in to a specific builder.

## Detailed design
[design]: #detailed-design

### Using AsyncMethodBuilderAttribute on methods

In `dotnet/runtime`, add `AttributeTargets.Method` to the targets for `System.Runtime.CompilerServices.AsyncMethodBuilderAttribute`:
```csharp
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates the type of the async method builder that should be used by a language compiler:
    /// - to build the return type of an async method that is attributed,
    /// - to build the attributed type when used as the return type of an async method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Delegate | AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
    public sealed class AsyncMethodBuilderAttribute : Attribute
    {
        /// <summary>Initializes the <see cref="AsyncMethodBuilderAttribute"/>.</summary>
        /// <param name="builderType">The <see cref="Type"/> of the associated builder.</param>
        public AsyncMethodBuilderAttribute(Type builderType) => BuilderType = builderType;

        /// <summary>Gets the <see cref="Type"/> of the associated builder.</summary>
        public Type BuilderType { get; }
    }
}
```

This allows the attribute to be applied on methods or local functions or lambdas.

Example of usage on a method:  
```C#
[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<int>))] // new usage, referring to some custom builder type
static async ValueTask<int> ExampleAsync() { ... }
```

It is an error to apply the attribute multiple times on a given method.

A developer who wants to use a specific custom builder for all of their methods can do so by putting the relevant attribute on each method.  

### Determining the builder type for an async method

When compiling an async method, the builder type is determined by:
1. using the builder type from the `AsyncMethodBuilder` attribute if one is present,
2. otherwise, falling back to the builder type determined by previous approach. (see [spec for task-like types](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/task-types.md)).  

If an `AsyncMethodBuilder` attribute is present, we take the builder type specified by the attribute and construct it if necessary.  
  If the override type is an open generic type, take the single type argument of the async method's return type and substitute it into the override type.  
  If the async method's return type does not have a single type argument, then we produce an error.  

We verify that the builder type is compatible with the return type of the async method:
1. look for the public `Create` method with no type parameters and no parameters on the constructed override type.  
  It is an error if the method is not found.
2. consider the return type of that `Create` method (a builder type) and look for the public `Task` property.  
  It is an error if the property is not found.
3. consider the type of that `Task` property (a task-like type):  
  It is an error if the task-like type does not matches the return type of the async method.

### Execution 

The builder type determined above is used as part of the existing async method design.

For example, today if a method is defined as:
```C#
public async ValueTask<T> ExampleAsync() { ... }
```
the compiler will generate code akin to:
```C#
[AsyncStateMachine(typeof(<ExampleAsync>d__29))]
[CompilerGenerated]
static ValueTask<int> ExampleAsync()
{
    <ExampleAsync>d__29 stateMachine;
    stateMachine.<>t__builder = AsyncValueTaskMethodBuilder<int>.Create();
    stateMachine.<>1__state = -1;
    stateMachine.<>t__builder.Start(ref stateMachine);
    return stateMachine.<>t__builder.Task;
}
```

With this change, if the developer wrote:
```C#
[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<int>))] // new usage, referring to some custom builder type
static async ValueTask<int> ExampleAsync() { ... }
```
it would instead be compiled to:
```C#
[AsyncStateMachine(typeof(<ExampleAsync>d__29))]
[CompilerGenerated]
[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<int>))] // retained but not necessary anymore
static ValueTask<int> ExampleAsync()
{
    <ExampleAsync>d__29 stateMachine;
    stateMachine.<>t__builder = PoolingAsyncValueTaskMethodBuilder<int>.Create(); // <>t__builder now a different type
    stateMachine.<>1__state = -1;
    stateMachine.<>t__builder.Start(ref stateMachine);
    return stateMachine.<>t__builder.Task;
}
```

Just those small additions enable:
- Anyone to write their own builder that can be applied to async methods that return `Task<T>` and `ValueTask<T>`
- As "anyone", the runtime to ship the experimental builder support as new public builder types that can be opted into on a method-by-method basis; the existing support would be removed from the existing builders.  Methods (including some we care about in the core libraries) can then be attributed on a case-by-case basis to use the pooling support, without impacting any other unattributed methods.

and with minimal surface area changes or feature work in the compiler.


Note that we need the emitted code to allow a different type being returned from `Create` method:
```
AsyncPooledBuilder _builder = AsyncPooledBuilderWithSize4.Create();
```

Note that this mechanism to change the the builder type cannot be used when the synthesized entry-point for top-level statements is async. An explicit entry-point should be used instead.  

## Drawbacks
[drawbacks]: #drawbacks

* The syntax for applying such an attribute to a method is verbose.  The impact of this is lessened if a developer can apply it to multiple methods en mass, e.g. at the type or module level.

## Alternatives
[alternatives]: #alternatives

- Implement a different task-like type and expose that difference to consumers.  `ValueTask` was made extensible via the `IValueTaskSource` interface to avoid that need, however.
- Address just the ValueTask pooling part of the issue by enabling the experiment as the on-by-default-and-only implementation.  That doesn't address other aspects, such as configuring the pooling, or enabling someone else to provide their own builder.
- Earlier versions of this document allowed for scoped override of builder types.

## Unresolved questions
[unresolved]: #unresolved-questions

1. **Replace or also create.** All of the examples in this proposal are about replacing a buildable task-like's builder.  Should the feature be scoped to just that? Or should you be able to use this attribute on a method with a return type that doesn't already have a builder (e.g. some common interface)?  That could impact overload resolution.
2. **Private Builders**. Should the compiler support non-public async method builders? This is not spec'd today, but experimentally we only support public ones.  That makes some sense when the attribute is applied to a type to control what builder is used with that type, since anyone writing an async method with that type as the return type would need access to the builder.  However, with this new feature, when that attribute is applied to a method, it only impacts the implementation of that method, and thus could reasonably reference a non-public builder.  Likely we will want to support library authors who have non-public ones they want to use.
