Low Level Struct Improvements
=====

## Summary
This proposal is an aggregation of several different proposals for `struct` performance improvements: `ref` fields and the ability to override lifetime defaults. The goal being a design which takes into account the various proposals to create a single overarching feature set for low level `struct` improvements.

## Motivation
Earlier versions of C# added a number of low level performance features to the language: `ref` returns, `ref struct`, function pointers, etc. ... These enabled .NET developers to create write highly performant code while continuing to leverage the C# language rules for type and memory safety.  It also allowed the creation of fundamental performance types in the .NET libraries like `Span<T>`.

As these features have gained traction in the .NET ecosystem developers, both internal and external, have been providing us with information on remaining friction points in the ecosystem. Places where they still need to drop to `unsafe` code to get their work done, or require the runtime to special case types like `Span<T>`. 

Today `Span<T>` is accomplished by using the `internal` type `ByReference<T>` which the runtime effectively treats as a `ref` field. This provides the benefit of `ref` fields but with the downside that the language provides no safety verification for it, as it does for other uses of `ref`. Further only dotnet/runtime can use this type as it's `internal`, so 3rd parties can not design their own primitives based on `ref` fields. Part of the [motivation for this work](https://github.com/dotnet/runtime/issues/32060) is to remove `ByReference<T>` and use proper `ref` fields in all code bases. 

This proposal plans to address these issues by building on top of our existing low level features. Specifically it aims to:

- Allow `ref struct` types to declare `ref` fields.
- Allow the runtime to fully define `Span<T>` using the C# type system and remove special case type like `ByReference<T>`
- Allow `struct` types to return `ref` to their fields.
- Allow runtime to remove `unsafe` uses caused by limitations of lifetime defaults
- Allow the declaration of safe `fixed` buffers for managed and unmanaged types in `struct`

## Detailed Design 
The rules for `ref struct` safety are defined in the [span safety document](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md). This document will describe the required changes to this document as a result of this proposal. Once accepted as an approved feature these changes will be incorporated into that document.

Once this design is complete our `Span<T>` definition will be as follows:

<a name="new-span"></a>

```c#
readonly ref struct Span<T>
{
    readonly ref T _field;
    readonly int _length;

    // This constructor does not exist today but will be added as a part 
    // of changing Span<T> to have ref fields. It is a convenient, and
    // safe, way to create a length one span over a stack value that today 
    // requires unsafe code.
    public Span(ref T value)
    {
        _field = ref value;
        _length = 1;
    }
}
```

### Provide ref fields and scoped
The language will allow developers to declare `ref` fields inside of a `ref struct`. This can be useful for example when encapsulating large mutable `struct` instances or defining high performance types like `Span<T>` in libraries besides the runtime.

``` C#
ref struct S 
{
    public ref int Value;
}
```

A `ref` field will be emitted into metadata using the `ELEMENT_TYPE_BYREF` signature. This is no different than how we emit `ref` locals or `ref` arguments. For example `ref int _field` will be emitted as `ELEMENT_TYPE_BYREF ELEMENT_TYPE_I4`. This will require us to update ECMA335 to allow this entry but this should be rather straight forward.

Developers can continue to initialize a `ref struct` with a `ref` field using the `default` expression in which case all declared `ref` fields will have the value `null`. Any attempt to use such fields will result in a `NullReferenceException` being thrown.

```c#
ref struct S 
{
    public ref int Value;
}

S local = default;
local.Value.ToString(); // throws NullReferenceException
```

While the C# language pretends that a `ref` cannot be `null` this is legal at the runtime level and has well defined semantics. Developers who introduce `ref` fields into their types need to be aware of this possibility and should be **strongly** discouraged from leaking this detail into consuming code. Instead `ref` fields should be validated as non-null using the [runtime helpers](https://github.com/dotnet/runtime/pull/40008) and throwing when an uninitialized `struct` is used incorrectly.

```c#
ref struct S1 
{
    private ref int Value;

    public int GetValue()
    {
        if (System.Runtime.CompilerServices.Unsafe.IsNullRef(ref Value))
        {
            throw new InvalidOperationException(...);
        }

        return Value;
    }
}
```

A `ref` field can be combined with `readonly` modifiers in the following ways:

- `readonly ref`: this is a field that cannot be ref re-assigned outside a constructor or `init` methods. It can be value assigned though outside those contexts
- `ref readonly`: this is a field that can be ref re-assigned but cannot be value assigned at any point. This how an `in` parameter could be ref re-assigned to a `ref` field.
- `readonly ref readonly`: a combination of `ref readonly` and `readonly ref`. 

```c#
ref struct ReadOnlyExample
{
    ref readonly int Field1;
    readonly ref int Field2;
    readonly ref readonly int Field3;

    void Uses(int[] array)
    {
        Field1 = ref array[0];  // Okay
        Field1 = array[0];      // Error: can't assign ref readonly value (value is readonly)
        Field2 = ref array[0];  // Error: can't repoint readonly ref
        Field2 = array[0];      // Okay
        Field3 = ref array[0];  // Error: can't repoint readonly ref
        Field3 = array[0];      // Error: can't assign ref readonly value (value is readonly)
    }
}
```

A `readonly ref struct` will require that `ref` fields are marked as `readonly ref`. There is no requirement that they are marked as `readonly ref readonly`. This does allow a `readonly struct` to have indirect mutations via such a field but that is no different than a `readonly` field that pointed to a reference type today.

A `readonly ref` will be emitted to metadata using the `initonly` flag, same as any other field. A `ref readonly` field will be attributed with `System.Runtime.CompilerServices.IsReadOnlyAttribute`. A `readonly ref readonly` will be emitted with both items.

This feature requires runtime support and changes to the ECMA spec. As such these will only be enabled when the corresponding feature flag is set in corelib. The issue tracking the exact API is tracked here https://github.com/dotnet/runtime/issues/64165

The set of changes to our span safety rules necessary to allow `ref` fields is small and targeted. The rules already account for `ref` fields existing and being consumed from APIs. The changes need to focus on only two aspects: how they are created and how they are ref re-assigned. 

First the rules establishing *ref-safe-to-escape* values for fields needs to be updated for `ref` fields as follows:

<a name="rules-field-lifetimes"></a>

> An lvalue designating a reference to a field, e.F, is *ref-safe-to-escape* (by reference) as follows:
> 1. If `F` is a `ref` field and `e` is `this`, it is *ref-safe-to-escape* from the enclosing method.
> 2. Else if `F` is a `ref` field its *ref-safe-to-escape* scope is the *safe-to-escape* scope of `e`.
> 3. Else if `e` is of a reference type, it has *ref-safe-to-escape* of *calling method*
> 4. Else its *ref-safe-to-escape* is taken from the *ref-safe-to-escape* of `e`.

