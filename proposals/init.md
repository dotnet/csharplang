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
    public init string FirstName;
    public init string LastName;
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

The rules around when `init` is settable extend across type hierarchies. If the 
member is accessible and the object is known to be in the construction phase
then the member is settable. That specifically allows for the following:

```cs
class Base
{
    public init bool Value;
}

class Derived : Base
{
    Derived()
    {
        // Not allowed by readonly but is allowed by init
        Value = true;
    }
}

class Consumption
{
    void Example()
    {
        var d = new Derived() { Value = true; };
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

At the point a `init set` member is invoked the instance is known to be 
in the open construction phase. Hence an `init set` is free to call other 
`init set` members as well as modify `init` or `readonly` fields.

```cs
class Complex
{
    readonly int Field1;
    int Field2;
    int Prop1 { get; init set; }
    int Prop2
    {
        get => 42;
        set
        {
            Field1 = 13; // okay
            Field2 = 13; // okay
            Prop1 = 13; // okay
        }
    }
}
```

When `init set` is used in a virtual property then all the overrides must also
be marked as `init set`. Likewise it is not possible to override a simple 
`set` with `init set`.

An `interface` declaration can also particpate in `init` style initalization 
via the following pattern:

```cs
interface IPerson
{
    string Name { get; init set; }
}

class Init
{
    void M<T>() where T : IPerson, new()
    {
        var local = new T()
        {
            Name = "Jared"
        };
        local.Name = "Jraed"; // Error
    }
}
```

Restrictions of this feature:
- The `init` modifier can only be used on:
    - Instance fields of a `class` or `struct`. Use on `static` fields are 
    illegal
    - Instance property `set` accessors on `class`, `struct` or `interface`.
- The `init` modifier cannot be paired with `readonly`. 
- All overrides of a property `set` must match the original declaration with
respect to `init`. This includes implementing interface members.

### Metadata encoding 
The `init` members will be encoded using the attribute
`System.Runtime.CompilerServices.InitOnlyAttribute` which will have the
following declaration:

```cs
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All)]
    public sealed class InitOnlyAttribute : Attribute
    {

    }
}
```

This attribute will be matched by full name. There is no requiremnet that it 
appear in the core library. In the case there are mulitple attributes by this
name available then the compiler will pick the one defined in the core library
should one exist.

An `init` field will be emitted as a `readonly` field that is marked with an 
`InitOnlyAttribute` instance.

```cs
struct Circle
{
    public init int Radius;
}

// Emitted as

struct Circle
{
    [InitOnly]
    public readonly int Radius;
}
```

An `init set` method will be emitted as a normal `set` accessor where the
`value` parameter contains a modreq referring to `InitOnlyAttribute`.


```cs
struct Circle
{
    public int Radius { get; init set; }
}

// Emitted as

struct Circle
{
    public int Radius { get; modreq(InitOnly) set; }
}
```

## Questions

### Breaking changes
One of the main pivot points in how this feature is encoded will come down to
the following question: 

> Is it a binary breaking change to remove `init` from a `set`?

Removing `init`, and thus making a field or property fully writable is never
a source breaking change. For fields it is never a binary breaking change 
either. Additionally removing it from a field is never a binary breaking 
change. The only behavior up in question is whether or not this remains 
true for a property. 

If we want to make the removal of `init` from a property a compatible change
then it will force our hand on the modreq vs. attributes decision below. If 
one the other hand this is seen as a non-interesting scenario then this will 
make the modreq vs. attribute decision less impactful.

**Resolution**
This scenario is not seen as compelling by LDM.

### Modreqs vs. attributes
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

**Resolution**
The feature will use a modreq to encode the property `init` setter. The
compelling factors were (in no particular order):

1. Desire to discourage older compilers from violating `init` semantics
1. Desire to make adding or removing `init` in a `virtual` declaratio or 
`interface` both a source and binary breaking change.

Given there was also no significant support for removing `init` to be a 
binary compatible change it made the choice of using modreq straight forward.

### init vs. initonly
Syntax debate time.

**Resolution**
There was no other syntax which was overwelming favored in LDM. For the time 
being we will be moving forward with the initial design laid out in this 
document. This may change as the feature progresses and LDM members have time
to digest the design here.

### Warn on failed init
Consider the following scenario. A type declares an `init` only member which
is not set in the constructor. Should the code which constructs the object 
get a warning if they failed to initialize the value?

At that point it is clear the field will never be set and hence has a lot of
similarities with the warning around failing to initialize `private` data. 
Hence a warning would seemingly have some value here?

There are significant downsides to this warning though:
1. It complicates the compatibility story of changing `readonly` to `init`. 
1. It requires carrying additional metadata around to denote the members
which are required to be initialized by the caller.

Further if we believe there is value here in the overall scenario of forcing
object creators to be warned / error'd about specific fields then this 
likely makes sense as a general feature. There is no reason it should be 
limited to just `init` members.

**Resolution**
There will be no warning on consumption of `init` fields and properties.

LDM wants to have a broader discussion on the idea of required fields and
properties. That may cause us to come back and reconsider our position on
`init` members and validation.

### Allow init as a type modifier
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

**Resolution** 
This feature is too *cute* here and conflicts with the `readonly struct` 
feature on which it is based. The `readonly struct` feature is simple in that 
it applies `readonly` to all members: fields, methods, etc ... The 
`init struct` feature would only apply to fields. This actually ends up making
it confusing for users. 

Given that `init` is only valid on certain aspects of a type we rejected the 
idea of having it as a type modifier.

## Considerations

### Compatibility
The `init` feature is designed to be compatible with existing data types. 
Specifically it is meant to be a completely additive change for data which is
`readonly` today but desires more flexible object creation semantics.

For example consider the following type:

```cs
class Name
{
    public readonly string First;
    public readonly string Last;

    public Name(string first, string last)
    {
        First = first;
        Last = last;
    }
}
```

It is not a breaking change to use `init` in place of `readonly` here:

```cs
class Name
{
    public init string First;
    public init string Last;

    public Name(string first, string last)
    {
        First = first;
        Last = last;
    }
}
```

The same applies for changing a `get` only property to have an `init set` 
accessor.

### IL verification
When .NET Core decides to re-implement IL verify the rules will need to be 
adjusted to account for `init` members. This will need to be included in the 
rule changes for non-mutating acess to `readonly` data.

## init members
The `init` modifier could be extended to apply to all instance members. This 
would generalize the concept of `init` during object construction and allow
types to declare helper methods that could partipate in the construction 
process to initialize `init` fields and properties.

Such members would have all the restricions that an `init set` accessor does
in this design. The need is questionable though and this can be safely added
in a future version of the language in a compatible manner.

### Generate three accessors
One potential implementation of `init` properties is to make `init` completely
separate from `set`. That means that a property can potentially have three 
different accessors: `get`, `set` and `init`.

This has the potential advantage of allowing the use of modreq to enforce 
correctness while maintaining binary compatibility. The implementation would
roughly be the following:

1. An `init` accessor is always emitted if there is a `set`. When not defined 
by the developer it is simply a reference to `set`. 
1. The set of a property in an object initializer will always use `init` if 
present but fall back to `set` if it's missing.

This means that a developer can always safely delete `init` from a property. 

The downside of this design is that is only useful if `init` is **always** 
emitted when there is a `set`. The language can't know if `init` was deleted
in the past, it has to assume it was and hence the `init` must always be
emitted. That would cause a significant metadata expansion and is simply not
worth the cost of the compatibility here.
