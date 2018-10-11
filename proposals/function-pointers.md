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
The language will allow for the declaration of function pointers using the `funcptr` syntax. The declaration and usage
of function pointers closely resemble that of `delegate`:

``` csharp
unsafe class Example {
    delegate void DAction(int a);
    funcptr void FAction(int a);

    void Example(DAction d, FAction f) {
        d(42);
        f(42);
    }
}
```

These types are represented using the function pointer type as outlined in ECMA-335. This means invocation
of a `funcptr` will use `calli` where invocation of a `delegate` will use `callvirt` on the `Invoke` method.
Syntactically though invocation looks no different:

The `calli` instruction requires the calling convention be specified as a part of the invocation. The default 
for `funcptr` will be managed. Alternate forms can be specified by adding the appropriate modifier after the 
`funcptr` keyword: `cdecl`, `fastcall`, `stdcall`, `thiscall` or `winapi`. Example:

``` csharp
// This method will be invoked using the cdecl calling convention
funcptr cdecl int Square(int value);
```

A `funcptr` type is a pointer type which means it has all of the capabilities and restrictions of a standard pointer
type:
- Only valid in an `unsafe` context.
- Methods which contain a `funcptr` parameter or return type can only be called from an `unsafe` context.
- Cannot be converted to `object`.
- Cannot be used an a generic argument.
- Can implicitly convert to and from `void*`

Conversions between `funcptr` types is done based on their signature, not name. Hence when two `funcptr` 
declarations have the same signature they have an identity conversion no matter what the name is:

``` csharp
unsafe class Example {
    funcptr int Sum(int left, int right);
    funcptr int Add(int x, int y);
    funcptr int Echo(int x);

    void Conversions() {
        Sum s = ...;
        Echo e = ...;

        Add a1 = s; // okay
        Add a1 = e; // error: incompatible signatures
        Console.WriteLine(a1 == s); // True
    }
}
```

In addition to declaring a named `funcptr` type, as you declare a `delegate`, it is possible to use an unnamed 
`funcptr` type directly. This type can be used anywhere a type declaration would occur:

``` csharp
unsafe struct Example {
    funcptr int (int) Field;
    unsafe void UnnamedExample(funcptr int(int) ptr) {
        int x = ptr(42);
        Field = ptr;
        ...
    }
}
```

Restrictions:

- Cannot overload when the only difference in parameter types is the name of the function pointer. 
- Custom attributes cannot be applied to a `funcptr` or any of its elements.
- A `funcptr` parameter cannot be marked as `params`
- A `funcptr` type has all of the restrictions of a normal pointer type.

### Allow addresss-of to target methods

Method groups will now be allowed as arguments to an address-of expression. The type of such an 
expression will be an unnamed `funcptr` which has the equivalent signature of the target method:

``` csharp
unsafe class Util { 
    public static void Log() { } 

    funcptr void Action();
    funcptr int Func();
    void Use() {
        funcptr void() ptr1 = &Util.Log; 
        Action ptr2 = &Util.Log;

        // Error: type "funcptr void()" not compatible with "funcptr int()";
        Func ptr3 = &Util.Log; 

        // Okay. Conversion to void* is always allowed.
        void* v = &Util.Log;
   }
}
```

The conversion of an address-of method group to `funcptr` has roughly the same process as method group to `delegate`  
conversion. The only additional restriction is that only members of the method group that are marked as `static` will
be considered. This means developers can depend on overload resolution rules to work in conjunction with the 
address-of operator:

``` csharp
unsafe class Util { 
    public static void Log() { } 
    public static void Log(string p1) { } 
    public static void Log(int i) { };

    funcptr void Action1();
    funcptr void Action2();

    void Use() {
        Action1 a1 = &Log; // Log()
        Action2 a2 = &Log; // Log(int i)

        // Error: ambiguous conversion from method group Log to "void*"
        void* v = &Log; 
    }
```

The address-of operator will be implemented using the `ldftn` instruction.

Restrictions of this feature:

- Only applies to methods marked as `static`.
- Local functions cannot be used in `&`. The implementation details of these methods are
deliberately not specified by the language. This includes whether they are static vs. instance or
exactly what signature they are emitted with.

### Better function member
The better function member specification will be changed to include the following line:

> A `funcptr` is more specific than `void*`

This means that it is possible to overload on `void*` and a `funcptr` and still sensibly use the address-of operator.

## Open Issuess

- Round tripping function pointer names, as well as parameter names, through metadata will require additional work. The
function pointer type itself is natively supported by CLI but that does not include any names. This is not anticipated
to be a big issue, just needs design work.

## Considerations

### Don't require unsafe at declaration
Instead of requiring `unsafe` at every use of a `funcptr`, only require it at the point where a method group is
converted to a `funcptr`. This is where the core safety issues come into play (knowing that the containing assembly
cannot be unloaded while the value is alive). Requiring `unsafe` on the other locations can be seen as excessive.

This is how the design was originally intended. But the resulting language rules felt very awkward. It's impossible to
hide the fact that this is a pointer value and it kept peeking through even without the `unsafe` keyword. For example 
the conversion to `object` can't be allowed, it can't be a member of a `class`, etc ... The C# design is to require
`unsafe` for all pointer uses and hence this design follows that.

Developers will still be capable of preventing a _safe_ wrapper on top of `funcptr` values the same way that they do 
for normal pointer types today. Consider:

``` csharp
unsafe struct Action {
    funcptr void() _ptr;

    Action(funcptr void() ptr) => _ptr = ptr;
    public void Invoke() => _ptr();
}
```

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

### No names altogether
Given that a `funcptr` can be used without names why even allow names at all? The underlying CLI primitive doesn't have
names hence the use of names is purely a C# invention. That ends up being a leaky abstraction in some cases (like
not allowing overloads when `funcptr` differ by only names). 

At the same time, `funcptr` look and feel so much like `delegate` types, not allowing them to be named would be seen
as an enormous gap by customers. The leaky abstraction is wort the trade offs here.

### Requiring names always
Given that names are allowed for `funcptr` why not just require them always? Given that `funcptr` is an existing CLI
type there are uses of it in the ecosystem today. None of those uses will have the metadata serialization format 
chosen by the C# compiler. This means the feature would be using CLI function pointers but not interopting with any 
existing usage.

## Future Considerations

### static local functions

This refers to [the proposal](https://github.com/dotnet/csharplang/issues/1565) to allow the 
`static` modifier on local functions. Such a function would be guaranteed to be emitted as 
`static` and with the exact signature specified in source code. Such a function should be a valid
argument to `&` as it contains none of the problems local functions have today