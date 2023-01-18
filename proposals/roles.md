# Roles

This proposal is divided into three parts, all relating to extending existing types:
A. static extensions
B. roles and extensions with members
C. roles and extensions that implement interfaces

The syntax for a roles is as follows:

```antlr
type_declaration
    : role_declaration // add
    | extension_declaration // add
    | ...
    ;

role_declaration
    : modifier* 'role' identifier type_parameter_list? role_underlying_type type_parameter_constraints_clause* role_body
    ;

extension_declaration
    : modifier* 'extension' identifier type_parameter_list? role_underlying_type type_parameter_constraints_clause* role_body
    ;

role_underlying_type
    : ':' type
    : ':' type ',' interface_type_list
    ;

role_body
    : '{' role_member_declaration* '}'
    ;

role_member_declaration
    : constant_declaration
    | field_declaration
    | method_declaration
    | property_declaration
    | event_declaration
    | indexer_declaration
    | operator_declaration
    | type_declaration
    ;
```

## Phase A: Adding static constants, fields, methods and properties

In this first subset of the feature, the syntax is restricted to **extension_declaration**
and containing only **constant_declaration** members and static **field_declaration**, **method_declaration** and **property_declaration** members.
TODO: events?

### Extension type

An extension type is declared by a non-nested **extension_declaration**.  
The permitted modifiers on an extension type are `partial`, `unsafe` and the accessibility modifiers `public` and `internal`.  
Note that `static` is disallowed.  
The standard rules for modifiers apply (valid combination of access modifiers, no duplicates).  
The **role_underlying_type** type may not be `dynamic`, a pointer, a nullable reference, or an extension type.  
The **role_underlying_type** type must include all the type parameters from the extension type.  
The **role_underlying_type** may not include an **interface_type_list** (this is part of Phase C).
This declares a ref struct type. It inherits from type `System.ValueType`. It has `internal` declared accessibility by default.  
TODO: attributes?
TODO: emitted name?

### Extension type members

The extension type members may not use the `new` modifier.
Accessibility modifiers including `protected` are disallowed.
The extension type does not **inherit** members from its underlying type (which may be `sealed` or a struct), but
the lookup rules are modified to achieve a similar effect (see below).  
TODO how do we emit the relationship to underlying type?

#### Constants

