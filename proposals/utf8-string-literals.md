Utf8 String Literals
===

## Summary
[summary]: #summary

<!-- One paragraph explanation of the feature. -->

## Motivation
[motivation]: #motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement, and include examples of how the feature is used. Please include syntax and desired semantics for the change, including linking to the relevant parts of the existing C# spec to describe the changes necessary to implement this feature. An initial proposal does not need to cover all cases, but it should have enough detail to enable a language team member to bring this proposal to design if they so choose. -->

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

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
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
