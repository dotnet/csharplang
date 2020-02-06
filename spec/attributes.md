# Attributes

## General

Much of the C# language enables the programmer to specify declarative information about the entities defined in the program. For example, the accessibility of a method in a class is specified by decorating it with the *method-modifiers* `public`, `protected`, `internal`, and `private`.

C# enables programmers to invent new kinds of declarative information, called ***attributes.*** Programmers can then attach attributes to various program entities, and retrieve attribute information in a run-time environment. 
>[!NOTE]
>For instance, a framework might define a `HelpAttribute` attribute that can be placed on certain program elements (such as classes and methods) to provide a mapping from those program elements to their documentation.

Attributes are defined through the declaration of attribute classes (§22.2), which can have positional and named parameters (§22.2.3). Attributes are attached to entities in a C# program using attribute specifications (§22.3), and can be retrieved at run-time as attribute instances (§22.4).

## Attribute classes

### General

A class that derives from the abstract class `System.Attribute`, whether directly or indirectly, is an ***attribute class***. The declaration of an attribute class defines a new kind of attribute that can be placed on program entities. By convention, attribute classes are named with a suffix of `Attribute`. Uses of an attribute may either include or omit this suffix.

A generic class declaration shall not use `System.Attribute` as a direct or indirect base class. [*Example*:

```csharp
using System;

public class B : Attribute {}

public class C<T> : B {} // Error – generic cannot be an attribute
```

*end example*]

### Attribute usage

The attribute `AttributeUsage` (§22.5.2) is used to describe how an attribute class can be used.

`AttributeUsage` has a positional parameter (§22.2.3) that enables an attribute class to specify the kinds of program entities on which it can be used. [*Example*: The example

```csharp
using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class SimpleAttribute: Attribute
{ 
    … 
}
```

defines an attribute class named `SimpleAttribute` that can be placed on *class-declaration*s and *interface-declaration*s only. The example
```csharp
[Simple] class Class1 {…}

[Simple] interface Interface1 {…}
```
shows several uses of the `Simple` attribute. Although this attribute is defined with the name `SimpleAttribute`, when this attribute is used, the `Attribute` suffix may be omitted, resulting in the short name `Simple`. Thus, the example above is semantically equivalent to the following

```csharp
[SimpleAttribute] class Class1 {…}

[SimpleAttribute] interface Interface1 {…}
```
*end example*]

`AttributeUsage` has a named parameter (§22.2.3), called `AllowMultiple`, which indicates whether the attribute can be specified more than once for a given entity. If `AllowMultiple` for an attribute class is true, then that attribute class is a ***multi-use attribute class***, and can be specified more than once on an entity. If `AllowMultiple` for an attribute class is false or it is unspecified, then that attribute class is a ***single-use attribute class***, and can be specified at most once on an entity.

[*Example*: The example

```csharp
using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AuthorAttribute: Attribute
{
    private string name;
    
    public AuthorAttribute(string name) {
        this.name = name;
    }
    
    public string Name {
        get { return name; }
    }
}
```
defines a multi-use attribute class named `AuthorAttribute`. The example

```csharp
[Author("Brian Kernighan"), Author("Dennis Ritchie")]
class Class1 
{
    …
}
```

shows a class declaration with two uses of the `Author` attribute. *end example*]

`AttributeUsage` has another named parameter (§22.2.3), called `Inherited`, which indicates whether the attribute, when specified on a base class, is also inherited by classes that derive from that base class. If `Inherited` for an attribute class is true, then that attribute is inherited. If `Inherited` for an attribute class is false then that attribute is not inherited. If it is unspecified, its default value is true.

An attribute class `X` not having an `AttributeUsage` attribute attached to it, as in

```csharp
using System;

class X: Attribute { … }
```

is equivalent to the following:

```csharp
using System;

[AttributeUsage(
AttributeTargets.All,
AllowMultiple = false,
Inherited = true)
]
class X: Attribute { … }
```

### Positional and named parameters

Attribute classes can have ***positional parameters*** and ***named parameters***. Each public instance constructor for an attribute class defines a valid sequence of positional parameters for that attribute class. Each non-static public read-write field and property for an attribute class defines a named parameter for the attribute class. Both accessors of a property need to be public for the property to define a named parameter.

