# Variable declarations under disjunctive patterns

## Summary  

Allow variable declarations under `or` and `not` patterns and across `case` labels in a `switch` section.

## Motivation

This feature would reduce code duplication where we could use the same piece of code if either of patterns is satisfied. For instance:
```cs
if (e is C c or Wrapper { Prop: C c })
    return c;
  
Expr Simplify(Expr e)
{
  switch (e) {
    case Mult(Const(1), var x):
    case Mult(var x, Const(1)): 
    case Add(Const(0), var x):
    case Add(var x, Const(0)):
        return Simplify(x);
    // ..
  }
}
```
Instead of:

```cs
if (e is C c1) 
    return c1;
if (e is Wrapper { Prop: C c2 }) 
    return c2;
  
Expr Simplify(Expr e)
{
  switch (e) {
    case Mult(Const(1), var x):
        return Simplify(x);
    case Mult(var x, Const(1)): 
        return Simplify(x);
    case Add(Const(0), var x):
        return Simplify(x);
    case Add(var x, Const(0)):
        return Simplify(x);
    // ..
  }
}
```

## Detailed design

### Scope

The single-declaration requirement of certain pattern variables is relaxed as follows:

- Pattern variables may be redeclared in each label of a *switch_section*:

	```cs
	case (var x, 0):
	case (0, var x):
	```
	
- Pattern variables may be redeclared in either side of a *disjunctive_pattern*:

  ```cs
  e is (0, var x) or (var x, 0)
  ```
  
- Pattern variables may be redeclared within the entire pattern if declared under a *negated_pattern*:

  ```cs
  e is not (0, var x) and not (var x, 0)
  ```
  But not if contained within a *disjunctive_pattern*:
  
  ```cs
  e is not (0, var x) or not (var x, 0) // error
  ```

Note that this does not alter the scope of pattern variables itself, rather these names can possibly reference either of variables within the existing scope based on the result of the pattern-matching at runtime.

Pattern variables with multiple declarations must be of the same type, excluding nullability for reference types:
```cs
case C { Property: int x } or D { Property: long x }: x.ToString(); // error
case C { NonNullable: var x } or D { Nullable: var x }: x.ToString(); // warning
case C { NonNullable: var x } or D { NonNullable: var x }: x.ToString(); // ok
```

Redeclaring pattern variables in any other case follows the usual scoping rules and is disallowed.

### Definite assignment

For an *is_pattern_expression* of the form `e is pattern`, the definite assignment state of *v* after *is_pattern_expression* is determined by:

- The state of *v* after *is_pattern_expression* is definitely assigned, if *pattern* is irrefutable; permitting use after:
    
  ```cs
  _ = x is var x;
  _ = 1 is int x;
  _ = (1, 2) is var (x, y) and var z;
  ```

- Otherwise, the state of *v* after *is_pattern_expression* is the same as the state of *v* after *pattern*.

For a *switch_section* of the form `case pattern_1 when condition_1: ... case pattern_N when condition_N:`, the definite assignment state of *v* is determined by:

- The state of *v* before *condition_i* is definitely assigned, if the state of *v* after *pattern_i* is "definitely assigned when true".
- The state of *v* before *switch_section_body* is definitely assigned, if the state of *v* after each *pattern_i* is "definitely assigned when true".
- Otherwise, the state of *v* is not definitely assigned.

#### General definite assignment rules for pattern variables

The following rule applies to any *primary_pattern* that declares a variable, namely *list_pattern*, *declaration_pattern*, *recursive_pattern*, and *var_pattern*, as well as any nested subpatterns.

- The state of *v* is "definitely assigned when true" after *primary_pattern*; permitting use after:

  ```cs
  if (o is int x)
  if (o is (int x, int y))
  ```

#### Definite assignment rules for pattern variables declared under logical patterns

For a *disjunctive_pattern* *p* of the form `left or right`, the definite assignment state of *v* after *p* is determined by:
- If the state of *v* after both *left* and *right* is "definitely assigned when true", then the state of *v* after *p* is "definitely assigned when true".
- If the state of *v* after both *left*  and *right* is "definitely assigned when false", then the state of *v* after *p* is "definitely assigned when false".
- Otherwise, the state of *v* after *p* is not definitely assigned.

For a *conjunctive_pattern* *p*  of the form `left and right`, the definite assignment state of *v* after *p* is determined by:
- If the state of *v* after either *left* or *right* is "definitely assigned when true", then the state of *v* after *p* is "definitely assigned when true".
- If the state of *v* after both *left*  and *right* is "definitely assigned when false", then the state of *v* after *p* is "definitely assigned when false".
- Otherwise, the state of *v* after *p* is not definitely assigned.

For a *negated_pattern* *p* of the form `not pattern`, the definite assignment state of *v* after *p* is determined by:
- If the state of *v* after *pattern* is "definitely assigned when true", then the state of *v* after *p* is "definitely assigned when false".
- If the state of *v* after *pattern* is "definitely assigned when false", then the state of *v* after *p* is "definitely assigned when true".

These rules cover the existing top-level `is not` pattern variables. However, instead of an error, the variables could be left unassigned.
