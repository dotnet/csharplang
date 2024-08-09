# Extension types

## Open issue: merging extension methods and extension members

We need to revise the preference of extension types over extension methods (should mix and disambiguate duplicates if needed instead)

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

Confirm what happens when we have different kinds of members:

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

## Open issue: migration from classic extension methods

https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-26.md#conclusion
For instance members, we will explore making the emit binary-compatible, 
and how much we will have to overhaul to get that to work, 
then come back and make a final decision on whether to block consumption.

## Open issue: how much support from other languages?

We're currently blocking usage of instance members from other languages
by using a `modreq`.

## Open issue: readonly members

Currently, readonly members are disallowed in extensions. This means that
a `ref readonly` variable cannot be used as the instance argument for an extension member without cloning.  
We should consider allowing `readonly` members.

```
var s = new S() { field = 42 };
M(in s);
System.Console.Write(s.field); // we can observe whether the receiver was cloned or not

void M(in S s)
{
    s.M();
}

public struct S
{
    public int field;
    public void Increment() { field++; }
}

public implicit extension E for S
{
    public void M() // readonly modifier is currently disallowed
    {
        this.Increment();
    }
}
```

## Open issue: disambiguating between extension type method and classic extension method

Should we disambiguate when signatures match?
```csharp
var x = new C().M; // ambiguous
new C().M(); // ambiguous once we mix all extensions

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

## Open issue: behavior of `static using` directives

We'll need to update "extension member lookup section" and "using static directives" to allow the following two scenarios:

```csharp
using static C;
using static D;

Nested1.M(); // Nested1 should be in scope too (as if it were declared as a static member of C)
Nested2.M();

class C { }
implicit extension E for C
{
    public class Nested1 { public static void M() { }  }
}

class D { public class Nested2 { public static void M() { } }}
```

```csharp
using static Extension;

// M and Nested should be in scope

explicit extension Extension for object
{
    public static void M() { }
    public class Nested { }
}
```

## Open issue: what kind of type is an extension type?

Many sections of the spec need to consider the kind of type. Consider a few examples:  
- the spec for a conditional element access `P?[A]B`
considers whether `P` is a nullable value type. So it will need to handle the case
where `P` is an instance of an extension type on a nullable value type,
- the spec for an object creation considers whether the type is
a value_type, a type_parameter, a class_type or a struct_type,
- the spec for satisfying constraints also consider what kind of type we're dealing with.

It may be possible to address all those cases without changing each such section of the spec,
but rather by adding general rules ("an extension on a class type is considered a class type" or some such).

## Open issue: specify semantics for type parameter receivers

The `this` access section needs to be updated to handle type parameters.  

The implementation details section needs to be updated to explain how
we capture the receiver when the type parameter is a reference type.  

```csharp
var o = new object();
o.M(o = null);

implicit extension E<T> for T
{
    // emitted as `void M(ref modreq(ExtensionAttribute) T, object)`
    void M(object x) { this.ToString(); }
}
```

## Open issue: type erasure

Confirm the format with partner teams.  
Monitor the volume of metadata with some preview usage.  
Finalize the format to support tuple names, dynamic, nullability and other erased information.  
Confirm how the encoding works with local functions.  

## Open issue: consider disallowing pointers to extension types

This question was raised in LDM 2024-07-22 but was not resolved.

## Open issue: allow variance in implicit extension compatibility

The current rules are pretty strict:
> a possible type substitution on the type parameters of `X` yields underlying type `U`, 
>  a base type of `U` or an implemented interface of `U`.  

Whereas extension method invocation says:
> An implicit identity, reference or boxing conversion exists from expr to the type of the first parameter of Mₑ.

We'll likely want to allow variance such as `object` vs. `dynamic`, tuple names, nullability.

Also, we may want to allow for variance (see example below).
But this would involve considering conversion when evaluating compatibility.  
```csharp
IEnumerable<string>.M();

