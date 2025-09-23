# Braced Union Syntax

## Summary

Nominal type unions are declared using a comma separated list of case members inside braces.

Some unions declare fresh cases like an enum does.

```csharp
union Gate
{
    Locked,             // declaration of Gate.Locked
    Closed,
    Open(float Percent) // declaration with a parameter list
}
```

While others refer to existing types as cases.

```csharp
record Cat(...);
record Dog(...);

union Pet
{
    Cat,                // reference to type Cat
    Dog
}
```

And some do both.

```csharp
union Result<T>
{
    T,                      // reference to type T
    Error(string Message)   // declaration of Result<T>.Error
}
```

Notice, there is an ambiguity here since simple named members sometimes refer to existing types and other times refer to declarations of fresh ones. How this ambiguity is resolved is the crux of the issues surrounding syntax for unions.

#### Disambiguating

The following is an explanation of how the proposal disambiguates without introducing additional syntax.

If a union member is a simple name that refers to a type in scope, it is a type reference.

```csharp
record Cat(...);
record Dog(...);

union Pet
{
    Cat,        // reference to type Cat
    Dog
}
```

If a union member is a simple name that does not refer to a type in scope, it is a declaration.

```csharp
union Gate
{
    Locked,     // declaration of Gate.Locked
    Closed,
    Open(float Percent);
}
```

If a union member is a simple name with a parameter list, it is a declaration.

```csharp
union Gate
{
    Locked,
    Closed,
    Open(float Percent);    // declaration of Gate.Open
}
```

If a union member is not a simple name, it is a type reference.

```csharp
union SomeUnion
{
    System.Int32,           // dotted name can only be reference 
    Int32?,                 // nullable types can only be references
    int,                    // keyword type can only be reference
    Int32[],                // array can only be reference
    IEnumerable<Int32>      // type arguments can only exist on references
}
```

To disambiguate a simple name to force it to be a declaration instead of a type reference add an empty parameter list.

```csharp
using Locked=Int32;

union Gate
{
    Locked(),                // declaration of Gate.Locked
    Closed,
    Open(float Percent)
}
```

#### Other Declarations

To include additional kinds of declarations within the braces, use a semicolon at the end of the case member list.

```csharp
union Pet
{
    Cat,
    Dog,
    Bird;

    public bool HasPaws => this is Cat or Dog;
}
```

*Note: The declaration or type reference list can be thought of as a single declaration in the body of the type. The semicolon is not acting as a separator between the two sections, it is a terminator for the case member declaration as with other declarations. However, it is optional if the case member list is the only declaration.*

## Motivation

To have a nominal union syntax that works equally well for declaring unions of existing types and declaring unions of fresh types in a single list.

Having a union declaration syntax be similar to enum syntax is ideal, since it leans on the existing enum concept of a set of finite values or states. An enum-like syntax is one that appears as a list of simple names with the one addition that members may include declarations of associated values.

This proposal tries to find a compromise between type references and type declarations that maintains this connection.


## Specification

This is a specification for an alternate syntax for nominal type unions. It otherwise depends on the existing nominal type union spec and case declarations spec, even though it eliminates the need for case declarations within union declarations.

### Syntax

```antlr
union_declaration: 
    attributes? struct_modifier* 'partial'? 
    'union' identifier type_parameter_list?
    type_parameter_constraints_clause* 
    `{` union_body `}` 
    ;

union_body:
    union_member_list
    (';' struct_member_declaration*)?
    ;

union_member_list: 
    union_member (',' union_member)*
    ;

union_member: 
    type 
    | identifier parameter_list
    ;
```

### Member Declarations

Union member declarations are translated to nested records with the same declaration. This is done using the same rules defined in case declarations feature, except members cannot declare type parameters, base types or bodies.

```csharp
union Pet
{
    Cat(string Name, string Personality),
    Dog(string Name, bool Friendly),
    Bird(string Name, string Species),
    None
}
```

Translates to:

```csharp
record struct Pet : IUnion
{
    public Pet(Cat value) { this.Value = value; }
    public Pet(Dog value) { this.Value = value; }
    public Pet(Bird value) { this.Value = value; }
    public object? Value { get; }

    public record Cat(string Name, string Personality);
    public record Dog(string Name, bool Friendly);
    public record Bird(string Name, string Species);
    public record None;
}
```

To describe a more full featured case type, you must declare the case types separately. They may be nested types.

