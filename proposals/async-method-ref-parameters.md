# Ref and ref-like parameters of async methods

Champion issue: <https://github.com/dotnet/csharplang/issues/10187>

Special thanks to community member [@IS4Code](https://github.com/IS4Code) from the for the initial proposal, and to runtime team member [@jakobbotsch](https://github.com/jakobbotsch) for consulting and guidance.

## Summary

This is a follow-up to the C# 13 feature [Allow ref and unsafe in iterators and async](csharp-13.0/ref-unsafe-in-iterators-async.md), extending the relaxation of `ref` and `ref struct` variables in `async` methods to method _parameters_ as well, with the same overall semantics. Under this proposal, `ref`/`in`/`out` parameters or parameters of `ref`-like types shall be usable in `async` methods, including local functions and anonymous functions. They shall have the same behavior as locals: they are available up to an `await` boundary, and require reassignment afterwards to be usable again.

```cs
public async ValueTask ReceiveMessageAsync(in Message message)
{
    var (sender, recipient) = (message.Sender, message.Recipient);
    await LogMessageAsync(sender, recipient);
    // do something else with the variables
}
```

This proposal does _not_ cover iterator methods, since their code always executes lazily.

## Motivation

The [Allow ref and unsafe in iterators and async](csharp-13.0/ref-unsafe-in-iterators-async.md) proposal brought a much smoother user experience by removing broad-brush restrictions and replacing them with precise restrictions. This precision allows users to interact directly with the fundamentals of the space of the stack and suspensions, rather than cordoning off the entire space indiscriminately. We further the spirit of that proposal by extending its rationale to parameters.

Consider a general message-receiving component:

```cs
public interface IMessageReceiver
{
    ValueTask ReceiveMessageAsync(in Message message);
}
```

Such an interface is designed with these goals in mind:

* The method returns a `ValueTask` so it can optimize synchronous paths (indeed many receivers might not need to implement it asynchronously at all).
* `Message` is a `struct` because its construction might happen over a local, and the same local can be reused for multiple sequential messages, reducing GC overhead (even using an object pool complicates reasoning about the lifetime of messages ‒ using a reference here protects against unwanted escaping).
* Since `Message` is a potentially bulky object with many fields gradually added as the application grows, it is passed by-reference, because not all receivers need to access all its fields (but the interface must stay the same, so the method can't request these fields explicitly).

In this context, the receivers are supposed to process the messages as they arrive, sometimes pass the references around into nested receivers, while a single dispatcher constructs the messages in-place (in an allocation-less style). Most receivers aren't asynchronous, and most messages do not need to persist past the `ReceiveMessageAsync` call, justifying this design. Since `message` is immutable, all receivers are always able to read all relevant fields up front using the reference, and do any processing later.

The issue appears immediately when trying to implement this method via `async` even when there is no `await`:

```cs
public async ValueTask ReceiveMessageAsync(in Message message) // error CS1988: Async methods cannot have ref, in or out parameters
{
    Log(message.Sender);
}
```

Such a pattern is somewhat common to capture any exceptions into the returned `ValueTask`, as most callers expect. Even in this case, with 100% synchronous execution (and hence `message` being available the whole time), the parameter is currently not allowed.

The usefulness of this pattern is not restricted to `await`-less code, of course:

```cs
public async ValueTask ReceiveMessageAsync(in Message message)
{
    var (sender, recipient) = (message.Sender, message.Recipient);
    await LogMessageAsync(sender, recipient);
    // do something else with the variables
}
```

Here all relevant data is extracted from the message first and utilized later, even after `await`.

Currently, one needs to go through a local function to implement such a method conveniently:

```cs
public ValueTask ReceiveMessageAsync(in Message message)
{
    var (sender, recipient) = (message.Sender, message.Recipient);
    return Inner();
    async ValueTask Inner()
    {
        await LogMessageAsync(sender, recipient);
        // ...
    }
}
```

However this can still be prone to issues regarding exception-wrapping and other potentially erroneous semantics, such as using `using` in the outer method, and it is far from perfect ‒ since `message` must not be accessed within `Inner` at all, everything must be stored up front.

Thus, this feature allows users to take the simple, direct path, rather than requiring jumping through hoops without a fundamental reason for those hoops.

## Examples

This method takes an input span of an arbitrary length, identifies the length of the thing serialized within `data`, parses it, adjusts the span accordingly so it points after the thing in `data`, and handles the thing off to additional processing.

```cs
public async ValueTask ParseAndProcessAsync(ref ReadOnlySpan<byte> data)
{
    int length = /* find the end of a serialized thing in data */;
    using var thing = Thing.Parse(data.Slice(0, length));
    data = data.Slice(length);
    await ProcessAsync(thing);
}
```

This mechanism ensures the extent of the thing within `data` is known even before the final processing, it also correctly wraps any exceptions during parsing in the resulting `ValueTask`, and it ensures cleanup of `thing` at end of the processing.

## Detailed Design

> [!TIP]
> While the word 'method' is used as a shorthand throughout this proposal, these changes apply to local functions and anonymous functions as well.

The overall design is similar to that of local variables:

* `async` methods can have parameters that are `ref`/`out`/`in` or `ref`-like.
* Such parameters are considered assigned from the start of the method right until the first `await`.
* After an `await` boundary, they are considered definitely unassigned. Consistently with byref locals, byref parameters can be reassigned to be usable again, e.g. <code><i>param</i> = ref <i>expr</i>;</code>.

That is, the semantics of such parameters within the implementation of an `async` method are functionally identical to that of locals defined immediately at the start of the method.

Unlike other cases, `out` parameters pose requirements on the implementation itself. In the case of an `out` parameter, its value must be definitely assigned by the point of the first `await`, in accordance with what the caller expects from calling the method _without `await`_.

Like in normal methods, these relaxed rules make it possible for references from parameters (or references to `this` or its members) to escape via `ref` or `out` parameters. Such an `async` method should have the exact same characteristics as a non-`async` method sharing the initial portion of the implementation up until the first `await` boundary, i.e. what can be achieved using the `Inner` function as above, again matching what the caller expects.

The implementation strategy considers two scenarios: methods using **runtime async**, and methods using **async state machines**.

### Runtime async

When a compilation has the runtime-async feature enabled, async methods whose return type is a `Task` or a `ValueTask` type are emitted using `MethodImplOptions.Async` rather than using an async state machine.

This implementation strategy is _**extremely simple**_: just remove the compiler diagnostic that prevents usage of ref and ref-like parameters, and allow them to be emitted just like on any other method. The runtime already supports IL equivalent to the following:

```cs
[MethodImpl(MethodImplOptions.Async)]
Task ExampleAsyncMethod(ref int someParameter)
{
    someParameter = 42; // Assign through the ref
    AsyncHelpers.Await(Task.Delay(1));
}
```

By following the same rules as for ref and ref-like locals, the compiler will not allow a ref parameter to be used after an await until it is ref-reassigned again, nor allow a ref-like parameter to be read after an await until it is reassigned.

### Async state machines

For async methods whose return type is not a `Task` type or a `ValueTask` type, or which are not in a compilation that has the runtime-async feature enabled, or which have an async method builder explicitly specified, the compiler emits a state machine for the async method.

Not all async method builders are compatible with ref and ref-like parameters. In order to be compatible, the async method builder's `Start` method must synchronously invoke `IAsyncStateMachine.MoveNext()` so that the start of the async method body runs _before_ the async method returns. Once the async method returns, and the ref and ref-like parameters which were passed to the async method can no longer be accessed by the start of the async method body during the first call to `MoveNext`.

Because async method builders have no way to declare such compatibility, a whitelist is employed to identify known-safe async method builders. A builder which is not in this whitelist is known as a "custom async method builder." The namespace-qualified type name of a known-safe async method builder must be one of the following:

* `System.Runtime.CompilerServices.AsyncTaskMethodBuilder`
* ``System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1``
* `System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`
* ``System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1``
* `System.Runtime.CompilerServices.AsyncVoidMethodBuilder`
* `System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`
* ``System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1``

To support usage of the `System.ValueTask` package, there is not a requirement that the builder type be defined in the corelib reference.

Custom async method builders are not supported by this feature. It will be a compile-time error if a ref or ref-like parameter is used on a method whose effective builder is a custom async method builder. A potential future expansion could allow custom method builders to opt in with a new API protocol (see [Possible extensions](#possible-extensions)).

### Async state machine - lowering strategy

The start of the async method body code is contained in the `IAsyncStateMachine.MoveNext()` method. It is not desirable move the start of the async method body code _outside_ the state machine. The async state machine builder `Start` methods wrap the initial `MoveNext` call so that exceptions are marshaled to the returned task object and so that the ExecutionContext and SynchronizationContext are captured and restored. Moving any of the async method's code outside of the `MoveNext` method would result in breaking behavior changes.

In order to make the ref and ref-like parameters available inside the `IAsyncStateMachine.MoveNext()` method, the following strategy is suggested. Compared to other approaches that were considered, this strategy is relatively simple and has broad runtime support.

Like with regular method parameters, the there will be a field in the async state machine for each ref and ref-like parameter as well. For a ref parameter, the field type will be `void*`. For a ref-like parameter passed by value, the field type will be a pointer to the type of the ref-like parameter, such as `SomeRefStruct*`.

#### Preparing state machine fields

For each ref parameter, a TypedReference local will be added to the async method. The TypedReference local will be initialized with a `ldarg` for the corresponding ref parameter, followed by `mkrefany`, followed by a `stloc`.

Then, when initializing the async state machine fields corresponding to parameters:

* If the parameter is a ref parameter, the state machine field type is `void*`. A pointer is taken to the ref parameter's corresponding TypedReference local, and the pointer is stored in the state machine field. (While the C# compiler allows compiling `TypedReference*` fields, the runtime does not support them and fails with TypeLoadException.)
* If the parameter is a ref-like parameter passed by value, the state machine field type is a pointer to the ref-like type. A pointer is taken to the ref-like parameter and stored in the state machine field.

Taking these pointers prevents the runtime from collecting the locals before the end of the frame, as well as preventing tail calls in async void methods which end with the call to the builder's `Start` method.

#### Using state machine fields

At the start of the async method body code in `IAsyncStateMachine.MoveNext()`, a local is created for each ref or ref-like parameter.

* If the parameter is a ref parameter, the local is a ref local of the same type as the parameter. It is initialized using `ldobj` with type `System.TypedReference` on the corresponding `void*` state machine field, followed by `refanyval` with the same type as the ref parameter, followed by `stloc`.
* If the parameter is a ref-like parameter passed by value, the local is a non-ref local of the same type as the parameter. The corresponding `SomeRefStruct*` state machine field is dereferenced to initialize the local.

Then, at the point when the initial async code accesses a ref or ref-like parameter, the local is used instead.

(In the compiler's discretion, these locals may be inlined or elided. To avoid a copy, the non-ref local for a ref-like parameter could also be a ref local.)

When ref and ref-like parameters are reassigned, the compiler will act as though a brand new local ref variable was being used rather than as though an existing variable was being reused. This behavior is consistent with the existing behavior when a ref or ref-like local is reassigned. Thus, reassignment will not write into state machine fields, but will rather dissociate the subsequent use of the parameter from the state machine fields.

#### State machine example code

To demonstrate the above rules, the following async method will be lowered:

```cs
async Task ExampleAsyncMethod(ref int p1, SomeRefStruct p2, ref SomeRefStruct p3)
{
    Console.WriteLine(p1);
    Console.WriteLine(p2.RefField);
    Console.WriteLine(p3.RefField);
    Console.WriteLine(p3.ref-likeField.Length);
    await Task.Delay(1);
}
```

The following C# code faithfully represents the lowering described above in full effect, though the details are different. For example, the emitted IL will not need `Unsafe.As` and will just use the ref struct type directly in `mkrefany` and `refanyvalue`. Nevertheless, the following sample is fully runnable on .NET 9+ and displays the intended behavior.

(.NET 9 is only needed for the `Unsafe.As` method, which the compiler will not need when emitting IL, but which the C# version needs for this sample. The following code can also been used to demonstrate broad runtime support across all .NET runtimes, with some workarounds to gain the equivalent of `Unsafe.As` for a ref struct pre-.NET 9.)

```cs
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

ref struct SomeRefStruct
{
    public ref int RefField;
    public Span<int> ref-likeField;
};

class Program
{
    private static int refTarget1 = 1, refTarget2 = 2;

    public static async Task Main()
    {
        var p1 = 42;
        var p2 = new SomeRefStruct { RefField = ref refTarget1 };
        var p3 = new SomeRefStruct { RefField = ref refTarget2, ref-likeField = [1, 2, 3] };
        await ExampleAsyncMethod(ref p1, p2, ref p3);
    }

    private static Task ExampleAsyncMethod(ref int p1, SomeRefStruct p2, ref SomeRefStruct p3)
    {
        TypedReference p1Ref = __makeref(p1);
        // The following is required for the C# compilation, but in IL this will just be:
        // = __makeref(p3)
        TypedReference p3Ref = __makeref(Unsafe.As<SomeRefStruct, byte>(ref p3));

        GeneratedStateMachine stateMachine = default;
        stateMachine.t__builder = AsyncTaskMethodBuilder.Create();
        unsafe
        {
            stateMachine.p1RefPtr = &p1Ref;
            stateMachine.p2Ptr = &p2;
            stateMachine.p3RefPtr = &p3Ref;
        }
        stateMachine.__state = -1;
        stateMachine.t__builder.Start(ref stateMachine);
        return stateMachine.t__builder.Task;
    }
}

[StructLayout(LayoutKind.Auto)]
[CompilerGenerated]
struct GeneratedStateMachine : IAsyncStateMachine
{
    public int __state;

    public AsyncTaskMethodBuilder t__builder;

    public unsafe void* p1RefPtr;
    public unsafe SomeRefStruct* p2Ptr;
    public unsafe void* p3RefPtr;

    private TaskAwaiter u__1;

    void IAsyncStateMachine.MoveNext()
    {
        int num = __state;
        try
        {
            TaskAwaiter awaiter;
            if (num != 0)
            {
                unsafe
                {
                    ref int p1 = ref __refvalue(*(TypedReference*)p1RefPtr, int);
                    SomeRefStruct p2 = *p2Ptr;
                    // The following is required for the C# compilation, but the IL version will just be:
                    // = ref __refvalue(*(TypedReference*)p3RefPtr, SomeRefStruct)
                    ref SomeRefStruct p3 = ref Unsafe.As<byte, SomeRefStruct>(ref __refvalue(*(TypedReference*)p3RefPtr, byte));
                    Console.WriteLine(p1);
                    Console.WriteLine(p2.RefField);
                    Console.WriteLine(p3.RefField);
                    Console.WriteLine(p3.ref-likeField.Length);
                }
                awaiter = Task.Delay(1).GetAwaiter();
                if (!awaiter.IsCompleted)
                {
                    num = (__state = 0);
                    u__1 = awaiter;
                    t__builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
                    return;
                }
            }
            else
            {
                awaiter = u__1;
                u__1 = default(TaskAwaiter);
                num = (__state = -1);
            }
            awaiter.GetResult();
        }
        catch (Exception exception)
        {
            __state = -2;
            t__builder.SetException(exception);
            return;
        }
        __state = -2;
        t__builder.SetResult();
    }

    [DebuggerHidden]
    void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
    {
        t__builder.SetStateMachine(stateMachine);
    }
}
```

## Possible extensions

* Parameters of pointer types are currently also prohibited, which is something that could be lifted as well, without any substantial redesigning necessary (this was more of a safety check than a limitation, considering things like `int*[] param` are allowed anyway). Such a thing could potentially be changed to a warning.

* Currently, `ref`/`ref`-like locals cannot be used in a local function or anything else that could generate a closure. In case this restriction is lifted, it could also apply to `async` functions referencing outside variables or parameters, due to the similarity between a closure and a `ref` parameter.

* Custom async method builders could be supported if they opt into a new API calling pattern which replaces the `Start` call with `BeginStart`+`EndStart` calls or similar, allowing the compiler to directly call `IAsyncStateMachine.MoveNext()` between them, forcing it to run while the ref parameters still exist before the async method returns.
