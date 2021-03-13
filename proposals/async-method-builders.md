# AsyncMethodBuilder override

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Allow per-method override of the async method builder to use.
For some async methods we want to customize the invocation of `Builder.Create()` to use a different _builder type_ and possibly pass some additional state information.

```C#
[AsyncMethodBuilderOverride(typeof(PoolingAsyncValueTaskMethodBuilder<int>))] // new, referring to some custom builder type
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

### AsyncMethodBuilderOverrideAttribute type and usage

In `dotnet/runtime`, add `AsyncMethodBuilderOverrideAttribute`:
```csharp
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates the type of the async method builder that should be used by a language compiler to
    /// build the attributed method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Event | AttributeTargets.Property | AttributeTargets.Module, Inherited = false, AllowMultiple = true)]
    public sealed class AsyncMethodBuilderOverrideAttribute : Attribute
    {
        /// <summary>Initializes the <see cref="AsyncMethodBuilderOverrideAttribute"/>.</summary>
        /// <param name="builderType">The <see cref="Type"/> of the associated builder.</param>
        public AsyncMethodBuilderOverrideAttribute(Type builderType) => BuilderType = builderType;

        /// <summary>Gets the <see cref="Type"/> of the associated builder.</summary>
        public Type BuilderType { get; }
    }
}
```

The attribute can be applied on methods (or local functions), constructors, events, properties, types and modules.

Example of usage on a method:  
```C#
[AsyncMethodBuilderOverride(typeof(PoolingAsyncValueTaskMethodBuilder<int>))] // new, referring to some custom builder type
static async ValueTask<int> ExampleAsync() { ... }
```

It is an error to apply the attribute multiple times on a given method (or local function).

A developer who wants to use a specific custom builder for all of their methods can do so by putting the relevant attribute on each method.  
Example of usage on module:  
```C#
[module: AsyncMethodBuilderOverride(typeof(PoolingAsyncValueTaskMethodBuilder))]
[module: AsyncMethodBuilderOverride(typeof(PoolingAsyncValueTaskMethodBuilder<>))]

