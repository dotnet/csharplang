# Global Using Directive

Syntax for a using directive is extended with an optional `global` keyword that can precede the `using` keyword:
```antlr
compilation_unit
    : extern_alias_directive* global_using_directive* using_directive* global_attributes? namespace_member_declaration*
    ;

global_using_directive
    : global_using_alias_directive
    | global_using_namespace_directive
    | global_using_static_directive
    ;

global_using_alias_directive
    : 'global' 'using' identifier '=' namespace_or_type_name ';'
    ;

global_using_namespace_directive
    : 'global' 'using' namespace_name ';'
    ;
    
global_using_static_directive
    : 'global' 'using' 'static' type_name ';'
    ;
```

- The *global_using_directive*s are allowed only on the Compilation Unit level (cannot be used inside a *namespace_declaration*).
- The *global_using_directive*s, if any, must precede any *using_directive*s. 
- The scope of a *global_using_directive*s extends over the *namespace_member_declaration*s of all compilation units within the program.
The scope of a *global_using_directive* specifically does not include other *global_using_directive*s. Thus, peer *global_using_directive*s or those from a different
compilation unit do not affect each other, and the order in which they are written is insignificant.
The scope of a *global_using_directive* specifically does not include *using_directive*s immediately contained in any compilation unit of the program.

The effect of adding a *global_using_directive* to a program can be thought of as the effect of adding a similar *using_directive* that resolves to the same target namespace or type to every compilation unit of the program. However, the target of a *global_using_directive* is resolved in context of the compilation unit that contains it. 

# Scopes 
https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#scopes

These are the relevant bullet points with proposed additions (which are **in bold**):
*  The scope of name defined by an *extern_alias_directive* extends over the ***global_using_directive*s,** *using_directive*s, *global_attributes* and *namespace_member_declaration*s of its immediately containing compilation unit or namespace body. An *extern_alias_directive* does not contribute any new members to the underlying declaration space. In other words, an *extern_alias_directive* is not transitive, but, rather, affects only the compilation unit or namespace body in which it occurs.
*  **The scope of a name defined or imported by a *global_using_directive* extends over the *global_attributes* and *namespace_member_declaration*s of all the *compilation_unit*s in the program.**

# Namespace and type names
https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#namespace-and-type-names

Changes are made to the algorithm determining the meaning of a *namespace_or_type_name* as follows.

