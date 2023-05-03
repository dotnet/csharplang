# Extension types

TODO3 No duplicate base extensions (to avoid ambiguities)

TODO2 adjust scoping rules so that type parameters are in scope within the 'for'  
TODO2 check Method type inference: 7.5.2.9 Lower-bound interfaces
TODO2 extensions are disallowed within interfaces with variant type parameters
TODO2 We should likely allow constructors and `required` properties

## Summary
[summary]: #summary

The purpose of "extensions" is to augment or adapt existing types to new scenarios,
when those types are not under your control, or where changing them would
negatively impact other uses of them. The adaptation can be in the form of
adding new function members as well as implementing additional interfaces.

`explicit extension CustomerExtension for ReadOnlySpan<byte> : PersonExtension { ... }`
`implicit extension EnumExtension for Enum { ... }`

## Motivation
[motivation]: #motivation

Explicit extensions address two main classes of scenarios: "augmentation" scenarios
(fit an existing value with a *new* member) and "adaptation" scenarios
(fit an existing value to an *existing* interface).

In addition, implicit extensions would provide for additional kinds of extension members
beyond today's extension methods, and for "extension interfaces".

### Augmentation scenarios

Augmentation scenarios allow existing values to be seen through the lens of
a "stronger" type - an extension that provides additional function members on the value.

### Adaptation scenarios

Adaptation scenarios allow existing values to be adapted to existing interfaces,
where an extension provides details on how the interface members are implemented.

## Design

This proposal is divided into three parts, all relating to extending existing types:  
A. static implicit extensions  
B. implicit and explicit extensions with members  
C. implicit and explixit extensions that implement interfaces  

The syntax for extensions is as follows:

```antlr
type_declaration
    | extension_declaration // add
    | ...
    ;

extension_declaration
    : extension_modifier* ('implicit' | 'explicit') 'extension' identifier type_parameter_list? ('for' type)? extension_base_type_list? type_parameter_constraints_clause* extension_body
    ;

extension_base_type_list
    : ':' extension_or_interface_type_list
    ;

extension_or_interface_type_list
    : interface_type_list
    : extension_type (',' extension_or_interface_type_list)
    ;

extension_body
    : '{' extension_member_declaration* '}'
    ;

extension_member_declaration
    : constant_declaration
    | field_declaration
    | method_declaration
    | property_declaration
    | event_declaration
    | indexer_declaration
    | operator_declaration
    | type_declaration
    ;

extension_modifier
    | 'partial'
    | 'unsafe'
    | 'static'
    | 'protected'
    | 'internal'
    | 'private'
    | 'file'
    ;
```

