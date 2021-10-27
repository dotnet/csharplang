Utf8 Strings Literals
===

## Summary
This proposal adds the ability to write UTF8 string literals in C# and have them automatically encoded into their `byte[]` representation.

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
The language will allow conversions between `string` constants and `byte` sequences where the text is converted into the equivalent UTF8 byte representation. Specifically the compiler will allow for implicit conversions from `string` constants to `byte[]`, `Span<byte>`, and `ReadOnlySpan<byte>`. 

```c# 
byte[] array = "hello";             // new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x20 }
Span<byte> span = "dog";            // new byte[] { 0x64, 0x6f, 0x67 }
ReadOnlySpan<byte> span = "cat";    // new byte[] { 0x63, 0x61, 0x74 }
```

When the input text for the conversion is a malformed UTF16 string then the language will emit an error:

```c#
const string text = "hello \uD801\uD802";
byte[] bytes = text; // Error: the input string is not valid UTF16
```

The predominant usage of this feature is expected to be with literals but it will work with any `string` constant value.

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

Once implemented string literals will have the same problem that other literals have in the language: what type they represent depends on how they are used. C# provides a literal suffix to disambiguate the meaning for other literals. For example developers can write `3.14f` to force the value to be a `float` or `1l` to force the value to be a `long`. Similarly the language will provide the `u8` suffix on string literals to force the type to be UTF8.

When the `u8` suffix is used the literal can still be converted to any of the allowed types: `byte[]`, `Span<byte>` or `ReadOnlySpan<byte>`. The natural type though will be `ReadOnlySpan<byte>`.

```c#
string s1 = "hello"u8;      // Error
var s2 = "hello"u8;         // Okay and type is ReadOnlySpan<byte>
Span<byte> s3 = "hello"u8;  // Okay
byte[] s4 = "hello"u8;      // Okay
```

While the inputs to these conversions are constants and the data is fully encoded at compile time, the conversion is **not** considered constant by the language. That is because arrays are not constant today. If the definition of `const` is expanded in the future to consider arrays then this conversion should also be considered. Practically though this means a UTF8 literal cannot be used as the default value of an optional parameter. 

```c#
// Error: The argument is not constant
void Write(ReadOnlySpan<byte> message = "missing") { ... } 
```

The language will lower the UTF8 encoded strings exactly as if the developer had typed the resulting `byte[]` literal in code. For example:

```c#
ReadOnlySpan<byte> span = "hello";

// Equivalent to

ReadOnlySpan<byte> span = new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x20 };
```

That means all optimizations that apply to the `new byte[] { ... }` form will apply to utf8 literals as well. This means the call site will be allocation free as C# will optimize this be stored in the `.data` section of the PE file.

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
ReadOnlySpan<byte> span = "Hello World; 
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

## Unresolved questions
### Depth of the conversion
Will it also work anywhere that a byte[] could work? Consider: 

```c# 
static readonly ReadOnlyMemory<byte> s_data1 = "Data"u8;
static readonly ReadOnlyMemory<byte> s_data2 = "Data";
```

The first example likely should work because of the natural type that comes from `u8`.

The second example is hard to make work because it requires conversions in both directions. That is unless we add `ReadOnlyMemory<byte>` as one of the allowed conversion types. 

### Overload resolution breaks

The following API would become ambiguous:

```c#
M("");
static void M1(char[] charArray) => ...;
static void M1(byte[] charArray) => ...;
```

What should we do to address this?

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

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
