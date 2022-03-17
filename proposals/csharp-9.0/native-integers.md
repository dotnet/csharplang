# Native-sized integers

## Summary
[summary]: #summary

Language support for a native-sized signed and unsigned integer types.

The motivation is for interop scenarios and for low-level libraries.

## Design
[design]: #design

The identifiers `nint` and `nuint` are new contextual keywords that represent native signed and unsigned integer types.
The identifiers are only treated as keywords when name lookup does not find a viable result at that program location.
```C#
nint x = 3;
string y = nameof(nuint);
_ = nint.Equals(x, 3);
```

The types `nint` and `nuint` are represented by the underlying types `System.IntPtr` and `System.UIntPtr` with compiler surfacing additional conversions and operations for those types as native ints.

### Constants

Constant expressions may be of type `nint` or `nuint`.
There is no direct syntax for native int literals. Implicit or explicit casts of other integral constant values can be used instead: `const nint i = (nint)42;`.

`nint` constants are in the range [ `int.MinValue`, `int.MaxValue` ].

`nuint` constants are in the range [ `uint.MinValue`, `uint.MaxValue` ].

There are no `MinValue` or `MaxValue` fields on `nint` or `nuint` because, other than `nuint.MinValue`, those values cannot be emitted as constants.

Constant folding is supported for all unary operators { `+`, `-`, `~` } and binary operators { `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `&`, `|`, `^`, `<<`, `>>` }.
Constant folding operations are evaluated with `Int32` and `UInt32` operands rather than native ints for consistent behavior regardless of compiler platform.
If the operation results in a constant value in 32-bits, constant folding is performed at compile-time.
Otherwise the operation is executed at runtime and not considered a constant.

### Conversions
There is an identity conversion between `nint` and `IntPtr`, and between `nuint` and `UIntPtr`.
There is an identity conversion between compound types that differ by native ints and underlying types only: arrays, `Nullable<>`, constructed types, and tuples.

The tables below cover the conversions between special types.
(The IL for each conversion includes the variants for `unchecked` and `checked` contexts if different.)

| Operand | Target | Conversion | IL |
|:---:|:---:|:---:|:---:|
| `object` | `nint` | Unboxing | `unbox` |
| `void*` | `nint` | PointerToVoid | `conv.i` |
| `sbyte` | `nint` | ImplicitNumeric | `conv.i` |
| `byte` | `nint` | ImplicitNumeric | `conv.u` |
| `short` | `nint` | ImplicitNumeric | `conv.i` |
| `ushort` | `nint` | ImplicitNumeric | `conv.u` |
| `int` | `nint` | ImplicitNumeric | `conv.i` |
| `uint` | `nint` | ExplicitNumeric | `conv.u` / `conv.ovf.u` |
| `long` | `nint` | ExplicitNumeric | `conv.i` / `conv.ovf.i` |
| `ulong` | `nint` | ExplicitNumeric | `conv.i` / `conv.ovf.i` |
| `char` | `nint` | ImplicitNumeric | `conv.i` |
| `float` | `nint` | ExplicitNumeric | `conv.i` / `conv.ovf.i` |
| `double` | `nint` | ExplicitNumeric | `conv.i` / `conv.ovf.i` |
| `decimal` | `nint` | ExplicitNumeric | `long decimal.op_Explicit(decimal) conv.i` / `... conv.ovf.i` |
| `IntPtr` | `nint` | Identity | |
| `UIntPtr` | `nint` | None | |
| `object` | `nuint` | Unboxing | `unbox` |
| `void*` | `nuint` | PointerToVoid | `conv.u` |
| `sbyte` | `nuint` | ExplicitNumeric | `conv.u` / `conv.ovf.u` |
| `byte` | `nuint` | ImplicitNumeric | `conv.u` |
| `short` | `nuint` | ExplicitNumeric | `conv.u` / `conv.ovf.u` |
| `ushort` | `nuint` | ImplicitNumeric | `conv.u` |
| `int` | `nuint` | ExplicitNumeric | `conv.u` / `conv.ovf.u` |
| `uint` | `nuint` | ImplicitNumeric | `conv.u` |
| `long` | `nuint` | ExplicitNumeric | `conv.u` / `conv.ovf.u` |
| `ulong` | `nuint` | ExplicitNumeric | `conv.u` / `conv.ovf.u` |
| `char` | `nuint` | ImplicitNumeric | `conv.u` |
| `float` | `nuint` | ExplicitNumeric | `conv.u` / `conv.ovf.u` |
| `double` | `nuint` | ExplicitNumeric | `conv.u` / `conv.ovf.u` |
| `decimal` | `nuint` | ExplicitNumeric | `ulong decimal.op_Explicit(decimal) conv.u` / `... conv.ovf.u.un`  |
| `IntPtr` | `nuint` | None | |
| `UIntPtr` | `nuint` | Identity | |
|Enumeration|`nint`|ExplicitEnumeration||
|Enumeration|`nuint`|ExplicitEnumeration||

| Operand | Target | Conversion | IL |
|:---:|:---:|:---:|:---:|
| `nint` | `object` | Boxing | `box` |
| `nint` | `void*` | PointerToVoid | `conv.i` |
| `nint` | `nuint` | ExplicitNumeric | `conv.u` / `conv.ovf.u` |
| `nint` | `sbyte` | ExplicitNumeric | `conv.i1` / `conv.ovf.i1` |
| `nint` | `byte` | ExplicitNumeric | `conv.u1` / `conv.ovf.u1` |
| `nint` | `short` | ExplicitNumeric | `conv.i2` / `conv.ovf.i2` |
| `nint` | `ushort` | ExplicitNumeric | `conv.u2` / `conv.ovf.u2` |
| `nint` | `int` | ExplicitNumeric | `conv.i4` / `conv.ovf.i4` |
| `nint` | `uint` | ExplicitNumeric | `conv.u4` / `conv.ovf.u4` |
| `nint` | `long` | ImplicitNumeric | `conv.i8` / `conv.ovf.i8` |
| `nint` | `ulong` | ExplicitNumeric | `conv.i8` / `conv.ovf.i8` |
| `nint` | `char` | ExplicitNumeric | `conv.u2` / `conv.ovf.u2` |
| `nint` | `float` | ImplicitNumeric | `conv.r4` |
| `nint` | `double` | ImplicitNumeric | `conv.r8` |
| `nint` | `decimal` | ImplicitNumeric | `conv.i8 decimal decimal.op_Implicit(long)` |
| `nint` | `IntPtr` | Identity | |
| `nint` | `UIntPtr` | None | |
| `nint` |Enumeration|ExplicitEnumeration||
| `nuint` | `object` | Boxing | `box` |
| `nuint` | `void*` | PointerToVoid | `conv.u` |
| `nuint` | `nint` | ExplicitNumeric | `conv.i` / `conv.ovf.i` |
| `nuint` | `sbyte` | ExplicitNumeric | `conv.i1` / `conv.ovf.i1` |
| `nuint` | `byte` | ExplicitNumeric | `conv.u1` / `conv.ovf.u1` |
| `nuint` | `short` | ExplicitNumeric | `conv.i2` / `conv.ovf.i2` |
| `nuint` | `ushort` | ExplicitNumeric | `conv.u2` / `conv.ovf.u2` |
| `nuint` | `int` | ExplicitNumeric | `conv.i4` / `conv.ovf.i4` |
| `nuint` | `uint` | ExplicitNumeric | `conv.u4` / `conv.ovf.u4` |
| `nuint` | `long` | ExplicitNumeric | `conv.i8` / `conv.ovf.i8` |
| `nuint` | `ulong` | ImplicitNumeric | `conv.u8` / `conv.ovf.u8` |
| `nuint` | `char` | ExplicitNumeric | `conv.u2` / `conv.ovf.u2.un` |
| `nuint` | `float` | ImplicitNumeric | `conv.r.un conv.r4` |
| `nuint` | `double` | ImplicitNumeric | `conv.r.un conv.r8` |
| `nuint` | `decimal` | ImplicitNumeric | `conv.u8 decimal decimal.op_Implicit(ulong)` |
| `nuint` | `IntPtr` | None | |
| `nuint` | `UIntPtr` | Identity | |
| `nuint` |Enumeration|ExplicitEnumeration||

Conversion from `A` to `Nullable<B>` is:
- an implicit nullable conversion if there is an identity conversion or implicit conversion from `A` to `B`;
- an explicit nullable conversion if there is an explicit conversion from `A` to `B`;
- otherwise invalid.

Conversion from `Nullable<A>` to `B` is:
- an explicit nullable conversion if there is an identity conversion or implicit or explicit numeric conversion from `A` to `B`;
- otherwise invalid.

Conversion from `Nullable<A>` to `Nullable<B>` is:
- an identity conversion if there is an identity conversion from `A` to `B`;
- an explicit nullable conversion if there is an implicit or explicit numeric conversion from `A` to `B`;
- otherwise invalid.

### Operators

The predefined operators are as follows.
These operators are considered during overload resolution based on normal rules for implicit conversions _if at least one of the operands is of type `nint` or `nuint`_.

(The IL for each operator includes the variants for `unchecked` and `checked` contexts if different.)

| Unary | Operator Signature | IL |
|:---:|:---:|:---:|
| `+` | `nint operator +(nint value)` | `nop` |
| `+` | `nuint operator +(nuint value)` | `nop` |
| `-` | `nint operator -(nint value)` | `neg` |
| `~` | `nint operator ~(nint value)` | `not` |
| `~` | `nuint operator ~(nuint value)` | `not` |

| Binary | Operator Signature | IL |
|:---:|:---:|:---:|
| `+` | `nint operator +(nint left, nint right)` | `add` / `add.ovf` |
| `+` | `nuint operator +(nuint left, nuint right)` | `add` / `add.ovf.un` |
| `-` | `nint operator -(nint left, nint right)` | `sub` / `sub.ovf` |
| `-` | `nuint operator -(nuint left, nuint right)` | `sub` / `sub.ovf.un` |
| `*` | `nint operator *(nint left, nint right)` | `mul` / `mul.ovf` |
| `*` | `nuint operator *(nuint left, nuint right)` | `mul` / `mul.ovf.un` |
| `/` | `nint operator /(nint left, nint right)` | `div` |
| `/` | `nuint operator /(nuint left, nuint right)` | `div.un` |
| `%` | `nint operator %(nint left, nint right)` | `rem` |
| `%` | `nuint operator %(nuint left, nuint right)` | `rem.un` |
| `==` | `bool operator ==(nint left, nint right)` | `beq` / `ceq` |
| `==` | `bool operator ==(nuint left, nuint right)` | `beq` / `ceq` |
| `!=` | `bool operator !=(nint left, nint right)` | `bne` |
| `!=` | `bool operator !=(nuint left, nuint right)` | `bne` |
| `<` | `bool operator <(nint left, nint right)` | `blt` / `clt` |
| `<` | `bool operator <(nuint left, nuint right)` | `blt.un` / `clt.un` |
| `<=` | `bool operator <=(nint left, nint right)` | `ble` |
| `<=` | `bool operator <=(nuint left, nuint right)` | `ble.un` |
| `>` | `bool operator >(nint left, nint right)` | `bgt` / `cgt` |
| `>` | `bool operator >(nuint left, nuint right)` | `bgt.un` / `cgt.un` |
| `>=` | `bool operator >=(nint left, nint right)` | `bge` |
| `>=` | `bool operator >=(nuint left, nuint right)` | `bge.un` |
| `&` | `nint operator &(nint left, nint right)` | `and` |
| `&` | `nuint operator &(nuint left, nuint right)` | `and` |
| <code>&#124;</code> | <code>nint operator &#124;(nint left, nint right)</code> | `or` |
| <code>&#124;</code> | <code>nuint operator &#124;(nuint left, nuint right)</code> | `or` |
| `^` | `nint operator ^(nint left, nint right)` | `xor` |
| `^` | `nuint operator ^(nuint left, nuint right)` | `xor` |
| `<<` | `nint operator <<(nint left, int right)` | `shl` |
| `<<` | `nuint operator <<(nuint left, int right)` | `shl` |
| `>>` | `nint operator >>(nint left, int right)` | `shr` |
| `>>` | `nuint operator >>(nuint left, int right)` | `shr.un` |

For some binary operators, the IL operators support additional operand types
(see [ECMA-335](https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf) III.1.5 Operand type table).
But the set of operand types supported by C# is limited for simplicity and for consistency with existing operators in the language.

Lifted versions of the operators, where the arguments and return types are `nint?` and `nuint?`, are supported.

Compound assignment operations `x op= y` where `x` or `y` are native ints follow the same rules as with other primitive types with pre-defined operators.
Specifically the expression is bound as `x = (T)(x op y)` where `T` is the type of `x` and where `x` is only evaluated once.

The shift operators should mask the number of bits to shift - to 5 bits if `sizeof(nint)` is 4, and to 6 bits if `sizeof(nint)` is 8.
(see [§11.10](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1110-shift-operators)) in C# spec).

The C#9 compiler will report errors binding to predefined native integer operators when compiling with an earlier language version,
but will allow use of predefined conversions to and from native integers.

`csc -langversion:9 -t:library A.cs`
```C#
public class A
{
    public static nint F;
}
```

`csc -langversion:8 -r:A.dll B.cs`
```C#
class B : A
{
    static void Main()
    {
        F = F + 1; // error: nint operator+ not available with -langversion:8
        F = (System.IntPtr)F + 1; // ok
    }
}
```

### Pointer arithmetic
There are no predefined operators in C# for pointer addition or subtraction with native integer offsets.
Instead, `nint` and `nuint` values are promoted to `long` and `ulong` and pointer arithmetic uses predefined operators for those types.
```C#
static T* AddLeftS(nint x, T* y) => x + y;   // T* operator +(long left, T* right)
static T* AddLeftU(nuint x, T* y) => x + y;  // T* operator +(ulong left, T* right)
static T* AddRightS(T* x, nint y) => x + y;  // T* operator +(T* left, long right)
static T* AddRightU(T* x, nuint y) => x + y; // T* operator +(T* left, ulong right)
static T* SubRightS(T* x, nint y) => x - y;  // T* operator -(T* left, long right)
static T* SubRightU(T* x, nuint y) => x - y; // T* operator -(T* left, ulong right)
```

### Binary numeric promotions
The _binary numeric promotions_ informative text (see [§11.4.7.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11473-binary-numeric-promotions)) in C# spec) is updated as follows:

> -   …
> -   Otherwise, if either operand is of type `ulong`, the other operand is converted to type `ulong`, or a binding-time error occurs if the other operand is of type `sbyte`, `short`, `int`, **`nint`**, or `long`.
> -   **Otherwise, if either operand is of type `nuint`, the other operand is converted to type `nuint`, or a binding-time error occurs if the other operand is of type `sbyte`, `short`, `int`, `nint`, or `long`.**
> -   Otherwise, if either operand is of type `long`, the other operand is converted to type `long`.
> -   Otherwise, if either operand is of type `uint` and the other operand is of type `sbyte`, `short`, **`nint`,** or `int`, both operands are converted to type `long`.
> -   Otherwise, if either operand is of type `uint`, the other operand is converted to type `uint`.
> -   **Otherwise, if either operand is of type `nint`, the other operand is converted to type `nint`.**
> -   Otherwise, both operands are converted to type `int`.

### Dynamic

The conversions and operators are synthesized by the compiler and are not part of the underlying `IntPtr` and `UIntPtr` types.
As a result those conversions and operators _are not available_ from the runtime binder for `dynamic`. 

```C#
nint x = 2;
nint y = x + x; // ok
dynamic d = x;
nint z = d + x; // RuntimeBinderException: '+' cannot be applied 'System.IntPtr' and 'System.IntPtr'
```

### Type members

The only constructor for `nint` or `nuint` is the parameter-less constructor.

The following members of `System.IntPtr` and `System.UIntPtr` _are explicitly excluded_ from `nint` or `nuint`:
```C#
// constructors
// arithmetic operators
// implicit and explicit conversions
public static readonly IntPtr Zero; // use 0 instead
public static int Size { get; }     // use sizeof() instead
public static IntPtr Add(IntPtr pointer, int offset);
public static IntPtr Subtract(IntPtr pointer, int offset);
public int ToInt32();
public long ToInt64();
public void* ToPointer();
```

The remaining members of `System.IntPtr` and `System.UIntPtr` _are implicitly included_ in `nint` and `nuint`. For .NET Framework 4.7.2:
```C#
public override bool Equals(object obj);
public override int GetHashCode();
public override string ToString();
public string ToString(string format);
```

Interfaces implemented by `System.IntPtr` and `System.UIntPtr` _are implicitly included_ in `nint` and `nuint`,
with occurrences of the underlying types replaced by the corresponding native integer types.
For instance if `IntPtr` implements `ISerializable, IEquatable<IntPtr>, IComparable<IntPtr>`,
then `nint` implements `ISerializable, IEquatable<nint>, IComparable<nint>`.

### Overriding, hiding, and implementing

`nint` and `System.IntPtr`, and `nuint` and `System.UIntPtr`, are considered equivalent for overriding, hiding, and implementing.

Overloads cannot differ by `nint` and `System.IntPtr`, and `nuint` and `System.UIntPtr`, alone.
Overrides and implementations may differ by `nint` and `System.IntPtr`, or `nuint` and `System.UIntPtr`, alone.
Methods hide other methods that differ by `nint` and `System.IntPtr`, or `nuint` and `System.UIntPtr`, alone.

### Miscellaneous

`nint` and `nuint` expressions used as array indices are emitted without conversion.
```C#
static object GetItem(object[] array, nint index)
{
    return array[index]; // ok
}
```

`nint` and `nuint` cannot be used as an `enum` base type from C#.
```C#
enum E : nint // error: byte, sbyte, short, ushort, int, uint, long, or ulong expected
{
}
```

Reads and writes are atomic for `nint` and `nuint`.

Fields may be marked `volatile` for types `nint` and `nuint`.
[ECMA-334](https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-334.pdf) 15.5.4 does not include `enum` with base type `System.IntPtr` or `System.UIntPtr` however.

`default(nint)` and `new nint()` are equivalent to `(nint)0`; `default(nuint)` and `new nuint()` are equivalent to `(nuint)0`.

`typeof(nint)` is `typeof(IntPtr)`; `typeof(nuint)` is `typeof(UIntPtr)`.

`sizeof(nint)` and `sizeof(nuint)` are supported but require compiling in an unsafe context (as required for `sizeof(IntPtr)` and `sizeof(UIntPtr)`).
The values are not compile-time constants.
`sizeof(nint)` is implemented as `sizeof(IntPtr)` rather than `IntPtr.Size`; `sizeof(nuint)` is implemented as `sizeof(UIntPtr)` rather than `UIntPtr.Size`.

Compiler diagnostics for type references involving `nint` or `nuint` report `nint` or `nuint` rather than `IntPtr` or `UIntPtr`.

### Metadata

`nint` and `nuint` are represented in metadata as `System.IntPtr` and `System.UIntPtr`.

Type references that include `nint` or `nuint` are emitted with a `System.Runtime.CompilerServices.NativeIntegerAttribute` to indicate which parts of the type reference are native ints.

```C#
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Event |
        AttributeTargets.Field |
        AttributeTargets.GenericParameter |
        AttributeTargets.Parameter |
        AttributeTargets.Property |
        AttributeTargets.ReturnValue,
        AllowMultiple = false,
        Inherited = false)]
    public sealed class NativeIntegerAttribute : Attribute
    {
        public NativeIntegerAttribute()
        {
            TransformFlags = new[] { true };
        }
        public NativeIntegerAttribute(bool[] flags)
        {
            TransformFlags = flags;
        }
        public readonly bool[] TransformFlags;
    }
}
```

The encoding of type references with `NativeIntegerAttribute` is covered in [NativeIntegerAttribute.md](https://github.com/dotnet/roslyn/blob/master/docs/features/NativeIntegerAttribute.md).

## Alternatives
[alternatives]: #alternatives

An alternative to the "type erasure" approach above is to introduce new types: `System.NativeInt` and `System.NativeUInt`.
```C#
public readonly struct NativeInt
{
    public IntPtr Value;
}
```
Distinct types would allow overloading distinct from `IntPtr` and would allow distinct parsing and `ToString()`.
But there would be more work for the CLR to handle these types efficiently which defeats the primary purpose of the feature - efficiency.
And interop with existing native int code that uses `IntPtr` would be more difficult. 

Another alternative is to add more native int support for `IntPtr` in the framework but without any specific compiler support.
Any new conversions and arithmetic operations would be supported by the compiler automatically.
But the language would not provide keywords, constants, or `checked` operations.

## Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-05-26.md
- https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-06-13.md
- https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-07-05.md#native-int-and-intptr-operators
- https://github.com/dotnet/csharplang/blob/master/meetings/2019/LDM-2019-10-23.md
- https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-03-25.md
