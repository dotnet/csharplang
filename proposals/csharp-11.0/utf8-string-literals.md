Utf8 Strings Literals
===

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
This proposal adds the ability to write UTF8 string literals in C# and have them automatically encoded into their UTF-8 `byte` representation.

## Motivation
UTF8 is the language of the web and its use is necessary in significant portions of the .NET stack. While much of data comes in the form of `byte[]` off the network stack there is still significant uses of constants in the code. For example networking stack has to commonly write constants like `"HTTP/1.0\r\n"`, `" AUTH"` or . `"Content-Length: "`. 

Today there is no efficient syntax for doing this as C# represents all strings using UTF16 encoding. That means developers have to choose between the convenience of encoding at runtime which incurs overhead, including the time spent at startup actually performing the encoding operation (and allocations if targeting a type that doesn't actually require them), or manually translating the bytes and storing in a `byte[]`. 

```c# 
// Efficient but verbose and error prone
static ReadOnlySpan<byte> AuthWithTrailingSpace => new byte[] { 0x41, 0x55, 0x54, 0x48, 0x20 };
WriteBytes(AuthWithTrailingSpace);

// Incurs allocation and startup costs performing an encoding that could have been done at compile-time
static readonly byte[] s_authWithTrailingSpace = Encoding.UTF8.GetBytes("AUTH ");
WriteBytes(s_authWithTrailingSpace);

// Simplest / most convenient but terribly inefficient
WriteBytes(Encoding.UTF8.GetBytes("AUTH "));
```

This trade off is a pain point that comes up frequently for our partners in the runtime, ASP.NET and Azure. Often times it causes them to leave performance on the table because they don't want to go through the hassle of writing out the `byte[]` encoding by hand.

To fix this we will allow for UTF8 literals in the language and encode them into the UTF8 `byte[]` at compile time.

## Detailed design

### `u8` suffix on string literals

The language will provide the `u8` suffix on string literals to force the type to be UTF8.
The suffix is case-insensitive, `U8` suffix will be supported and will have the same meaning as `u8` suffix.

When the `u8` suffix is used, the value of the literal is a ```ReadOnlySpan<byte>``` containing a UTF-8 byte representation of the string.
A null terminator is placed beyond the last byte in memory (and outside the length of the ```ReadOnlySpan<byte>```) in order to handle some
interop scenarios where the call expects null terminated strings.

```c#
string s1 = "hello"u8;             // Error
var s2 = "hello"u8;                // Okay and type is ReadOnlySpan<byte>
ReadOnlySpan<byte> s3 = "hello"u8; // Okay.
byte[] s4 = "hello"u8;             // Error - Cannot implicitly convert type 'System.ReadOnlySpan<byte>' to 'byte[]'.
byte[] s5 = "hello"u8.ToArray();   // Okay.
Span<byte> s6 = "hello"u8;         // Error - Cannot implicitly convert type 'System.ReadOnlySpan<byte>' to 'System.Span<byte>'.
```

Since the literals would be allocated as global constants, the lifetime of the resulting `ReadOnlySpan<byte>` would not prevent it from being returned or passed around to elsewhere. However, certain contexts, most notably within async functions, do not allow locals of ref struct types, so there would be a usage penalty in those situations, with a `ToArray()` call or similar being required.

A `u8` literal doesn't have a constant value. That is because ```ReadOnlySpan<byte>``` cannot be type of a constant today. If the definition of `const` is expanded
in the future to consider ```ReadOnlySpan<byte>```, then this value should also be considered a constant. Practically though this means a `u8`
literal cannot be used as the default value of an optional parameter.

```c#
// Error: The argument is not constant
void Write(ReadOnlySpan<byte> message = "missing"u8) { ... } 
```

When the input text for the literal is a malformed UTF16 string, then the language will emit an error:

```c#
var bytes = "hello \uD801\uD802"u8; // Error: the input string is not valid UTF16
```

### Addition operator

A new bullet point will be added to [§11.9.5 Addition operator](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1195-addition-operator) as follows.

- UTF8 byte representation concatenation:

  ```csharp
  ReadOnlySpan<byte> operator +(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y);
  ```

  This binary `+` operator performs byte sequences concatenation and is applicable if and only if both operands are semantically UTF8 byte representations.
  An operand is semantically a UTF8 byte representation when it is eiher a value of a `u8` literal, or a value produced by the UTF8 byte representation concatenation operator. 

  The result of the UTF8 byte representation concatenation is a ```ReadOnlySpan<byte>``` that consists of the bytes of the left operand followed by the bytes of the right operand. A null terminator is placed beyond the last byte in memory (and outside the length of the ```ReadOnlySpan<byte>```) in order to handle some
