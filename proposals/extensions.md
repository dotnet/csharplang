# Extension types

TODO(inheritance) No duplicate base extensions (to avoid ambiguities)  
TODO(inheritance) issue with variance of extended type if we erase to a ref struct with a ref field.  

TODO(instance) need to spec why extension properties are not found during lookup for attribute properties, or explicitly disallow them  
TODO(static) adjust scoping rules so that type parameters are in scope within the 'for'  
TODO2 check Method type inference: 7.5.2.9 Lower-bound interfaces  
TODO(static) extensions are disallowed within interfaces with variant type parameters  
TODO2 We should likely allow constructors and `required` properties  
TODO attributes and attribute targets  

## Open issue: merging extension methods and extension members

TODO(instance) revise preference of extension types over extension methods (should mix and disambiguate duplicates if needed instead)

```c#
static class Extensions
{
    public static X ToX<Y>(this IEnumerable<Y> values) => ...
}

implicit extension ImmutableArrayExtensions<Y> for ImmutableArray<Y>
{
    public X ToX() => ...
}

// or reverse:

static class Extensions
{
    public static X ToX<Y>(this ImmutableArray<Y> values) => ...
}

implicit extension IEnumerableExtensions<Y> for IEnumerable<Y>
{
    public X ToX() => ...
}
```

In this world, i have existing extensions and i add the new features because it feels like the right way to do
modern C#.  And either has a problem depending on a priority system picking the "worse" overload.  I *want* these
mixed.  Just as if i had done:

```c#
static class Extensions
{
    public static X ToX<Y>(this ImmutableArray<Y> values) => ...
    public static X ToX<Y>(this IEnumerable<Y> values) => ...
}
```

TODO(instance) confirm what happens when we have different kinds of members

```cs
var c = new C();
c.M(ImmutableArray.Create(1, 2, 3)); // What should happen?

class C
{
}

public static class CExt
{
    public static void M(this C c, IEnumerable<int> e) => ...
}

public implicit extension E1 for C
{
    public Action<ImmutableArray<int>> M => ...
}
```

```c#
class TableIDoNotOwn : IEnumerable<Item> { }

static class IEnumerableExtensions
{
    public int Count<T>(this IEnumerable<T> t);
}

implicit extension MyTableExtensions for TableIDoNotOwn
{
    public int Count { get { ... } }
}

// What happens here?
var v = table.Count; // Let's get a read from LDM
```

## Summary
[summary]: #summary

The purpose of "extensions" is to augment or adapt existing types to new scenarios,
when those types are not under your control, or where changing them would
negatively impact other uses of them. The adaptation can be in the form of
adding new function members as well as implementing additional interfaces.

`explicit extension CustomerExtension for byte[] : PersonExtension { ... }`
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

TODO(static) should we have a naming convention like `Extension` suffixes? (`DataObjectExtension`)  

## Extension type

An extension type (new kind of type) is declared by a *extension_declaration*.  

The extension type does not **inherit** members from its underlying type 
(which may be `sealed` or a struct), but
the lookup rules are modified to achieve a similar effect (see below).  

There is an identity conversion between an extension and its underlying type,
and between an extension and its base extensions.

An extension type satisfies the constraints satisfied by its underlying type (see section on constraints). 
In the Interface Phase, some additional constraints can be satisfied (additional implemented interfaces).  

### Underlying type

The *extension_underlying_type* type may not be `dynamic`, a pointer, 
a ref struct type, a ref type or an extension.  
The underlying type may not include an *interface_type_list* (this is part of the Interface Phase).  
The extension type must be static if its underlying type is static.  

When a partial extension declaration includes an underlying type specification,
that underlying type specification shall reference the same type as all other parts
of that partial type that include an underlying type specification.
It is a compile-time error if no part of a partial extension includes
an underlying type specification.  

It is a compile-time error if the underlying type differs amongst all the parts
of an extension declaration.  

TODO2(inheritance) we need rules to only allow an underlying type that is compatible with the base extensions.  

TODO(inheritance) should the underlying type be inferred when none was specified but
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
explicit extension X1 for U { }
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

Note: the rules still disallow access to protected members of the underlying type through the extension type.

TODO(static)

Let `B` be a base class that declares a protected instance member `M`, 
and let `D` be a class that derives from `B`. Within the *class_body* of `D`, 
access to `M` can take one of the following forms:

