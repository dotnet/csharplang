# Enums

## General

An ***enum type*** is a distinct value type (§9.2) that declares a set of named constants. \[*Example*: The example

enum Color\
{\
Red,\
Green,\
Blue\
}

declares an enum type named Color with members Red, Green, and Blue. *end example*\]

## Enum declarations

An enum declaration declares a new enum type. An enum declaration begins with the keyword enum, and defines the name, accessibility, underlying type, and members of the enum.

[]{#Grammar_enum_declaration .anchor}enum-declaration:\
attributes~opt~ enum-modifiers~opt~ *enum* identifier enum-base~opt~ enum-body *;*~opt~[]{#Grammar_enum_base .anchor}

enum-base:\
*:* integral-type

[]{#Grammar_enum_body .anchor}enum-body:\
*{* enum-member-declarations~opt~ *}*\
*{* enum-member-declarations *,* *}*

Each enum type has a corresponding integral type called the ***underlying type*** of the enum type. This underlying type shall be able to represent all the enumerator values defined in the enumeration. An enum declaration may explicitly declare an underlying type of byte, sbyte, short, ushort, int, uint, long or ulong. \[*Note*: char cannot be used as an underlying type. *end note*\] An enum declaration that does not explicitly declare an underlying type has an underlying type of int.

\[*Example*: The example

enum Color: long\
{\
Red,\
Green,\
Blue\
}

declares an enum with an underlying type of long. *end example*\] \[*Note*: A developer might choose to use an underlying type of long, as in the example, to enable the use of values that are in the range of long but not in the range of int, or to preserve this option for the future. *end note*\]

\[*Note*: C\# allows a trailing comma in an *enum-body*, just like it allows one in an *array-initializer* (§17.7). *end note*\]

## Enum modifiers

An *enum-declaration* may optionally include a sequence of enum modifiers:

[]{#Grammar_enum_modifiers .anchor}enum-modifiers:\
enum-modifier\
enum-modifiers enum-modifier

[]{#Grammar_enum_modifier .anchor}enum-modifier:\
*new*\
*public\
protected\
internal*\
*private*

[[[]{#_Ref485188801 .anchor}]{#_Ref485188649 .anchor}]{#_Ref483210964 .anchor}It is a compile-time error for the same modifier to appear multiple times in an enum declaration.

The modifiers of an enum declaration have the same meaning as those of a class declaration (§15.2.2). However, the abstract, and sealed, and static modifiers are not permitted in an enum declaration. Enums cannot be abstract and do not permit derivation.

## Enum members

The body of an enum type declaration defines zero or more enum members, which are the named constants of the enum type. No two enum members can have the same name.

[]{#Grammar_enum_member_declarations .anchor}enum-member-declarations:\
enum-member-declaration\
enum-member-declarations *,* enum-member-declaration

[]{#Grammar_enum_member_declaration .anchor}enum-member-declaration:\
attributes~opt~ identifier\
attributes~opt~ identifier *=* constant-expression

Each enum member has an associated constant value. The type of this value is the underlying type for the containing enum. The constant value for each enum member shall be in the range of the underlying type for the enum. \[*Example*: The example

enum Color: uint\
{\
Red = -1,\
Green = -2,\
Blue = -3\
}

results in a compile-time error because the constant values -1, -2, and –3 are not in the range of the underlying integral type uint. *end example*\]

Multiple enum members may share the same associated value. \[*Example*: The example

enum Color\
{\
Red,\
Green,\
Blue,\
\
Max = Blue\
}

shows an enum in which two enum members—Blue and Max—have the same associated value. *end example*\]

The associated value of an enum member is assigned either implicitly or explicitly. If the declaration of the enum member has a *constant-expression* initializer, the value of that constant expression, implicitly converted to the underlying type of the enum, is the associated value of the enum member. If the declaration of the enum member has no initializer, its associated value is set implicitly, as follows:

-   If the enum member is the first enum member declared in the enum type, its associated value is zero.

-   Otherwise, the associated value of the enum member is obtained by increasing the associated value of the textually preceding enum member by one. This increased value shall be within the range of values that can be represented by the underlying type, otherwise a compile-time error occurs.

\[*Example*: The example

using System;

enum Color\
{\
Red,\
Green = 10,\
Blue\
}

class Test\
{\
static void Main() {\
Console.WriteLine(StringFromColor(Color.Red));\
Console.WriteLine(StringFromColor(Color.Green));\
Console.WriteLine(StringFromColor(Color.Blue));\
}

static string StringFromColor(Color c) {\
switch (c) {\
case Color.Red:\
return String.Format("Red = {0}", (int) c);

case Color.Green:\
return String.Format("Green = {0}", (int) c);

case Color.Blue:\
return String.Format("Blue = {0}", (int) c);

default:\
return "Invalid color";\
}\
}\
}

prints out the enum member names and their associated values. The output is:

Red = 0\
Green = 10\
Blue = 11

for the following reasons:

-   the enum member Red is automatically assigned the value zero (since it has no initializer and is the first enum member);

-   the enum member Green is explicitly given the value 10;

-   and the enum member Blue is automatically assigned the value one greater than the member that textually precedes it.

*end example*\]

The associated value of an enum member may not, directly or indirectly, use the value of its own associated enum member. Other than this circularity restriction, enum member initializers may freely refer to other enum member initializers, regardless of their textual position. Within an enum member initializer, values of other enum members are always treated as having the type of their underlying type, so that casts are not necessary when referring to other enum members.

\[*Example*: The example

enum Circular\
{\
A = B,\
B\
}

results in a compile-time error because the declarations of A and B are circular. A depends on B explicitly, and B depends on A implicitly. *end example*\]

Enum members are named and scoped in a manner exactly analogous to fields within classes. The scope of an enum member is the body of its containing enum type. Within that scope, enum members can be referred to by their simple name. From all other code, the name of an enum member shall be qualified with the name of its enum type. Enum members do not have any declared accessibility—an enum member is accessible if its containing enum type is accessible.

## The System.Enum type

The type System.Enum is the abstract base class of all enum types (this is distinct and different from the underlying type of the enum type), and the members inherited from System.Enum are available in any enum type. A boxing conversion (§11.2.8) exists from any enum type to System.Enum, and an unboxing conversion (§11.3.6) exists from System.Enum to any enum type.

Note that System.Enum is not itself an *enum-type*. Rather, it is a *class-type* from which all *enum-type*s are derived. The type System.Enum inherits from the type System.ValueType (§9.3.2), which, in turn, inherits from type object. At run-time, a value of type System.Enum can be null or a reference to a boxed value of any enum type.

## Enum values and operations

Each enum type defines a distinct type; an explicit enumeration conversion (§11.3.3) is required to convert between an enum type and an integral type, or between two enum types. The set of values of the enum type is the same as the set of values of the underlying type and is not restricted to the values of the named constants. Any value of the underlying type of an enum can be cast to the enum type, and is a distinct valid value of that enum type.

Enum members have the type of their containing enum type (except within other enum member initializers: see §19.4). The value of an enum member declared in enum type E with associated value v is (E)v.

The following operators can be used on values of enum types:

-   ==, !=, &lt;, &gt;, &lt;=, &gt;= (§12.11.6)

-   binary + (§12.9.5)

-   binary - (§12.9.6)

-   \^, &, | (§12.12.3)

-   \~ (§12.8.5)

-   ++, -- (§12.7.10 and §12.8.6)

-   sizeof (§23.6.9)

Every enum type automatically derives from the class System.Enum (which, in turn, derives from System.ValueType and object). Thus, inherited methods and properties of this class can be used on values of an enum type.

