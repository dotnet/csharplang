# Unsafe Evolution

Champion issue: https://github.com/dotnet/csharplang/issues/9704

## Summary

We update the definition of `unsafe` in C# from referring to locations where pointer types are used, to be locations where memory unmanaged by the runtime is dereferenced. These locations
are where memory unsafety occurs, and are responsible for the bulk of CVEs (Common Vulnerabilities and Exposures) categorized as memory safety issues.

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

Background for this feature can also be found in https://github.com/dotnet/designs/blob/main/accepted/2025/memory-safety/caller-unsafe.md, which tracks the broader ecosystem changes that will be needed as part of this proposal.
These include BCL updates to properly annotate methods as being unsafe, as well as tooling updates for better understanding of where memory unsafety occurs. For C#
specifically, we want to make sure that memory unsafety is properly tracked by the language; today, it can be difficult to look at a program holistically and understand all locations where
memory unsafety occurs. This is because various helpers such as the `System.Runtime.CompilerServices.Unsafe`, `System.Runtime.InteropServices.Marshal`, and others do not express that they
violate memory safety and need special consideration. Methods that then use these helpers aren't immediately obvious, and when auditing code for memory safety issues (either ahead of time
when doing review, or when trying to determine the cause of a vulnerability that is being reported) it can be difficult to pinpoint the locations that could be contributing to issues.

Historically, `unsafe` in C# has referred to a specific memory-safety hole: the existence of pointer types. The moment that a pointer type is no longer involved, C# is perfectly happy to let
memory unsafety lie latent in code. It is this issue that we are looking to address with this evolution of `unsafe` in C# and the .NET ecosystem, labeling areas where memory unsafety could
potentially occur, making it easier for reviewers and auditors to understand the boundaries of potential memory unsafety in a program. Importantly, this means that we will be _changing_
the meaning of `unsafe`, not just augmenting it. The existence of a pointer is not itself unsafe; the unsafe action is dereferencing the pointer. This extends further to types themselves;
types cannot be inherently unsafe. It is only the action of using a type that could be unsafe, not the existence of that type.

