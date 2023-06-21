# Pattern-matching changes for C# 9.0

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

We are considering a small handful of enhancements to pattern-matching for C# 9.0 that have natural synergy and work well to address a number of common programming problems:
- https://github.com/dotnet/csharplang/issues/2925 Type patterns
- https://github.com/dotnet/csharplang/issues/1350 Parenthesized patterns to enforce or emphasize precedence of the new combinators
- https://github.com/dotnet/csharplang/issues/1350 Conjunctive `and` patterns that require both of two different patterns to match;
- https://github.com/dotnet/csharplang/issues/1350 Disjunctive `or` patterns that require either of two different patterns to match;
- https://github.com/dotnet/csharplang/issues/1350 Negated `not` patterns that require a given pattern *not* to match; and
- https://github.com/dotnet/csharplang/issues/812 Relational patterns that require the input value to be less than, less than or equal to, etc a given constant.

## Parenthesized Patterns

Parenthesized patterns permit the programmer to put parentheses around any pattern.  This is not so useful with the existing patterns in C# 8.0, however the new pattern combinators introduce a precedence that the programmer may want to override.

```antlr
primary_pattern
    : parenthesized_pattern
    | // all of the existing forms
    ;
parenthesized_pattern
    : '(' pattern ')'
    ;
```

## Type Patterns

We permit a type as a pattern:

``` antlr
primary_pattern
    : type-pattern
    | // all of the existing forms
    ;
type_pattern
    : type
    ;
```

This retcons the existing *is-type-expression* to be an *is-pattern-expression* in which the pattern is a *type-pattern*, though we would not change the syntax tree produced by the compiler.

One subtle implementation issue is that this grammar is ambiguous.  A string such as `a.b` can be parsed either as a qualified name (in a type context) or a dotted expression (in an expression context).  The compiler is already capable of treating a qualified name the same as a dotted expression in order to handle something like `e is Color.Red`.  The compiler's semantic analysis would be further extended to be capable of binding a (syntactic) constant pattern (e.g. a dotted expression) as a type in order to treat it as a bound type pattern in order to support this construct.

After this change, you would be able to write
```csharp
void M(object o1, object o2)
{
    var t = (o1, o2);
    if (t is (int, string)) {} // test if o1 is an int and o2 is a string
    switch (o1) {
        case int: break; // test if o1 is an int
        case System.String: break; // test if o1 is a string
    }
}
```

## Relational Patterns

Relational patterns permit the programmer to express that an input value must satisfy a relational constraint when compared to a constant value:

``` C#
    public static LifeStage LifeStageAtAge(int age) => age switch
    {
        < 0 =>  LifeStage.Prenatal,
        < 2 =>  LifeStage.Infant,
        < 4 =>  LifeStage.Toddler,
        < 6 =>  LifeStage.EarlyChild,
        < 12 => LifeStage.MiddleChild,
        < 20 => LifeStage.Adolescent,
        < 40 => LifeStage.EarlyAdult,
        < 65 => LifeStage.MiddleAdult,
        _ =>    LifeStage.LateAdult,
    };
```

Relational patterns support the relational operators `<`, `<=`, `>`, and `>=` on all of the built-in types that support such binary relational operators with two operands of the same type in an expression. Specifically, we support all of these relational patterns for `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, `double`, `decimal`, `nint`, and `nuint`.

```antlr
primary_pattern
    : relational_pattern
    ;
relational_pattern
    : '<' relational_expression
    | '<=' relational_expression
    | '>' relational_expression
    | '>=' relational_expression
    ;
