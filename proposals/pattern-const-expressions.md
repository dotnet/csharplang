# Constant `is` pattern expressions

* [x] Proposed
* [ ] Prototype: None
* [ ] Implementation: None
* [ ] Specification: Started, below

## Summary
[summary]: #summary

Consider `is` pattern expressions as constant if both the RHS and the LHS are constant values, expressions, or patterns.

## Motivation
[motivation]: #motivation

Certain pattern expressions that can be converted into simpler boolean expressions using equality and comparison operators are never considered constant expressions, even when all parts of the expression are considered constant and can be evaluated during compilation. However, the lowered versions of those expressions (that only involve equality and comparison operators) are constant.

The above restriction prevents the ability to utilize `is` expressions for operations like comparing against a range (e.g. `x is >= 'a' and 'z'`), or checking against distinct values (e.g. `x is Values.A or Values.B or Values.C`), and instead relying on using traditional comparison operators like so for the last example: `x == Values.A || x == Values.B || x == Values.C`.

## Detailed design
[design]: #detailed-design

A pattern expression is constant if all its contained subpatterns are all also constant. The following subpatterns are constant:
- Boolean literal (`true` or `false`)
- Numeric literal (e.g. `0`, `1f`, `3m`, etc.)
- Character literal (e.g. `'c'`, `'\0'`, etc.)
- String literal (e.g. `"Hello world."`, `""`, etc.)
- Relative numeric literal (e.g. `> 3`, `< 0.3m`, etc.)
- Relative character literal (e.g. `> 'a'`, `<= 'z'`, etc.)
- Null literal (`null`)
- Default literal (`default(T)`), but not `default` without a type
- And, or and not patterns (e.g. the *and* in  `>= 'a' and <= 'z'`)
- Parenthesized patterns (e.g. the parentheses in `is (not (X or Y))`)
- Constant field (including enum fields) or local, e.g.
  - `x is y` when y is a const field or local, or
  - `x is SomeEnum.None` when `None` is an enum field

The allowed subpatterns as shown in the list above are called "constant subpatterns", as they will be eligible for compile-time evaluation.

If the `is` expression consists of a constant expression on the LHS, and is compared against a constant pattern on the right, the entire `is` expression is a constant expression.

### Spec
[spec]: #spec

The [§12.23 section](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1223-constant-expressions) in the spec needs to be updated accordingly, in the following segments:

> Only the following constructs are permitted in constant expressions:
> - Literals (including the null literal).
> - References to const members of class and struct types.
> - References to members of enumeration types.
> - References to local constants.
> - Parenthesized subexpressions, which are themselves constant expressions.
> - Cast expressions.
> - checked and unchecked expressions.
> - nameof expressions.
> - The predefined +, –, !, and ~ unary operators.
> - The predefined +, –, *, /, %, <<, >>, &, |, ^, &&, ||, ==, !=, <, >, <=, and >= binary operators.
> - The ?: conditional operator.
> - sizeof expressions, provided the unmanaged-type is one of the types specified in §23.6.9 for which sizeof returns a constant value.
> - Default value expressions, provided the type is one of the types listed above.
> - **`is` expressions with only the following subpatterns:**
>   - **Boolean patterns.**
>   - **Numeric patterns.**
>   - **Character patterns.**
>   - **String patterns.**
>   - **Relative numeric patterns.**
>   - **Relative character patterns.**
>   - **Null patterns.**
>   - **Default patterns.**
>   - **And, or and not patterns.**
>   - **Parenthesized patterns.**
>   - **References to constant fields or locals inside constant patterns.**

We additionally define the concept of constant contexts, which are contexts where a constant expression is required, as indicated in the [§12.23 section](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1223-constant-expressions), specifically:

> Constant expressions are required in the contexts listed below and this is indicated in the grammar by using *constant_expression*. In these contexts, a compile-time error occurs if an expression cannot be fully evaluated at compile-time.
> 
> - Constant declarations ([§15.4](classes.md#154-constants))
> - Enumeration member declarations ([§19.4](enums.md#194-enum-members))
> - Default arguments of formal parameter lists ([§15.6.2](classes.md#1562-method-parameters))
> - `case` labels of a `switch` statement ([§13.8.3](statements.md#1383-the-switch-statement)).
> - `goto case` statements ([§13.10.4](statements.md#13104-the-goto-statement))
> - Dimension lengths in an array creation expression ([§12.8.16.5](expressions.md#128165-array-creation-expressions)) that includes an initializer.
> - Attributes ([§22](attributes.md#22-attributes))
> - In a *constant_pattern* ([§11.2.3](patterns.md#1123-constant-pattern))

### Grammar
[grammar]: #grammar
The grammar remains untouched, as nothing changes syntactically for this feature.

### Semantics
[semantics]: #semantics
A pattern expression only consisting of constant subpatterns with a constant LHS may be evaluated at compile time currently, but it is not considered as constant. With this change, the following adjustments enable constant pattern expressions to be used in constant contexts, like assigning a constant field or local, a default parameter, the condition in a constant conditional expression, etc.

### Examples
[examples]: #examples
The below example shows pattern expressions being assigned to constant fields:
```csharp
public const int A = 4;
public const bool B = A is 4;
public const bool C = A is not 3 and <= 4 or 6;

// DeploymentEnvironment is an enum type
public const DeploymentEnvironment Environment = DeploymentEnvironment.Production;
public const bool D = Environment
    is DeploymentEnvironment.Production
    or DeploymentEnvironment.Test;
```

In the expression assigned to `C`, since `A` is constant, and all the subpatterns on the pattern are constant, the entire expression only consists of constants. This result will be evaluated during compile-time and then be assigned to the constant field.

Likewise, `Environment` is constant as an enum value, and so are the other subpatterns of the expression assigned to `D`.

Another example, using `null` and default value checks:
```csharp
const int a = 4;
const bool b = false;
const long c = 4;
const string d = "hello";

const bool x = a is default(int); // always false
const bool y = b is default(bool); // always true
const bool z = d is not null; // always true
const bool p = b is null; // always false
```

### Diagnostics
[diagnostics]: #diagnostics

All the above are currently valid pattern matching expressions, that also emit warnings about their constant evaluation results, being always true or false.

When assigning those expressions in a constant context, these warnings about the constant result of the expression will **not** be reported, as the user intends to capture the constant value of the expression.

The contexts that will accept constant `is` pattern expressions are constant contexts that accept `bool` values, namely:
- Constant declarations
- Default arguments of formal parameter lists
- Attribute arguments
- `case` labels of a `switch` statement
- `goto case` statements
- Switch expression arms

Examples for the above:
```csharp
const int a = 4;
const bool b = false;
const int c = 4;
const string d = "hello";
const Sign e = Sign.Negative;

// constant declaration
const bool x = a is default(int); // always false, no warning/error

// attribute argument
[assembly: Something(a is 3)] // always false, no warning/error

// default argument of formal parameter list
// always true, no warning/error
int Negate(int value, bool negate = e is Sign.Negative) { }

// switch statement case label
// + goto case statement
switch (b)
{
    // always false, no warning/error
    case a is not c:
        break;

    default:
        // always false, no warning/error
        goto case a is not c;
}

// switch expression arm
var p = b switch
{
    // 'a is c' is always true, no warning/error about the expression
    a is c => 1,
    _ => 2,
};
```

**NOTE**: we do not introduce any breaking changes in the reported diagnostics. Currently, all the above cases are illegal reporting "CS0150: A constant value is expected". A warning about the values always evaluating to either true or false is also reported alongside the error. We remove the warnings from those places where `is` expressions are currently not permitted to be used due to the error.

### Other patterns
[other-patterns]: #other-patterns

Pattern expressions containing non-constant subpatterns, like accessing properties, list patterns and var patterns, are **not** constant. In the below examples, all expressions will report compiler errors:

```csharp
const bool q = d is string { Length: 5 }; // Error: not a constant expression
const bool r = d is [.. var prefix, 'l', 'o']; // Error: not a constant expression
const bool s = d is var someString; // Error: not a constant expression
```

### Switch expressions
[switch-expressions]: #switch-expressions

When evaluating a constant value on a switch expression, we adjust the reported diagnostics based on the matching arms:
- If any subpattern is matched, we keep reporting the warning about the missing default arm.
- If no subpattern is matched, we report an **error** instead of a warning about the unmatched subpattern, asking the user to either handle the specific value, or the default case.

For example:
```csharp
const int a = 1;
const int b = 2;
const int c = 3;

// we get a warning about the missing default arm,
// the expression always returns the value of b
int x = a switch
{
    a => b,
    b => a,
    c => a + b,
};

// we get no warning about the missing default arm, since we are in constant context
// the expression results in xc being assigned the value of b
const int xc = a switch
{
    a => b,
    b => a,
    c => a + b,
};

// we get a warning about the missing default arm,
// as no subpattern matches the constant value,
// and the program will throw at runtime
int y = a switch
{
    b => a,
    c => a + b,
};

// we get an error about the missing default arm,
// as no subpattern matches the constant value
const int yc = a switch
{
    b => a,
    c => a + b,
};
```

## Drawbacks
[drawbacks]: #drawbacks

None.

## Alternatives
[alternatives]: #alternatives

Currently, equality and comparison operators can be used to compare against other literals, including nullability of the objects (e.g. `x != null`, or `y == 4 || y < 3`).

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Requires LDM review
- [ ] Should we also consider `switch` expressions evaluating and returning constant expressions as constant too? (Discussion [#7489](https://github.com/dotnet/csharplang/discussions/7489))

## Design meetings
[meetings]: #design-meetings

- Approval for Any Time milestone: [Here](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-09.md#is-expression-evaluating-const-expression-should-be-considered-constant)
- November 27th, 2023 [Discussion](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-11-27.md#making-patterns-constant-expressions)