interop scenarios where the call expects null terminated strings.

### Lowering

The language will lower the UTF8 encoded strings exactly as if the developer had typed the resulting `byte[]` literal in code. For example:

```c#
ReadOnlySpan<byte> span = "hello"u8;

// Equivalent to

ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x00 }).
                               Slice(0,5); // The `Slice` call will be optimized away by the compiler.
```

That means all optimizations that apply to the `new byte[] { ... }` form will apply to utf8 literals as well. This means the call site will be allocation free as C# will optimize this be stored in the `.data` section of the PE file.

Multiple consecutive applications of UTF8 byte representation concatenation operators are collapsed into a single creation of `ReadOnlySpan<byte>` with byte array containing the final byte sequence.

```c#
ReadOnlySpan<byte> span = "h"u8 + "el"u8 + "lo"u8;

// Equivalent to

ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x00 }).
                               Slice(0,5); // The `Slice` call will be optimized away by the compiler.
```

## Drawbacks
### Relying on core APIs
The compiler implementation will use `UTF8Encoding` for both invalid string detection as well as translation to `byte[]`. The exact APIs will possibly depend on which target framework the compiler is using. But `UTF8Encoding` will be the workhorse of the implementation.

Historically the compiler has avoided using runtime APIs for literal processing. That is because it takes control of how constants are processed away from the language and into the runtime. Concretely it means items like bug fixes can change constant encoding and mean that the outcome of C# compilation depends on which runtime the compiler is executing on. 

This is not a hypothetical problem. Early versions of Roslyn used `double.Parse` to handle floating point constant parsing. That caused a number of problems. First it meant that some floating point values had different representations between the native compiler and Roslyn. Second as .NET core envolved and fixed long standing bugs in the `double.Parse` code it meant that the meaning of those constants changed in the language depending on what runtime the compiler executed on. As a result the compiler ended up writing it's own version of floating point parsing code and removing the dependency on `double.Parse`. 

This scenario was discussed with the runtime team and we do not feel it has the same problems we've hit before. The UTF8 parsing is stable across runtimes and there are no known issues in this area that are areas for future compat concerns. If one does come up we can re-evaluate the strategy. 

## Alternatives
### Target type only
The design could rely on target typing only and remove the `u8` suffix on `string` literals. In the majority of cases today the `string` literal is being assigned directly to a `ReadOnlySpan<byte>` hence it's unnecessary. 

```c#
ReadOnlySpan<byte> span = "Hello World;" 
```

The `u8` suffix exists primarily to support two scenarios: `var` and overload resolution. For the latter consider the following use case: 

```c# 
void Write(ReadOnlySpan<byte> span) { ... } 
void Write(string s) {
    var bytes = Encoding.Utf8.GetBytes(s);
    Write(bytes.AsSpan());
}
```

Given the implementation it is better to call `Write(ReadOnlySpan<byte>)` and the `u8` suffix makes this convenient: `Write("hello"u8)`. Lacking that developers need to resort to awkward casting `Write((ReadOnlySpan<byte>)"hello")`. 

Still this is a convenience item, the feature can exist without it and it is non-breaking to add it at a later time. 

### Wait for Utf8String type
While the .NET ecosystem is standardizing on `ReadOnlySpan<byte>` as the defacto Utf8 string type today it's possible the runtime will introduce an actual `Utf8String` type is the future.

We should evaluate our design here in the face of this possible change and reflect on whether we'd regret the decisions we've made. This should be weighed though against the realistic probability we'll introduce `Utf8String`, a probability which seems to decrease every day we find `ReadOnlySpan<byte>` as an acceptable alternative.

It seems unlikely that we would regret the target type conversion between string literals and `ReadOnlySpan<byte>`. The use of `ReadOnlySpan<byte>` as utf8 is embedded in our APIs now and hence there is still value in the conversion even if `Utf8String` comes along and is a "better" type. The language could simply prefer conversions to `Utf8String` over `ReadOnlySpan<byte>`.

It seems more likely that we'd regret the `u8` suffix pointing to `ReadOnlySpan<byte>` instead of `Utf8String`. It would be similar to how we regret that `stackalloc int[]` has a natural type of `int*` instead of `Span<int>`. This is not a deal breaker though, just an inconvenience.

### Conversions between `string` constants and `byte` sequences

