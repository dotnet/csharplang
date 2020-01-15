# Structs

## General

Structs are similar to classes in that they represent data structures that can contain data members and function members. However, unlike classes, structs are value types and do not require heap allocation. A variable of a struct type directly contains the data of the struct, whereas a variable of a class type contains a reference to the data, the latter known as an object.

\[*Note*: Structs are particularly useful for small data structures that have value semantics. Complex numbers, points in a coordinate system, or key-value pairs in a dictionary are all good examples of structs. Key to these data structures is that they have few data members, that they do not require use of inheritance or referential identity, and that they can be conveniently implemented using value semantics where assignment copies the value instead of the reference. *end note*\]

As described in §9.3.5, the simple types provided by C\#, such as int, double, and bool, are, in fact, all struct types.

## Struct declarations

### General

A *struct-declaration* is a *type-declaration* (§14.7) that declares a new struct:

[]{#Grammar_struct_declaration .anchor}struct-declaration:\
attributes~opt~ struct-modifiers~opt~ *partial*~opt~ *struct* identifier type-parameter-list~opt\
~struct-interfaces~opt~ type-parameter-constraints-clauses~opt~ struct-body *;*~opt~

A *struct-declaration* consists of an optional set of *attributes* (§22), followed by an optional set of *struct-modifiers* (§16.2.2), followed by an optional partial modifier (§15.2.7), followed by the keyword struct and an *identifier* that names the struct, followed by an optional *type-parameter-list* specification (§15.2.3), followed by an optional *struct-interfaces* specification (§16.2.4), followed by an optional *type-parameter-constraints-clauses* specification (§15.2.5), followed by a *struct-body* (§16.2.5), optionally followed by a semicolon.

A struct declaration shall not supply a *type-parameter-constraints-clauses* unless it also supplies a *type-parameter-list*.

A struct declaration that supplies a *type-parameter-list* is a generic struct declaration.

### Struct modifiers

A *struct-declaration* may optionally include a sequence of struct modifiers:

[]{#Grammar_struct_modifiers .anchor}struct-modifiers:\
struct-modifier\
struct-modifiers struct-modifier

[]{#Grammar_struct_modifier .anchor}struct-modifier:\
*new*\
*public\
protected\
internal*\
*private*

It is a compile-time error for the same modifier to appear multiple times in a struct declaration.

The modifiers of a struct declaration have the same meaning as those of a class declaration (§15.2.2).

### Partial modifier

The partial modifier indicates that this *struct-declaration* is a partial type declaration. Multiple partial struct declarations with the same name within an enclosing namespace or type declaration combine to form one struct declaration, following the rules specified in §15.2.7.

### Struct interfaces

A struct declaration may include a *struct-interfaces* specification, in which case the struct is said to directly implement the given interface types. For a constructed struct type, including a nested type declared within a generic type declaration (§15.3.9.7), each implemented interface type is obtained by substituting, for each *type-parameter* in the given interface, the corresponding *type-argument* of the constructed type.

[]{#Grammar_struct_interfaces .anchor}struct-interfaces:\
*:* interface-type-list

The handling of interfaces on multiple parts of a partial struct declaration (§15.2.7) are discussed further in §15.2.4.3.

Interface implementations are discussed further in §18.6.

### Struct body

The *struct-body* of a struct defines the members of the struct.

[]{#Grammar_struct_body .anchor}struct-body:\
*{* struct-member-declarations~opt~ *}*

## Struct members

The members of a struct consist of the members introduced by its *struct-member-declaration*s and the members inherited from the type System.ValueType.

[]{#Grammar_struct_member_declarations .anchor}struct-member-declarations:\
struct-member-declaration\
struct-member-declarations struct-member-declaration

struct-member-declaration:\
constant-declaration\
field-declaration\
method-declaration\
property-declaration\
event-declaration\
indexer-declaration\
operator-declaration\
constructor-declaration\
static-constructor-declaration\
type-declaration

\[*Note*: All kinds of *class-member-declaration*s except *finalizer-declaration* are also *struct-member-declaration*s. *end note*\] Except for the differences noted in §16.4, the descriptions of class members provided in §15.3 through §15.12 apply to struct members as well.

## Class and struct differences

### General

Structs differ from classes in several important ways:

-   Structs are value types (§16.4.2).

-   All struct types implicitly inherit from the class System.ValueType (§16.4.3).

-   Assignment to a variable of a struct type creates a *copy* of the value being assigned (§16.4.4).

-   The default value of a struct is the value produced by setting all fields to their default value (§16.4.5).

-   Boxing and unboxing operations are used to convert between a struct type and certain reference types (§16.4.6).

-   The meaning of this is different within struct members (§16.4.7).

-   Instance field declarations for a struct are not permitted to include variable initializers (§16.4.8).

-   A struct is not permitted to declare a parameterless instance constructor (§16.4.9).

-   A struct is not permitted to declare a finalizer.

### Value semantics

Structs are value types (§9.3) and are said to have value semantics. Classes, on the other hand, are reference types (§9.2) and are said to have reference semantics.

A variable of a struct type directly contains the data of the struct, whereas a variable of a class type contains a reference to an object that contains the data. When a struct B contains an instance field of type A and A is a struct type, it is a compile-time error for A to depend on B or a type constructed from B. A struct X ***directly depends on*** a struct Y if X contains an instance field of type Y. Given this definition, the complete set of structs upon which a struct depends is the transitive closure of the ***directly depends on*** relationship. \[*Example*:

struct Node\
{\
int data;

Node next; // error, Node directly depends on itself

}

is an error because Node contains an instance field of its own type. Another example

struct A { B b; }

struct B { C c; }

struct C { A a; }

is an error because each of the types A, B, and C depend on each other. *end example*\]

With classes, it is possible for two variables to reference the same object, and thus possible for operations on one variable to affect the object referenced by the other variable. With structs, the variables each have their own copy of the data (except in the case of ref and out parameter variables), and it is not possible for operations on one to affect the other. Furthermore, except when explicitly nullable (§9.3.11), it is not possible for values of a struct type to be null. \[*Note*: If a struct contains a field of reference type then the contents of the object referenced can be altered by other operations. However the value of the field itself, i.e., which object it references, cannot be changed through a mutation of a different struct value. *end note*\]

\[*Example*: Given the declaration

struct Point\
{\
public int x, y;

public Point(int x, int y) {\
this.x = x;\
this.y = y;\
}\
}

the code fragment

Point a = new Point(10, 10);\
Point b = a;\
a.x = 100;\
System.Console.WriteLine(b.x);

outputs the value 10. The assignment of a to b creates a copy of the value, and b is thus unaffected by the assignment to a.x. Had Point instead been declared as a class, the output would be 100 because a and b would reference the same object. *end example*\]

### Inheritance

All struct types implicitly inherit from the class System.ValueType, which, in turn, inherits from class object. A struct declaration may specify a list of implemented interfaces, but it is not possible for a struct declaration to specify a base class.

Struct types are never abstract and are always implicitly sealed. The abstract and sealed modifiers are therefore not permitted in a struct declaration.

Since inheritance isn’t supported for structs, the declared accessibility of a struct member cannot be protected or protected internal.

Function members in a struct cannot be abstract or virtual, and the override modifier is allowed only to override methods inherited from System.ValueType.

### Assignment

Assignment to a variable of a struct type creates a *copy* of the value being assigned. This differs from assignment to a variable of a class type, which copies the reference but not the object identified by the reference.

Similar to an assignment, when a struct is passed as a value parameter or returned as the result of a function member, a copy of the struct is created. A struct may be passed by reference to a function member using a ref or out parameter.

When a property or indexer of a struct is the target of an assignment, the instance expression associated with the property or indexer access shall be classified as a variable. If the instance expression is classified as a value, a compile-time error occurs. This is described in further detail in §12.18.2.

### Default values

As described in §10.3, several kinds of variables are automatically initialized to their default value when they are created. For variables of class types and other reference types, this default value is null. However, since structs are value types that cannot be null, the default value of a struct is the value produced by setting all value type fields to their default value and all reference type fields to null.

\[*Example*: Referring to the Point struct declared above, the example

Point\[\] a = new Point\[100\];

initializes each Point in the array to the value produced by setting the x and y fields to zero. *end example*\]

The default value of a struct corresponds to the value returned by the default constructor of the struct (§9.3.3). Unlike a class, a struct is not permitted to declare a parameterless instance constructor. Instead, every struct implicitly has a parameterless instance constructor, which always returns the value that results from setting all fields to their default values.

\[*Note*: Structs should be designed to consider the default initialization state a valid state. In the example

using System;

struct KeyValuePair\
{\
string key;\
string value;

public KeyValuePair(string key, string value) {\
if (key == null || value == null) throw new ArgumentException();\
this.key = key;\
this.value = value;\
}\
}

the user-defined instance constructor protects against null values only where it is explicitly called. In cases where a KeyValuePair variable is subject to default value initialization, the key and value fields will be null, and the struct should be prepared to handle this state. *end note*\]

### Boxing and unboxing

A value of a class type can be converted to type object or to an interface type that is implemented by the class simply by treating the reference as another type at compile-time. Likewise, a value of type object or a value of an interface type can be converted back to a class type without changing the reference (but, of course, a run-time type check is required in this case).

Since structs are not reference types, these operations are implemented differently for struct types. When a value of a struct type is converted to certain reference types (as defined in §11.2.8), a boxing operation takes place. Likewise, when a value of certain reference types (as defined in §11.3.6) is converted back to a struct type, an unboxing operation takes place. A key difference from the same operations on class types is that boxing and unboxing *copies* the struct value either into or out of the boxed instance. \[*Note*: Thus, following a boxing or unboxing operation, changes made to the unboxed struct are not reflected in the boxed struct. *end note*\]

For further details on boxing and unboxing, see §11.2.8 and §11.3.6.

### Meaning of this

The meaning of this in a struct differs from the meaning of this in a class, as described in §12.7.8.When a struct type overrides a virtual method inherited from System.ValueType (such as Equals, GetHashCode, or ToString), invocation of the virtual method through an instance of the struct type does not cause boxing to occur. This is true even when the struct is used as a type parameter and the invocation occurs through an instance of the type parameter type. \[*Example*:

using System;

struct Counter\
{\
int value;

public override string ToString() {\
value++;\
return value.ToString();\
}\
}

class Program\
{\
static void Test&lt;T&gt;() where T: new() {\
T x = new T();\
Console.WriteLine(x.ToString());\
Console.WriteLine(x.ToString());\
Console.WriteLine(x.ToString());\
}

static void Main() {\
Test&lt;Counter&gt;();\
}\
}

The output of the program is:

1\
2\
3

Although it is bad style for ToString to have side effects, the example demonstrates that no boxing occurred for the three invocations of x.ToString(). *end example*\]

Similarly, boxing never implicitly occurs when accessing a member on a constrained type parameter when the member is implemented within the value type. For example, suppose an interface ICounter contains a method Increment, which can be used to modify a value. If ICounter is used as a constraint, the implementation of the Increment method is called with a reference to the variable that Increment was called on, never a boxed copy. \[*Example*:

using System;

interface ICounter\
{\
void Increment();\
}

struct Counter: ICounter\
{\
int value;

public override string ToString() {\
return value.ToString();\
}

void ICounter.Increment() {\
value++;\
}\
}

class Program\
{\
static void Test&lt;T&gt;() where T: ICounter, new() {\
T x = new T();\
Console.WriteLine(x);\
x.Increment(); // Modify x\
Console.WriteLine(x);\
((ICounter)x).Increment(); // Modify boxed copy of x\
Console.WriteLine(x);\
}

static void Main() {\
Test&lt;Counter&gt;();\
}\
}

The first call to Increment modifies the value in the variable x. This is not equivalent to the second call to Increment, which modifies the value in a boxed copy of x. Thus, the output of the program is:

0\
1\
1

*end example*\]

### Field initializers

As described in §16.4.5, the default value of a struct consists of the value that results from setting all value type fields to their default value and all reference type fields to null. For this reason, a struct does not permit instance field declarations to include variable initializers. This restriction applies only to instance fields. Static fields of a struct are permitted to include variable initializers. \[*Example*: The following

struct Point\
{\
public int x = 1; // Error, initializer not permitted\
public int y = 1; // Error, initializer not permitted\
}

is in error because the instance field declarations include variable initializers. *end example*\]

### Constructors

Unlike a class, a struct is not permitted to declare a parameterless instance constructor. Instead, every struct implicitly has a parameterless instance constructor, which always returns the value that results from setting all value type fields to their default value and all reference type fields to null (§9.3.3). A struct can declare instance constructors having parameters. \[*Example*:

struct Point\
{\
int x, y;

public Point(int x, int y) {\
this.x = x;\
this.y = y;\
}\
}

Given the above declaration, the statements

Point p1 = new Point();\
Point p2 = new Point(0, 0);

both create a Point with x and y initialized to zero. *end example*\]

A struct instance constructor is not permitted to include a constructor initializer of the form base(*argument-list~opt~*).

The this parameter of a struct instance constructor corresponds to an out parameter of the struct type. As such, this shall be definitely assigned (§10.4) at every location where the constructor returns. Similarly, it cannot be read (even implicitly) in the constructor body before being definitely assigned.

If the struct instance constructor specifies a constructor initializer, that initializer is considered a definite assignment to this that occurs prior to the body of the constructor. Therefore, the body itself has no initialization requirements. \[*Example*: Consider the instance constructor implementation below:

struct Point\
{\
int x, y;

public int X {\
set { x = value; }\
}

public int Y {\
set { y = value; }\
}

public Point(int x, int y) {\
X = x; // error, this is not yet definitely assigned\
Y = y; // error, this is not yet definitely assigned\
}\
}

No instance function member (including the set accessors for the properties X and Y) can be called until all fields of the struct being constructed have been definitely assigned. Note, however, that if Point were a class instead of a struct, the instance constructor implementation would be permitted.

*end example*\]

### Static constructors

Static constructors for structs follow most of the same rules as for classes. The execution of a static constructor for a struct type is triggered by the first of the following events to occur within an application domain:

-   A static member of the struct type is referenced.

-   An explicitly declared constructor of the struct type is called.

\[*Note*: The creation of default values (§16.4.5) of struct types does not trigger the static constructor. (An example of this is the initial value of elements in an array.) *end note*\]

### Automatically implemented properties

Automatically implemented properties (§15.7.4) use hidden backing fields, which are only accessible to the property accessors. \[*Note*: This access restriction means that constructors in structs containing automatically implemented properties often need an explicit constructor initializer where they would not otherwise need one, to satisfy the requirement of all fields being definitely assigned before any function member is invoked or the constructor returns. *end note*\]

[[]{#_Ref451675312 .anchor}]{#_Toc445783066 .anchor}

