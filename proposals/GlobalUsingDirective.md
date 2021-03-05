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

- The *global_using_directive*s are allowed only on the Compilation Unit level (cannot be used inside a namespace declaration).
- The *global_using_directive*s, if any, must precede any *using_directive*s. 
- The scope of a *global_using_directive*s extends over the namespace member declarations and *using_directive*s of all compilation units within the program.
The scope of a *global_using_directive* specifically does not include other *global_using_directive*s. Thus, peer *global_using_directive*s or those from a different
compilation unit do not affect each other, and the order in which they are written is insignificant.

# Scopes 
https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#scopes

This are the relevant bullet points with proposed additions (which are **in bold**):
*  The scope of name defined by an *extern_alias_directive* extends over the ***global_using_directive*s,** *using_directive*s, *global_attributes* and *namespace_member_declaration*s of its immediately containing compilation unit or namespace body. An *extern_alias_directive* does not contribute any new members to the underlying declaration space. In other words, an *extern_alias_directive* is not transitive, but, rather, affects only the compilation unit or namespace body in which it occurs.
*  **The scope of a name defined or imported by a *global_using_directive* extends over the *using_directive*s, *global_attributes* and *namespace_member_declaration*s of all the *compilation_unit*s in the program.**


# Simple names
https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#simple-names

The following changes are made to the *simple_name* evaluation rules as follows.

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
      * Otherwise, if the namespaces and type declarations imported by the *using_namespace_directive*s and *using_static_directive*s of the namespace declaration contain exactly one accessible type or non-extension static member having name `I` and `K` type parameters, then the *simple_name* refers to that type or member constructed with the given type arguments.
      * Otherwise, if the namespaces and types imported by the *using_namespace_directive*s of the namespace declaration contain more than one accessible type or non-extension-method static member having name `I` and `K` type parameters, then the *simple_name* is ambiguous and an error occurs.
      * **Otherwise, if the namespaces and type declarations imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program contain exactly one accessible type or non-extension static member having name `I` and `K` type parameters, then the *simple_name* refers to that type or member constructed with the given type arguments.**
      * **Otherwise, if the namespaces and types imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program contain more than one accessible type or non-extension-method static member having name `I` and `K` type parameters, then the *simple_name* is ambiguous and an error occurs.**