This is the relevant bullet point with proposed additions (which are **in bold**):
*   If the *namespace_or_type_name* is of the form `I` or of the form `I<A1, ..., Ak>`:
    * If `K` is zero and the *namespace_or_type_name* appears within a generic method declaration ([Methods](classes.md#methods)) and if that declaration includes a type parameter ([Type parameters](classes.md#type-parameters)) with name `I`, then the *namespace_or_type_name* refers to that type parameter.
    * Otherwise, if the *namespace_or_type_name* appears within a type declaration, then for each instance type `T` ([The instance type](classes.md#the-instance-type)), starting with the instance type of that type declaration and continuing with the instance type of each enclosing class or struct declaration (if any):
        * If `K` is zero and the declaration of `T` includes a type parameter with name `I`, then the *namespace_or_type_name* refers to that type parameter.
        * Otherwise, if the *namespace_or_type_name* appears within the body of the type declaration, and `T` or any of its base types contain a nested accessible type having name `I` and `K` type parameters, then the *namespace_or_type_name* refers to that type constructed with the given type arguments. If there is more than one such type, the type declared within the more derived type is selected. Note that non-type members (constants, fields, methods, properties, indexers, operators, instance constructors, destructors, and static constructors) and type members with a different number of type parameters are ignored when determining the meaning of the *namespace_or_type_name*.
    * If the previous steps were unsuccessful then, for each namespace `N`, starting with the namespace in which the *namespace_or_type_name* occurs, continuing with each enclosing namespace (if any), and ending with the global namespace, the following steps are evaluated until an entity is located:
        * If `K` is zero and `I` is the name of a namespace in `N`, then:
            * If the location where the *namespace_or_type_name* occurs is enclosed by a namespace declaration for `N` and the namespace declaration contains an *extern_alias_directive* or *using_alias_directive* that associates the name `I` with a namespace or type, **or any namespace declaration for `N` in the program contains a *global_using_alias_directive* that associates the name `I` with a namespace or type,** then the *namespace_or_type_name* is ambiguous and a compile-time error occurs.
            * Otherwise, the *namespace_or_type_name* refers to the namespace named `I` in `N`.
        * Otherwise, if `N` contains an accessible type having name `I` and `K` type parameters, then:
            * If `K` is zero and the location where the *namespace_or_type_name* occurs is enclosed by a namespace declaration for `N` and the namespace declaration contains an *extern_alias_directive* or *using_alias_directive* that associates the name `I` with a namespace or type, **or any namespace declaration for `N` in the program contains a *global_using_alias_directive* that associates the name `I` with a namespace or type,** then the *namespace_or_type_name* is ambiguous and a compile-time error occurs.
            * Otherwise, the *namespace_or_type_name* refers to the type constructed with the given type arguments.
        * Otherwise, if the location where the *namespace_or_type_name* occurs is enclosed by a namespace declaration for `N`:
            * If `K` is zero and the namespace declaration contains an *extern_alias_directive* or *using_alias_directive* that associates the name `I` with an imported namespace or type, **or any namespace declaration for `N` in the program contains a *global_using_alias_directive* that associates the name `I` with an imported namespace or type,** then the *namespace_or_type_name* refers to that namespace or type.
            * Otherwise, if the namespaces and type declarations imported by the *using_namespace_directive*s and *using_alias_directive*s of the namespace declaration **and the namespaces and type declarations imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program** contain exactly one accessible type having name `I` and `K` type parameters, then the *namespace_or_type_name* refers to that type constructed with the given type arguments.
            * Otherwise, if the namespaces and type declarations imported by the *using_namespace_directive*s and *using_alias_directive*s of the namespace declaration **and the namespaces and type declarations imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program** contain more than one accessible type having name `I` and `K` type parameters, then the *namespace_or_type_name* is ambiguous and an error occurs.
    * Otherwise, the *namespace_or_type_name* is undefined and a compile-time error occurs.

# Simple names
https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#simple-names

Changes are made to the *simple_name* evaluation rules as follows.

This is the relevant bullet point with proposed additions (which are **in bold**):
*  Otherwise, for each namespace `N`, starting with the namespace in which the *simple_name* occurs, continuing with each enclosing namespace (if any), and ending with the global namespace, the following steps are evaluated until an entity is located:
   *  If `K` is zero and `I` is the name of a namespace in `N`, then:
      * If the location where the *simple_name* occurs is enclosed by a namespace declaration for `N` and the namespace declaration contains an *extern_alias_directive* or *using_alias_directive* that associates the name `I` with a namespace or type, **or any namespace declaration for `N` in the program contains a *global_using_alias_directive* that associates the name `I` with a namespace or type,** then the *simple_name* is ambiguous and a compile-time error occurs.
      * Otherwise, the *simple_name* refers to the namespace named `I` in `N`.
   *  Otherwise, if `N` contains an accessible type having name `I` and `K` type parameters, then:
      * If `K` is zero and the location where the *simple_name* occurs is enclosed by a namespace declaration for `N` and the namespace declaration contains an *extern_alias_directive* or *using_alias_directive* that associates the name `I` with a namespace or type, **or any namespace declaration for `N` in the program contains a *global_using_alias_directive* that associates the name `I` with a namespace or type,** then the *simple_name* is ambiguous and a compile-time error occurs.
      * Otherwise, the *namespace_or_type_name* refers to the type constructed with the given type arguments.
   *  Otherwise, if the location where the *simple_name* occurs is enclosed by a namespace declaration for `N`:
      * If `K` is zero and the namespace declaration contains an *extern_alias_directive* or *using_alias_directive* that associates the name `I` with an imported namespace or type, **or any namespace declaration for `N` in the program contains a *global_using_alias_directive* that associates the name `I` with an imported namespace or type,** then the *simple_name* refers to that namespace or type.
      * Otherwise, if the namespaces and type declarations imported by the *using_namespace_directive*s and *using_static_directive*s of the namespace declaration **and the namespaces and type declarations imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program** contain exactly one accessible type or non-extension static member having name `I` and `K` type parameters, then the *simple_name* refers to that type or member constructed with the given type arguments.
      * Otherwise, if the namespaces and types imported by the *using_namespace_directive*s of the namespace declaration **and the namespaces and type declarations imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program** contain more than one accessible type or non-extension-method static member having name `I` and `K` type parameters, then the *simple_name* is ambiguous and an error occurs.

# Extension method invocations
https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#extension-method-invocations

Changes are made to the algorithm to find the best *type_name* `C` as follows.
This is the relevant bullet point with proposed additions (which are **in bold**):
*  Starting with the closest enclosing namespace declaration, continuing with each enclosing namespace declaration, and ending with the containing compilation unit, successive attempts are made to find a candidate set of extension methods:
   * If the given namespace or compilation unit directly contains non-generic type declarations `Ci` with eligible extension methods `Mj`, then the set of those extension methods is the candidate set.
   * If types `Ci` imported by *using_static_declarations* and directly declared in namespaces imported by *using_namespace_directive*s in the given namespace or compilation unit **and, if containing compilation unit is reached, imported by *global_using_static_declarations* and directly declared in namespaces imported by *global_using_namespace_directive*s in the program** directly contain eligible extension methods `Mj`, then the set of those extension methods is the candidate set.

# Compilation units
https://github.com/dotnet/csharplang/blob/master/spec/namespaces.md#compilation-units

A *compilation_unit* defines the overall structure of a source file. A compilation unit consists of **zero or more *global_using_directive*s followed by** zero or more *using_directive*s followed by zero or more *global_attributes* followed by zero or more *namespace_member_declaration*s.

```antlr
compilation_unit
    : extern_alias_directive* global_using_directive* using_directive* global_attributes? namespace_member_declaration*
    ;
```

A C# program consists of one or more compilation units, each contained in a separate source file. When a C# program is compiled, all of the compilation units are processed together. Thus, compilation units can depend on each other, possibly in a circular fashion.

The *global_using_directive*s of a compilation unit affect the *global_attributes* and *namespace_member_declaration*s of all compilation units in the program.

# Extern aliases
https://github.com/dotnet/csharplang/blob/master/spec/namespaces.md#extern-aliases

The scope of an *extern_alias_directive* extends over the ***global_using_directive*s,** *using_directive*s, *global_attributes* and *namespace_member_declaration*s of its immediately containing compilation unit or namespace body.

# Using alias directives
https://github.com/dotnet/csharplang/blob/main/spec/namespaces.md#using-alias-directives

The order in which *using_alias_directive*s are written has no significance, and resolution of the *namespace_or_type_name* referenced by a *using_alias_directive* is not affected by the *using_alias_directive* itself or by other *using_directive*s in the immediately containing compilation unit or namespace body, **and, if the *using_alias_directive* is immediately contained in a compilation unit, is not affected by the *global_using_directive*s in the program**. In other words, the *namespace_or_type_name* of a *using_alias_directive* is resolved as if the immediately containing compilation unit or namespace body had no *using_directive*s **and, if the *using_alias_directive* is immediately contained in a compilation unit, the program had no *global_using_directive*s**. A *using_alias_directive* may however be affected by *extern_alias_directive*s in the immediately containing compilation unit or namespace body.

# Global Using alias directives

A *global_using_alias_directive* introduces an identifier that serves as an alias for a namespace or type within the program.

```antlr
global_using_alias_directive
    : 'global' 'using' identifier '=' namespace_or_type_name ';'
    ;
```

Within member declarations in any compilation unit of a program that contains a *global_using_alias_directive*, the identifier introduced by the *global_using_alias_directive* can be used to reference the given namespace or type.

The *identifier* of a *global_using_alias_directive* must be unique within the declaration space of any compilation unit of a program that contains the *global_using_alias_directive*.

Just like regular members, names introduced by *global_using_alias_directive*s are hidden by similarly named members in nested scopes.

The order in which *global_using_alias_directive*s are written has no significance, and resolution of the *namespace_or_type_name* referenced by a *global_using_alias_directive* is not affected by the *global_using_alias_directive* itself or by other *global_using_directive*s or *using_directive*s in the program. In other words, the *namespace_or_type_name* of a *global_using_alias_directive* is resolved as if the immediately containing compilation unit had no *using_directive*s and the entire containig program had no *global_using_directive*s. A *global_using_alias_directive* may however be affected by *extern_alias_directive*s in the immediately containing compilation unit.

A *global_using_alias_directive* can create an alias for any namespace or type.

Accessing a namespace or type through an alias yields exactly the same result as accessing that namespace or type through its declared name.

Using aliases can name a closed constructed type, but cannot name an unbound generic type declaration without supplying type arguments.

# Global Using namespace directives

A *global_using_namespace_directive* imports the types contained in a namespace into the program, enabling the identifier of each type to be used without qualification.

```antlr
global_using_namespace_directive
    : 'global' 'using' namespace_name ';'
    ;
```

Within member declarations in a program that contains a *global_using_namespace_directive*, the types contained in the given namespace can be referenced directly.

A *global_using_namespace_directive* imports the types contained in the given namespace, but specifically does not import nested namespaces.

Unlike a *global_using_alias_directive*, a *global_using_namespace_directive* may import types whose identifiers are already defined within a compilation unit of the program. In effect, in a given compilation unit, names imported by any *global_using_namespace_directive* in the program are hidden by similarly named members in the compilation unit.

When more than one namespace or type imported by *global_using_namespace_directive*s or *global_using_static_directive*s in the same program contain types by the same name, references to that name as a *type_name* are considered ambiguous.

Furthermore, when more than one namespace or type imported by *global_using_namespace_directive*s or *global_using_static_directive*s in the same program contain types or members by the same name, references to that name as a *simple_name* are considered ambiguous.

The *namespace_name* referenced by a *global_using_namespace_directive* is resolved in the same way as the *namespace_or_type_name* referenced by a *global_using_alias_directive*. Thus, *global_using_namespace_directive*s in the same program do not affect each other and can be written in any order.


# Global Using static directives

A *global_using_static_directive* imports the nested types and static members contained directly in a type declaration into the containing program, enabling the identifier of each member and type to be used without qualification.

```antlr
global_using_static_directive
    : 'global' 'using' 'static' type_name ';'
    ;
```

Within member declarations in a program that contains a *global_using_static_directive*, the accessible nested types and static members (except extension methods) contained directly in the declaration of the given type can be referenced directly.

A *global_using_static_directive* specifically does not import extension methods directly as static methods, but makes them available for extension method invocation.

A *global_using_static_directive* only imports members and types declared directly in the given type, not members and types declared in base classes.

Ambiguities between multiple *global_using_namespace_directive*s and *global_using_static_directives* are discussed in the section for *global_using_namespace_directive*s (above).

# Namespace alias qualifiers
https://github.com/dotnet/csharplang/blob/master/spec/namespaces.md#namespace-alias-qualifiers

Changes are made to the algorithm determining the meaning of a *qualified_alias_member* as follows.

This is the relevant bullet point with proposed additions (which are **in bold**):
*  Otherwise, starting with the namespace declaration ([Namespace declarations](namespaces.md#namespace-declarations)) immediately containing the *qualified_alias_member* (if any), continuing with each enclosing namespace declaration (if any), and ending with the compilation unit containing the *qualified_alias_member*, the following steps are evaluated until an entity is located:

   * If the namespace declaration or compilation unit contains a *using_alias_directive* that associates `N` with a type, **or, when a compilation unit is reached, the program contains a *global_using_alias_directive* that associates `N` with a type,** then the *qualified_alias_member* is undefined and a compile-time error occurs.
   * Otherwise, if the namespace declaration or compilation unit contains an *extern_alias_directive* or *using_alias_directive* that associates `N` with a namespace, ***or, when a compilation unit is reached, the program contains a *global_using_alias_directive* that associates `N` with a namespace,** then:
     * If the namespace associated with `N` contains a namespace named `I` and `K` is zero, then the *qualified_alias_member* refers to that namespace.
     * Otherwise, if the namespace associated with `N` contains a non-generic type named `I` and `K` is zero, then the *qualified_alias_member* refers to that type.
     * Otherwise, if the namespace associated with `N` contains a type named `I` that has `K` type parameters, then the *qualified_alias_member* refers to that type constructed with the given type arguments.
     * Otherwise, the *qualified_alias_member* is undefined and a compile-time error occurs.