```

The expression is required to evaluate to a constant value.  It is an error if that constant value is `double.NaN` or `float.NaN`.  It is an error if the expression is a null constant.

When the input is a type for which a suitable built-in binary relational operator is defined that is applicable with the input as its left operand and the given constant as its right operand, the evaluation of that operator is taken as the meaning of the relational pattern.  Otherwise we convert the input to the type of the expression using an explicit nullable or unboxing conversion.  It is a compile-time error if no such conversion exists.  The pattern is considered not to match if the conversion fails.  If the conversion succeeds then the result of the pattern-matching operation is the result of evaluating the expression `e OP v` where `e` is the converted input, `OP` is the relational operator, and `v` is the constant expression.

## Pattern Combinators

Pattern *combinators* permit matching both of two different patterns using `and` (this can be extended to any number of patterns by the repeated use of `and`), either of two different patterns using `or` (ditto), or the *negation* of a pattern using `not`.

A common use of a combinator will be the idiom

``` c#
if (e is not null) ...
```

More readable than the current idiom `e is object`, this pattern clearly expresses that one is checking for a non-null value.

The `and` and `or` combinators will be useful for testing ranges of values

``` c#
bool IsLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
```

This example illustrates that `and` will have a higher parsing priority (i.e. will bind more closely) than `or`.  The programmer can use the *parenthesized pattern* to make the precedence explicit:

``` c#
bool IsLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
```

Like all patterns, these combinators can be used in any context in which a pattern is expected, including nested patterns, the *is-pattern-expression*, the *switch-expression*, and the pattern of a switch statement's case label.

```antlr
pattern
    : disjunctive_pattern
    ;
disjunctive_pattern
    : disjunctive_pattern 'or' conjunctive_pattern
    | conjunctive_pattern
    ;
conjunctive_pattern
    : conjunctive_pattern 'and' negated_pattern
    | negated_pattern
    ;
negated_pattern
    : 'not' negated_pattern
    | primary_pattern
    ;
primary_pattern
    : // all of the patterns forms previously defined
    ;
```

## Change to 7.5.4.2 Grammar Ambiguities

Due to the introduction of the *type pattern*, it is possible for a generic type to appear before the token `=>`.  We therefore add `=>` to the set of tokens listed in *7.5.4.2 Grammar Ambiguities* to permit disambiguation of the `<` that begins the type argument list.  See also https://github.com/dotnet/roslyn/issues/47614.

## Open Issues with Proposed Changes

### Syntax for relational operators

Are `and`, `or`, and `not` some kind of contextual keyword?  If so, is there a breaking change (e.g. compared to their use as a designator in a *declaration-pattern*).

### Semantics (e.g. type) for relational operators

We expect to support all of the primitive types that can be compared in an expression using a relational operator.  The meaning in simple cases is clear

``` c#
bool IsValidPercentage(int x) => x is >= 0 and <= 100;
```

But when the input is not such a primitive type, what type do we attempt to convert it to?

``` c#
bool IsValidPercentage(object x) => x is >= 0 and <= 100;
```

We have proposed that when the input type is already a comparable primitive, that is the type of the comparison. However, when the input is not a comparable primitive, we treat the relational as including an implicit type test to the type of the constant on the right-hand-side of the relational.  If the programmer intends to support more than one input type, that must be done explicitly:

``` c#
bool IsValidPercentage(object x) => x is
    >= 0 and <= 100 or    // integer tests
    >= 0F and <= 100F or  // float tests
    >= 0D and <= 100D;    // double tests
