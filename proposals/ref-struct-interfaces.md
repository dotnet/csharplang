Ref Struct Interfaces
=====

## Summary
This proposal will expand the capabilities of `ref struct` such that they can implement interfaces and participate as generic type arguments.

## Motivation
The inability for `ref struct` to implement interfaces means they cannot partipcate in fairly fundamental abstraction techniques of .NET. A `Span<T>`, even though it has all the attributes of a sequential list cannot participate in methods that take `IReadOnlyList<T>`, `IEnumerable<T>`, etc ... Instead specific methods must be coded for `Span<T>` that have virtually the same implementation. Allowing `ref struct` to implement interfaces will allow operations to be abstracted over them as they are for other types. 

## Detailed Design

### ref struct interfaces
<a name="ref-struct-interface"></a>
The language will allow for `ref struct` types to implement interfaces. The syntax and rules are the same as for normal `struct` with a few exceptions to account for the limitations of `ref struct` types. 

The ability to implement interfaces does not impact the existing limitations against boxing `ref struct` instances. That means even if a `ref struct` implements a particular interface,  it cannot be directly cast to it as that represents a boxing action.

```csharp
ref struct File : IDisposable
{
    private SafeHandle _handle;
    public void Dispose()
    {
        _handle.Dispose();
    }
}

File f = ...;
// Error: cannot box `ref struct` type `File`
IDisposable d = f;
```

The ability to implement interfaces is only useful when combined with the ability for `ref struct` to participate in generic arguments (as [laid out later](#ref-struct-generic)).

To allow for interfaces to cover the full expressiveness of a `ref struct`, the language will allow `[UnscopedRef]` to appear on interface methods and properties. When a `ref struct` member implements an interface member with a `[UnscopedRef]` attribute, that `ref struct` member must also be decorated with `[UnscopedRef]`. The attribute is ignored when the a `class` implements the interface. 

Default interface methods pose a problem for `ref struct` as there are no protections against the default implementation boxing the `this` member. 

```csharp
interface I1
{
    void M()
    {
        I1 local1 = this;
        object local2 = this;
    }
}

ref struct S = I1 { }
```

To handle this a `ref struct` will be forced to implement all members of an interface, even if they have default implementations. The runtime will also be updated to throw an exception if a default interface member is called on a `ref struct` type.
Detailed Notes:
- A `ref struct` can implement an interface
- A `ref struct` cannot participate in default interface members
- A `ref struct` cannot be cast to interfaces it implements as that is a boxing operation

### ref struct Generic Parameters
<a name="ref-struct-generic"></a>
The language will allow for generic parameters to opt into supporting `ref struct` as arguments by using the `allow T : ref struct` syntax: 

```csharp
T Identity<T>(T p) 
    allow T : ref struct
    => p;

// Okay
Span<T> local = Identity(new Span<int>(new int[10]));
```

**TO COVER**
- lifetime rules

Detailed notes: 
- A `allow T : ref struct` generic parameter cannot 
    - Have `where T : U` where `U` is a known reference type
    - Have `where T : class` constraint
- A type parameter `T` which has `allow T: ref struct` has all the same limitations as a `ref struct` type.

## Open Issues

### Anti-Constraint syntax 
This proposal chooses the `allow T: ref struct` syntax for expressing anti-constraints. There are alternative proposals like using `where T: ~...` to express an anti-constraint. Essentially letting `~` negate the constraint listed after. This is a valid approach to the problem that should be considered.

```csharp
// Proposed
void M<T>(T p)
    where T : IDisposable
    allow T : ref struct
{
    p.Dispose();
}

// Alternative
void M<T>(T p)
    where T : IDisposable, ~ref struct
{
    p.Dispose();
}
```

### Co and contra variance
To be maximally useful type parameters that are `allow T : ref struct` must be able to participate in generic variance. Lacking that they would not be usable in many of the most popular `delegate` and `interface` types in .NET like `Func<T>`, `Action<T>`, `IEnumerable<T>`, etc ...

This feels very approachable but the author lacks the right background to properly sketch this out. Going to be relying on others like @agocke to help out here.

### Auto-applying to delegate members
The design auto-applies `allow T : ref struct` to delegates with compatible type parameters. The rationale is there is no downside to doing so, it purely increases the expressiveness of the type.

While that is true it can present a problem in multi-targeted scenarios. Code would compile in one target framework but fail in another. This could lead to confusion with customers and result in a desire for a more explicit opt-in.

### Metadata encoding

## Considerations

### Runtime support
DIM
SRM


### Span<Span<T>>
This combination of features does not allow for constructs such as `Span<Span<T>>`. This is made a bit clearer by looking at the definition of `Span<T>`: 

```csharp
readonly ref struct Span<T>
{
    public readonly ref T _data;
    public readonly int _length;

    public Span(T[] array) { ... }
}
```

If this type definition were to include `allow T : ref struct` then all `T` instances in the definition would need be treated as if they were potentially a `ref struct` type. That presents two classes of problems.

The first is for APIs like `Span(T[] array)` as a `ref struct` cannot be an array element. There are a handful of public APIs on `Span<T>` that represent `T` in an illegal place if it were a `ref struct`. These are public API that cannot be deleted and it's hard to generalize these into a feature. The most likely path forward is the compiler will special case `Span<T>` and issue an error code ever bound to one of these APIs when the argument for `T` is _potentially_ a `ref struct`.

The second is that the language does not support `ref` fields that are `ref struct`. There is a [design proposal](https://github.com/dotnet/csharplang/pull/7555) for allowing that feature. It's unclear if that will be accepted into the language or if it's expressive enough to handle the full set of scenarios around `Span<T>`.

## Related Items
Related Items:
- https://github.com/dotnet/csharplang/issues/7608
- https://github.com/dotnet/csharplang/pull/7555
- https://github.com/dotnet/runtime/blob/main/docs/design/features/byreflike-generics.md
- https://github.com/dotnet/runtime/pull/67783