This does not represent a rule change though as the rules have always accounted for `ref` state to exist inside a `ref struct`. This is in fact how the `ref` state in `Span<T>` has always worked and the consumption rules correctly account for this. The change here is just accounting for developers to be able to access `ref` fields directly and ensure they do so by the existing rules implicitly applied to `Span<T>`. 

This does mean though that `ref` fields can be returned as `ref` from a `ref struct` but normal fields cannot.

```c#
ref struct RS
{
    ref int _refField;
    int _field;

    // Okay: this falls into bullet one above. 
    public ref int Prop1 => ref _refField;

    // Error: This is bullet four above and the ref-safe-to-escape of `this`
    // in a `struct` is the current method scope.
    public ref int Prop2 => ref _field;
}
```

This may seem like an error at first glance but this is a deliberate design point. Again though, this is not a new rule being created by this proposal, it is instead acknowledging the existing rules `Span<T>` behaved by now that developers can declare their own `ref` state.

Next the rules for ref re-assignment need to be adjusted for the presence of `ref` fields. The primary scenario for ref re-assignment is `ref struct` constructors storing `ref` parameters into `ref` fields. The support will be more general but this is the core scenario. To support this the rules for ref re-assignment will be adjusted to account for `ref` fields as follows:

<a name="rules-ref-re-assignment"></a>

> For a ref reassignment in the form ...
> 1. `x.e1 = ref e2`: where `x` is *safe-to-escape* to *calling method* then `e2` must be *ref-safe-to-escape* to the *calling method*
> 2. `e1 = ref e2`: where `e1` is a local or parameter, the *ref-safe-to-escape* of `e2` must be at least as wide a scope as the *ref-safe-to-escape* of `e1`.

That means the desired `Span<T>` constructor works without any extra annotation:

```c#
readonly ref struct Span<T>
{
    readonly ref T _field;
    readonly int _length;

    public Span(ref T value)
    {
        // Falls into the `x.e1 = ref e2` case, where `x` is the implicit `this`. The 
        // safe-to-escape of `this` and ref-safe-to-escape of `value` is *calling method* hence 
        // this is legal.
        _field = ref value;
        _length = 1;
    }
}
```