An example with multiple base explicit extension:
```
explicit extension DiamondExtension for NarrowerUnderlyingType : BaseExtension1, Interface1, BaseExtension2, Interface2 { }`
```

TODO should we have a naming convention like `Extension` suffixes? (`DataObjectExtension`)

## Extension type

An extension type (new kind of type) is declared by a *extension_declaration*.  

The extension type does not **inherit** members from its underlying type 
(which may be `sealed` or a struct), but
the lookup rules are modified to achieve a similar effect (see below).  

There is an identity conversion between an extension and its underlying type,
and between an extension and its base extensions.

An extension type satisfies the constraints satisfied by its underlying type (see section on constraints). 
In phase C, some additional constraints can be satisfied (additional implemented interfaces).  

### Underlying type

The *extension_underlying_type* type may not be `dynamic`, a pointer, 
a ref struct type, a ref type or an extension.  
The underlying type may not include an *interface_type_list* (this is part of Phase C).  
The extension type must be static if its underlying type is static.  

When a partial extension declaration includes an underlying type specification,
that underlying type specification shall reference the same type as all other parts
of that partial type that include an underlying type specification.
It is a compile-time error if no part of a partial extension includes
an underlying type specification.  

It is a compile-time error if the underlying type differs amongst all the parts
of an extension declaration.  

TODO2 we need rules to only allow an underlying type that is compatible with the base extensions.  

TODO should the underlying type be inferred when none was specified but
base extensions are specified?

### Modifiers

It is a compile-time error if the `implicit` or `explicit` modifiers differ amongst
all the parts of an extension declaration.  

The permitted modifiers on an extension type are `partial`, 
`unsafe`, `static`, `file` and the accessibility modifiers.  
A static extension shall not be instantiated, shall not be used as a type and shall
contain only static members.  
The standard rules for modifiers apply (valid combination of access modifiers, no duplicates).  
Extension types may not contain instance fields (either explicitly or implicitly).

When a partial extension declaration includes an accessibility specification, 
that specification shall agree with all other parts that include an accessibility specification. 
If no part of a partial extension includes an accessibility specification, 
the type is given the appropriate default accessibility (`internal`).

## Implicit extension type

An implicit extension type is an extension whose members can be found on the underlying type
(or a value of the underlying type) when the extension is "in scope"
and compatible with the underlying type (see extension member lookup section).

The underlying type must include all the type parameters from the implicit extension type.  

### Terminology

We'll use "extends" for relationship to underlying/extended type 
(comparable to "inherits" for relationship to base type).  
We'll use "inherits" for relationship to inherited/base extensions 
(comparable to "implements" for relationship to implemented interfaces).  

```csharp
struct U { }
explicit extension X for U { }
explicit extension Y for U : X, X1 { }
```
"Y has underlying type U"  
"Y extends U"  
"Y inherits X and X1"  
"Derived extension Y inherits members from inherited/base extensions X and X1"  

Similarly, extension don't have a base type, but have base extensions.  

`implicit extension R<T> for T where T : I1, I2 { }`
`implicit extension R<T> for T where T : INumber<T> { }`
An extension may be a value or reference type, and this may not be known at compile-time. 

### Accessibility constraints

We modify the [accessibility constraints](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/basic-concepts.md#755-accessibility-constraints) as follows:

The following accessibility constraints exist:
- [...]
- \***The underlying type of an extension type shall be at least as accessible as the extension type itself.**
- \***The base extensions of an extension type shall be at least as accessible as the extension type itself.**

Note those also apply to visibility constraints of file-local types (TODO not yet specified).

### Protected access

We modify the [protected access rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/basic-concepts.md#754-protected-access) as follows:

\***When a `protected` (or other accessibility with `protected`) extension member is accessed, 
the access shall take place within an extension declaration that derives from 
the extension in which it is declared. 
Furthermore, the access is required to take place *through* an instance of that 
derived extension type or an extension type constructed from it.
This restriction prevents one derived extension from accessing protected members of 
other derived extensions, even when the members are inherited from the same base extension.**

TODO

Let `B` be a base class that declares a protected instance member `M`, 
and let `D` be a class that derives from `B`. Within the *class_body* of `D`, 
access to `M` can take one of the following forms:

- An unqualified *type_name* or *primary_expression* of the form `M`.
- A *primary_expression* of the form `E.M`, provided the type of `E` is `T` or 
  a class derived from `T`, where `T` is the class `D`, or a class type constructed from `D`.
- A *primary_expression* of the form `base.M`.
- A *primary_expression* of the form `base[`*argument_list*`]`.

### Extension type members

The extension type members may not use the `virtual`, `abstract`, `sealed`, `override` modifiers.  
Member methods may not use the `readonly` modifier.  
The `new` modifier is allowed and the compiler will warn that you should
use `new` when shadowing.  

An extension cannot contain a member declaration with the same name as the extension.

#### Signatures, overloading and hiding

The existing [rules for signatures](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/basic-concepts.md#76-signatures-and-overloading) apply.  
Two signatures differing by an extension vs. its underlying type, or an extension vs. 
one of its base extensions are considered to be the *same signature*.

TODO2 this needs to be refined to allow overload on different underlying types.
```
explicit extension ObjectExtension : object;
explicit extension StringExtension : string, ObjectExtension;
void M(ObjectExtension r)
void M(StringExtension r) // overload is okay
```

Shadowing includes underlying type and inherited extensions.  

```
class U { public void M() { } }
explicit extension R for U { /*new*/ public void M() { } } // wins when dealing with an R
```

```
class U { public void M() { } }
implicit extension X for U { /*new*/ public void M() { } } // ignored in some cases
U u;
u.M(); // U.M (ignored X.M)
X x;
x.M(); // X.M
```

```
class U { }
explicit extension R for U { public void M() { } }
explicit extension R2 for U : R { /*new*/ public void M() { } } // wins when dealing with an R2
```

#### Constants

Existing [rules for constants](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#144-constants) 
apply (so duplicates or the `static` modifier are disallowed).

#### Fields

A *field_declaration* in an *extension_declaration* shall explicitly include a `static` modifier.  
Otherwise, existing [rules for fields](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#145-fields) apply.  

#### Methods

TODO allow `this` (of type current extension).  
Parameters with the `this` modifier are disallowed.
Otherwise, existing [rules for methods](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#146-methods) apply.
In particular, a static method does not operate on a specific instance, 
and it is a compile-time error to refer to `this` in a static method.  
Extension methods are disallowed.

#### Properties

TODO allow `this` (of type current extension).  
Auto-properties must be static (since instance fields are disallowed).  

Existing [rules for properties](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#147-properties) apply.
In particular, a static property does not operate on a specific instance, 
and it is a compile-time error to refer to `this` in a static property.

#### Nested types

TODO any special rules?

```
extension Extension : UnderlyingType
{
    class NestedType { }
}
class UnderlyingType { }

