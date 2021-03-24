Init Only Setters
=====

## Summary
This proposal adds the concept of init only properties and indexers to C#. 
These properties and indexers can be set at the point of object creation 
but become effectively `get` only once object creation has completed.
This allows for a much more flexible immutable model in C#. 

## Motivation
The underlying mechanisms for building immutable data in C# haven't changed
since 1.0. They remain:

1. Declaring fields as `readonly`.
1. Declaring properties that contain only a `get` accessor.

These mechanisms are effective at allowing the construction of immutable data
but they do so by adding cost to the boilerplate code of types and opting
such types out of features like object and collection initializers. This means
developers must choose between ease of use and immutability.

A simple immutable object like `Point` requires twice as much boiler plate code
to support construction as it does to declare the type. The bigger the type 
the bigger the cost of this boiler plate:

```cs
struct Point
{
    public int X { get; }
    public int Y { get; }

    public Point(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
}
```

The `init` accessor makes immutable objects more flexible by allowing the
caller to mutate the members during the act of construction. That means the
object's immutable properties can participate in object initializers and thus
removes the need for all constructor boilerplate in the type. The `Point`
type is now simply:

```cs
struct Point
{
    public int X { get; init; }
    public int Y { get; init; }
}
```

The consumer can then use object initializers to create the object

```cs
var p = new Point() { X = 42, Y = 13 };
```

## Detailed Design

### init accessors
An init only property (or indexer) is declared by using the `init` accessor in place of the 
`set` accessor:

```cs
class Student
{
    public string FirstName { get; init; }
    public string LastName { get; init; }
}
```

An instance property containing an `init` accessor is considered settable in
the following circumstances, except when in a local function or lambda:

- During an object initializer
- During a `with` expression initializer
- Inside an instance constructor of the containing or derived type, on `this` or `base`
- Inside the `init` accessor of any property, on `this` or `base`
- Inside attribute usages with named parameters

The times above in which the `init` accessors are settable are collectively
referred to in this document as the construction phase of the object.

This means the `Student` class can be used in the following ways:

```cs
var s = new Student()
{
    FirstName = "Jared",
    LastName = "Parosns",
};
s.LastName = "Parsons"; // Error: LastName is not settable
```

The rules around when `init` accessors are settable extend across type
hierarchies. If the member is accessible and the object is known to be in the
construction phase then the member is settable. That specifically allows for
the following:

```cs
class Base
{
    public bool Value { get; init; }
}

class Derived : Base
{
    Derived()
    {
        // Not allowed with get only properties but allowed with init
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

At the point an `init` accessor is invoked, the instance is known to be 
in the open construction phase. Hence an `init` accessor is allowed to take 
the following actions in addition to what a normal `set` accessor can do:

1. Call other `init` accessors available through `this` or `base`
1. Assign `readonly` fields declared on the same type through `this`

```cs
class Complex
{
    readonly int Field1;
    int Field2;
    int Prop1 { get; init ; }
    int Prop2
    {
        get => 42;
        init
        {
            Field1 = 13; // okay
            Field2 = 13; // okay
            Prop1 = 13; // okay
        }
    }
}
```

The ability to assign `readonly` fields from an `init` accessor is limited to 
those fields declared on the same type as the accessor. It cannot be used to 
assign `readonly` fields in a base type. This rule ensures that type authors
remain in control over the mutability behavior of their type. Developers who do
not wish to utilize `init` cannot be impacted from other types choosing to
do so:

```cs
class Base
{
    internal readonly int Field;
    internal int Property
    {
        get => Field;
        init => Field = value; // Okay
    }

    internal int OtherProperty { get; init; }
}

class Derived : Base
{
    internal readonly int DerivedField;
    internal int DerivedProperty
    {
        get => DerivedField;
        init
        {
            DerivedField = 42;  // Okay
            Property = 0;       // Okay
            Field = 13;         // Error Field is readonly
        }
    }

    public Derived()
    {
        Property = 42;  // Okay 
        Field = 13;     // Error Field is readonly
    }
}
```

When `init` is used in a virtual property then all the overrides must also
be marked as `init`. Likewise it is not possible to override a simple 
`set` with `init`.

```cs
class Base
{
    public virtual int Property { get; init; }
}

class C1 : Base
{
    public override int Property { get; init; }
}

