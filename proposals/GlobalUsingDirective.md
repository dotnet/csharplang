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
- The scope of a *global_using_directive*s extends over the namespace member declarations and *using_directive*s of all compilation units within the program.
The scope of a *global_using_directive* specifically does not include other *global_using_directive*s. Thus, peer *global_using_directive*s or those from a different
compilation unit do not affect each other, and the order in which they are written is insignificant.

# Scopes 
https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#scopes

This are the relevant bullet points with proposed additions (which are **in bold**):
*  The scope of name defined by an *extern_alias_directive* extends over the ***global_using_directive*s,** *using_directive*s, *global_attributes* and *namespace_member_declaration*s of its immediately containing compilation unit or namespace body. An *extern_alias_directive* does not contribute any new members to the underlying declaration space. In other words, an *extern_alias_directive* is not transitive, but, rather, affects only the compilation unit or namespace body in which it occurs.
*  **The scope of a name defined or imported by a *global_using_directive* extends over the *using_directive*s, *global_attributes* and *namespace_member_declaration*s of all the *compilation_unit*s in the program.**

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
            * Otherwise, if the namespaces and type declarations imported by the *using_namespace_directive*s and *using_alias_directive*s of the namespace declaration contain exactly one accessible type having name `I` and `K` type parameters, then the *namespace_or_type_name* refers to that type constructed with the given type arguments.
            * Otherwise, if the namespaces and type declarations imported by the *using_namespace_directive*s and *using_alias_directive*s of the namespace declaration contain more than one accessible type having name `I` and `K` type parameters, then the *namespace_or_type_name* is ambiguous and an error occurs.
            * **Otherwise, if the namespaces and type declarations imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program contain exactly one accessible type having name `I` and `K` type parameters, then the *namespace_or_type_name* refers to that type constructed with the given type arguments.**
            * **Otherwise, if the namespaces and types imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program contain more than one accessible typehaving name `I` and `K` type parameters, then the *namespace_or_type_name* is ambiguous and an error occurs.**
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
      * Otherwise, if the namespaces and type declarations imported by the *using_namespace_directive*s and *using_static_directive*s of the namespace declaration contain exactly one accessible type or non-extension static member having name `I` and `K` type parameters, then the *simple_name* refers to that type or member constructed with the given type arguments.
      * Otherwise, if the namespaces and types imported by the *using_namespace_directive*s of the namespace declaration contain more than one accessible type or non-extension-method static member having name `I` and `K` type parameters, then the *simple_name* is ambiguous and an error occurs.
      * **Otherwise, if the namespaces and type declarations imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program contain exactly one accessible type or non-extension static member having name `I` and `K` type parameters, then the *simple_name* refers to that type or member constructed with the given type arguments.**
      * **Otherwise, if the namespaces and types imported by the *global_using_namespace_directive*s and *global_using_static_directive*s of any namespace declaration for `N` in the program contain more than one accessible type or non-extension-method static member having name `I` and `K` type parameters, then the *simple_name* is ambiguous and an error occurs.**

# Extension method invocations
https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#extension-method-invocations

Changes are made to the algorithm to find the best *type_name* `C` as follows.
This is the relevant bullet point with proposed additions (which are **in bold**):
*  Starting with the closest enclosing namespace declaration, continuing with each enclosing namespace declaration, and ending with the containing compilation unit, successive attempts are made to find a candidate set of extension methods:
   * If the given namespace or compilation unit directly contains non-generic type declarations `Ci` with eligible extension methods `Mj`, then the set of those extension methods is the candidate set.
   * If types `Ci` imported by *using_static_declarations* and directly declared in namespaces imported by *using_namespace_directive*s in the given namespace or compilation unit directly contain eligible extension methods `Mj`, then the set of those extension methods is the candidate set.
   * **If containing compilation unit is reached and if types `Ci` imported by *global_using_static_declarations* and directly declared in namespaces imported by *global_using_namespace_directive*s in the program directly contain eligible extension methods `Mj`, then the set of those extension methods is the candidate set.**

# Compilation units
https://github.com/dotnet/csharplang/blob/master/spec/namespaces.md#compilation-units

```antlr
compilation_unit
    : extern_alias_directive* global_using_directive* using_directive* global_attributes? namespace_member_declaration*
    ;
```

The *global_using_directive*s of a compilation unit affect the *using_directive*s, *global_attributes* and *namespace_member_declaration*s of all compilation units in the program.

# Extern aliases
https://github.com/dotnet/csharplang/blob/master/spec/namespaces.md#extern-aliases

The scope of an *extern_alias_directive* extends over the ***global_using_directive*s,** *using_directive*s, *global_attributes* and *namespace_member_declaration*s of its immediately containing compilation unit or namespace body.


