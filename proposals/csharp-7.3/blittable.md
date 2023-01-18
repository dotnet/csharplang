# Unmanaged type constraint

## Summary
[summary]: #summary

The unmanaged constraint feature will give language enforcement to the class of types known as "unmanaged types" in the C# language spec.  This is defined in section 18.2 as a type which is not a reference type and doesn't contain reference type fields at any level of nesting.  

## Motivation
[motivation]: #motivation

The primary motivation is to make it easier to author low level interop code in C#. Unmanaged types are one of the core building blocks for interop code, yet the lack of support in generics makes it impossible to create re-usable routines across all unmanaged types. Instead developers are forced to author the same boiler plate code for every unmanaged type in their library:

```csharp
int Hash(Point point) { ... } 
int Hash(TimeSpan timeSpan) { ... } 
```

To enable this type of scenario the language will be introducing a new constraint: unmanaged:

```csharp
void Hash<T>(T value) where T : unmanaged
{
    ...
}
```

This constraint can only be met by types which fit into the unmanaged type definition in the C# language spec. Another way of looking at it is that a type satisfies the unmanaged constraint if it can also be used as a pointer. 

```csharp
Hash(new Point()); // Okay 
Hash(42); // Okay
Hash("hello") // Error: Type string does not satisfy the unmanaged constraint
```

Type parameters with the unmanaged constraint can use all the features available to unmanaged types: pointers, fixed, etc ... 

```csharp
void Hash<T>(T value) where T : unmanaged
{
    // Okay
    fixed (T* p = &value) 
    { 
        ...
    }
}
```

This constraint will also make it possible to have efficient conversions between structured data and streams of bytes. This is an operation that is common in networking stacks and serialization layers:

```csharp
Span<byte> Convert<T>(ref T value) where T : unmanaged 
{
    ...
}
```

Such routines are advantageous because they are provably safe at compile time and allocation free.  Interop authors today can not do this (even though it's at a layer where perf is critical).  Instead they need to rely on allocating routines that have expensive runtime checks to verify values are correctly unmanaged.

## Detailed design
[design]: #detailed-design

The language will introduce a new constraint named `unmanaged`. In order to satisfy this constraint a type must be a struct and all the fields of the type must fall into one of the following categories:

- Have the type `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, `double`, `decimal`, `bool`, `IntPtr` or `UIntPtr`.
- Be any `enum` type.
- Be a pointer type.
- Be a user defined struct that satisfies the `unmanaged` constraint.

Compiler generated instance fields, such as those backing auto-implemented properties, must also meet these constraints. 

For example:

```csharp
// Unmanaged type
struct Point 
{ 
    int X;
    int Y {get; set;}
}

// Not an unmanaged type
struct Student 
{ 
    string FirstName;
    string LastName;
}
``` 

The `unmanaged` constraint cannot be combined with `struct`, `class` or `new()`. This restriction derives from the fact that `unmanaged` implies `struct` hence the other constraints do not make sense.

The `unmanaged` constraint is not enforced by CLR, only by the language. To prevent mis-use by other languages, methods which have this constraint will be protected by a mod-req. This will 
prevent other languages from using type arguments which are not unmanaged types.

The token `unmanaged` in the constraint is not a keyword, nor a contextual keyword. Instead it is like `var` in that it is evaluated at that location and will either:

- Bind to user defined or referenced type named `unmanaged`: This will be treated just as any other named type constraint is treated. 
- Bind to no type: This will be interpreted as the `unmanaged` constraint.

In the case there is a type named `unmanaged` and it is available without qualification in the current context, then there will be no way to use the `unmanaged` constraint. This parallels the rules surrounding the feature `var` and user defined types of the same name. 

## Drawbacks
[drawbacks]: #drawbacks

The primary drawback of this feature is that it serves a small number of developers: typically low level library authors or frameworks.  Hence it's spending precious language time for a small number of developers. 

Yet these frameworks are often the basis for the majority of .NET applications out there.  Hence performance / correctness wins at this level can have a ripple effect on the .NET ecosystem.  This makes the feature worth considering even with the limited audience.

## Alternatives
[alternatives]: #alternatives

There are a couple of alternatives to consider:

- The status quo:  The feature is not justified on its own merits and developers continue to use the implicit opt in behavior.

## Questions
[quesions]: #questions

### Metadata Representation

The F# language encodes the constraint in the signature file which means C# cannot re-use their representation. A new attribute will need to be chosen for this constraint. Additionally a method which has this constraint must be protected by a mod-req.

### Blittable vs. Unmanaged
The F# language has a very [similar feature](https://docs.microsoft.com/dotnet/articles/fsharp/language-reference/generics/constraints) which uses the keyword unmanaged. The blittable name comes from the use in Midori.  May want to look to precedence here and use unmanaged instead. 

**Resolution** The language decide to use unmanaged 

### Verifier

Does the verifier / runtime need to be updated to understand the use of pointers to generic type parameters?  Or can it simply work as is without changes?

**Resolution** No changes needed. All pointer types are simply unverifiable. 

## Design meetings

n/a
