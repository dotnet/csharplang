# Namespaces

## General

C\# programs are organized using namespaces. Namespaces are used both as an “internal” organization system for a program, and as an “external” organization system—a way of presenting program elements that are exposed to other programs.

Using directives (§14.5) are provided to facilitate the use of namespaces.

## Compilation units

A *compilation-unit* defines the overall structure of a source file. A compilation unit consists of zero or more *extern-alias-directive*s followed by zero or more *using-directive*s followed by zero or more *global-attributes* followed by zero or more *namespace-member-declaration*s.

[]{#Grammar_compilation_unit .anchor}compilation-unit:\
extern-alias-directives~opt~ using-directives~opt~ global-attributes~opt~\
namespace-member-declarations~opt~

A C\# program consists of one or more compilation units, each contained in a separate source file. When a C\# program is compiled, all of the compilation units are processed together. Thus, compilation units can depend on each other, possibly in a circular fashion.

The *extern-alias-directives* of a compilation unit affect the *using-directives*, *global-attributes* and *namespace-member-declarations* of that compilation unit, but have no effect on other compilation units.

The *using-directives* of a compilation unit affect the *global-attributes* and *namespace-member-declarations* of that compilation unit, but have no effect on other compilation units.

The *global-attributes* (§22.3) of a compilation unit permit the specification of attributes for the target assembly and module. Assemblies and modules act as physical containers for types. An assembly may consist of several physically separate modules.

The *namespace-member-declarations* of each compilation unit of a program contribute members to a single declaration space called the global namespace. \[*Example*:

File A.cs:

class A {}

File B.cs:

class B {}

The two compilation units contribute to the single global namespace, in this case declaring two classes with the fully qualified names A and B. Because the two compilation units contribute to the same declaration space, it would have been an error if each contained a declaration of a member with the same name. *end example*\]

## Namespace declarations

A *namespace-declaration* consists of the keyword namespace, followed by a namespace name and body, optionally followed by a semicolon.

[]{#Grammar_namespace_declaration .anchor}namespace-declaration:\
*namespace* qualified-identifier namespace-body *;*~opt~

[]{#Grammar_qualified_identifier .anchor}qualified-identifier:\
identifier\
qualified-identifier *.* identifier

[]{#Grammar_namespace_body .anchor}namespace-body:\
*{* extern-alias-directives~opt~ using-directives~opt~ namespace-member-declarations~opt~ *}*

A *namespace-declaration* may occur as a top-level declaration in a *compilation-unit* or as a member declaration within another *namespace-declaration*. When a *namespace-declaration* occurs as a top-level declaration in a *compilation-unit*, the namespace becomes a member of the global namespace. When a *namespace-declaration* occurs within another *namespace-declaration*, the inner namespace becomes a member of the outer namespace. In either case, the name of a namespace shall be unique within the containing namespace.

Namespaces are implicitly public and the declaration of a namespace cannot include any access modifiers.

Within a *namespace-body*, the optional *using-directives* import the names of other namespaces and types, allowing them to be referenced directly instead of through qualified names. The optional *namespace-member-declarations* contribute members to the declaration space of the namespace.

The *qualified-identifier* of a *namespace-declaration* may be a single identifier or a sequence of identifiers separated by “.” tokens. The latter form permits a program to define a nested namespace without lexically nesting several namespace declarations. \[*Example*:

namespace N1.N2\
{\
class A {}

class B {}\
}

is semantically equivalent to

namespace N1\
{\
namespace N2\
{\
class A {}

class B {}\
}\
}

*end example*\]

Namespaces are open-ended, and two namespace declarations with the same fully qualified name (§8.8.2) contribute to the same declaration space (§8.3). \[*Example*: In the following code

namespace N1.N2\
{\
class A {}\
}

namespace N1.N2\
{\
class B {}\
}

the two namespace declarations above contribute to the same declaration space, in this case declaring two classes with the fully qualified names N1.N2.A and N1.N2.B. Because the two declarations contribute to the same declaration space, it would have been an error if each contained a declaration of a member with the same name. *end example*\]

## Extern alias directives

An *extern-alias-directive* introduces an identifier that serves as an alias for a namespace. The specification of the aliased namespace is external to the source code of the program and applies also to nested namespaces of the aliased namespace.

[]{#Grammar_extern_alias_directives .anchor}extern-alias-directives:\
extern-alias-directive\
extern-alias-directives extern-alias-directive

[]{#Grammar_extern_alias_directive .anchor}extern-alias-directive:\
*extern* *alias* identifier *;*

The scope of an *extern-alias-directive* extends over the *using-directives*, *global-attributes* and *namespace-member-declarations* of its immediately containing *compilation-unit* or *namespace-body*.

Within a compilation unit or namespace body that contains an *extern-alias-directive*, the identifier introduced by the *extern-alias-directive* can be used to reference the aliased namespace. It is a compile-time error for the *identifier* to be the word global.

Within C\# source code, a type is declared a member of a single namespace. However, a namespace hierarchy referenced by an extern alias may contain types that are also members of other namespaces. For example, if A and B are extern aliases, the names A::X, B::C.Y and global::D.Z may, depending on the external specification supported by the particular compiler, all refer to the same type.

The alias introduced by an *extern-alias-directive* is very similar to the alias introduced by a *using-alias-directive*. See §14.5.2 for more detailed discussion of *extern-alias-directive*s and *using-alias-directive*s.

alias is a contextual keyword (§7.4.4) and only has special meaning when it immediately follows the extern keyword in an *extern-alias-directive*. \[*Example*: In fact an extern alias could use the identifier alias as its name:

extern alias alias;

*end example*\]

## Using directives

### General

***Using directives*** facilitate the use of namespaces and types defined in other namespaces. Using directives impact the name resolution process of *namespace-or-type-name*s (§8.8) and *simple-name*s (§12.7.3), but unlike declarations, *using-directives* do not contribute new members to the underlying declaration spaces of the compilation units or namespaces within which they are used.

[]{#Grammar_using_directives .anchor}using-directives:\
using-directive\
using-directives using-directive

[]{#Grammar_using_directive .anchor}using-directive:\
using-alias-directive\
using-namespace-directive

A *using-alias-directive* (§14.5.2) introduces an alias for a namespace or type.

A *using-namespace-directive* (§14.5.3) imports the type members of a namespace.

The scope of a *using-directive* extends over the *namespace-member-declarations* of its immediately containing compilation unit or namespace body. The scope of a *using-directive* specifically does not include its peer *using-directive*s. Thus, peer *using-directive*s do not affect each other, and the order in which they are written is insignificant. In contrast, the scope of an *extern-alias-directive* includes the *using-directives* defined in the same compilation unit or namespace body.

### Using alias directives

A *using-alias-directive* introduces an identifier that serves as an alias for a namespace or type within the immediately enclosing compilation unit or namespace body.

[]{#Grammar_using_alias_directive .anchor}using-alias-directive:\
*using* identifier *=* namespace-or-type-name *;*

Within global attributes and member declarations in a compilation unit or namespace body that contains a *using-alias-directive*, the identifier introduced by the *using-alias-directive* can be used to reference the given namespace or type. \[*Example*:

namespace N1.N2\
{\
class A {}\
}

namespace N3\
{\
using A = N1.N2.A;

class B: A {}\
}

Above, within member declarations in the N3 namespace, A is an alias for N1.N2.A, and thus class N3.B derives from class N1.N2.A. The same effect can be obtained by creating an alias R for N1.N2 and then referencing R.A:

namespace N3\
{\
using R = N1.N2;

class B: R.A {}\
}

*end example*\]

Within using directives, global attributes and member declarations in a compilation unit or namespace body that contains an *extern-alias-directive*, the identifier introduced by the *extern-alias-directive* can be used to reference the associated namespace. \[*Example*: For example:

namespace N1\
{\
extern alias N2;

class B: N2::A {}\
}

Above, within member declarations in the N1 namespace, N2 is an alias for some namespace whose definition is external to the source code of the program. Class N1.B derives from class N2.A. The same effect can be obtained by creating an alias A for N2.A and then referencing A:

namespace N1\
{\
extern alias N2;\
using A = N2::A;

class B: A {}\
}

*end example*\]

An *extern-alias-directive* or *using-alias-directive* makes an alias available within a particular compilation unit or namespace body, but it does not contribute any new members to the underlying declaration space. In other words, an alias directive is not transitive, but, rather, affects only the compilation unit or namespace body in which it occurs. \[*Example*: In the following code

namespace N3\
{\
extern alias R1;\
using R2 = N1.N2;\
}

namespace N3\
{\
class B: R1::A, R2.I {} // Error, R1 and R2 unknown\
}

the scopes of the alias directives that introduce R1 and R2 only extend to member declarations in the namespace body in which they are contained, so R1 and R2 are unknown in the second namespace declaration. However, placing the alias directives in the containing compilation unit causes the alias to become available within both namespace declarations:

extern alias R1;\
using R2 = N1.N2;

namespace N3\
{\
class B: R1::A, R2.I {}\
}

namespace N3\
{\
class C: R1::A, R2.I {}\
}

*end example*\]

Each *extern-alias-directive* or *using-alias-directive* in a *compilation-unit* or *namespace-body* contributes a name to the ***alias declaration space*** (§8.3) of the immediately enclosing *compilation-unit* or *namespace-body*. The *identifier* of the alias directive shall be unique within the corresponding alias declaration space. The alias identifier need not be unique within the global declaration space or the declaration space of the corresponding namespace. \[*Example*:

extern alias A;\
extern alias B;

using A = N1.N2; // Error: alias A already exists

class B {} // Ok

The using alias named A causes an error since there is already an alias named A in the same compilation unit. The class named B does not conflict with the extern alias named B since these names are added to distinct declaration spaces. The former is added to the global declaration space and the latter is added to the alias declaration space for this compilation unit.

When an alias name matches the name of a member of a namespace, usage of either must be appropriately qualified:

namespace N1.N2\
{\
class B {}\
}

namespace N3\
{\
class A {}\
class B : A {}\
}

namespace N3\
{\
using A = N1.N2;\
using B = N1.N2.B;

class W : B {} // Error: B is ambiguous\
class X : A.B {} // Error: A is ambiguous\
class Y : A::B {} // Ok: uses N1.N2.B\
class Z : N3.B {} // Ok: uses N3.B\
}

In the second namespace body for N3, unqualified use of B results in an error, since N3 contains a member named B and the namespace body that also declares an alias with name B; likewise for A. The class N3.B can be referenced as N3.B or global::N3.B. The alias A can be used in a *qualified-alias-member* (§14.8), such as A::B. The alias B is essentially useless. It cannot be used in a *qualified-alias-member* since only namespace aliases can be used in a *qualified-alias-member* and B aliases a type. *end example*\]

Just like regular members, names introduced by *alias-directives* are hidden by similarly named members in nested scopes. \[*Example*: In the following code

using R = N1.N2;

namespace N3\
{\
class R {}

class B: R.A {} // Error, R has no member A\
}

the reference to R.A in the declaration of B causes a compile-time error because R refers to N3.R, not N1.N2. *end example*\]

The order in which *extern-alias-directive*s are written has no significance. Likewise, the order in which *using-alias-directive*s are written has no significance, but all *using-alias-directives* must come after all *extern-alias-directives* in the same compilation unit or namespace body. Resolution of the *namespace-or-type-name* referenced by a *using-alias-directive* is not affected by the *using-alias-directive* itself or by other *using-directive*s in the immediately containing compilation unit or namespace body, but may be affected by *extern-alias-directives* in the immediately containing compilation unit or namespace body. In other words, the *namespace-or-type-name* of a *using-alias-directive* is resolved as if the immediately containing compilation unit or namespace body had no *using-directive*s but has the correct set of *extern-alias-directive*s. \[*Example*: In the following code

namespace N1.N2 {}

namespace N3\
{\
extern alias E;

using R1 = E::N; // OK

using R2 = N1; // OK

using R3 = N1.N2; // OK

using R4 = R2.N2; // Error, R2 unknown\
}

the last *using-alias-directive* results in a compile-time error because it is not affected by the previous *using-alias-directive*. The first *using-alias-directive* does not result in an error since the scope of the extern alias E includes the *using-alias-directive*. *end example*\]

A *using-alias-directive* can create an alias for any namespace or type, including the namespace within which it appears and any namespace or type nested within that namespace.

Accessing a namespace or type through an alias yields exactly the same result as accessing that namespace or type through its declared name. \[*Example*: Given

namespace N1.N2\
{\
class A {}\
}

namespace N3\
{\
using R1 = N1;\
using R2 = N1.N2;

class B\
{\
N1.N2.A a; // refers to N1.N2.A\
R1.N2.A b; // refers to N1.N2.A\
R2.A c; // refers to N1.N2.A\
}\
}

the names N1.N2.A, R1.N2.A, and R2.A are equivalent and all refer to the class declaration whose fully qualified name is N1.N2.A. *end example*\]

Although each part of a partial type (§15.2.7) is declared within the same namespace, the parts are typically written within different namespace declarations. Thus, different extern alias directives and using directives can be present for each part. When interpreting simple names (§12.7.3) within one part, only the extern alias directives and using directives of the namespace bodies and compilation unit enclosing that part are considered. This may result in the same identifier having different meanings in different parts. \[*Example*:

namespace N\
{\
using List = System.Collections.ArrayList;

partial class A\
{\
List x; // x has type System.Collections.ArrayList\
}\
}

namespace N\
{\
using List = Widgets.LinkedList;

partial class A\
{\
List y; // y has type Widgets.LinkedList\
}\
}

*end example*\]

Using aliases can name a closed constructed type, but cannot name an unbound generic type declaration without supplying type arguments. \[*Example*:

namespace N1\
{\
class A&lt;T&gt;\
{\
class B {}\
}\
}

namespace N2\
{\
using W = N1.A; // Error, cannot name unbound generic type

using X = N1.A.B; // Error, cannot name unbound generic type

using Y = N1.A&lt;int&gt;; // Ok, can name closed constructed type

using Z&lt;T&gt; = N1.A&lt;T&gt;; // Error, using alias cannot have type parameters\
}

*end example*\]

### Using namespace directives

A *using-namespace-directive* imports the types contained in a namespace into the immediately enclosing compilation unit or namespace body, enabling the identifier of each type to be used without qualification.

[]{#Grammar_using_namespace_directive .anchor}using-namespace-directive:\
*using* namespace-name *;*

Within member declarations in a compilation unit or namespace body that contains a *using-namespace-directive*, the types contained in the given namespace can be referenced directly. \[*Example*:

namespace N1.N2\
{\
class A {}\
}

namespace N3\
{\
using N1.N2;

class B: A {}\
}

Above, within member declarations in the N3 namespace, the type members of N1.N2 are directly available, and thus class N3.B derives from class N1.N2.A. *end example*\]

A *using-namespace-directive* imports the types contained in the given namespace, but specifically does not import nested namespaces. \[*Example*: In the following code

namespace N1.N2\
{\
class A {}\
}

namespace N3\
{\
using N1;

class B: N2.A {} // Error, N2 unknown\
}

the *using-namespace-directive* imports the types contained in N1, but not the namespaces nested in N1. Thus, the reference to N2.A in the declaration of B results in a compile-time error because no members named N2 are in scope. *end example*\]

Unlike a *using-alias-directive*, a *using-namespace-directive* may import types whose identifiers are already defined within the enclosing compilation unit or namespace body. In effect, names imported by a *using-namespace-directive* are hidden by similarly named members in the enclosing compilation unit or namespace body. \[*Example*:

namespace N1.N2\
{\
class A {}

class B {}\
}

namespace N3\
{\
using N1.N2;

class A {}\
}

Here, within member declarations in the N3 namespace, A refers to N3.A rather than N1.N2.A. *end example*\]

Because names may be ambiguous when more than one imported namespace introduces the same type name, a *using-alias-directive* is useful to disambiguate the reference. \[*Example*: In the following code

namespace N1\
{\
class A {}\
}

namespace N2\
{\
class A {}\
}

namespace N3\
{\
using N1;

using N2;

class B: A {} // Error, A is ambiguous\
}

both N1 and N2 contain a member A, and because N3 imports both, referencing A in N3 is a compile-time error. In this situation, the conflict can be resolved either through qualification of references to A, or by introducing a *using-alias-directive* that picks a particular A. For example:

namespace N3\
{\
using N1;

using N2;

using A = N1.A;

class B: A {} // A means N1.A\
}

*end example*\]

Like a *using-alias-directive*, a *using-namespace-directive* does not contribute any new members to the underlying declaration space of the compilation unit or namespace, but, rather, affects only the compilation unit or namespace body in which it appears.

The *namespace-name* referenced by a *using-namespace-directive* is resolved in the same way as the *namespace-or-type-name* referenced by a *using-alias-directive*. Thus, *using-namespace-directive*s in the same compilation unit or namespace body do not affect each other and can be written in any order.

## Namespace member declarations

A *namespace-member-declaration* is either a *namespace-declaration* (§14.3) or a *type-declaration* (§14.7).

[]{#Grammar_namespace_member_declarations .anchor}namespace-member-declarations:\
namespace-member-declaration\
namespace-member-declarations namespace-member-declaration

[]{#Grammar_namespace_member_declaration .anchor}namespace-member-declaration:\
namespace-declaration\
type-declaration

A compilation unit or a namespace body can contain *namespace-member-declarations*, and such declarations contribute new members to the underlying declaration space of the containing compilation unit or namespace body.

## Type declarations

A *type-declaration* is a *class-declaration* (§15.2), a *struct-declaration* (§16.2), an *interface-declaration* (§18.2), an *enum-declaration* (§19.2), or a *delegate-declaration* (§20.2).

[]{#Grammar_type_declaration .anchor}type-declaration:\
class-declaration\
struct-declaration\
interface-declaration\
enum-declaration\
delegate-declaration

A *type-declaration* can occur as a top-level declaration in a compilation unit or as a member declaration within a namespace, class, or struct.

When a type declaration for a type T occurs as a top-level declaration in a compilation unit, the fully qualified name (§8.8.2) of the type declaration is the same as the unqualified name of the declaration (§8.8.2). When a type declaration for a type T occurs within a namespace, class, or struct declaration, the fully qualified name (§8.8.3) of the type declarationis S.N, where S is the fully qualified name of the containing namespace, class, or struct declaration, and N is the unqualified name of the declaration.

A type declared within a class or struct is called a nested type (§15.3.9).

The permitted access modifiers and the default access for a type declaration depend on the context in which the declaration takes place (§8.5.2):

-   Types declared in compilation units or namespaces can have public or internal access. The default is internal access.

-   Types declared in classes can have public, protected internal, protected, internal, or private access. The default is private access.

-   Types declared in structs can have public, internal, or private access. The default is private access.

## Qualified alias member

### General

The ***namespace alias qualifier ***:: makes it possible to guarantee that type name lookups are unaffected by the introduction of new types and members. The namespace alias qualifier always appears between two identifiers referred to as the left-hand and right-hand identifiers. Unlike the regular . qualifier, the left-hand identifier of the :: qualifier is looked up only as an extern or using alias.

A *qualified-alias-member* provides explicit access to the global namespace and to extern or using aliases that are potentially hidden by other entities.

[]{#Grammar_qualified_alias_member .anchor}qualified-alias-member:\
identifier *::* identifier type-argument-list~opt~

A *qualified-alias-member* can be used as a *namespace-or-type-name* (§8.8) or as the left operand in a *member-access* (§12.7.5).

A *qualified-alias-member* consists of two identifiers, referred to as the left-hand and right-hand identifiers, seperated by the :: token and optionally followed by a *type-argument-list*. When the left-hand identifier is global then the global namespace is searched for the right-hand identifier. For any other left-hand identifier, that identifier is looked up as an extern or using alias (§14.4 and §14.5.2). A compile-time error occurs if there is no such alias or the alias references a type. If the alias references a namespace then that namespace is searched for the right-hand identifier.

A *qualified-alias-member* has one of two forms:

-   N::I&lt;A~1~, …, A~K~&gt;, where N and I represent identifiers, and &lt;A~1~, …, A~K~&gt; is a type argument list. (K is always at least one.)

-   N::I, where N and I represent identifiers. (In this case, K is considered to be zero.)

Using this notation, the meaning of a *qualified-alias-member* is determined as follows:

-   If N is the identifier global, then the global namespace is searched for I:

<!-- -->

-   If the global namespace contains a namespace named I and K is zero, then the *qualified-alias-member* refers to that namespace.

-   Otherwise, if the global namespace contains a non-generic type named I and K is zero, then the *qualified-alias-member* refers to that type.

-   Otherwise, if the global namespace contains a type named I that has K type parameters, then the *qualified-alias-member* refers to that type constructed with the given type arguments.

-   Otherwise, the *qualified-alias-member* is undefined and a compile-time error occurs.

<!-- -->

-   Otherwise, starting with the namespace declaration (§14.3) immediately containing the *qualified-alias-member* (if any), continuing with each enclosing namespace declaration (if any), and ending with the compilation unit containing the *qualified-alias-member*, the following steps are evaluated until an entity is located:

<!-- -->

-   If the namespace declaration or compilation unit contains a *using-alias-directive* that associates N with a type, then the *qualified-alias-member* is undefined and a compile-time error occurs.

-   Otherwise, if the namespace declaration or compilation unit contains an *extern-alias-directive* or *using-alias-directive* that associates N with a namespace, then:

<!-- -->

-   If the namespace associated with N contains a namespace named I and K is zero, then the *qualified-alias-member* refers to that namespace.

-   Otherwise, if the namespace associated with N contains a non-generic type named I and K is zero, then the *qualified-alias-member* refers to that type.

-   Otherwise, if the namespace associated with N contains a type named I that has K type parameters, then the *qualified-alias-member* refers to that type constructed with the given type arguments.

-   Otherwise, the *qualified-alias-member* is undefined and a compile-time error occurs.

<!-- -->

-   Otherwise, the *qualified-alias-member* is undefined and a compile-time error occurs.

\[*Example*: In the code:

using S = System.Net.Sockets;

class A {\
public static int x;\
}

class C {\
public void F(int A, object S) {\
// Use global::A.x instead of A.x\
global::A.x += A;

// Use S::Socket instead of S.Socket\
S::Socket s = S as S::Socket;\
}\
}

the class A is referenced with global::A and the type System.Net.Sockets.Socket is referenced with S::Socket. Using A.x and S.Socket instead would have caused compile-time errors because A and S would have resolved to the parameters. *end example*\]

\[*Note*: The identifier global has special meaning only when used as the left-hand identifier of a *qualified-alias-name*. It is not a keyword and it is not itself an alias; it is a contextual keyword (§7.4.4). In the code:

class A { }

class C {\
global.A x; // Error: global is not defined\
global::A y; // Valid: References A in the global namespace\
}

using global.A causes a compile-time error since there is no entity named global in scope. If some entity named global were in scope, then global in global.A would have resolved to that entity.

Using global as the left-hand identifier of a *qualified-alias-member* always causes a lookup in the global namespace, even if there is a using alias named global. In the code:

using global = MyGlobalTypes;

class A { }

class C {\
global.A x; // Valid: References MyGlobalTypes.A\
global::A y; // Valid: References A in the global namespace\
}

global.A resolves to MyGlobalTypes.A and global::A resolves to class A in the global namespace. *end note*\]

### Uniqueness of aliases

Each compilation unit and namespace body has a separate declaration space for extern aliases and using aliases. Thus, while the name of an extern alias or using alias shall be unique within the set of extern aliases and using aliases declared in the immediately containing compilation unit or namespace body, an alias is permitted to have the same name as a type or namespace as long as it is used only with the :: qualifier.

\[*Example*: In the following:

namespace N\
{\
public class A {}

public class B {}\
}

namespace N\
{\
using A = System.IO;

class X\
{\
A.Stream s1; // Error, A is ambiguous

A::Stream s2; // Ok\
}\
}

the name A has two possible meanings in the second namespace body because both the class A and the using alias A are in scope. For this reason, use of A in the qualified name A.Stream is ambiguous and causes a compile-time error to occur. However, use of A with the :: qualifier is not an error because A is looked up only as a namespace alias. *end example*\]