Existing [rules for constants](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#144-constants) apply (so duplicates or the `static` modifier are disallowed).

#### Fields

A **field_declaration** in an **extension_declaration** shall explicitly include a `static` modifier.  
Otherwise, existing [rules for fields](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#145-fields) apply.  

#### Methods

A **method_declaration** in an **extension_declaration** shall explicitly include a `static` modifier.  
Parameters with the `this` modifier are disallowed.
Otherwise, existing [rules for methods](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#146-methods) apply.
In particular, a static method does not operate on a specific instance, and it is a compile-time error to refer to `this` in a static method.

#### Properties

A **property_declaration** in an **extension_declaration** shall explicitly include a `static` modifier.  
Otherwise, existing [rules for properties](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#147-properties) apply.

### Simple names

We modify the [simple names rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1174-simple-names) as follows:

The *simple_name* with identifier `I` is evaluated and classified as follows:
- ... the *simple_name* refers to that local variable, parameter or constant.
- ... the *simple_name* refers to that [generic method declaration's] type parameter.
- Otherwise, for each instance type `T`, starting with the instance type of the immediately enclosing type declaration and continuing with the instance type of each enclosing class or struct declaration (if any):
  - ... the *simple_name* refers to that [type declaration's] type parameter.
  - Otherwise, if a member lookup of `I` in `T` with `e` type arguments produces a match:
    - If `T` is the instance type of the immediately enclosing class or struct type and the lookup identifies one or more methods, the result is a method group with an associated instance expression of `this`. If a type argument list was specified, it is used in calling a generic method.
    - Otherwise, if `T` is the instance type of the immediately enclosing class or struct type, if the lookup identifies an instance member, and if the reference occurs within the *block* of an instance constructor, an instance method, or an instance accessor, the result is the same as a member access of the form `this.I`. This can only happen when `e` is zero.
    - Otherwise, the result is the same as a member access of the form `T.I` or `T.I<A₁, ..., Aₑ>`.
  - **Otherwise, if `T` is not an extension type and an ***extension member lookup*** of `I` for underlying type `T` with `e` type arguments produces a match:**
    ...
  - **Otherwise, if `T` is a role (only relevant in phase B) or extension type and a member lookup of `I` in underlying type `U` with `e` type arguments produces a match:**
    ...
- Otherwise, for each namespace `N`, starting with the namespace in which the *simple_name* occurs, continuing with each enclosing namespace (if any), and ending with the global namespace, the following steps are evaluated until an entity is located:
  ...
- Otherwise, the simple_name is undefined and a compile-time error occurs.

### Member access 

We modify the [member access rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1176-member-access) as follows:

- ... the result is that namespace.
- ... the result is that type constructed with the given type arguments.
- If `E` is classified as a type, if `E` is not a type parameter, and if a member lookup of `I` in `E` with `K` type parameters produces a match, then `E.I` is evaluated and classified as follows:  
  > *Note*: When the result of such a member lookup is a method group and `K` is zero, the method group can contain methods having type parameters. This allows such methods to be considered for type argument inferencing. *end note*
  - If `I` identifies a type, then the result is that type constructed with any given type arguments.
  - If `I` identifies one or more methods, then the result is a method group with no associated instance expression.
  - If `I` identifies a static property, then the result is a property access with no associated instance expression.
  - If `I` identifies a static field:
    - If the field is readonly and the reference occurs outside the static constructor of the class or struct in which the field is declared, then the result is a value, namely the value of the static field `I` in `E`.
    - Otherwise, the result is a variable, namely the static field `I` in `E`.
  - If `I` identifies a static event:
    - If the reference occurs within the class or struct in which the event is declared, and the event was declared without *event_accessor_declarations*, then `E.I` is processed exactly as if `I` were a static field.
    - Otherwise, the result is an event access with no associated instance expression.
  - If `I` identifies a constant, then the result is a value, namely the value of that constant.
  - If `I` identifies an enumeration member, then the result is a value, namely the value of that enumeration member.
  - Otherwise, `E.I` is an invalid member reference, and a compile-time error occurs.
- **If `E` is classified as a type, if `E` is not a type parameter or an extension type, and if an ***extension member lookup*** of `I` in `E` with `K` type parameters produces a match, then `E.I` is evaluated and classified as follows:**  
  ...
- **If `E` is classified as a role (only relevant in phase B) or extension type, and if a member lookup of `I` in underlying type `U` with `K` type parameters produces a match, then `E.I` is evaluated and classified as follows:** 
  ...
- If `E` is a property access, indexer access, variable, or value, the type of which is `T`, and a member lookup of `I` in `T` with `K` type arguments produces a match, then `E.I` is evaluated and classified as follows:
  ...
- **(only relevant in phase B) If `E` is a property access, indexer access, variable, or value, the type of which is `T`, where `T` is not a type parameter or a role type, and an **extension member lookup** of `I` in `T` with `K` type arguments produces a match, then `E.I` is evaluated and classified as follows:**
  ...
- **(only relevant in phase B) If `E` is a property access, indexer access, variable, or value, the type of which is `T`, where `T` is a role or extension type, and a member lookup of `I` in underlying type `U` with `K` type arguments produces a match, then `E.I` is evaluated and classified as follows:**
  ...
- Otherwise, an attempt is made to process `E.I` as an extension method invocation. If this fails, `E.I` is an invalid member reference, and a binding-time error occurs.

TODO one downside of this approach is that we stop once a method with proper name is found, even if it will be applicable. We could tweak the above rules (member access and simple names) by separating invocations. 

### Extension member lookup

Given an underlying type `U` and an identifier `I`, the objective is to find an extension member `X.I`, if possible.

An extension type `X` is compatible with given type `U` if:
- `X` is non-generic and its underlying type is `U`
- a possible type substitution on the type parameters of `X` yields underlying type `U`. We call the resulting substituted type `X` the "compatible substituted extension type"

We process as follows (TODO more details needed):
- Starting with the closest enclosing namespace declaration, continuing with each enclosing namespace declaration, and ending with the containing compilation unit, successive attempts are made to find a candidate set of extension members:
  - If the given namespace or compilation unit directly contains extension types, those will be considered first.
  - If namespaces imported by using-namespace directives in the given namespace or compilation unit directly contain extension types, those will be considered second.
- Check which extension types are compatible with the given underlying type `U` and collect resulting compatible substituted extension types.
- Perform member lookup for `I` in each compatible substituted extension type `X` (note this takes into account whether the member is invoked).
- Merge the results
- If the set is empty, proceed to the next enclosing namespace
- If the set consists of a single member that is not a method, then this member is the result of the lookup.
- Otherwise, if the set contains only methods, then this group of methods is the result of the lookup.
- Otherwise, the lookup is ambiguous, and a binding-time error occurs.
- If no candidate set is found in any enclosing namespace declaration or compilation unit, the result of the lookup is empty.

The preceding rules mean that extension members available in inner namespace declarations take precedence over extension members available in outer namespace declarations,
and that extension members declared directly in a namespace take precedence over extension members imported into that same namespace with a using namespace directive.

## B. Roles and extensions with members

In this second subset of the feature, the **role_declaration** becomes allowed
and non-static members become allowed.

### Role type

A role type is declared by a non-nested **role_declaration**.  
The permitted modifiers on an extension type are `partial`, `unsafe` and the accessibility modifiers `public` and `internal`.  
Note that `static` is disallowed.  
The standard rules for modifiers apply (valid combination of access modifiers, no duplicates).  
The **role_underlying_type** type may not be `dynamic`, a pointer, a nullable reference, a role (TODO), or an extension type.  

### Role and extension type members

The restrictions on modifiers from phase A remain (`new` and `protected` disallowed).  
Non-static members become allowed in phase B.  

#### Lookup rules

The simple names and member access rules from phase A section (above) take full effect, as role types now exist and  
extension types may have non-static members.  
TODO: the simple names rules find `object.ToString()` for `ToString()` in instance extension method, rather than `U.ToString()`. Can we improve on that?

#### Fields

A **field_declaration** in a **role_declaration** or **extension_declaration** shall explicitly include a `static` modifier.  

#### Methods

TODO
Non-static methods are emitted as static methods with an additional parameter with the type underlying the role or extension.  
TODO What if I have a static method `void M(UnderlyingType x)` and a non-static method `void M()`. How do avoid conflict in metadata? How do we avoid confusion in invocation?

#### Instance invocations

TODO
An additional argument is passed in.

## C. Roles and extensions that implement interfaces