```csharp
union Pet
{
    Cat,
    Dog,
    Bird,
    None;

    public record Cat(string Name, string Personality) : IHavePaws {...};
    public record Dog(string Name, bool Friendly) : IHavePaws {...};
    public record Bird(string Name, string Species) { ... };
    public record None {...};
}
```

### Type Reference Scope

The scope available for members to be type references is the body of the union itself, including any type parameters declared.

Unions can refer to their own nested types.
```csharp
union Shape
{
    Point,
    Line,
    Circle;

    public class Point {...};
    public class Line {...};
    public class Circle {...};
}
```

Unions can refer to types that are their own type parameters.

```csharp
union Union<T1, T2> { T1, T2 };
```

Or combination thereof.

```csharp
union OneOrMore<T> 
{ 
    T, 
    IEnumerable<T>,
    Two;

    public record Two(T First, T Second) : IEnumerable<T> {...};
};
```

A union member can refer to another union member, but that would be an error to include the same member twice.

```csharp
union Pet
{
    Cat(...),
    Cat
}
```

## Concerns

* Simple name declarations can accidentally become references if same named types are introduced in scope.

    > This may not be a big concern, since this kind of change will likely impact use sites and cause build breaks.  And it can be avoided be using parentheses to disambiguate.

    > Possible solution may be to add a warning when a simple name matches a type in scope and the union also has member declarations.

    ```csharp
    record Cat(...);

    union Pet 
    {
        Cat,        // warning - Identifier refers to external type in 
        Dog(...)    //           union with member declarations.
    }
    ```

* Simple names type references can unexpectedly become declarations if/when the type is removed or no longer in scope.

    > No solution yet for this one.    

* There is no easy grow up story for declared members.  

    > Having to declare the member fully as a nested type is not that much hardship when deviating from the common case.


## Alternatives

The inability to be certain about the nature of simple names in the primary proposal leads to a desire to explore alternatives that are easier to digest.

### Simple Names are Type References

In this alternative, all simple names are interpreted as type references. The only way to specify a member declaration in the member list is to include a parameter list, if only an empty one.

```csharp
union Pet 
{ 
    Cat,                        // reference to existing type or error
    Dog
}

union Gate
{
    Locked(),                   // declaration of Gate.Locked
    Closed(),
    Open(float Percent)
}
```

#### Variation

A variation of this is to require additional syntax on all declarations.

```csharp
union Pet 
{ 
    Cat,                        // reference to existing type or error
    Dog
}

union Gate
{
    case Locked,                // declaration of Gate.Locked
    case Closed,
    case Open(float Percent)
}
```

This eliminates the need to put parentheses at the end of member declarations, but it does require non-enum-like syntax for the main enum-like scenario.

It also may appear awkward when mixed.

```csharp
record Cat(...);

union Pet
{
    Cat,
    case Dog(...);
}
```

For example, it's non-intuitive why the declaration is the only one denoted as a case, when they are all cases. Possibly a different keyword would work better.

```csharp
union Pet
{
    Cat,
    declare Dog(...);
}
```

Downsides:

* It is no longer enum-like. You cannot simply start with an enum and upgrade it to a union without adding parentheses to all members.


### Simple Names Are Declarations

In this alternative, all simple names are considered to be declarations. Instead, type references require additional syntax. This makes union syntax be enum-like by default for declarations.

```csharp
union Gate
{
    Locked,                     // declaration of Gate.Locked
    Closed,
    Open(float Percent)
}
```

Additional punctuation indicates it is a type reference.

```csharp
union Pet
{   
    ~Cat,                       // reference to existing type or error
    ~Dog
}
```

Other single character punctuation options:

```csharp
^Cat,       
`Cat,       // finally the back-tick
:Cat,
=Cat,       // maybe
>Cat,
!Cat,
?Cat,
$Cat,
&Cat,
*Cat,
/Cat,
+Cat,
-Cat,
[Cat]
(Cat)
<Cat>
```

#### Variation: Keywords

A keyword indicates the name is a type reference.

```csharp
union Pet
{
    type Cat,                     // reference to existing type or error
    type Dog
}
```
*Note: An alternative to this alternative is to only require the extra syntax for simple names (identifiers). Unfortunately, simple name type references are the common case.*

Downsides:
* Type references end up requiring additional syntax making the union declaration feel biased toward being used with declarations when in fact the underlying union concept is the opposite.

#### Variation: Conditional

Simple names are declarations if other declarations also exist in the same union declaration, otherwise they are type references.

This alternative determines the meaning of simple names depending on the meaning of other members.  If any other member has a parameter list then simple names are member declarations too.

```csharp
union Pet
{
    Cat,               // type reference since no members have parameter lists
    Dog
}

