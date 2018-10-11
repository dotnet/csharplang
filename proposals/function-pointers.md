# Function Pointers

## Summary
This proposal provides language constructs that expose low level IL opcodes that cannot currently
be accessed efficiently, or at all: `ldftn`, `ldvirtftn`, and `calli`. These low level op 
codes can be important in high performance code and developers need an efficient way to access 
them.

## Motivation
The motivations and background for this feature are described in the following issue (as is a 
potential implementation of the feature): 

https://github.com/dotnet/csharplang/issues/191

This is an alternate design propsoal to [compiler intrinsics](https://github.com/dotnet/csharplang/blob/master/proposals/intrinsics.md)

## Detailed Design 

### funcptr
The language will allow for the declaration of function pointers using the `funcptr` syntax. This form closely 
resembles the syntax form of `delegate`.

``` csharp
delegate void DAction(int a);
funcptr void FAction(int a);
```

These types are represented using the function pointer type as outlined in ECMA-335. This means invocation
of a `funcptr` will use `calli` where invocation of a `delegate` will use `callvirt` on the `Invoke` method.
Syntactically though invocation looks no different:

``` csharp
void Example(FAction f) {
    f(42);
}
```

The `calli` instruction requires the calling convention be specified as a part of the invocation. The default 
for `funcptr` will be managed. Alternate forms can be specified by adding the appropriate modifier after the 
`funcptr` keyword: `cdecl`, `fastcall`, `stdcall`, `thiscall` or `winapi`. Example:

``` csharp
// This method will be invoked using the cdecl calling convention
funcptr cdecl int Square(int value);
```

Conversions between `funcptr` types is done based on their signature, not name. Hence when two `funcptr` 
declarations have the same signature they have an identity conversion no matter what their name is:

``` csharp
funcptr int Sum(int left, int right);
funcptr int Add(int x, int y);

void Conversions() {
    Sum s = ...;
    Add a = s; // okay
    Console.WriteLine(a == s); // True
}
```

The use of ECMA-335 function pointer types means instances are not convertible to `objec

Invocations of a `funcptr` will use the `calli` instruction. 

The `funcptr` type differs from a `delegate` in the following ways:

Restrictions:

- Cannot overload when the only difference in parameter types is the name of the function pointer. 
- Custom attributes cannot be applied to a `funcptr` or any of its elements.
- A `funcptr` type is not convertible to `object`. 

### Address of functions

### Metadata


## Open Issuess

### Issue 1

### Issue 2

## Considerations

### Using delegates
Instead of using a new syntax element, `funcptr`, simply use exisiting `delegate` types with a `*` following the type:

``` csharp
Func<object, object, bool>* ptr = &object.ReferenceEquals;
```

Handling calling convention can be done by annotating the `delegate` types with an attribute that specifies 
a `CallingConvention` value. The lack of an attribute would signify the managed calling convention.

Encoding this in IL is problematic. The underlying value needs to be represented as a pointer yet it also must:

1. Have a unique type to allow for overloads with different function pointer types. 
1. Be equivalent for OHI purposes across assembly boundaries.

The last point is particularly problematic. This mean that every assembly which uses `Func<int>*` must encode 
an equivalent type in metadata even though `Func<int>*` is defined in an assembly though don't control. 
Additionally any other type which is defined with the name `System.Func<T>` in an assembly that is not mscorlib 
must be different than the version defined in mscorlib.

One option that was explored was emitting such a pointer as `mod_req(Func<int>) void*`. This doesn't 
work though as a `mod_req` cannot bind to a `TypeSpec` and hence cannot target generic instantiations.

### Unsafe
Requiring the unsafe keyword 

## Future Considerations
