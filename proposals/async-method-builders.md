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

#### P0: AsyncMethodBuilderOverrideAttribute applied on async methods

In `dotnet/runtime`, add `AsyncMethodBuilderOverrideAttribute`:
```csharp
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates the type of the async method builder that should be used by a language compiler to
    /// build the attributed method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Module, Inherited = false, AllowMultiple = false)]
    public sealed class AsyncMethodBuilderOverrideAttribute : Attribute
    {
        /// <summary>Initializes the <see cref="AsyncMethodBuilderOverrideAttribute"/>.</summary>
        /// <param name="builderType">The <see cref="Type"/> of the associated builder.</param>
        public AsyncMethodBuilderOverrideAttribute(Type builderType) => BuilderType = builderType;

        // for scoped application (use property for targetReturnType? have compiler check that it's provided)
        public AsyncMethodBuilderOverrideAttribute(Type builderType, Type targetReturnType) => ...

        /// <summary>Gets the <see cref="Type"/> of the associated builder.</summary>
        public Type BuilderType { get; }
    }
}
```

In the C# compiler, prefer the attribute on the method when determining what builder to use over the one defined on the type.  For example, today if a method is defined as:
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

#### P1: Passing state to the builder instantiation

In some scenarios, it would be useful to pass some information to the builder.  This could be static (e.g. constants configuring the size of the pool to use) or dynamic (e.g. reference to cache or singleton to use).

