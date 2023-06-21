# Permit `stackalloc` in nested contexts

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Stack allocation

We modify the section *Stack allocation* ([§22.9](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/unsafe-code.md#229-stack-allocation)) of the C# language specification to relax the places when a `stackalloc` expression may appear. We delete

``` antlr
local_variable_initializer_unsafe
    : stackalloc_initializer
    ;

stackalloc_initializer
    : 'stackalloc' unmanaged_type '[' expression ']'
    ;
```

and replace them with

``` antlr
primary_no_array_creation_expression
    : stackalloc_initializer
    ;

stackalloc_initializer
    : 'stackalloc' unmanaged_type '[' expression? ']' array_initializer?
    | 'stackalloc' '[' expression? ']' array_initializer
    ;
```

Note that the addition of an *array_initializer* to *stackalloc_initializer* (and making the index expression optional) was an [extension in C# 7.3](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/stackalloc-array-initializers.md) and is not described here.

The *element type* of the `stackalloc` expression is the *unmanaged_type* named in the stackalloc expression, if any, or the common type among the elements of the *array_initializer* otherwise.

The type of the *stackalloc_initializer* with *element type* `K` depends on its syntactic context:
- If the *stackalloc_initializer* appears directly as the *local_variable_initializer* of a *local_variable_declaration* statement or a *for_initializer*, then its type is `K*`.
- Otherwise its type is `System.Span<K>`.

## Stackalloc Conversion

The *stackalloc conversion* is a new built-in implicit conversion from expression. When the type of a *stackalloc_initializer* is `K*`, there is an implicit *stackalloc conversion* from the *stackalloc_initializer* to the type `System.Span<K>`.
