# ref struct interfaces

[Proposal](https://github.com/dotnet/csharplang/blob/main/proposals/ref-struct-interfaces.md)

## Questions

1. Move forward with ref struct interfaces out of preview
2. Do we want to introduce a warning in the DIM case?
3. Do we want to limit the constraint language?

## Concrete Usages

### Needed for `ITensor<T>`

The tensor team wants to ship the following interface that is meant

```csharp
interface ITensor<T>
{
    [UnscopedRef]
    ReadOnlySpan<nint> Lengths { get; }
}
```

### Comparer Interfaces

This is now used by the runtime in the [comparer][comparer] interfaces.

```csharp
public static int BinarySearch<T, TComparer>(this System.ReadOnlySpan<T> span, T value, TComparer comparer)
    where TComparer : System.Collections.Generic.IComparer<T>, allows ref struct;

public static int BinarySearch<T, TComparable>(this System.ReadOnlySpan<T> span, TComparable comparable)
    where TComparable : System.IComparable<T>, allows ref struct;
```


[comparer]: https://github.com/dotnet/runtime/pull/103604

### Enumerator

Runtime wants to have existing `ref struct` based enumerators inherit `IEnumerator<T>`

### Math interfaces 

Runtime is considering them for the math related interfaces:

- `IAdditionOperators`
- `IParsable`
- `ISpanParsable`
- `IUtf8SpanParsable`

### Customer Scenarios

- [U8String project](https://github.com/dotnet/csharplang/discussions/8211#discussioncomment-9883809) alows for unification
- [Asset Ripper](https://github.com/AssetRipper/AssetRipper.Text.Html) makes [heavy use](https://github.com/AssetRipper/AssetRipper.Text.Html/pull/1/files) of this already


## Warn on DIM case?

The runtime doesn't, and never will, support calling a default implemented member (DIM) when the receiver type is a `ref struct`. The  compiler will require that a `ref struct` implement all members. Could also choose to warn at the point the code invites this problem if we wanted to. 


```csharp
interface I1
{
    // Virtual method with default implementation
    void M() { }
}

// Invocation of a virtual instance method with default implementation in a generic method that has the `allows ref struct`
// anti-constraint
void M<T>(T p)
    where T : allows ref struct, I1
{
    p.M(); // Warn?
}
```

## Limit Constraint Language?

The language allows for us to do this today

```csharp
void M<T>(T t) where T : IDisposable, allows ref struct { }
```

That is strange if there is no way `T` can be `ref struct` but not also implement interfaces
