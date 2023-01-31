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
    : ':' type ',' role_or_interface_type_list
    ;

role_or_interface_type_list
    : interface_type_list
    : role_type (',' role_or_interface_type_list)
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

An example with multiple base roles:
```
role DiamondRole : NarrowerUnderlyingType, BaseRole1, BaseRole2, Interface1, Interface2 { }`
```

TODO there are some open questions on extension syntax 
(who decides to turn a role into an extension?)  
TODO should we have a naming convention like `Role` and `Extension` suffixes? 
(`CustomerRole` and `DataObjectExtension`)

## Role type

A role type (new kind of type) is declared by a *role_declaration*.  
The permitted modifiers on an extension type are `partial`, `unsafe`, `static`, `file` and 
the accessibility modifiers.  
A static role shall not be instantiated, shall not be used as a type and shall
contain only static members.  
The standard rules for modifiers apply (valid combination of access modifiers, no duplicates).  

The extension type does not **inherit** members from its underlying type 
(which may be `sealed` or a struct), but
the lookup rules are modified to achieve a similar effect (see below).  

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

There is an identity conversion between a role and its underlying type,
and between a role and its base roles.

A role type satisfies the constraints satisfied by its underlying type (see section on constraints). 
In phase C, some additional constraints can be satisfied (additional implemented interfaces).  

### Terminology

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

TODO2 slightly different meaning for `protected`  

### Accessibility constraints

We modify the [accessibility constraints](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/basic-concepts.md#755-accessibility-constraints) as follows:

The following accessibility constraints exist:
- [...]
- **The underlying type of a role type shall be at least as accessible as the role type itself.**
- **The base roles of a role type shall be at least as accessible as the role type itself.**

### Signatures and overloading

The existing [rules for signatures](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/basic-concepts.md#76-signatures-and-overloading) apply.  
Two signatures differing by a role vs. its underlying type, or a role vs. 
one of its base types are considered to be the *same signature*.

### Role type members

The role type members may not use the `virtual` or `override` modifiers.  
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

A role cannot contain a member declaration with the same name as the role.

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

TODO2 `UnderlyingType.NestedType` would find the `NestedType` from an extension.

#### Events

TODO

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

#### Indexers

TODO

TODO more members (static constructors, constructors)

## Extension type

An extension type is a role type declared by an *extension_declaration*.  
The above rules from role types apply, namely the permitted modifiers and rules on underlying type.  
TODO2

## Constraints

TL;DR: A role satisfies the constraints satisfied by its underlying type. Roles cannot be used as type constraints.  

We modify the [rules on satisfying constraints](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/types.md#845-satisfying-constraints) as follows:

Whenever a constructed type or generic method is referenced, the supplied type arguments
are checked against the type parameter constraints declared on the generic type or method.
For each `where` clause, the type argument `A` that corresponds to the named type parameter
is checked against each constraint as follows:

- If the constraint is a class type, an interface type, or a type parameter,
  let `C` represent that constraint with the supplied type arguments substituted 
  for any type parameters that appear in the constraint. To satisfy the constraint, 
  it shall be the case that type `A` is convertible to type `C` by one of the following:
  - An identity conversion
  - An implicit reference conversion
  - A boxing conversion, provided that type `A` is a non-nullable value type.
  - An implicit reference, boxing or type parameter conversion from a type parameter `A` to `C`.
- If the constraint is the reference type constraint (`class`), the type `A` shall satisfy one of the following:
  - `A` is an interface type, class type, delegate type, array type or the dynamic type.
  - `A` is a type parameter that is known to be a reference type.
  - **`A` is a role type with an underlying type that satisfies the reference type constraint.**
- If the constraint is the value type constraint (`struct`), the type `A` shall satisfy one of the following:
  - `A` is a `struct` type or `enum` type, but not a nullable value type.
  - `A` is a type parameter having the value type constraint.
  - **`A` is a role type with an underlying type that satisfies the value type constraint.**
- If the constraint is the constructor constraint `new()`, 
  the type `A` shall not be `abstract` and shall have a public parameterless constructor. 
  This is satisfied if one of the following is true:
  - `A` is a value type, since all value types have a public default constructor.
  - `A` is a type parameter having the constructor constraint.
  - `A` is a type parameter having the value type constraint.
  - `A` is a `class` that is not abstract and contains an explicitly declared public constructor with no parameters.
  - `A` is not `abstract` and has a default constructor.

A compile-time error occurs if one or more of a type parameter’s constraints are not satisfied by the given type arguments.

By the existing [rules on type parameter constraints](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#1425-type-parameter-constraints)
roles are disallowed in constraints (a role is neither a class or an interface type).

```
where T : Role // error
```

TODO Does this restriction on constraints cause issues with structs?

## Extension methods

We modify the [extension methods rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#14610-extension-methods) as follows:

[...] The first parameter of an extension method may have no modifiers other than `this`, 
and the parameter type may not be a pointer **or a role** type.

## Lookup rules

TODO2 Will need to spec or disallow `base.` syntax?
Casting seems an adequate solution to access hidden members: `((R)r2).M()`.  

### Simple names

TL;DR: After doing an unsuccessful member lookup in a role, we'll also perform a
member lookup in the underlying type.  

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

### Member access

TL;DR: After doing an unsuccessful member lookup in a type, we'll perform an member lookup
in the underlying type if we were dealing with a role, or we'll perform an extension member lookup
if we were not dealing with a role.

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

TODO Is the "where `T` is not a type parameter" portion still relevant?

### Member lookup

TL;DR: Member lookup understands that roles inherit from their base roles, but not from `object`.

We modify the [member lookup rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#115-member-lookup) as follows:

A member lookup of a name `N` with `K` type arguments in a type `T` is processed as follows:

- First, a set of accessible members named `N` is determined:
  - If `T` is a type parameter, then the set is the union of the sets of accessible members named `N` in each of the types specified as a primary constraint or secondary constraint for `T`, along with the set of accessible members named `N` in `object`.
  - Otherwise, the set consists of all accessible members named `N` in `T`, including inherited members and **for non-role types** the accessible members named `N` in `object`. If `T` is a constructed type, the set of members is obtained by substituting type arguments as described in §14.3.3. Members that include an `override` modifier are excluded from the set.
- Next, if `K` is zero, all nested types whose declarations include type parameters are removed. If `K` is not zero, all members with a different number of type parameters are removed. When `K` is zero, methods having type parameters are not removed, since the type inference process might be able to infer the type arguments.
- Next, if the member is invoked, all non-invocable members are removed from the set.
- Next, members that are hidden by other members are removed from the set. For every member `S.M` in the set, where `S` is the type in which the member `M` is declared, the following rules are applied:
  - If `M` is a constant, field, property, event, or enumeration member, then all members declared in a base type of `S` are removed from the set.
  - If `M` is a type declaration, then all non-types declared in a base type of `S` are removed from the set, and all type declarations with the same number of type parameters as `M` declared in a base type of `S` are removed from the set.
  - If `M` is a method, then all non-method members declared in a base type of `S` are removed from the set.
- Next, interface members that are hidden by class members are removed from the set. This step only has an effect if `T` is a type parameter and `T` has both an effective base class other than `object` and a non-empty effective interface set. For every member `S.M` in the set, where `S` is the type in which the member `M` is declared, the following rules are applied if `S` is a class declaration other than `object`:
  - If `M` is a constant, field, property, event, enumeration member, or type declaration, then all members declared in an interface declaration are removed from the set.
  - If `M` is a method, then all non-method members declared in an interface declaration are removed from the set, and all methods with the same signature as `M` declared in an interface declaration are removed from the set.
- Finally, having removed hidden members, the result of the lookup is determined:
  - If the set consists of a single member that is not a method, then this member is the result of the lookup.
  - Otherwise, if the set contains only methods, then this group of methods is the result of the lookup.
  - Otherwise, the lookup is ambiguous, and a binding-time error occurs.

For purposes of member lookup, a type `T` is considered to have the following base types:

- If `T` is `object` or `dynamic`, then `T` has no base type.
- If `T` is an *enum_type*, the base types of `T` are the class types `System.Enum`, `System.ValueType`, and `object`.
- If `T` is a *struct_type*, the base types of `T` are the class types `System.ValueType` and `object`.
- If `T` is a *class_type*, the base types of `T` are the base classes of `T`, including the class type `object`.
- If `T` is an *interface_type*, the base types of `T` are the base interfaces of `T` and the class type `object`.
- If `T` is an *array_type*, the base types of `T` are the class types `System.Array` and `object`.
- If `T` is a *delegate_type*, the base types of `T` are the class types `System.Delegate` and `object`.
- **If `T` is an *role_type*, the base types of `T` are the base roles of `T`.**.

```csharp
role R : U
{
    void M()
    {
        var s = ToString(); // find `U.ToString()` as opposed to `object.ToString()`
    }
}
```

### Compatible substituted extension type

TL;DR: We can determine whether an extension is compatible with a given underlying type
and when successful this process yields an extension type we can use
(including required substitutions).  

An extension type `X` is compatible with given type `U` if:
- `X` is non-generic and its underlying type is `U`, a base type of `U` or an implemented interface of `U`
- a possible type substitution on the type parameters of `X` yields underlying type `U`, 
  a base type of `U` or an implemented interface of `U`.  
  Such substitution is unique (because of the requirement that all type parameters
  from the role/extension appear in the underlying type).  
  We call the resulting substituted type `X` the "compatible substituted extension type".

```csharp
#nullable enable
role Extension<T> : Underlying<T> where T : class { }
class Base<T> { }
class Underlying<T> : Base<T> { }

