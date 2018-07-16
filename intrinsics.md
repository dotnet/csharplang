# Compiler Intrinsics

## Summary

This proposal provides language constructs that expose low level IL opcodes that cannot currently
be accessed efficiently, or at all: `ldftn`, `ldvirtftn`, `ldtoken` and `calli`. These low level op 
codes can be important in high performance code and developers need an efficient way to access 
them.

## Motivation

The motivations and background for this feature are described in the following issue (as is a 
potential implementation of the feature): 

https://github.com/dotnet/csharplang/issues/191

This alternate design proposal comes after reviewing a prototype implementation of the original
proposal by @msjabby as well as the use throughout a significant code base. This design was done 
with significant input from @mjsabby, @tmat and @jkotas.

## Detailed Design 

### Allow address of to target methods

Method groups will now be allowed as arguments to an address-of expression. The type of such an 
expression will be `void*`. 

``` csharp
class Util { 
    public static void Log() { } 
}

// ldftn Util.Log
void* ptr = &Util.Log; 
```

Given there is no delegate conversion here the only mechanism for filtering members in the method
group is by static / instance access. If that cannot distinguish the members then a compile time
error will occur.

``` csharp
class Util { 
    public void Log() { } 
    public void Log(string p1) { } 
    public static void Log(int i) { };
}

unsafe {
    // Error: Method group Log has more than one applicable candidate.
    void* ptr1 = &Log; 

    // Okay: only one static member to consider here.
    void* ptr2 = &Util.Log;
}
```

The addressof expression in this context will be implemented in the following manner:

- ldftn: when the method is non-virtual.
- ldvirtftn: when the method is virtual.

Restrictions of this feature:

- Instance methods can only be specified when using an invocation expression on a value
- Local functions cannot be used in `&`. The implementation details of these methods are
deliberately not specified by the language. This includes whether they are static vs. instance or
exactly what signature they are emitted with.

### handleof

The `handleof` contextual keyword will translate a field, member or type into their equivalent 
`RuntimeHandle` type using the `ldtoken` instruction. The exact type of the expression will 
depend on the kind of the name in `handleof`:

- field: `RuntimeFieldHandle`
- type: `RuntimeTypeHandle`
- method: `RuntimeMethodHandle`

The arguments to `handleof` are identical to `nameof`. It must be a simple name, qualified name, 
member access, base access with a specified member, or this access with a specified member. The 
argument expression identifies a code definition, but it is never evaluated.

The `handleof` expression is evaluated at runtime and has a return type of `RuntimeHandle`. This 
can be executed in safe code as well as unsafe. 

``` 
RuntimeHandle stringHandle = handleof(string);
```

Restrictions of this feature:

- Properties cannot be used in a `handleof` expression.
- The `handleof` expression cannot be used when there is an existing `handleof` name in scope. For 
example a type, namespace, etc ...

### calli

The compiler will add support for a new type of `extern` function that efficiently translates into
a `.calli` instruction. The extern attribute will be marked with an attribute of the following
shape:

``` csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class CallIndirectAttribute : Attribute
{
    public CallingConvention CallingConvention { get; }
    public CallIndirectAttribute(CallingConvention callingConvention)
    {
        CallingConvention = callingConvention;
    }
}
```

This allows developers to define methods in the following form:

``` csharp
[CallIndirect(CallingConvention.Cdecl)]
static extern int MapValue(string s, void *ptr);

unsafe {
    var i = MapValue("42", &int.Parse);
    Console.WriteLine(i);
}
```

Restrictions on the method which has the `CallIndirect` attribute applied:

- Cannot have a `DllImport` attribute.
- Cannot be generic.

## Open Issuess

### CallingConvention

The `CallIndirectAttribute` as designed uses the `CallingConvention` enum which lacks an entry for
managed calling conventions. The enum either needs to be extended to include this calling convention
or the attribute needs to take a different approach.

## Considerations

### Disambiguating method groups

There was some discussion around features that would make it easier to disambiguate method groups
passed to an address-of expression. For instance potentially adding signature elements to the 
syntax:

``` csharp
class Util {
    public static void Log() { ... }
    public static void Log(string) { ... }
}

unsafe {
    // Error: ambiguous Log
    void *ptr1 = &Util.Log;

    // Use Util.Log();
    void *ptr2 = &Util.Log();
}
```

This was rejected because a compelling case could not be made nor could a simple syntax be 
envisioned here. Also there is a fairly straight forward work around: simple define another 
method that is unambiguous and uses C# code to call into the desired function. 

``` csharp
class Workaround {
    public static void LocalLog() => Util.Log();
}
unsafe { 
    void* ptr = &Workaround.LocalLog;
}
```

This becomes even simpler if `static` local functions enter the language. Then the work around
could be defined in the same function that used the ambiguous address-of operation:

``` csharp
unsafe { 
    static void LocalLog() => Util.Log();
    void* ptr = &Workaround.LocalLog;
}
```

### LoadTypeTokenInt32

The original proposal allowed for metadata tokens to be loaded as `int` values at compile time. 
Essentially have `tokenof` that has the same arguments as `handleof` but is evaluated at 
compile time to an `int` constant. 

This was rejected as it causes significant problem for IL rewrites (of which .NET has many). Such 
rewriters often manipulate the metadata tables in a way that could invalidate these values. There 
is no reasonable way for such rewriters to update these values when they are stored as simple 
`int` values.

The underlying idea of having an opaque handle for metadata entries will continue to be explored 
by the runtime team. 

## Future Considerations

### static local functions

This refers to [the proposal](https://github.com/dotnet/csharplang/issues/1565) to allow the 
`static` modifier on local functions. Such a function would be guaranteed to be emitted as 
`static` and with the exact signature specified in source code. Such a function should be a valid
argument to `&` as it contains none of the problems local functions have today.

### NativeCallableAttribute

The CLR has a feature that allows for managed methods to be emitted in such a way that they are 
directly callabe from native code. This is done by adding the `NativeCallableAttribute` to 
methods. Such a method is only callable from native code and hence must contain only blittable 
types in the signature. Calling from managed code results in a runtime error. 

This feature would pattern well with this proposal as it would allow:

- Passing a funtion defined in managed code to native code as a function pointer (via address-of)
with no overhead in managed or native code. 
- Runtime can introduce use site errors for such functions in managed code to prevent them from
being invoked at compile time.




