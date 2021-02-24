# Parameterless struct constructors

## Summary

Support explicit parameterless constructors for value types.

## Proposal

If the parameterless constructor is less accessible than the value type, a warning is reported and the constructor is ignored when inaccessible.
```csharp
struct NoConstructor
{
    // ok
}
struct PublicConstructor
{
    internal PublicConstructor() { } // ok: always accessible
}
struct PrivateConstructor
{
    private PrivateConstructor() { } // warning: parameterless .ctor will be ignored if inaccessible
}
```

`default(S)` ignores any parameterless constructor and generates a zeroed `S`.
```csharp
NoConstructor x = default;      // ok
PublicConstructor y = default;  // ok
PrivateConstructor z = default; // ok
```

For a type parameter `T` with a `new()` constraint, `new T()` is emitted as a call to `System.Activator.CreateInstance<T>()` which ignores any explicit parameterless constructor.
A warning is reported if there is an accessible parameterless constructor.

_Should `Activator.CreateInstance()` call the parameterless constructor if available and accessible?_
```csharp
static T Create<T>() where T : new() => new T();

x = Create<NoConstructor>();      // ok
y = Create<PublicConstructor>();  // warning: constructor ignored
z = Create<PrivateConstructor>(); // ok
```

A local or field of value type `S` that is not explicitly initialized is zeroed.
The compiler already reports `error: use of unassigned local` for uninitialized locals of a value type that is not empty, and there is no additional handling beyond that for value types with explicit parameterless constructors.
```csharp
NoConstructor x;
PublicConstructor y;
PrivateConstructor z;
x.ToString(); // error: use of unassigned local (unless type is empty)
y.ToString(); // error: use of unassigned local (unless type is empty)
z.ToString(); // error: use of unassigned local (unless type is empty)
```

Array allocation ignores any parameterless constructor and generates zeroed elements.
A warning is reported if there is an accessible parameterless constructor.
```csharp
var a = new NoConstructor[1];      // ok
var b = new PublicConstructor[1];  // warning: constructor ignored
var c = new PrivateConstructor[1]; // ok
```

### Instance field initializers
_[Adapted from [classes.md#instance-variable-initializers](https://github.com/dotnet/csharplang/blob/master/spec/classes.md#instance-variable-initializers).]_
When an instance constructor has no constructor initializer `this(...)`, that constructor implicitly performs the initializations specified in the _variable_initializers_ of the instance fields.
This corresponds to a sequence of assignments that are executed immediately upon entry to the constructor and before the implicit invocation of the direct base class.
The variable initializers are executed in the textual order in which they appear in the `struct` declaration.

If there are instance field initializers but no explicit parameterless constructor, a `public` parameterless constructor is synthesized. The parameterless constructor is synthesized even if all initializer values are zeros.

```csharp
struct P0
{
    private int _x;
    private object _y;
    // no synthesized constructor
}
struct P1
{
    private int _x;
    private object _y = 1;
    public P1() { _x = 2; }
    // no synthesized constructor
}
struct P2
{
    private int _x = 0;
    private object _y = null;
    // synthesized: public P2() { _x = 0; _y = null; base(); }
}
struct P3
{
    private int _x;
    private object _y = 3;
    public P3(int x, int y) { }
    // synthesized: public P3() { _y = 3; base(); }
}
```

## See also

- https://github.com/dotnet/roslyn/issues/1029

## Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2021/LDM-2021-01-27.md#field-initializers