UnderlyingType.NestedType x = null; // okay
```

#### Events

TODO
TODO2 Event with an associated instance field (error)

#### Fields

TODO

#### Constructors

TODO

#### Operators

##### Conversions

TODO
```
explicit extension R for U { } 
R r = default;
object o = r; // what conversion is that? if R doesn't have `object` as base type. What about interfaces?
```

Should allow conversion operators. Extension conversion is useful. 
Example: from `int` to `string` (done by `StringExtension`).  
But we should disallow user-defined conversions from/to underlying type 
or inherited extensions, because a conversion already exists.  
Conversion to interface still disallowed.  

#### Indexers

TODO

## Constraints

TL;DR: An extension satisfies the constraints satisfied by its underlying type. Extensions cannot be used as type constraints.  

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
  - \***`A` is an extension type with an underlying type that satisfies the reference type constraint.**
- If the constraint is the value type constraint (`struct`), the type `A` shall satisfy one of the following:
  - `A` is a `struct` type or `enum` type, but not a nullable value type.
  - `A` is a type parameter having the value type constraint.
  - \***`A` is an extension type with an underlying type that satisfies the value type constraint.**
- If the constraint is the constructor constraint `new()`, 
  the type `A` shall not be `abstract` and shall have a public parameterless constructor. 
  This is satisfied if one of the following is true:
  - `A` is a value type, since all value types have a public default constructor.
  - `A` is a type parameter having the constructor constraint.
  - `A` is a type parameter having the value type constraint.
  - `A` is a `class` that is not abstract and contains an explicitly declared public constructor with no parameters.
  - `A` is not `abstract` and has a default constructor.
  - \***`A` is an extension type with an underlying type that satisfies the constructor constraint.**

A compile-time error occurs if one or more of a type parameter’s constraints are not satisfied by the given type arguments.

By the existing [rules on type parameter constraints](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#1425-type-parameter-constraints)
extensions are disallowed in constraints (an extension is neither a class or an interface type).

```
where T : Extension // error
```

TODO Does this restriction on constraints cause issues with structs?

## Extension methods

We modify the [extension methods rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#14610-extension-methods) as follows:

[...] The first parameter of an extension method may have no modifiers other than `this`, 
and the parameter type may not be a pointer **or an extension** type.

## Nullability

TODO2 Open question on top-level nullability on underlying type.
TODO2 disallow nullable annotation on base extension? Or at least need to clarify what `Extension?` means in various scenarios.

## Compat breaks

TODO2 types may not be called "extension" (reserved, break)  

## Lookup rules

TODO2 give an overview
TODO2 Will need to spec or disallow `base.` syntax?
Casting seems an adequate solution to access hidden members: `((R)r2).M()`.  

### Simple names

TL;DR: After doing an unsuccessful member lookup in an extension, we'll also perform a
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
  - \***Otherwise, if `T` is an extension (only relevant in phase B) and 
    a member lookup of `I` in underlying type `U` with `e` type arguments produces a match:**  
    ...
- Otherwise, for each namespace `N`, starting with the namespace in which the *simple_name* occurs, 
  continuing with each enclosing namespace (if any), and ending with the global namespace, 
  the following steps are evaluated until an entity is located:  
  ...
- Otherwise, the simple_name is undefined and a compile-time error occurs.

### Member access

TL;DR: After doing an unsuccessful member lookup in a type, we'll perform an member lookup
in the underlying type if we were dealing with an extension, and if that is still unsuccessful,
we'll perform an extension member lookup.

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
- \***If `E` is classified as an extension, 
  and if a member lookup of `I` in underlying type `U` with `K` type parameters produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- \***If `E` is classified as a type, if `E` is not a type parameter, 
  and if an ***extension member lookup*** of `I` in `E` with `K` type parameters produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- If `E` is a property access, indexer access, variable, or value, the type of which is `T`, 
  and a member lookup of `I` in `T` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:  
  ...
- \***(only relevant in phase B) If `E` is a property access, indexer access, variable, or value, 
  the type of which is `T`, where `T` is an extension type, and 
  a member lookup of `I` in underlying type `U` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- \***(only relevant in phase B) If `E` is a property access, indexer access, variable, or value, 
  the type of which is `T`, where `T` is not a type parameter, and 
  an **extension member lookup** of `I` in `T` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- Otherwise, an attempt is made to process `E.I` as an extension method invocation. 
  If this fails, `E.I` is an invalid member reference, and a binding-time error occurs.

TODO Is the "where `T` is not a type parameter" portion still relevant?

### Member lookup

TL;DR: Member lookup understands that extensions inherit from their base extensions, but not from `object`.

We modify the [member lookup rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#115-member-lookup) as follows (only change is what counts as "base type"):

A member lookup of a name `N` with `K` type arguments in a type `T` is processed as follows:

- First, a set of accessible members named `N` is determined:
  - If `T` is a type parameter, then the set is the union of the sets of accessible members named `N` in each of the types specified as a primary constraint or secondary constraint for `T`, along with the set of accessible members named `N` in `object`.
  - Otherwise, the set consists of all accessible members named `N` in `T`, including inherited members and **for non-extension types** the accessible members named `N` in `object`. If `T` is a constructed type, the set of members is obtained by substituting type arguments as described in §14.3.3. Members that include an `override` modifier are excluded from the set.
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
- \***If `T` is an *extension_type*, the base types of `T` are the base extensions of `T`.**

```csharp
explicit extension R : U
{
    void M()
    {
        var s = ToString(); // find `U.ToString()` as opposed to `object.ToString()`
    }
}
```

### Compatible substituted extension type

TL;DR: We can determine whether an implicit extension is compatible with a given underlying type
and when successful this process yields an extension type we can use
(including required substitutions).  

An extension type `X` is compatible with given type `U` if:
- `X` is non-generic and its underlying type is `U`, a base type of `U` or an implemented interface of `U`
- a possible type substitution on the type parameters of `X` yields underlying type `U`, 
  a base type of `U` or an implemented interface of `U`.  
  Such substitution is unique (because of the requirement that all type parameters
  from the extension appear in the underlying type).  
  We call the resulting substituted type `X` the "compatible substituted extension type".

```csharp
#nullable enable
implicit extension Extension<T> for Underlying<T> where T : class { }
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
- Merge the results
- Next, members that are hidden by other members are removed from the set.  
  (Same rules as in member lookup, but "base type" is extended to mean "base extension")