[*Example*: The example

```csharp
using System;

[AttributeUsage(AttributeTargets.Class)]
public class HelpAttribute: Attribute
{

    public HelpAttribute(string url) { // url is a positional parameter
        …
    }
    
    public string Topic { // Topic is a named parameter
        get {…}
        set {…}
    }
    
    public string Url {
         get {…} 
    }
}
```

defines an attribute class named `HelpAttribute` that has one positional parameter, `url`, and one named parameter, `Topic`. Although it is non-static and public, the property `Url` does not define a named parameter, since it is not read-write.

This attribute class might be used as follows:

```csharp
[Help("http://www.mycompany.com/…/Class1.htm")]
class Class1
{
    ...
}

[Help("http://www.mycompany.com/…/Misc.htm", Topic ="Class2")]
class Class2
{
    .....
}
```

*end example*]

### Attribute parameter types

The types of positional and named parameters for an attribute class are limited to the ***attribute parameter types***, which are:

*  One of the following types: `bool`, `byte`, `char`, `double`, `float`, `int`, `long`, `sbyte`, `short`, `string`, `uint`, `ulong`, `ushort`.

*  The type `object`.

*  The type `System.Type`.

*  Enum types.

*  Single-dimensional arrays of the above types.

*  A constructor argument or public field that does not have one of these types, shall not be used as a positional or named parameter in an attribute specification.

## Attribute specification

***Attribute specification*** is the application of a previously defined attribute to a program entity. An attribute is a piece of additional declarative information that is specified for a program entity. Attributes can be specified at global scope (to specify attributes on the containing assembly or module) and for *type-declaration*s (§14.7), *class-member-declaration*s (§15.3), *interface-member-declaration*s (§18.4), *struct-member-declaration*s (§16.3), *enum-member-declaration*s (§19.2), *accessor-declaration*s (§15.7.3), *event-accessor-declaration*s (§15.8), elements of *formal-parameter-list*s (§15.6.2), and elements of *type-parameter-list*s (§15.2.3).

Attributes are specified in ***attribute sections***. An attribute section consists of a pair of square brackets, which surround a comma-separated list of one or more attributes. The order in which attributes are specified in such a list, and the order in which sections attached to the same program entity are arranged, is not significant. For instance, the attribute specifications `[A][B]`, `[B][A]`, `[A, B]`, and `[B, A]` are equivalent.

```ANTLR
global-attributes:
    global-attribute-sections

global-attribute-sections:
    global-attribute-section
    global-attribute-sections global-attribute-section

global-attribute-section:
    [ global-attribute-target-specifier attribute-list ]
    [ global-attribute-target-specifier attribute-list , ]

global-attribute-target-specifier:
    global-attribute-target :

global-attribute-target:
    identifier equal to assembly or module

attributes:
    attribute-sections

attribute-sections:
    attribute-section
    attribute-sections attribute-section

attribute-section:
    [ attribute-target-specifier~opt~ attribute-list ]
    [ attribute-target-specifier~opt~ attribute-list , ]

attribute-target-specifier:
    attribute-target *:*

attribute-target:
    identifier *not equal to assembly or module
    keyword

attribute-list:
    attribute
    attribute-list , attribute

attribute:
    attribute-name attribute-arguments~opt~

attribute-name:
    type-name

attribute-arguments:
    ( positional-argument-list~opt~ )
    ( positional-argument-list , named-argument-list )
    ( named-argument-list )

positional-argument-list:
    positional-argument
    positional-argument-list , positional-argument

positional-argument:
    argument-name~opt~ attribute-argument-expression

named-argument-list:
    named-argument
    named-argument-list , named-argument

named-argument:
    identifier = attribute-argument-expression

attribute-argument-expression:
    expression
```
For the above productions *global-attribute-target* and *attribute-target*, and in the text below, the referenced equality is that defined in §7.4.3.

An attribute consists of an *attribute-name* and an optional list of positional and named arguments. The positional arguments (if any) precede the named arguments. A positional argument consists of an *attribute-argument-expression*; a named argument consists of a name, followed by an equal sign, followed by an *attribute-argument-expression*, which, together, are constrained by the same rules as simple assignment. The order of named arguments is not significant.
>[!NOTE]
>For convenience, a trailing comma is allowed in a *global-attribute-section* and an *attribute-section*, just as one is allowed in an *array-initializer* (§17.7).

