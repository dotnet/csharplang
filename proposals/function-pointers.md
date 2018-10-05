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

### Item 1

Restrictions:

### Item 2

## Open Issuess

### Issue 1

### Issue 2

## Considerations

### Using delegates
Instead of using a new syntax element, `funcptr`, simply use exisiting `delegate` types with a `*` following the type:

``` csharp
Func<object, object, bool>* ptr = &object.ReferenceEquals;
```

Handling calling convention can be done by annotating the `delegate` types with an attribute that specifies a `CallingConvention` value. The lack of an attribute would signify the managed calling convention.

Encoding this in IL is problematic. The underlying value needs to be represented as a pointer yet it also must:

1. Have a unique type to allow for overloads with different function pointer types. 
1. Be equivalent for OHI purposes across assembly boundaries.

The last point is particularly problematic. This mean that every assembly which uses `Func<int>*` must encode an equivalent type in metadata even though `Func<int>*` is defined in an assembly though don't control. Additionally any other type which is defined with the name `System.Func<T>` in an assembly that is not mscorlib must be different than the version defined in mscorlib.

One option that was explored was emitting such a pointer as `mod_req(Func<int>) void*`. This doesn't work though as a `mod_req` cannot bind to a `TypeSpec` and hence cannot target generic instantiations.

## Future Considerations
