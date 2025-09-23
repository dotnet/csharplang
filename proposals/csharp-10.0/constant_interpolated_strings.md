# Constant Interpolated Strings

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Champion issue: <https://github.com/dotnet/csharplang/issues/2951>

## Summary
[summary]: #summary

Enables constants to be generated from interpolated strings of type string constant.

## Motivation
[motivation]: #motivation

The following code is already legal:
```
public class C
{
    const string S1 = "Hello world";
    const string S2 = "Hello" + " " + "World";
    const string S3 = S1 + " Kevin, welcome to the team!";
}
```
However, there have been many community requests to make the following also legal:
```
public class C
{
    const string S1 = $"Hello world";
    const string S2 = $"Hello{" "}World";
    const string S3 = $"{S1} Kevin, welcome to the team!";
}
```
This proposal represents the next logical step for constant string generation, where existing string syntax that works in other situations is made to work for constants.

## Detailed design
[design]: #detailed-design

The following represent the updated specifications for constant expressions under this new proposal. Current specifications from which this was directly based on can be found in [§12.23](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1223-constant-expressions).

### Constant Expressions

A *constant_expression* is an expression that can be fully evaluated at compile-time.

```antlr
constant_expression
    : expression
    ;
```

A constant expression must be the `null` literal or a value with one of  the following types: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, `double`, `decimal`, `bool`, `object`, `string`, or any enumeration type. Only the following constructs are permitted in constant expressions:

*  Literals (including the `null` literal).
*  References to `const` members of class and struct types.
*  References to members of enumeration types.
*  References to `const` parameters or local variables
*  Parenthesized sub-expressions, which are themselves constant expressions.
*  Cast expressions, provided the target type is one of the types listed above.
*  `checked` and `unchecked` expressions
*  Default value expressions
*  Nameof expressions
*  The predefined `+`, `-`, `!`, and `~` unary operators.
*  The predefined `+`, `-`, `*`, `/`, `%`, `<<`, `>>`, `&`, `|`, `^`, `&&`, `||`, `==`, `!=`, `<`, `>`, `<=`, and `>=` binary operators, provided each operand is of a type listed above.
*  The `?:` conditional operator.
*  *Interpolated strings `${}`, provided that all components are constant expressions of type `string` and all interpolated components lack alignment and format specifiers.*

The following conversions are permitted in constant expressions:

*  Identity conversions
*  Numeric conversions
*  Enumeration conversions
*  Constant expression conversions
*  Implicit and explicit reference conversions, provided that the source of the conversions is a constant expression that evaluates to the null value.

Other conversions including boxing, unboxing and implicit reference conversions of non-null values are not permitted in constant expressions. For example:
```csharp
class C 
{
    const object i = 5;         // error: boxing conversion not permitted
    const object str = "hello"; // error: implicit reference conversion
}
```
the initialization of i is an error because a boxing conversion is required. The initialization of str is an error because an implicit reference conversion from a non-null value is required.

Whenever an expression fulfills the requirements listed above, the expression is evaluated at compile-time. This is true even if the expression is a sub-expression of a larger expression that contains non-constant constructs.

The compile-time evaluation of constant expressions uses the same rules as run-time evaluation of non-constant expressions, except that where run-time evaluation would have thrown an exception, compile-time evaluation causes a compile-time error to occur.

Unless a constant expression is explicitly placed in an `unchecked` context, overflows that occur in integral-type arithmetic operations and conversions during the compile-time evaluation of the expression always cause compile-time errors ([§12.23](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1223-constant-expressions)).

Constant expressions occur in the contexts listed below. In these contexts, a compile-time error occurs if an expression cannot be fully evaluated at compile-time.

*  Constant declarations ([§15.4](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#154-constants)).
*  Enumeration member declarations ([§19.4](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/enums.md#194-enum-members)).
*  Default arguments of formal parameter lists ([§15.6.2](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#1562-method-parameters))
*  `case` labels of a `switch` statement ([§13.8.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md#1383-the-switch-statement)).
*  `goto case` statements ([§13.10.4](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md#13104-the-goto-statement)).
*  Dimension lengths in an array creation expression ([§12.8.17.5](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#128175-array-creation-expressions)) that includes an initializer.
*  Attributes ([§22](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/attributes.md#22-attributes)).

An implicit constant expression conversion ([§10.2.11](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#10211-implicit-constant-expression-conversions)) permits a constant expression of type `int` to be converted to `sbyte`, `byte`, `short`, `ushort`, `uint`, or `ulong`, provided the value of the constant expression is within the range of the destination type.

## Drawbacks
[drawbacks]: #drawbacks

This proposal adds additional complexity to the compiler in exchange for broader applicability of interpolated strings. As these strings are fully evaluated at compile time, the valuable automatic formatting features of interpolated strings are less neccesary. Most use cases can be largely replicated through the alternatives below.

## Alternatives
[alternatives]: #alternatives

The current `+` operator for string concatenation can combine strings in a similar manner to the current proposal.

## Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still undecided?

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.


