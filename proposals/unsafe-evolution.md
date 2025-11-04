# Unsafe Evolution

Champion issue: https://github.com/dotnet/csharplang/issues/9704

## Summary

We update the definition of `unsafe` in C# from referring to locations where pointer types are used, to be locations where memory unmanaged by the runtime is dereferenced. These locations
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

Background for this feature can also be found in https://github.com/dotnet/designs/blob/main/proposed/caller-unsafe.md, which tracks the broader ecosystem changes that will be needed as part of this proposal.
These include BCL updates to properly annotate methods as being unsafe, as well as tooling updates for better understanding of where memory unsafety occurs. For C#
specifically, we want to make sure that memory unsafety is properly tracked by the language; today, it can be difficult to look at a program holistically and understand all locations where
memory unsafety occurs. This is because various helpers such as the `System.Runtime.CompilerServices.Unsafe`, `System.Runtime.InteropServices.Marshal`, and others do not express that they
violate memory safety and need special consideration. Methods that then use these helpers aren't immediately obvious, and when auditing code for memory safety issues (either ahead of time
when doing review, or when trying to determine the cause of a vulnerability that is being reported) it can be difficult to pinpoint the locations that could be contributing to issues.

Historically, `unsafe` in C# has referred to a specific memory-safety hole: the existence of pointer types. The moment that a pointer type is no longer involved, C# is perfectly happy to let
memory unsafety lie latent in code. It is this issue that we are looking to address with this evolution of `unsafe` in C# and the .NET ecosystem, labeling areas where memory unsafety could
potentially occur, making it easier for reviewers and auditors to understand the boundaries of potential memory unsafety in a program. Importantly, this means that we will be _changing_
the meaning of `unsafe`, not just augmenting it. The existence of a pointer is not itself unsafe; the unsafe action is dereferencing the pointer. This extends further to types themselves;
types cannot be inherently unsafe. It is only the action of using a type that could be unsafe, not the existence of that type. The new meaning of `unsafe` on a type, and what an unsafe context
is, is covered in [unsafe contexts](#unsafe-contexts).

In order for this information to flow through the system, we therefore need to have a way to mark methods themselves as `unsafe`. Today, `unsafe` as a method modifier has no external impact,
it only allows pointers to be used in the signature and body of the member. Going forward, `unsafe` as a modifier will actually publicly change the meaning of the member; it will indicate that
the member has memory safety concerns and any usages must be manually validated by the programmer using the member.

This is a potentially large breaking change for particular segments of the C# user base. Our hope is that, for many of our users, this is effectively transparent, and updating to the new rules
will be seamless. However, given that some large API surfaces like large parts of reflection may need to be marked `unsafe`, we do think it likely that there will need to be a decent on-ramp to
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

#### Expression memory safety state

> [!NOTE]
> Now accepting naming suggestions

We introduce a new state that is tracked for all expressions in C#: the memory safety state. There are two possible safety states for any expression: safe, or unsafe. Expressions with a memory safety
state of safe may be used anywhere they are normally legal in C#. Expressions with a memory safety state of unsafe can only be used in an [unsafe context][unsafe-context-spec], and any use outside of
an unsafe context is a warning.

For every expression production in [§12](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md), if all of its nested expressions have a memory safety state of safe, then that
expression has a memory safety state of safe. If that expression has no nested expressions, it has a memory safety state of safe. The following expressions always have a memory safety state of unsafe:

* [Pointer indirections][pointer-indirection]
* [Pointer member access][pointer-member-access]
* [Pointer element access][pointer-element-access]
* Function pointer invocation
* Element access on a fixed-size buffer
* `stackalloc` under the conditions defined [below](#stack-allocation)

In addition to these expressions, expressions can also conditionally introduce a memory safety state of unsafe if they depend on any symbol that is marked as `unsafe`. For example, calling a method
that is marked as `unsafe` will cause the _invocation_expression_ to have a memory safety state of unsafe, even if the receiver and all arguments have a memory safety state of safe.

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
access memory that is not managed the runtime. They remain in [§24][unsafe-code.md], and continue to require an `unsafe` context to be used. Any use outside of an `unsafe` context is a warning.
No semantics about these operators change; they still continue to mean exactly the same thing that they do today. These expressions always have a memory safety state of unsafe.

The [fixed statement][fixed-statement] moves to [§13](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md), with references to `unsafe` contexts removed.

Function pointers are not yet incorporated into the main C# specification, but they are similarly affected; everything but function pointer invocation is moved into the standard specification.
A function pointer invocation expression always has a memory safety state of unsafe.

#### Fixed-size buffers

The story for [fixed-size buffers][fixed-size-buffer-declarations] is similar to [pointers](#pointer-types). The definition of a fixed-size buffer is not itself dangerous, and moves to
[§16.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/structs.md#163-struct-members). Accessing a fixed-size buffer in an expression is similarly safe, unless the expression occurs
as the _primary_expression_ of an `element_access`; these are evaluated as a _pointer_element_access_, which is unsafe, as per the rules above.

#### Stack allocation

Again, the story for [stack allocation][stack-allocation-spec] is very similar to [pointers](#pointer-types). Converting a `stackalloc` to a pointer is no longer unsafe; it is the deference of that
pointer that is unsafe. We do add one new rule, however:

A _stackalloc_expression_ has a [safety state](#expression-memory-safety-state) of unsafe if all of the following statements are true:

* The _stackalloc_expression_ is being converted to a `Span<T>` or a `ReadOnlySpan<T>`.
* The _stackalloc_expression_ does not have a _stackalloc_initializer_.
* The _stackalloc_expression_ is used within a member that has `SkipLocalsInitAttribute` applied.

In these contexts, the resulting stack space could have unknown memory contents, and it is being converted to a type that provides a safe wrapper around unmanaged memory access. This violates the
contract of `Span<T>` and `ReadOnlySpan<T>`, and so must be subject to extra scrutiny by the author and reviewers of such code.

> [!NOTE]
> This means that assigning a `stackalloc` to a pointer is _always_ safe, regardless of context.

### Overriding, inheritance, and implementation

It is a memory safety warning to add `unsafe` at the member level in any override or implementation of a member that does not have `unsafe` on it originally, because callers may be using the base
definition and not see any addition of `unsafe` by a derived implementation.

### Delegates and lambdas

It is a memory safety warning to convert an `unsafe` member to a delegate type that is not marked `unsafe`. The [_function type_](csharp-10.0/lambda-improvements.md#natural-function-type) definition
is updated to include whether the _anonymous function_ has the `unsafe` keyword, or the _method group_ is to a member that is marked `unsafe`. If it is, an anonymous function type is created, just as
it would be if any parameter were a by-`ref`, optional, or `params`.

It is a memory safety warning convert a delegate type that is marked as `unsafe` to `System.Delegate`/`System.Linq.Expressions.Expression`/`System.Linq.Expressions.Expression<T>`.

A delegate type that is marked `unsafe` can only be invoked in an `unsafe` context, and the invocation of an `unsafe` delegate has a memory safety state of `unsafe`. If a delegate type is `unsafe`,
then its `Invoke`, `BeginInvoke`, and `EndInvoke` methods are also marked as `unsafe`.

### `extern`

Because `extern` methods are to native locations that cannot be guaranteed by the runtime, any `extern` method is automatically considered `unsafe`. Even methods that only take `unmanaged` parameters by
value cannot be safely called by C#, as the calling convention used for the method could be incorrectly specified by the user and must be manually verified by review.

### Unsafe modifiers and contexts

Today, as covered by the [unsafe context specification][unsafe-context-spec], `unsafe` behaves in a lexical manner, marking the entire textual body contained by the `unsafe` block as an `unsafe` context
(except for iterator bodies). We propose changing this definition from textual to sematic. `unsafe` on a type will now mean that all members declared by that type are considered `unsafe`, and all of the
member bodies of that type are considered an `unsafe` context. `unsafe` on a member will mean that that member is `unsafe`, and the body of that member is considered an `unsafe` context. For existing code
moving to the new definition of `unsafe`, this may produce a number of false positives for methods that don't need to be considered `unsafe`; we believe this better than false positives around not doing
this, or making it an error to put `unsafe` on a type which would easily be the largest breaking change that we've ever introduced in C#.

`unsafe` on a member is _not_ applied to any nested anonymous or local functions inside the member. To mark a anonymous or local function as `unsafe`, it must manually be marked as `unsafe`. The same goes for
anonymous and local functions declared inside of an `unsafe` block.

When a type is `partial`, `unsafe` on a part of that type marks everything declared or defined in that part as `unsafe`. If a `partial` member is declared or defined in an `unsafe partial` part, that member
is considered `unsafe`, even if it is also declared or defined in a part that not marked as `unsafe`. If one part of a member is declared as `unsafe`, then all parts of that member are considered unsafe.

```cs
new C1().M1(); // Warning, M1() must be called in an `unsafe` context
new C2().M2(); // Warning, M2() must be called in an `unsafe` context

unsafe partial class C1
{
    public partial void M1();
}

partial class C1
{
    public partial void M1() => Console.WriteLine("hello world");
}

partial class C2
{
    public partial unsafe void M2();
}

partial class C2
{
    public partial void M2() => Console.WriteLine("hello world");
}
```

For properties, `get` and `set/init` members can be independently declared as `unsafe`; marking the entire property as `unsafe` means that both the `get` and `set/init` members are unsafe.

## Open questions

### How breaking do we want to skew

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

#### Method signature breaks

Right now, we propose that `unsafe` as a keyword on the method move from something that is lexically scoped without a semantic impact to something that has semantic impact, and isn't lexically scoped.
We could limit this break by introducing a new keyword for when the caller of a method or member must be in an `unsafe` context; for example, `callerunsafe` as a modifier.

#### Defaults for source generators

For nullable, we force generator authors to explicitly opt-in to nullable regardless of whether the entire project has opted into the feature by default, so that generator output isn't broken by the user
turning on nullable and warn as error. Should we do the same for source generators?

### Local functions/lambda safe contexts

Right now `unsafe` on a method body is lexically scoped. Any nested local functions or lambdas inherit this, and their bodies are in a memory unsafe context. Is this behavior that we want to keep in
the language? Note that if we do keep `unsafe` as the modifier used to expose that the caller must be unsafe, this could then have impacts on the signature of the method. As currently proposed, nested
anonymous and local functions do not keep the unsafe context of their containing member.

### Lambda/method group conversion to safe delegate types

Is conversion of a lambda or method group marked `unsafe` to a non-unsafe delegate type permitted without warning or error in an `unsafe` context? If we don't do this, then it could be fairly painful
for various parts of the ecosystem, particularly any enumerables that are passed through LINQ queries.

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

### `unsafe` fields

Today, no proposal is made around `unsafe` on a field. We may need to add it though, such that any read from or write to a field marked as `unsafe` must be in an `unsafe` context. This would
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
