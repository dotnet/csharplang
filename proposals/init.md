Init Only Members
=====

**Wildly non-detailed sketch, reader beware**

## Syntax 

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

## Considerations

### Mod reqs
This proposal does not use modreq but instead plain old attributes. The quick
summary of why is that attributes provide the greatest flexbility for type
evolution and it's inline with the protections that `readonly` provides today.

modreqs provide limited extra value here, this feature is not needed for type
safety, and actually significantly constrains some use cases.

**Jared will add his detaile justification for not using modreq in this solution**

## Open Questions

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