The *attribute-name* identifies an attribute class.

When an attribute is placed at the global level, a *global-attribute-target-specifier* is required. When the *global-attribute-target* is equal to:

* `assembly` – the target is the containing assembly

* `module` – the target is the containing module

No other values for *global-attribute-target* are allowed.

The standardized *attribute-target* names are `event`, `field`, `method`, `param`, `property`, `return`, `type`, and `typevar`. These target names shall only be used in the following contexts:

*   `event` — an event.

*   `field` — a field. A field-like event (i.e., one without accessors) can also have an attribute with this target.

*   `method` — a constructor, finalizer, method, operator, property get and set accessors, indexer get and set accessors, and event add and remove accessors. A field-like event (i.e., one without accessors) can also have an attribute with this target.

*   `param` — a property set accessor, an indexer set accessor, event add and remove accessors, and a parameter in a constructor, method, and operator.

*   `property` — a property and an indexer.

*   `return` — a delegate, method, operator, property get accessor, and indexer get accessor.

*   `type` — a delegate, class, struct, enum, and interface.

*   `typevar` — a type parameter.

Certain contexts permit the specification of an attribute on more than one target. A program can explicitly specify the target by including an *attribute-target-specifier*. Without an *attribute-target-specifier* a default is applied, but an *attribute-target-specifier* can be used to affirm or override the default. The contexts are resolved as follows:

*   For an attribute on a delegate declaration the default target is the delegate. Otherwise when the attribute-target is equal to:

*   `type` — the target is the delegate

*   `return` — the target is the return value

*   For an attribute on a method declaration the default target is the method. Otherwise when the *attribute-target* is equal to:

*   method — the target is the method

*   return — the target is the return value

*   For an attribute on an operator declaration the default target is the operator. Otherwise when the *attribute-target* is equal to:

*   method — the target is the operator

*   return — the target is the return value

*   For an attribute on a get accessor declaration for a property or indexer declaration the default target is the associated method. Otherwise when the *attribute-target* is equal to:

*   method — the target is the associated method

*   return — the target is the return value

*   For an attribute specified on a set accessor for a property or indexer declaration the default target is the associated method. Otherwise when the *attribute-target* is equal to:

*   `method` — the target is the associated method

*   `param` — the target is the lone implicit parameter

*   For an attribute specified on an event declaration that omits event-accessor-declarations the default target is the event declaration. Otherwise when the *attribute-target* is equal to:

*   `event` — the target is the event declaration

*   `field` — the target is the field

*   `method` — the targets are the methods

*   In the case of an event declaration that does not omit event-accessor-declarations the default target is the method.

*   `method` — the target is the associated method

*   `param` — the target is the lone parameter

In all other contexts, inclusion of an *attribute-target-specifier* is permitted but unnecessary. [*Example*: a class declaration may either include or omit the specifier `type`:

```csharp
[type: Author("Brian Kernighan")]
class Class1 {}

[Author("Dennis Ritchie")]
class Class2 {}
```

*end example*.]

An implementation can accept other *attribute-target*s, the purposes of which are implementation defined. An implementation that does not recognize such an *attribute-target* shall issue a warning and ignore the containing *attribute-section*.

By convention, attribute classes are named with a suffix of `Attribute`. An *attribute-name* can either include or omit this suffix. Specifically, an *attribute-name* is resolved as follows:

*   If the right-most identifier of the *attribute-name* is a verbatim identifier (§7.4.3), then the *attribute-name* is resolved as a *type-name* (§8.8). If the result is not a type derived from `System.Attribute`, a compile-time error occurs.

*   Otherwise,

    *   The *attribute-name* is resolved as a *type-name* (§8.8) except any errors are suppressed. If this resolution is successful and results in a type derived from `System.Attribute` then the type is the result of this step.
    
    *   The characters Attribute are appended to the right-most identifier in the *attribute-name* and the resulting string of tokens is resolved as a *type-name* (§8.8) except any errors are suppressed. If this resolution is successful and results in a type derived from `System.Attribute` then the type is the result of this step.
    
    If exactly one of the two steps above results in a type derived from `System.Attribute`, then that type is the result of the *attribute-name*. Otherwise a compile-time error occurs.

