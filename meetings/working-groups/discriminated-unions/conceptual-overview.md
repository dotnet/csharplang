# Conceptual overview

> The text that follows is the working draft for how we'll introduce this concept to customers. It's meant to address two distinct challenges:
>
> - Many of the scenarios for unions can also be handled by inheritance. That prompts the question on the value of adding unions to C#.
>
> - Unions have different meanings in different computer languages. There are tagged unions, type unions, C-style (unsafe) unions. Which do we mean?
>
> The language is intended to be less formal than the specification and serve as an introduction for customers.

*Discriminated unions* provide a new way to express the concept that *an expression may be one of a fixed set of types or values*.

C# already provides syntax for special cases of discriminated unions. Nullable types express one example: A `int?` is either an `int`, or `null`. A `string?` is either a `string` value or `null`. Another example is `enum` types. An `enum` type represents a closed set of values. The expression is limited to one of those values.

## Closed set of possibilities

The `enum` example highlights one limitation in C# now: An `enum` is represented by an underlying integral type. The compiler issues a warning if an invalid integral value is assigned to a variable of an `enum` type. But, that's the limit of the enforcement. Therefore, when you use an `enum` type in a `switch` expression, you must also check for invalid values using a discard (`_`) pattern.

C# developers often use *inheritance* to express that an expression is *one of many types*. You declare that an expression is of a base class, and it could be any class derived from that type. Inheritance differs from union types in two ways. Most importantly, a union represents one of a *known* set of types. An inheritance hierarchy likely includes derived classes beyond the known set of derived types. Secondly, a union doesn't require an inheritance relationship. A union can represent *one of many known `struct` types*, or even a union of some `struct` types and some `class` types. Inheritance and unions have some overlap in expressiveness, but both have unique features as well.

Union types may optimize memory storage based on knowledge of the closed set of types allowed in that union. 

## Example scenarios

We've been working through three example scenarios where we expect developers to use union types:

- Option types
- Result types
- State machines

### Option - Value or "nothing"

Honest question: How much do we expect this to be used instead of nullable types?

### Result - Expression or error

A *Result* type is a union that contains one of two types: the result of some operation, or an error type that provides detailed information about the failure. The code could look something like the following:

```csharp
var result = SomeOperation();

result => switch
{
   ValueType v => ...,
   ErrorType e => ...,
};
```

The result type approach will be preferred in scenarios where error results aren't "exceptional*, where throwing an exception is the standard way to report failures. First, this approach is more performant than using exceptions for expected failures. Throwing and, optionally, catching exceptions involves unwinding the stack, finding the correct handlers, and running any `finally` clauses. Returning a result type follows the normal control flow.

Second, both the result value and the error object can be sophisticated types. In particular, the `Error` object type may include information on what failed. It may include interim results that can be used to save steps when restarting the algorithm. It may include any updates that need to be reverted in case of failure.

### Finite state machines

A union can model a sophisticated finite state machine. At each state, a different type can represent the properties of that state. Each input moves the state machine to a new state. The properties for that new state may be different values, or even represented by different types.
