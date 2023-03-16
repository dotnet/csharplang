Safe Fixed Size Buffers
=====

## Summary

Provide a general-purpose and safe mechanism for declaring fixed sized buffers within C# classes, structs and interfaces.

## Motivation

This proposal plans to address the many limitations of https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/unsafe-code.md#228-fixed-size-buffers.
Specifically it aims to allow the declaration of safe `fixed` buffers for managed and unmanaged types in a `struct`, `class`, or `interface`.
And provide language safety verification for them.

## Detailed Design 

Grammar for `variable_declarator` in https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#145-fields
will be extended to allow specifying the size of the buffer:

``` antlr
variable_declarator
    : identifier ('=' variable_initializer)?
    | fixed_size_buffer_declarator
    ;
    
fixed_size_buffer_declarator
    : identifier '[' constant_expression ']'
    ;    
```

A `volatile` modifier cannot be used with `fixed_size_buffer_declarator`.

