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

A *fixed_size_buffer_declarator* introduces a fixed-size buffers of a given element type.

The buffer element type is the *type* specified in `field_declaration`. A fixed-size buffer declarator introduces a new member and consists of an identifier that names the member, followed by a constant expression enclosed in `[` and `]` tokens. The constant expression denotes the number of elements in the member introduced by that fixed-size buffer declarator. The type of the constant expression must be implicitly convertible to type `int`, and the value must be a non-zero positive integer.

The elements of a fixed-size buffer shall be laid out sequentially in memory as though they are elements of an array.

A *field_declaration* with a *fixed_size_buffer_declarator* cannot have the `volatile` modifier, and it cannot be marked with a `System.ThreadStaticAttribute`.

Depending on the situation (details are specified below), an access to a fixes-size buffer member is classified as a value (never a variable) of either
`System.ReadOnlySpan<S>` or `System.Span<S>`, where S is the element type of the fixed-size buffer. Both types provide indexers returning refernce to a
specific element with appropriate "readonly-ness". Which prevents direct assignment to the elements when language rules don't permit that.

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