[*Example*: If an attribute class is found both with and without this suffix, an ambiguity is present, and a compile-time error results. If the *attribute-name* is spelled such that its right-most *identifier* is a verbatim identifier (§7.4.3), then only an attribute without a suffix is matched, thus enabling such an ambiguity to be resolved. The example

```csharp
using System;

[AttributeUsage(AttributeTargets.All)]
public class Example: Attribute
{}

[AttributeUsage(AttributeTargets.All)]
public class ExampleAttribute: Attribute
{}

[Example] // Error: ambiguity
class Class1 {}

[ExampleAttribute] // Refers to ExampleAttribute
class Class2 {}

[@Example] // Refers to Example
class Class3 {}

[@ExampleAttribute] // Refers to ExampleAttribute
class Class4 {}
```

shows two attribute classes named `Example` and `ExampleAttribute`. The attribute [Example] is ambiguous, since it could refer to either `Example` or `ExampleAttribute`. Using a verbatim identifier allows the exact intent to be specified in such rare cases. The attribute [ExampleAttribute] is not ambiguous (although it would be if there was an attribute class named `ExampleAttributeAttribute`!). If the declaration for class `Example` is removed, then both attributes refer to the attribute class named `ExampleAttribute`, as follows:

```csharp
using System;

[AttributeUsage(AttributeTargets.All)]
public class ExampleAttribute: Attribute
{}

[Example] // Refers to ExampleAttribute
class Class1 {}

[ExampleAttribute] // Refers to ExampleAttribute
class Class2 {}

[@Example] // Error: no attribute named “Example”
class Class3 {}
```

*end example*]

It is a compile-time error to use a single-use attribute class more than once on the same entity. [*Example*: The example

```csharp
using System;

[AttributeUsage(AttributeTargets.Class)]
public class HelpStringAttribute: Attribute
{
    string value;
    
    public HelpStringAttribute(string value) {
        this.value = value;
    }
    
    public string Value { 
            get {…} 
    }
}

[HelpString("Description of Class1")]
[HelpString("Another description of Class1")]
public class Class1 {}
```

results in a compile-time error because it attempts to use `HelpString`, which is a single-use attribute class, more than once on the declaration of `Class1`. *end example*]

An expression `E` is an *attribute-argument-expression* if all of the following statements are true:

*    The type of `E` is an attribute parameter type (§22.2.4).

*   At compile-time, the value of `E` can be resolved to one of the following:

*   A constant value.

*   A `System.Type` object obtained using a *typeof-expression* (§12.7.12) specifying a non-generic type, a closed constructed type (§9.4.3), or an unbound generic type (§9.4.4), but not an open type (§9.4.3).

*   A single-dimensional array of *attribute-argument-expression*s.

[*Example*:

```csharp
using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)]
public class TestAttribute: Attribute
{
    public int P1 {
    get {…}
    set {…}
}

public Type P2 {
    get {…}
    set {…}
}

public object P3 {
    get {…}
    set {…}
    }
}

[Test(P1 = 1234, P3 = new int[]{1, 3, 5}, P2 = typeof(float))]
class MyClass {}

class C<T> {
    [Test(P2 = typeof(T))] // Error – T not a closed type.
    int x1;
    
    [Test(P2 = typeof(C<T>))] // Error – C<;T>; not a closed type.
    int x2;
    
    [Test(P2 = typeof(C<int>))] // Ok
    int x3;
    
    [Test(P2 = typeof(C<>))] // Ok
    int x4;
}

```
*end example*]

The attributes of a type declared in multiple parts are determined by combining, in an unspecified order, the attributes of each of its parts. If the same attribute is placed on multiple parts, it is equivalent to specifying that attribute multiple times on the type. [*Example*: The two parts:

```csharp
[Attr1, Attr2("hello")]
partial class A {}

[Attr3, Attr2("goodbye")]
partial class A {}
```

are equivalent to the following single declaration:

```csharp
[Attr1, Attr2("hello"), Attr3, Attr2("goodbye")]
class A {}
```
*end example*]

Attributes on type parameters combine in the same way.

## Attribute instances

### General

An ***attribute instance*** is an instance that represents an attribute at run-time. An attribute is defined with an attribute class, positional arguments, and named arguments. An attribute instance is an instance of the attribute class that is initialized with the positional and named arguments.