- An unqualified *type_name* or *primary_expression* of the form `M`.
- A *primary_expression* of the form `E.M`, provided the type of `E` is `T` or 
  a class derived from `T`, where `T` is the class `D`, or a class type constructed from `D`.
- A *primary_expression* of the form `base.M`.
- A *primary_expression* of the form `base[`*argument_list*`]`.

```csharp
class Base
{
    protected void M() { }
}
extension E1 for Base
{
    // cannot use Base.M
    protected void M2() { }
}
extension E2 for Base : E1
{
    // can use E1.M2
}
```

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

TODO2(inheritance) this needs to be refined to allow overload on different underlying types.
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

TODO(instance) allow `this` (of type current extension).  
Parameters with the `this` modifier are disallowed.
Otherwise, existing [rules for methods](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#146-methods) apply.
In particular, a static method does not operate on a specific instance, 
and it is a compile-time error to refer to `this` in a static method.  
Extension methods are disallowed.

#### Properties

TODO(instance) allow `this` (of type current extension).  
Auto-properties must be static (since instance fields are disallowed).  

Existing [rules for properties](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#147-properties) apply.
In particular, a static property does not operate on a specific instance, 
and it is a compile-time error to refer to `this` in a static property.

#### Nested types

TODO(static) any special rules?

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

TODO(static)

#### Constructors

TODO

#### Operators

##### Conversions

TODO(static)
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

TODO(instance)

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

TODO2(static) Open question on top-level nullability on underlying type.
TODO2(instance) disallow nullable annotation on base extension? Or at least need to clarify what `Extension?` means in various scenarios.

## Compat breaks

TODO2(static) types may not be called "extension" (reserved, break)  

## Lookup rules

TL;DR: For certain syntaxes (member access, element access), we'll fall back to an implicit extension member lookup.  

### Simple names

No changes to [simple names rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1174-simple-names) 
are needed. Member lookup on a type or value of extension type includes accessible members from its extended type.

### Member access

TL;DR: After doing an unsuccessful member lookup in a type,
we'll perform an extension member lookup for non-invocations
or attempt an extension invocation for invocations.

We modify the [member access rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1287-member-access) as follows:

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
  - \***~~Otherwise, `E.I` is an invalid member reference, and a compile-time error occurs.~~**

- \***If `E.I` is not invoked and `E` is classified as a type, if `E` is not a type parameter, 
  and if an ***extension member lookup*** of `I` in `E` with `K` type parameters produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- If `E` is a property access, indexer access, variable, or value, the type of which is `T`, 
  and a member lookup of `I` in `T` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:  
  ...
- \***(only relevant in Instance Phase) If `E.I` is not invoked and `E` is a property access, indexer access, variable, or value, 
  the type of which is `T`, where `T` is not a type parameter, and 
  an **extension member lookup** of `I` in `T` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- Otherwise, an attempt is made to process `E.I` as an \***extension invocation**.
  If this fails, `E.I` is an invalid member reference, and a binding-time error occurs.

Note: the path to extension invocation from this section is only for empty results from member lookup.
We can also get to extension invocation in:
1. invocation scenarios where the set of *applicable* candidate methods is empty.
2. indexer access scenarios where the set of *applicable* candidate indexers is empty.
3. TODO(static) there may be more scenarios (operator resolution, delegate conversion, natural function types)

That is covered below.

TODO3(static) Is the "where `T` is not a type parameter" portion still relevant?

### Method invocations

TL;DR: Instead of falling back to "extension method invocation" directly, we'll now fall back to "extension invocations" which replaces it.

We modify the [method invocations rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12892-method-invocations) as follows:

\[...]
- If the resulting set of candidate methods is empty, then further processing along the following steps are abandoned, and instead an attempt is made to process the invocation as \***an extension invocation**. If this fails, then no applicable methods exist, and a binding-time error occurs.
\[...]

### Extension invocations

We replace the [extension method invocations rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12893-extension-method-invocations)
with the following:

In an invocation of one of the forms

```csharp
«Type» . «identifier» ( )  
«Type» . «identifier» ( «args» )  
«Type» . «identifier» < «typeargs» > ( )  
«Type» . «identifier» < «typeargs» > ( «args» )
«expr» . «identifier» ( )  
«expr» . «identifier» ( «args» )  
«expr» . «identifier» < «typeargs» > ( )  
«expr» . «identifier» < «typeargs» > ( «args» )
```

if the normal processing of the invocation finds no applicable methods,
an attempt is made to process the construct as an invocation of an extension type member
or an extension method.  
If `expr` or any of te «args» has compile-time type `dynamic`, 
extensions (extension type members or extension methods) will not apply.

This succeeds if we find either:
- for the `Type` case,
  an substituted compatible implicit extension type `X` for `Type`
  so that the corresponding invocation can take place:
```csharp
X . «identifier» ( )
X . «identifier» ( «args» )
X . «identifier» < «typeargs» > ( )
X . «identifier» < «typeargs» > ( «args» )
```
- for the `expr` case where the expression has type `Type`,
  a substituted compatible implicit extension type `X` for `Type`
  so that the corresponding invocation can take place:
```csharp
((X)expr) . «identifier» ( )
((X)expr) . «identifier» ( «args» )
((X)expr) . «identifier» < «typeargs» > ( )
((X)expr) . «identifier» < «typeargs» > ( «args» )
```
- for the `expr` case, the best *type_name* `C`
  so that the corresponding static extension method invocation can take place:
```csharp
C . «identifier» ( «expr» )
C . «identifier» ( «expr» , «args» )
C . «identifier» < «typeargs» > ( «expr» )
C . «identifier» < «typeargs» > ( «expr» , «args» )
```

\[Extension method eligibility remains unchanged]

The search proceeds as follows:

- Starting with the closest enclosing type declaration, continuing with each type declaration,
  then continuing with each enclosing namespace declaration, and ending with
  the containing compilation unit, successive attempts are made:
  - If the given type, namespace or compilation unit directly contains extension types or methods,
    those will be considered first.
  - If namespaces imported by using-namespace directives in the given namespace or 
    compilation unit directly contain extension types or methods, those will be considered second.

TODO4(instance) need to merge extension members and extension methods
  - First, try extension types: 
    - Check which extension types in the current scope are compatible with the given underlying type `Type` and 
      collect resulting compatible substituted extension types.
    - Perform member lookup for `identifier` in each compatible substituted extension type.
      (note this takes into account that the member is invoked)
      (note this doesn't include members from the underlying type)
    - Merge the results
    - Next, members that are hidden by other members are removed from the set.  
      (note: "base types" means "base extensions and underlying type" for extension types)
    - Next, less specific extension members are removed if they are "hidden" by more specific extension members. 
      For every member `X.M` in the set, where `X` is the type in which the member `M` is declared,
      if no member in the set has a containing type `Y` that is more specific than `X` then the following rules are applied:
      - If `M` is a method, then all non-method members declared in a less specific type than `X` are removed from the set.
      - Otherwise, all members declared in a less specific type than `X` are removed from the set.
    - Finally, having removed hidden and less specific members:
      - If the set is empty, proceed to extension methods below.
      - If the set consists of a single member that is not a method, then:
        - If it is a value of a *delegate_type*, the *invocation_expression* 
          is evaluated as a delegate invocation.
        - If it is a value of a *function_pointer_type*, the *invocation_expression* 
          is evaluated as a function pointer invocation.
        - If it is a value of a type `dynamic`, the *invocation_expression* 
          is evaluated as a dynamic member invocation.
      - If the set contains only methods, we remove all the methods that are not 
        accessible or applicable (see "method invocations").
        - If the set is empty, proceed to extension methods below.
        - Otherwise, overload resolution is applied to the candidate methods:
          - If a single best method is found, the *invocation_expression* 
            is evaluated as the invocation of this method.
          - If no single best method is found, a compile-time error occurs.

  - Next, try extension methods (only for the `expr` case):
    - Check which extension methods in the current scope are eligible.
      - If the set is empty, proceed to the next enclosing scope.
      - Otherwise, overload resolution is applied to the candidate set. 
        - If a single best method is found, the *invocation_expression* 
          is evaluated as a static method invocation.
        - If no single best method is found, a compile-time error occurs.

  - Proceed to the next enclosing scope
- If no extension type member or extension method is found to be suitable for the invocation 
  in any enclosing scope, a compile-time error occurs.

The preceding rules mean:
- that instance methods take precedence over extension methods, 
- that extension type members available in a given namespace take precedence 
  over extension methods in that namespace,
- that extension type members available in inner namespace declarations take precedence
  over extension type members available in outer namespace declarations,
- that extension methods available in inner namespace declarations take precedence
  over extension methods available in outer namespace declarations, 
- that extension type members declared directly in a namespace take precedence
  over extension type members imported into that same namespace with a using namespace directive,
- and that extension methods declared directly in a namespace take precedence
  over extension methods imported into that same namespace with a using namespace directive.

TODO(static) clarify behavior for extension on `object` or `dynamic` used as `dynamic.M()`?

### Indexer access

TL;DR: If no candidate is applicable, then we attempt extension indexer access instead.

We modify the [element access rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#128111-general) as follows:

/[...]
An *element_access* is dynamically bound if \[...]
If the *primary_no_array_creation_expression* of an *element_access* is a value of an *array_type*, the *element_access* is an array access. 
Otherwise, the *primary_no_array_creation_expression* shall be a variable or value of a class, struct, or interface type  TODOTODO
that has one or more indexer members, in which case the *element_access* is an indexer access.
\***Otherwise, the *primary_no_array_creation_expression* shall be a variable or value of a class, struct, or interface type 
that has no indexer members, in which case the *element_access* is an extension indexer access.**

We modify the [indexer access rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#128113-indexer-access) as follows:

/[...]

The binding-time processing of an indexer access of the form `P[A]`, where `P` is a *primary_no_array_creation_expression* of a class, struct, or interface type `T`, and `A` is an *argument_list*, consists of the following steps:

- The set of indexers provided by `T` is constructed. \[...]
- The set is reduced to those indexers that are applicable and not hidden by other indexers. \[...]
- \***If the resulting set of candidate indexers is empty, then further processing 
  along the following steps are abandoned, and instead an attempt is made 
  to process the indexer access as an extension indexer access. If this fails, 
  then no applicable indexers exist, and a binding-time error occurs.**
- ~~If the resulting set of candidate indexers is empty, then no applicable indexers exist, and a binding-time error occurs.~~
- The best indexer of the set of candidate indexers is identified using the overload resolution rules. If a single best indexer cannot be identified, the indexer access is ambiguous, and a binding-time error occurs.
- /[...]

/[...]

#### Extension indexer access

In an element access of one of the forms

```csharp
«expr» [ ]
«expr» [ «args» ]
```

if the normal processing of the element access finds no applicable indexers, 
an attempt is made to process the construct as an extension indexer access. 
If «expr» or any of the «args» has compile-time type `dynamic`, extension methods will not apply.

This succeeds if, given that «expr» has underlying type `Type`, we find
a substituted compatible implicit extension type `X` for `Type`
so that the corresponding element access can take place:
```csharp
((X)expr) . «identifier» [ ]
((X)expr) . «identifier» [ «args» ]
```

The search proceeds as follows:
- If «expr» has an extension type, `Type` is the underlying type of that extension type. Otherwise,
 `Type` is the compile-time type of «expr».
- Starting with the closest enclosing type declaration, continuing with each type declaration,
  then continuing with each enclosing namespace declaration, and ending with
  the containing compilation unit, successive attempts are made:
  - If the given type, namespace or compilation unit directly contains extension types or methods,
    those will be considered first.
  - If namespaces imported by using-namespace directives in the given namespace or 
    compilation unit directly contain extension types or methods, those will be considered second.
  - Check which extension types in the current scope are compatible with the given underlying type `Type` and 
    collect resulting compatible substituted extension types.
  - The set of indexers is constructed from all indexers declared in each substituted extension type
    that are not override declarations and are accessible in the current context.
  - Merge the results
  - Next, members that are hidden by other members are removed from the set.  
    (note: "base types" means "base extensions and underlying type" for extension types)
  - Next, members that are not applicable with respect to the given **argument_list** are removed from the set.
  - Finally, having removed hidden and inapplicable members:
    - If the set is empty, proceed to the next enclosing scope.
    - Otherwise, overload resolution is applied to the candidate indexers:
      - If a single best indexer is found, the *element_access*
        is evaluated as the invocation of either the *get_accessor* or the *set_accessor* of the indexer.
      - If no single best indexer is found, a compile-time error occurs.

- If no extension indexer is found to be suitable for the element access
  in any enclosing scope, a compile-time error occurs.

### Member lookup (reviewed in LDM 2024-02-24)

TL;DR: Member lookup on an extension type includes members from its base extensions, its extended type and base types.  

We modify the [member lookup rules](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#125-member-lookup) 
as follows:

#### 12.5.2 Base types

For purposes of member lookup, a type `T` is considered to have the following base types:

- If `T` is `object` or `dynamic`, then `T` has no base type.
- If `T` is an *enum_type*, the base types of `T` are the class types `System.Enum`, `System.ValueType`, and `object`.
- If `T` is a *struct_type*, the base types of `T` are the class types `System.ValueType` and `object`.
- If `T` is a *class_type*, the base types of `T` are the base classes of `T`, including the class type `object`.
- If `T` is an *interface_type*, the base types of `T` are the base interfaces of `T` and the class type `object`.
- If `T` is an *array_type*, the base types of `T` are the class types `System.Array` and `object`.
- If `T` is a *delegate_type*, the base types of `T` are the class types `System.Delegate` and `object`.
- \***If `T` is an *extension_type*, the base types of `T` are the base extensions of `T` and the extended type of `T` and its base types.**

TODO(inheritance) will need to revisit once we have inheritance and we allow variance of extended types.

Note: this allows method groups that contain members from the extension and the extended type together:
```csharp
class U
{
    public void M2() { }
}

explicit extension R : U
{
    public void M2(int i) { }
    void M()
    {
        M2(); // find `U.M2()`
    }
}
```

Note: this also affects what members are considered shadowed, so that we don't get an overload resolution ambiguity in a scenario like this:
```csharp
class U
{
    public void M2() { }
}

explicit extension R : U
{
    public void M2() { } // warning: needs `new`
    void M()
    {
        M2(); // find `R.M2()`, no ambiguity
    }
}
```

### Compatible substituted extension types

TL;DR: We can determine whether an implicit extension is compatible with a given underlying type
and when successful this process yields one or more extension types we can use
(including required substitutions).  

An extension type `X` is compatible with given type `U` if:
- `X` is non-generic and its underlying type is `U`, a base type of `U` or an implemented interface of `U`
- a possible type substitution on the type parameters of `X` yields underlying type `U`, 
  a base type of `U` or an implemented interface of `U`.  

Note: it is possible for multiple substitutions on the type parameters of `X` to satisfy the condition above.

We call the resulting substituted types of `X` the "compatible substituted extension types".

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

Note: members from some other implicit extension type can apply to an extension type:
```csharp
explicit extension E1 for C
{
    void M()
    {
        this.M2(); // ok, E2 is compatible with type E1 since C is a base type of E1 and E2 extends C
    }
}
implicit extension E2 for C
{
    public void M2() { }
}
```

Note: there may be multiple substitutions for a given compatible extension type:
```csharp
_ = C.M; // ambiguous, C<int>.M and C<string>.M are both applicable

interface I<T> { }
class C : I<int>, I<string> { }

implicit extension E<T> for I<T>
{
    public static string M = null;
}
```

### Extension member lookup

TL;DR: Given an underlying type, we'll search enclosing types and namespaces 
(and their imports) for compatible extensions and for each "layer" we'll do member lookups.  
Given an extension type, we'll do an extension member lookup for its extended type.  

If the *member_access* occurs as the *primary_expression* of an *invocation_expression*, 
the member is said to be invoked.

Given a *member_access* of the form `E.I` and `T` the type of `E`, the objective
is to find an extension member `X.I` or an extension method group `X.I`, if possible.

We process as follows:
- We find `U` as the underlying type of `T`. If `T` is not an extension type, then `U` is `T`.
- Starting with the closest enclosing type declaration, continuing with each enclosing type declaration,
  then continuing with each enclosing namespace declaration, and ending with
  the containing compilation unit, successive attempts are made to find a candidate set of extension members:
  - If the given type, namespace or compilation unit directly contains extension types,
    those will be considered first.
  - If namespaces imported by using-namespace directives in the given namespace or 
    compilation unit directly contain extension types, those will be considered second.
- Build a set of extension methods and extension types members: 
  - If `E` is a value (not a type) and if the scope contains eligible extension methods, 
    then merge this set into the result of the lookup. 
    TODO4 this doesn't fit, as eligibility is based on applicability which requires arguments
  - Look for extension type members:
    - Check which extension types are compatible with the given underlying type `U` and 
      collect resulting compatible substituted extension types.
    - Perform member lookup for `I` in each compatible substituted extension type `X` 
      (note this takes into account whether the member is invoked).
    - Merge the results
  - Next, members that are hidden by other members are removed from the set.  
    (note: "base types" means "base extensions and underlying type" for extension types)
  - Next, less specific extension members are removed if they are "hidden" by more specific extension members.  
    For every member `X.M` in the set, where `X` is the extension type in which the member `M` is declared, 
    if no member in the set has a containing type `Y` that is more specific than `X` then the following rules are applied:
    - If `M` is a constant, field, property, event, or enumeration member, 
      then all members declared in a less specific type than `X` are removed from the set.
    - If `M` is a type declaration, then all non-types declared in a less specific type than `X` are removed from the set, 
      and all type declarations with the same number of type parameters as `M` declared in a less specific type than `X` are removed from the set.
    - If `M` is a method, then all non-method members declared in a less specific type than `X` are removed from the set.
- Finally, having removed hidden members, the result of the lookup is determined:
  - If the set is empty, proceed to the next enclosing scope.
  - If the set consists of a single member that is not a method,
    then this member is the result of the lookup.
  - Otherwise, if the set contains only methods,
    then this method group is the result of the lookup.
  - Otherwise, the lookup is ambiguous, and a binding-time error occurs.
- Otherwise, continue the search through namespaces and their imports.
- If no candidate set is found in any enclosing namespace declaration or compilation unit, 
  the result of the lookup is empty.

TODO3(static) explain static usings:
  meaning of `using static SomeType;` (probably should look for extension types declared within `SomeType`)
  meaning of `using static Extension;`

```
using static ClassicExtensionType;
using static NewExtensionType; // What should this do? 
// Are the static methods from that extension in scope?
```

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

### Less specific extension type

TL;DR: As part of extension member lookup, extension invocations and overload resolution,
we consider that members from "less specific" extension types are "hidden" by members from "more specific" extension types.

If `X` extends `C` and `Y` extends `D`, `X` is considered less specific than `Y`
when:
- `C` is a base type of `D`, or
- `C` is an interface implemented by `D`.

For example:
```csharp
C.M(42); // extension method E2.M is preferred as E1.M is less specific

class Base { }

class C : Base { }

implicit extension E1 for Base
{
    public static int M(int i) => throw null;
}

implicit extension E2 for C
{
    public static int M(int i) => i;
}
```

```csharp
_ = C.P; // extension property E2.P is preferred as E1.P is less specific

class Base { }

class C : Base { }

implicit extension E1 for Base
{
    public static int P => throw null;
}

implicit extension E2 for C
{
    public static int P => i;
}
```

### 12.6.4 Overload resolution

We update the [overload resolution section](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12641-general) as follows:

Each of these contexts defines the set of candidate function members and the list of arguments in its own unique way. 
For instance, the set of candidates for a method invocation does not include methods marked override, 
methods in a base class are not candidates if any method in a derived class is applicable,
\***and extension methods in a less specific extension type are not candidates if any extension method in a more specific extension type is applicable**.

### Natural function type

The rules for determining the [natural function type of a method group](https://github.com/dotnet/csharplang/blob/main/proposals/method-group-natural-type-improvements.md) are modified as follows:

1. For each scope, we construct the set of all candidate methods:
  - for the initial scope, methods on the relevant type with arity matching the provided type arguments and satisfying constraints with the provided type arguments are in the set if they are static and the receiver is a type, or if they are non-static and the receiver is a value
    - extension methods in that scope that can be substituted with the provided type arguments and reduced using the value of the receiver while satisfying constraints are in the set
    - \***methods from compatible implicit extension types applicable in that scope which can be substituted with the provided type arguments and satisfying constraints with those are in the set**
  1. If we have no candidates in the given scope, proceed to the next scope.
  2. If the signatures of all the candidates do not match, then the method group doesn't have a natural type
  3. Otherwise, resulting signature is used as the natural type
2. If the scopes are exhausted, then the method group doesn't have a natural type

Note: extension types members and extension methods are considered on par, as illustrated by this example:  
```
var x = new C().M; // no natural function type

class C { }

implicit extension E for C
{
	public static void M() { }
}

static class Extensions
{
	public static void M(this C c, int i) { }
}
```

TODO4(instance) should we disambiguate when signatures match?
```
var x = new C().M; // ambiguous

class C { }

implicit extension E for C
{
    public static void M() { }
}

static class Extensions
{
    public static void M(this C c) { }
}
```

TODO4 would like to brainstorm tweaks to member access and natural function type to make this work better:

Note: by the current rules, there are some unfortunate interactions between member access and natural function type.
> Note: When the result of such a member lookup is a method group and K is zero, 
> the method group can contain methods having type parameters. 
> This allows such methods to be considered for type argument inferencing. end note
```
var x = C.Member; // error: member lookup finds C.Member (method group) and lacks type arguments to apply to that match

class C 
{
    public static void Member<T>() { }
}

implicit extension E for C
{
    public static int Member = 42;
}
```

### Identical simple names and type names

For context see [Identical simple names and type names](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/primary-constructors.md#identical-simple-names-and-type-names).
TODO3

### Base access

TODO(instance) review with LDM  

We'll start by disallowing [base access](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12814-base-access) 
within extension types.  
Casting seems an adequate solution to access hidden members: `((R)r2).M()`.  
TODO(instance) Maybe `base.` could refer to underlying value.   

### Method invocations

The change to the Base Types section also affects the [method invocation rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12892-method-invocations):

> The set of candidate methods is reduced to contain only methods from the most derived types:
> For each method `C.F` in the set, where `C` is the type in which the method `F` is declared, all methods declared in a base type of `C` are removed from the set.

For example:

```csharp
E.Method(); // picks `E.Method` over `C.Method`

static class C
{
    public static void Method() => throw null;
}

static explicit extension E for C
{
    public static void Method() { } // picked
}
```

### Element access

TODO3(instance) write this section, including preference for more specific extension indexers

TL;DR: For non-extension types, we'll fall back to an implicit extension member lookup. For extension types, we include indexers from the underlying type.

We modify the [indexer access](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#117103-indexer-access) section as follows:

For an indexer access, the *primary_no_array_creation_expression* of the *element_access* shall be a variable or value 
of a class, struct, interface, /***or extension** type, and this type shall implement one or more indexers that are 
applicable with respect to the *argument_list* of the *element_access*.

The binding-time processing of an indexer access of the form `P[A]`, where `P` is a *primary_no_array_creation_expression* 
of a class, struct, interface, /***or extension** type `T`, and `A` is an *argument_list*, consists of the following steps:

- The set of indexers provided by `T` is constructed. The set consists of all indexers declared in `T` or a base type of `T` that are not override declarations and are accessible in the current context.
- The set is reduced to those indexers that are applicable and not hidden by other indexers. The following rules are applied to each indexer `S.I` in the set, where `S` is the type in which the indexer `I` is declared:
  - If `I` is not applicable with respect to `A`, then `I` is removed from the set.
  - If `I` is applicable with respect to `A`, then all indexers declared in a base type of `S` are removed from the set.
  - If `I` is applicable with respect to `A` and `S` is a class type other than `object`, all indexers declared in an interface are removed from the set.
- If the resulting set of candidate indexers is empty, then no applicable indexers exist, and a binding-time error occurs.
- The best indexer of the set of candidate indexers is identified using the overload resolution rules. If a single best indexer cannot be identified, the indexer access is ambiguous, and a binding-time error occurs.
- The index expressions of the *argument_list* are evaluated in order, from left to right. The result of processing the indexer access is an expression classified as an indexer access. The indexer access expression references the indexer determined in the step above, and has an associated instance expression of `P` and an associated argument list of `A`, and an associated type that is the type of the indexer. If `T` is a class type, the associated type is picked from the first declaration or override of the indexer found when starting with `T` and searching through its base classes.

### Operators

TODO(static)
User-defined conversion should be allowed, except where it conflicts with a built-in
conversion (such as with an underlying type).  

### Collection initializers

TODO(instance)
https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#128164-collection-initializers
Explain how extension types factor in when resolving `Add` calls.

### Method group conversions

TODO4 spec this section

https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/conversions.md#108-method-group-conversions
TODO A single method is selected corresponding to a method invocation, 
but with some tweaks related to normal form and optional parameters.  

### Pattern-based invocations and member access

TODO(static) Need to scan through all pattern-based rules for known members to consider whether to include extension type members.
  If extension methods were already included, then we should certainly include extension type methods.
  Otherwise, we should consider it.
  - `Deconstruct` in deconstruction and patterns
  - `GetEnumerator`, `Current` and `MoveNext` in `foreach`
  - CollectionBuilder in collection expressions
  - `Add` in collection initializers
  - `GetPinnableReference` in fixed statements
  - `GetAwaiter` in `await` expressions
  - `Dispose` in `using` statements

### Simple assignment

The current [simple assignment rules](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12212-simple-assignment) state:

> When a property or indexer declared in a struct_type is the target of an assignment,
> the instance expression associated with the property or indexer access shall be classified as a variable.
> If the instance expression is classified as a value, a binding-time error occurs.

We're expecting those rules to be updated to check whether the receiver is a reference or value type instead.
Then the rule will also apply to extension properties/indexers/events.

TODO update this section once the rules are spelled out for https://github.com/dotnet/csharpstandard/issues/1078

```
1.Property = 42; // Error reporting that 1 (which is a struct) is not a variable

implicit extension E for int
{
    public int Property { set => throw null; }
}
```

# Implementation details

Extensions are emitted as structs with an extension marker method and an instance field for non-static extensions.  
The type is marked with Obsolete and CompilerFeatureRequired attributes.  TODO we'll need to relax that

## Marker method

The extension marker method encodes the underlying type and base extensions as parameters in that order.  
It is private and static, and is called `<ImplicitExtension>$` for implicit extensions and 
`<ExplicitExtension>$` for explicit extensions.  

For example: `implicit extension R for UnderlyingType : BaseExtension1, BaseExtension2` yields
`private static void <ImplicitExtension>$(UnderlyingType, BaseExtension1, BaseExtension2)`.  

## Instance field

If the extension not static, then we emit a private instance field of the underlying type
into the struct.  
We use the default layout for structs (sequential layout, with pack and size 0)
ensuring that the single instance field is placed at offset zero.

Note: Although we could not find an explicit statement to that effect,
this behavior falls out from ECMA 335 (II.10.1.2), which states for sequential layout:
> The CLI shall lay out the fields in sequential order, based on the order of the fields in the logical metadata table (§II.22.15).

> [Rationale: ... sequential layout is intended to instruct the CLI to match layout rules commonly followed by languages like C and C++
> on an individual platform, where this is possible while still guaranteeing verifiable layout. ...]

and from the C99 standard section 6.7.2.1 bullet point 13 (gated):

> Within a structure object, the non-bit-field members and the units in which bit-fields 
> reside have addresses that increase in the order in which they are declared. 
> A pointer to a structure object, suitably converted, points to its initial member 
> (or if that member is a bit-field, then to the unit in which it resides), 
> and vice versa. There may be unnamed padding within a structure object, but not at its beginning.

For example `implicit extension E for UnderlyingType` yields
```csharp
struct E
{
	private UnderlyingType <UnderlyingInstance>$;
	private static void <ImplicitExtension>$(UnderlyingType) { }
}
```

## Instance invocations

The design and layout of the instance field will allow us to re-interpret an instance of `UnderlyingType`
as an instance of `E` with `public static ref TTo Unsafe.As<TFrom,TTo>(ref TFrom source)`.  

If the receiver of an extension member invocation on a value of the underlying type is `r`,
we will replace it with `Unsafe.As<UnderlyingType, E>(ref r)`.
If `r` does not refer to a location, a temporary variable will be created and initialized, and its reference
will be used.

The re-interpreted receiver will be only computed once where possible. For example, in a compound assignment
`r.P += value;`.

TODO we may wrap this method in a compiler-generated helper to increase verifiability of the generated code.  
TODO there is also a verifiability issue with taking a reference to `this` (which is readonly)

## Type references

TODO4 this section is not finalized

Extensions appearing in signatures are emitted as the extension's underlying type
marked with a modopt of the extension type.

```
void M(Extension r) // emitted as `void M(modopt(Extension) UnderlyingType r)`
```

## Phases 

### Static extension members

In this first subset of the feature, only static members are allowed in extension types.  

### Instance extension members

In this second subset of the feature, instance members become allowed.

## Inheritance

In this third subset of the feature, extensions are allowed to have base extensions.

## Interfaces

In this final subset of the feature, extensions are allowed to implement interfaces.

### Extension type members

The restrictions on modifiers from Static Phase remain (`new`).  
Non-static members become allowed in Instance Phase.  

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

