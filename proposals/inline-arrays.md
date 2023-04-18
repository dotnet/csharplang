Inline Arrays
=====

## Summary

Provide a general-purpose and safe mechanism for consuming struct types utilizing
[InlineArrayAttribute](https://github.com/dotnet/runtime/issues/61135) feature.
Provide a general-purpose and safe mechanism for declaring inline arrays within C# classes, structs, and interfaces.

## Motivation

This proposal plans to address the many limitations of https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#228-fixed-size-buffers.
Specifically it aims to allow:
- accessing elements of struct types utilizing [InlineArrayAttribute](https://github.com/dotnet/runtime/issues/61135) feature;
- the declaration of inline arrays for managed and unmanaged types in a `struct`, `class`, or `interface`.

And provide language safety verification for them.


## Detailed Design 

Recently runtime added [InlineArrayAttribute](https://github.com/dotnet/runtime/issues/61135) feature.
In short, a user can declare a structure type like the following:

``` C#
[System.Runtime.CompilerServices.InlineArray(10)]
public struct Buffer
{
    private object _element0;
}
```

Runtime provides a special type layout for the ```Buffer``` type:
- The size of the type is extended to fit 10 (the number comes from the InlineArray attribute) elements of ```object```
  type (the type comes from the type of the only instance field in the struct, ```_element0``` in this example).
- The first element is aligned with the instance field and with the beginning of the struct
- The elements are laid out sequentially in memory as though they are elements of an array.

Runtime provides regular GC tracking for all elements in the struct.

This proposal will refer to types like this as "inline array types".

Elements of an inline array type can be accessed through pointers or through span instances returned by
[System.Runtime.InteropServices.MemoryMarshal.CreateSpan](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.memorymarshal.createspan?view=net-7.0)/[System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.memorymarshal.createreadonlyspan?view=net-7.0) APIs. However, neither
the pointer approach, nor the APIs provide type and bounds checking out of the box.

Language will provide a type-safe/ref-safe way for accessing elements of inline array types. The access will be span based. 
This limits support to inline array types with element types that can be used as a type argument.
For example, a pointer type cannot be used as an element type. Other examples the span types.

### Obtaining instances of span types for an inline array type

Since there is a guarantee that the first element in an inline array type is aligned at the beginning of the type (no gap), compiler will use the
following code to get a ```Span``` value:
``` C#
MemoryMarshal.CreateSpan(ref Unsafe.As<TBuffer, TElement>(ref buffer), size)
```

And the following code to get a ```ReadOnlySpan``` value:
``` C#
MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<TBuffer, TElement>(ref Unsafe.AsRef(in buffer)), size)
```

In order to reduce IL size at use sites compiler should be able to add two generic reusable helpers into private implementation detail type and
use them across all use sites in the same program.

``` C#
public static System.Span<TElement> InlineArrayAsSpan<TBuffer, TElement>(ref TBuffer buffer, int size) where TBuffer : struct
{
    return MemoryMarshal.CreateSpan(ref Unsafe.As<TBuffer, TElement>(ref buffer), size);
}

public static System.ReadOnlySpan<TElement> InlineArrayAsReadOnlySpan<TBuffer, TElement>(in TBuffer buffer, int size) where TBuffer : struct
{
    return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<TBuffer, TElement>(ref Unsafe.AsRef(in buffer)), size);
}
```

### Element access

The [Element access](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#11710-element-access) will be extended
to support inline array element access.

An *element_access* consists of a *primary_no_array_creation_expression*, followed by a “`[`” token, followed by an *argument_list*, followed by a “`]`” token. The *argument_list* consists of one or more *argument*s, separated by commas.

```ANTLR
element_access
    : primary_no_array_creation_expression '[' argument_list ']'
    ;
```

The *argument_list* of an *element_access* is not allowed to contain `ref` or `out` arguments.

An *element_access* is dynamically bound ([§11.3.3](expressions.md#1133-dynamic-binding)) if at least one of the following holds:

- The *primary_no_array_creation_expression* has compile-time type `dynamic`.
- At least one expression of the *argument_list* has compile-time type `dynamic` and the *primary_no_array_creation_expression* does not have an array type,
  **and the *primary_no_array_creation_expression* does not have an inline array type or there is more than one item in the argument list**.

In this case, the compiler classifies the *element_access* as a value of type `dynamic`. The rules below to determine the meaning of the *element_access* are then applied at run-time, using the run-time type instead of the compile-time type of those of the *primary_no_array_creation_expression* and *argument_list* expressions which have the compile-time type `dynamic`. If the *primary_no_array_creation_expression* does not have compile-time type `dynamic`, then the element access undergoes a limited compile-time check as described in [§11.6.5](expressions.md#1165-compile-time-checking-of-dynamic-member-invocation).

If the *primary_no_array_creation_expression* of an *element_access* is a value of an *array_type*, the *element_access* is an array access ([§11.7.10.2](expressions.md#117102-array-access)). **If the *primary_no_array_creation_expression* of an *element_access* is a variable or value of an inline array type and the *argument_list* consists of a single argument, the *element_access* is an inline array element access.** Otherwise, the *primary_no_array_creation_expression* shall be a variable or value of a class, struct, or interface type that has one or more indexer members, in which case the *element_access* is an indexer access ([§11.7.10.3](expressions.md#117103-indexer-access)).

#### Inline array element access

For an inline array element access, the *primary_no_array_creation_expression* of the *element_access* must be a variable or value of an inline array type. Furthermore, the *argument_list* of an inline array element access is not allowed to contain named arguments. The *argument_list* must contain a single expression, and the expression must be 
- of type `int`, or
- implicitly convertible to `int`, or
- implicitly convertible to ```System.Index```, or 
- implicitly convertible to ```System.Range```. 

##### When the expression type is int

If *primary_no_array_creation_expression* is a writable variable, the result of evaluating an inline array element access is a writable variable
equivalent to invoking [`public ref T this[int index] { get; }`](https://learn.microsoft.com/en-us/dotnet/api/system.span-1.item?view=net-8.0) with
that integer value on an instance of ```System.Span<T>``` returned by ```System.Span<T> InlineArrayAsSpan``` method on *primary_no_array_creation_expression*. 

If *primary_no_array_creation_expression* is a readonly variable, the result of evaluating an inline array element access is a readonly variable
equivalent to invoking [`public ref readonly T this[int index] { get; }`](https://learn.microsoft.com/en-us/dotnet/api/system.readonlyspan-1.item?view=net-8.0) with
that integer value on an instance of ```System.ReadOnlySpan<T>``` returned by ```System.ReadOnlySpan<T> InlineArrayAsReadOnlySpan```
method on *primary_no_array_creation_expression*. 

If *primary_no_array_creation_expression* is a value, the result of evaluating an inline array element access is a value
equivalent to invoking [`public ref readonly T this[int index] { get; }`](https://learn.microsoft.com/en-us/dotnet/api/system.readonlyspan-1.item?view=net-8.0) with
that integer value on an instance of ```System.ReadOnlySpan<T>``` returned by ```System.ReadOnlySpan<T> InlineArrayAsReadOnlySpan```
method on *primary_no_array_creation_expression*. 

For example:
``` C#
[System.Runtime.CompilerServices.InlineArray(10)]
public struct Buffer10<T>
{
    private T _element0;
}

void M1(Buffer10<int> x)
{
    ref int a = ref x[0]; // Ok, equivalent to `ref int a = ref InlineArrayAsSpan<Buffer10<int>, int>(ref x, 10)[0]`
}

void M2(in Buffer10<int> x)
{
    ref readonly int a = ref x[0]; // Ok, equivalent to `ref readonly int a = ref InlineArrayAsReadOnlySpan<Buffer10<int>, int>(in x, 10)[0]`
    ref int b = ref x[0]; // An error, `x` is a readonly variable => `x[0]` is a readonly variable
}

Buffer10<int> GetBuffer() => default;

void M3()
{
    int a = GetBuffer()[0]; // Ok, equivalent to `int a = InlineArrayAsReadOnlySpan<Buffer10<int>, int>(GetBuffer(), 10)[0]` 
    ref readonly int b = ref GetBuffer()[0]; // An error, `GetBuffer()[0]` is a value
    ref int c = ref GetBuffer()[0]; // An error, `GetBuffer()[0]` is a value
}
```

Indexing into an inline array with a constant expression outside of the declared inline array bounds is a compile time error.

##### When the expression is implicitly convertible to `int`

The expression is converted to int and then the element access is interpreted as described in **When the expression type is int** section.

##### When the expression implicitly convertible to ```System.Index```

The expression is converted to ```System.Index```, which is then transformed to an int-based index value as described at https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md#implicit-index-support, assuming that the
length of the collection is known at compile time and is equal to the amount of elements in the inline array type of
the *primary_no_array_creation_expression*. Then the element access is interpreted as described in
**When the expression type is int** section.

##### When the expression implicitly convertible to ```System.Range```

If *primary_no_array_creation_expression* is a writable variable, the result of evaluating an inline array element access is a value
equivalent to invoking [`public Span<T> Slice (int start, int length)`](https://learn.microsoft.com/en-us/dotnet/api/system.span-1.slice?view=net-8.0)
on an instance of ```System.Span<T>``` returned by ```System.Span<T> InlineArrayAsSpan``` method on *primary_no_array_creation_expression*. 

If *primary_no_array_creation_expression* is a readonly variable, the result of evaluating an inline array element access is a value
equivalent to invoking [`public ReadOnlySpan<T> Slice (int start, int length)`](https://learn.microsoft.com/en-us/dotnet/api/system.readonlyspan-1.slice?view=net-8.0)
on an instance of ```System.ReadOnlySpan<T>``` returned by ```System.ReadOnlySpan<T> InlineArrayAsReadOnlySpan```
method on *primary_no_array_creation_expression*. 

If *primary_no_array_creation_expression* is a value, an error is reported. 

The arguments for the ```Slice``` method invocation are calculated from the index expression converted to```System.Range``` as described at
https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md#implicit-range-support, assuming that the length of the collection
is known at compile time and is equal to the amount of elements in the inline array type of the *primary_no_array_creation_expression*.

Compiler can omit the ```Slice``` call if it is known at compile time that `start` is 0 and `length` is less or equal to the amount of elements in the
inline array type. Compiler can also report an error if it is known at compile time that slicing goes out of inline array bounds.

For example:
``` C#
void M1(Buffer10<int> x)
{
    System.Span<int> a = x[..]; // Ok, equivalent to `System.Span<int> a = InlineArrayAsSpan<Buffer10<int>, int>(ref x, 10).Slice(0, 10)`
}

void M2(in Buffer10<int> x)
{
    System.ReadOnlySpan<int> a = x[..]; // Ok, equivalent to `System.ReadOnlySpan<int> a = InlineArrayAsReadOnlySpan<Buffer10<int>, int>(in x, 10).Slice(0, 10)`
    System.Span<int> b = x[..]; // An error, System.ReadOnlySpan<int> cannot be converted to System.Span<int>
}

Buffer10<int> GetBuffer() => default;

void M3()
{
    _ = GetBuffer()[..]; // An error, `GetBuffer()` is a value
}
```


### Conversions

A new conversion, an inline array conversion, from expression will be added. The inline array conversion is
a [standard conversion](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#104-standard-conversions).

There is an implicit conversion from expression of an inline array type to the following types:
- ```System.Span<T>```
- ```System.ReadOnlySpan<T>```

However, converting a readonly variable to ```System.Span<T>``` or converting a value to either type is an error.

For example:
``` C#
void M1(Buffer10<int> x)
{
    System.ReadOnlySpan<int> a = x; // Ok, equivalent to `System.ReadOnlySpan<int> a = InlineArrayAsReadOnlySpan<Buffer10<int>, int>(in x, 10)`
    System.Span<int> b = x; // Ok, equivalent to `System.Span<int> b = InlineArrayAsSpan<Buffer10<int>, int>(ref x, 10)`
}

void M2(in Buffer10<int> x)
{
    System.ReadOnlySpan<int> a = x; // Ok, equivalent to `System.ReadOnlySpan<int> a = InlineArrayAsReadOnlySpan<Buffer10<int>, int>(in x, 10)`
    System.Span<int> b = x; // An error, readonly mismatch
}

Buffer10<int> GetBuffer() => default;

void M3()
{
    System.ReadOnlySpan<int> a = GetBuffer(); // An error, ref-safety
    System.Span<int> b = GetBuffer(); // An error, ref-safety
}
```


### List patterns

[List patterns](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/list-patterns.md) will not be supported for instances
of inline array types.

### Definite assignment checking

Regular definite assignment rules are applicable to variables that have an inline array type. 



## Open design questions

### Initializer

Should we support initialization at declaration site with, perhaps, [collection literals](https://github.com/dotnet/csharplang/blob/main/proposals/collection-literals.md)?

## Alternatives

### Detailed Design (Option 2)

Note, that for the purpose of this proposal a term "fixed-size buffer" refers to a the proposed "safe fixed-size buffer" feature rather than to a buffer described at https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#228-fixed-size-buffers.

In this design, fixed-size buffer types do not get general special treatment by the language.
There is a special syntax to declare members that represent fixed-size buffers and new rules around consuming those members.
They are not fields from the language point of view.

The grammar for *variable_declarator* in https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#145-fields
will be extended to allow specifying the size of the buffer:

``` diff antlr
field_declaration
    : attributes? field_modifier* type variable_declarators ';'
    ;

field_modifier
    : 'new'
    | 'public'
    | 'protected'
    | 'internal'
    | 'private'
    | 'static'
    | 'readonly'
    | 'volatile'
    | unsafe_modifier   // unsafe code support
    ;

variable_declarators
    : variable_declarator (',' variable_declarator)*
    ;
    
variable_declarator
    : identifier ('=' variable_initializer)?
+   | fixed_size_buffer_declarator
    ;
    
fixed_size_buffer_declarator
    : identifier '[' constant_expression ']'
    ;    
```

A *fixed_size_buffer_declarator* introduces a fixed-size buffer of a given element type.

The buffer element type is the *type* specified in `field_declaration`. A fixed-size buffer declarator introduces a new member and consists of an identifier that names the member, followed by a constant expression enclosed in `[` and `]` tokens. The constant expression denotes the number of elements in the member introduced by that fixed-size buffer declarator. The type of the constant expression must be implicitly convertible to type `int`, and the value must be a non-zero positive integer.

The elements of a fixed-size buffer shall be laid out sequentially in memory as though they are elements of an array.

A *field_declaration* with a *fixed_size_buffer_declarator* in an interface must have `static` modifier.

Depending on the situation (details are specified below), an access to a fixed-size buffer member is classified as a value (never a variable) of either
`System.ReadOnlySpan<S>` or `System.Span<S>`, where S is the element type of the fixed-size buffer. Both types provide indexers returning a reference to a
specific element with appropriate "readonly-ness", which prevents direct assignment to the elements when language rules don't permit that.

This limits the set of types that can be used as a fixed-size buffer element type to types that can be used as type arguments. For example, a pointer type cannot be used as an element type.

The resulting span instance will have a length equal to the size declared on the fixed-size buffer.
Indexing into the span with a constant expression outside of the declared fixed-size buffer bounds is a compile time error.

The *safe-to-escape* scope of the value will be equal to the *safe-to-escape* scope of the container, just as it would if the backing data was accessed as a field.

#### Fixed-size buffers in expressions

Member lookup of a fixed-size buffer member proceeds exactly like member lookup of a field.

A fixed-size buffer can be referenced in an expression using a *simple_name* or a *member_access* .

When an instance fixed-size buffer member is referenced as a simple name, the effect is the same as a member access of the form `this.I`, where `I` is the fixed-size buffer member. When a static fixed-size buffer member is referenced as a simple name, the effect is the same as a member access of the form `E.I`, where `I` is the fixed-size buffer member and `E` is the declaring type.

##### Non-readonly fixed-size buffers

In a member access of the form `E.I`, if `E` is of a struct type and a member lookup of `I` in that struct type identifies a non-readonly instance fixed-size member,
then `E.I` is evaluated and classified as follows:

- If `E` is classified as a value, then `E.I` can be used only as a *primary_no_array_creation_expression* of
  an [element access](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#11710-element-access)
  with index of ```System.Index``` type, or of a type implicitly convertible to int. Result of the element access is
  a fixed-size member's element at the specified position, classified as a value.   
- Otherwise, if `E` is classified as a readonly variable and the result of the expression is classified as a value of type `System.ReadOnlySpan<S>`,
  where S is the element type of `I`. The value can be used to access member's elements.
- Otherwise, `E` is classified as a writable variable and the result of the expression is classified as a value of type `System.Span<S>`,
  where S is the element type of `I`. The value can be used to access member's elements.

In a member access of the form `E.I`, if `E` is of a class type and a member lookup of `I` in that class type identifies a non-readonly instance fixed-size member,
then `E.I` is evaluated and classified as a value of type `System.Span<S>`, where S is the element type of `I`.

In a member access of the form `E.I`, if member lookup of `I` identifies a non-readonly static fixed-size member,
then `E.I` is evaluated and classified as a value of type `System.Span<S>`, where S is the element type of `I`.

##### Readonly fixed-size buffers

When a *field_declaration* includes a `readonly` modifier, the member introduced by the fixed_size_buffer_declarator is a ***readonly fixed-size buffer***.
Direct assignments to elements of a readonly fixed-size buffer can only occur in an instance constructor, init member or static constructor in the same type.
Specifically, direct assignments to an element of readonly fixed-size buffer are permitted only in the following contexts:

- For an instance member, in the instance constructors or init member of the type that contains the member declaration; for a static member,
  in the static constructor of the type that contains the member declaration. These are also the only contexts in which it is valid to pass
  an element of readonly fixed-size buffer as an `out` or `ref` parameter.

Attempting to assign to an element of a readonly fixed-size buffer or pass it as an `out` or `ref` parameter in any other context is a compile-time error.
This is achieved by the following.

A member access for a readonly fixed-size buffer is evaluated and classified as follows:

- In a member access of the form `E.I`, if `E` is of a struct type and `E` is classified as a value, then `E.I` can be used only as a
  *primary_no_array_creation_expression* of an [element access](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#11710-element-access)
  with index of ```System.Index``` type, or of a type implicitly convertible to int. Result of the element access is a fixed-size member's element at the specified
  position, classified as a value.
- If access occurs in a context where direct assignments to an element of readonly fixed-size buffer are permitted, the result of the expression
  is classified as a value of type `System.Span<S>`, where S is the element type of the fixed-size buffer. The value can be used to access member's elements.
- Otherwise, the expression is classified as a value of type `System.ReadOnlySpan<S>`, where S is the element type of the fixed-size buffer.
  The value can be used to access member's elements.

#### Definite assignment checking

Fixed-size buffers are not subject to definite assignment-checking, and fixed-size buffer members are ignored for purposes of definite-assignment checking of struct type variables.

When a fixed-size buffer member is static or the outermost containing struct variable of a fixed-size buffer member is a static variable, an instance variable of a class instance, or an array element, the elements of the fixed-size buffer are automatically initialized to their default values. In all other cases, the initial content of a fixed-size buffer is undefined.

#### Metadata

##### Metadata emit and code generation

For metadata encoding compiler will rely on recently added [```System.Runtime.CompilerServices.InlineArrayAttribute```](https://github.com/dotnet/runtime/issues/61135). 

Fixed-size buffers like:
``` C#
public partial class C
{
    public int buffer1[10];
    public readonly int buffer2[10];
}
```
will be emitted as  fields of a specially decorated struct type.

Equivalent C# code will be:

``` C#
public partial class C
{
    public Buffer10<int> buffer1;
    public readonly Buffer10<int> buffer2;
}

[System.Runtime.CompilerServices.InlineArray(10)]
public struct Buffer10<T>
{
    private T _element0;

    [UnscopedRef]
    public System.Span<T> AsSpan()
    {
        return System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref _element0, 10);
    }

    [UnscopedRef]
    public readonly System.ReadOnlySpan<T> AsReadOnlySpan()
    {
        return System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
                    ref System.Runtime.CompilerServices.Unsafe.AsRef(in _element0), 10);
    }
}
```

The actual naming conventions for the type and its members are TBD. The framework will likely include a set of predefined "buffer" types that cover
a limited set of buffer sizes. When a predefined type doesn't exist, compiler will synthesize it in the module being built. Names of the generated types
will be "speakable" in order to support consumption from other languages. 

A code generated for an access like: 
``` C#
public partial class C
{
    void M1(int val)
    {
        buffer1[1] = val;
    }

    int M2()
    {
        return buffer2[1];
    }
}
```

will be equivalent to:
``` C#
public partial class C
{
    void M1(int val)
    {
        buffer.AsSpan()[1] = val;
    }

    int M2()
    {
        return buffer2.AsReadOnlySpan()[1];
    }
}
```

##### Metadata import

When compiler imports a field declaration of type *T* and the following conditions are all met:
- *T* is a struct type decorated with the ```InlineArray``` attribute, and
- The first instance field declared within *T* has type *F*, and
- There is a ```public System.Span<F> AsSpan()``` within *T*, and 
- There is a ```public readonly System.ReadOnlySpan<T> AsReadOnlySpan()``` or ```public System.ReadOnlySpan<T> AsReadOnlySpan()``` within *T*. 

the field will be treated as C# fixed-size buffer with element type *F*. Otherwise, the field will be treated as a regular field of type *T*.


### Method or property group like approach in the language

One thought is to treat these members more like method groups, in that they aren't automatically a value in and of themselves,
but can be made into one if necessary. Here’s how that would work:
- Safe fixed-size buffer accesses have their own classification (just like e.g. method groups and lambdas)
- They can be indexed directly as a language operation (not via span types) to produce a variable (which is readonly
  if the buffer is in a readonly context, just the same as fields of a struct)
- They have implicit conversions-from-expression to ```Span<T>``` and ```ReadOnlySpan<T>```, but use of the former is an error if
  they are in a readonly context
- Their natural type is ```ReadOnlySpan<T>```, so that’s what they contribute if they participate in type inference (e.g., var, best-common-type or generic)

### C/C++ fixed-size buffers

C/C++ has a different notion of fixed-size buffers. For example, there is a notion of "zero-length fixed sized buffers",
which is often used as a way to indicate that the data is "variable length". It is not a goal of this proposal to be
able to interop with that.