We brainstormed a few options (documented below for the record) but end up recommending doing nothing (option #0) until we determine that supporting dynamic state would be worthwhile.

##### Option 0: no extra state

It is possible to approximate passing static information by wrapping builder types.
For instance, one could create a custom builder type that hardcodes a certain configuration:

```C#
public struct AsyncPooledBuilderWithSize4
{
    AsyncPooledBuilder Create() => AsyncPooledBuilder.Create(4);
}
```

##### Option 1: use constants in attribute as arguments for `Create`

The `AsyncMethodBuilderOverrideAttribute` would have accept some additional information:
```C#
	public AsyncMethodBuilderOverrideAttribute(Type builderType, params object[] args)
```

The arguments collected in the attribute:
```C#
[AsyncMethodBuilderOverride(typeof(AsyncPooledBuilder), 4)]
```
would be used in invocation of the `Create` method:
```C#
AsyncPooledBuilder.Create(4);
```

##### Option 2: use arguments of the async method

In addition to `AsyncMethodBuilderOverrideAttribute` we would have an attribute to tag one of the async method's parameters:
```C#
[AsyncMethodBuilderOverride(typeof(AsyncPooledBuilder))]
async ValueTask MyMethodAsync(/* regular arguments */, [FOR_BUILDER] int i)
```

This would result in the value for that parameter being passed to the `Create` invocation:
```C#
BuilderType.Create(i);
```

In most cases, the user would end up writing a wrapper to achieve the desired signature:
```C#
public ValueTask MyMethodWrapperAsync(/* regular parameters */)
{
    return MyMethodAsync(/* pass values from regular parameters through */, 4); // static or dynamic value for the builder
}
```

One could even pass cached delegates this way:
```C#
.ctor()
{
    s_func_take = () => get_item();
    s_action_put = t => free_item(t);
}

public ValueTask MyMethodWrapperAsync(/* regular parameters */)
{
   return MyMethodAsync(/* pass values from regular parameters through */, (s_func_take, s_action_put));
}
```

This approach resembles how `EnumeratorCancellationAttribute` works. But the extra parameter isn't useful to user code, so we're polluting the signature and state machine.
This approach overlaps with option #1, so we probably wouldn't want to support both.

##### Option 3: pass a lambda that instantiates builders

As a replacement for `AsyncMethodBuilderOverrideAttribute` (or possibly in addition to it) we would have an attribute to tag one of the async method's parameters:
```C#
async ValueTask MyMethodAsync(/* regular parameters */, [FOR_BUILDER] Func<AsyncPooledBuilder> lambda)
```
That parameter would need to have a delegate type with no parameters and returning a builder type.

The compiler could them generate:
```C#
...
static ValueTask<int> MyMethodAsync(/* regular parameters */, [FOR_BUILDER] Func<AsyncPooledBuilder> lambda)
{
    <MyMethodAsync>d__29 stateMachine;
    stateMachine.<>t__builder = lambda();
    ...
}
```

We still have the problem of polluting the method signature and the state machine.

In the case where we want a builder holding two cached delegates

```C#
.ctor()
{
    s_func_take = () => get_item();
    s_action_put = t => free_item(t);
    s_func = () => Builder.Create(take: s_func_take, put: s_action_put);
}

public ValueTask MyMethodWrapperAsync(...)
{
    return MyMethodAsync(..., s_func);
}
```

#### P2: Enable at the module (and type?) level as well

A developer that wants to using a specific custom builder for all of their methods can do so by putting the relevant attribute on each method.  But we could also enable attributing at the module or type level, in which case every relevant method within that scope would behave as if it were directly annotated, e.g.
```C#
[module: AsyncMethodBuilderOverride(typeof(PoolingAsyncValueTaskMethodBuilder), typeof(ValueTask)]
[module: AsyncMethodBuilderOverride(typeof(PoolingAsyncValueTaskMethodBuilder<>), typeof(ValueTask<>)]

class MyClass
{
    public async ValueTask Method1Async() { ... } // would use PoolingAsyncValueTaskMethodBuilder
    public async ValueTask<int> Method2Async() { ... } // would use PoolingAsyncValueTaskMethodBuilder<int>
    public async ValueTask<string> Method3Async() { ... } // would use PoolingAsyncValueTaskMethodBuilder<string>
}
```

For this we would add the following members to the attribute:
```C#
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Module, Inherited = false, AllowMultiple = false)]
    public sealed class AsyncMethodBuilderOverrideAttribute : Attribute
    {
        ...
        // for scoped application (use property for targetReturnType? have compiler check that it's provided)
        public AsyncMethodBuilderOverrideAttribute(Type builderType, Type targetReturnType) => ...

        public Type TargetReturnType { get; }
    }
}
```

This would not only make it more convenient than putting the attribute on every method, it would also make it easier to employ different builds that used different builders.  For example, a build optimized for throughput might include a .cs file that specifies a pooling builder in a module-level attribute, whereas a build optimized for size might a include a .cs file that specifies a minimalistic builder that opts to use more allocation/boxing instead of lots of generic specialization and throughput optimizations that lead to code bloat.


## Drawbacks
[drawbacks]: #drawbacks

* The syntax for applying such an attribute to a method is verbose.  The impact of this is lessened if a developer can apply it to multiple methods en mass, e.g. at the type or module level.

## Alternatives
[alternatives]: #alternatives

- Implement a different task-like type and expose that difference to consumers.  `ValueTask` was made extensible via the `IValueTaskSource` interface to avoid that need, however.
- Address just the ValueTask pooling part of the issue by enabling the experiment as the on-by-default-and-only implementation.  That doesn't address other aspects, such as configuring the pooling, or enabling someone else to provide their own builder.

## Unresolved questions
[unresolved]: #unresolved-questions

1. **Attribute.** Should we reuse `[AsyncMethodBuilder(typeof(...))]` or introduce yet another attribute?
2. **Replace or also create.** All of the examples in this proposal are about replacing a buildable task-like's builder.  Should the feature be scoped to just that? Or should you be able to use this attribute on a method with a return type that doesn't already have a builder (e.g. some common interface)?  That could impact overload resolution.
3. **Virtuals / Interfaces.** What is the behavior if the attribute is specified on an interface method?  I think it should either be a nop or a compiler warning/error, but it shouldn't impact implementations of the interface.  A similar question exists for base methods that are overridden, and there again I don't think the attribute on the base method should impact how an override implementation behaves. Note the current attribute has Inherited = false on its AttributeUsage.
4. **Precedence.** If we wanted to do the module/type-level annotation, we would need to decide on which attribution wins in the case where multiple ones applied (e.g. one on the method, one on the containing module).  We would also need to determine if this would necessitate using a different attribute (see (1) above), e.g. what would the behavior be if a task-like type was in the same scope?  Or if a buildable task-like itself had async methods on it, would they be influenced by the attributed applied to the task-like type to specify its default builder?
5. **Private Builders**. Should the compiler support non-public async method builders? This is not spec'd today, but experimentally we only support public ones.  That makes some sense when the attribute is applied to a type to control what builder is used with that type, since anyone writing an async method with that type as the return type would need access to the builder.  However, with this new feature, when that attribute is applied to a method, it only impacts the implementation of that method, and thus could reasonably reference a non-public builder.  Likely we will want to support library authors who have non-public ones they want to use.
6. **Passthrough state to enable more efficient pooling**.  Consider a type like SslStream or WebSocket.  These expose read/write async operations, and allow for reading and writing to happen concurrently but at most 1 read operation at a time and at most 1 write operation at a time.  That makes these ideal for pooling, as each SslStream or WebSocket instance would need at most one pooled object for reads and one pooled object for writes.  Further, a centralized pool is overkill: rather than paying the costs of having a central pool that needs to be managed and access synchronized, every SslStream/WebSocket could just maintain a field to store the singleton for the reader and a singleton for the writer, eliminating all contention for pooling and eliminating all management associated with pool limits.  The problem is, how to connect an arbitrary field on the instance with the pooling mechanism employed by the builder.  We could at least make it possible if we passed through all arguments to the async method into a corresponding signature on the builder's Create method (or maybe a separate Bind method, or some such thing), but then the builder would need to be specialized for that specific type, knowing about its fields.  The Create method could potentially take a rent delegate and a return delegate, and the async method could be specially crafted to accept such arguments (along with an object state to be passed in).  It would be great to come up with a good solution here, as it would make the mechanism significantly more powerful and valuable.

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.