```

### Flowing type information from the left to the right of `and`

It has been suggested that when you write an `and` combinator, type information learned on the left about the top-level type could flow to the right.  For example

```csharp
bool isSmallByte(object o) => o is byte and < 100;
```

Here, the *input type* to the second pattern is narrowed by the *type narrowing* requirements of left of the `and`.  We would define type narrowing semantics for all patterns as follows.  The *narrowed type* of a pattern `P` is defined as follows:
1. If `P` is a type pattern, the *narrowed type* is the type of the type pattern's type.
2. If `P` is a declaration pattern, the *narrowed type* is the type of the declaration pattern's type.
3. If `P` is a recursive pattern that gives an explicit type, the *narrowed type* is that type.
4. If `P` is [matched via the rules for](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/patterns.md#positional-pattern) `ITuple`, the *narrowed type* is the type `System.Runtime.CompilerServices.ITuple`.
5. If `P` is a constant pattern where the constant is not the null constant and where the expression has no *constant expression conversion* to the *input type*, the *narrowed type* is the type of the constant.
6. If `P` is a relational pattern where the constant expression has no *constant expression conversion* to the *input type*, the *narrowed type* is the type of the constant.
7. If `P` is an `or` pattern, the *narrowed type* is the common type of the *narrowed type* of the subpatterns if such a common type exists. For this purpose, the common type algorithm considers only identity, boxing, and implicit reference conversions, and it considers all subpatterns of a sequence of `or` patterns (ignoring parenthesized patterns).
8. If `P` is an `and` pattern, the *narrowed type* is the *narrowed type* of the right pattern. Moreover, the *narrowed type* of the left pattern is the *input type* of the right pattern.
9. Otherwise the *narrowed type* of `P` is `P`'s input type.

### Variable definitions and definite assignment

The addition of `or` and `not` patterns creates some interesting new problems around pattern variables and definite assignment.  Since variables can normally be declared at most once, it would seem any pattern variable declared on one side of an `or` pattern would not be definitely assigned when the pattern matches.  Similarly, a variable declared inside a `not` pattern would not be expected to be definitely assigned when the pattern matches.  The simplest way to address this is to forbid declaring pattern variables in these contexts.  However, this may be too restrictive.  There are other approaches to consider.

One scenario that is worth considering is this

``` csharp
if (e is not int i) return;
M(i); // is i definitely assigned here?
```

This does not work today because, for an *is-pattern-expression*, the pattern variables are considered *definitely assigned* only where the *is-pattern-expression* is true ("definitely assigned when true").

Supporting this would be simpler (from the programmer's perspective) than also adding support for a negated-condition `if` statement.  Even if we add such support, programmers would wonder why the above snippet does not work.  On the other hand, the same scenario in a `switch` makes less sense, as there is no corresponding point in the program where *definitely assigned when false* would be meaningful.  Would we permit this in an *is-pattern-expression* but not in other contexts where patterns are permitted?  That seems irregular.

Related to this is the problem of definite assignment in a *disjunctive-pattern*.

```csharp
if (e is 0 or int i)
{
    M(i); // is i definitely assigned here?
}
```

We would only expect `i` to be definitely assigned when the input is not zero.  But since we don't know whether the input is zero or not inside the block, `i` is not definitely assigned.  However, what if we permit `i` to be declared in different mutually exclusive patterns?

```csharp
if ((e1, e2) is (0, int i) or (int i, 0))
{
    M(i);
}
```

Here, the variable `i` is definitely assigned inside the block, and takes it value from the other element of the tuple when a zero element is found.

It has also been suggested to permit variables to be (multiply) defined in every case of a case block:

```csharp
    case (0, int x):
    case (int x, 0):
        Console.WriteLine(x);
```

To make any of this work, we would have to carefully define where such multiple definitions are permitted and under what conditions such a variable is considered definitely assigned.

Should we elect to defer such work until later (which I advise), we could say in C# 9
- beneath a `not` or `or`, pattern variables may not be declared.

Then, we would have time to develop some experience that would provide insight into the possible value of relaxing that later.

### Diagnostics, subsumption, and exhaustiveness

These new pattern forms introduce many new opportunities for diagnosable programmer error.  We will need to decide what kinds of errors we will diagnose, and how to do so.  Here are some examples:

``` csharp
case >= 0 and <= 100D:
```

This case can never match (because the input cannot be both an `int` and a `double`).  We already have an error when we detect a case that can never match, but its wording ("The switch case has already been handled by a previous case" and "The pattern has already been handled by a previous arm of the switch expression") may be misleading in new scenarios.  We may have to modify the wording to just say that the pattern will never match the input.

``` csharp
case 1 and 2:
```

Similarly, this would be an error because a value cannot be both `1` and `2`.

``` csharp
case 1 or 2 or 3 or 1:
```

This case is possible to match, but the `or 1` at the end adds no meaning to the pattern.  I suggest we should aim to produce an error whenever some conjunct or disjunct of a compound pattern does not either define a pattern variable or affect the set of matched values.

``` csharp
case < 2: break;
case 0 or 1 or 2 or 3 or 4 or 5: break;
```

Here, `0 or 1 or` adds nothing to the second case, as those values would have been handled by the first case.  This too deserves an error.

``` csharp
byte b = ...;
int x = b switch { <100 => 0, 100 => 1, 101 => 2, >101 => 3 };
```

A switch expression such as this should be considered *exhaustive* (it handles all possible input values).

In C# 8.0, a switch expression with an input of type `byte` is only considered exhaustive if it contains a final arm whose pattern matches everything (a *discard-pattern* or *var-pattern*).  Even a switch expression that has an arm for every distinct `byte` value is not considered exhaustive in C# 8.  In order to properly handle exhaustiveness of relational patterns, we will have to handle this case too.  This will technically be a breaking change, but no user is likely to notice.
