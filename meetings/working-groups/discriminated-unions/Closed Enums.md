# Closed Enums

* [ ] Proposed
* [ ] Prototype: [Not Started](pr/1)
* [ ] Implementation: [Not Started](pr/1)
* [ ] Specification: [Not Started](pr/1)

## Summary

Allow an enum type to be declared closed: 

``` c#
public closed enum Color
{
    Red,
    Green,
    Blue
}
```

This prevents creation of enum values other than the specified members.

A consuming switch expression that covers all its specified members can therefore be concluded to "exhaust" the closed enum - it does not need to provide a default case to avoid warnings:

``` c#
Color color = ...;

string description = color switch
{
    Red => "red",
    Green => "green",
    Blue => "blue"
    // No warning about missing cases
};
```

## Motivation

Many enum types are not intended to take on values beyond the declared members, but the language provides no way to express that intent, let alone guard against it happening. For consumers of the type this means that no set of enum values short of the full range of the underlying integral type will be considered to "exhaust" the enum type, and a switch expression needs to include a catch-all case to avoid warnings.

Closed enums provide a way to indicate that the set of enum mebers is complete, and allow consuming code to rely on that for exhaustiveness in switch expressions.

## Detailed design

### Syntax

Allow `closed` as a modifier on enum types. 

### Enforcement

The following restrictions apply to closed enum types compared to other enum types:

- A closed enum must declare a member corresponding to the integral value `0`.
- Explicit enumeration conversions are not allowed _to_ a closed enum type, except from a constant whose value corresponds to a declared member.
- Operators that return a closed enum type are only allowed over constant operands, and it is an error for them to produce a value that is not a declared member.

``` c#
Color c = 0;         // Ok, all closed enums hve a member corresponding to the value `0`
c = (Color)1;        // Ok, the constant `1` corresponds to the member `Green`
c = (Color)10;       // Error, there is no member corresponding to the constant `10`
c = (Color)myInt;    // Error, `myInt` is not constant
_ = c == Color.Blue; // Ok, `==` does not return a closed enum
_ = Color.Red + 1;   // Ok, operands are constant and result corresponds to the member `Green`
_ = c + 1;           // Error, operand `c` is not constant
```

### Exhaustiveness in switches

A `switch` expression that handles all of the members of a closed enum type will be considered to have exhausted that enum type. That means that some non-exhaustiveness warnings will no longer be given:

``` c#
Color c = ...;
_ = c switch
{
    Red => ...,
    Green => ...,
    Blue => ...
    // No warning about non-exhaustive switch
};
```

On the other hand this also means that it can be an error for the closed enum type to occur as a case after all its members:

``` c#
Color c = ...;
_ = c switch
{
    Red => ...,
    Green => ...,
    Blue => ...,
    Color => ... // Error, case cannot be reached
};
```

### Lowering

Closed enums are generated with a `Closed` attribute, to allow them to be recognized by a consuming compiler.

## Drawbacks

It can be a breaking change to add a `closed` modifier to an existing enum, or to add an additional member to an existing closed enum. Before publishing a closed enum type, the author needs to consider the long term contract it implies with its consumers.

## Alternatives

- Instead of a new `closed` modifier, a closed enum could be designated with a `[Closed]` attribute.

## Open questions

- Can closed enums be generated into IL in a way that prevents other languages and compilers from allowing unintended operations on them even if they do not implement the feature?
- Should closed `[Flags]` enums be supported? If so, they should allow the binary logical (bitwise) operators `&`, `|` and `^`.