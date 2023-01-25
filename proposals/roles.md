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
    : modifier* 'role' identifier type_parameter_list? role_underlying_type? type_parameter_constraints_clause* role_body
    ;

extension_declaration
    : modifier* 'extension' identifier type_parameter_list? role_underlying_type? type_parameter_constraints_clause* role_body
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

TODO we may need to allow a role to be defined on top of multiple other roles. 
`role DiamondRole : NarrowerUnderlyingType, BaseRole1, Interface1, BaseRole2, Interface2 { }`
We should assume multiple inheritance for now. Will update syntax to allow multiple inherited roles.  
In that syntax, the first type will be the underlying type.  
`role R : I { } // First type is underlying type (not an implemented interface)`

TODO there are some open questions on extension syntax 
(who decides to turn a role into an extension?)

## Overview of role types 

We'll use "augments" for relationship to underlying type 
(comparable to "inherits" for relationship to base type).  
We'll use "inherits" for relationship to inherited roles 
(comparable to "implements" for relationship to implemented interfaces).  

```csharp
struct U { }
role X : U { }
role Y : U, X, X1 { }
```
"Y has underlying type U"  
"Y augments U"  
"Y inherits X and X1"  
"Derived role Y inherits members from inherited roles X and X1"  

Similarly, roles don't have a base type, but have base roles.    

`role R<T> : T where T : I1, I2 { }`
`role R<T> : T where T : INumber<T> { }`
A role may be a value or reference type, and this may not be known at compile-time. 

### Role type

A role type is declared by a non-nested *role_declaration*.  
The above rules from extension types apply, namely the permitted modifiers and rules on underlying type.

TODO: constructors?
TODO: what is the base type of a role? Do we tweak member lookup to avoid looking into `object`,
or do we say that role doesn't have `object` in its base chain?

### Extension type

An extension type is a role type (new kind of type) declared by an *extension_declaration*.  
The permitted modifiers on an extension type are `partial`, `unsafe`, `file` and 
the accessibility modifiers.  
Note that `static` is disallowed. The extension type is static if its underlying type
is static.  
The standard rules for modifiers apply (valid combination of access modifiers, no duplicates).  

The *role_underlying_type* type may not be `dynamic`, a pointer, a nullable reference (no top-level nullability), 
a ref struct type.  
The *role_underlying_type* type must include all the type parameters from the extension type.  
The *role_underlying_type* may not include an *interface_type_list* (this is part of Phase C).  

An extension declaration must include an underlying type, unless it is partial. 
It is a compile-time error if no part of a partial extension type includes an underlying type, 
or the underlying type differs amongst all the parts.  

When a partial extension declaration includes an accessibility specification, 
that specification shall agree with all other parts that include an accessibility specification. 
If no part of a partial extension includes an accessibility specification, 
the type is given the appropriate default accessibility (`internal`).

The underlying type of an extension type shall be at least as accessible as the extension type itself.  

A role type satisfies the constraints satisfied by its underlying type. In phase C,
some additional constraints can be satisfied (additional implemented interfaces).  

TODO slightly different meaning for `protected`  

## Implementation details

Roles will be implemented as ref structs.  

TODO what is a role type? what is its base type?  
TODO downlevel concerns (relates to disallowing `static` modifier)?  
TODO how do we emit an extension type versus a handcrafted ref struct?  
TODO how do we emit the relationship to underlying type? base type or special constructor?  
TODO our emit strategy should allow using pointer types and ref structs as underlying types
in the future.  

## Phase A: Adding static constants, fields, methods and properties

In this first subset of the feature, the syntax is restricted to *extension_declaration*
and containing only *constant_declaration* members and static *field_declaration*, 
*method_declaration*, *property_declaration* and *type_declaration* members.  
TODO: events?

### Constraints

TODO `struct`, `class`
`where T : Extension`, `where T : Role`
Disallow roles/extensions in type constraints for now. Is there an issue with struct?