Retrieval of an attribute instance involves both compile-time and run-time processing, as described in the following subclauses.

### Compilation of an attribute

The compilation of an *attribute* with attribute class `T`, *positional-argument-list * `P`, *named-argument-list* `N`, and specified on a program entity `E` is compiled into an assembly `A` via the following steps:

*   Follow the compile-time processing steps for compiling an *object-creation-expression* of the form new `T(P)`. These steps either result in a compile-time error, or determine an instance constructor C on T that can be invoked at run-time.

*   If `C` does not have public accessibility, then a compile-time error occurs.

*   For each *named-argument* `Arg` in `N`:

*   Let `Name` be the *identifier* of the *named-argument* `Arg`.

*   `Name` shall identify a non-static read-write public field or property on `T`. If `T` has no such field or property, then a compile-time error occurs.

*   If any of the values within *positional-argument-list* P or one of the values within *named-argument-list* `N` is of type `System.String` and the value is not well-formed as defined by the Unicode Standard, it is implementation-defined whether the value compiled is equal to the run-time value retrieved (§22.4.3). 
>[!NOTE]
>As an example, a string which contains a high surrogate UTF-16 code unit which isn't immediately followed by a low surrogate code unit is not well-formed.

*   Store the following information (for run-time instantiation of the attribute) in the assembly output by the compiler as a result of compiling the program containing the attribute: the attribute class `T`, the instance constructor `C` on `T`, the *positional-argument-list * `P`, the *named-argument-list* `N`, and the associated program entity `E`, with the values resolved completely at compile-time.

### Run-time retrieval of an attribute instance

The attribute instance represented by `T`, `C`, `P`, and `N`, and associated with `E` can be retrieved at run-time from the assembly `A` using the following steps:

-   Follow the run-time processing steps for executing an *object-creation-expression* of the form `new T(P)`, using the instance constructor `C` and values as determined at compile-time. These steps either result in an exception, or produce an instance `O` of `T`.

-   For each *named-argument* `Arg` in `N`, in order:

-   Let `Name` be the *identifier* of the *named-argument* `Arg`. If `Name` does not identify a non-static public read-write field or property on `O`, then an exception is thrown.

-   Let `Value` be the result of evaluating the *attribute-argument-expression* of `Arg`.

-   If `Name` identifies a field on `O`, then set this field to `Value`.

-   Otherwise, Name identifies a property on `O`. Set this property to Value.

-   The result is `O`, an instance of the attribute class `T` that has been initialized with the *positional-argument-list * `P` and the *named-argument-list* `N`.

>[!NOTE]
>The format for storing `T`, `C`, `P`, `N` (and associating it with `E`) in `A` and the mechanism to specify `E` and retrieve `T`, `C`, `P`, `N` from `A` (and hence how an attribute instance is obtained at runtime) is beyond the scope of this standard.

[*Example*: In an implementation of the CLI, the `Help` attribute instances in the assembly created by compiling the example program in §22.2.3 can be retrieved with the following program:

```csharp
using System;
using System.Reflection;

public sealed class InterrogateHelpUrls
{
    public static void Main(string[] args) {
        Type helpType = typeof(HelpAttribute);
        string assemblyName = args[0];
        foreach (Type t in Assembly.Load(assemblyName).GetTypes()) {
            Console.WriteLine("Type : {0}", t.ToString());
            HelpAttribute[] helpers =
            (HelpAttribute[])t.GetCustomAttributes(helpType, false);
            for (int at = 0; at != helpers.Length; at++) {
                Console.WriteLine("\\tUrl : {0}", helpers[at].Url);
                }
        }
    }
}
```
*end example*]

## Reserved attributes

### General

A small number of attributes affect the language in some way. These attributes include:

*   `System.AttributeUsageAttribute` (§22.5.2), which is used to describe the ways in which an attribute class can be used.

*   `System.Diagnostics.ConditionalAttribute` (§22.5.3), is a multi-use attribute class which is used to define conditional methods and conditional attribute classes. This attribute indicates a condition by testing a conditional compilation symbol.

*   `System.ObsoleteAttribute` (§22.5.4), which is used to mark a member as obsolete.

*   `System.Runtime.CompilerServices.CallerLineNumberAttribute` (§22.5.5.2), 
    `System.Runtime.CompilerServices.CallerFilePathAttribute` (§22.5.5.3), and 
    `System.Runtime.CompilerServices.CallerMemberNameAttribute` (§22.5.5.4), which are used to supply information about the calling context to optional parameters.

