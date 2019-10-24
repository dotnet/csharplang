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
var y = nameof(nuint);
var z = nint.Zero;
```

The types `nint` and `nuint` are represented by the underlying types `System.IntPtr` and `System.UIntPtr` with compiler surfacing additional conversions and operations for those types as native ints.

### Constants

There is no direct syntax for native int literals. Explicit casts of other integral constant values can be used instead: `(nint)42`.

`nint` constants are in the range [ `int.MinValue`, `int.MaxValue` ].

`nuint` constants are in the range [ `uint.MinValue`, `uint.MaxValue` ].

There are no `MinValue` or `MaxValue` fields on `nint` or `nuint` because, other than `nuint.MinValue`, those values cannot be emitted as constants.

Constant folding is supported for all unary operators { `+`, `-`, `~` } and binary operators { `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `&`, `|`, `<<`, `>>` }.
Constant folding operations are evaluated with `Int32` and `UInt32` operands rather than native ints for consistent behavior regardless of compiler platform.

### Conversions
There are identity conversions between native ints and the underlying types in both directions.
There are identity conversions between compound types that differ by native ints and underlying types only: arrays, `Nullable<>`, constructed types, and tuples.

The tables below conver the conversions between special types.
(The IL for each conversion includes the variants for `unchecked` and `checked` contexts if different.)

| Operand | Target | Conversion | IL |
|:---:|:---:|:---:|:---:|
| `object` | `nint` | Unboxing | `unbox` |
| `string` | `nint` | None | |
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
| `decimal` | `nint` | ExplicitNumeric | `long IntPtr.op_Explicit(decimal) IntPtr IntPtr.op_Explicit(long)` |
| `IntPtr` | `nint` | Identity | |
| `UIntPtr` | `nint` | None | |
| `object` | `nuint` | Unboxing | `unbox` |
| `string` | `nuint` | None | |
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
| `decimal` | `nuint` | ExplicitNumeric | `ulong UIntPtr.op_Explicit(decimal) UIntPtr UIntPtr.op_Explicit(ulong)` |
| `IntPtr` | `nuint` | None | |
| `UIntPtr` | `nuint` | Identity | |

| Operand | Target | Conversion | IL |
|:---:|:---:|:---:|:---:|
| `nint` | `object` | Boxing | `box` |
| `nint` | `string` | None | |
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
| `nint` | `decimal` | ExplicitNumeric | `long decimal.op_Explicit(decimal) IntPtr IntPtr.op_Explicit(long)` |
| `nint` | `IntPtr` | Identity | |
| `nint` | `UIntPtr` | None | |
| `nuint` | `object` | Boxing | `box` |
| `nuint` | `string` | None | |
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
| `nuint` | `decimal` | ExplicitNumeric | `ulong decimal.op_Explicit(decimal) UIntPtr UIntPtr.op_Explicit(ulong)` |
| `nuint` | `IntPtr` | None | |
| `nuint` | `UIntPtr` | Identity | |

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

The following operators are supported.
These operators are considered during overload resolution based on normal rules for implicit conversions of arguments.

In cases where there are two overloads, one for `nint` and one for `nuint`, the native int operand types are marked `native`. In other cases the specific types are provided.
(The IL for each operator includes the variants for `nint` and `nuint` and the variants for `unchecked` and `checked` contexts if different.)

| Unary | Operand | Result | IL |
|:---:|:---:|:---:|:---:|
| `+` | `native` | `native` | |
| `-` | `nint` | `nint` | `neg` |
| `~` | `native` | `native` | `not` |

