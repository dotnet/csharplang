# Variable declarations under disjunctive patterns

## Summary  

- Allow variable declarations under `or` and `not` patterns and across `case` labels in a `switch` section to share code.
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

- Also relax single-declaration rules within expression boundaries as long as each variable is assigned once.
	```cs
	if (e is C c || e is Wrapper { Prop: C c }) ;
	if (b ? e is C c : e is Wrapper { Prop: C c }) ;
	```
	
- Also relax single-declaration rules within conditions of an `if/else`:
	```cs
	if (e is C c) { }
        else if (e is Wrapper { Prop: C c }) { }
	```
	
## Detailed design

### Variable redeclaration

- Pattern variables are allowed to be redeclared in the following locations if not already definitely assigned:
	- Within case labels for the same switch section (includes `when` clauses)
	- Within a single expression (includes `is` expressions)
	- Within a single pattern (includes `switch` expression arms)
	- Across top-level condition expressions within a single `if` statement (includes `else if`)

  These names can possibly reference either of variables based on the result of the pattern-matching at runtime.
- Pattern variables with multiple declarations must be of the same type, excluding top-level nullability for reference types. Differences in nested nullability are subject to standard nullability conversion warnings.

        if (e is C c || e is Wrapper { Prop: var c }) { /* c may be null */ }

### Definite assignment

For an *is_pattern_expression* of the form `e is pattern`, the definite assignment state of *v* after *is_pattern_expression* is determined by:

- The state of *v* after *is_pattern_expression* is definitely assigned, if *pattern* is irrefutable.
- Otherwise, the state of *v* after *is_pattern_expression* is the same as the state of *v* after *pattern*.

For a *switch_section* of the form `case pattern_1 when condition_1: ... case pattern_N when condition_N:`, the definite assignment state of *v* is determined by:

- The state of *v* before *condition_i* is definitely assigned, if the state of *v* after *pattern_i* is "definitely assigned when true".
- The state of *v* before *switch_section_body* is definitely assigned, if the state of *v* after each *switch_section_label* is "definitely assigned when true".
- Otherwise, the state of *v* is not definitely assigned.

#### General definite assignment rules for pattern variables

The following rule applies to any *primary_pattern* *p* that declares a variable, namely *list_pattern*, *declaration_pattern*, *recursive_pattern*, and *var_pattern*, as well as any nested subpatterns.

- The state of *v* after *p* is "definitely assigned when true".

#### Definite assignment rules for pattern variables declared under logical patterns

For a *disjunctive_pattern* *p* of the form `left or right`, the definite assignment state of *v* is determined by:
- The state of *v* before *right* is definitely assigned if and only if the state of *v* after *left* is "definitely assigned when false".
- The state of *v* after *p* is "definitely assigned when true" if the state of *v* after both *left* and *right* is "definitely assigned when true".
- The state of *v* after *p* is "definitely assigned when false" if the state of *v* after either *left* or *right* is "definitely assigned when false".

For a *conjunctive_pattern* *p* of the form `left and right`, the definite assignment state of *v* is determined by:
- The state of *v* before *right* is definitely assigned if and only if the state of *v* after *left* is "definitely assigned when true".
- The state of *v* after *p* is "definitely assigned when true" if the state of *v* after either *left* or *right* is "definitely assigned when true".
- The state of *v* after *p* is "definitely assigned when false" if the state of *v* after both *left* and *right* is "definitely assigned when false".

For a *negated_pattern* *p* of the form `not pattern`, the definite assignment state of *v* after *p* is determined by:
- If the state of *v* after *pattern* is "definitely assigned when true", then the state of *v* after *p* is "definitely assigned when false".
- If the state of *v* after *pattern* is "definitely assigned when false", then the state of *v* after *p* is "definitely assigned when true".

These rules cover the existing top-level `is not` pattern variables. However, in any other scenario the variables could be left unassigned.

## Unresolved questions
- Would it be possible to permit different types for each variable especially within `if`/`else` chains?
  And if so, would it be a part of this proposal or a separate feature? (discussed in LDM 2022/10/17)
