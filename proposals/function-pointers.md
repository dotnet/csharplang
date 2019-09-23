# Function Pointers

## Summary
This proposal provides language constructs that expose IL opcodes that cannot currently be accessed efficiently,
or at all, in C# today: `ldftn` and `calli`. These IL opcodes can be important in high performance code and developers
need an efficient way to access them.

## Motivation
The motivations and background for this feature are described in the following issue (as is a 
potential implementation of the feature): 

https://github.com/dotnet/csharplang/issues/191

This is an alternate design proposal to [compiler intrinsics]
(https://github.com/dotnet/csharplang/blob/master/proposals/intrinsics.md)

## Detailed Design 

### Function pointers
The language will allow for the declaration of function pointers using the `func*` syntax. The full syntax is described
in detail in the next section but it is meant to resemble the syntax used by `delegate` declarations.

``` csharp
unsafe class Example {
    delegate void DAction(int a);

    void Example(DAction d, func* void(int) f) {
        d(42);
        f(42);
    }
}
```

These types are represented using the function pointer type as outlined in ECMA-335. This means invocation
of a `func*` will use `calli` where invocation of a `delegate` will use `callvirt` on the `Invoke` method.
Syntactically though invocation is identical for both constructs.

The ECMA-335 definition of method pointers includes the calling convention as part of the type signature (section 7.1).
The default calling convention will be `managed`. Alternate forms can be specified by adding the appropriate modifier 
after the `func*` syntax: `cdecl`, `fastcall`, `stdcall`, `thiscall` or `winapi`. Example:

``` csharp
// This method will be invoked using the cdecl calling convention
func* cdecl int(int value);

// This method will be invoked using the stdcall calling convention
func* stdcall int(int value);
```

Conversions between `func*` types is done based on their signature including the calling convention. 

``` csharp
unsafe class Example {
    void Conversions() {
        func* int(int, int) p1 = ...;
        func* managed int(int, int) p2 = ...;
        func* cdecl int(int, int) p3 = ...;

        p1 = p2; // okay p1 and p2 have compatible signatures
        Console.WriteLine(p2 == p1); // True
        p2 = p3; // error: calling conventions are incompatible
    }
}
```

A `func*` type is a pointer type which means it has all of the capabilities and restrictions of a standard pointer
type:
- Only valid in an `unsafe` context.
- Methods which contain a `func*` parameter or return type can only be called from an `unsafe` context.
- Cannot be converted to `object`.
- Cannot be used as a generic argument.
- Can implicitly convert `func*` to `void*`.
- Can explicitly convert from `void*` to `func*`.

Restrictions:
- Custom attributes cannot be applied to a `func*` or any of its elements.
- A `func*` parameter cannot be marked as `params`
- A `func*` type has all of the restrictions of a normal pointer type.

### Function pointer syntax
The full function pointer syntax is represented by the following grammar:

``` 
funcptr_type = 
    'func' '*' [calling_convention] type method_arglist |
    '(' funcptr_type ')' ;

calling_convention = 
    'cdecl' |
    'managed' |
    'stdcall' | 
    'thiscall' |
    'unmanaged' ;
```

The `unmanaged` calling convention represents the default calling convention for native code on the current platform, and is encoded as winapi.

When there is a nested function pointer, a function pointer which has or returns a function pointer, parens can be 
optionally used to disambiguate the signature. Though they are not required and the resulting types are equivalent.

``` csharp
delegate int Func1(string s);
delegate Func1 Func2(Func1 f);

// Function pointer equivalent without parens or calling convention
func* int(string);
func* func* int(string) int(func* int(string));

// Function pointer equivalent without parens and with calling convention
func* managed int(string);
func* managed func* managed int(string) int(func* managed int(string));

// Function pointer equivalent with parens and without calling convention
func* int(string);
func* (func* int(string)) int((func* int(string));

// Function pointer equivalent of with parens and calling convention
func* int(string)
func* managed (func* managed int(string)) int((func* managed int(string));
```

When the calling convention is omitted from the syntax then `managed` will be used as the calling convention. That means
all of the forms of `Func1` and `Func2` defined above are equivalent signatures.

The calling convention cannot be omitted when the return type of the function pointer has the same name as a calling 
convention. In that case, the parser would process the return type as a calling convention instead of a type. To resolve
this the developer must specify both the calling convention and the return type. 

``` csharp
class cdecl { }

// Function pointer which has a cdecl calling convention, a cdecl return type and takes a single 
// parameter of type cdecl;
func* cdecl cdecl(cdecl);
```

### Allow address-of to target methods

Method groups will now be allowed as arguments to an address-of expression. The type of such an 
expression will be a `func*` which has the equivalent signature of the target method and a managed 
calling convention:

``` csharp
unsafe class Util { 
    public static void Log() { } 

    void Use() {
        func* void() ptr1 = &Util.Log;

        // Error: type "func* void()" not compatible with "func int()";
        func* int() ptr2 = &Util.Log; 

        // Okay. Conversion to void* is always allowed.
        void* v = &Util.Log;
   }
}
```

The conversion of an address-of method group to `func*` has roughly the same process as method group to `delegate`  
conversion. There are two additional restrictions to the existing process:
- Only members of the method group that are marked as `static` will be considered. 
- Only a `func*` with a managed calling convention can be the target of such a conversion.

This means developers can depend on overload resolution rules to work in conjunction with the 
address-of operator:

``` csharp
unsafe class Util { 
    public static void Log() { } 
    public static void Log(string p1) { } 
    public static void Log(int i) { };

    void Use() {
        func* void() a1 = &Log; // Log()
        func* void(int) a2 = &Log; // Log(int i)

        // Error: ambiguous conversion from method group Log to "void*"
        void* v = &Log; 
    }
```

The address-of operator will be implemented using the `ldftn` instruction.

Restrictions of this feature:
- Only applies to methods marked as `static`.
- Non-`static` local functions cannot be used in `&`. The implementation details of these methods are
deliberately not specified by the language. This includes whether they are static vs. instance or
exactly what signature they are emitted with.

### Better function member
The better function member specification will be changed to include the following line:

> A `func*` is more specific than `void*`

This means that it is possible to overload on `void*` and a `func*` and still sensibly use the address-of operator.

## Open Issues

### NativeCallableAttribute
This is an attribute used by the CLR to avoid the managed to native prologue when invoking. Methods marked by this 
attribute are only callable from native code, not managed (can’t call methods, create a delegate, etc …). The attribute
is not special to mscorlib; the runtime will treat any attribute with this name with the same semantics. 

It's possible for the runtime and language to work together to fully support this. The language could choose to treat
address-of `static` members with a `NativeCallable` attribute as a `func*` with the specified calling convention.

``` csharp
unsafe class NativeCallableExample {
    [NativeCallable(CallingConvention.CDecl)]
    static void CloseHandle(IntPtr p) => Marshal.FreeHGlobal(p);

    void Use() {
        func* void(IntPtr) p1 = &CloseHandle; // Error: Invalid calling convention

        func* cdecl void(IntPtr) p2 = &CloseHandle; // Okay
    }
}

```

Additionally the language would likely also want to: 

- Flag any managed calls to a method tagged with `NativeCallable` as an error. Given the function can't be invoked from
managed code the compiler should prevent developers from attempting such an invocation.
- Prevent method group conversions to `delegate` when the method is tagged with `NativeCallable`. 

This is not necessary to support `NativeCallable` though. The compiler can support the `NativeCallable` attribute as is
using the existing syntax. The program would simply need to cast to `void*` before casting to the correct `func*` 
signature. That would be no worse than the support today.

``` csharp
void* v = &CloseHandle;
func* cdecl bool(IntPtr) f1 = (func* cdecl bool(IntPtr))v;
```

### Extensible set of unmanaged calling conventions

The set of unmanaged calling conventions supported by the current ECMA-335 encodings is outdated. We have seen requests to add support
for more unmanaged calling conventions, for example:

- [vectorcall](https://docs.microsoft.com/cpp/cpp/vectorcall) https://github.com/dotnet/coreclr/issues/12120
- StdCall with explicit this https://github.com/dotnet/coreclr/pull/23974#issuecomment-482991750

The design of this feature should allow extending the set of unmanaged calling conventions as needed in future. The problems include
limited space for encoding calling conventions (12 out of 16 values are taken in `IMAGE_CEE_CS_CALLCONV_MASK`) and number of places
that need to be touched in order to add a new calling convention. A potential solution is to introduce a new encoding that represents
the calling convention using [`System.Runtime.InteropServices.CallingConvention`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.callingconvention) enum.

For reference, https://github.com/llvm/llvm-project/blob/master/llvm/include/llvm/IR/CallingConv.h has the list of calling conventions
supported by LLVM. While it is unlikely that .NET will ever need to support all of them, it demonstrates that the space of calling
conventions is very rich.

## Considerations

### Allow instance methods
The proposal could be extended to support instance methods by taking advantage of the `EXPLICITTHIS` CLI calling 
convention (named `instance` in C# code). This form of CLI function pointers puts the `this` parameter as an explicit
first parameter of the function pointer syntax. 

``` csharp
unsafe class Instance {
    void Use() {
        func* instance string(Instance) f = &ToString;
        f(this);
    }
}
```

This is sound but adds some complication to the proposal. Particularly because function pointers which differed by the
calling convention `instance` and `managed` would be incompatible even though both cases are used to invoke managed 
methods with the same C# signature. Also in every case considered where this would be valuable to have there was a 
simple work around: use a `static` local function.

``` csharp
unsafe class Instance {
    void Use() {
        static string toString(Instance i) = i.ToString();
        func* string(Instance) f = &toString;
        f(this);
    }
}
```

### Don't require unsafe at declaration
Instead of requiring `unsafe` at every use of a `func*`, only require it at the point where a method group is
converted to a `func*`. This is where the core safety issues come into play (knowing that the containing assembly
cannot be unloaded while the value is alive). Requiring `unsafe` on the other locations can be seen as excessive.

This is how the design was originally intended. But the resulting language rules felt very awkward. It's impossible to
hide the fact that this is a pointer value and it kept peeking through even without the `unsafe` keyword. For example 
the conversion to `object` can't be allowed, it can't be a member of a `class`, etc ... The C# design is to require
`unsafe` for all pointer uses and hence this design follows that.

Developers will still be capable of preventing a _safe_ wrapper on top of `func*` values the same way that they do 
for normal pointer types today. Consider:

``` csharp
unsafe struct Action {
    func* void() _ptr;

    Action(func* void() ptr) => _ptr = ptr;
    public void Invoke() => _ptr();
}
```

### Using delegates
Instead of using a new syntax element, `func*`, simply use existing `delegate` types with a `*` following the type:

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

### Named function pointers
The function pointer syntax can be cumbersome, particularly in complex cases like nested function pointers. Rather than
have developers type out the signature every time the language could allow for named declarations of function pointers
as is done with `delegate`. 

``` csharp
func* void Action();

unsafe class NamedExample {
    void M(Action a) {
        a();
    }
}
```

Part of the problem here is the underlying CLI primitive doesn't have names hence this would be purely a C# invention 
and require a bit of metadata work to enable. That is doable but is a significant about of work. It essentially requires
C# to have a companion to the type def table purely for these names.

Also when the arguments for named function pointers were examined we found they could apply equally well to a number of
other scenarios. For example it would be just as convenient to declare named tuples to reduce the need to type out
the full signature in all cases. 

``` csharp
(int x, int y) Point;

class NamedTupleExample {
    void M(Point p) {
        Console.WriteLine(p.x);
    }
}
```

After discussion we decided to not allow named declaration of `func*` types. If we find there is significant need for
this based on customer usage feedback then we will investigate a naming solution that works for function pointers, 
tuples, generics, etc ... This is likely to be similar in form to other suggestions like full `typedef` support in
the language.

## Future Considerations

### static local functions
This refers to [the proposal](https://github.com/dotnet/csharplang/issues/1565) to allow the 
`static` modifier on local functions. Such a function would be guaranteed to be emitted as 
`static` and with the exact signature specified in source code. Such a function should be a valid
argument to `&` as it contains none of the problems local functions have today

### static delegates
This refers to [the proposal](https://github.com/dotnet/csharplang/issues/302) to allow for the declaration of 
`delegate` types which can only refer to `static` members. The advantage being that such `delegate` instances can be 
allocation free and better in performance sensitive scenarios.

If the function pointer feature is implemented the `static delegate` proposal will likely be closed out. The proposed
advantage of that feature is the allocation free nature. However recent investigations have found that is not possible
to achieve due to assembly unloading. There must be a strong handle from the `static delegate` to the method it refers
to in order to keep the assembly from being unloaded out from under it.

To maintain every `static delegate` instance would be required to allocate a new handle which runs counter to the goals
of the proposal. There were some designs where the allocation could be amortized to a single allocation per call-site
but that was a bit complex and didn't seem worth the trade off. 

That means developers essentially have to decide between the following trade offs:

1. Safety in the face of assembly unloading: this requires allocations and hence `delegate` is already a sufficient 
option.
1. No safety in face of assembly unloading: use a `func*`. This can be wrapped in a `struct` to allow usage outside 
an `unsafe` context in the rest of the code. 
