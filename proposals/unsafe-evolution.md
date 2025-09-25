# Unsafe Evolution

Champion issue: https://github.com/dotnet/csharplang/issues/9704

## Summary

We update the definition of `unsafe` in C# from refering to locations where pointer types are used, to be locations where memory unmanaged by the runtime is dereferenced. These locations
are where memory unsafety occurs, and are responsible for the bulk of CVEs categorized as memory safety issues.

```cs
void M()
{
    int i = 1;
    int* ptr = &i; // Not unsafe
    unsafe
    {
        Console.WriteLine(*ptr); // Dereference of memory not managed by the runtime. This is unsafe.
        ref int intRef = Unsafe.AsRef(ptr); // Conversion of memory not managed by the runtime to a `ref`. This is unsafe.
    }
}
```

## Motivation

Background for this feature can also be found in https://github.com/dotnet/designs/pull/330, which tracks the broader ecosystem changes that will be needed as part of this proposal. For C#
specifically, we want to make sure that memory unsafety is properly tracked by the language; today, it can be difficult to look at a program hollistically and understand all locations where
memory unsafety occurs. This is because various helpers such as the `System.Runtime.CompilerServices.Unsafe`, `System.Runtime.InteropServices.Marshal`, and others do not express that they
violate memory safety and need special consideration. Methods that then use these helpers aren't immediately obvious, and when auditing code for memory safety issues (either ahead of time
when doing review, or when trying to determine the cause of a vulnerability that is being reported) it can be difficult to pinpoint the locations that could be contributing to issues.

Historically, `unsafe` in C# has referred to a specific memory-safety hole: the existence of pointer types. The moment that a pointer type is no longer involved, C# is perfectly happy to let
memory unsafety lie latent in code. It is this issue that we are looking to address with this evolution of `unsafe` in C# and the .NET ecosystem, labeling areas where memory unsafety could
potentially being occuring, making it easier for reviewers and auditors to understand the boundaries of potential memory unsafety in a program. Importantly, this means that we will be _changing_
the meaning of `unsafe`, not just augmenting it. The existence of a pointer is not itself unsafe; the unsafe action is dereferencing the pointer. This extends further to types themselves;
types cannot be inherently unsafe. It is only the action of using a type that could be unsafe, not the existence of that type.

In order for this information to flow through the system, we therefore need to have a way to mark methods themselves as `unsafe`. Today, `unsafe` as a method modifier has no external impact,
it only allows pointers to be used in the body of the member. Going forward, `unsafe` as a modifier will actually publicly change the meaning of the member; it will indicate that the member
has memory safety concerns and any usages must be manually validated by the programmer using the member.

This is a potentially large breaking change for particular segments of the C# user base. Our hope is that, for many of our users, this is effectively transparent, and updating to the new rules
will be seemless. However, given that some large API surfaces like large parts of reflection may need to be marked `unsafe`, we do think it likely that there will need to be a decent on-ramp to
the new rules to avoid entirely bifurcating the ecosystem.

## Detailed Design

### Existing `unsafe` rules

The existing C# specification has a large section devoted to `unsafe`: [§24 Unsafe code][unsafe-code.md]. It is defined as conditionally normative, as it is not required for a valid C# compiler
to support the `unsafe` feature. Much of what is currently considered conditionally normative will no longer be so after this change, as most of the definition of pointers is no longer considered
unsafe in itself. [Pointer types][pointer-types-spec], [Fixed and moveable variables][fixed-and-moveable-variables], all [pointer expressions][pointer-expressions] (except for
[pointer indirection][pointer-indirection], [pointer member access][pointer-member-access], and [pointer element access][pointer-element-access]), and [the `fixed` statement][fixed-statement]
are all no longer considered `unsafe`, and exist in normal C# with no requirement to be used in an `unsafe` context. Similarly, declaring a [fixed size buffer][fixed-size-buffer-declarations] or
an initialized [`stackalloc`][stack-allocation-spec] are also perfectly legal in safe C#. For all of these cases, it is only _accessing_ the memory that is unsafe.

Given the extensive rewrite of both the `unsafe` code section and other parts C# specification inherent in this change, it would be unwieldy and likely not useful to provide a line-by-line diff
of the existing rules of the specification. Instead, we will provide an overview of the change to make in a given section, as well as specific new rules for what is allowed in `unsafe` contexts.

#### Expression safety state

> [!NOTE]
> Now accepting naming suggestions

We introduce a new state that is tracked for all expressions in C#: the safety state. There are two possible safety states for any expression: safe, or unsafe. Expressions with a safety state of
safe may be used anywhere they are normally legal in C#. Expressions with a safety state of unsafe can only be used in an [unsafe context][unsafe-context-spec], and any use outside of an unsafe context
is an error.