implicit extension E for IEnumerable<object> 
{
    public static void M() { }
}
```

## Open issue: need to specify nullability analysis rules

Should we allow top-level nullability on underlying type?  

```csharp
implicit extension E for object? { }
```

What is the nullability of `this` within an extension member?  

```csharp
implicit extension E for object
{
    public void M()
    {
        this.ToString(); // is this safe?
    }
}
```

```csharp
object? x = null;
x.M(); // is this safe?
```

```csharp
C<object>.M(); // warn?
implicit extension E for C<object?>
{
    public static void M() { }
}
```

## Open issue: need to specify lookup rules within attributes

Need to spec why extension properties are not found during lookup for attribute properties, or explicitly disallow them  

## Open issue: allow attributes on extensions

From WG discussion, we'd allow attributes on extensions types, using the AttributeTargets.Class target,
since extensions are emitted as static classes.

## Open issue: extension member lookup in usings

We don't resolve implicit extension members in usings, due to cycles.  
This needs to be investigated further to disentangle whether all cycles are implementation-specific
or some are inherent to the language feature.

## Open issue: need to specify scoping rules for `for UnderlyingType`

Adjust scoping rules so that type parameters are in scope within the 'for':
```csharp
implicit extension E<T> for T { }
```

## Open issue: need to specify user-defined conversion rules

```
implicit extension EU for U
{
    public static implicit operator EU(int i) => ...;
}
implicit extension EInt for int { }

R r = 42;
U u = 43;

EInt ei = 44;
R r = ei;
U u = ei;
```

Should allow conversion operators. Extension conversion is useful. 
Example: from `int` to `string` (done by `StringExtension`).  
But we should disallow user-defined conversions from/to underlying type 
or inherited extensions, because a conversion already exists.  
Conversion to interface still disallowed.  
Could we make the conversion to be explicit identity conversion instead
of implicit?  
User-defined conversion should be allowed, except where it conflicts with a built-in
conversion (such as with an underlying type).  

## Open issue: need to specify constructor and operator members

Do we want to allow constructors and `required` properties?  
Do we want to allow operators?  

## Open issue: need to specify type inference rules

We need to review Method type inference: 7.5.2.9 Lower-bound interfaces, to see whether any updates are needed.  

We want to allow the following and decide the inferred type:
```csharp
var c = new C();
var e = (E)c; // E is an extension type
M(c, e); // M<C> or M<E>?

void M<T>(T t1, T t2) { }
```

## Open issue: naming convention?

Should we have a naming convention like `Extension` suffixes? (`DataObjectExtension`)  

## Open issue: need to specify pattern-based invocations and member access

We need to scan through all pattern-based rules for known members to consider whether to include extension type members.
  If extension methods were already included, then we should certainly include extension type methods.
  Otherwise, we should consider it.
  - `Deconstruct` in deconstruction and patterns
  - `GetEnumerator`, `Current` and `MoveNext` in `foreach`
  - CollectionBuilder in collection expressions
  - `Add` in collection initializers
  - `GetPinnableReference` in fixed statements
  - `GetAwaiter` in `await` expressions
  - `Dispose` in `using` statements

## Open issue: need to disallow extensions within interfaces with variant type parameters  

Will need to check with the WG as I don't recall the reasoning for this.

## Open issue: consider allowing ref structs as underlying types

The static+this codegen strategy should work for ref structs and classic extension methods are allowed on ref structs.

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
beyond today's extension methods, and potentially for "extension interfaces" in the future.

### Augmentation scenarios

Augmentation scenarios allow existing values to be seen through the lens of
a "stronger" type - an extension that provides additional function members on the value.

### Adaptation scenarios

Adaptation scenarios allow existing values to be adapted to existing interfaces,
where an extension provides details on how the interface members are implemented.  
Those are not out-of-scope for this document.  

## Design

The syntax for extensions is as follows:

```antlr
type_declaration
    | extension_declaration // add
    | ...
    ;

extension_declaration
    : extension_modifier* ('implicit' | 'explicit') 'extension' identifier type_parameter_list? ('for' type)? type_parameter_constraints_clause* extension_body
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

## Extension type

An extension type (new kind of type) is declared by a *extension_declaration*.  

The extension type does not **inherit** members from its underlying type 
(which may be `sealed` or a struct), but
the lookup rules are modified to achieve a similar effect (see below).  

There is an identity conversion between an extension and its underlying type.

An extension type satisfies the constraints satisfied by its underlying type (see section on constraints). 

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

```csharp
struct U { }
explicit extension X for U { }
```
"X has underlying type U"  
"X extends U"  

### Accessibility constraints

