# Variable declarations under disjunctive patterns

## Summary  

Allow variable declarations under `or` patterns and across `case` labels in a `switch` section.

## Motivation

This feature would reduce code duplication where we could use the same piece of code if either of patterns is satisfied. For instance:
```cs
if (e is (int x, 0) or (0, int x))
    Use(x);
  
switch (e)
{
    case (int x, 0):
    case (0, int x):
        Use(x);
        break;
}
```
Instead of:

```cs
if (e is (int x1, 0))
    Use(x1);
else if (e is (0, int x2))
    Use(x2);
  
switch (e)
{
    case (int x, 0):
        Use(x);
        break;
    case (0, int x):
        Use(x);
        break;
}
```

## Detailed design

Variables *must* be redeclared under all disjuncitve patterns because assignment of such variables depend on the order of evaluation which is undefined in the context of pattern-matching.

- In a *disjunctive_pattern*, pattern variables declared on one side must be redeclared on the other side.
- In a *switch_section*, pattern variables declared under each case label must be redeclared under every other case label.

In any other case, variable declaration follows the usual scoping rules and is disallowed.

These names can reference either of variables based on the result of the pattern-matching at runtime. Under the hood, it's the same local being assigned in each pattern.

Redeclaring pattern variables is only permitted for variables of the same type.

## Unresolved questions

- How identical these types should be?
- Could we support variable declarations under `not` patterns?
    ```cs
    if (e is not (int x, 0) and not (0, int x))
    ```
- Could we relax the scoping rules beyond pattern boundaries?
    ```cs
    if (e is (int x, 0) || a is (0, int x))
    ```
- Could we relax the redeclaration requirement in a switch section? 
    ```cs
    case (int x, 0) a when Use(x, a): // ok
    case (0, int x) b when Use(x, b): // ok
        Use(x); // ok
        Use(a); // error; not definitely assigned
        Use(b); // error; not definitely assigned
        break;
    ```
