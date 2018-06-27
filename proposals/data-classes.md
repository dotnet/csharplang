
# Working with Data

When we talk about C# and start to talk about data, the conversation often
moves to talk about advanced data structures and complicated use cases. Here
I'd like to do the opposite: talk about simple data and the representations
we use for it.

To start, let's talk about what data is and what it isn't.

Data *is* a collection of values with potentially heterogenous types. Some
examples include

* Database rows
* JSON/XML messages
* Login info
* Configuration options

Data *is not*

* A process
* A computation
* A conversation
* Interactive
* An object

What you can do with data

* Name it
* Read it
* Modify it (or prevent modification)
* Compose it
* Compare it
* Copy it

What you can't do

* Call it
* Query it

For an object-oriented language this may seem strange, because isn't
everything an object? In some sense, you can view data as a degenerate object
-- fields with pure transparency. But this also misses the point. The point
of object-oriented architecture is to bundle state and behavior and provide
composable objects that can interactively respond to the system, like cells
in an organism. There's value in this structure, but it also creates a
binding between the data and the behavior. By creating data individually we
allow the data to shift contexts and allow other components to define their
own behaviors.

## What does it look like in C#?

C# has a couple different ways to represent simple data, but I contend that
all have fundamental problems.

**Anonymous types**. Things look pretty good at first: you can name, it
can read it, and you can compare it easily (equality is automatically
defined). You can't modify it, though -- anonymous types are always immutable
and you can't easily create a copy with only one change. The real problem
is composability. You can't use it as a real type anywhere outside the
current method and you can't nest it in other data structures, except as
an object.

**Tuples**. Tuples are a lot like anonymous types. You can provide names,
read the elements, modify them, and compare them. You can't make them
immutable, but the real problem is in composition. Tuples aren't really
an abstraction -- you describe the data structure in full in every
place you use it. This makes it hard to expand tuples past a certain
size and makes it difficult to compose with other data structures because
you cannot refer to them by name.

**Classes/Structs**. This is by far the most common representation of data
in C#. A canonical example looks something like this:

```C#
public class LoginResource
{
    public string Username { get; set; }
    public string Password { get; set; }
    public bool RememberMe { get; set; }
}
```

This feature provides names, for both the members and the data structure, it
provides easy nominal composition, and is easily composable with all other
data structures. It also provides a convenient syntax for creation by
interacting directly with the named data, e.g.

```C#
var x = new LoginResource {
    Username = "andy",
    Password = password
}
```

Unfortunately, there are still serious problems. There is no piecewise
comparer implicitly defined for C# classes, so if you want simple data
comparison, the real example looks like this:

```C#
using System;

public class LoginResource : IEquatable<LoginResource>
{
    public string Username { get; set; }
    public string Password { get; set; }
    public bool RememberMe { get; set; } = false;

    public override bool Equals(object obj)
        => obj is LoginResource resource && Equals(resource);

    public bool Equals(LoginResource other)
    {
        return other != null &&
               Username == other.Username &&
               Password == other.Password &&
               RememberMe == other.RememberMe;
    }

    public override int GetHashCode()
    {
        var hashCode = -736459255;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Username);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Password);
        hashCode = hashCode * -1521134295 + RememberMe.GetHashCode();
        return hashCode;
    }

    public override string ToString()
    {
        return $"{{{nameof(Username)} = {Username}, {nameof(Password)} = {Password}, {nameof(RememberMe)} = {RememberMe}}}";
    }

    public static bool operator ==(LoginResource resource1, LoginResource resource2)
    {
        return EqualityComparer<LoginResource>.Default.Equals(resource1, resource2);
    }

    public static bool operator !=(LoginResource resource1, LoginResource resource2)
    {
        return !(resource1 == resource2);
    }
}
```

Immutable data is also a problem. The object initializer syntax provides
a simple name-based mechanism to create a data type. With `readonly`
fields or properties, a constructor must be used instead. This creates
another set of problems:

1. A constructor must be manually defined.
1. The constructor parameters are ordered, while the fields are not.
   Consumers have now taken a dependency on the parameter ordering.
1. The constructor must be maintained with any field changes.
1. Constructor parameter names don't necessarily line up with field/property
names hence named argument passing doesn't have the same ease of use as
object initializers.

There is also no way to create a copy of a data structure with readonly
fields with only one item changed. A new type must be constructed manually.

## Proposal

To resolve many of these issues, I propose a new modifier for classes and structs: `data`.
`data` classes or structs are meant to satisfy the goals listed above by doing the
following things:

1. Automatically generating `Equals`, `GetHashCode`, `ToString`, `==`, `!=`, and `IEquatable<T>`
   based on the member data of the type.
1. Allow object initializers to also initialize readonly members.

Data classes or structs represent unordered, *named* data, like the simple
`LoginResource` class that people write today.

The LoginResource class now could be defined as

```C#
public data class LoginResource
{
    public string Username { get; }
    public string Password { get; }
    public bool RememberMe { get; } = false;
}
```

and the use would be identical:

```C#
var x = new LoginResource {
    Username = "andy",
    Password = password
};
```

Note that `RememberMe` must have an initializer to avoid a warning in the
object initializer about an unset read-only property.

However, the generated class code would look like:

