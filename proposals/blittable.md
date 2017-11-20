# Blittable Types

* [x] Proposed
* [ ] Prototype
* [ ] Implementation
* [ ] Specification

## Summary
[summary]: #summary

The blittable feature will give language enforcement to the class of types known as "unmanaged types" in the C# language spec.  This is defined in section 18.2 as a type which is not a reference type and doesn't contain reference type fields at any level of nesting.  

## Motivation
[motivation]: #motivation

The primary motivation is to make it easier to author low level interop code in C#.  Blittable types are one of the core building blocks for interop code, yet abstracting around them is difficult today due to a lack of declarative language support.  

This is the case even though blittable types are well defined in the language spec and many features, like pointers, can operate only on them.  The language chooses to let structs implicitly opt into being blittable by virtue of their construction with no avenue for opting out of such a classification.

While attractive in small applications this is more difficult to manage across a large set of libraries authored by different developers.  It means small field additions to structs can cause compile and runtime breaks in downstream consumers with no warning to the developers that made the change.  This spooky action at a distance is one of the core problems with having an implicit opt in model here. 

A declarative, explicit opt in model makes it easier to code in this area.  Developers can be very explicit about the types they intend for interop.  This gives them compiler help when mistakes are made in their application and in any libraries they consume.  

The lack of compile time support also makes it difficult to abstract over blittable types.  It's not possible for instance to author common helper routines using generic code:

``` c#
void Hash<T>(T value) where T : blittable struct
{
    using (T* p = &value) { 
        ...
    }
}
```

Instead developers are forced to rewrite virtually the same code for all of their blittable types:

``` c#
void Hash(Point p) { 
    ...
}

void Hash(Time t) { 
    ...
}
```

The lack of constraints here also make it impossible to have efficient conversions between streams of data and more structured types.  This is an operation that is common in networking stacks and serialization layers:

``` c#
Span<byte> Convert<T>(ref T value) where T : blittable {
    ...
}
```

Such routines are advantageous because they are provably safe at compile time and allocation free.  Interop authors today can not due this (even though it's at a layer where perf is critical).  Instead they need to rely on allocating routines that have expensive runtime checks to verify values are correctly blittable.

## Detailed design
[design]: #detailed-design

The language will introduce a new declaration modifier named `blittable` that can be applied to `struct` definitions.  

``` c#
blittable struct Point 
{
    public int X;
    public int Y;
}
```

The compiler will enforce that the fields of such a struct definition fit one of the following categories:

- `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, `double`, `decimal`, or `bool`.
- Any `enum` type
- Any pointer type which points to a `blittable` type
- Any user defined struct explicitly declared as `blittable`

Any type fitting the definition above, or any element in the list, is considered a valid `blittable` type.

``` c#
struct User
{
    string FirstName;
    string LastName;
}

blittable struct ItemData
{
    // Error: blittable struct cannot contain field of type User which is not blittable
    User User;
    int Id;
}
```

Note that a user defined struct must be explicitly declared as `blittable` in order to meet the requirements above.  This is required even if the struct otherwise meets the requirements of `blittable`. 

``` c#
struct SimplePoint
{
    public int X;
    public int Y;
}

blittable struct Data
{
    // Error: blittable struct cannot contain field of type SimplePoint which is not blittable
    SimplePoint Point;
}
```

The language will also support the ability to constrain generic type parameters to be `blittable` types.  

``` C#
void M<T>(T p) where T : blittable struct
{
    ...
}

M<Point>(); // Ok
M<User>();  // Error: Type User does not satisfy the blittable constraint.
```

One of the primary motivations for `blittable` structs is ease of interop.  As such it's imperative that such a type have it's field ordered in a sequential, or explicit, layout.  An auto layout of fields makes it impossible to reliably interop the data.  

The default today is for a sequential layout so this doesn't represent a substantial change.  However the compiler will make it illegal to mark such types as having an auto layout.  

``` c#
// Error: A blittable struct may not be marked with an auto layout
[StructLayout(LayoutKind.Auto)]
blittable struct LayoutExample 
{
    ...
}
```

## Drawbacks
[drawbacks]: #drawbacks

The primary drawback of this feature is that it serves a small number of developers: typically low level library authors or frameworks.  Hence it's spending precious language time for a small number of developers. 

Yet these frameworks are are often the basis for the majority of .NET applications out there.  Hence performance / correctness wins at this level can have a riple effect on the .NET ecosystem.  This makes the feature worth considering even with the limited audience. 

There will also likely be a small transition period after this is released where core libraries move to adopt it.  Types like [System.Runtime.InteropServices.ComTypes.FILETIME](https://msdn.microsoft.com/en-us/library/system.runtime.interopservices.comtypes.filetime(v=vs.110).aspx) are common in interop code.  Until it is marked as `blittable` in source though, developers won't be able to depend on it in their libraries.  

## Alternatives
[alternatives]: #alternatives

There are a couple of alternatives to consider:

- The status quo:  The feature is not justified on its own merits and developers continue to use the implicit opt in behavior.
- Generic constraint only: The blittable keyword is used on generic constraints only.  This allows for developers to author efficient helper libraries.  But the types involved lack any declarative support and hence are fragile across distributed development. 


## Unresolved questions
[unresolved]: #unresolved-questions

- blittable vs. unmanaged.  The F# language has a very [similar feature](https://docs.microsoft.com/en-us/dotnet/articles/fsharp/language-reference/generics/constraints) which uses the keyword unmanaged. The blittable name comes from the use in Midori.  May want to look to precedence here and use unmanaged instead. 
- Verifier: does the verifier / runtime need to be updated to understad the use of pointers to generic type parameters?  Or can it simply work as is without changes. 

## Design meetings

n/a