An execution environment may provide additional implementation-specific attributes that affect the execution of a C# program.

### The AttributeUsage attribute

The attribute `AttributeUsage` is used to describe the manner in which the attribute class can be used.

A class that is decorated with the `AttributeUsage` attribute shall derive from `System.Attribute`, either directly or indirectly. Otherwise, a compile-time error occurs.

>[!NOTE]
>For an example of using this attribute, see §22.2.2.

### The Conditional attribute

#### General

The attribute `Conditional` enables the definition of ***conditional methods*** and ***conditional attribute classes***.

#### Conditional methods

A method decorated with the `Conditional` attribute is a conditional method. Each conditional method is thus associated with the conditional compilation symbols declared in its `Conditional` attributes. [*Example*:

```csharp
using System.Diagnostics;
class Eg
{
    [Conditional("ALPHA")]
    [Conditional("BETA")]
    public static void M() {
    //…
    }
}
```
declares `Eg.M` as a conditional method associated with the two conditional compilation symbols `ALPHA` and `BETA`. *end example*]

A call to a conditional method is included if one or more of its associated conditional compilation symbols is defined at the point of call, otherwise the call is omitted.

A conditional method is subject to the following restrictions:

*   The conditional method shall be a method in a *class-declaration* or *struct-declaration*. A compile-time error occurs if the `Conditional` attribute is specified on a method in an interface declaration.

*   The conditional method shall have a return type of `void`.

*   The conditional method shall not be marked with the `override` modifier. A conditional method can be marked with the `virtual` modifier, however. Overrides of such a method are implicitly conditional, and shall not be explicitly marked with a `Conditional` attribute.

*   The conditional method shall not be an implementation of an interface method. Otherwise, a compile-time error occurs.

*   The parameters of the conditional method shall not have the out modifier.

In addition, a compile-time error occurs if a delegate is created from a conditional method.