In order for this information to flow through the system, we therefore need to have a way to mark methods themselves as unsafe. Applying an attribute (`RequiresUnsafe`) to a member will indicate that
the member has memory safety concerns and any usages must be manually validated by the programmer using the member (the error will go away if the member is used inside an `unsafe` context).
We are not going to use the `unsafe` modifier in signature to denote requires-unsafe members to avoid a breaking change
(it won't even be required to allow pointers in signature as pointers are now safe; it will merely introduce an `unsafe` context).

Nevertheless, this is still a breaking change for particular segments of the C# user base. Our hope is that, for many of our users, this is effectively transparent, and updating to the new rules
will be seamless. However, given that some large API surfaces like large parts of reflection may need to be marked `unsafe`, we do think it likely that there will need to be a decent on-ramp to
the new rules to avoid entirely bifurcating the ecosystem.

## Detailed Design

Terminology: we call members *requires-unsafe* (previously known as *caller-unsafe*) if
- under [the updated memory safety rules](#metadata) they [have the `RequiresUnsafe` attribute](#metadata) or [are `extern`](#extern),
- under [the legacy memory safety rules](#metadata) they [contain pointers in signature](#compat-mode).

### Existing `unsafe` rules

The existing C# specification has a large section devoted to `unsafe`: [§24 Unsafe code][unsafe-code]. It is defined as conditionally normative, as it is not required for a valid C# compiler
to support the `unsafe` feature. Much of what is currently considered conditionally normative will no longer be so after this change, as most of the definition of pointers is no longer considered
unsafe in itself. [Pointer types][pointer-types-spec], [Fixed and moveable variables][fixed-and-moveable-variables], all [pointer expressions][pointer-expressions] (except for
[pointer indirection][pointer-indirection], [pointer member access][pointer-member-access], and [pointer element access][pointer-element-access]), and [the `fixed` statement][fixed-statement]
are all no longer considered `unsafe`, and exist in normal C# with no requirement to be used in an `unsafe` context. Similarly, declaring a [fixed size buffer][fixed-size-buffer-declarations] or
an initialized [`stackalloc`][stack-allocation-spec] are also perfectly legal in safe C#. For all of these cases, it is only _accessing_ the memory that is unsafe.

Given the extensive rewrite of both the `unsafe` code section and other parts C# specification inherent in this change, it would be unwieldy and likely not useful to provide a line-by-line diff
of the existing rules of the specification. Instead, we will provide an overview of the change to make in a given section, as well as specific new rules for what is allowed in `unsafe` contexts.

#### Redefining expressions that require unsafe contexts

The following expressions require an `unsafe` context when used:

* [Pointer indirections][pointer-indirection]
* [Pointer member access][pointer-member-access]
* [Pointer element access][pointer-element-access]
* Function pointer invocation
* Element access on a fixed-size buffer
* `stackalloc` under the conditions defined [below](#stack-allocation)

In addition to these expressions, expressions and statements can also conditionally require an `unsafe` context if they depend on any symbol that is marked as `unsafe`. For example, calling a method
that is requires-unsafe will cause the _invocation_expression_ to require an `unsafe` context. Statements with invocations embedded (such as `using`s, `foreach`, and similar) can also require an
`unsafe` context when they use a requires-unsafe member.

When we say "requires an unsafe context" or similar in this document, it means emitting an error that the construct requires an `unsafe` context to be used.

> [!NOTE]
> This section probably needs expansion to formally declare what each expression and statement must consider to require an `unsafe` context.

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
access memory that is not managed the runtime. They remain in [§24][unsafe-code], and continue to require an `unsafe` context to be used. Any use outside of an `unsafe` context is an error.
No semantics about these operators change; they still continue to mean exactly the same thing that they do today. These expressions must always occur in an `unsafe` context.

The [fixed statement][fixed-statement] moves to [§13](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md), with references to `unsafe` contexts removed.

Function pointers are not yet incorporated into the main C# specification, but they are similarly affected; everything but function pointer invocation is moved into the standard specification.
A function pointer invocation expression must always occur in an `unsafe` context.

#### Fixed-size buffers

The story for [fixed-size buffers][fixed-size-buffer-declarations] is similar to [pointers](#pointer-types). The definition of a fixed-size buffer is not itself dangerous, and moves to
[§16.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/structs.md#163-struct-members). Accessing a fixed-size buffer in an expression is similarly safe, unless the expression occurs
as the _primary_expression_ of an `element_access`; these are evaluated as a _pointer_element_access_, which is unsafe, as per the rules above.

#### Stack allocation

Again, the story for [stack allocation][stack-allocation-spec] is very similar to [pointers](#pointer-types). Converting a `stackalloc` to a pointer is no longer unsafe; it is the deference of that
pointer that is unsafe. We do add one new rule, however:

A _stackalloc_expression_ is unsafe if all of the following statements are true:

* The _stackalloc_expression_ is being converted to a `Span<T>` or a `ReadOnlySpan<T>`.
* The _stackalloc_expression_ does not have a _stackalloc_initializer_.
* The _stackalloc_expression_ is used within a member that has `SkipLocalsInitAttribute` applied.

In these contexts, the resulting stack space could have unknown memory contents, and it is being converted to a type that provides a safe wrapper around unmanaged memory access. This violates the
contract of `Span<T>` and `ReadOnlySpan<T>`, and so must be subject to extra scrutiny by the author and reviewers of such code.

> [!NOTE]
> This means that assigning a `stackalloc` to a pointer is _always_ safe, regardless of context.

#### `sizeof`

For certain predefined types, `sizeof` has always been constant and safe ([§12.8.19][sizeof-const]) and that remains unchanged under the new rules.
For other types, `sizeof` used to require unsafe context ([§24.6.9][sizeof-unsafe]) but it is now safe under the new memory safety rules.

### Overriding, inheritance, and implementation

It is a memory safety error to add `RequiresUnsafe` at the member level in any override or implementation of a member that is not requires-unsafe originally, because callers may be using the base
definition and not see any addition of `RequiresUnsafe` by a derived implementation.

### Delegates and lambdas

It is a memory safety error to convert a requires-unsafe member to a delegate type that is not requires-unsafe. The [_function type_](csharp-10.0/lambda-improvements.md#natural-function-type) definition
is updated to include whether the _anonymous function_ has the `RequiresUnsafe` attribute, or the _method group_ is to a member that is requires-unsafe. If it is, a requires-unsafe anonymous function type is created, just as
it would be if any parameter were a by-`ref`, optional, or `params`.

It is a memory safety error to convert a delegate type that is requires-unsafe to `System.Delegate`/`System.Linq.Expressions.Expression`/`System.Linq.Expressions.Expression<T>`, or any interface those
types implement or base type of those types. They also cannot be used as type parameters.

A delegate type that is requires-unsafe can only be invoked in an `unsafe` context. A delegate type is requires-unsafe if and only if its `Invoke`, `BeginInvoke`, and `EndInvoke` methods are marked as `RequiresUnsafe`
(note that requires-unsafe is not enough, i.e., `extern` or pointer-containing invoke methods don't count).

> [!NOTE]
> We don't actually attribute the delegate type itself, just the `Invoke`, `BeginInvoke`, and `EndInvoke` methods. Determining whether a delegate type is requires-unsafe is done by examining those 3 methods.
> If all are marked as `RequiresUnsafe`, the delegate type is considered requires-unsafe. If only some are marked as `RequiresUnsafe`, then it is presumed that calling the others is safe and only calling the member that is
> marked as `RequiresUnsafe` will cause a memory safety error. It will be a memory safety error to convert a requires-unsafe lambda or method group to a delegate type that does not have all of `Invoke`, `BeginInvoke`,
> and `EndInvoke` marked as `RequiresUnsafe`.

### `extern`

Because `extern` methods are to native locations that cannot be guaranteed by the runtime, any `extern` method is automatically considered requires-unsafe
if compiled under the updated memory safety rules (i.e., it gets the `RequiresUnsafeAttribute`).
Even methods that only take `unmanaged` parameters by value cannot be safely called by C#,
as the calling convention used for the method could be incorrectly specified by the user and must be manually verified by review.

`extern` methods from assemblies using the legacy memory safety rules are not considered implicitly `unsafe` because
`extern` is considered implementation detail that is not part of public surface.
`extern` is not guaranteed to be preserved in reference assemblies.

Note that this is different from the [compat mode](#compat-mode) which applies to legacy-rules assemblies too
because methods with pointers in signature would always need an unsafe context at the call site.

### Unsafe modifiers and contexts

Today (and unchanged in this proposal), as covered by the [unsafe context specification][unsafe-context-spec], `unsafe` behaves in a lexical manner,
marking the entire textual body contained by the `unsafe` block as an `unsafe` context (except for iterator bodies),
and also some surrounding contexts in case of declarations:

```cs
class A : Attribute
{
    [RequiresUnsafe] public A() { }
}
class C
{
    [A] void M1() { } // error: cannot use `A..ctor` in safe context
    [A] unsafe void M1() { } // ok: the `unsafe` context applies to the `A..ctor` usage
}
```

Since pointer types are now safe, an `unsafe` modifier on declarations without bodies does not have a meaning anymore. Hence `unsafe` on the following declarations will produce a warning:
- `using static`,
- `using` alias.

`RequiresUnsafe` on a member is _not_ applied to any nested anonymous or local functions inside the member. To mark an anonymous or local function as requires-unsafe, it must manually be marked as `RequiresUnsafe`. The same goes for
anonymous and local functions declared inside of an `unsafe` block.

When a member is `partial`, both parts must agree on the `unsafe` modifier, but only one can specify the `RequiresUnsafe` attribute, unchanged from C# rules today.

For properties, `get` and `set/init` accessors can be independently declared as `RequiresUnsafe`; marking the entire property as `RequiresUnsafe` means that both the `get` and `set/init` accessors are requires-unsafe.
For events, `add` and `remove` accessors can be independently declared as `RequiresUnsafe`; marking the entire event as `RequiresUnsafe` means that both the `add` and `remove` accessors are requires-unsafe.

#### Metadata

When an assembly is compiled with the new memory safety rules, it gets marked with `MemorySafetyRulesAttribute` (detailed below), filled in with `15` as the language version. This is a signal to
any downstream consumers that any members defined in the assembly will be properly attributed with `RequiresUnsafeAttribute` (detailed below) if an `unsafe` context is required to call them.
Any member in such an assembly that is not marked with `RequiresUnsafeAttribute` does not require an `unsafe` context to be called, regardless of the types in the signature of the member.

The compiler ignores `RequiresUnsafeAttribute`-marked members from assemblies that are using the legacy memory safety rules (instead, the [compat mode](#compat-mode) is used there).

When a member under the new memory safety rules is `extern`, the compiler will synthesize a `RequiresUnsafeAttribute` application on the member in metadata. When a user-facing requires-unsafe member generates
hidden members, such as an auto-property's backing field or get/set methods, both the user-facing member and any hidden members generated by that user-facing member are all requires-unsafe,
and `RequiresUnsafeAttribute` is applied to all of them.

```cs
namespace System.Runtime.CompilerServices
{
    /// <summary>Indicates the language version of the memory safety rules used when the module was compiled.</summary>
    [AttributeUsage(AttributeTargets.Module, Inherited = false)]
    public sealed class MemorySafetyRulesAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="MemorySafetyRulesAttribute"/> class.</summary>
        /// <param name="version">The language version of the memory safety rules used when the module was compiled.</param>
        public MemorySafetyRulesAttribute(int version) => Version = version;
 
        /// <summary>Gets the language version of the memory safety rules used when the module was compiled.</summary>
        public int Version { get; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, AllowMultiple = false, Inherited = true)]
    public sealed class RequiresUnsafeAttribute : Attribute
    {
    }
}
```

#### Compat mode

For compat purposes, and to reduce the number of false negatives that occur when enabling the new rules, we have a fallback rule for modules that have not been updated to the new rules. For such modules,
a member is considered requires-unsafe if it contains a pointer or function pointer type somewhere among its parameter types or return type (can be nested in a non-pointer type, e.g., `int*[]`).
Note that this doesn't apply to pointers in constraint types (e.g., `where T : I<int*[]>`) as those wouldn't need unsafe context at the call sites previously either.

## Alternatives

### Use `unsafe` to denote requires-unsafe members

Instead of using `RequiresUnsafeAttribute` to denote requires-unsafe members, we could use the `unsafe` keyword on the member
(and only use the attribute for metadata representation of requires-unsafe members).
See [a previous version of this speclet](https://github.com/dotnet/csharplang/blob/61f06216967ed264a8f83c71bff482f3eb6ac113/proposals/unsafe-evolution.md)
before [the alternative](https://github.com/dotnet/csharplang/blob/61f06216967ed264a8f83c71bff482f3eb6ac113/meetings/working-groups/unsafe-evolution/unsafe-alternative-syntax.md) was incorporated into it.

Advantages of `unsafe`:
- similar to other languages and hence easier to understand,
- more discoverable than an attribute.

Advantages of an attribute (or another keyword):
- avoids breaking existing members marked as `unsafe`,
- incremental adoption possible (member-by-member),
- doesn't force marking the whole body as `unsafe` (even with `unsafe` keyword we could
  [change](https://github.com/dotnet/csharplang/blob/61f06216967ed264a8f83c71bff482f3eb6ac113/proposals/unsafe-evolution.md#unsafe-context-defaults-in-members)
  `unsafe` to not have an effect on bodies though).

## Open questions

### Local functions/lambda safe contexts

Right now `unsafe` on a method body is lexically scoped. Any nested local functions or lambdas inherit this, and their bodies are in a memory unsafe context. Is this behavior that we want to keep in
the language? Note that if we do keep `unsafe` as the modifier used to expose that the caller must be unsafe, this could then have impacts on the signature of the method. As currently proposed, nested
anonymous and local functions do not keep the unsafe context of their containing member.

### Lambda/method group conversion to safe delegate types

Is conversion of a requires-unsafe lambda or method group to a non-requires-unsafe delegate type permitted without warning or error in an `unsafe` context? If we don't do this, then it could be fairly painful
for various parts of the ecosystem, particularly any enumerables that are passed through LINQ queries.

### Delegate type `unsafe`ty

We could remove the ability to make delegate types as requires-unsafe entirely, and simply require that all conversions of requires-unsafe lambdas or method groups to a delegate type occur inside an `unsafe` context.
This could simplify the model around `unsafe` in C#, but at the risk of forcing `unsafe` annotations in the wrong spot and having an area where the real area of `unsafe`ty isn't properly called out. There
are a lot of corner cases here, particularly involving generics and conversions, so it may be better to simply leave the concept for later when we determine it's needed.

> [!NOTE]
> This is currently implemented (i.e., it's not possible to mark delegates as requires-unsafe and converting requires-unsafe methods/lambdas must happen in an `unsafe` context) because it seems like a good starting point.

> [!NOTE]
> If delegates indeed cannot be marked requires-unsafe, we should add them to the list of declarations that produce a warning for the `unsafe` modifier being meaningless.

### Lambda/method group natural types

Today, the only real impact on semantics and codegen (besides additional metadata) is changing the *function_type* of a lambda or method group when marked as `RequiresUnsafe`. If we were to avoid doing this, then
there would be no real impact to either, which could give adopters more confidence that behavior has not subtly changed under the hood.

### `stackalloc` as initialized

Today, [the spec](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12822-stack-allocation) always considers `stackalloc` memory as uninitialized, and says that the contents
are undefined unless manually cleared or assigned. Do we consider this a spec bug, or do we need to change what we consider `unsafe` for `stackalloc` purposes?

### `unsafe` expressions

Other languages with more comprehensive `unsafe` features have added `unsafe` as an expression, to enable improved user ergonomics and allow authors to more precisely limit where `unsafe` is used. Is this
something that we want to have in C#? Consider an inline call to an `unsafe` member that handles the safety directly: right now, the author would either need to wrap the entire statement in an `unsafe`
block, expanding the scope of the `unsafe` context, or they would need to break out the inner function call into an intermediate variable.

```cs
extern int Add(int i1, int i2); // Some fancy extern addition function

// Code I want to write:
Console.WriteLine(unsafe(Add(1, 2)));

// Code I have to write option 1, unsafe context unnecessary includes the WriteLine call
unsafe
{
    Console.WriteLine(Add(1, 2));
}

// Code I have to write option 2, very verbose and harder to read:
int result;
unsafe
{
    result = Add(1, 2);
}
Console.WriteLine(result);
```

### `unsafe` on types

We could consider not automatically making the entire lexical scope of an `unsafe` type to be an `unsafe` context and warn for an `unsafe` on a type as it would have no meaning
apart from edge cases like the following which we might not care about because they have no real-world use-cases:

```cs
class A : Attribute
{
    [RequiresUnsafe] public A() { }
}
[A] class C; // unavoidable error for using requires-unsafe A..ctor?
[A] unsafe class C; // if unsafe still introduces an unsafe context, this makes the error go away
```

### More meaningless `unsafe` warnings

Should more declarations produce the meaningless `unsafe` warning?
For example, fields without initializers (assuming we don't support [requires-unsafe fields](#requires-unsafe-fields)), methods with empty bodies (or `extern`), etc.
We already have an IDE analyzer for unnecessary `unsafe` though.

### Requires-unsafe fields

Today, no proposal is made around `RequiresUnsafe` on a field. We may need to add it though, such that any read from or write to a field marked as requires-unsafe must be in an `unsafe` context. This would
enable us to better annotate the concerns around code such as:

```cs
class SafeWrapper
{
     internal byte* _p;

     public void DoStuff()
     {
            unsafe
            {
                  // ... validate that the object state is good ...
                  // ... perform operation with _p .... 
            }
     }

}

// Elsewhere in safe code:
void M(SafeWrapper w)
{
     w._p = stackalloc byte[10];
}
```

### Taking the address of an uninitialized variable

Today, taking the address of a not definitely assigned variable can consider that variable definitely assigned, exposing uninitialized member. We have a couple of options to solving that:

1. Require that variables be definitely assigned before allowing an address-of operator to be used on them.
2. Make taking the address of an uninitialized variable unsafe.

Examples:

```cs
static void SkipInit<T>(out T value)  
{
    // value is considered definitely assigned after the address-of
    fixed (void* ptr = &value);
}
```

```cs
int i;
// i is considered definitely assigned after the address-of
_ = &i;
// Incrementing whatever was on the stack
i++;
```

### Value of `MemorySafetyRulesAttribute`

What should be the "enabled"/"updated" memory safety rules version? `2`? `15`? `11`?
See also https://github.com/dotnet/designs/blob/main/accepted/2025/memory-safety/sdk-memory-safety-enforcement.md.

### `extern` implicitly unsafe

This is currently the only place where `RequiresUnsafeAttribute` is synthesized by the compiler.
Are we okay with this outlier?

Also, CoreLib exposes many extern methods (FCalls) as safe today.
Treating extern methods as implicitly unsafe will require wrapping the implicitly unsafe extern methods with a safe wrapper.
We may run into situations where adding the extra wrapper is difficult due to runtime implementation details.

### `RequiresUnsafe` on `partial` members

It is required to have the `unsafe` modifier at both partial member parts by pre-existing C# rules.
On the other hand, attributes may be specified only at one of those parts
and even cannot be specified at both parts unless they have `AllowMultiple`, but then they are effectively present multiple times.
We have [changed](#use-unsafe-to-denote-requires-unsafe-members) the way to denote requires-unsafe members via an attribute instead of the `unsafe` modifier
but haven't discussed this aspect of the change.
Should we allow the attribute to be specified multiple times (via `AllowMultiple` or via special compiler behavior for this attribute and `partial` members only),
or even require it (via special compiler checks for this attribute only)?

## Answered questions

### How breaking do we want to skew

<details>
<summary>Question text</summary>

The initial proposal is a maximally-breaking approach, mainly as a litmus test for how aggressive we want to be. It proposes no ability to opt in/out sections of the code, changes the meaning of `unsafe`
on methods, prohibits the usage of `unsafe` on types, uses errors instead of warnings, and generally forces migration to occur all at once, at the time the compiler is upgraded (and then potentially
repeatedly as dependencies update and add `unsafe` to members that were already in use). However, we have a wealth of experience in making changes like this that we can draw on to scope the size of
the breaks down and allow incremental adoption. These options are covered below.

#### Opt in/out for code regions

This is not the first time that C# has redefined the "base" case of unannotated code. C# 8.0 introduced the nullable reference type feature, which in many ways can be seen as a blueprint for how the
`unsafe` feature is shaping up. It had similar goals (prevent bugs that cost billions of dollars by redefining the way default C# is interpreted) and a similar general featureset (add new info to types
to propagate states and avoid bugs). It was also heavily breaking, and needed a strong set of opt in and opt out functionality to allow the feature to be adopted over time by codebases. That
functionality is the "nullable reference type context". This is a lexical scope that informs the compiler, for a given region in code, both how to interpret unannotated type references and what types
of warnings to give to the user. We could use this as a model for `unsafe` as well, adding an "safety rules context" or similar to allow controlling whether these new rules are being applied or not.

One advantage that we have with the new `unsafe` features is that they are much less prevalent. While there are a decent number of `unsafe` calls in top libraries, our guesstimates on the percentage
of top libraries that use `unsafe` is much lower than "every single line of C# code ever written". Hopefully this means that, while some ability to opt in/out is possibly needed, we don't need as
complicated a mechanism as nullable has, with dedicated preprocessor switches and the like.

#### Warnings vs errors

The proposal currently states that memory safety requirements are currently enforced via a warning, rather than error. This is drawing from our experience working with the nullable feature, where warnings
allowed code bases to incrementally adopt the new feature and not need to convert large swathes of code all at once. We expect a similar process will be needed for unsafe warnings: many codebases will
simply be able to turn on the new rules globally and move on with their lives. But we expect the codebases we most care about adopting the new rules will have large amounts of code to annotate, and we
want them to be able to move forward with the feature, rather than seeing a wall of errors and giving up immediately. By making the requirements warnings, we allow these codebases to fix warnings file-by-file
or method-by-method as required, disabling the warnings everywhere else.

#### Method signature breaks

Right now, we propose that `unsafe` as a keyword on the method move from something that is lexically scoped without a semantic impact to something that has semantic impact, and isn't lexically scoped.
We could limit this break by introducing a new keyword for when the caller of a method or member must be in an `unsafe` context; for example, `callerunsafe` as a modifier.

#### Defaults for source generators

For nullable, we force generator authors to explicitly opt-in to nullable regardless of whether the entire project has opted into the feature by default, so that generator output isn't broken by the user
turning on nullable and warn as error. Should we do the same for source generators?

</details>

#### Conclusion

Answered in https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-11-05.md#unsafe-evolution. We will report errors for memory safety issues when the new rules are turned on, and no exceptions
for source generators will be made.


[unsafe-code]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#128-primary-expressions
[sizeof-const]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12819-the-sizeof-operator
[unsafe-context-spec]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#242-unsafe-contexts
[pointer-types-spec]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#243-pointer-types
[fixed-and-moveable-variables]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#244-fixed-and-moveable-variables
[pointer-conversions]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#245-pointer-conversions
[pointer-expressions]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#246-pointers-in-expressions
[the-addressof-operator]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2465-the-address-of-operator
[pointer-indirection]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2462-pointer-indirection
[pointer-member-access]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2463-pointer-member-access
[pointer-element-access]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2464-pointer-element-access
[sizeof-unsafe]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2469-the-sizeof-operator
[fixed-statement]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#247-the-fixed-statement
[fixed-size-buffer-declarations]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#2482-fixed-size-buffer-declarations
[stack-allocation-spec]: https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#249-stack-allocation
