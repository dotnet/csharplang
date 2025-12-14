# Labeled `break` and `continue` Statements

## Summary

Allow `break` and `continue` statements to optionally specify a label that identifies which loop or `switch`
statement to target, enabling cleaner control flow in nested constructs without requiring `goto` statements.

## Motivation

When working with nested loops or loops containing `switch` statements, developers often need to break out
of or continue an outer loop from within an inner context. Currently, there are two primary approaches to
achieve this, both with significant drawbacks:

### Using `goto` statements

```csharp
string foundValue = null;
for (int x = 0; x < xMax; x++)
{
    for (int y = 0; y < yMax; y++)
    {
        foundValue = GetValue(x, y);
        if (foundValue == target)
            goto FOUND;
    }
}
FOUND:
ProcessValue(foundValue);
```

While `goto` works, it requires placing labels after the loop construct and doesn't clearly
communicate the intent to break from a specific loop. For continuing an outer loop, the
approach becomes even more awkward:

```csharp
for (int x = 0; x < xMax; x++)
{
    for (int y = 0; y < yMax; y++)
    {
        if (ShouldSkipRest(x, y))
            goto CONTINUE_OUTER;
    }
    CONTINUE_OUTER: ;
}
```

This pattern is confusing because the label must be placed at the end of the loop body, just
before the closing brace, so that the incrementor and condition checking happen. When both
`break` and `continue` are needed for the same outer loop, two separate labels are required:

```csharp
for (int x = 0; x < xMax; x++)
{
    for (int y = 0; y < yMax; y++)
    {
        if (ShouldSkipRest(x, y))
            goto CONTINUE_OUTER;
        
        if (ShouldExitAll(x, y))
            goto BREAK_OUTER;
    }
    CONTINUE_OUTER: ;
}
BREAK_OUTER:
// Subsequent statements
```

### Using flag variables

```csharp
string foundValue = null;
bool shouldBreak = false;
for (int x = 0; x < xMax; x++)
{
    for (int y = 0; y < yMax; y++)
    {
        foundValue = GetValue(x, y);
        if (foundValue == target)
        {
            shouldBreak = true;
            break;
        }
    }
    if (shouldBreak)
        break;
}
ProcessValue(foundValue);
```

This approach requires additional state management, increases code verbosity, and obscures the control flow intent.

### Proposed solution

With labeled `break` and `continue`, the code becomes clearer and more maintainable:

```csharp
string foundValue = null;
outer: for (int x = 0; x < xMax; x++)
{
    for (int y = 0; y < yMax; y++)
    {
        foundValue = GetValue(x, y);
        if (foundValue == target)
            break outer;
    }
}
ProcessValue(foundValue);
```

The label is placed directly on the loop it identifies, and the break/continue statement explicitly names its target.
For continuing:

```csharp
outer: for (int x = 0; x < xMax; x++)
{
    for (int y = 0; y < yMax; y++)
    {
        if (ShouldSkipRest(x, y))
            continue outer;
    }
}
```

This naturally expresses "continue the outer loop," without the confusion of label placement associated with
`goto`. A single label can be used for both operations:

```csharp
outer: for (int x = 0; x < xMax; x++)
{
    for (int y = 0; y < yMax; y++)
    {
        if (ShouldSkipRest(x, y))
            continue outer;
        
        if (ShouldExitAll(x, y))
            break outer;
    }
}
```

This feature has been requested extensively in the C# community, with discussions dating back decades and
the topic being reintroduced and rerequested continuously.  Similar features exist in several other modern languages:

