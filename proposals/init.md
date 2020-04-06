Init Only Members
=====

## Summary
This proposal adds the concept of init-only members to C#. Members which can be
written at the point of objection creation but become `readonly` once object
creation has completed. This allows for a much more flexible immutable model
in C#. 

## Motivation
The underlying mechanisms for building immutable data in C# haven't changed
since 1.0. They remain

1. Declaring fields as `readonly`.
1. Declaring properties that contain only a `get` accessor.

These mechanisms are effective at allowing the construction of immutable data
but they do so by adding cost to the boiler plate code of types and opting
such types out of features like object and collection initializers. This means
developers must choose between easy of use and immutability.

Simple immutable object like `Point` requires twice as much boiler plate code
to support construction as it does to declare the type. The bigger the type 
the bigger the cost of this boiler plate:

```cs
struct Point
{
    public readonly int X;
    public readonly int Y;

    public Point(int X, int Y)
    {
        this.X = x;
        this.Y = y;
    }
}
```

The `init` modifier makes immutable objects more flexible by allowing the
caller to mutate the members during the act of construction. That means the 
object can participate in object initialzers and thus removes the need for 
all boiler plate code in the type. The `Point` type is now simply:

```cs
struct Point
{
    public init int X;
    public init int Y;
}
```

The consumer can then use object initializers to create the object

```cs
var p = new Point() { X = 42, Y = 13 };
```

## Detailed Design

### init members
An init only field is declared by using the `init` modifier. 

```cs
class Student
{
    public init FirstName;
    public init LastName;
}
```

An instance field marked with `init` is considered writable in the following
circumstances:

- During an object initializer
- Inside an instance constructor of the containing or derived type
- Inside the `set` accessor of an `init` property

This means the `Student` class can be used in the following ways:

```cs
var s = new Student()
{
    FirstName = "Jared",
    LastName = "Parosns",
};
s.LastName = "Parsons"; // Error: LastName is `readonly`.
```

The rules around setting a `init` field inside a constructor allow the 
following (just as simply `readonly` would):

```cs
class Base
{
    protected init bool Value;
}

class Derived : Base
{
    Derived()
    {
        Value = true;
    }
}
```

An instance property can likewise add the `init` modifier to the `set`
accessor. That will extend the places the `set` can be used to include all
the places an `init` field can be written. That means the `Student` class could
also be written as follows:

```cs
class Student
{
    public string FirstName { get; init set; };
    public string LastName { get; init set; };
}
```

When `init set` is used in a virtual property then all the overrides must also
be marked as `init set`. Likewise it is not possible to override a simple 
`set` with `init set`.

In the same way the `readonly` modifier can be applied to a `struct` to 
automatically declare all fields as `readonly`, the `init` only modifier can
be declared on a `struct` or `class` to automatically mark all fields as `init`.
This means the following two type declarations are equivalent:

```cs
struct Point
{
    public init int X;
    public init int Y;
}

// vs. 

init struct Point
{
    public int X;
    public int Y;
}
```

Restrictions of this feature:
- The `init` modifier can only be used on:
    - Instance fields of a `class` or `struct`. Use on `static` fields are 
    illegal
    - Instance property `set` accessors inside a `class` or `struct`.
- A field can be marked as `readonly` and `init` but will function as `init`
- The fields of a `readonly struct` can be marked as `init`. 
- All overrides of a property `set` must match the original declaration with
respect to `init`

### InitOnlyAttribute

**Mention that InitOnlyAttribute is emitted as needed by compiler**
- The `InitOnlyAttribute` is recognized by full name. It does not need an 
- identity requirement

### Metadata encoding 

An `init` field will be emitted as a `readonly` field that is marked with an 
`InitOnlyAttribute` instance.

```cs
// Emitted as 
struct Point {
    [InitOnly]
    public readonly X;
}
```

An init only property setter is recoginized by using `init` in place of a 
`set` accessor. 

```cs
struct Student {
    public string Name { get; init; }
}
```

This type of property will be emitted as a normal `set` accessor but will be 
marked with the `InitOnlyAttribute`:

```cs
struct Student {
    public string Name { get; [InitOnly]set; }
}
```


## Open Questions

### Mod reqs vs. attributes
The emit strategy for `init` property accessors must choose between using 
attributes or modreqs when emitting during metadata. These have different 
trade offs that need to be considered.

Annotating a property set accessor with a modreq declaration means CLI compliant
compilers will ignore the accessor unless it understands the modreq. That means
only compilers aware of `init` will read the member. Compilers unaware of 
`init` will ignore the `set` member and hence will not accidentally treat the
property as read / write. 

The downside of modreq is `init` becomes a part of the binary signature of 
the `set` accessor. Adding or removing `init` will break binary compatbility 
of the application.

Using attributes to annotate the `set` accessor means that only compilers which
understand the attribute will know to limit access to it. A compiler unaware 
of `init` will see it as a simple read / write property and allow access.

This would seemingly mean this decision is a choice between extra safety at 
the expense of binary compatibility. Digging in a bit the extra safety is not
exactly what it seems. It will not for instance protect against the following
circumstances:

1. Reflection over `public` members
1. The use of `dynamic` 
1. Compilers that don't recognize modreqs

It should also be considered that when we complete the IL verification rules 
for .NET 5, `init` will be one of those rules. that means extra enforcement 
will be gained from simply verifying compilers emitting verifiable IL.

The primary languages for .NET (C#, F# and VB) will all be updated to 
recognize these `init` accessors. Hence the only realistic scenario here is 
when a C# 9 compiler emits `init` properties and they are seen by a C# 8 
compiler. That is the trade off to consider and weigh against binary 
compatibility.

Note: this discussion applies to the accessor members only, not to fields. There
is no value to be gained by using a modreq on a field. The `init` feature for 
fields is a relaxation of an existing rule. All existing compilers already 
support `readonly` and hence an attribute serves fine as a way to alert them
that write access can be extended in certain circumstances.

### init only struct
Given that we allow for a `readonly struct` declaration to implicitly declare
all members `readonly` should we likewise allow for a `init struct` to 
implicitly declare all members `init`?

```cs
init struct Point {
    public int X, Y;
}

// Generates as 
struct Point {
    public init X;
    public init Y;
}
```

### virtual members
If a `virtual` property has an `init` setter do the derived properties also
need to have an `init` setter? Pretty sure yes but I need to sit down and 
think through it.

## init members

If we ever want them then we need to move `init` to the `set` accessor 
instead of the property body

### Is removing init from a property a breaking change

## Considerations


### Compatibility

### Warn on failed init

### Generate three accessors


