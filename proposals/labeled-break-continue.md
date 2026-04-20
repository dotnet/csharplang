# Labeled `break` and `continue` Statements

* Championed issue: https://github.com/dotnet/csharplang/issues/9875

## Summary

Allow `break` and `continue` statements to optionally specify a label that identifies which loop or `switch`
statement to target, enabling cleaner control flow in nested constructs without requiring `goto` statements,
or other contortions like nested functions, tuple returns, etc.

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
- **Go**: [Labeled statements](https://go.dev/ref/spec#Labeled_statements)
- **Zig**: [Labelled loops](https://zig.guide/language-basics/labelled-loops/)
- **Dart**: [Loops](https://dart.dev/language/loops)

In all these cases, the languages operate in the saem way as in this specification.  Namely, some constructs can have a
label, and it is possible to reference that label from their respective `continue` or `break` statements.  

## Detailed design

The following updates are presented as a diff against the corresponding sections of the C# 6 standard
([statements.md](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md)).
Throughout this section, ~~strikethrough~~ indicates text being removed from the existing specification,
and **bold** indicates text being added. Unchanged prose is quoted verbatim for context.

### [§12.5 Labeled statements](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#125-labeled-statements)

Insert the following paragraph immediately after the existing paragraph "*A label can be referenced from `goto` statements ([§12.10.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#12104-the-goto-statement)) within the scope of the label.*":

**If the *statement* immediately nested within a *labeled_statement* is a *switch_statement* ([§12.8.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1283-the-switch-statement)) or an *iteration_statement* ([§12.9](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#129-iteration-statements)), the nested statement is said to be *labeled with* the *identifier* of the *labeled_statement*. A *break_statement* ([§12.10.2](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#12102-the-break-statement)) or *continue_statement* ([§12.10.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#12103-the-continue-statement)) can specify such an *identifier* to reference the containing labeled statement.**

***Note**: Only the *statement* that is **immediately** nested within a *labeled_statement* is labeled with that identifier. For example, given `a: b: while (…) …`, only `b` labels the *iteration_statement*; `a` labels the inner *labeled_statement* `b: while (…) …`, which is not itself a *switch_statement* or *iteration_statement*. Consequently, `break a;` or `continue a;` appearing within the loop body does not target the `while` statement. **end note***

### [§12.10.2 The break statement](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#12102-the-break-statement)

```ANTLR
break_statement
    : 'break' identifier? ';'
    ;
```

~~The `break` statement exits the nearest enclosing `switch`, `while`, `do`, `for`, or `foreach` statement.~~

**The `break` statement exits the nearest enclosing *switch_statement* ([§12.8.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1283-the-switch-statement)) or *iteration_statement* ([§12.9](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#129-iteration-statements)), or, if an *identifier* is specified, the nearest enclosing *switch_statement* or *iteration_statement* labeled with that *identifier* (see [§12.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#125-labeled-statements)).**

The target of a `break` statement is the end point of the nearest enclosing ~~`switch`, `while`, `do`, `for`, or `foreach` statement~~ **statement determined as above**. ~~If a `break` statement is not enclosed by a `switch`, `while`, `do`, `for`, or `foreach` statement, a compile-time error occurs.~~ **If no such enclosing statement exists, a compile-time error occurs.**

~~When multiple `switch`, `while`, `do`, `for`, or `foreach` statements are nested within each other, a `break` statement applies only to the innermost statement. To transfer control across multiple nesting levels, a `goto` statement ([§12.10.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#12104-the-goto-statement)) shall be used.~~

A `break` statement cannot exit a `finally` block ([§12.11](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1211-the-try-statement)). When a `break` statement occurs within a `finally` block, the target of the `break` statement shall be within the same `finally` block; otherwise a compile-time error occurs.

A `break` statement is executed as follows:

- If the `break` statement exits one or more `try` blocks with associated `finally` blocks, control is initially transferred to the `finally` block of the innermost `try` statement. When and if control reaches the end point of a `finally` block, control is transferred to the `finally` block of the next enclosing `try` statement. This process is repeated until the `finally` blocks of all intervening `try` statements have been executed.
- Control is transferred to the target of the `break` statement.

Because a `break` statement unconditionally transfers control elsewhere, the end point of a `break` statement is never reachable.

> ***Example**: A labeled `break` resolves to the nearest enclosing *switch_statement* or *iteration_statement* with the matching label:*
>
> ```csharp
> outer: for (int i = 0; i < 10; i++)
> {
>     for (int j = 0; j < 10; j++)
>     {
>         if (i * j > 20)
>             break outer; // exits the outer for-loop
>     }
> }
> ```
>
> ***end example***

### [§12.10.3 The continue statement](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#12103-the-continue-statement)

```ANTLR
continue_statement
    : 'continue' identifier? ';'
    ;
```

~~The `continue` statement starts a new iteration of the nearest enclosing `while`, `do`, `for`, or `foreach` statement.~~ 

**The `continue` statement starts a new iteration of the nearest enclosing *iteration_statement* ([§12.9](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#129-iteration-statements)), or, if an *identifier* is specified, the nearest enclosing *iteration_statement* labeled with that *identifier* (see [§12.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#125-labeled-statements)).**

The target of a `continue` statement is the end point of the embedded statement of the nearest enclosing ~~`while`, `do`, `for`, or `foreach` statement~~ **_iteration_statement_ determined as above**. ~~If a `continue` statement is not enclosed by a `while`, `do`, `for`, or `foreach` statement, a compile-time error occurs.~~ **If no such enclosing statement exists, a compile-time error occurs.**

~~When multiple `while`, `do`, `for`, or `foreach` statements are nested within each other, a `continue` statement applies only to the innermost statement. To transfer control across multiple nesting levels, a `goto` statement ([§12.10.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#12104-the-goto-statement)) shall be used.~~

A `continue` statement cannot exit a `finally` block ([§12.11](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1211-the-try-statement)). When a `continue` statement occurs within a `finally` block, the target of the `continue` statement shall be within the same `finally` block; otherwise a compile-time error occurs.

A `continue` statement is executed as follows:

- If the `continue` statement exits one or more `try` blocks with associated `finally` blocks, control is initially transferred to the `finally` block of the innermost `try` statement. When and if control reaches the end point of a `finally` block, control is transferred to the `finally` block of the next enclosing `try` statement. This process is repeated until the `finally` blocks of all intervening `try` statements have been executed.
- Control is transferred to the target of the `continue` statement.

Because a `continue` statement unconditionally transfers control elsewhere, the end point of a `continue` statement is never reachable.

> ***Example**: A labeled `continue` resolves to the nearest enclosing *iteration_statement* with the matching label:*
>
> ```csharp
> outer: for (int i = 0; i < 10; i++)
> {
>     for (int j = 0; j < 10; j++)
>     {
>         if (ShouldSkip(i, j))
>             continue outer; // continues the outer for-loop
>     }
> }
> ```
>
> ***end example***

## Drawbacks/Alternatives

### Keep using `goto` statements

C# already supports `goto`, which can accomplish the same control flow. However, `goto` has several disadvantages compared to labeled break/continue:

- Requires separate labels for break vs. continue scenarios (break labels go after the loop, continue labels go before the closing brace)
- Label placement is less intuitive and differs based on whether you're breaking or continuing
- Less explicit about intent (jumping to a location vs. breaking/continuing a specific loop)
- Brittle and error-prone: developers must ensure no statements are accidentally placed between labels and their target constructs. For example, with `goto END_LOOP;` followed by `END_LOOP:`, it's easy to inadvertently insert a statement between them during maintenance, breaking the intended control flow. Labeled loops prevent this issue by binding the label directly to the construct.
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

<details>

- [Discussion #6634: C# Break nested loop](https://github.com/dotnet/csharplang/discussions/6634)
- [Issue #869: Discussion: C# Break nested loop](https://github.com/dotnet/csharplang/issues/869)
- [Discussion #5525: [Proposal] Labeled loops like in Java](https://github.com/dotnet/csharplang/discussions/5525)
- [Issue #1597: [Proposal] Labeled loops like in Java](https://github.com/dotnet/csharplang/issues/1597)
- [Discussion #5521: nested loops interruption with break X, continue X](https://github.com/dotnet/csharplang/discussions/5521)
- [Issue #4109: [Proposal]: Syntactic sugar for breaking out of or continuing nested loops](https://github.com/dotnet/csharplang/issues/4109)
- [Issue #3511: [Proposal] "doublecontine", to contine outer loop](https://github.com/dotnet/csharplang/issues/3511)
- [Issue #2024: break and continue inhancements](https://github.com/dotnet/csharplang/issues/2024)
- [Discussion #8434: Chained Control Flow Statements: break [, break]... [,continue]](https://github.com/dotnet/csharplang/discussions/8434)

</details>

## Design meetings

TBD