- **Java**: [Branching Statements (Oracle Tutorial)](https://docs.oracle.com/javase/tutorial/java/nutsandbolts/branch.html)
- **JavaScript**: [Labeled Statement (MDN)](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Statements/label)
- **Kotlin**: [Returns and Jumps](https://kotlinlang.org/docs/returns.html)
- **Swift**: [Control Flow - Labeled Statements](https://docs.swift.org/swift-book/documentation/the-swift-programming-language/controlflow/#Labeled-Statements)
- **Rust**: [Loop Labels](https://doc.rust-lang.org/reference/expressions/loop-expr.html#loop-labels)

## Detailed design

### Grammar changes

The grammar for `break` and `continue` statements is extended to allow an optional identifier:

```diff
 break_statement
-    : 'break' ';'
+    : 'break' identifier? ';'
     ;

 continue_statement
-    : 'continue' ';'
+    : 'continue' identifier? ';'
     ;
```

### Semantic rules

#### Label requirements

When a `break` or `continue` statement includes an identifier:

1. The identifier must refer to a label on a labeled statement that lexically contains the `break` or `continue` statement.

2. For `break` statements, the labeled statement must be one of:
   - An iteration statement (`for`, `foreach`, `while`, or `do` statement)
   - A `switch` statement

3. For `continue` statements, the labeled statement must be an iteration statement (`for`, `foreach`, `while`, or `do` statement).

4. It is a compile-time error if the identifier does not refer to a label in scope.

5. It is a compile-time error if the label refers to a statement that does not meet the requirements above.

#### Updated `break` statement semantics

The existing semantics of the `break` statement are updated as follows:

```diff
 A break statement exits the nearest enclosing switch statement,
-while statement, do statement, for statement, or foreach statement.
+while statement, do statement, for statement, or foreach statement,
+or, if an identifier is specified, the labeled statement identified
+by that label.
+
+When an identifier is specified, the labeled statement must be a
+switch statement or iteration statement that lexically contains the
+break statement.

 The target of a break statement is the end point of the nearest
-enclosing switch statement, while statement, do statement, for statement,
-or foreach statement.
+enclosing construct (as determined above).
```

#### Updated `continue` statement semantics

The existing semantics of the `continue` statement are updated as follows:

```diff
 A continue statement starts a new iteration of the nearest enclosing
-while statement, do statement, for statement, or foreach statement.
+while statement, do statement, for statement, or foreach statement,
+or, if an identifier is specified, the labeled iteration statement
+identified by that label.
+
+When an identifier is specified, the labeled statement must be an
+iteration statement that lexically contains the continue statement.

 The target of a continue statement is the end point of the embedded
-statement of the nearest enclosing while statement, do statement, for
-statement, or foreach statement.
+statement of the enclosing construct (as determined above).
```

### Behavior

A `break` statement with a label behaves exactly as if it were an unlabeled `break` statement directly
within the labeled construct. Similarly, a `continue` statement with a label behaves as if it were an
unlabeled `continue` directly within the labeled iteration statement.

For example, these two code fragments are semantically equivalent:

```csharp
// With labeled break
outer: for (int i = 0; i < 10; i++)
{
    for (int j = 0; j < 10; j++)
    {
        if (i * j > 20)
            break outer;
    }
}
```

```csharp
// Equivalent using goto
for (int i = 0; i < 10; i++)
{
    for (int j = 0; j < 10; j++)
    {
        if (i * j > 20)
            goto END_OUTER;
    }
}
END_OUTER: ;
```

And for `continue`:

```csharp
// With labeled continue  
outer: for (int i = 0; i < 10; i++)
{
    for (int j = 0; j < 10; j++)
    {
        if (ShouldSkip(i, j))
            continue outer;
    }
}
```

```csharp
// Equivalent using goto
for (int i = 0; i < 10; i++)
{
    for (int j = 0; j < 10; j++)
    {
        if (ShouldSkip(i, j))
            goto CONTINUE_OUTER;
    }
    CONTINUE_OUTER: ;
}
```

## Drawbacks/Alternatives

### Keep using `goto` statements

C# already supports `goto`, which can accomplish the same control flow. However, `goto` has several disadvantages compared to labeled break/continue:

- Requires separate labels for break vs. continue scenarios (break labels go after the loop, continue labels go before the closing brace)
- Label placement is less intuitive and differs based on whether you're breaking or continuing
- Less explicit about intent (jumping to a location vs. breaking/continuing a specific loop)
- Carries historical stigma that labeled break/continue avoids

### Use flag variables

As shown in the motivation section, flag variables work but add significant boilerplate and obscure the control flow logic.

### Use `break N` or `continue N` with numeric levels

- Fragile during refactoring (adding/removing a loop level requires updating all numeric references)
- Harder to read (must count levels to understand the target)
- Less explicit than named labels
- Lack of clarity (1-based? 0-based?)

### Refactor into separate methods

While this is often good practice, it's not always feasible or appropriate, and sometimes introduces unnecessary complexity for what should be simple control flow.

## Related discussions and issues

This proposal consolidates and addresses the following community discussions:

- [Discussion #6634: C# Break nested loop](https://github.com/dotnet/csharplang/discussions/6634)
- [Issue #869: Discussion: C# Break nested loop](https://github.com/dotnet/csharplang/issues/869)
- [Discussion #5525: [Proposal] Labeled loops like in Java](https://github.com/dotnet/csharplang/discussions/5525)
- [Issue #1597: [Proposal] Labeled loops like in Java](https://github.com/dotnet/csharplang/issues/1597)
- [Discussion #5521: nested loops interruption with break X, continue X](https://github.com/dotnet/csharplang/discussions/5521)
- [Issue #4109: [Proposal]: Syntactic sugar for breaking out of or continuing nested loops](https://github.com/dotnet/csharplang/issues/4109)
- [Issue #3511: [Proposal] "doublecontine", to contine outer loop](https://github.com/dotnet/csharplang/issues/3511)
- [Issue #2024: break and continue inhancements](https://github.com/dotnet/csharplang/issues/2024)
- [Discussion #8434: Chained Control Flow Statements: break [, break]... [,continue]](https://github.com/dotnet/csharplang/discussions/8434)

## Design meetings

TBD
