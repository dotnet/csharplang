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

Certain pattern expressions that can be converted into simpler boolean expressions using equality and comparison operators are never considered constant expressions, even when all parts of the expression are considered constant and can be evaluated during compilation. However, the lowered versions of those expressions (that only involve equality and comparison operators) are always considered constant. This prohibits the ability to utilize `is` expressions for operations like comparing against a range (e.g. `x is >= 'a' and 'z'`), or checking against distinct values (e.g. `x is Values.A or Values.B or Values.C`).

## Detailed design
[design]: #detailed-design

### Spec
[spec]: #spec

The [section](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1223-constant-expressions) in the spec needs to be updated accordingly, in the following segments:

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
> - **Pattern expressions with only the following subpatterns:**
>   - **Boolean literal patterns.**
>   - **Numeric literal patterns.**
>   - **Character literal patterns.**
>   - **String literal patterns.**
>   - **Relative numeric literal patterns.**
>   - **Relative character literal patterns.**
>   - **Null literal patterns.**
>   - **Default literal patterns.**
> - sizeof expressions, provided the unmanaged-type is one of the types specified in §23.6.9 for which sizeof returns a constant value.
> - Default value expressions, provided the type is one of the types listed above.

The allowed subpatterns as shown in the list above are called "constant subpatterns", as they will be eligible for compile-time evaluation.

### Grammar
[grammar]: #grammar
The grammar remains untouched, as nothing changes syntactically for this change.

### Semantics
[semantics]: #semantics
A pattern expression only consisting of the above subpatterns with a constant LHS may be evaluated at compile time currently, but it cannot be assigned to a constant symbol. With this change, constant fields and locals of type `bool` may be assigned pattern expressions, as already evaluated during compilation.

### Examples
[examples]: #examples
The below example shows pattern expressions being assigned to a constant field.
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

Since `A` is constant, and all the operands on the right are constant, the entire expression only consists of constants, thus the expression is evaluated during compilation. This result will then be assigned to the constant field. Likewise, `Environment` is also constant as an enum value, and so are the other subpatterns of the expression.

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
- [ ] Should we introduce a new error for non-constant subpatterns in order to isolate the root cause of the inability to consider the expression constant?

## Design meetings
[meetings]: #design-meetings

- Approval for Any Time milestone: [Here](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-09.md#is-expression-evaluating-const-expression-should-be-considered-constant)