- Finally, having removed hidden members, the result of the lookup is determined:
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

TODO3 explain static usings

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

Extensions are implemented as ref structs with an extension marker method.  
The type is marked with Obsolete and CompilerFeatureRequired attributes.  
The extension marker method encodes the underlying type and base extensions as parameters in that order.  
The marker method is called `<ImplicitExtension>$` for implicit extensions and 
`<ExplicitExtension>$` for explicit extensions.  
For example: `implicit extension R for UnderlyingType : BaseExtension1, BaseExtension2` yields
`private static void <ImplicitExtension>$(UnderlyingType, BaseExtension1, BaseExtension2)`.  

If the extension has any instance member, then we emit a ref field (of underlying type)
into the ref struct and a constructor.  
TODO2 The wrapping can be done with a static unspeakable factory method  

Values of extension types are left as values of the underlying value type, until an extension
member is accessed. When an extension member is accessed, an extension instance is created
with a reference to the underlying value and the member is accessed on that instance.

```
Extension r = default(UnderlyingType); // emitted as a local of type `UnderlyingType`
r.ExtensionMember(); // emitted as `new Extension(ref r).ExtensionMember();`
```

Extensions appearing in signatures are emitted as the extension's underlying type
marked with a modopt of the extension type.

```
void M(Extension r) // emitted as `void M(modopt(Extension) UnderlyingType r)`
```

TODO issues in async code with ref structs

## Phase A: Adding static constants, fields, methods and properties

In this first subset of the feature, the syntax is restricted to implicit *extension_declaration*
and containing only *constant_declaration* members and static *field_declaration*, 
*method_declaration*, *property_declaration* and *type_declaration* members.  
TODO: events?

## Phase B. Explicit extensions with members

In this second subset of the feature, the explicit *extension_declaration* becomes allowed
and non-static members other than fields or auto-properties become allowed.

### Extension type members

The restrictions on modifiers from phase A remain (`new`).  
Non-static members become allowed in phase B.  

#### Fields

A *field_declaration* in a *extension_declaration* shall explicitly include a `static` modifier.  

#### Methods

TODO allow `this` (of type current extension).  

#### Properties

Auto-properties must still be static (since instance fields are disallowed).  
TODO allow `this` (of type current extension).  

#### Operators

##### Conversions

TODO
```
explicit extension R : U { } 
R r = default;
object o = r; // what conversion is that? if R doesn't have `object` as base type. What about interfaces?
```

Should allow conversion operators. Extension conversion is useful. 
Example: from `int` to `string` (done by `StringExtension`).  
But we should disallow user-defined conversions from/to underlying type 
or inherited extensions, because a conversion already exists.  
Conversion to interface still disallowed.  
TODO2: Could we make the conversion to be explicit identity conversion instead
of implicit?  

#### Events

TODO

#### Indexers

TODO

### Instance invocations

TODO

## Phase C. Extensions that implement interfaces

