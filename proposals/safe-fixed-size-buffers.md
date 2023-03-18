Safe Fixed Size Buffers
=====

## Summary

Provide a general-purpose and safe mechanism for declaring fixed sized buffers within C# classes, structs, and interfaces.

## Motivation

This proposal plans to address the many limitations of https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#228-fixed-size-buffers.
Specifically it aims to allow the declaration of safe `fixed` buffers for managed and unmanaged types in a `struct`, `class`, or `interface`, and provide language safety verification for them.

## Detailed Design 

The grammar for *variable_declarator* in https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#145-fields
will be extended to allow specifying the size of the buffer:

``` antlr
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
    | fixed_size_buffer_declarator
    ;
    
fixed_size_buffer_declarator
    : identifier '[' constant_expression ']'
    ;    
```

A *fixed_size_buffer_declarator* introduces a fixed-size buffer of a given element type.

The buffer element type is the *type* specified in `field_declaration`. A fixed-size buffer declarator introduces a new member and consists of an identifier that names the member, followed by a constant expression enclosed in `[` and `]` tokens. The constant expression denotes the number of elements in the member introduced by that fixed-size buffer declarator. The type of the constant expression must be implicitly convertible to type `int`, and the value must be a non-zero positive integer.

The elements of a fixed-size buffer shall be laid out sequentially in memory as though they are elements of an array.

A *field_declaration* with a *fixed_size_buffer_declarator* cannot have the `volatile` modifier, and it cannot be marked with a `System.ThreadStaticAttribute`.
A *field_declaration* with a *fixed_size_buffer_declarator* in an interface must have `static` modifier.

Depending on the situation (details are specified below), an access to a fixed-size buffer member is classified as a value (never a variable) of either
`System.ReadOnlySpan<S>` or `System.Span<S>`, where S is the element type of the fixed-size buffer. Both types provide indexers returning a reference to a
specific element with appropriate "readonly-ness", which prevents direct assignment to the elements when language rules don't permit that.

This limits the set of types that can be used as a fixed-size buffer element type to types that can be used as type arguments. For example, a pointer type cannot be used as an element type.

The resulting span instance will have a length equal to the size declared on the fixed-size buffer.
Indexing into the span with a constant expression outside of the declared fixed-size buffer bounds is a compile time error.

The *safe-to-escape* scope of the value will be equal to the *safe-to-escape* scope of the container, just as it would if the backing data was accessed as a field.

### Fixed-size buffers in expressions

Member lookup of a fixed-size buffer member proceeds exactly like member lookup of a field.

A fixed-size buffer can be referenced in an expression using a *simple_name* or a *member_access* .

When an instance fixed-size buffer member is referenced as a simple name, the effect is the same as a member access of the form `this.I`, where `I` is the fixed-size buffer member. When a static fixed-size buffer member is referenced as a simple name, the effect is the same as a member access of the form `E.I`, where `I` is the fixed-size buffer member and `E` is the declaring type.

#### Non-readonly fixed-size buffers

In a member access of the form `E.I`, if `E` is of a struct type and a member lookup of `I` in that struct type identifies an instance fixed-size member,
then `E.I` is evaluated and classified as follows:

- If `E` is classified as a value, the result of the expression is classified as a value of type `System.ReadOnlySpan<S>`, where S is the element type of `I`.
  The value can be used to access members’ elements.
- Otherwise, `E` is classified as a variable and the result of the expression is classified as a value of type `System.Span<S>`, where S is the element type of `I`.
  The value can be used to access members’ elements.

In a member access of the form `E.I`, if `E` is of a class type and a member lookup of `I` in that class type identifies an instance fixed-size member,
then `E.I` is evaluated and classified as a value of type `System.Span<S>`, where S is the element type of `I`.

In a member access of the form `E.I`, if member lookup of `I` identifies a static fixed-size member,
then `E.I` is evaluated and classified as a value of type `System.Span<S>`, where S is the element type of `I`.

#### Readonly fixed-size buffers

