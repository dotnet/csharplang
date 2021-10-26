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

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