The language will allow conversions between `string` constants and `byte` sequences where the text is converted into the equivalent UTF8 byte representation. Specifically the compiler will allow _string_constant_to_UTF8_byte_representation_conversion_ - implicit conversions from `string` constants to `byte[]`, `Span<byte>`, and `ReadOnlySpan<byte>`.
A new bullet point will be added to the implicit conversions [§10.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#102-implicit-conversions) section. This conversion is not a standard conversion [§10.4](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#104-standard-conversions).

```c# 
byte[] array = "hello";             // new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f }
Span<byte> span = "dog";            // new byte[] { 0x64, 0x6f, 0x67 }
ReadOnlySpan<byte> span = "cat";    // new byte[] { 0x63, 0x61, 0x74 }
```

When the input text for the conversion is a malformed UTF16 string then the language will emit an error:

```c#
const string text = "hello \uD801\uD802";
byte[] bytes = text; // Error: the input string is not valid UTF16
```

The predominant usage of this feature is expected to be with literals but it will work with any `string` constant value.
A conversion from a `string` constant with `null` value will be supprted as well. The result of the conversion will be `default`
value of the target type.

```c#
const string data = "dog"
ReadOnlySpan<byte> span = data;     // new byte[] { 0x64, 0x6f, 0x67 }
```

In the case of any constant operation on strings, such as `+`, the encoding to UTF8 will occur on the final `string` vs. happening for the individual parts and then concatenating the results. This ordering is important to consider because it can impact whether or not the conversion succeeds. 

```c#
const string first = "\uD83D";  // high surrogate
const string second = "\uDE00"; // low surrogate
ReadOnlySpan<byte> span = first + second;
```

The two parts here are invalid on their own as they are incomplete portions of a surrogate pair. Individually there is no correct translation to UTF8 but together they form a complete surrogate pair that can be successfully translated to UTF8.

The _string_constant_to_UTF8_byte_representation_conversion_ is not allowed in Linq Expression Trees.

While the inputs to these conversions are constants and the data is fully encoded at compile time, the conversion is **not** considered constant by the language. That is because arrays are not constant today. If the definition of `const` is expanded in the future to consider arrays then these conversions should also be considered. Practically though this means a result of these conversions cannot be used as the default value of an optional parameter. 

```c#
// Error: The argument is not constant
void Write(ReadOnlySpan<byte> message = "missing") { ... } 
```

Once implemented string literals will have the same problem that other literals have in the language: what type they represent depends on how they are used. C# provides a literal suffix to disambiguate the meaning for other literals. For example developers can write `3.14f` to force the value to be a `float` or `1l` to force the value to be a `long`.

## Unresolved questions

### (Resolved) Conversions between a `string` constant with `null` value and `byte` sequences

Whether this conversion is supported and, if so, how it is performed is not specified.

*Proposal:* 

Allow implicit conversions from a `string` constant with `null` value to `byte[]`, `Span<byte>`, and `ReadOnlySpan<byte>`. The result of the conversion is `default` value of the target type.

*Resolution:*

The proposal is approved - https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md#conversions-from-null-literals.

### (Resolved) Where does _string_constant_to_UTF8_byte_representation_conversion_ belong?

Is _string_constant_to_UTF8_byte_representation_conversion_ a bullet point in the implicit conversions [§10.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#102-implicit-conversions) section on its own, or is it part of [§10.2.11](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#10211-implicit-constant-expression-conversions), or does it belong to some other existing implicit conversions group?

*Proposal:* 

It is a new bullet point in implicit conversions [§10.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#102-implicit-conversions), similar to "Implicit interpolated string conversions" or "Method group conversions". It doesn't feel like it belongs to "Implicit constant expression conversions" because, even though the source is a constant expression, the result is never a constant expression. Also, "Implicit constant expression conversions" are considered to be "Standard implicit conversions" [§10.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#1042-standard-implicit-conversions), which is likely to lead to non-trivial behavior changes involving user-defined conversions.

*Resolution:*

We will introduce a new conversion kind for string constant to UTF-8 bytes - https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md#conversion-kinds

### (Resolved) Is _string_constant_to_UTF8_byte_representation_conversion_ a standard conversion

In addition to "pure" Standard Conversions (the standard conversions are those pre-defined conversions that can occur as part of a user-defined conversion), compiler also treats some predefined conversions as "somewhat" standard. For example, an implicit interpolated string conversion can occur as part of a user-defined conversion if there is an explicit cast to the target type in code. As if it is a Standard Explicit Conversion, even though it is an implicit conversion not explicitly included into the set of standard implicit or explicit conversions. For example:

``` C#
class C
{
    static void Main()
    {
        C1 x = $"hello"; // error CS0266: Cannot implicitly convert type 'string' to 'C1'. An explicit conversion exists (are you missing a cast?)
        var y = (C1)$"dog"; // works
    }
}

class C1
{
    public static implicit operator C1(System.FormattableString x) => new C1();
}
```

*Proposal:* 

The new conversion is not a standard conversion. This will avoid non-trivial behavior changes involving user-defined conversions. For example, we won't need to worry about user-defined cinversions under implicit tuple literal conversions, etc.

*Resolution:*

Not a standard conversion, for now - https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md#implicit-standard-conversion.

### (Resolved) Linq Expression Tree conversion

Should _string_constant_to_UTF8_byte_representation_conversion_ be allowed in context of a Linq Expression Tree conversion?
We can disallow it for now, or we could simply include the "lowered" form into the tree. For example:
``` C#
Expression<Func<byte[]>> x = () => "hello";           // () => new [] {104, 101, 108, 108, 111}
Expression<FuncSpanOfByte> y = () => "dog";           // () => new Span`1(new [] {100, 111, 103}) 
Expression<FuncReadOnlySpanOfByte> z = () => "cat";   // () => new ReadOnlySpan`1(new [] {99, 97, 116})
```

What about string literals with `u8` suffix? We could surface those as byte array creations:
``` C#
Expression<Func<byte[]>> x = () => "hello"u8;           // () => new [] {104, 101, 108, 108, 111}
```

*Resolution:*

Disallow in Linq Expression Trees - https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md#expression-tree-representation.

### (Resolved) The natural type of a string literal with `u8` suffix

The "Detailed design" section says: "The natural type though will be `ReadOnlySpan<byte>`." At the same time: "When the `u8` suffix is used the literal can still be converted to any of the allowed types: `byte[]`, `Span<byte>` or `ReadOnlySpan<byte>`." 

There are several disadvantages with this approach:
- `ReadOnlySpan<byte>` is not available on desktop framework;
- There are no existing conversions from `ReadOnlySpan<byte>` to `byte[]` or `Span<byte>`. In order to support them we will likely need to treat the literals as target typed. Both the language rules and implementation will become more complicated.  

*Proposal:* 

The natural type will be `byte[]`. It is readily available on all frameworks. BTW, at runtime we will always be starting with creating a byte array, even with the original proposal. We also don't need any special conversion rules to support conversions to `Span<byte>` and `ReadOnlySpan<byte>`. There are already implicit user-defined conversions from `byte[]` to `Span<byte>` and `ReadOnlySpan<byte>`. There is even implicit user-defined conversion to `ReadOnlyMemory<byte>` (see the "Depth of the conversion" question below). There is a disadvantage, language doesn't allow chaining user-defined conversions. So, the following code will not compile:
```C#
using System;
class C
{
    static void Main()
    {
        var y = (C2)"dog"u8; // error CS0030: Cannot convert type 'byte[]' to 'C2'
        var z = (C3)"cat"u8; // error CS0030: Cannot convert type 'byte[]' to 'C3'
    }
}

class C2
{
    public static implicit operator C2(Span<byte> x) => new C2();
}

class C3
{
    public static explicit operator C3(ReadOnlySpan<byte> x) => new C3();
}
```
However, as with any user-defined conversion, an explicit cast can be used to make one user-defined conversion a part of another user-defined conversion.

It feels like all motivating scenarios are going to be addressed with `byte[]` as the natural type, but the language rules and implementation will be significantly simpler.

*Resolution:*

The proposal is approved - https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md#natural-type-of-u8-literals.
We will likely want to have a deeper debate about whether `u8` string literals should have a type of a mutable array, but we don't
think that debate is necessary for now.

### (Resolved) Depth of the conversion
Will it also work anywhere that a byte[] could work? Consider: 

```c# 
static readonly ReadOnlyMemory<byte> s_data1 = "Data"u8;
static readonly ReadOnlyMemory<byte> s_data2 = "Data";
```

The first example likely should work because of the natural type that comes from `u8`.

The second example is hard to make work because it requires conversions in both directions. That is unless we add `ReadOnlyMemory<byte>` as one of the allowed conversion types. 

*Proposal:* 

Don't do anything special.

*Resolution:*

No new conversion targets added for now https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md#conversion-depth.

### (Resolved) Overload resolution breaks

The following API would become ambiguous:

```c#
M("");
static void M1(ReadOnlySpan<char> charArray) => ...;
static void M1(byte[] byteArray) => ...;
```

What should we do to address this?

*Proposal:* 

Similar to https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/lambda-improvements.md#overload-resolution, Better function member ([§11.6.4.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11643-better-function-member)) is updated to prefer members where none of the conversions involved require converting `string` constants to UTF8 `byte` sequences.

> #### Better function member
> ...
> Given an argument list `A` with a set of argument expressions `{E1, E2, ..., En}` and two applicable function members `Mp` and `Mq` with parameter types `{P1, P2, ..., Pn}` and `{Q1, Q2, ..., Qn}`, `Mp` is defined to be a ***better function member*** than `Mq` if
>
> 1. **for each argument, the implicit conversion from `Ex` to `Px` is not a _string_constant_to_UTF8_byte_representation_conversion_, and for at least one argument, the implicit conversion from `Ex` to `Qx` is a _string_constant_to_UTF8_byte_representation_conversion_, or**
> 2. for each argument, the implicit conversion from `Ex` to `Px` is not a _function_type_conversion_, and
>    *  `Mp` is a non-generic method or `Mp` is a generic method with type parameters `{X1, X2, ..., Xp}` and for each type parameter `Xi` the type argument is inferred from an expression or from a type other than a _function_type_, and
>    *  for at least one argument, the implicit conversion from `Ex` to `Qx` is a _function_type_conversion_, or `Mq` is a generic method with type parameters `{Y1, Y2, ..., Yq}` and for at least one type parameter `Yi` the type argument is inferred from a _function_type_, or
> 3. for each argument, the implicit conversion from `Ex` to `Qx` is not better than the implicit conversion from `Ex` to `Px`, and for at least one argument, the conversion from `Ex` to `Px` is better than the conversion from `Ex` to `Qx`.

Note that the addition of this rule is not going to cover scenarios with instance methods becoming applicable and "shadowing" extension methods. For example:
``` C#
using System;

class Program
{
    static void Main()
    {
        var p = new Program();
        Console.WriteLine(p.M(""));
    }

    public string M(byte[] b) => "byte[]";
}

static class E
{
    public static string M(this object o, string s) => "string";
}
```
Behavior of this code will silently change from printing "string" to printing "byte[]".

Are we Ok with this behavior change? Should it be documented as a breaking change?

Note that there is no proposal to make _string_constant_to_UTF8_byte_representation_conversion_ unavailable when C#10 language version is targeted. In that case, the example above becomes an error rather than returns to C#10 behavior. This follows a general principle that target language version doesn't affect semantics of the language.

Are we Ok with this behavior? Should it be documented as a breaking change?

The new rule also is not going to prevent breaks involving tuple litearal conversions. For example,
``` C#
class C
{
    static void Main()
    {
        System.Console.Write(Test(("s", 1)));
    }

    static string Test((object, int) a) => "object";
    static string Test((byte[], int) a) => "array";
}
```
is going to silently print "array" instead of "object". 

Are we Ok with this behavior? Should it be documented as a breaking change? Perhaps we could complicate the new rule to dig into the tuple literal conversions.

*Resolution:*

The prototype will not adjust any rules here, so we can hopefully see what breaks in practice - https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md#breaking-changes.

### (Resolved) Should `u8` suffix be case-insensitive?

*Proposal:* 

Support `U8` suffix as well for consistency with numeric suffixes.

*Resolution:*

Approved - https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md#suffix-case-sensitivity.

## Examples today
Examples of where runtime has manually encoded the UTF8 bytes today

- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/Common/src/System/Net/Http/aspnetcore/Http2/Hpack/StatusCodes.cs#L13-L78
- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/System.Memory/src/System/Buffers/Text/Base64Encoder.cs#L581-L591
- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/System.Net.HttpListener/src/System/Net/Windows/HttpResponseStream.Windows.cs#L284
- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/Http2Stream.cs#L30
- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/Http3RequestStream.cs#L852
- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/System.Text.Json/src/System/Text/Json/JsonConstants.cs#L35-L42
 
Examples where we leave perf on the table
- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/System.Net.Security/src/System/Net/Security/Pal.Managed/SafeChannelBindingHandle.cs#L16-L17
- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/HttpConnection.cs#L37-L43
- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/Http2Connection.cs#L78
- https://github.com/dotnet/runtime/blob/e095fde94baa480a6d65dfdee43d9cc0ad0d0b38/src/libraries/System.Net.Mail/src/System/Net/Mail/SmtpCommands.cs#L669-L687

## Design meetings

https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-26.md
https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-04-18.md
https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-06-06.md
