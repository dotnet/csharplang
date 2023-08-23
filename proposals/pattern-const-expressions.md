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

When the pattern expressions are converted into simpler boolean expressions using equality and comparison operators, they are considered constant expressions if all parts of the expression are considered constant and can be evaluated during compilation. Also, the expressions returned from the ternary operator can also be considered constant if all sides are constant (condition, consequence and alternative).

## Detailed design
[design]: #detailed-design

### Spec
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
> - **Pattern expressions.**
> - sizeof expressions, provided the unmanaged-type is one of the types specified in §23.6.9 for which sizeof returns a constant value.
> - Default value expressions, provided the type is one of the types listed above.

### Grammar
The grammar remains untouched, as nothing changes syntactically for this change.

### Semantics
A pattern expression with a constant LHS may be evaluated at compile time currently, but it cannot be assigned to a constant symbol. With this change, constant fields and locals of type `bool` may be assigned pattern expressions, as already evaluated during compilation.

### Examples
The below example shows a pattern expression being assigned to a constant field.
```csharp
public const int A = 4;
public const bool B = A is 4;
public const bool C = A is not 3 and < 1 or 5; 
```

Since `A` is constant, and pattern expressions require that the operands on the right are constant, all operands are constant, and the expression is thus evaluated during compilation. This result will then be assigned to the constant field.

Another example, using type and `null` value checks:
```csharp
const int a = 4;
const bool b = false;
const long c = 4;
const string d = "hello";

const bool x = a is int; // always true
const bool y = a is long; // always false
const bool z = d is not null; // always true
const bool p = b is null; // always false
```

All the above are currently valid pattern matching expressions, that also emit warnings about their constant evaluation results, about them being always true or false.

When assigning those expressions to a constant symbol, it would be preferrable to not report these warnings about the constant result of the expression.

Expressions accessing properties and comparing their values are illegal, as property evaluation occurs at runtime:

```csharp
const bool q = d is string { Length: 5 }; // Error: not a constant expression
```

## Drawbacks
[drawbacks]: #drawbacks

None.

## Alternatives
[alternatives]: #alternatives

Currently, equality and comparison operators can be used to compare against other literals, including nullability of the objects (e.g. `x != null`, or `y == 4 || y < 3`). However, type checking cannot be currently peformed at compile time and assigned to a constant.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Requires LDM review

## Design meetings
[meetings]: #design-meetings

None.
