# Improved Definite Assignment Analysis

## Summary
[Definite assignment analysis](../spec/variables.md#definite-assignment) as specified has a few gaps which have caused users inconvenience. In particular, scenarios involving comparison to boolean constants, conditional-access, and null coalescing.

## Related discussions and issues
csharplang discussion of this proposal: https://github.com/dotnet/csharplang/discussions/4240

Probably a dozen or so user reports can be found via this or similar queries (i.e. search for "definite assignment" instead of "CS0165", or search in csharplang).
https://github.com/dotnet/roslyn/issues?q=is%3Aclosed+is%3Aissue+label%3A%22Resolution-By+Design%22+cs0165

I have included related issues in the scenarios below to give a sense of the relative impact of each scenario.

## Scenarios

As a point of reference, let's start with a well-known "happy case" that does work in definite assignment and in nullable.
```cs
#nullable enable

C c = new C();
if (c != null && c.M(out object obj0))
{
    obj0.ToString(); // ok
}

public class C
{
    public bool M(out object obj)
    {
        obj = new object();
        return true;
    }
}
```

### Comparison to bool constant
- https://github.com/dotnet/csharplang/discussions/801
- https://github.com/dotnet/roslyn/issues/45582
  - Links to 4 other issues where people were affected by this.
```cs
if ((c != null && c.M(out object obj1)) == true)
{
    obj1.ToString(); // undesired error
}

if ((c != null && c.M(out object obj2)) is true)
{
    obj2.ToString(); // undesired error
}
```

### Comparison between a conditional access and a constant value

- https://github.com/dotnet/roslyn/issues/33559
- https://github.com/dotnet/csharplang/discussions/4214
- https://github.com/dotnet/csharplang/issues/3659
- https://github.com/dotnet/csharplang/issues/3485
- https://github.com/dotnet/csharplang/issues/3659

This scenario is probably the biggest one. We do support this in nullable but not in definite assignment.

```cs
if (c?.M(out object obj3) == true)
{
    obj3.ToString(); // undesired error
}
```

### Conditional access coalesced to a bool constant

- https://github.com/dotnet/csharplang/discussions/916
- https://github.com/dotnet/csharplang/issues/3365

This scenario is very similar to the previous one. This is also supported in nullable but not in definite assignment.

```cs
if (c?.M(out object obj4) ?? false)
{
    obj4.ToString(); // undesired error
}
```

### Conditional expressions where one arm is a bool constant
- https://github.com/dotnet/roslyn/issues/4272

It's worth pointing out that we already have special behavior for when the condition expression is constant (i.e. `true ? a : b`). We just unconditionally visit the arm indicated by the constant condition and ignore the other arm.

Also note that we haven't handled this scenario in nullable.

```cs
if (c != null ? c.M(out object obj4) : false)
{
    obj4.ToString(); // undesired error
}
```

# Specification

## ?. (null-conditional operator) expressions
We introduce a new section **?. (null-conditional operator) expressions**. See the [null-conditional operator](../spec/expressions.md#null-conditional-operator) specification and [definite assignment rules](../spec/variables.md#precise-rules-for-determining-definite-assignment) for context.

As in the definite assignment rules linked above, we refer to a given initially unassigned variable as *v*.

We introduce the concept of "directly contains". An expression *E* is said to "directly contain" a subexpression *E<sub>1</sub>* if it is not subject to a [user-defined conversion](../spec/conversions.md#user-defined-conversions) whose parameter is not of a non-nullable value type, and one of the following conditions holds:
- *E* is *E<sub>1</sub>*. For example, `a?.b()` directly contains the expression `a?.b()`.
- If *E* is a parenthesized expression `(E2)`, and *E<sub>2</sub>* directly contains *E<sub>1</sub>*.
- If *E* is a null-forgiving operator expression `E2!`, and *E<sub>2</sub>* directly contains *E<sub>1</sub>*.
- If *E* is a cast expression `(T)E2`, and the cast does not subject *E<sub>2</sub>* to a non-lifted user-defined conversion whose parameter is not of a non-nullable value type, and *E<sub>2</sub>* directly contains *E<sub>1</sub>*.

For an expression *E* of the form `primary_expression null_conditional_operations`, let *E<sub>0</sub>* be the expression obtained by textually removing the leading ? from each of the *null_conditional_operations* of *E* that have one, as in the linked specification above.

In subsequent sections we will refer to *E<sub>0</sub>* as the *non-conditional counterpart* to the null-conditional expression. Note that some expressions in subsequent sections are subject to additional rules that only apply when one of the operands directly contains a null-conditional expression.

- The definite assignment state of *v* at any point within *E* is the same as the definite assignment state at the corresponding point within *E0*.
- The definite assignment state of *v* after *E* is the same as the definite assignment state of *v* after *primary_expression*.

### Remarks
We use the concept of "directly contains" to allow us to skip over relatively simple "wrapper" expressions when analyzing conditional accesses that are compared to other values. For example, `((a?.b(out x))!) == true` is expected to result in the same flow state as `a?.b == true` in general.

We also want to allow analysis to function in the presence of a number of possible conversions on a conditional access. Propagating out "state when not null" is not possible when the conversion is user-defined, though, since we can't count on user-defined conversions to honor the constraint that the output is non-null only if the input is non-null. The only exception to this is when the user-defined conversion's input is a non-nullable value type. For example:
```cs
public struct S1 { }
public struct S2 { public static implicit operator S2?(S1 s1) => null; }
```

This also includes lifted conversions like the following:
```cs
string x;

S1? s1 = null;
_ = s1?.M1(x = "a") ?? s1.Value.M2(x = "a");

x.ToString(); // ok

public struct S1
{
    public S1 M1(object obj) => this;
    public S2 M2(object obj) => new S2();
}
public struct S2
{
    public static implicit operator S2(S1 s1) => null;
}
```

When we consider whether a variable is assigned at a given point within a null-conditional expression, we simply assume that any preceding null-conditional operations within the same null-conditional expression succeeded.

For example, given a conditional expression `a?.b(out x)?.c(x)`, the non-conditional counterpart is `a.b(out x).c(x)`. If we want to know the definite assignment state of `x` before `?.c(x)`, for example, then we perform a "hypothetical" analysis of `a.b(out x)` and use the resulting state as an input to `?.c(x)`.

## Boolean constant expressions
We introduce a new section "Boolean constant expressions":

For an expression *expr* where *expr* is a constant expression with a bool value:
- The definite assignment state of *v* after *expr* is determined by:
  - If *expr* is a constant expression with value *true*, and the state of *v* before *expr* is "not definitely assigned", then the state of *v* after *expr* is "definitely assigned when false".
  - If *expr* is a constant expression with value *false*, and the state of *v* before *expr* is "not definitely assigned", then the state of *v* after *expr* is "definitely assigned when true".

### Remarks

We assume that if an expression has a constant value bool `false`, for example, it's impossible to reach any branch that requires the expression to return `true`. Therefore variables are assumed to be definitely assigned in such branches. This ends up combining nicely with the spec changes for expressions like `??` and `?:` and enabling a lot of useful scenarios.

It's also worth noting that we never expect to be in a conditional state *before* visiting a constant expression. That's why we do not account for scenarios such as "*expr* is a constant expression with value *true*, and the state of *v* before *expr* is "definitely assigned when true".

## ?? (null-coalescing expressions) augment
We augment the section [?? (null coalescing) expressions](../spec/variables.md#-null-coalescing-expressions) as follows:

For an expression *expr* of the form `expr_first ?? expr_second`:
- ...
- The definite assignment state of *v* after *expr* is determined by:
  - ...
  - If *expr_first* directly contains a null-conditional expression *E*, and *v* is definitely assigned after the non-conditional counterpart *E<sub>0</sub>*, then the definite assignment state of *v* after *expr* is the same as the definite assignment state of *v* after *expr_second*.

### Remarks
The above rule formalizes that for an expression like `a?.M(out x) ?? (x = false)`, either the `a?.M(out x)` was fully evaluated and produced a non-null value, in which case `x` was assigned, or the `x = false` was evaluated, in which case `x` was also assigned. Therefore `x` is always assigned after this expression.

This also handles the `dict?.TryGetValue(key, out var value) ?? false` scenario, by observing that *v* is definitely assigned after `dict.TryGetValue(key, out var value)`, and *v* is "definitely assigned when true" after `false`, and concluding that *v* must be "definitely assigned when true".

The more general formulation also allows us to handle some more unusual scenarios, such as:
- `if (x?.M(out y) ?? (b && z.M(out y))) y.ToString();`
- `if (x?.M(out y) ?? z?.M(out y) ?? false) y.ToString();`

## ?: (conditional) expressions
We augment the section [**?: (conditional) expressions**](../spec/variables.md#-conditional-expressions) as follows:

For an expression *expr* of the form `expr_cond ? expr_true : expr_false`:
- ...
- The definite assignment state of *v* after *expr* is determined by:
  - ...
  - If the state of *v* after *expr_true* is "definitely assigned when true", and the state of *v* after *expr_false* is "definitely assigned when true", then the state of *v* after *expr* is "definitely assigned when true".
  - If the state of *v* after *expr_true* is "definitely assigned when false", and the state of *v* after *expr_false* is "definitely assigned when false", then the state of *v* after *expr* is "definitely assigned when false".

### Remarks

This makes it so when both arms of a conditional expression result in a conditional state, we join the corresponding conditional states and propagate it out instead of unsplitting the state and allowing the final state to be non-conditional. This enables scenarios like the following:

```cs
bool b = true;
object x = null;
int y;
if (b ? x != null && Set(out y) : x != null && Set(out y))
{
  y.ToString();
}

bool Set(out int x) { x = 0; return true; }
```

This is an admittedly niche scenario, that compiles without error in the native compiler, but was broken in Roslyn in order to match the specification at the time. See [internal issue](http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/529603).

## ==/!= (relational equality operator) expressions
We introduce a new section **==/!= (relational equality operator) expressions**.

The [general rules for expressions with embedded expressions](../spec/variables.md#general-rules-for-expressions-with-embedded-expressions) apply, except for the scenarios described below.

For an expression *expr* of the form `expr_first == expr_second`, where `==` is a [predefined comparison operator](../spec/expressions.md#relational-and-type-testing-operators) or a [lifted operator](../spec/expressions.md#lifted-operators), the definite assignment state of *v* after *expr* is determined by:
  - If *expr_first* directly contains a null-conditional expression *E* and *expr_second* is a constant expression with value *null*, and the state of *v* after the non-conditional counterpart *E<sub>0</sub>* is "definitely assigned", then the state of *v* after *expr* is "definitely assigned when false".
  - If *expr_first* directly contains a null-conditional expression *E* and *expr_second* is an expression of a non-nullable value type, or a constant expression with a non-null value, and the state of *v* after the non-conditional counterpart *E<sub>0</sub>* is "definitely assigned", then the state of *v* after *expr* is "definitely assigned when true".
  - If *expr_first* is of type *boolean*, and *expr_second* is a constant expression with value *true*, then the definite assignment state after *expr* is the same as the definite assignment state after *expr_first*.
  - If *expr_first* is of type *boolean*, and *expr_second* is a constant expression with value *false*, then the definite assignment state after *expr* is the same as the definite assignment state of *v* after the logical negation expression `!expr_first`.

For an expression *expr* of the form `expr_first != expr_second`, where `!=` is a [predefined comparison operator](../spec/expressions.md#relational-and-type-testing-operators) or a [lifted operator](../spec/expressions.md#lifted-operators), the definite assignment state of *v* after *expr* is determined by:
  - If *expr_first* directly contains a null-conditional expression *E* and *expr_second* is a constant expression with value *null*, and the state of *v* after the non-conditional counterpart *E<sub>0</sub>* is "definitely assigned", then the state of *v* after *expr* is "definitely assigned when true".
  - If *expr_first* directly contains a null-conditional expression *E* and *expr_second* is an expression of a non-nullable value type, or a constant expression with a non-null value, and the state of *v* after the non-conditional counterpart *E<sub>0</sub>* is "definitely assigned", then the state of *v* after *expr* is "definitely assigned when false".
  - If *expr_first* is of type *boolean*, and *expr_second* is a constant expression with value *true*, then the definite assignment state after *expr* is the same as the definite assignment state of *v* after the logical negation expression `!expr_first`.
  - If *expr_first* is of type *boolean*, and *expr_second* is a constant expression with value *false*, then the definite assignment state after *expr* is the same as the definite assignment state after *expr_first*.

All of the above rules in this section are commutative, meaning that if a rule applies when evaluated in the form `expr_second op expr_first`, it also applies in the form `expr_first op expr_second`.

### Remarks
The general idea expressed by these rules is:
- if a conditional access is compared to `null`, then we know the operations definitely occurred if the result of the comparison is `false`
- if a conditional access is compared to a non-nullable value type or a non-null constant, then we know the operations definitely occurred if the result of the comparison is `true`.
- since we can't trust user-defined operators to provide reliable answers where initialization safety is concerned, the new rules only apply when a predefined `==`/`!=` operator is in use.

We may eventually want to refine these rules to thread through conditional state which is present at the end of a member access or call. Such scenarios don't really happen in definite assignment, but they do happen in nullable in the presence of `[NotNullWhen(true)]` and similar attributes. This would require special handling for `bool` constants in addition to just handling for `null`/non-null constants.

Some consequences of these rules:
- `if (a?.b(out var x) == true)) x() else x();` will error in the 'else' branch
- `if (a?.b(out var x) == 42)) x() else x();` will error in the 'else' branch
- `if (a?.b(out var x) == false)) x() else x();` will error in the 'else' branch
- `if (a?.b(out var x) == null)) x() else x();` will error in the 'then' branch
- `if (a?.b(out var x) != true)) x() else x();` will error in the 'then' branch
- `if (a?.b(out var x) != 42)) x() else x();` will error in the 'then' branch
- `if (a?.b(out var x) != false)) x() else x();` will error in the 'then' branch
- `if (a?.b(out var x) != null)) x() else x();` will error in the 'else' branch

## `is` operator and `is` pattern expressions
We introduce a new section **`is` operator and `is` pattern expressions**.

For an expression *expr* of the form `E is T`, where *T* is any type or pattern
- The definite assignment state of *v* before *E* is the same as the definite assignment state of *v* before *expr*.
- The definite assignment state of *v* after *expr* is determined by:
  - If *E* directly contains a null-conditional expression, and the state of *v* after the non-conditional counterpart *E<sub>0</sub>* is "definitely assigned", and `T` is any type or a pattern that only matches a non-null input, then the state of *v* after *expr* is "definitely assigned when true".
  - If *E* directly contains a null-conditional expression, and the state of *v* after the non-conditional counterpart *E<sub>0</sub>* is "definitely assigned", and `T` is a pattern which only matches a null input, then the state of *v* after *expr* is "definitely assigned when false".
  - If *E* is of type boolean and `T` is a pattern which only matches a `true` input, then the definite assignment state of *v* after *expr* is the same as the definite assignment state of *v* after E.
  - If *E* is of type boolean and `T` is a pattern which only matches a `false` input, then the definite assignment state of *v* after *expr* is the same as the definite assignment state of *v* after the logical negation expression `!expr`.
  - Otherwise, if the definite assignment state of *v* after E is "definitely assigned", then the definite assignment state of *v* after *expr* is "definitely assigned".

### Remarks

This section is meant to address similar scenarios as in the `==`/`!=` section above.
This specification does not address recursive patterns, e.g. `(a?.b(out x), c?.d(out y)) is (object, object)`. Such support may come later if time permits.

# Additional scenarios

This specification doesn't currently address scenarios involving pattern switch expressions and switch statements. For example:

```cs
_ = c?.M(out object obj4) switch
{
    not null => obj4.ToString() // undesired error
};
```

It seems like support for this could come later if time permits.

There have been several categories of bugs filed for nullable which require we essentially increase the sophistication of pattern analysis. It is likely that any ruling we make which improves definite assignment would also be carried over to nullable.

https://github.com/dotnet/roslyn/issues/49353  
https://github.com/dotnet/roslyn/issues/46819  
https://github.com/dotnet/roslyn/issues/44127

## Drawbacks
[drawbacks]: #drawbacks

It feels odd to have the analysis "reach down" and have special recognition of conditional accesses, when typically flow analysis state is supposed to propagate upward. We are concerned about how a solution like this could intersect painfully with possible future language features that do null checks.

## Alternatives
[alternatives]: #alternatives

Two alternatives to this proposal:
1. Introduce "state when null" and "state when not null" to the language and compiler. This has been judged to be too much effort for the scenarios we are trying to solve, but that we could potentially implement the above proposal and then move to a "state when null/not null" model later on without breaking people.
2. Do nothing.

## Unresolved questions
[unresolved]: #unresolved-questions

There are impacts on switch expressions that should be specified: https://github.com/dotnet/csharplang/discussions/4240#discussioncomment-343395

## Design meetings

https://github.com/dotnet/csharplang/discussions/4243