We modify the [accessibility constraints](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/basic-concepts.md#755-accessibility-constraints) as follows:

The following accessibility constraints exist:
- [...]
- ***The underlying type of an extension type shall be at least as accessible as the extension type itself.**

Note those also apply to visibility constraints of file-local types (not yet specified).

### Extension type members

The extension type members may not use the `virtual`, `abstract`, `sealed`, `override` modifiers,
or the `protected` access modifier.  
Member methods may not use the `readonly` modifier.  
The `new` modifier is allowed and the compiler will warn that you should
use `new` when shadowing.  

An extension cannot contain a member declaration with the same name as the extension.

#### Signatures, overloading and hiding

The existing [rules for signatures](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/basic-concepts.md#76-signatures-and-overloading) apply.  
Two signatures differing by an extension vs. its underlying type are considered to be the *same signature*.

Shadowing includes underlying type.  

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

#### Constants

Existing [rules for constants](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#144-constants) 
apply (so duplicates or the `static` modifier are disallowed).

#### Fields

A *field_declaration* in an *extension_declaration* shall explicitly include a `static` modifier.  
Otherwise, existing [rules for fields](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#145-fields) apply.  

#### Methods

Parameters with the `this` modifier are disallowed.
Otherwise, existing [rules for methods](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#146-methods) apply.
In particular, a static method does not operate on a specific instance, 
and it is a compile-time error to refer to `this` in a static method.  

We modify the [extension methods rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#14610-extension-methods) as follows:

[...] The first parameter of an extension method may have no modifiers other than `this`, 
and the parameter type may not be a pointer **or an extension** type.

#### Properties

Auto-properties must be static (since instance fields are disallowed).  

Existing [rules for properties](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#147-properties) apply.
In particular, a static property does not operate on a specific instance, 
and it is a compile-time error to refer to `this` in a static property.

#### Nested types

```
extension Extension : UnderlyingType
{
    class NestedType { }
}
class UnderlyingType { }

UnderlyingType.NestedType x = null; // okay
```

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
  - ***`A` is an extension type with an underlying type that satisfies the reference type constraint.**
- If the constraint is the value type constraint (`struct`), the type `A` shall satisfy one of the following:
  - `A` is a `struct` type or `enum` type, but not a nullable value type.
  - `A` is a type parameter having the value type constraint.
  - ***`A` is an extension type with an underlying type that satisfies the value type constraint.**
- If the constraint is the constructor constraint `new()`, 
  the type `A` shall not be `abstract` and shall have a public parameterless constructor. 
  This is satisfied if one of the following is true:
  - `A` is a value type, since all value types have a public default constructor.
  - `A` is a type parameter having the constructor constraint.
  - `A` is a type parameter having the value type constraint.
  - `A` is a `class` that is not abstract and contains an explicitly declared public constructor with no parameters.
  - `A` is not `abstract` and has a default constructor.
  - ***`A` is an extension type with an underlying type that satisfies the constructor constraint.**

A compile-time error occurs if one or more of a type parameter’s constraints are not satisfied by the given type arguments.

By the existing [rules on type parameter constraints](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#1425-type-parameter-constraints)
extensions are disallowed in constraints (an extension is neither a class or an interface type).

```
where T : Extension // error
```

TODO Does this restriction on constraints cause issues with structs?

## Compat breaks

Types and aliases may not be called "extension".  

## Types

We update the [Types section](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/types.md#821-general) as follows:

A reference type is a class type, an interface type, an array type, a delegate type, the dynamic type,
***or an extension type with an underlying type that is a reference type**.

A value type is either a struct type or an enumeration type,
***or an extension type with an underlying type that is a value type**.

***An extension type with an underlying type that is an enumeration type is an enumeration type.**

All value types ***except extension types** implicitly inherit from the class System.ValueType

A nullable value type can represent all values of its underlying type plus an additional null value. 
A nullable value type is written `T?`, where T is the underlying type. 
This syntax is shorthand for `System.Nullable<T>`, and the two forms can be used interchangeably.
***An extension type with an underlying type that is a nullable value type is also a nullable value type.**

## Conversions

We update the [Conversions section](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#10-conversions) as follows:
TODO2

## Expressions

### Primary expressions

TL;DR: For certain syntaxes (member access, element access), we'll fall back to an implicit extension member lookup.  

#### Simple names

No changes to [simple names rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1174-simple-names) 
are needed. Member lookup on a type or value of extension type includes accessible members from its extended type.

#### Member access

TL;DR: After doing an unsuccessful member lookup in a type,
we'll perform an extension member lookup for non-invocations
or attempt an extension invocation for invocations.

We modify the [member access rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1287-member-access) as follows:

The member_access is evaluated and classified as follows:
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
  - ***~~Otherwise, `E.I` is an invalid member reference, and a compile-time error occurs.~~**

- ***If `E.I` is not invoked and `E` is classified as a type, if `E` is not a type parameter, 
  and if an ***extension member lookup*** of `I` in `E` with `K` type parameters produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- If `E` is a property access, indexer access, variable, or value, the type of which is `T`, 
  and a member lookup of `I` in `T` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:  
  ...
- ***If `E.I` is not invoked and `E` is a property access, indexer access, variable, or value, 
  the type of which is `T`, and 
  an **extension member lookup** of `I` in `T` with `K` type arguments produces a match, 
  then `E.I` is evaluated and classified as follows:**  
  ...
- Otherwise, an attempt is made to process `E.I` as an ***extension invocation**.
  If this fails, `E.I` is an invalid member reference, and a binding-time error occurs.

Note: the path to extension invocation from this section is only for empty results from member lookup.

Note: We allow static lookups on type parameters only for members that are static virtual members. 
Since extensions members are not virtual, we don't allow static extension lookups on type parameters either.
```
void M<T>(T t)
{
    T.MStatic() // disallow
    t.MInstance() // allow
}
implicit extension E for U
{
    public static void MStatic() { }
    public void MInstance() { }
}
```

#### Method invocations

TL;DR: Instead of falling back to "extension method invocation" directly, we'll now fall back to "extension invocations" which replaces it.

We modify the [method invocations rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12892-method-invocations) as follows:

[...]
- If the resulting set of candidate methods is empty, then further processing along the following steps are abandoned, and instead an attempt is made to process the invocation as ***an extension invocation**. If this fails, then no applicable methods exist, and a binding-time error occurs.
[...]

Note: the change to the Base Types section also affects the method invocation rules:

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

#### Extension invocations

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

[Extension method eligibility remains unchanged]

The search proceeds as follows:

- Starting with the closest enclosing type declaration, continuing with each type declaration,
  then continuing with each enclosing namespace declaration, and ending with
  the containing compilation unit, successive attempts are made:
  - If the given type, namespace or compilation unit directly contains extension types or methods,
    those will be considered first.
  - If namespaces imported by using-namespace directives in the given namespace or 
    compilation unit directly contain extension types or methods, those will be considered second.
  - First, try extension types: 
    - Check which extension types in the current scope are compatible with the given underlying type `Type` and 
      collect resulting compatible substituted extension types.
    - Perform member lookup for `identifier` in each compatible substituted extension type.
      (note this takes into account that the member is invoked)
      (note this doesn't include members from the underlying type)
    - Merge the results
    - Next, members that are hidden by other members are removed from the set.  
      (note: "base types" means "underlying type" for extension types)
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

#### Indexer access

TL;DR: For non-extension types, we'll fall back to an implicit extension member lookup. For extension types, we include indexers from the underlying type.

We modify the [element access rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#128111-general) as follows:

An *element_access* is dynamically bound if [...]
If the *primary_no_array_creation_expression* of an *element_access* is a value of an *array_type*, the *element_access* is an array access. 
Otherwise, the *primary_no_array_creation_expression* shall be a variable or value of a class, struct, interface, ***or extension** type
that has one or more indexer members, in which case the *element_access* is an indexer access.
***Otherwise, the *primary_no_array_creation_expression* shall be a variable or value of a class, struct, or interface type 
that has no indexer members, in which case the *element_access* is an extension indexer access.**

We modify the [indexer access rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#128113-indexer-access) as follows:

For an indexer access, the *primary_no_array_creation_expression* of the *element_access* shall be a variable or value 
of a class, struct, interface, ***or extension** type, and this type shall implement one or more indexers that are 
applicable with respect to the *argument_list* of the *element_access*.

The binding-time processing of an indexer access of the form `P[A]`, where `P` is a *primary_no_array_creation_expression* 
of a class, struct, interface, ***or extension** type `T`, and `A` is an *argument_list*, consists of the following steps:

- The set of indexers provided by `T` is constructed. 
  The set consists of all indexers declared in `T` or a base type of `T` that are not override declarations and are accessible in the current context.
- The set is reduced to those indexers that are applicable and not hidden by other indexers.
  (note: "base types" means "underlying type" for extension types)
  [...]
- ***If the resulting set of candidate indexers is empty, then further processing 
  along the following steps are abandoned, and instead an attempt is made 
  to process the indexer access as an extension indexer access. If this fails, 
  then no applicable indexers exist, and a binding-time error occurs.**
- ~~If the resulting set of candidate indexers is empty, then no applicable indexers exist, and a binding-time error occurs.~~
- The best indexer of the set of candidate indexers is identified using the overload resolution rules. 
  If a single best indexer cannot be identified, the indexer access is ambiguous, and a binding-time error occurs.
- [...]

```csharp
new C()[42]; // binds to C.this[int] from instance type
new E()[42]; // binds to C.this[int] from underlying type
new C()[""]; // binds to C.this[string] from instance type
new E()[""]; // binds to E.this[string] (extension indexer access)

class C
{
    public int this[string s] => throw null;
    public int this[int i] => throw null;
}

implicit extension E for C
{
    public new int this[string s] => throw null;
}
```

#### Extension indexer access
TODO write this section, including preference for more specific extension indexers

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
    (note: "base types" means "underlying type" for extension types)
  - Next, members that are not applicable with respect to the given **argument_list** are removed from the set.
  - Finally, having removed hidden and inapplicable members:
    - If the set is empty, proceed to the next enclosing scope.
    - Otherwise, overload resolution is applied to the candidate indexers:
      - If a single best indexer is found, the *element_access*
        is evaluated as the invocation of either the *get_accessor* or the *set_accessor* of the indexer.
      - If no single best indexer is found, a compile-time error occurs.

- If no extension indexer is found to be suitable for the element access
  in any enclosing scope, a compile-time error occurs.

#### This access

We modify the [this access rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12813-this-access) as follows:

A this_access has one of the following meanings:
- [...]
- ***When this is used in a primary_expression within an instance method or instance accessor of an extension with an underlying type known to be a reference type,
  it is classified as a value. The type of the value is the instance type of the extension within which the usage occurs, 
  and the value is a reference to the object for which the method or accessor was invoked.**
- ***When this is used in a primary_expression within an instance method or instance accessor of an extension with an underlying type known to be a value type, 
  it is classified as a variable. The type of the variable is the instance type of the extension within which the usage occurs.
  - If the method or accessor is not an iterator or async function, the `this` variable represents the extension for which the method or accessor was invoked.
    - If the value type is a readonly struct, the `this` variable behaves exactly the same as an `in` parameter of the struct type
    - Otherwise the `this` variable behaves exactly the same as a `ref` parameter of the value type
  - If the method or accessor is an iterator or async function, the `this` variable represents a copy of the value
    for which the method or accessor was invoked, and behaves exactly the same as a value parameter of the value type.**

#### Base access

We'll start by disallowing [base access](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12814-base-access) 
within extension types.  
Casting seems an adequate solution to access hidden members: `((R)r2).M()`.  
In the future, maybe `base.` could refer to underlying value.   

### Member lookup (reviewed in LDM 2024-02-24)

TL;DR: Member lookup on an extension type includes members from its extended type and base types.  

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
- ***If `T` is an *extension_type*, the base types of `T` are the extended type of `T` and its base types.**

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

#### Compatible substituted extension types

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

#### Extension member lookup

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
    (note: "base types" means "underlying type" for extension types)
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

#### Less specific extension type

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
***and extension methods in a less specific extension type are not candidates if any extension method in a more specific extension type is applicable**.

### Natural function type

The rules for determining the [natural function type of a method group](https://github.com/dotnet/csharplang/blob/main/proposals/method-group-natural-type-improvements.md) are modified as follows:

1. For each scope, we construct the set of all candidate methods:
  - for the initial scope, methods on the relevant type with arity matching the provided type arguments and satisfying constraints with the provided type arguments are in the set if they are static and the receiver is a type, or if they are non-static and the receiver is a value
    - extension methods in that scope that can be substituted with the provided type arguments and reduced using the value of the receiver while satisfying constraints are in the set
    - ***methods from compatible implicit extension types applicable in that scope which can be substituted with the provided type arguments and satisfying constraints with those are in the set**
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

### Identical simple names and type names

For context see [Identical simple names and type names](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/primary-constructors.md#identical-simple-names-and-type-names).
TODO3

### Method group conversions

No changes to [method group conversion rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/conversions.md#108-method-group-conversions).  
The extension behavior falls out of method invocation rules.

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

Extensions are emitted as static class types with an extension marker method.  

The extension marker method encodes the underlying type as parameter.  
It is public and static, and is called `<ImplicitExtension>$` for implicit extensions and 
`<ExplicitExtension>$` for explicit extensions.  
It allows roundtripping of extension symbols through metadata (full and reference assemblies).  

For example: `implicit extension R for UnderlyingType` yields
`public static void <ImplicitExtension>$(UnderlyingType)`.  

Instance method/property/indexer declarations in source are represented as static declarations in metadata:
  - A new parameter is added at the beginning, it represents `this` with erased extension type.
  - The parameter's name is unspeakable
  - The type of the parameter is the extended type
  -  A `modreq(System.Runtime.CompilerServices.ExtensionAttribute)` is added to the type.
     On import the presence of the modreq is checked by verifying fully qualified name of the ExtensionAttribute type.
     Location of the type and its other properties are not checked.
  - The parameter is a 'ref' parameter, unless its type is known to be a reference type

A method example:
``` C#
public implicit extension E for C
{
    public void Method()
    {
    }
}

public class C {}
```
``` IL
	.method public hidebysig static 
		void Method (
			class C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute) '<>4__this'
		) cil managed 
```

Other tools and compilers that follow a requirement to not consume APIs with an unknown modreq will be unable to consume the method. Including VB
compiler and previous C# compiler.

A property example: 
``` C#
public implicit extension E for C
{
    public int P1
    {
        get => ...;
        set => ...; 
    }
}

public struct C
{
}
```
``` IL
	.method public hidebysig specialname static 
		int32 get_P1 (
			valuetype C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute)& '<>4__this'
		) cil managed 

 	.method public hidebysig specialname static 
		void set_P1 (
			valuetype C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute)& '<>4__this',
			int32 'value'
		) cil managed 

 	.property int32 P1(
		valuetype C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute)& '<>4__this'
	)
	{
		.get int32 E::get_P1(valuetype C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute)&)
		.set void E::set_P1(valuetype C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute)&, int32)
	}
```

Other tools and compilers that follow a requirement to not consume APIs with an unknown modreq will be unable to consume the property/indexer and the accessors.
Including VB compiler and previous C# compiler.

An event example:
``` C#
public implicit extension E for C
{
    public event System.Action E1
    {
        add => ...;
        remove => ...; 
    }
}

public class C
{
}
```
``` IL
	.method public hidebysig specialname static 
		void add_E1 (
			class C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute) '<>4__this',
			class [System.Runtime]System.Action 'value'
		) cil managed 

	.method public hidebysig specialname static 
		void remove_E1 (
			class C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute) '<>4__this',
			class [System.Runtime]System.Action 'value'
		) cil managed 

 	.event [System.Runtime]System.Action E1
	{
		.addon void E::add_E1(class C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute), class [System.Runtime]System.Action)
		.removeon void E::remove_E1(class C modreq([System.Runtime]System.Runtime.CompilerServices.ExtensionAttribute), class [System.Runtime]System.Action)
	}
```

Note, the extra parameter for event accessors are not CLS compliant, therefore, tools and other compilers
likely won't be able to consume them as regular static events.
Other tools and compilers that follow a requirement to not consume APIs with an unknown modreq will be unable to consume the accessors.
Including VB compiler and previous C# compiler.

Since at the moment we are not supporting overriding or interface implementation by extension types, presence of the
`modreq(System.Runtime.CompilerServices.ExtensionAttribute)` is sufficient to avoid a signature conflict with a user
defined static member because, given the restriction, there is no way for any `modreq` to get its way into
signature of a user defined static method. Support for interface implementation is likely to change that and a signature
conflict could possibly occur in some edge cases. Therefore, we should block consumption/implementation of interface methods
with ExtensionAttribute modreq.

However, other tools and compilers (VB, for example) won't be able to disambiguate APIs based on presence of the `modreq`.
``` C#
public implicit extension E for C
{
    public void Method()
    {
        System.Console.Write(1);
    }

    public static void Method(C c)
    {
        System.Console.Write(2);
    }
}

public class C
{
}
```

C# consumer:
``` C#
class Program
{
    static void Main()
    {
        var c = new C();
        c.Method(); // Prints 1
        C.Method(c); // Prints 2
    }
}
```

VB consumer:
``` VB
Class Program
    Shared Sub Main()
        Dim c = new C()
        E.Method(c) ' BC31429: 'Method' is ambiguous because multiple kinds of members with this name exist in structure 'E'.
    End Sub
End Class
```



When a reference is provided for the special `ref <>4__this` parameter and the type of the parameter could be a reference
type at runtime, compiler should ensure that a reference to a temporary location with a copy of a value from
the user specified location is provided instead of the original user specified location. This redirection should happen only when the type
is a reference type during execution of the code. The goal is to ensure that the instance on which the extension method is executed
doesn't change for the duration of the method call.

Example:
``` C#
class C
{
}

class Program
{
    C _f;
    
    void Test() => _f.Method();
}

public implicit extension E<T> for T
{
    public void Method() {}
}
``` 

The emitted body of `Program.Test` will be something like:
``` C#
C temp = _f;
E<C>.Method(ref temp);
```

instead of 
``` C#
E<C>.Method(ref _f);
```

which would allow to change the target instance by changing value stored in `_f` while `Method` is executed.

## Type erasure

The codegen for type references involving extension types has the following requirements:
- It should be done in a way that allows switching between the extension type and the underlying type without binary break.  
- It should allow for using type parameters as type arguments: `void M<T>(E<T> e)`.  
- It should allow for using extension types as type arguments without violating type parameter constraints: `C<E>` with `class C<T> where T : struct, I { }`.  
- It should allow encoding tuple names, dynamic, nullability and other such information twice: once the erased type and once for the un-erased type.  

This is solved by erasing the extension types and storing all the information needed
to roundtrip back to the un-erased type in an attribute as a string.

```
namespace System.Runtime.CompilerServices
{
    public class ExtensionErasureAttribute : System.Attribute
    {
        public ExtensionErasureAttribute(string encodedType) { }
        ... tuple names, dynamic, nullability, etc ...
    }
}
```

For example:
- `void M(E)` would be emitted as `void M([ExtensionErasure("E")] UnderlyingType)`.
- `void M(C<E>)` would be emitted as `void M([ExtensionErasure("C[E]")] C<UnderlyingType>)`.

The serialization format is based on the one used for `typeof` in attributes.  
A few examples:  
- `E, AssemblyE, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null`
- ```E`1[[System.String, netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51]]```
- ```Container+NestedE```

Support for type parameters is added using `!N` and `!!N` notation from ECMA-335:  

> II.9 Generics
> Within a generic type definition, its generic parameters are referred to by their index. Generic
> parameter zero is referred to as !0, generic parameter one as !1, and so on. Similarly, within the body
> of a generic method definition, its generic parameters are referred to by their index; generic parameter
> zero is referred to as !!0, generic parameter one as !!1, and so on.

For example:
- `class C<T> { void M(E<T> e) { } }` would be emitted as `void M([Attribute("E[!0]")] Underlying)`.
- `void M<T>(E<T> e) { }` would be emitted as `void M<T>([Attribute("E[!!0]")] Underlying)`.

The attribute also encodes the tuple names, dynamic, native integer and nullability information for the type with extensions un-erased.
For example: `void M(E<dynamic>)` with `C<(dynamic a, dynamic b)>` as the underlying type for `E<dynamic>` would be emitted as
`void M([ExtensionErasure("E<object>"", Dynamic = ... }] [... existing attributes for dynamic and tuple names ... ] C<ValueTuple<object, object>>)`.  

The attribute may not be applied in source.  

Note: when an extension type appears as a containing type, it should not be erased. For example: `E.Nested`.  

Note: the `typeof` serialization format does not support function pointers at the moment.
This support is not planned as part of the extensions work. Tracked by https://github.com/dotnet/roslyn/issues/48765

### Extension type members

#### Fields

A *field_declaration* in a *extension_declaration* shall explicitly include a `static` modifier.  

#### Properties

Auto-properties must still be static (since instance fields are disallowed).  