### Extension type members

The extension type members may not use the `virtual` or `override` modifiers.  
The `new` modifier is allowed and the compiler will warn that you should
use `new` when shadowing.  
Shadowing includes underlying type and inherited roles.  

```
class U { public void M() { } }
role R : U { /*new*/ public void M() { } } // wins when dealing with an R
```

```
class U { public void M() { } }
extension X : U { /*new*/ public void M() { } } // ignored in some cases, but extension is a role so rule should apply anyways
U u;
u.M(); // U.M (ignored X.M)
X x;
x.M(); // X.M
```

```
class U { }
role R : U { public void M() { } }
role R2 : U, R { /*new*/ public void M() { } } // wins when dealing with an R2
```

The extension type does not **inherit** members from its underlying type 
(which may be `sealed` or a struct), but
the lookup rules are modified to achieve a similar effect (see below).  

#### Constants

Existing [rules for constants](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#144-constants) 
apply (so duplicates or the `static` modifier are disallowed).

#### Fields

A *field_declaration* in an *extension_declaration* shall explicitly include a `static` modifier.  
Otherwise, existing [rules for fields](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#145-fields) apply.  

#### Methods

A *method_declaration* in an *extension_declaration* shall explicitly include a `static` modifier.  
Parameters with the `this` modifier are disallowed.
Otherwise, existing [rules for methods](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#146-methods) apply.
In particular, a static method does not operate on a specific instance, 
and it is a compile-time error to refer to `this` in a static method.

#### Properties

A *property_declaration* in an *extension_declaration* shall explicitly include a `static` modifier.  
Otherwise, existing [rules for properties](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#147-properties) apply.
In particular, a static method does not operate on a specific instance, 
and it is a compile-time error to refer to `this` in a static method.

#### Nested types

TODO `UnderlyingType.NestedType` would find the `NestedType` from an extension.

#### Events

TODO

### Lookup rules

Will need to spec or disallow `base.` syntax.
Casting seems an adequate solution to access hidden members: `((R)r2).M()`.  
We may not need a syntax like `base(R).` which was brainstormed for some other features.

We want to ensure that both of these are possible:  
From an extension, need to access a hidden thing.  
From an underlying type, still need to access the extension member when extension loses.  

#### Simple names

We modify the [simple names rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1174-simple-names) as follows:

The *simple_name* with identifier `I` is evaluated and classified as follows:
- ... the *simple_name* refers to that local variable, parameter or constant.
- ... the *simple_name* refers to that [generic method declaration's] type parameter.
- Otherwise, for each instance type `T`, starting with the instance type of the 
  immediately enclosing type declaration and continuing with the instance type 
  of each enclosing class or struct declaration (if any):
  - ... the *simple_name* refers to that [type declaration's] type parameter.
  - Otherwise, if a member lookup of `I` in `T` with `e` type arguments produces a match:
    - If `T` is the instance type of the immediately enclosing class or struct 
      type and the lookup identifies one or more methods, the result is a method group 
      with an associated instance expression of `this`. 
      If a type argument list was specified, it is used in calling a generic method.
    - Otherwise, if `T` is the instance type of the immediately enclosing class or struct type, 
      if the lookup identifies an instance member, and if the reference occurs 
      within the *block* of an instance constructor, an instance method, or an instance accessor, 
      the result is the same as a member access of the form `this.I`. 
      This can only happen when `e` is zero.
    - Otherwise, the result is the same as a member access of the form `T.I` or `T.I<A₁, ..., Aₑ>`.
  - **Otherwise, if `T` is a role (only relevant in phase B) or extension type and 
    a member lookup of `I` in underlying type `U` with `e` type arguments produces a match:**  
    ...
- Otherwise, for each namespace `N`, starting with the namespace in which the *simple_name* occurs, 
  continuing with each enclosing namespace (if any), and ending with the global namespace, 
  the following steps are evaluated until an entity is located:  
  ...
- Otherwise, the simple_name is undefined and a compile-time error occurs.

TODO confirm we don't want extension type lookup here, since we didn't do 
any extension method lookup previously.

#### Member access

We modify the [member access rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1176-member-access) as follows:

- ... the result is that namespace.
- ... the result is that type constructed with the given type arguments.
- If `E` is classified as a type, if `E` is not a type parameter, and 
  if a member lookup of `I` in `E` with `K` type parameters produces a match, 
  then `E.I` is evaluated and classified as follows:  
  > *Note*: When the result of such a member lookup is a method group and `K` is zero, 
    the method group can contain methods having type parameters. 
    This allows such methods to be considered for type argument inferencing. *end note*
  - If `I` identifies a type, then the result is that type constructed with any given type arguments.
  - If `I` identifies one or more methods, then the result is a method group with no associated instance expression.
  - If `I` identifies a static property, then the result is a property access with no associated instance expression.
  - If `I` identifies a static field:
    - If the field is readonly and the reference occurs outside the static constructor 
      of the class or struct in which the field is declared, then the result is a value, 
      namely the value of the static field `I` in `E`.
    - Otherwise, the result is a variable, namely the static field `I` in `E`.
  - If `I` identifies a static event:
    - If the reference occurs within the class or struct in which the event is declared, 
      and the event was declared without *event_accessor_declarations*, 
      then `E.I` is processed exactly as if `I` were a static field.
    - Otherwise, the result is an event access with no associated instance expression.
  - If `I` identifies a constant, then the result is a value, namely the value of that constant.
  - If `I` identifies an enumeration member, then the result is a value, namely the value of that enumeration member.
  - Otherwise, `E.I` is an invalid member reference, and a compile-time error occurs.
- **If `E` is classified as a type, if `E` is not a type parameter or an extension type, 
  and if an ***extension member lookup*** of `I` in `E` with `K` type parameters produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- **If `E` is classified as a role (only relevant in phase B) or extension type, 
  and if a member lookup of `I` in underlying type `U` with `K` type parameters produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- If `E` is a property access, indexer access, variable, or value, the type of which is `T`, 
  and a member lookup of `I` in `T` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:  
  ...
- **(only relevant in phase B) If `E` is a property access, indexer access, variable, or value, 
  the type of which is `T`, where `T` is not a type parameter or a role type, and 
  an **extension member lookup** of `I` in `T` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- **(only relevant in phase B) If `E` is a property access, indexer access, variable, or value, 
  the type of which is `T`, where `T` is a role or extension type, and 
  a member lookup of `I` in underlying type `U` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- Otherwise, an attempt is made to process `E.I` as an extension method invocation. 
  If this fails, `E.I` is an invalid member reference, and a binding-time error occurs.

TODO Note: above rules prevent `underlying.M()` from binding to `LegacyExtension.M(this Role)`.
TODO Is the "where `T` is not a type parameter" portion still relevant?

#### Member lookup

TODO Lookup in base types only extends through roles/extensions 
(not System.Object/System.ValueType/System.Role)
TODO as a result we'll find `UnderlyingType.ToString` instead of 
`object.ToString` when inside an extension/role.

#### Compatible substituted extension type

An extension type `X` is compatible with given type `U` if:
- `X` is non-generic and its underlying type is `U`, a base type of `U` or an implemented interface of `U`
- a possible type substitution on the type parameters of `X` yields underlying type `U`, 
  a base type of `U` or an implemented interface of `U`.  
  Such substitution is unique (because of the requirement that all type parameters
  from the role/extension appear in the underlying type).  
  We call the resulting substituted type `X` the "compatible substituted extension type".

#### Extension member lookup

If the *simple_name* or *member_access* occurs as the *primary_expression* of an *invocation_expression*, 
the member is said to be invoked.

Given an underlying type `U` and an identifier `I`, the objective is to find an extension member `X.I`, if possible.

We process as follows:
- Starting with the closest enclosing namespace declaration, continuing with each enclosing namespace declaration, 
  and ending with the containing compilation unit, successive attempts are made to find a candidate set of extension members:
  - If the given namespace or compilation unit directly contains extension types, those will be considered first.
  - If namespaces imported by using-namespace directives in the given namespace or 
    compilation unit directly contain extension types, those will be considered second.
- Check which extension types are compatible with the given underlying type `U` and 
  collect resulting compatible substituted extension types.
- Perform member lookup for `I` in each compatible substituted extension type `X` 
  (note this takes into account whether the member is invoked).
- Merge the results (TODO need more details).
- If the set is empty, proceed to the next enclosing namespace.
- If the set consists of a single member that is not a method, 
  then this member is the result of the lookup.
- Otherwise, if the set contains only methods and the member is invoked, 
  overload resolution is applied to the candidate set.
  - If a single best method is found, this member is the result of the lookup.
  - If no best method is found, continue the search through namespaces and their imports.
  - Otherwise (ambiguity), a compile-time error occurs.
- Otherwise, the lookup is ambiguous, and a binding-time error occurs.
- If no candidate set is found in any enclosing namespace declaration or compilation unit, 
  the result of the lookup is empty.

TODO need to account for nested extension types. We'll start looking in enclosing types then enclosing namespaces.  
TODO explain static usings and nested extension types.

The preceding rules mean that extension members available in inner namespace declarations 
take precedence over extension members available in outer namespace declarations,
and that extension members declared directly in a namespace take precedence over 
extension members imported into that same namespace with a using namespace directive.  

The difference between invocation and non-invocation handling is that for invocation scenarios, 
we can look past a result and continue looking at enclosing namespaces.  

For example, if an "inner" extension has a method `void M(int)` and an "outer" extension
has a method `void M(string)`, an extension member lookup for `M("hello")` will look over
the `int` extension and successfully find the `string` extension.  
On the other hand, if an "inner" extension has an `int` property and an "outer" extension
has a string property, an assignment of a string to that property will fail, as
extension member lookup will find the `int` property and stop there.  

For context see [extension method invocation rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#11783-extension-method-invocations).

### Element access

https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#117103-indexer-access
TODO

### Operators

TODO

### Method group conversions

https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/conversions.md#108-method-group-conversions
TODO A single method is selected corresponding to a method invocation, 
but with some tweaks related to normal form and optional parameters.
TODO There's also the scenario where a method group contains a single method (lambda improvements).

## B. Roles and extensions with members

In this second subset of the feature, the *role_declaration* becomes allowed
and non-static members other than fields become allowed.

TODO issues in async code


### Role and extension type members

The restrictions on modifiers from phase A remain (`new`).  
Non-static members become allowed in phase B.  

#### Fields

A *field_declaration* in a *role_declaration* or *extension_declaration* 
shall explicitly include a `static` modifier.  

#### Methods

TODO allow `this` (of type current role).  

#### Properties

Auto-properties must still be static (since instance fields are disallowed).  
TODO allow `this` (of type current role).  

#### Operators

##### Conversions

TODO
```
role R : U { } 
R r = default;
object o = r; // what conversion is that? if R doesn't have `object` as base type. What about interfaces?
```

Should allow conversion operators. Extension conversion is useful. 
Example: from `int` to `string` (done by `StringExtension`).  
But we should disallow user-defined conversions from/to underlying type 
or inherited roles, because a conversion already exists.  
Conversion to interface still disallowed.  

#### Events

TODO

#### Indexers

TODO

### Lookup rules

The simple names and member access rules from phase A section (above) take full effect, 
as role types now exist and extension types may have non-static members.  
TODO: the simple names rules find `object.ToString()` for `ToString()` 
in instance extension method, rather than `U.ToString()`. Can we improve on that?

### Instance invocations

TODO

## C. Roles and extensions that implement interfaces