class C2 : Base
{
    // Error: Property must have init to override Base.Property
    public override int Property { get; set; }
}
```

An `interface` declaration can also participate in `init` style initialization 
via the following pattern:

```cs
interface IPerson
{
    string Name { get; init; }
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
- The `init` accessor can only be used on instance properties
- A property cannot contain both an `init` and `set` accessor
- All overrides of a property must have `init` if the base had `init`. This rule
also applies to interface implementation.

### Readonly structs

`init` accessors (both auto-implemented accessors and manually-implemented
accessors) are permitted on properties of `readonly struct`s, as well as
`readonly` properties. `init` accessors are not permitted to be marked
`readonly` themselves, in both `readonly` and non-`readonly` `struct` types.

```cs
readonly struct ReadonlyStruct1
{
    public int Prop1 { get; init; } // Allowed
}

struct ReadonlyStruct2
{
    public readonly int Prop2 { get; init; } // Allowed

    public int Prop3 { get; readonly init; } // Error
}
```

### Metadata encoding 
Property `init` accessors will be emitted as a standard `set` accessor with
the return type marked with a modreq of `IsExternalInit`. This is a new type
which will have the following definition:

```cs
namespace System.Runtime.CompilerServices
{
    public sealed class IsExternalInit
    {
    }
}
```

The compiler will match the type by full name. There is no requirement
that it appear in the core library. If there are multiple types by this name
then the compiler will tie break in the following order:

1. The one defined in the project being compiled
1. The one defined in corelib

If neither of these exist then a type ambiguity error will be issued.

The design for `IsExternalInit` is futher covered in [this issue](https://github.com/dotnet/runtime/issues/34978)

## Questions

### Breaking changes
One of the main pivot points in how this feature is encoded will come down to
the following question: 

> Is it a binary breaking change to replace `init` with `set`?

Replacing `init` with `set` and thus making a property fully writable is never
a source breaking change on a non-virtual property. It simply expands the set
of scenarios where the property can be written. The only behavior in question is
whether or not this remains a binary breaking change.

If we want to make the change of `init` to `set` a source and binary compatible
change then it will force our hand on the modreq vs. attributes decision
below because it will rule out modreqs as a solution. If on the other hand
this is seen as a non-interesting then this will make the modreq vs. attribute
decision less impactful.

**Resolution**
This scenario is not seen as compelling by LDM.

### Modreqs vs. attributes
The emit strategy for `init` property accessors must choose between using 
attributes or modreqs when emitting during metadata. These have different 
trade offs that need to be considered.

Annotating a property set accessor with a modreq declaration means CLI compliant
compilers will ignore the accessor unless it understands the modreq. That means
only compilers aware of `init` will read the member. Compilers unaware of 
`init` will ignore the `set` accessor and hence will not accidentally treat the
property as read / write. 

The downside of modreq is `init` becomes a part of the binary signature of 
the `set` accessor. Adding or removing `init` will break binary compatbility 
of the application.

Using attributes to annotate the `set` accessor means that only compilers which
understand the attribute will know to limit access to it. A compiler unaware 
of `init` will see it as a simple read / write property and allow access.

This would seemingly mean this decision is a choice between extra safety at 
the expense of binary compatibility. Digging in a bit the extra safety is not
exactly what it seems. It will not protect against the following circumstances:

1. Reflection over `public` members
1. The use of `dynamic` 
1. Compilers that don't recognize modreqs

It should also be considered that, when we complete the IL verification rules 
for .NET 5, `init` will be one of those rules. That means extra enforcement 
will be gained from simply verifying compilers emitting verifiable IL.

The primary languages for .NET (C#, F# and VB) will all be updated to 
recognize these `init` accessors. Hence the only realistic scenario here is 
when a C# 9 compiler emits `init` properties and they are seen by an older 
toolset such as C# 8, VB 15, etc ... C# 8. That is the trade off to consider
and weigh against binary compatibility.

**Note**
This discussion primarily applies to members only, not to fields. While `init`
fields were rejected by LDM they are still interesting to consider for the 
modreq vs. attribute discussion. The `init` feature for fields is a relaxation
of the existing restriction of `readonly`. That means if we emit the fields as
`readonly` + an attribute there is no risk of older compilers mis-using the 
field because they would already recognize `readonly`. Hence using a modreq here
doesn't add any extra protection.

**Resolution**
The feature will use a modreq to encode the property `init` setter. The
compelling factors were (in no particular order):

* Desire to discourage older compilers from violating `init` semantics
* Desire to make adding or removing `init` in a `virtual` declaration or 
`interface` both a source and binary breaking change.

Given there was also no significant support for removing `init` to be a 
binary compatible change it made the choice of using modreq straight forward.

### init vs. initonly
There were three syntax forms which got significant consideration during our
LDM meeting:

```cs
// 1. Use init 
int Option1 { get; init; }
// 2. Use init set
int Option2 { get; init set; }
// 3. Use initonly
int Option3 { get; initonly; }
```

**Resolution**
There was no syntax which was overwhelmingly favored in LDM.

One point which got significant attention was how the choice of syntax would
impact our ability to do `init` members as a general feature in the future.
Choosing option 1 would mean that it would be difficult to define a property
which had an `init` style `get` method in the future. Eventually it was decided
that if we decided to go forward with general `init` members in future, we could
allow `init` to be a modifier in the property accessor list as well as a short
hand for `init set`. Essentially the following two declarations would be
identical.

```cs
int Property1 { get; init; }
int Property1 { get; init set; }
```

The decision was made to move forward with `init` as a standalone accessor in
the property accessor list.

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

## Allow init as a field modifier
In the same way `init` can serve as a property accessor it could also serve as
a designation on fields to give them similar behaviors as `init` properties.
That would allow for the field to be assigned before construction was complete
by the type, derived types, or object initializers.

```cs
class Student
{
    public init string FirstName;
    public init string LastName;
}

var s = new Student()
{
    FirstName = "Jarde",
    LastName = "Parsons",
}

s.FirstName = "Jared"; // Error FirstName is readonly
```

In metadata these fields would be marked in the same way as `readonly` fields 
but with an additional attribute or modreq to indicate they are `init` style
fields. 

**Resolution**
LDM agrees this proposal is sound but overall the scenario felt disjoint from 
properties. The decision was to proceed only with `init` properties for now. 
This has a suitable level of flexibility as an `init` property can mutate a 
`readonly` field on the declaring type of the property. This will be
reconsidered if there is significant customer feedback that justifies the 
scenario.

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
`init struct` feature would only apply to properties. This actually ends up making
it confusing for users. 

Given that `init` is only valid on certain aspects of a type, we rejected the 
idea of having it as a type modifier.

## Considerations

### Compatibility
The `init` feature is designed to be compatible with existing `get` only 
properties. Specifically it is meant to be a completely additive change for 
a property which is `get` only today but desires more flexbile object creation
semantics.

For example consider the following type:

```cs
class Name
{
    public string First { get; }
    public string Last { get; }

    public Name(string first, string last)
    {
        First = first;
        Last = last;
    }
}
```

Adding `init` to these properties is a non-breaking change:

```cs
class Name
{
    public string First { get; init; }
    public string Last { get; init; }

    public Name(string first, string last)
    {
        First = first;
        Last = last;
    }
}
```

### IL verification
When .NET Core decides to re-implement IL verification, the rules will need to be 
adjusted to account for `init` members. This will need to be included in the 
rule changes for non-mutating acess to `readonly` data.

The IL verification rules will need to be broken into two parts: 

1. Allowing `init` members to set a `readonly` field.
1. Determining when an `init` member can be legally called.

The first is a simple adjustment to the existing rules. The IL verifier can 
be taught to recognize `init` members and from there it just needs to consider
a `readonly` field to be settable on `this` in such a member.

The second rule is more complicated. In the simple case of object initializers
the rule is straight forward. It should be legal to call `init` members when 
the result of a `new` expression is still on the stack. That is until the 
value has been stored in a local, array element or field or passed as an
argument to another method it will still be legal to call `init` members. This
ensures that once the result of the `new` expression is published to a named
identifier (other than `this`) then it will no longer be legal to call `init`
members. 

The more complicated case though is when we mix `init` members, object
initializers and `await`. That can cause the newly created object to be
temporarily hoisted into a state machine and hence put into a field.

```cs
var student = new Student() 
{
    Name = await SomeMethod()
};
```

Here the result of `new Student()` will be hoised into a state machine as a 
field before the set of `Name` occurs. The compiler will need to mark such
hoisted fields in a way that the IL verifier understands they're not user 
accessible and hence doesn't violate the intended semantics of `init`.

### init members
The `init` modifier could be extended to apply to all instance members. This 
would generalize the concept of `init` during object construction and allow
types to declare helper methods that could partipate in the construction 
process to initialize `init` fields and properties.

Such members would have all the restricions that an `init` accessor does
in this design. The need is questionable though and this can be safely added
in a future version of the language in a compatible manner.

### Generate three accessors
One potential implementation of `init` properties is to make `init` completely
separate from `set`. That means that a property can potentially have three 
different accessors: `get`, `set`, and `init`.

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
