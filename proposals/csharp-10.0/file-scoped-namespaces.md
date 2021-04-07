## File Scoped Namespaces

- [ ] Proposed
- [ ] Prototype: Started
- [ ] Implementation: Started
- [ ] Specification: Started

### Summary

Allow a simpler format for the common case of file containing only one namespace in it.  This format is `namespace X.Y.Z;` (note the semicolon and lack of braces).  This allows for files like so:

```c#
namespace X.Y.Z;

using System;

class X
{
}
```

The semantics are that using the `namespace X.Y.Z;` form is equivalent to writing `namespace X.Y.Z { ... }` where the remainder of the file following the file-scoped namespace is in the `...` section of a standard namespace declaration.

### Motivation

Analysis of the C# ecosystem shows that around 99.7% (or more) files are all of either one of these forms:

```c#
namespace X.Y.Z
{
    // usings

    // types
}
```

or

```c#
// usings

namespace X.Y.Z
{
    // types
}
```

However, both these forms force the user to indent the majority of their code and add a fair amount of ceremony for what is effectively a very basic concept.  This affects clarity, uses horizontal and vertical space, and is often unsatisfying for users both used to C# and coming from other languages (which commonly have less ceremony here).

The primary goal of the feature therefore is to meet the needs of the majority of the ecosystem with less unnecessary boilerplate.

### Detailed design

This proposal takes the form of a diff to the existing https://github.com/dotnet/csharplang/blob/main/spec/namespaces.md#compilation-units section of the specification.

#### Diff

~~A *compilation_unit* defines the overall structure of a source file. A compilation unit consists of zero or more *using_directive*s followed by zero or more *global_attributes* followed by zero or more *namespace_member_declaration*s.~~

A *compilation_unit* defines the overall structure of a source file. A compilation unit consists of zero or more *using_directive*s followed by zero or more *global_attributes* followed by a *compilation_unit_body*. A *compilation_unit_body* can either be a *file_scoped_namespace_declaration* or zero or more *statement*s and *namespace_member_declaration*s.

```antlr
compilation_unit
~~    : extern_alias_directive* using_directive* global_attributes? namespace_member_declaration*~~
    : extern_alias_directive* using_directive* global_attributes? compilation_unit_body
    ;

compilation_unit_body
    : statement* namespace_member_declaration*
    | file_scoped_namespace_declaration
    ;
```

... unchanged ...

A *file_scoped_namespace_declaration* will contribute members corresponding to the *namespace_declaration* it is semantically equivalent to.  See ([Namespace Declarations](#namespace-declarations)) for more details.

## Namespace declarations

A *namespace_declaration* consists of the keyword `namespace`, followed by a namespace name and body, optionally followed by a semicolon.
A *file_scoped_namespace_declaration* consists of the keyword `namespace`, followed by a namespace name, a semicolon and an optional list of *extern_alias_directive*s, *using_directive*s and *type_declaration*s.

```antlr
namespace_declaration
    : 'namespace' qualified_identifier namespace_body ';'?
    ;
    
file_scoped_namespace_declaration
    : 'namespace' qualified_identifier ';' extern_alias_directive* using_directive* type_declaration*
    ;

... unchanged ...
```

... unchanged ...

the two namespace declarations above contribute to the same declaration space, in this case declaring two classes with the fully qualified names `N1.N2.A` and `N1.N2.B`. Because the two declarations contribute to the same declaration space, it would have been an error if each contained a declaration of a member with the same name.

A *file_scoped_namespace_declaration* permits a namespace declaration to be written without the `{ ... }` block.  For example:

```csharp
extern alias A;
namespace Name;
using B;
class C
{
}
```

is semantically equivalent to

```csharp
extern alias A;
namespace Name
{
    using B;
    class C
    {
    }
}
```

Specifically, a *file_scoped_namespace_declaration* is treated the same as a *namespace_declaration* at the same location in the *compilation_unit* with the same *qualified_identifier*.  The *extern_alias_directive*s, *using_directive*s and *type_declaration*s of that *file_scoped_namespace_declaration* act as if they were declared in the same order inside the *namespace_body* of that *namespace_declaration*.

A source file cannot contain both a *file_scoped_namespace_declaration* and a *namespace_declaration*.  A source file cannot contain multiple *file_scoped_namespace_declaration*s. A *compilation_unit* cannot contain both a *file_scoped_namespace_declaration* and any top level *statement*s. *type_declaration*s cannot precede a *file_scoped_namespace_declaration*.  

## Extern aliases

... unchanged ...