| Binary | Left | Right | Result | IL |
|:---:|:---:|:---:|:---:|:---:|
| `+` | `native` | `native` | `native` | `add` / `add.ovf` / `add.ovf.un` |
| `-` | `native` | `native` | `native` | `sub` / `sub.ovf` / `sub.ovf.un` |
| `*` | `native` | `native` | `native` | `mul` / `mul.ovf` / `mul.ovf.un` |
| `/` | `native` | `native` | `native` | `div` / `div.un` |
| `%` | `native` | `native` | `native` | `rem` / `rem.un` |
| `==` | `native` | `native` | `native` | `beq` / `ceq` |
| `!=` | `native` | `native` | `native` | `bne` |
| `<` | `native` | `native` | `native` | `blt` / `clt` / `blt.un` / `clt.un` |
| `<=` | `native` | `native` | `native` | `ble` / `ble.un` |
| `>` | `native` | `native` | `native` | `bgt` / `cgt` / `bgt.un` / `cgt.un` |
| `>=` | `native` | `native` | `native` | `bge` / `bge.un` |
| `&` | `native` | `native` | `native` | `and` |
| <code>&#124;</code> | `native` | `native` | `native` | `or` |
| `^` | `native` | `native` | `native` | `xor` |
| `<<` | `native` | `int` | `native` | `shl` |
| `>>` | `native` | `int` | `native` | `shr` / `shr.un` |

For some binary operators, the IL operators support additional operand types
(see [ECMA-335](https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf) III.1.5 Operand type table).
But the set of operand types supported by C# is limited for simplicity and for consistency with existing operators in the language.

Lifted versions of the operators, where the arguments and return types are `nint?` and `nuint?`, are supported.

Compound assignment operations `x op= y` where `x` or `y` are native ints follow the same rules as with other primitive types with pre-defined operators.
Specifically the expression is bound as `x = (T)(x op y)` where `T` is the type of `x` and where `x` is only evaluated once.

The shift operators should mask the number of bits to shift appropriately
(see [shift operators](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#shift-operators) in C# spec).

### dynamic

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

Interfaces implemented by `System.IntPtr` and `System.UIntPtr` _are implicitly included_ in `nint` and `nuint`.
```C#
nint n = 42;
IEquatable<nint> i = n; // ok, IntPtr implements IEquatable<IntPtr>
```

### Overriding, hiding, and implementing

The `native int` aliases and the underlying types are considered equivalent for overriding, hiding and implementing.

Overloads cannot differ by `native int` and underlying type alone.
Overrides and implementations may differ by `native int` and underlying type alone.
Methods hide other methods that differ by `native int` and underlying type alone.

### Miscellaneous

`native int` types can be used as array indices without conversion.
```C#
static object GetItem(object[] array, nint index)
{
    return array[index]; // ok
}
```

`native int` types should be allowed as `enum` underlying types.
_This could be handled separately from the rest of the proposal._
```C#
enum E : nint // ok
{
}
```

`default(nint)` and `new nint()` are equivalent to `(nint)0`.

`typeof(nint)` is `typeof(IntPtr)`.

`sizeof(nint)` is supported but requires compiling in an unsafe context (as does `sizeof(IntPtr)`).
The value is not a compile-time constant.
`sizeof(nint)` is implemented as `sizeof(IntPtr)` rather than `IntPtr.Size`.

Compiler diagnostics for type references involving `nint` or `nuint` report `nint` or `nuint` rather than `IntPtr` or `UIntPtr`.

### Metadata

`nint` and `nuint` are represented in metadata as `System.IntPtr` and `System.UIntPtr`.

Type references that include `nint` or `nuint` are emitted with a `System.Runtime.CompilerServices.NativeTypeAttribute` to indicate which parts of the type reference are native ints.

```C#
namespace System.Runtime.CompilerServices
{
    public sealed class NativeTypeAttribute : System.Attribute
    {
        public NativeTypeAttribute() { }
        public NativeTypeAttribute(byte[] flags) { }
    }
}
```

The optional attribute argument contains a bit for each primitive type in the type reference.
If there is a single primitive type, the parameter-less constructor can be used.

```C#
nuint A;                    // [NativeType] UIntPtr A
(Stream, nint) B;           // [NativeType] ValueType<Stream, IntPtr> B
Dictionary<object, nint> C; // [NativeType(new[] { 0, 1 })] Dictionary<object, IntPtr> C
```

_There are now four attributes that encode state for types within a type reference as an array of values: `DynamicAttribute`, `NullableAttribute`, `TupleElementNamesAttribute`, and `NativeTypeAttribute`. Each of these attributes uses a distinct mapping to generate the array elements._

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

https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-05-26.md
https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-06-13.md
https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-07-05.md#native-int-and-intptr-operators
https://github.com/dotnet/csharplang/blob/master/meetings/2019/LDM-2019-10-23.md
