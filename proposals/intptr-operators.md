# Operators should be exposed for `System.IntPtr` and `System.UIntPtr`

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

The CLR supports a set of operators for the `System.IntPtr` and `System.UIntPtr` types (`native int`). These operators can be seen in `III.1.5` of the Common Language Infrastructure specification (`ECMA-335`). However, these operators are not supported by C#.

Language support should be provided for the full set of operators supported by `System.IntPtr` and `System.UIntPtr`. These operators are: `Add`, `Divide`, `Multiply`, `Remainder`, `Subtract`, `Negate`, `Equals`, `Compare`, `And`, `Not`, `Or`, `XOr`, `ShiftLeft`, `ShiftRight`.

## Motivation
[motivation]: #motivation

Today, users can easily write C# applications targeting multiple platforms using various tools and frameworks, such as: `Xamarin`, `.NET Core`, `Mono`, etc...

When writing cross-platform code, it is often necessary to write interop code that interacts with a particular target platform in a specific manner. This could include writing graphics code, calling some System API, or interacting with an existing native library.

This interop code often has to deal with handles, unmanaged memory, or even just platform-specific sized integers.

The runtime provides support for this by defining a set of operators that can be used on the `native int` (`System.IntPtr`) and `native unsigned int` (`System.UIntPtr`) primtive types.

C# has never supported these operators and so users have to workaround the issue. This often increases code complexity and lowers code maintainability.

As such, the language should begin to support these operators to help advance the language to better support these requirements.

## Detailed design
[design]: #detailed-design

The full set of operators supported are defined in `III.1.5` of the Common Language Infrastructure specification (`ECMA-335`). The specification is available here: [https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf](https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf)

## Drawbacks
[drawbacks]: #drawbacks

The actual use of these operators may be small and limited to end-users who are writing lower level libraries or interop code. Most end-users would likely be consuming these lower level libraries themselves which would have the native sized integers, handles, and interop code abstracted away. As such, they would not have need of the operators themselves.

## Alternatives
[alternatives]: #alternatives

Have the framework implement the required operators by writing them directly in IL. Additionally, the runtime could provide intrinsic support for the operators defined by the framework, so as to better optimize the end performance.

## Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still TBD?

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.


