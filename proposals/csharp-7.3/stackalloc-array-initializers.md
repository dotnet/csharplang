# Stackalloc array initializers

## Summary
[summary]: #summary

Allow array initializer syntax to be used with `stackalloc`.

## Motivation
[motivation]: #motivation

Ordinary arrays can have their elements initialized at creation time. It seems reasonable to allow that in `stackalloc` case.

The question of why such syntax is not allowed with `stackalloc` arises fairly frequently.  
See, for example, [#1112](https://github.com/dotnet/csharplang/issues/1112)

## Detailed design

Ordinary arrays can be created through the following syntax:

```csharp
new int[3]
new int[3] { 1, 2, 3 }
new int[] { 1, 2, 3 }
new[] { 1, 2, 3 }
```

We should allow stack allocated arrays be created through:  

```csharp
stackalloc int[3]				// currently allowed
stackalloc int[3] { 1, 2, 3 }
stackalloc int[] { 1, 2, 3 }
stackalloc[] { 1, 2, 3 }
```

The semantics of all cases is roughly the same as with arrays.  
For example: in the last case the element type is inferred from the initializer and must be an "unmanaged" type.

NOTE: the feature is not dependent on the target being a `Span<T>`. It is just as applicable in `T*` case, so it does not seem reasonable to predicate it on `Span<T>` case.  

## Translation

The naive implementation could just initialize the array right after creation through a series of element-wise assignments.  

Similarly to the case with arrays, it might be possible and desirable to detect cases where all or most of the elements are blittable types and use more efficient techniques by copying over the pre-created state of all the constant elements. 

## Drawbacks
[drawbacks]: #drawbacks

## Alternatives
[alternatives]: #alternatives

This is a convenience feature. It is possible to just do nothing.

## Unresolved questions
[unresolved]: #unresolved-questions

## Design meetings

None yet. 