Base<object> b; // Extension<object> is a compatible extension with b
Underlying<string> u; // Extension<string> is a compatible extension with u
Underlying<int> u2; // But no substitution of Extension<T> is compatible with u2
Underlying<string?> u3; // Extensions<string?> is a compatible extension with u3
                       // but its usage will produce a warning
```

### Extension member lookup

TL;DR: Given an underlying type, we'll search enclosing types and namespaces 
(and their imports) for compatible extensions and for each "layer" we'll do member lookups.  

If the *simple_name* or *member_access* occurs as the *primary_expression* of an *invocation_expression*, 
the member is said to be invoked.

Given an underlying type `U` and an identifier `I`, the objective is to find an extension member `X.I`, if possible.

We process as follows:
- Starting with the closest enclosing type declaration, continuing with each type declaration,
  then continuing with each enclosing namespace declaration, and ending with
  the containing compilation unit, successive attempts are made to find a candidate set of extension members:
  - If the given type, namespace or compilation unit directly contains extension types,
    those will be considered first.
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

TODO2 explain static usings

The preceding rules mean that:
1. extension members available in inner type declarations take precedence over
extension members available in outer type declarations,
2. extension members available in inner namespace declarations 
take precedence over extension members available in outer namespace declarations,
3. and that extension members declared directly in a namespace take precedence over 
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
User-defined conversion should be allowed, except where it conflicts with a built-in
conversion (such as with an underlying type).  

### Method group conversions

https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/conversions.md#108-method-group-conversions
TODO A single method is selected corresponding to a method invocation, 
but with some tweaks related to normal form and optional parameters.  
TODO There's also the scenario where a method group contains a single method (lambda improvements).  

## Implementation details

Roles are implemented as ref structs.  
If the role any instance member, then we emit a ref field (of underlying type)
into the ref struct and a constructor.  
Values of role types are left as values of the underlying value type, until a role
member is accessed. When a role member is accessed, a role instance is created
with a reference to the underlying value and the member is accessed on that instance.

```
Role r = default(UnderlyingType); // emitted as a local of type `UnderlyingType`
r.RoleMember(); // emitted as `new Role(ref r).RoleMember();`
```

Roles appearing in signatures are emitted as the role's underlying type
marked with a modopt of the role type.

```
void M(Role r) // emitted as `void M(modopt(Role) UnderlyingType r)`
```

TODO how do we emit an extension type versus a handcrafted ref struct?  
TODO how do we emit the relationship to underlying type? base type or special constructor?  
TODO our emit strategy should allow using pointer types and ref structs as underlying types
in the future.  
TODO issues in async code with ref structs

## Phase A: Adding static constants, fields, methods and properties

In this first subset of the feature, the syntax is restricted to *extension_declaration*
and containing only *constant_declaration* members and static *field_declaration*, 
*method_declaration*, *property_declaration* and *type_declaration* members.  
TODO: events?

## Phase B. Roles and extensions with members

In this second subset of the feature, the *role_declaration* becomes allowed
and non-static members other than fields or auto-properties become allowed.

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

### Instance invocations

TODO

## Phase C. Roles and extensions that implement interfaces

