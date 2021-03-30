# Variable declarations under pattern combinators

## Summary  

## Motivation

## Specification

### Scope

#### Pattern variable redeclaration

Introducing a new concept similar to *name hiding*, but these names can possibly reference either of variables based on the result of the pattern-matching at runtime.

For a *disjunctive_pattern* of the form `left_pattern or right_pattern`:
- Pattern variables declared in the *left_pattern* can be redeclared in the *right_pattern*; permitting:

  ```cs
  e is (0, var x) or (var x, 0)
  ```

For a *logical_or_expression* of the form `left_expr || right_expr`:
- Pattern variables declared in *left_expr* can be redeclared in the *right_expr*; permitting:

  ```cs
  e is (0, var x) || e is (var x, 0)
  ```
  Note: This is the expression variation of the previous case.

For a *negated_pattern* of the form `not pattern_operand`:
- Pattern variables declared in the *pattern_operand* can be redeclared anywhere in the containing expression; permitting:

	```cs
	e is not (0, var x) and not (var x, 0)
	e is not (0, var x) && e is not (var x, 0)
	```
   Note: This is the DeMorgan's transformation of the previous case.

For a *logical_not_expression* of the form `!expr_operand`:
- Pattern variables declared in the *expr_operand* can be redeclared anywhere in the containing expression; permitting:

  ```cs
  !(e is (0, var x)) && !(e is (var x, 0))
  ```
  Note: This is the expression variation of the previous case.

For a *switch_case_label* of the form `case case_pattern when when_expr`:
- Pattern variables declared in the *case_pattern* or *when_expr* can be redeclared in other case labels; permitting:

	```cs
	case (var x, 0) when e is int i:
	case (0, var x) when a is int i:
	```

	Note: This rule is currently defined for each individual *switch_section* only.
	
For a *conditional_expression* of the form `expr_cond ? expr_true : expr_false`:
- Pattern variables declared in *expr_true* can be redeclared in *expr_false*; permitting:

	```cs
	b ? x is int i : y is int i
	```

Redeclaring pattern variables is only permitted for variables of the same type.

> **Open question**: How identical these types should be?

Redeclaring pattern variables in any other case follows the usual scoping rules and is disallowed.

### Definite assignment

Note: This section is unchanged and included for the sake of completeness.

For an *is_pattern_expression* of the form `e is pattern`:

- The state of *v* after *is_pattern_expression* is definitely assigned, if the *pattern* is irrefutable. permitting use after:
    
    ```cs
    _ = x is var x;
    _ = 1 is int x;
    _ = (1, 2) is var (x, y) and var z;
    ```

- Otherwise, the state of *v* after *is_pattern_expression* is the same as the state of *v* after *pattern*.

For a *var_pattern* of the form `var variable_designation`:

- The definite assignment state of *v* after *var_pattern* is determined by:

    - The state of *v* after *var_pattern* is definitely assigned if *variable_designation* is a *single_variable_designation*.
    - Otherwise, the state of *v* after *var_pattern* is "definitely assigned when true".

#### General rules for pattern variables in simple patterns 

Note: This section is unchanged and included for the sake of completeness.

The following rules applies to any *primary_pattern* that declares a variable:

- The state of *v* is "definitely assigned when true" after *primary_pattern*; permitting use after:

    ```cs
    if (o is int x)
    ```

#### Definite assignment rules for pattern variables in pattern combinators

Note: This section is derived by definite assignment rules for expressions.

> *Open question*: We should confirm if we want to allow patterns like `var x and 1`.

For a *disjunctive_pattern* of the form `left_pattern or right_pattern`:

- The definite assignment state of *v* after *disjunctive_pattern* is determined by:
    - If the state of *v* after *left_pattern* is definitely assigned, then the state of *v* after *disjunctive_pattern* is definitely assigned.
    - Otherwise, if the state of *v* after *right_pattern* is definitely assigned, and the state of *v* after *left_pattern* is "definitely assigned when true", then the state of *v* after *disjunctive_pattern* is definitely assigned.
    - Otherwise, if the state of *v* after *right_pattern* is definitely assigned or "definitely assigned when false", then the state of *v* after *disjunctive_pattern* is "definitely assigned when false".
    - Otherwise, if the state of *v* after *left_pattern* is "definitely assigned when true", and the state of *v* after *right_pattern* is "definitely assigned when true", then the state of *v* after *disjunctive_pattern* is "definitely assigned when true".
    - Otherwise, the state of *v* after *disjunctive_pattern* is not definitely assigned.

For a *conjunctive_pattern* of the form `left_pattern and right_pattern`:

- The definite assignment state of *v* after *conjunctive_pattern* is determined by:
    - If the state of *v* after *left_pattern* is definitely assigned, then the state of *v* after *conjunctive_pattern* is definitely assigned.
    - Otherwise, if the state of *v* after *right_pattern* is definitely assigned, and the state of *v* after *left_pattern* is "definitely assigned when false", then the state of *v* after *conjunctive_pattern* is definitely assigned.
    - Otherwise, if the state of *v* after *right_pattern* is definitely assigned or "definitely assigned when true", then the state of *v* after *conjunctive_pattern* is "definitely assigned when true".
    - Otherwise, if the state of *v* after *left_pattern* is "definitely assigned when false", and the state of *v* after *right_pattern* is "definitely assigned when false", then the state of *v* after *conjunctive_pattern* is "definitely assigned when false".
    - Otherwise, the state of *v* after *conjunctive_pattern* is not definitely assigned.

For a *negated_pattern* of the form `not pattern_operand`:

- The definite assignment state of *v* after *negated_pattern* is determined by:
    - If the state of *v* after *pattern_operand* is "definitely assigned when true", then the state of *v* after *negated_pattern* is "definitely assigned when false".
    - If the state of *v* after *pattern_operand* is "definitely assigned when false", then the state of *v* after *negated_pattern* is "definitely assigned when true".
    - If the state of *v* after *pattern_operand* is definitely assigned, then the state of *v* after *negated_pattern* is definitely assigned.

Note that these rules cover the existing top-level `is not` patterns. However, in other cases instead of a hard error, the variables will be left unassigned.

### Remarks

Definite assignment specification in any other case is unchanged. For instance, for a *conditional_expression*:

- If *expr_cond* is a constant expression with value `true` then the state of *v* after *expr* is the same as the state of *v* after *expr_true*; permitting use after:

	```cs
	if (true ? x is int i : y is int i)
	```

This document does not propose any changes to definite assignment rules for a *conditional_expression* to propagate conditional states when *cond_expr* is not a constant as it is covered by [improved definite assignment](https://github.com/dotnet/csharplang/blob/main/proposals/improved-definite-assignment.md#specification) proposal.