[*Example*: The example

```csharp
#define DEBUG

using System;
using System.Diagnostics;

class Class1
{
    [Conditional("DEBUG")]
    public static void M() {
        Console.WriteLine("Executed Class1.M");
    }
}

class Class2
{
    public static void Test() {
        Class1.M();
    }
}
```
declares `Class1.M` as a conditional method. `Class2`'s `Test` method calls this method. Since the conditional compilation symbol `DEBUG` is defined, if `Class2.Test` is called, it will call `M`. If the symbol `DEBUG` had not been defined, then `Class2.Test` would not call `Class1.M`. *end example*]

It is important to understand that the inclusion or exclusion of a call to a conditional method is controlled by the conditional compilation symbols at the point of the call. [*Example*: In the following code

File `class1.cs`:

```csharp
using System.Diagnostics;

class Class1
{
    [Conditional("DEBUG")]
    public static void F() {
        Console.WriteLine("Executed Class1.F");
    }
}
```
File `class2.cs`:

```csharp
#define DEBUG

class Class2
{
    public static void G() {
        Class1.F(); // F is called
    }
}
```

File `class3.cs`:
```csharp
#undef DEBUG

class Class3
{
    public static void H() {
        Class1.F(); // F is not called
    }
}
```

the classes `Class2` and `Class3` each contain calls to the conditional method `Class1.F`, which is conditional based on whether or not `DEBUG` is defined. Since this symbol is defined in the context of `Class2` but not `Class3`, the call to F in `Class2` is included, while the call to `F` in `Class3` is omitted. *end example*]

The use of conditional methods in an inheritance chain can be confusing. Calls made to a conditional method through `base`, of the form `base.M`, are subject to the normal conditional method call rules. [*Example*: In the following code

File `class1.cs`

```csharp
using System;
using System.Diagnostics;

class Class1
{
    [Conditional("DEBUG")]
    public virtual void M() {
        Console.WriteLine("Class1.M executed");
    }
}
```
File `class2.cs`

```csharp
using System;

class Class2: Class1{
    public override void M() {
        Console.WriteLine("Class2.M executed");
        base.M(); // base.M is not called!\
    }
}
```
File `class3.cs`

```csharp
#define DEBUG

using System;

class Class3
{
    public static void Test() {
        Class2 c = new Class2();
        c.M(); // M is called
    }
}
```
`Class2` includes a call to the `M` defined in its base class. This call is omitted because the base method is conditional based on the presence of the symbol `DEBUG`, which is undefined. Thus, the method writes to the console “`Class2.M executed`” only. Judicious use of *pp-declaration*s can eliminate such problems. *end example*]

#### Conditional attribute classes

An attribute class (§22.2) decorated with one or more `Conditional` attributes is a ***conditional attribute class***. A conditional attribute class is thus associated with the conditional compilation symbols declared in its `Conditional` attributes.

[*Example*:

```csharp
using System;
using System.Diagnostics;
[Conditional("ALPHA")]
[Conditional("BETA")]
public class TestAttribute : Attribute {}
```

declares `TestAttribute` as a conditional attribute class associated with the conditional compilations symbols `ALPHA` and `BETA`. *end example*]

Attribute specifications (§22.3) of a conditional attribute are included if one or more of its associated conditional compilation symbols is defined at the point of specification, otherwise the attribute specification is omitted.

It is important to note that the inclusion or exclusion of an attribute specification of a conditional attribute class is controlled by the conditional compilation symbols at the point of the specification. [*Example*: In the example

File `test.cs`:

```csharp
using System;
using System.Diagnostics;

[Conditional(“DEBUG”)]

public class TestAttribute : Attribute {}
```

File `class1.cs`:

```csharp
#define DEBUG

[Test] // TestAttribute is specified

class Class1 {}
```
File `class2.cs`:

```csharp
#undef DEBUG

[Test] // TestAttribute is not specified

class Class2 {}
```

the classes `Class1` and `Class2` are each decorated with attribute `Test`, which is conditional based on whether or not `DEBUG` is defined. Since this symbol is defined in the context of `Class1` but not `Class2`, the specification of the Test attribute on `Class1` is included, while the specification of the `Test` attribute on `Class2` is omitted. *end example*]

### The Obsolete attribute

The attribute `Obsolete` is used to mark types and members of types that should no longer be used.

If a program uses a type or member that is decorated with the `Obsolete` attribute, the compiler shall issue a warning or an error. Specifically, the compiler shall issue a warning if no error parameter is provided, or if the error parameter is provided and has the value `false`. The compiler shall issue an error if the error parameter is specified and has the value `true`.

[*Example*: In the following code

```csharp
[Obsolete("This class is obsolete; use class B instead")]
class A
{
    public void F() {}
}

class B
{
    public void F() {}
}

class Test
{
    static void Main() {
        A a = new A(); // Warning
        a.F();
    }
}
```

the class `A` is decorated with the `Obsolete` attribute. Each use of `A` in `Main` results in a warning that includes the specified message, “This class is obsolete; use class `B` instead.” *end example*]

### Caller-info attributes

#### General

For purposes such as logging and reporting, it is sometimes useful for a function member to obtain certain compile-time information about the calling code. The caller-info attributes provide a way to pass such information transparently.

When an optional parameter is annotated with one of the caller-info attributes, omitting the corresponding argument in a call does not necessarily cause the default parameter value to be substituted. Instead, if the specified information about the calling context is available, that information will be passed as the argument value.

[*Example*:

```csharp
using System.Runtime.CompilerServices

…

public void Log(
[CallerLineNumber] int line = -1,
[CallerFilePath] string path = null,
[CallerMemberName] string name = null
)
{
    Console.WriteLine((line <; 0) ? "No line" : "Line "+ line);
    Console.WriteLine((path == null) ? "No file path" : path);
    Console.WriteLine((name == null) ? "No member name" : name);
}
```

A call to `Log()` with no arguments would print the line number and file path of the call, as well as the name of the member within which the call occurred. *end example*]

Caller-info attributes can occur on optional parameters anywhere, including in delegate declarations. However, the specific caller-info attributes have restrictions on the types of the parameters they can attribute, so that there will always be an implicit conversion from a substituted value to the parameter type.

It is an error to have the same caller-info attribute on a parameter of both the defining and implementing part of a partial method declaration. Only caller-info attributes in the defining part are applied, whereas caller-info attributes occurring only in the implementing part are ignored.

Caller information does not affect overload resolution. As the attributed optional parameters are still omitted from the source code of the caller, overload resolution ignores those parameters in the same way it ignores other omitted optional parameters (§12.6.4).

Caller information is only substituted when a function is explicitly invoked in source code. Implicit invocations such as implicit parent constructor calls do not have a source location and will not substitute caller information. Also, calls that are dynamically bound will not substitute caller information. When a caller-info attributed parameter is omitted in such cases, the specified default value of the parameter is used instead.

One exception is query expressions. These are considered syntactic expansions, and if the calls they expand to omit optional parameters with caller-info attributes, caller information will be substituted. The location used is the location of the query clause which the call was generated from.

If more than one caller-info attribute is specified on a given parameter, they are preferred in the following order: `CallerLineNumber`, `CallerFilePath`, `CallerMemberName`.

#### The CallerLineNumber attribute

The `System.Runtime.CompilerServices.CallerLineNumberAttribute` is allowed on optional parameters when there is a standard implicit conversion (§11.2.2) from the constant value `int.MaxValue` to the parameter’s type. This ensures that any non-negative line number up to that value can be passed without error.

```csharp
namespace System.Runtime.CompilerServices
{
    [AttributeUsageAttribute(AttributeTargets.Parameter, Inherited = false)]
    public sealed class CallerLineNumberAttribute : Attribute
    
    {
        public CallerLineNumberAttribute() 
            {…}
    
    }
}
```
If a function invocation from a location in source code omits an optional parameter with the `CallerLineNumberAttribute`, then a numeric literal representing that location's line number is used as an argument to the invocation instead of the default parameter value.

If the invocation spans multiple lines, the line chosen is implementation-dependent.

The line number may be affected by `#line` directives (§7.5.8).

#### The CallerFilePath attribute

The `System.Runtime.CompilerServices.CallerFilePathAttribute` is allowed on optional parameters when there is a standard implicit conversion (§11.2.2) from `string` to the parameter’s type.

```csharp
namespace System.Runtime.CompilerServices
{
    [AttributeUsageAttribute(AttributeTargets.Parameter, Inherited = false)]
    public sealed class CallerFilePathAttribute : Attribute
    
    {
        public CallerFilePathAttribute() {…}
    
    }
}
```
If a function invocation from a location in source code omits an optional parameter with the `CallerFilePathAttribute`, then a string literal representing that location's file path is used as an argument to the invocation instead of the default parameter value.

The format of the file path is implementation-dependent.

The file path may be affected by `#line` directives (§7.5.8).

#### The CallerMemberName attribute

The `System.Runtime.CompilerServices.CallerMemberNameAttribute` is allowed on optional parameters when there is a standard implicit conversion (§11.2.2) from `string` to the parameter’s type.
```csharp
namespace System.Runtime.CompilerServices
{
    [AttributeUsageAttribute(AttributeTargets.Parameter, Inherited = false)]
    public sealed class CallerMemberNameAttribute : Attribute
    
    {
    public CallerMemberNameAttribute() 
        {…}
    
    }
}
```
If a function invocation from a location within the body of a function member or within an attribute applied to the function member itself or its return type, parameters or type parameters in source code omits an optional parameter with the `CallerMemberNameAttribute`, then a string literal representing the name of that member is used as an argument to the invocation instead of the default parameter value.

For invocations that occur within generic methods, only the method name itself is used, without the type parameter list.

For invocations that occur within explicit interface member implementations, only the method name itself is used, without the preceding interface qualification.

For invocations that occur within property or event accessors, the member name used is that of the property or event itself.

For invocations that occur within indexer accessors, the member name used is that supplied by an `IndexerNameAttribute` (§) on the indexer member, if present, or the default name `Item` otherwise.

For invocations that occur within field or event initializers, the member name used is the name of the field or event being initialized.

For invocations that occur within declarations of instance constructors, static constructors, finalizers and operators the member name used is implementation-dependent.

## Attributes for interoperation

For interoperation with other languages, an indexer may be implemented using indexed properties. If no `IndexerName` attribute is present for an indexer, then the name `Item` is used by default. The `IndexerName` attribute enables a developer to override this default and specify a different name.

```csharp
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Property)]
    public class IndexerNameAttribute: Attribute
    {
        public IndexerNameAttribute(string indexerName) {…}
    
        public string Value { get {…} }
    }
}
```