union Gate
{
    Locked,            // declaration of Gate.Locked
    Closed,
    Open(float Percent)
}
```

This alternative works well since the common case is that unions have case members that are either all references or all declarations, and those with declarations will have at least one with parameters.

However, there would be no way to include a type that can only be specified as a simple name when there is also a declaration.

```csharp
union Result<T>
{
    T,                      // oops, this is a now a declaration 
    Error(string message)
}
```

A variant of this variation could introduce an optional syntax to disambiguate, but would have all the same issues as the other alternate syntaxes.

```csharp
union Result<T>
{
    ~T,
    Error(string message)
}
```

Downsides:
* The condition makes it cumbersome to understand the meaning of simple names.
* It still needs a disambiguation syntax.

### Separate Indicator

In this alternative some part of syntax outside the braces indicates the meaning of simple named members inside the braces.

In this example, the keyword `type` is added to the declaration to indicate that simple names inside the braces mean references to existing types, not declarations.

```csharp
type union Pet
{
    Cat,        // reference to existing type or error
    Dog,
    Bird
}
```

Without the additional keyword, the default meaning is a fresh case member.
```csharp
union Gate
{
    Locked,     // declaration of Gate.Locked
    Closed,
    Open(float Percent)
}
```

To have a mix of both simple named type references and declarations, you must disambiguate by adding empty parameter lists to the declarations.

```csharp
record Unknown;

type union Gate
{
    Unknown,          // reference to external type
    Locked(),         // reference to Gate.Locked
    Closed(),
    Open(float Percent)
}
```

Downsides:
* There now seems to be two different almost identical syntaxes and the reader must be aware which is in use to understand the meaning of a simple name.

### Type References Outside

In this alternative all type references are specified somewhere outside the braces leaving the body to only specify declarations. This allows a declaration-only union to remain enum-like and takes advantage of the intuition that braces inside declarations contain other declarations.

The example includes a list of references to existing types inside parentheses, similar to the current plan-of-record proposal, except also including the enum-like syntax for fresh case member declarations.

```csharp
union Pet (Cat, Dog, Bird);  // references to existing types

union Gate
{
    Locked,                 // declaration of Gate.Locked
    Closed,
    Open(float Percent)
}
```

#### Variation: Brackets

This alternative changes the parentheses to square brackets to avoid similarity with primary constructors.

```csharp
union Pet [Cat, Dog, Bird];  // clearly not a primary constructor or declarations

union Gate
{
    Locked,                 // declaration of Gate.Locked
    Closed,
    Open(float Percent)
}
```

This similarity of the square brackets to collection expressions helps reinforce that the members listed are not declarations.

#### Variation: Keyword list

This alternative is similar to the 'allows' keyword proposal, but it retains the enum-like syntax for declarations inside the braces.

A keyword initiated clause with list of type expressions appears somewhere in the syntax after the name and possible type parameters.

```csharp
union Pet allows Cat, Dog, Bird;

union Gate
{
    Locked,
    Closed,
    Open(float Percent)
}
```

Other example keywords:

```csharp
union Pet includes Cat, Dog, Bird;
union Pet contains Cat, Dog, Bird;
union Pet has Cat, Dog, Bird;
union Pet with Cat, Dog, Bird;
union Pet of Cat, Dog, Bird;
```

Downsides:
* The keyword implies meaning that may not correctly describe what is going on. 
* It makes the case members appear to be more of an addendum than the core of the declaration.

## Extra Credit

### Declarations Outside Braces

This alternative moves away from using braces as a means of listing case types, allowing declarations to occur in a type list separate from any body.

```csharp
union Pet(Cat, Dog, Bird);  // type references

union Gate(Locked(), Closed(), Open(float Percent));  // declarations
```

It has all the issues as the braces syntax, requiring the same kinds of solutions, but does not try to be enum-like.

#### Variation: Leaving the Nest

A variation of this proposal allows for declarations both inside and outside of the braces. Declarations inside the braces become nested, while declarations outside the braces become types in the same declaration scope as the union.

```csharp
namespace NS;

union Gate
(
    Locked(),  // declaration of NS.Locked
    Closed(),
    Open(float Percent)
);

union Gate
{
    Locked(),   // declaration of Gate.Locked
    Closed(),
    Open(float Percent)
}
```
