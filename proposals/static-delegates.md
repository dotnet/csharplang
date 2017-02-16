# Static Delegates

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Provide a general-purpose, lightweight callback capability to the C# language.

## Motivation
[motivation]: #motivation

Today, users have the ability to create callbacks using the `System.Delegate` type. However, these are fairly heavyweight (such as requiring a heap allocation and always having handling for chaining callbacks together).

Additionally, `System.Delegate` does not provide the best interop with unmanaged function pointers, namely due being non-blittable and requiring marshalling anytime it crosses the managed/unmanaged boundary.

With a few minor tweaks, we could provide a new type of delegate that is lightweight, general-purpose, and interops well with native code.

## Detailed design
[design]: #detailed-design

One would declare a static delegate via the following:

```C#
static delegate int Func()
```

One could additionally attribute the declaration with something similar to `System.Runtime.InteropServices.UnmanagedFunctionPointer` so that the calling convention, string marshalling, and set last error behavior can be controlled. NOTE: Using `System.Runtime.InteropServices.UnmanagedFunctionPointer` itself will not work, as it is only usable on Delegates.

The declaration would get translated into an internal representation by the compiler that is similar to the following

```C#
struct <name>
{
    IntPtr pFunction;

    static int Func();
}
```

That is to say, it is internally represented by a struct that has a single member of type `IntPtr` (such a struct is blittable and does not incur any heap allocations). The member contains the address of the function that is to be the callback. Additionally, the type declares a method matching the method signature of the callback.

The value of the static delegate can only be bound to a static method that matches the signature of the callback.

Chaining callbacks together is not supported.

Invocation of the callback would be implemented by the `calli` instruction.


## Drawbacks
[drawbacks]: #drawbacks

Static Delegates would not work with existing APIs that use regular delegates (one would need to wrap said static delegate in a regular delegate of the same signature).

Additional work would be needed to make Static Delegate readily usable in the core framework.

## Alternatives
[alternatives]: #alternatives

TBD

## Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still TBD?

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.


