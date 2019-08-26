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

The most common complaint is that fixed-size buffers cannot be indexed in safe code. Inability to use more types is the second.

With a few minor tweaks, we could provide general-purpose fixed-sized buffers which support any type, can be used in a safe context, and have automatic bounds checking performed.

## Detailed design
[design]: #detailed-design

One would declare a safe fixed-sized buffer via the following:

```csharp
public fixed DXGI_RGB GammaCurve[1025];
```

The declaration would get translated into an internal representation by the compiler that is similar to the following

```csharp
[FixedBuffer(typeof(DXGI_RGB), 1024)]
public ConsoleApp1.<Buffer>e__FixedBuffer_1024<DXGI_RGB> GammaCurve;

// Pack = 0 is the default packing and should result in indexable layout.
[CompilerGenerated, UnsafeValueType, StructLayout(LayoutKind.Sequential, Pack = 0)]
struct <Buffer>e__FixedBuffer_1024<T>
{
    private T _e0;
    private T _e1;
    // _e2 ... _e1023
    private T _e1024;

    public ref T this[int index] => ref (uint)index <= 1024u ?
                                         ref RefAdd<T>(ref _e0, index):
                                         throw new IndexOutOfRange();
}
```

Since such fixed-sized buffers no longer require use of `fixed`, it makes sense to allow any element type.  

> NOTE: `fixed` will still be supported, but only if the element type is `blittable`

## Drawbacks
[drawbacks]: #drawbacks

* There could be some challenges with backwards compatibility, but given that the existing fixed-sized buffers only work with a selection of primitive types, it should be possible for the compiler to continue "just-working" if the user treats the fixed-buffer as a pointer.
* Incompatible constructs may need to use slightly different `v2` encoding to hide the fields from old compiler.
* Packing is not well defined in IL spec for generic types. While the approach should work, we will be bordering on undocumented behavior. We should make that documented and make sure other JITs like Mono have the same behavior.
* Specifying a separate type for every length (an possibly another for `readonly` fields, if supported) will have impact on metadata. It will be bound by the number of arrays of different sizes in the given app.
* `ref` math is not formally verifiable (since it is unsafe). We will need to find a way to update verification rules to know that our use is ok.

## Alternatives
[alternatives]: #alternatives

Manually declare your structures and use unsafe code to construct indexers.

## Unresolved questions
[unresolved]: #unresolved-questions

- should we allow `readonly`?  (with readonly indexer)
- should we allow array initializers?
- is `fixed` keyword necessary?
- `foreach`?
- only instance fields in structs?

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.