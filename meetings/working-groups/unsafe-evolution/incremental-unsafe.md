# Incremental approach to unsafe evolution

This proposal builds on the [Unsafe Evolution](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md). It assumes the syntax adopted by LDM on [2026-01-26](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-01-26.md), but does not argue one way or another on syntax choices. However, it may conflict with some syntax choices.

## Summary

The core proposal is to adopt most of the semantics proposed in [Unsafe Evolution](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md) *unconditionally* in C#, instead of behind one big opt-in switch. A weaker version of this was proposed in [Alternative syntax for caller-unsafe](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/unsafe-evolution/unsafe-alternative-syntax.md), but for ease of reading, the present proposal makes no further reference to that.

In this proposal, there is still a compiler opt-in as described [here](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md#attributes). *The opt-in means that the developer certifies that any caller-unsafe code is duly annotated*.

However, even without opt-in, the following applies:

- There is a notion of caller-unsafe members, which includes members explicitly annotated with `[RequiresUnsafe]`, as well as extern members and members with pointers or function pointers anywhere in their signature, unless they are explicitly annotated with `[RequiresUnsafe(false)]`.
- Calling caller-unsafe members outside of an unsafe context yields a warning. Users can suppress the warning or turn it into an error.
- Pointer types are no longer inherently unsafe, as proposed [here](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md#pointer-types).

When opt-in happens,

- The compiler adds assembly metadata to certify to callers that caller-unsafe annotation is deemed complete.
- Calling caller-unsafe members outside of an unsafe context becomes an error instead of a warning.
- Calling members from a non-certified (not opted-in) assembly outside of an unsafe context becomes a warning.

## Motivation

The proposal allows code bases to work gradually towards being "certified", adopting the new semantics - and dealing with its consequences - incrementally while still being able to compile and test.

It prevents useful caller-unsafe knowledge about members from being hidden from code that hasn't yet reached the point of opting in to the full certification.

It avoids having two different sets of semantics around pointers depending on opt-in, while still preventing extern members and probably-caller-unsafe members with pointers in their signature from seeming safe to call.

It unconditionally removes any semantic reason for having `unsafe` on types or members, and therefore combines seamlessly with any proposal to narrow the scope of `unsafe` to within member bodies.

It avoids forcing very-likely-caller-unsafe members declared as `extern` or with pointers in their signature to be explicitly annotated, while providing an escape hatch (`[RequiresUnsafe(false)]`) for those few that might be safe to call.

It provides a soft breaking change by default (warning when caller-unsafe members are called outside of unsafe context), which prevents users from ignoring safety issues. They at least have to explicitly silence the warning.

## Detailed design

### Pointer types

Pointer types are no longer inherently unsafe. The relaxations described in detail  [here](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md#redefining-expressions-that-require-unsafe-contexts) take unconditional effect in C# regardless of opt-in status.

### RequiresUnsafe attribute

The `RequiresUnsafeAttribute` is augmented with an optional bool parameter:

```csharp
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, AllowMultiple = false, Inherited = true)]
public sealed class RequiresUnsafeAttribute(bool required = true) : Attribute
{
    public bool Required { get; } = required;
}
```

*Note:* If we end up with a different syntax for caller-unsafe, a corresponding syntax for "caller-safe" will be needed.

### Caller-unsafe members

The following are caller-unsafe members:

- Members annotated with `[RequiresUnsafe]` (or `[RequiresUnsafe(true)]`)
- Members declared as `extern` and not annotated with `[RequiresUnsafe(false)]`
- Members with pointer types or function pointer types occurring in their signature that are not annotated with `[RequiresUnsafe(false)]`

All other members are caller-safe.

### Opt-in to certification

This proposal does not change the manner of opt-in, but only its effect. A flag given to the compiler will signal opt-in and trigger the changes in semantics described here.

Successful compilation under opt-in means an attribute is added by the compiler to the emitted assembly signifying that it is "certified", as described in detail [here](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md#attributes).

### Enforcing caller safety

If a call to a caller-unsafe member occurs outside of an unsafe context, a diagnostic is given:

- An error if opted-in to certification.
- A warning if not opted in. The warning can be suppressed or turned into an error in the same manner as other warnings.

### Enforcing recursive certification

If opted-in to certification, calls to members declared in non-certified assemblies yield a warning. The warning can be suppressed or turned into an error in the same manner as other warnings.

## Drawbacks

- **Breaking change**: This proposal breaks existing calls to now-caller-unsafe members outside of unsafe contexts, by yielding warnings by default. Such calls are in fact unsafe, and ignoring them by default leaves the user without awareness. However, the break may be opposed on the grounds that it constitutes an upgrade blocker.
- **Syntax**: While the proposal is not tied to the use of `[RequiresUnsafe]` for annotation, it likely doesn't work well with the alternative design of re-purposing the `unsafe` keyword on member declarations, as annotation no longer is gated by opt-in.

## Alternatives

- **Stronger annotation requirements**: It's been proposed that, under new rules, all members containing unsafe code must explicitly be annotated to be either caller-safe or caller-unsafe. This may help ensure proper scrutiny, at the cost of annotating many more members. The present proposal could be adapted to embrace this, with such a requirement kicking in only when opted-in to certification. 

- **Different recursive certification requirements**: Unless I missed it, other proposals do not require dependencies to be "certified" for the current opted-in compilation to succeed. The main Unsafe Evolution proposal briefly describes a [Compat mode](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md#compat-mode) for this situation. In the present proposal, a warning is produced when calling into "uncertified" assemblies outside of unsafe contexts, but we could choose not to do so (meaning the user won't be made aware that they depend on uncertified code), or to instead yield an error (meaning the user cannot suppress the diagnostic but must make such calls from an unsafe context).
