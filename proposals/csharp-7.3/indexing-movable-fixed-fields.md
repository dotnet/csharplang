# Indexing `fixed` fields should not require pinning regardless of the movable/unmovable context. #

The change has the size of a bug fix. It can be in 7.3 and does not conflict with whatever direction we take further.
This change is only about allowing the following scenario to work even though `s` is moveable. It is already valid when `s` is not moveable. 

NOTE: in either case, it still requires `unsafe` context. It is possible to read uninitialized data or even out of range. That is not changing.

```csharp
unsafe struct S
{
    public fixed int myFixedField[10];
}

class Program
{
    static S s;

    unsafe static void Main()
    {
        int p = s.myFixedField[5]; // indexing fixed-size array fields would be ok
    }
}
```

The main “challenge” that I see here is how to explain the relaxation in the spec. 
In particular, since the following would still need pinning. 
(because `s` is moveable and we explicitly use the field as a pointer)

```csharp
unsafe struct S
{
    public fixed int myFixedField[10];
}

class Program
{
    static S s;

    unsafe static void Main()
    {
        int* ptr = s.myFixedField; // taking a pointer explicitly still requires pinning.
        int p = ptr[5];
    }
}
```

One reason why we require pinning of the target when it is movable is the artifact of our code generation strategy, - we always convert to an unmanaged pointer and thus force the user to pin via `fixed` statement. However, conversion to unmanaged is unnecessary when doing indexing. The same unsafe pointer math is equally applicable when we have the receiver in the form of a managed pointer. If we do that, then the intermediate ref is managed (GC-tracked) and the pinning is unnecessary.

The change https://github.com/dotnet/roslyn/pull/24966 is a prototype PR that relaxes this requirement.
