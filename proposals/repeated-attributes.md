# Repeated Attributes in Partial Members

## Summary

Allow each declaration of a partial member to independently apply an attribute not marked with `[AttributeUsage(AllowMultiple = true)]`, as long as the attribute arguments are identical in all applications.

## Motivation

When considering what attributes are present on a 'partial' method, the language unions together all the attributes in all corresponding positions on both declarations. For example, the method `M` below has attributes `A` and `B`.

```cs
[A]
partial void M();

[B]
partial void M() { }
```

This means that attributes which are not marked `[AttributeUsage(AllowMultiple = true)]` cannot be present across both parts:

```cs
[A]
partial void M();

[A] // error: duplicate attribute!
partial void M() { }
```

This presents a usability/readability issue, because some attributes are designed to inform the user and/or maintainer of the method of what pre/postconditions or invariants the method requires. For example:

```cs
public partial bool TryGetValue([NotNullWhen(true)] out object? value);
public partial bool TryGetValue(out object? value) { ... }
```

A partial member typically facilitates the relationship between a code generator and an end user--each party provides one of the declarations of the partial member in order for a code generator to provide functionality to the user, or for the user to access an extension point in generated code. In the situation where only one declaration is allowed to have these single-application attributes, the generator and the user can't effectively communicate their requirements to each other. If a generator produces a defining declaration with a `NotNullWhen` attribute, for instance, the user cannot write an implementing declaration with that same attribute, even though the postcondition is applicable to the implementation, and checked by the compiler. This creates confusion for users when tracking down the root causes of warnings or when trying to understand the behaviors of a method.

## Solution

Allow a non-AllowMultiple attribute to be used once on each symbol (member, return, parameter, etc.) in each partial declaration, as long as the attribute arguments are identical. Since attribute arguments are all constants, the compiler can verify this. When attributes are unioned across declarations, each non-AllowMultiple attribute will be de-duplicated and only one instance of the attribute will be emitted.

```cs
public partial bool TryGetValue([NotNullWhen(true)] out object? value);
public partial bool TryGetValue([NotNullWhen(true)] out object? value) { ... } // ok

// equivalent to:
public bool TryGetValue([NotNullWhen(true)] out object value) { ... }

// error when attribute arguments do not match
public partial bool TryGetValue([NotNullWhen(true)] out object? value);
public partial bool TryGetValue([NotNullWhen(false)] out object? value) { ... } // error
```

### Open questions

1. Should such repetition of attributes be permitted on 'partial' type declarations or only on non-type members (e.g. methods)?
2. Should attributes which *do* allow multiple usages on a symbol be permitted to "opt in" to de-duplication of equivalent usages of an attribute?

### Design meetings
#### [6th July 2020](../meetings/2020/LDM-2020-07-06.md#repeated-attributes-on-partial-members)
The proposal is accepted.
  - Repetition of non-AllowMultiple attributes will be permitted across partial type declarations (open question 1).
  - Repeated application of AllowMultiple attributes will not change in behavior, and an "opt in" mechanism for de-duplication may be considered in a future proposal (open question 2).
