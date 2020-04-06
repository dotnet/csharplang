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
An init only field is declared by using the 

An init only field is recognized with the `init` modifier.

```cs
struct Point {
    public init X;
}
```

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

A field or property which is marked as `init` is considered settable in the
following circumstances:
1. For members of the type, or derived types, that defines the field / property
  1. Inside the constructor
  1. Inside `init` accessors
1. From inside a constructor of the type that defines the member or derives 
from the type that defines the member

The rules should specifically allow the following:

```cs
class Base {
    protecetd init bool Prop1;
}

class Derived : Base {
    protected int Prop2 {
        get => 42;
        init => Prop1 = true;
    }

    Dervide() {
        Prop1 = false;
        Prop2 = 13;
    }
}
```

Detailed Info:
- An `init` accessor cannot be combined with a `set` accessor
- A member of `readonly struct` can be decorated with a `init` modifier
- The `init` modifier is only legal on instance fields and properties, it is 
not legal on `static` members
- The `InitOnlyAttribute` is recognized by full name. It does not need an 
- identity requirement

### InitOnlyAttribute

**Mention that InitOnlyAttribute is emitted as needed by compiler**

### Metadata encoding 

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

### Is removing init from a property a breaking change

## Considerations


### Compatibility

### Warn on failed init

### Generate three accessors