The change to ref re-assignment rules means `ref` parameters can now escape from a method as a `ref` field in a `ref struct` value. As discussed in the [compat considerations section](#new-span-challenges) this can change the rules for existing APIs that never intended for `ref` parameters to escape as a `ref` field. The lifetime rules for parameters are based soley on their declaration not on their usage. All `ref` and `in` parameters are *ref-safe-to-escape* to the *calling method* and hence can now be returned by `ref` or a `ref` field. In order to support APIs having `ref` parameters that can be escaping or non-escaping, and thus restore C# 10 call site semantics, the language will introduce limited lifetime annotations.

<a name="rules-scoped"></a>

The keyword `scoped` will be used to restrict the lifetime of a value. It can be applied to a `ref` or a value that is a `ref struct` and has the impact of restricting the *ref-safe-to-escape* or *safe-to-escape* lifetime, respectively, to the *current method*. For example: 

| Parameter or Local | ref-safe-to-escape | safe-to-escape |
|---|---|---|
| `Span<int> s` | *current method* | *calling method* | 
| `scoped Span<int> s` | *current method* | *current method* | 
| `ref Span<int> s` | *calling method* | *calling method* | 
| `scoped ref Span<int> s` | *current method* | *calling method* | 
| `ref scoped Span<int> s` | *current method* | *current method* | 

The declaration `scoped ref scoped Span<int>` is allowed but is redundant with `ref scoped Span<int>`. The *ref-safe-to-escape* of a value can never exceed the *safe-to-escape* hence once the *safe-to-escape* is restricted via `ref scoped` the *ref-safe-to-escape* is implicitly restricted as well.

This allows for APIs in C# 11 to be annotated such that they have the same rules as C# 10:

```c#
Span<int> CreateSpan(scoped ref int parameter)
{
    // Just as with C# 10, the implementation of this method isn't relevant to callers.
}

Span<int> BadUseExamples(int parameter)
{
    // Legal in C# 10 and legal in C# 11 due to scoped ref
    return CreateSpan(ref parameter);

    // Legal in C# 10 and legal in C# 11 due to scoped ref
    int local = 42;
    return CreateSpan(ref local);

    // Legal in C# 10 and legal in C# 11 due to scoped ref
    Span<int> span = stackalloc int[42];
    return CreateSpan(ref span[0]);
}
```

The `scoped` annotation also means that the `this` parameter of a `struct` can now be defined as `scoped ref T`. Previously it had to be special cased in the rules as `ref` parameter that had different *ref-safe-to-escape* rules than other `ref` parameters (see all the references to including or excluding the receiver in the span safety rules). Now it can be expressed as a general concept throughout the rules which further simplifies them.

In addition to parameters the `scoped` annotation can be applied to locals or `struct` instance methods. These annotations have the same impact to lifetimes when applied to locals. For locals these annotations concretely define the lifetimes and override the lifetime that would be inferred via the initializer. 

```c#
Span<int> ScopedLocalExamples()
{
    // Error: `span` has a safe-to-escape of *current method*. That is true even though the 
    // initializeer has a safe-to-escape of *calling method*. The annotation overrides the 
    // initializer
    scoped Span<int> span = default;
    return span;

    // Okay: the initializer has safe-to-escape of *calling method* hence so does `span2` 
    // and the return is legal.
    Span<int> span2 = default;
    return span2;

    // The declarations of `span3` and `span4` are functionally identical because the 
    // initializer has a safe-to-escape of *current method* meaning the `scoped` annotation
    // is effectively implied on `span3`
    Span<int> span3 = stackalloc int[42];
    scoped Span<int> span4 = stackalloc int[42];
}
```

Other uses for `scoped` on locals are discussed [below](#exmaples-scoped-locals).

When `scoped` is applied to an instance method the `this` parameter will have the type `scoped ref T`. By default this is redundant as `scoped ref T` is the default type of `this`. It is useful in the case the `struct` is declared as `unscoped` (detailed [below](#return-fields-by-ref)).

The `scoped` annotation cannot be applied to any other location including returns, fields, array elements, etc ... Further while `scoped` can be applied to any `ref`, `in` or `out` it can only be applied to values which are `ref struct`. Having declarations like `scoped int` adds no value to the rules and will be prevented to avoid developer confusion.

<a name="out-compat-change"></a>

To further limit the impact of the compat change of making `ref` and `in` parameters returnable as `ref` fields, the language will change the default *ref-safe-to-escape* value for `out` parameters to be *current method*. Effectively `out` parameters are implicitly `scoped out` going forward. From a compat perspective this means they cannot be returned by `ref`:

```c#
ref int Sneaky(out int i) 
{
    i = 42;

    // Error: ref-safe-to-escape of out is now the current method
    return ref i;
}
```

This change to `out` reduces the overall compat impact of this change. The ability to return `out` by reference is not practically useful, it's essentially a compiler trivia question. However it negatively impacts call site analysis because the rules must consider the case that it is returned by `ref` or `ref` field. Hence `out` arguments, even though 99% of the time are not returned by `ref` must be considered as such and that conflates lifetime issues. This would reduce the flexbility of APIs that return `ref struct` values and have `out` parameters. This is a common pattern in reader style APIs. 

```c#
Span<byte> Read(Span<byte> buffer, out int read)
{
    // .. 
}

Span<int> Use()
{
    var buffer = new byte[256];

    // If we keep current `out` ref-safe-to-escape this is an error. The langauge must consider
    // the `read` parameter as returnable as a `ref` field
    //
    // If we change `out` ref-safe-to-escape this is legal. The langauge does not consider the 
    // `read` parameter to be returnable hence this is safe
    int read;
    return Read(buffer, out read);
}
```

The span safety rules will be written in terms of `scoped ref` and `ref`. For span saftey purposes an `in` parameter is equivalent to `ref` and `out` is equivalent to `scoped ref`. Both `in` and `out` will only be specifically called out when it is important to the semantic of the rule. Otherwise they are just considered `ref` and `scoped ref` respectively.

<a name="rules-method-invocation"></a>

The span safety rules for method invocation will be updated in several ways. The first is by recognizing the impact that `scoped` has on arguments. For a given argument `a` that is passed to parameter `p`:

> 1. If `p` is `scoped ref` then `a` does not contribute *ref-safe-to-escape* when considering arguments. Note that `ref scoped` is included here as it implies `scoped ref scoped`.
> 2. If `p` is `scoped` then `a` does not contribute *safe-to-escape* when considering arguments. 

The language "does not contribute" means the arguments are simply not considered when calculating the *ref-safe-to-escape* or *safe-to-escape* value of the method return respectively. That is because the values can't contribute to that lifetime as the `scoped` annotation prevents it.

The method invocation for lvalue returns can now be simplified. The receiver no longer needs to be special cased, in the case of `struct` it is now simply a `scoped ref T`, nor do `out` parameters need to be considered in the argument list. 

> An lvalue resulting from a ref-returning method invocation `e1.M(e2, ...)` is *ref-safe-to-escape* the smallest of the following scopes:
> 1. The *calling method*
> 2. The *ref-safe-to-escape* of all `ref` arguments
> 3. The *ref-safe-to-escape* of all `in` parameters when the argument is an lvalue otherwise *current method*
> 4. The *safe-to-escape* of all argument expressions

The method invocation for rvalue returns needs to change as follows to account for `ref` field returns.

> An rvalue resulting from a method invocation `e1.M(e2, ...)` is *safe-to-escape* from the smallest of the following scopes:
> 1. The *calling method*
> 2. The *safe-to-escape* of all argument expressions
> 3. When the return is a `ref struct` then *ref-safe-to-escape* of all `ref` arguments

This rule now lets us define the two variants of desired methods:

```c#
Span<int> CreateWithoutCapture(scoped ref int value)
{
    // Error: RValue Rule 3 specifies that the safe-to-escape be limited to the ref-safe-to-escape
    // of the ref argument. That is the *current method* for value hence this is not allowed.
    return new Span<int>(ref value);
}

Span<int> CreateAndCapture(ref int value)
{
    // Okay: RValue Rule 3 specifies that the safe-to-escape be limited to the ref-safe-to-escape
    // of the ref argument. That is the *calling method* for value hence this is not allowed.
    return new Span<int>(ref value)
}

Span<int> ComplexScopedRefExample(scoped ref Span<int> span)
{
    // Okay: the safe-to-escape of `span` is *calling method* hence this is legal.
    return span;

    // Okay: the local `refLocal` has a ref-safe-to-escape of *current method* and a 
    // safe-to-escape of *calling method*. In the call below it is passed to a 
    // parameter that is `scoped ref` which means it does not contribute 
    // ref-safe-to-escape. It only contributes its safe-to-escape hence the returned
    // rvalue ends up as safe-to-escape of *calling method*
    Span<int> local = default;
    ref Span<int> refLocal = ref local;
    return ComplexScopedRefExample(ref refLocal);

    // Error: similar analysis as above but the safe-to-escape scope of `stackLocal` is 
    // *current method* hence this is illegal
    Span<int> stackLocal = stackalloc int[42];
    return ComplexScopedRefExample(ref stackLocal);
}
```

<a name="rules-method-arguments-must-match"></a>

The presence of `scoped` allows us to also refine the method arguments must match rule. It can now be reduced to 

> For a method invocation if there is a `ref` or `scoped ref` argument of a `ref struct` type with *safe-to-escape* E1 then no argument may contribute a narrower *safe-to-escape* than E1.

Impact of this change is discussed more deeply [below](#examples-method-arguments-must-match). Overall this will allow developers to make call sites more flexible by annotating non-escaping ref-like values with `scoped`.

The section on `ref` field and `scoped` is long so wanted to close with a brief summary of the proposed breaking changes:

* A value that has *ref-safe-to-escape* to the *calling method* is returnable by `ref` or `ref` field.
* A `out` parameter would be considered `ref-safe-to-escape` within the *current method*.

Detailed Notes:
- A `ref` field can only be declared inside of a `ref struct` 
- A `ref` field cannot be declared `static`, `volatile` or `const`
- The reference assembly generation process must preserve the presence of a `ref` field inside a `ref struct` 
- A `readonly ref struct` must declare its `ref` fields as `readonly ref`
- The span safety rules document will be updated as outlined in this document.
- The new span safety rules will be in effect when either 
    - The core library contains the feature flag indicating support for `ref` fields
    - The `langversion` value is 11 or higher

### Sunset restricted types
The compiler has a concept of a set of "restricted types" which is largely undocumented. These types were given a special status because in C# 1.0 there was no general purpose way to express their behavior. Most notably the fact that the types can contain references to the execution stack. Instead the compiler had special knowledge of them and restricted their use to ways that would always be safe: disallowed returns, cannot use as array elements, cannot use in generics, etc ...

Once `ref` fields are available these types can be correctly defined in C# using a combination of `ref struct` and `ref` fields. Therefore when the compiler detects that a runtime supports `ref` fields it will no longer have a notion of restricted types. It will instead use the types as they are defined in the code. 

To support this our span safety rules will be updated as follows:

- `__makeref` will be treated as a method with the signature `static TypedReference __makeref<T>(ref T value)`
- `__refvalue` will be treated as a method with the signature `static ref T __refvalue<T>(TypedReference tr)`. The expression `__refvalue(tr, int)` will effectively use the second argument as the type parameter.
- `__arglist` as a parameter will have a *ref-safe-to-escape* and *safe-to-escape* of *current method*. 
- `__arglist(...)` as an expression will have a *ref-safe-to-escape* and *safe-to-escape* of *current method*. 

Conforming runtimes will ensure that `TypedReference`, `RuntimeArgumentHandle` and `ArgIterator` are defined as `ref struct`. That combined with the above rules will ensure references to the stack do not escape beyond their lifetime.

Note: strictly speaking this is a compiler implementation detail vs. part of the language. But given the relationship with `ref` fields it is being included in the language proposal for simplicity.

### Provide unscoped
One of the most notable friction points is the inability to return fields by `ref` in instance members of a `struct`. This means developers can't create `ref` returning methods / properties and have to resort to exposing fields directly. This reduces the usefulness of `ref` returns in `struct` where it is often the most desired. 

```c#
struct S
{
    int _field;

    // Error: this, and hence _field, can't return by ref
    public ref int Prop => ref _field;
}
```

The [rationale](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md#struct-this-escape) for this default is reasonable but there is nothing inherently wrong with a `struct` escaping `this` by reference, it is simply the default chosen by the span safety rules. 

<a name="rules-unscoped"></a>

To fix this the  language will provide the opposite of the `scoped` lifetime annotation in the syntax `unscoped`.  The keyword `unscoped` will be used to expand the lifetime of a value. It can be applied to any `ref` which is implicitly `unscoped` and has the impact of changing its *ref-safe-to-escape* to *calling method*.

```c#
struct S
{
    int field; 

    // Error: `field` has the ref-safe-to-escape of `this` which is *current method* because 
    // it is a `scoped ref`
    ref int Prop1 => ref field;

    // Okay: `field` has the ref-safe-to-escape of `this` which is *calling method* because 
    // it is a `ref`
    unscoped ref int Prop1 => ref field;
}
```

The annotation can also be placed directly on the `struct` declaration and has the impact of changing `this` to simply `ref T` on all instance methods.

```c#
unscoped struct S
{
    int field;

    // Okay
    ref int Prop => ref field;
}
```

This will naturally, by the existing rules in the span safety spec, allow for returning transitive fields in addition to direct fields.

```c#
unscoped struct Child
{
    int _value;
    public ref int Value => ref _value;
}

unscoped struct Container
{
    Child _child;

    // In this case the ref-safe-to-escape of `_child` is to the calling method because that is 
    // the value of `this` and fields derive it from their receiver. From there method invocation 
    // rules take over 
    public ref int Value => ref _child.Value;
}
```

The annotation can also be placed on `out` parameters to restore them to C# 10 behavior.

```c#
ref int SneakyOut(unscoped out int i)
{
    i = 42;
    return ref i;
}
```

For the purposes of span safety rules, such an `unscoped out` is considered simply a `ref`. Similar to how `in` is considered `ref` for lifetime purposes.

The `unscoped` annotation will be disallowed on `init` members and constructors. Those members are already special with respect to `ref` semantics as they view `readonly` members as mutable. This means taking `ref` to those members appears as a simple `ref`, not `ref readonly`. This is allowed within the boundary of constructors and `init`. Allowing `unscoped` would permit such `ref` to incorrectly escape outside the constructor and permit mutation after `readonly` semantics had taken place.

Detailed Notes:
- An instance method or property annotated with `unscoped` has *ref-safe-to-escape* of `this` set to the *calling method*. It means `this` is effectively a `ref` parameter to the method.
- A `struct` annotated with `unscoped` has the same effect of annotating every instance method and property with `unscoped`
- A member annotated with `unscoped` cannot implement an interface.
- It is an error to use `unscoped` on 
    - Any type other than a `struct` (although it is legal for all variations like `readonly struct`)
    - Any member that is not declared on a `struct`
    - Any `static` member, `init` member or constructor on a `struct`

### LifetimeAnnotationAttribute
The `scoped` and `unscoped` annotations will be emitted into metadata via the type `System.Runtime.CompilerServices.LifetimeAttribute` attribute. This attribute will be matched by name meaning it does not need to appear in any specific assembly.

The type will have the following definition:

```c#
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class LifetimeAnnotationAttribute : Attribute
{
    public bool IsRefScoped { get; set; }
    public bool IsValueScoped { get; set; }

    public LifetimeAnnotationAttribute(bool isRefScoped, bool isValueScoped)
    {
        IsRefScoped = isRefScoped;
        IsValueScoped = isValueScoped;
    }
}
```

The compiler will emit this attribute on the element targeted by the `scoped` or `unscoped` syntax. This is true for types, methods and parameters. This will only be emitted when the syntax causes the value to differ from its default state. For example `scoped out` will cause no attribute to be emitted.

### Safe fixed size buffers
The language will relax the restrictions on fixed sized arrays such that they can be declared in safe code and the element type can be managed or unmanaged.  This will make types like the following legal:

```c#
internal struct CharBuffer
{
    internal char Data[128];
}
```

These declarations, much like their `unsafe` counter parts, will define a sequence of `N` elements in the containing type. These members can be accessed with an indexer and can also be converted to `Span<T>` and `ReadOnlySpan<T>` instances.

When indexing into a `fixed` buffer of type `T` the `readonly` state of the container must be taken into account.  If the container is `readonly` then the indexer returns `ref readonly T` else it returns `ref T`. 

Accessing a `fixed` buffer without an indexer has no natural type however it is convertible to `Span<T>` types. In the case the container is `readonly` the buffer is implicitly convertible to `ReadOnlySpan<T>`, else it can implicitly convert to `Span<T>` or `ReadOnlySpan<T>` (the `Span<T>` conversion is considered *better*). 

The resulting `Span<T>` instance will have a length equal to the size declared on the `fixed` buffer. The *safe-to-escape* scope of the returned value will be equal to the *safe-to-escape* scope of the container, just as it would if the backing data was accessed as a field.

For each `fixed` declaration in a type where the element type is `T` the language will generate a corresponding `get` only indexer method whose return type is `ref T`. The indexer will be annotated with the `unscoped` annotation as the implementation will be returning fields of the declaring type. The accessibility of the member will match the accessibility on the `fixed` field.

For example, the signature of the indexer for `CharBuffer.Data` will be the following:

```c#
unscoped internal ref char <>DataIndexer(int index) => ...;
```

If the provided index is outside the declared bounds of the `fixed` array then an `IndexOutOfRangeException` will be thrown. In the case a constant value is provided then it will be replaced with a direct reference to the appropriate element. Unless the constant is outside the declared bounds in which case a compile time error would occur.

There will also be a named accessor generated for each `fixed` buffer that provides by value `get` and `set` operations. Having this means that `fixed` buffers will more closely resemble existing array semantics by having a `ref` accessor as well as byval `get` and `set` operations. This means compilers will have the same flexibility when emitting code consuming `fixed` buffers as they do when consuming arrays. This should be operations like `await` over `fixed` buffers easier to emit. 

This also has the added benefit that it will make `fixed` buffers easier to consume from other languages. Named indexers is a feature that has existed since the 1.0 release of .NET. Even languages which cannot directly emit a named indexer can generally consume them (C# is actually a good example of this).

The backing storage for the buffer will be generated using the `[InlineArray]` attribute. This is a mechanism discussed in [issue 12320](https://github.com/dotnet/runtime/issues/12320) which allows specifically for the case of efficiently declaring sequence of fields of the same type. This particular issue is still under active discussion and the expectation is that the implementation of this feature will follow however that discussion goes.

## Considerations
There are considerations other parts of the development stack should consider when evaluating this feature.

### Compat Considerations
The challenge in this proposal is the compatibility implications this design has to our existing [span safety rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md). While those rules fully support the concept of a `ref struct` having `ref` fields they do not allow for APIs, other than `stackalloc`, to capture `ref` state that refers to the stack. The span safety rules have a [hard assumption](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md#span-constructor) that a constructor of the form `Span(ref T value)` does not exist. That means the safety rules do not account for a `ref` parameter being able to escape as a `ref` field hence it allows for code like the following.

```c#
Span<int> CreateSpan<int>()
{
    // This is legal according to the 7.2 span rules because they do not account
    // for a constructor in the form Span(ref T value) existing. 
    int local = 42;
    return new Span<int>(ref local);
}
```

<a name="ways-to-escape"></a>

Effectively there are three ways for a `ref` parameter to escape from a method invocation: 

1. By value
2. By `ref` 
3. By `ref` field

The existing rules only account for (1) and (2). They do not account for (3) hence gaps like returning locals as `ref` fields are not accounted for. This design must change the rules to account for (3). This will have a small impact to compatibility for existing APIs. Specifically it will impact APIs that have the following properties.

- Have a `ref struct` in the signature
    - Where the `ref struct` is a return type, `ref` or `out` parameter
    - Has an additional `in` or `ref` parameter excluding the receiver

In C# 10 callers of such APIs never had to consider that `ref` state input to the API could be captured as a `ref` field. That allowed for several patterns to exist, safely in C# 10, that will be unsafe in C# 11 due to the ability for `ref` state to escape as a `ref` field. For example:

<a name="new-span-challenges"></a>

```c#
Span<int> CreateSpan(ref int parameter)
{
    // The implementation of this method is irrelevant when considering the lifetime of the 
    // returned Span<T>. The span safety rules only look at the method signature, not the 
    // implementation. In C# 10 ref fields didn't exist hence there was no way for `parameter`
    // to escape by ref in this method
}

Span<int> BadUseExamples(int parameter)
{
    // Legal in C# 10 but would be illegal with ref fields
    return CreateSpan(ref parameter);

    // Legal in C# 10 but would be illegal with ref fields
    int local = 42;
    return CreateSpan(ref local);

    // Legal in C# 10 but would be illegal with ref fields
    Span<int> span = stackalloc int[42];
    return CreateSpan(ref span[0]);
}
```

The impact of this compatibility break is expected to be very small. The impacted API shape made little sense in the absence of `ref` fields hence it is unlikely customers created many of these. Experiments running tools to spot this API shape over existing repositories back up that assertion. The only repository with any significant counts of this shape is [dotnet/runtime](https://github.com/dotnet/runtime) and that is because that repo can create `ref` fields via the `ByReference<T>` intrinsic type.

Even so the design must account for such APIs existing because it expresses a valid pattern, just not a common one. Hence the design must give developers the tools to restore the existing lifetime rules when upgrading to C# 10. Specifically it must provide mechanisms that allow developers to annotate `ref` parameters as unable to escape by `ref` or `ref` field. That allows customers to define APIs in C# 11 that have the same C# 10 callsite rules.

### Reference Assemblies
A reference assembly for a compilation using features described in this proposal must maintain the elements that convey span safety information. That means all lifetime annotation attributes must be preserved in their original position. Any attempt to replace or omit them can lead to invalid reference assemblies.

Representing `ref` fields is more nuanced. Ideally a `ref` field would appear in a reference assembly as would any other field. However a `ref` field represents a change to the metadata format and that can cause issues with tool chains that are not updated to understand this metadata change. A concrete example is C++/CLI which will likely error if it consumes a `ref` field. Hence it's advantageous if `ref` fields can be omitted from reference assemblies in our core libraries. 

A `ref` field by itself has no impact on span safety rules. As a concrete example consider that flipping the existing `Span<T>` definition to use a `ref` field has no impact on consumption. Hence the `ref` itself can be omitted safely. However a `ref` field does have other impacts to consumption that must be preserved: 

- A `ref struct` which has a `ref` field is never considered `unmanaged` 
- The type of the `ref` field impacts infinite generic expansion rules. Hence if the type of a `ref` field contains a type parameter that must be preserved 

Given those rules here is a valid reference assembly transformation for a `ref struct`: 

```c#
// Impl assembly 
ref struct S<T>
{
    ref T _field;
}

// Ref assembly 
ref struct S<T>
{
    object _o; // force managed 
    T _f; // mantain generic expansion protections
}
```

### Example demonstrating rules 

#### Ref re-assignmet and call sites

Demonstrating how [ref re-assignment](#rules-ref-re-assignment) and [method invocation](#rules-method-invocation) work together.

```c#
ref struct RS
{
    ref int _refField;

    public ref int Prop => ref _refField;

    public RS(int[] array)
    {
        _refField = ref array[0];
    }

    public RS(ref int i)
    {
        _refField = ref i;
    }

    public RS CreateRS() => ...;

    public ref int M1(RS rs)
    {
        // The arguments contribute here:
        //   - `rs` contributes no ref-safe-to-escape as the corresponding parameter, 
        //      which is `this`, is `scoped ref`
        //   - `rs` contribute safe-to-escape of *calling method*
        // 
        // This is an lvalue invocation and the arguments contribute only safe-to-escape 
        // values of *calling method*. That means `local` is safe-to-escape to *calling method*
        ref int local1 = ref rs.Prop;

        // Okay: this is legal because `local` has safe-to-escape of *calling method*
        return ref local1;

        // The arguments contribute here:
        //   - `this` contributes no ref-safe-to-escape as the corresponding parameter
        //     is `scoped ref`
        //   - `this` contributes safe-to-escape of *calling method*
        //
        // This is an rvalue invocation and following those rules the safe-to-escape of 
        // `local2` will be *calling method*
        RS local2 = CreateRS();

        // Okay: this follows the same analysis as `ref rs.Prop` above
        return ref local2.Prop;

        // The arguments contribute here:
        //   - `local3` contributes ref-safe-to-escape of *current method*
        //   - `local3` contributes safe-to-escape of *calling method*
        // 
        // This is an rvalue invocation which returns a `ref struct` and following those 
        // rules the safe-to-escape of `local4` will be *current method*
        int local3 = 42;
        var local4 = new RS(ref local3);

        // Error: 
        // The arguments contribute here:
        //   - `local4` contributes no ref-safe-to-escape as the corresponding parameter
        //     is `scoped ref`
        //   - `local4` contributes safe-to-escape of *current method*
        // 
        // This is an lvalue invocation and following those rules the ref-safe-to-escape 
        // of the return is *current method*
        return ref local4.Prop1;
    }
}
```

#### scoped locals
<a name="examples-scoped-locals"></a>

The use of `scoped` on locals will be particularly helpful to code patterns which conditionally assign values with different *safe-to-escape* scope to locals. It means code no longer needs to rely on initializaion tricks like `= stackalloc byte[0]` to define a local *safe-to-escape* but now can simply use `scoped`. 

```c#
// Old way 
// Span<byte> span = stackalloc byte[0];
// New way 
scoped Span<byte> span;
int len = ...;
if (len < MaxStackLen)
{
    span = stackalloc byte[len];
}
else
{
    span = new byte[len];
}
```

This pattern comes up frequently in low level code. When the `ref struct` involved is `Span<T>` the above trick can be used. It is not applicable to other `ref struct` types though and can result in low level code needing to resort to `unsafe` to work around the inability to properly specify the lifetime. 

#### scoped parameter values
One source of repeated friction in low level code is the default escape for parameters is permissive. They are *safe-to-escape* to the *calling method*. This is a sensible default because it lines up with the coding patterns of .NET as a whole. In low level code though there is a larger use of  `ref struct` and this default can cause friction with other parts of the span safety rules.

The main friction point occurs because of the [method arguments must match](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md#method-arguments-must-match) rule. This rule most commonly comes into play with instance methods on `ref struct` where at least one parameter is also a `ref struct`. This is a common pattern in low level code where `ref struct` types commonly leverage `Span<T>` parameters in their methods. For example it will occur on any writer style `ref struct` that uses `Span<T>` to pass around buffers.

This rule exists to prevent scenarios like the following:

```c#
ref struct RS
{
    Span<int> _field;
    void Set(Span<int> p)
    {
        _field = p;
    }

    static void DangerousCode(ref RS p)
    {
        Span<int> span = stackalloc int[] { 42 };

        // Error: if allowed this would let the method return a reference to 
        // the stack
        p.Set(span);
    }
}
```

Essentially this rule exists because the language must assume that all inputs to a method escape to their maximum allowed scope. When there are `ref` or `out` parameters, including the receivers, it's possible for the inputs to escape as fields of those `ref` values (as happens in `RS.Set` above).

In practice though there are many such methods which pass `ref struct` as parameters that never intend to capture them in output. It is just a value that is used within the current method. For example:

```c#
ref struct JsonReader
{
    Span<char> _buffer;
    int _position;

    internal bool TextEquals(ReadOnySpan<char> text)
    {
        var current = _buffer.Slice(_position, text.Length);
        return current == text;
    }
}

class C
{
    static void M(ref JsonReader reader)
    {
        Span<char> span = stackalloc char[4];
        span[0] = 'd';
        span[1] = 'o';
        span[2] = 'g';

        // Error: The safe-to-escape of `span` is the current method scope 
        // while `reader` is outside the current method scope hence this fails
        // by the above rule.
        if (reader.TextEquals(span))
        {
            ...
        }
    }
}
```

In order to work around this low level code will resort to `unsafe` tricks to lie to the compiler about the lifetime of their `ref struct`. This significantly reduces the value proposition of `ref struct` as they are meant to be a means to avoid `unsafe` while continuing to write high performance code.

This is where `scoped` is an effective tool on `ref struct` parameters because it removes them from consideration as being returned from the method according to the updated [method arguments must match rule](#rules-method-arguments-must-match). A `ref struct` parameter which is consumed, but never returned, can be labeled as `scoped` to make call sites more flexible. 

```c#
ref struct JsonReader
{
    Span<char> _buffer;
    int _position;

    internal bool TextEquals(scoped ReadOnySpan<char> text)
    {
        var current = _buffer.Slice(_position, text.Length);
        return current == text;
    }
}

class C
{
    static void M(ref JsonReader reader)
    {
        Span<char> span = stackalloc char[4];
        span[0] = 'd';
        span[1] = 'o';
        span[2] = 'g';

        // Okay: the compiler never considers `span` as capturable here hence it doesn't
        // contribute to the method arguments must match rule
        if (reader.TextEquals(span))
        {
            ...
        }
    }
}
```

## Open Issues

### Change the design to avoid compat breaks
This design proposes several compatibility breaks with our existing span safety rules. Even though the breaks are believed to be minimally impactful significant consideration was given to a design which had no breaking changes.

The compat preserving design though was significantly more complex than this one. In order to preserve compat `ref` fields need distinct lifetimes for the ability to return by `ref` and return by `ref` field. Essentially it requires us to provide *ref-field-safe-to-escape* tracking for all parameters to a method. This needs to be calculated for all expressions and tracked in all values virtually everywhere that *ref-safe-to-escape* is tracked today.

Further this value has relationships with *ref-safe-to-escape*. For example it's non-sensical to have a value can be returned as a `ref` field but not directly as `ref`. That is because `ref` fields can be trivially returned by `ref` already (`ref` state in a `ref struct` can be returned by `ref` even when the containing value cannot). Hence the rules further need constant adjustment to ensure these values are sensible with respect to each other. 

Also it means the language needs syntax to represent `ref` parameters that can be returned in three different ways: by `ref` field, by `ref` and by value. The default being returnable by `ref`. Going forward though the more natural return, particularly when `ref struct` are involved is expected to be by `ref` field or `ref`. That means new APIs require an extra syntax annotation to be correct by default. This is undesirable. 

These compat changes though will impact methods that have the following properties:

- Have a `Span<T>` or `ref struct`
    - Where the `ref struct` is a return type, `ref` or `out` parameter
    - Has an additional `in` or `ref` parameter (excluding the receiver)

To understand the impact it's helpful to break APIs into categories:

1. Want consumers to account for `ref` being captured as a `ref` field. Prime example is the `Span(ref T value)` constructors 
2. Do not want consumers to account for `ref` being captured as a `ref` field. These though break into two catogries
    1. Unsafe APIs. These are APIS inside the `Unsafe` and `MemoryMarshal` types, of which `MemoryMarshal.CreateSpan` is the most prominent. These APIs do capture the `ref` unsafely but they are also known to be unsafe APIs.
    2. Safe APIs. These are APIs which take `ref` parameters for efficiency but it is not actually captured anywhere. Examples are small but one is `AsnDecoder.ReadEnumeratedBytes`

This change primarily benefits (1) above. These are expected to make up the majority of APIs that take a `ref` and return a `ref struct` going forward. The changes negatively impact (2.1) and (2.2) as it breaks the existing calling semantics because the lifetime rules change. 

The APIs in category (2.1) though are largely authored by Microsoft or by developers who stand the most to benefit from `ref` fields (the Tanner's of the world). It is reasonable to assume this class of developers would be ammenable to a compatability tax on upgrade to C# 11 in the form of a few annotations to retain the existing semantics if `ref` fields were provided in return.

The APIs in category (2.2) are the biggest issue. It is unknown how many such APIs exist and it's unclear if these would be more / less frequent in 3rd party code. The expectation is there is a very small number of them, particularly if we take the compat break on `out`. Searches so far have revealed a very small number of these existing in `public` surface area. This is a hard pattern to search for though as it requires semantic analysis. Before taking this change a tool based approach would be needed to verify the assumptions around this impacting a small number of known cases.

For both cases in category (2) though the fix is straight forward. The `ref` parameters that do not want to be considered capturable must add `scoped` to the `ref`. In (2.1) this will likely also force the developer to use `Unsafe` or `MemoryMarshal` but that is expected for unsafe style APIs.

Ideally the language could reduce the impact of silent breaking changes by issuing a warning when an API silently falls into the troublesome behavior. That would be a method that both takes a `ref`, returns `ref struct` but does not actually capture the `ref` in the `ref struct`. The compiler could issue a diagnostic in that case informing developers such `ref` should be annotated as `scoped ref` instead. 


**Decision** This design can be achieved but the resulting feature is more difficult to use to the point the decision was made to take the compat break.
**Decision** The compiler will provide a warning when a method meets the criteria but does not capture the `ref` parameter as a `ref` field. This should suitably warn customers on upgrade about the potential issues they are creating

### Keywords vs. attributes
This design calls for using attributes to annotate the new lifetime rules. This also could've been done just as easily with contextual keywords. For instance `[DoesNotEscape]` could map to `scoped`. However keywords, even the contextual ones, generally must meet a very high bar for inclusion. They take up valuable language real estate and are more prominent parts of the language. This feature, while valuable, is going to serve a minority of C# developers.

On the surface that would seem to favor not using keywords but there are two important points to consider: 

1. The annotations will effect program semantics. Having attributes impact program semantics is a line C# is reluctant to cross and it's unclear if this is the feature that should justify the language taking that step.
1. The developers most likely to use this feature intersect strongly with the set of developers that use function pointers. That feature, while also used by a minority of developers, did warrant a new syntax and that decision is still seen as sound. 

Taken together this means syntax should be considered.

A rough sketch of the syntax would be: 

- `[RefDoesNotEscape]` maps to `scoped ref` 
- `[DoesNotEscape]` maps to `scoped`
- `[RefDoesEscape]` maps to `unscoped`

**Decision** Use synatx

### Allow fixed buffer locals
This design allows for safe `fixed` buffers that can support any type. One possible extension here is allowing such `fixed` buffers to be declared as local variables. This would allow a number of existing `stackalloc` operations to be replaced with a `fixed` buffer. It would also expand the set of scenarios we could have stack style allocations as `stackalloc` is limited to unmanaged element types while `fixed` buffers are not. 

```c#
class FixedBufferLocals
{
    void Example()
    {
        Span<int> span = stakalloc int[42];
        int buffer[42];
    }
}
```

This holds together but does require us to extend the syntax for locals a bit.  Unclear if this is or isn't worth the extra complexity. Possible we could decide no for now and bring back later if sufficient need is demonstrated.

Example of where this would be beneficial: https://github.com/dotnet/runtime/pull/34149

**Decision** hold off on this for now

### To use modreqs or not
A decision needs to be made if methods marked with new lifetime attributes should or should not translate to `modreq` in emit. There would be effectively a 1:1 mapping between annotations and `modreq` if this approach was taken.

The rationale for adding a `modreq` is the attributes change the semantics of span safety. Only languages which understand these semantics should be calling the methods in question. Further when applied to OHI scenarios, the lifetimes become a contract that all derived methods must implement. Having the annotations exist without `modreq` can lead to situations where `virtual` method chains with conflicting lifetime annotations are loaded (can happen if only one part of `virtual` chain is compiled and other is not). 

The initial span safety work did not use `modreq` but instead relied on languages and the framework to understand. At the same time though all of the elements that contribute to the span safety rules are a strong part of the method signature: `ref`, `in`, `ref struct`, etc ... Hence any change to the existing rules of a method already results in a binary change to the signature. To give the new lifetime annotations the same impact they will need `modreq` enforcement.

The concern is whether or not this is overkill. It does have the negative impact that making signatures more flexible, by say adding `[DoesNotEscape]` to a paramater, will result in a binary compat change. That trade off means that over time frameworks like BCL likely won't be able to relax such signatures. It could be mitigated to a degree by taking some approach the language does with `in` parameters and only apply `modreq` in virtual positions. 

**Decision** Do not use `modreq` in metadata. The difference between `out` and `ref` is not `modreq` but they now have different span safety lifetimes. There is no real benefit to only half enforcing the rules with `modreq` here.

### Allow multi-dimensional fixed buffers
Should the design for `fixed` buffers be extended to include multi-dimensional style arrays? Essentially allowing for declarations like the following:

```c#
struct Dimensions
{
    int array[42, 13];
}
```

**Decision** Do not allow for now

### Violating scoped
The runtime repository has several non-public APIs that capture `ref` paramters as `ref` fields. These are unsafe because the lifetime of the resulting value is not tracked. For example the `Span<T>(ref T value, int length)` constructor.

The majority of these APIs will likely choose to have proper lifetime tracking on the return which will be achieved simply by updating to C# 11. A few though will want to keep their current semantics of not tracking the return value because their entire intent is to be unsafe. The most notable examples are `MemoryMarshal.CreateSpan` and `MemoryMarshal.CreateReadOnlySpan`. This will be achieved by marking the parameters as `scoped`.

That means the runtime needs an established pattern for unsafely removing `scoped` from a parameter. This can be done today via a combination of existing methods:

```c#
Span<T> CreateSpan<T>(scoped ref T value, int length)
{
    ref T local = Unsafe.AsRef<T>(Unsafe.AsPointer(ref value));
    return new Span<T>(local, length);
}
```

This will work but is likely going to result in unnecessary code generation. One other consideration is that either:

1. `Unsafe.AsRef<T>(in T value)` could expand its existing purpose by changing to `scoped in T value`. This would allow it to both remove `in` and `scoped` from parameters. It then becomes the universal "remove ref safety" method
2. Introduce a new method whose entire purpose is to remove `scoped`: `ref T Unsafe.AsUnscoped<T>(scoped in T value)`. This removes `in` as well because if it did not then callers still need a combination of method calls to "remove ref safety" at which point the existing solution is likely sufficient.

### What will make C# 11.0
The features outlined in this document don't need to be implemented in a single pass. Instead they can be implemented in phases across several language releases in the following buckets:

1. `ref` fields and `scoped`
2. Sunset restricted types
3. `unscoped` 
4. fixed sized buffers

What gets implemented in which release is merely a scoping exercise. 

**Decision** Only `ref` fields, `scoped` and sunsetting restricted types will make C# 11.0. LDM is happy to revisit `unscoped` if a more natural keyword can be settled on or data suggests it's possible to make `this` `unscoped` by default in all cases.

## Future Considerations

### Advanced lifetime annotations
The lifetime annotations in this proosal are limited in that they allow developers to change the default escape / don't escape behavior of values. This does add powerful flexibility to our model but it does not radically change the set of relationships that can be expressed. At the core the C# model is still effectively binary: can a value be returned or not?

That allows limited lifetime relationships to be understood. For example a value that can't be returned from a method has a smaller lifetime than one that can be returned from a method. There is no way to describe the lifetime relationship between values that can be returned from a method though. Specifically there is no way to say that one value has a larger lifetime than the other once it's established both can be returned from a method. The next step in our lifetime evolution would be allowing such relationships to be described. 

Other methods such as Rust allow this type of relationship to be expressed and hence can implement handle more complex `scoped` style operations. Our language could similarly benifit if such a feature were included. At the moment there is no movitating pressure to do this but if there is in the future our `scoped` model could be expanded to inclued it in a fairly straight forward fashion. 

Every `scoped` could be assigned a named lifetime by adding a generic style argument to the syntax. For example `scoped<'a>` is a value that has lifetime `'a`. Constraints like `where` could then be used to describe the relationships between these lifetimes.

```c#
void M(scoped<'a> ref MyStruct s, scoped<'b> Span<int> span)
  where 'b >= 'a
{
    s.Span = span;
}
```

This method defines two lifetimes `'a` and `'b` and there relationship, specifically that `'b` is greater than `'a`. This allows for the callsite to have more granular rules for how values can be safely passed into methods vs. the more coarse grained rules present today.

## Related Information

### Issues
The following issues are all related to this proposal:

- https://github.com/dotnet/csharplang/issues/1130
- https://github.com/dotnet/csharplang/issues/1147
- https://github.com/dotnet/csharplang/issues/992
- https://github.com/dotnet/csharplang/issues/1314
- https://github.com/dotnet/csharplang/issues/2208
- https://github.com/dotnet/runtime/issues/32060
- https://github.com/dotnet/runtime/issues/61135
- https://github.com/dotnet/csharplang/discussions/78

### Proposals
The following proposals are related to this proposal:

- https://github.com/dotnet/csharplang/blob/725763343ad44a9251b03814e6897d87fe553769/proposals/fixed-sized-buffers.md

### Existing samples

[Utf8JsonReader](https://github.com/dotnet/runtime/blob/f1a7cb3fdd7ffc4ce7d996b7ac6867ffe2c953b9/src/libraries/System.Text.Json/src/System/Text/Json/Reader/Utf8JsonReader.cs#L523-L528)

This particular snippet requires unsafe because it runs into issues with passing around a `Span<T>` which can be stack allocated to an instance method on a `ref struct`. Even though this parameter is not captured the language must assume it is and hence needlessly causes friction here.

[Utf8JsonWriter](https://github.com/dotnet/runtime/blob/f1a7cb3fdd7ffc4ce7d996b7ac6867ffe2c953b9/src/libraries/System.Text.Json/src/System/Text/Json/Writer/Utf8JsonWriter.WriteProperties.String.cs#L122-L127)

This snippet wants to mutate a parameter by escaping elements of the data. The escaped data can be stack allocated for efficiency. Even though the parameter is not escaped the compiler assigns it a *safe-to-escape* scope of outside the enclosing method because it is a parameter. This means in order to use stack allocation the implementation must use `unsafe` in order to assign back to the parameter after escaping the data.

### Fun Samples

#### ReadOnlySpan<T>

```c#
public readonly ref struct ReadOnlySpan<T>
{
    readonly ref readonly T _value;
    readonly int _length;

    public ReadOnlySpan<T>(in T value)
    {
        _value = ref value;
        _length = 1;
    }
}
```

#### Frugal list

```c#
struct FrugalList<T>
{
    private T _item0;
    private T _item1;
    private T _item2;

    public int Count = 3;

    public ref T this[int index]
    {
        unscoped get
        {
            switch (index)
            {
                case 0: return ref _item1;
                case 1: return ref _item2;
                case 2: return ref _item3;
                default: throw null;
            }
        }
    }
}
```

#### Stack based linked list

```c#
ref struct StackLinkedListNode<T>
{
    T _value;
    ref StackLinkedListNode<T> _next;

    public T Value => _value;

    public bool HasNext => !Unsafe.IsNullRef(ref _next);

    public ref StackLinkedListNode<T> Next 
    {
        get
        {
            if (!HasNext)
            {
                throw new InvalidOperationException("No next node");
            }

            return ref _next;
        }
    }

    public StackLinkedListNode(T value)
    {
        this = default;
        _value = value;
    }

    public StackLinkedListNode(T value, ref StackLinkedListNode<T> next)
    {
        _value = value;
        _next = ref next;
    }
}
```

### Important Cases
There are several important cases that need to be handled correctly by the rules. These are those cases and explanation of how the rules prevent them from happening.

#### Preventing tricky ref assignment from readonly mutation
When a `ref` is taken to a `readonly` field in a constructor or `init` member the type is `ref` not `ref readonly`. This is a long standing behavior that allows for code like the following:

```c#
struct S
{
    readonly int i; 

    public S(string s)
    {
        M(ref i);
    }

    static void M(ref int i) { }
}
```

That does pose a potential problem though if such a `ref` were able to be stored into a `ref` field on the same type. It would allow for direct mutation of a `readonly struct` from an instance member:

```c#
readonly ref struct S
{ 
    readonly int i; 
    readonly ref int r; 
    public S()
    {
        i = 0;
        r = ref i;
    }

    public void Oops()
    {
        r++;
    }
```

The proposal prevents this though because it violates the span safety rules. Consider the following:

- The *ref-safe-to-escape* of `this` is *current method* and *safe-to-escape* is *calling method*. These are both standard for `this` in a `struct` member.
- The *ref-safe-to-escape* of `i` is *current method*. This falls out from the [field lifetimes rules](#rules-field-lifetimes). Specifically rule 4.

At that point the line `r = ref i` is illegal by [ref re-assignment rules](#rules-ref-re-assignment). 

These rules were not intended to prevent this behavior but do so as a side effect. It's important to keep this in mind for any future rule update to evaluate the impact to scenarios like this.
