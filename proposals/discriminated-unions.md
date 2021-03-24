
# Discriminated unions / `enum class`

`enum class`es are a new kind of type declaration, sometimes referred to as discriminated unions,
where each every possible instance the type is listed, and each instance is non-overlapping.

An `enum class` is defined using the following syntax:

```antlr
enum_class
    : 'partial'? 'enum class' identifier type_parameter_list? type_parameter_constraints_clause* 
      '{' enum_class_body '}'
    ;

enum_class_body
    : enum_class_cases?
    | enum_class_cases ','
    ;

enum_class_cases
    : enum_class_case
    | enum_class_case ',' enum_class_cases
    ;

enum_class_case
    : enum_class
    | class_declaration
    | identifier type_parameter_list? '(' formal_parameter_list? ')'
    | identifier
    ;

```

Sample syntax:

```C#
enum class Shape
{
    Rectangle(float Width, float Length),
    Circle(float Radius),
}
```

## Semantics

An `enum class` definition defines a root type, which is an abstract class of the same name as
the `enum class` declaration, and a set of members, each of which has a type which is a subtype
of the root type. If there are multiple `partial enum class` definitions, all members will be
considered members of the enum class definition. Unlike a user-defined abstract class definition,
the `enum class` root type is partial by default and defined to have a default *private*
parameter-less constructor.

Note that, since the root type is defined to be a partial abstract class, partial definitions of
the *root type* may also be added, where standard syntax forms for a class body are allowed.
However, no types may directly inherit from the root type in any declaration, aside from those
specified as `enum class` members. In addition, no user-defined constructors are permitted for
the root type.

There are four kinds of `enum class` member declarations:

* simple class members

* complex class members

* `enum class` members

* value members.

### Simple class members

A simple class member declaration defines a new nested "record" class (intentionally left undefined in
this document) with the same name. The nested class inherits from the root type.

Given the sample code above,

```C#
enum class Shape
{
    Rectangle(float Width, float Length),
    Circle(float Radius)
}
```

the `enum class` declaration has semantics equivalent to the following declaration

```C#
 abstract partial class Shape
{
    public record Rectangle(float Width, float Length) : Shape;
    public record Circle(float Radius) : Shape;
}
```

### Complex class members

You can also nest an entire class declaration below an `enum class` declaration. It will be treated as
a nested class of the root type. The syntax allows any class declaration, but it is required for the
complex class member to inherit from the direct containing `enum class` declaration. 

### `enum class` members

`enum classes` can be nested under each other, e.g.

```C#
enum class Expr
{
    enum class Binary
    {
        Addition(Expr left, Expr right),
        Multiplication(Expr left, Expr right)
    }
}
```

This is almost identical to the semantics of a top-level `enum class`, except that
the nested enum class defines a nested root type, and everything below the nested enum
class is a subtype of the nested root type, instead of the top-level root type.

```C#
abstract partial class Expr
{
    abstract partial class Binary : Expr
    {
        public record Addition(Expr left, Expr right) : Binary;
        public record Multiplication(Expr left, Expr right) : Binary;
    }
}
```

### Value members

`enum classes` can also contain value members. Value members define public get-only static
properties on the root type that also return the root type, e.g.

```C#
enum class Color
{
    Red,
    Green
}
```

has properties equivalent to

```C#
partial abstract class Color
{
    public static Color Red => ...;
    public static Color Green => ...;
}
```

The complete semantics are considered an implementation detail, but it is guaranteed that
one unique instance will be returned for each property, and the same instance will be returned
on repeated invocations.


### Switch expression and patterns

There are some proposed adjustments to pattern matching and the switch expression to handle
`enum classes`. Switch expressions can already match types through the variable pattern, but
for currently for reference types, no set of switch arms in the switch expression are considered
complete, except for matching against the static type of the argument, or a subtype.

Switch expressions would be changed such that, if the root type of an `enum class` is the static
type of the argument to the switch expression, and there is a set of patterns matching all
members of the enum, then the switch will be considered exhaustive.

Since value members are not constants and do not define new static types, they currently cannot
be matched by pattern. To make this possible, a new pattern using the constant pattern syntax
will be added to allow match against `enum class` value members. The match is defined to succeed
if and only if the argument to the pattern match and the value returned by the `enum class` value
member would be reference equal, although the implementation is not required to perform this
check.


## Open questions

- [ ] What does the common type algorithm say about `enum class` members? Is this valid code?
    * `var x = b ? new Shape.Rectangle(...) : new Shape.Circle(...)`

- [ ] Adding a new pattern just for value members seems heavy handed. Is there a more general version
      construction that makes sense?
    - [ ] Value members also do not map well to a parallel nested class construction because of this

- [ ] Is switching against an argument with an `enum class` static type guaranteed to be constant-time?

- [ ] Should there be a way to make `enum class`es not be considered complete in the switch
      expression? Prefix with `virtual`?

- [ ] What modifiers should be permitted on `enum class`?