```C#
public class LoginResource : IEquatable<LoginResource>
{
    public string <>Backing_Username;
    public string Username => <>Backing_Username;
    public string <>Backing_Password;
    public string Password => <>Backing_Password;
    public string <>Backing_RememberMe = false;
    public bool RememberMe => <>Backing_RememberMe;

    protected LoginResource() { }

    public static LoginResource <>Init() => new LoginResource();

    public override bool Equals(object obj)
        => obj is LoginResource resource && Equals(resource);

    public bool Equals(LoginResource other)
    {
        return other != null &&
               EqualityContractOrigin == other.EqualityContractOrigin &&
               Username == other.Username &&
               Password == other.Password &&
               RememberMe == other.RememberMe;
    }

    protected virtual Type EqualityContractOrigin => typeof(LoginResource);

    public override int GetHashCode()
    {
        unchecked
        {
            return EqualityComparer<string>.Default.GetHashCode(Username) +
                EqualityComparer<string>.Default.GetHashCode(Password) +
                RememberMe.GetHashCode();
        }
    }

    public override string ToString()
    {
        return $"{{{nameof(Username)} = {Username}, {nameof(Password)} = {Password}, {nameof(RememberMe)} = {RememberMe}}}";
    }

    public static bool operator ==(LoginResource resource1, LoginResource resource2)
    {
        return EqualityComparer<LoginResource>.Default.Equals(resource1, resource2);
    }

    public static bool operator !=(LoginResource resource1, LoginResource resource2)
    {
        return !(resource1 == resource2);
    }
}
```

The above usage would translate into:

```C#
var x = LoginResource.<>Init();
x.<>Backing_Username = "andy";
x.<>Backing_Password = password;
```

### Equality

First, the generation of equality support. Data members are only public
fields and auto-properties. This allows data classes to have private
implementation details without giving up simple equality semantics. There are
a few places this could be problematic. For instance, only auto-properties
are considered data members by default, but it's not uncommon to have some
simple validation included in a property getter that does not meaningfully
change the semantics, e.g.

```C#
{
    ...
    private int _field;
    public int Field
    {
        get
        {
            Debug.Assert(_field >= 0);
            return _field;
        }
        set { ... }
    }
}
```

To support these cases and provide an easy escape hatch, I propose a
new attribute, `DataMemberAttribute` with a boolean flag argument on the
constructor. This allows users to override the normal behavior and include
or exclude extra members in equality. The previous example would now read:

```C#
{
    ...
    private int _field;

    [DataMember(true)]
    public int Field
    {
        get
        {
            Debug.Assert(_field >= 0);
            return _field;
        }
    }
}
```

Equality itself would be defined in terms of its data members. A `data` type
is equal to another `data` type when there is an implicit conversion between
the target type and the source type and each of the corresponding members
are equal. The members are compared by `==` if it is available. Otherwise,
the method `Equals` is tried according to overload resolution rules (st. an
`Equals` method with an identity conversion to the target type is preferred
over the virtual `Equals(object)` method).

There is also one hidden data member, `protected virtual Type EqualityContractOrigin { get; }`, that is
always considered in equality. By default this member always returns the
static type of its containing type, i.e. `typeof(Containing)`. This means
that sub-classes are not, by default, considered equal to their base classes,
or vice versa. This also ensures that equality is commutative and `GetHashCode`
matches the results of `Equals`. These methods are virtual, so they can be
overridden, but then it is the user's responsibility to ensure that they
abide by the appropriate contract.

`GetHashCode` would be implemented by calling `GetHashCode` on each of
the data members.

### Readonly initialization

Support for `readonly` members in object initializers may seem like a small
feature, but it's important that making a `data` type readonly not come with
a lot of extra ceremony. The essence of a `data` type is a set of named fields
and that should stay true regardless of whether or not the fields are 
`readonly`. It may be tempting for implementation simplicity to try to use
constructors instead, but this is a design smell that conflates positional
semantics with `readonly` semantics. Requiring initialization via constructor
means that field order becomes a public API and requires careful versioning,
which is not true of mutable `data` types and is a constraint that should
be irrelevant to `readonly` semantics.

One way to remove dependence on a constructor is simply not make the members
`readonly` in metadata. The CLR treats `readonly` mostly as guidance -- it
can easily be overridden using reflection anyway. Most of the safety of
`readonly` members in C# is not provided by the runtime, but by C# safety
rules. One way we could enforce compiler rules would be to generate public
`get`-only properties and make the backing field public and mutable. Object
initializers would be able to set the properties, but user code wouldn't be
able to because the backing properties are unspeakable.

One problem with this strategy is `readonly` fields. In that case there is
no backing field to hide. There are two possible solutions. The first is
to forbid public `readonly` fields and require properties. The second is
to make all fields into properties automatically. This is strange because
we would be generating a property from a field syntax. However, it removes
what will be a meaningless restriction for the user, only mandated by
implementation difficulties. Property substitution will never be a perfect
abstraction (reflection will be able to see properties, for example) but the
solution would probably be able to match user expectations for the vast
majority of cases. The properties would also be `ref readonly` returning, so
even uses of `in` or `ref readonly` would function as expected.

If data classes contain any `readonly` members that do not have initializers,
they also do not define a default public constructor like other classes.
Instead, they define a protected constructor with no arguments, and an
unspeakable public "initialization" method. This method is called when using
an object initializer and the compiler verifies that all `readonly` members
are initialized, or an error is produced.


## Extensible data classes (data class subtyping)

Like normal C# classes, data classes are not sealed by default and can be
inherited from in sub-classes. 

In non-`data` sub-classes, if there are any readonly members without
default initialization in the base class, the subclass is required to
define a protected constructor. The constructor must assign all readonly
members of the base class before the constructor ends, or an error is
produced.

In `data` sub-classes, the requirements of the base become requirements
of the sub-class, such that initialization of the sub-class must also
initialize all of the required members of the base.


## TODO: "With"-ers