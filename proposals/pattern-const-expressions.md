# Constant `is` pattern expressions

* [x] Proposed
* [ ] Prototype: None
* [ ] Implementation: None
* [ ] Specification: Started, below

## Summary
[summary]: #summary

Consider `is` pattern expressions as constant if the LHS is constant.

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
>   - **Boolean literal patterns.**
>   - **Numeric literal patterns.**
>   - **Character literal patterns.**
>   - **String literal patterns.**
>   - **Relative numeric literal patterns.**
>   - **Relative character literal patterns.**
>   - **Null literal patterns.**
>   - **Default literal patterns.**
>   - **And, or and not patterns.**
>   - **Parenthesized patterns.**
>   - **References to constant fields or locals inside constant patterns.**

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

All the above are currently valid pattern matching expressions, that also emit warnings about their constant evaluation results, about them being always true or false.

When assigning those expressions to a constant symbol, these warnings about the constant result of the expression will **not** be reported, as the user intends to capture the constant value of the expression.

Pattern expressions containing non-constant subpatterns, like accessing properties, list patterns and var patterns, are **not** constant. In the below examples, all expressions will report compiler errors:

```csharp
const bool q = d is string { Length: 5 }; // Error: not a constant expression
const bool r = d is [.. var prefix, 'l', 'o']; // Error: not a constant expression
const bool s = d is var someString; // Error: not a constant expression
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
- [ ] Should this be extended to `switch` expressions too? (Discussion [#7489](https://github.com/dotnet/csharplang/discussions/7489))

## Design meetings
[meetings]: #design-meetings

- Approval for Any Time milestone: [Here](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-09.md#is-expression-evaluating-const-expression-should-be-considered-constant)