When a *field_declaration* includes a `readonly` modifier, the member introduced by the fixed_size_buffer_declarator is a ***readony fixed-size buffer***.
Direct assignments to elements of a readonly fixed-size buffer can only occur in an instance constructor, init member or static constructor in the same type.
Specifically, direct assignments to an element of readonly fixed-size buffer are permitted only in the following contexts:

- For an instance member, in the instance constructors or init member of the type that contains the member declaration; for a static member,
  in the static constructor of the type that contains the member declaration. These are also the only contexts in which it is valid to pass
  an element of readonly fixed-size buffer as an `out` or `ref` parameter.

Attempting to assign to an element of a readonly fixed-size buffer or pass it as an `out` or `ref` parameter in any other context is a compile-time error.
This is achieved by the following.

A member access for a readonly fixed-size buffer is evaluated and classified as follows:

- If access occurs in a context where direct assignments to an element of readonly fixed-size buffer are permitted, the result of the expression is classified as a value of type `System.Span<S>`, where S is the element type of the fixed-size buffer.
  The value can be used to access members’ elements.
- Otherwise, the expression is classified as a value of type `System.ReadOnlySpan<S>`, where S is the element type of the fixed-size buffer.
  The value can be used to access members’ elements.

### Definite assignment checking

Fixed-size buffers are not subject to definite assignment-checking, and fixed-size buffer members are ignored for purposes of definite-assignment checking of struct type variables.

When a fixed-size buffer member is static or the outermost containing struct variable of a fixed-size buffer member is a static variable, an instance variable of a class instance, or an array element, the elements of the fixed-size buffer are automatically initialized to their default values. In all other cases, the initial content of a fixed-size buffer is undefined.

### Metadata

#### Metadata emit and code generation

For metadata encoding compiler will rely on recently added [```System.Runtime.CompilerServices.InlineArrayAttribute```](https://github.com/dotnet/runtime/issues/61135). 

Fixed-size buffers like:
``` C#
public struct C
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
        // Note, an attempt to compile reports error CS1605: Cannot use '_element0' as a ref or out value because it is read-only
        return System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref _element0, 10);  
    }
}
```

The actual naming conventions for the type and its members are TBD. Framework will likely to include a set of predefined "buffer" types that cover
limited set of buffer sizes. When predefined type doesn't exist, compiler will synthesize it in the module being built. Names of the generated types
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

#### Metadata import

When compiler imports a field declaration of type *T* and the following conditions are all met:
- *T* is a struct type decorated with the ```InlineArray``` attribute, and
- The first instance field declared within *T* has type *F*, and
- There is a ```public System.Span<F> AsSpan()``` within *T*, and 
- There is a ```public readonly System.ReadOnlySpan<T> AsReadOnlySpan()``` or ```public System.ReadOnlySpan<T> AsReadOnlySpan()``` within *T*. 

the field will be treated as C# fixed-size buffer with element type *F*. Otherwise, the field will be treated as a regular field of type *T*.

## Open design questions

### Initializer

Should we support initialization at declaration site with, perhaps, [collection literals](https://github.com/dotnet/csharplang/blob/main/proposals/collection-literals.md)?

## Alternatives

### Method or property group like approach in the language

One thought is to treat these members more like method groups, in that they aren't automatically a value in and of themselves,
but can be made into one if necessary. Here’s how that would work:
- Safe fixed-size buffer accesses have their own classification (just like e.g. method groups and lambdas)
- They can be indexed directly as a language operation (not via span types) to produce a variable (which is readonly
  if the buffer is in a readonly context, just the same as fields of a struct)
- They have implicit conversions-from-expression to ```Span<T>``` and ```ReadOnlySpan<T>```, but use of the former is an error if
  they are in a readonly context
- Their natural type is ```ReadOnlySpan<T>```, so that’s what they contribute if they participate in type inference (e.g., var, best-common-type or generic)

### C/C++ fixed-size bufers

C/C++ has a different notion of fixed-size bufers. For example, there is a notion of "zero-length fixed sized buffers",
which is often used as a way to indicate that the data is "variable length". It is not a goal of this proposal to be
able to interop with that.