For every expression production in [§12](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md), if all of its nested expressions have a safety state of safe, then that
expression has a safety state of safe. If that expression has no nested expressions, it has a safety state of safe. The following expressions always have a safety state of unsafe:

* [Pointer indirections][pointer-indirection]
* [Pointer member access][pointer-member-access]
* [Pointer element access][pointer-element-access]
* Element access on a fixed-size buffer
* `stackalloc` under the conditions defined [below](#stack-allocation)

In addition to these expressions, expressions can also conditionally introduce a safety state of unsafe if they depend on any symbol that is marked as `unsafe`. For example, calling a method that is
marked as `unsafe` will cause the _invocation_expression_ to have a safety state of unsafe, even if the receiver and all arguments have a safety state of safe.

> [!NOTE]
> This section probably needs expansion to formally declare the various expression types and symbols that can change the safety state of an expression.

#### Pointer types

As mentioned, pointers become no longer inherently unsafe. Any references to unsafe contexts in [§24.3][pointer-types-spec] are deleted. Pointer types exist in normal C# and do not require `unsafe`
to bring them into existence. The type definitions should be worked into [§8.1](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/types.md#81-general) and its following sections, as
other types.

Similarly, [pointer conversions][pointer-conversions] should be worked into [§10](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#10-conversions), with references to
`unsafe` contexts removed.

Similarly, [pointer expressions][pointer-expressions], except for [pointer indirection][pointer-indirection], [pointer member access][pointer-member-access], and
[pointer element access][pointer-element-access], should be worked into [§12](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md), with references to `unsafe` contexts
removed. No semantics change about the meaning of these expressions; the only change is that they no longer require an `unsafe` context to use.

For [pointer indirection][pointer-indirection], [pointer member access][pointer-member-access], and [pointer element access][pointer-element-access], these operators remain unsafe, as these
access memory that is not managed the runtime. They remain in [§24][unsafe-code.md], and continue to require an `unsafe` context to be used. Any use outside of an `unsafe` context is an error.
No semantics about these operators change; they still continue to mean exactly the same thing that they do today.

The [fixed statement][fixed-statement] moves to [§13](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md), with references to `unsafe` contexts removed.

#### Fixed-size buffers

The story for [fixed-size buffers][fixed-size-buffer-declarations] is similar to [pointers](#pointer-types). The definition of a fixed-size buffer is not itself dangerous, and moves to
[§16.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/structs.md#163-struct-members). Accessing a fixed-size buffer in an expression is similarly safe, unless the expression occurs
as the _primary_expression_ of an `element_access`; these are evaluated as a _pointer_element_access_, which is unsafe, as per the rules above.

#### Stack allocation

Again, the story for [stack allocation][stack-allocation-spec] is very similar to [pointers](#pointer-types). Converting a `stackalloc` to a pointer is no longer unsafe; it is the deference of that
pointer that is unsafe. We do add one new rule, however:

A _stackalloc_expression_ has a [safety state](#expression-safety-state) of unsafe if all of the following statements are true:

* The _stackalloc_expression_ is being converted to a `Span<T>` or a `ReadOnlySpan<T>`.
* The _stackalloc_expression_ does not have a _stackalloc_initializer_.
* The _stackalloc_expression_ is used within a member that has `SkipLocalsInitAttribute` applied.

In these contexts, the resulting stack space could have unknown memory contents, and it is being converted to a type that provides a safe wrapper around unmanaged memory access. This violates the
contract of `Span<T>` and `ReadOnlySpan<T>`, and so must be subject to extra scrutiny by the author and reviewers of such code.

> [!NOTE]
> This means that assigning a `stackalloc` to a pointer is _always_ safe, regardless of context. Arguably, this is a bit of failure of the proposal, as the creation of the pointer could be far from
> the actual dereference of the pointer.

[unsafe-code.md]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#128-primary-expressions
[unsafe-context-spec]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#242-unsafe-contexts
[pointer-types-spec]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#243-pointer-types
[fixed-and-moveable-variables]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#244-fixed-and-moveable-variables
[pointer-conversions]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#245-pointer-conversions
[pointer-expressions]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#246-pointers-in-expressions
[the-addressof-operator]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2465-the-address-of-operator
[pointer-indirection]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2462-pointer-indirection
[pointer-member-access]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2463-pointer-member-access
[pointer-element-access]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2464-pointer-element-access
[fixed-statement]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#247-the-fixed-statement
[fixed-size-buffer-declarations]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2482-fixed-size-buffer-declarations
[stack-allocation-spec]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#249-stack-allocation
