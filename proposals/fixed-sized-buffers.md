# Fixed Sized Buffers

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Provide a general-purpose and safe mechanism for declaring fixed sized buffers to the C# language.

## Motivation
[motivation]: #motivation

Today, users have the ability to create fixed-sized buffers in an unsafe-context. However, this requires the user to deal with pointers, manually perform bounds checks, and only supports a limited set of types (`bool`, `byte`, `char`, `short`, `int`, `long`, `sbyte`, `ushort`, `uint`, `ulong`, `float`, and `double`).

With a few minor tweaks, we could provide general-purpose fixed-sized buffers which support any type, can be used in a safe context, and have automatic bounds checking performed.

## Detailed design
[design]: #detailed-design

One would declare a safe fixed-sized buffer via the following:

```C#
public fixed DXGI_RGB GammaCurve[1025];
```

The declaration would get translated into an internal representation by the compiler that is similar to the following

```C#
[CompilerGenerated, StructLayout(LayoutKind.Sequential, Pack = 1)]
struct <GammaCurve>e__FixedBuffer
{
    private DXGI_RGB _e0;
    private DXGI_RGB _e1;
    // _e2 ... _e1023
    private DXGI_RGB _e1024;

    public ref DXGI_RGB this[int index]
    {
        get;
    }
}
```

## Drawbacks
[drawbacks]: #drawbacks

Some additional syntax may be needed to ensure we don't break users that are currently using 'unsafe fixed-buffers'.
* Given that the existing fixed-sized buffers only work with a selection of primitive types, it should be possible for the compiler to continue "just-working" if the user treats the fixed-buffer as a pointer.
* Its probably worth extending the ability to work with fixed-sized buffers as pointers to all `blittable` types.

In order to ensure that the safe fixed-sized buffer is resilient to future field-layout changes (and for it to work with non-blittable types), the compiler needs to explicitly layout each field of the array.
* This can quickly become 'unwieldly' for large arrays
* The JIT may not behave well with types that contain a large number of fields
 * We may be able to work with the JIT/Runtime to provide a special flag they can use (basically, it would give the contained type and the element count, then they just need to compute the size of the base struct and multiply, rather than actually checking each field).

## Alternatives
[alternatives]: #alternatives

Manually declare your structures

## Unresolved questions
[unresolved]: #unresolved-questions

Can/should this additionally be extended to stackalloc which provides a very similar mechanism.

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.
