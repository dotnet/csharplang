# Pattern-based `fixed` statement

## Summary
[summary]: #summary

Introduce a pattern that would allow types to participate in `fixed` statements. 

## Motivation
[motivation]: #motivation

The language provides a mechanism for pinning managed data and obtain a native pointer to the underlying buffer.

```csharp
fixed(byte* ptr = byteArray)
{
   // ptr is a native pointer to the first element of the array
   // byteArray is protected from being moved/collected by the GC for the duration of this block 
}

```

The set of types that can participate in `fixed` is hardcoded and limited to arrays and `System.String`. Hardcoding "special" types does not scale when new primitives such as `ImmutableArray<T>`, `Span<T>`, `Utf8String` are introduced. 

In addition, the current solution for `System.String` relies on a fairly rigid API. The shape of the API implies that `System.String` is a contiguous object that embeds UTF16 encoded data at a fixed offset from the object header. Such approach has been found problematic in several proposals that could require changes to the underlying layout. 
It would be desirable to be able to switch to something more flexible that decouples `System.String` object from its internal representation for the purpose of unmanaged interop. 

## Detailed design
[design]: #detailed-design

## *Pattern* ##
A viable pattern-based “fixed” need to:
-	Provide the managed references to pin the instance and to initialize the pointer (preferably this is the same reference)
-	Convey unambiguously the type of the unmanaged element   (i.e. “char” for “string”)
-	Prescribe the behavior in "empty" case when there is nothing to refer to. 
-	Should not push API authors toward design decisions that hurt the use of the type outside of `fixed`.

I think the above could be satisfied by recognizing a specially named ref-returning member:
 `ref [readonly] T GetPinnableReference()`.

In order to be used by the `fixed` statement the following conditions must be met:

1. There is only one such member provided for a type.
1. Returns by `ref` or `ref readonly`. 
(`readonly` is permitted so that authors of immutable/readonly types could implement the pattern without adding writeable API that could be used in safe code)
1. T is an unmanaged type.
(since `T*` becomes the pointer type. The restriction will naturally expand if/when the notion of "unmanaged" is expanded)
1. Returns managed `nullptr` when there is no data to pin – probably the cheapest way to convey emptiness.
(note that “” string returns a ref to '\0' since strings are null-terminated)

Alternatively for the `#3` we can allow the result in empty cases be undefined or implementation-specific. 
That, however, may make the API more dangerous and prone to abuse and unintended compatibility burdens. 

## *Translation* ##

```csharp
fixed(byte* ptr = thing)
{ 
    // <BODY>
}
```

becomes the following pseudocode (not all expressible in C#)

```csharp
byte* ptr;
// specially decorated "pinned" IL local slot, not visible to user code.
pinned ref byte _pinned;

try
{
    // NOTE: null check is omitted for value types 
    // NOTE: `thing` is evaluated only once (temporary is introduced if necessary) 
    if (thing != null)
    {
        // obtain and "pin" the reference
        _pinned = ref thing.GetPinnableReference();

        // unsafe cast in IL
        ptr = (byte*)_pinned;
    }
    else
    {
        ptr = default(byte*);
    }

    // <BODY> 
}
finally   // finally can be omitted when not observable
{
    // "unpin" the object
    _pinned = nullptr;
}
```

## Drawbacks
[drawbacks]: #drawbacks

- GetPinnableReference is intended to be used only in `fixed`, but nothing prevents its use in safe code, so implementor must keep that in mind.

## Alternatives
[alternatives]: #alternatives

Users can introduce GetPinnableReference or similar member and use it as
 
```csharp
fixed(byte* ptr = thing.GetPinnableReference())
{ 
    // <BODY>
}
```

There is no solution for `System.String` if alternative solution is desired.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Behavior in "empty" state. - `nullptr` or `undefined` ? 
- [ ] Should the extension methods be considered ? 
- [ ] If a pattern is detected on `System.String`, should it win over ? 

## Design meetings

None yet. 
