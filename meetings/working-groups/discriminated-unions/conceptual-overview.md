# Conceptual overview

> The text that follows is the working draft for how we'll introduce this concept to customers. It's meant to address two distinct challenges:
>
> - Many of the scenarios for unions can also be handled by inheritance. That prompts the question on the value of adding unions to C#.
>
> - Unions have different meanings in different computer languages. There are tagged unions, type unions, C-style (unsafe) unions. Which do we mean?
>
> The language is intended to be less formal than the specification and serve as an introduction for customers.

*Discriminated unions* provide a new way to express the concept that *an expression may be one of many known types*.

C# already provides syntax for special cases of discriminated unions. Nullable types express one example: A `int?` is either an `int`, or nothing. A `string?` is either a `string` value or nothing. Another example is `enum` types. An `enum` type represents a closed set of values. The expression is limited to one of those values.

## Closed set of possibilities

The `enum` example highlights one limitation in C# now: An `enum` is represented by an underlying integral type. The compiler issues a warning if an invalid integral value is assigned to a variable of an `enum` type. But, that's the limit of the enforcement. Therefore, when you use an `enum` type in a `switch` expression, you must also check for invalid values using a discard (`_`) pattern.

C# developers often use *inheritance* to express that an expression is *one of many types*. You declare that an expression is of a base class, and it could be any class derived from that type. Inheritance differs from union types in two ways. Most importantly, a union represents one of a *known* set of types. An inheritance hierarchy likely includes derived classes beyond the known set of derived types. Secondly, a union doesn't require an inheritance relationship. A union can represent *one of many known `struct` types*, or even a union of some `struct` types and some `class` types. Inheritance and unions have some overlap in expressiveness, but both have unique features as well.

## On to examples

Add examples based on our existing design.