[AsyncMethodBuilderOverride(typeof(PoolingAsyncTaskMethodBuilder<>))]
class MyClass
{
    public async ValueTask Method1Async() { ... } // would use PoolingAsyncValueTaskMethodBuilder
    public async ValueTask<int> Method2Async() { ... } // would use PoolingAsyncValueTaskMethodBuilder<int>
    public async ValueTask<string> Method3Async() { ... } // would use PoolingAsyncValueTaskMethodBuilder<string>
    public async Task<string> Method4Async() { ... } // would use PoolingAsyncTaskMethodBuilder<string>
    public async Task Method5Async() { ... } // would use AsyncTaskMethodBuilder (no override)
}
```

### Determining the builder type for an async method (or local function or lambda)

When compiling an async method (or local function or lambda), the builder type is determined by:
1. looking in the containing scopes for an override attribute that specifies a builder type compatible with the method's return type.
2. otherwise, falling back to the builder type determined by previous approach. (see [spec for task-like types](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/task-types.md)) 


Any member or type that may have the override attribute is considered a scope. This includes local functions, methods, containing types and finally the module.  
Each scope is considered in turn, walking outwards.  
If a scope has one or more override attributes, the following process is used to determine if it is compatible with the current async method:
1. take the override type specified by the attribute and construct it if necessary.  
  If the override type is an open generic type, take the single type argument of the async method's return type and substitute it into the override type. If
  the async method's return type does not have a single type argument, then that override is not compatible.
2. look for the public `Create` method with no type parameters and no parameters on the constructed override type:  
  If the method is not found, then that override is not compatible.
3. consider the return type of that `Create` method (a builder type) and look for the public `Task` property.  
  If the property is not found, then that override is not compatible.
4. consider the type of that `Task` property (a task-like type):  
  If the task-like type matches the return type of the async method, then the override is compatible. Otherwise, it is not compatible.

If the current async method (or local function) has an override attribute but it is found incompatible, an error is produced and the search is interrupted.  
If no compatible override is found on a given scope, then look at the next scope.  
If one override is found compatible, we have successfully found the builder type override to use.  
If more than one override is found to be compatible on a given scope, an error is produced.

### Execution 

The override type determined above is used as part of the existing async method design.

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
[AsyncMethodBuilderOverride(typeof(PoolingAsyncValueTaskMethodBuilder<int>))] // new, referring to some custom builder type
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

Note that when the synthesized entry-point for top-level statements is async, it will be subject to module-level override attributes.

## Drawbacks
[drawbacks]: #drawbacks

* The syntax for applying such an attribute to a method is verbose.  The impact of this is lessened if a developer can apply it to multiple methods en mass, e.g. at the type or module level.

## Alternatives
[alternatives]: #alternatives

- Implement a different task-like type and expose that difference to consumers.  `ValueTask` was made extensible via the `IValueTaskSource` interface to avoid that need, however.
- Address just the ValueTask pooling part of the issue by enabling the experiment as the on-by-default-and-only implementation.  That doesn't address other aspects, such as configuring the pooling, or enabling someone else to provide their own builder.

## Unresolved questions
[unresolved]: #unresolved-questions

1. Confirm that the compiler should produce a diagnostic if the method-level attribute was found not compatible. (recommend yes)
2. Confirm that the compiler should produce a diagnostic if multiple override attributes are specified on a method. (recommend yes)
3. **Attribute.** Should we reuse `[AsyncMethodBuilder(typeof(...))]` or introduce yet another attribute? (answer: we need a new attribute)
4. **Replace or also create.** All of the examples in this proposal are about replacing a buildable task-like's builder.  Should the feature be scoped to just that? Or should you be able to use this attribute on a method with a return type that doesn't already have a builder (e.g. some common interface)?  That could impact overload resolution.
5. **Virtuals / Interfaces.** What is the behavior if the attribute is specified on an interface method?  I think it should either be a nop or a compiler warning/error, but it shouldn't impact implementations of the interface.  A similar question exists for base methods that are overridden, and there again I don't think the attribute on the base method should impact how an override implementation behaves. Note the current attribute has Inherited = false on its AttributeUsage.
6. **Precedence.** If we wanted to do the module/type-level annotation, we would need to decide on which attribution wins in the case where multiple ones applied (e.g. one on the method, one on the containing module).  We would also need to determine if this would necessitate using a different attribute (see (1) above), e.g. what would the behavior be if a task-like type was in the same scope?  Or if a buildable task-like itself had async methods on it, would they be influenced by the attributed applied to the task-like type to specify its default builder?
7. **Private Builders**. Should the compiler support non-public async method builders? This is not spec'd today, but experimentally we only support public ones.  That makes some sense when the attribute is applied to a type to control what builder is used with that type, since anyone writing an async method with that type as the return type would need access to the builder.  However, with this new feature, when that attribute is applied to a method, it only impacts the implementation of that method, and thus could reasonably reference a non-public builder.  Likely we will want to support library authors who have non-public ones they want to use.
8. **Passthrough state to enable more efficient pooling**.  Consider a type like SslStream or WebSocket.  These expose read/write async operations, and allow for reading and writing to happen concurrently but at most 1 read operation at a time and at most 1 write operation at a time.  That makes these ideal for pooling, as each SslStream or WebSocket instance would need at most one pooled object for reads and one pooled object for writes.  Further, a centralized pool is overkill: rather than paying the costs of having a central pool that needs to be managed and access synchronized, every SslStream/WebSocket could just maintain a field to store the singleton for the reader and a singleton for the writer, eliminating all contention for pooling and eliminating all management associated with pool limits.  The problem is, how to connect an arbitrary field on the instance with the pooling mechanism employed by the builder.  We could at least make it possible if we passed through all arguments to the async method into a corresponding signature on the builder's Create method (or maybe a separate Bind method, or some such thing), but then the builder would need to be specialized for that specific type, knowing about its fields.  The Create method could potentially take a rent delegate and a return delegate, and the async method could be specially crafted to accept such arguments (along with an object state to be passed in).  It would be great to come up with a good solution here, as it would make the mechanism significantly more powerful and valuable.
9. Should we allow **Assembly** target for the override attribute? (recommend no)
10. Confirm that we should we allow **Property**, **Event**, **Constructor** targets for the override attribute. (recommend yes)
11. Confirm that we should allow using an open generic type as override even when the attribute is used directly on a method. (recommend yes)
