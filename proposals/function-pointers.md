# Function Pointers

## Summary
This proposal provides language constructs that expose IL opcodes that cannot currently be accessed efficiently,
or at all, in C# today: `ldftn` and `calli`. These IL opcodes can be important in high performance code and developers
need an effecient way to access them.

## Motivation
The motivations and background for this feature are described in the following issue (as is a 
potential implementation of the feature): 

https://github.com/dotnet/csharplang/issues/191

This is an alternate design propsoal to [compiler intrinsics]
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
The default calling convention will be `managed `. Alternate forms can be specified by adding the appropriate modifier 
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

        p1 = p2; // okay Func1 and Func3 have compatible signatures
        Console.WriteLine(p2 == p1); // True
        p2 = p2; // error: calling conventions are incompatible
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
    'managed' |
    'cdecl' |
    'winapi' | 
    'fastcall' | 
    'stdcall' | 
    'thiscall' ;
```

When there is a nested function pointer, a function pointer which has or returns a function pointer, parens can be 
opitionally used to disambiguate the signature. Though they are not required and the resulting types are equivalent.

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
convention. Inthat case the parser would process the return type as a calling convention instead of a type. To resolve
this the developer must specify both the calling convention and the return type. 

``` csharp
class cdecl { }

// Function pointer which has a cdecl calling convention, a cdecl return type and takes a single 
// paramater of type cdecl;
func* cdecl cdecl(cdecl);
```

### Allow addresss-of to target methods

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
- Local functions cannot be used in `&`. The implementation details of these methods are
deliberately not specified by the language. This includes whether they are static vs. instance or
exactly what signature they are emitted with.

### Better function member
The better function member specification will be changed to include the following line:

> A `func*` is more specific than `void*`

This means that it is possible to overload on `void*` and a `func*` and still sensibly use the address-of operator.

## Open Issuess

- The address-of operator is limited to `static` methods in this proposal. It can be made to work with instance methods 
but the behavior can be confusing to developers. The `this` type becomes an explicit first parameter on the `func*` 
type. This means the behavior and usage would differ significantly from `delegate`. This extra confusion was the main 
reason it was not included in the design.

## Considerations

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
Instead of using a new syntax element, `func*`, simply use exisiting `delegate` types with a `*` following the type:

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
Given that a `func*` can be used without names why even allow names at all? The underlying CLI primitive doesn't have
names hence the use of names is purely a C# invention. That ends up being a leaky abstraction in some cases (like
not allowing overloads when `func*` differ by only names). 

At the same time, `func*` look and feel so much like `delegate` types, not allowing them to be named would be seen
as an enormous gap by customers. The leaky abstraction is wort the trade offs here.

### Requiring names always
Given that names are allowed for `func*` why not just require them always? Given that `func*` is an existing CLI
type there are uses of it in the ecosystem today. None of those uses will have the metadata serialization format 
chosen by the C# compiler. This means the feature would be using CLI function pointers but not interopting with any 
existing usage.

## Future Considerations

### static local functions

This refers to [the proposal](https://github.com/dotnet/csharplang/issues/1565) to allow the 
`static` modifier on local functions. Such a function would be guaranteed to be emitted as 
`static` and with the exact signature specified in source code. Such a function should be a valid
argument to `&` as it contains none of the problems local functions have today


*** static delegates https://github.com/dotnet/csharplang/blob/master/proposals/static-delegates.md
*** PR feedback needs to be gone through.