# Enhanced switch statements

## Summary
[summary]: #summary

This proposal is an enhancement to the existing switch statement syntax to permit switch-expression-style arms instead of using case statements. This feature is orthogonal to https://github.com/dotnet/csharplang/issues/3037 and can be done independently, but was conceived of as taking https://github.com/dotnet/csharplang/issues/3037's switch expressions and applying them to switch statements.

## Motivation
[motivation]: #motivation

This is an attempt to bring more recent C# design around switch expressions into a statement form, that can be used when there is nothing to return. The existing switch statement construct, while very familiar to C/C++ programmers, has several design choices that can feel dated by current C# standards. These include requiring `break;` statements in each case, even though there is no implicit fall-through in C#, and variable scopes that extend across all cases of the switch statement for variables declared _in_ the body, but not for variables declared in patterns in the case labels. We attempt to modernize this by providing an alternate syntax based on the switch expression syntax added in C# 8.0 and improved with the first part of this proposal.

## Detailed design
[design]: #detailed-design

We enhance the grammar of switch statements, creating an alternate form based on the grammar of switch expressions. This alternate form is not compatible with the existing form of switch statements: you must use either the new form or the old form, but not both.

```cs
var o = ...;
switch (o)
{
    1 => Console.WriteLine("o is 1"),
    string s => Console.WriteLine($"o is string {s}"),
    List<string> l => {
        Console.WriteLine("o is a list of strings:");
        foreach (var s in l)
        {
            Console.WriteLine($"\t{s}");
        }
    }
}
```

We make the following changes to the grammar:

```antlr
switch_block
    : '{' switch_section* '}'
    | '{' switch_statement_arms ','? '}'
    ;

switch_statement_arms
    : switch_statement_arm
    | switch_statement_arms ',' switch_statement_arm
    ;

switch_statement_arm
    : pattern case_guard? '=>' statement_expression
    | pattern case_guard? '=>' block
    ;
```

Unlike a switch expression, an enhanced switch statement does not require the switch_statement_arms to have a best common type or to be target-typed. Whether the arm conditions have to be exhaustive like a switch expression is currently an open design question. Block-bodied statement arms are not required to have the end of the block be unreachable, and while a `break;` in the body will exit the switch arm, it is not required at the end of the block like in traditional switch statements.

## Drawbacks
[drawbacks]: #drawbacks

As with any proposals, we will be complicating the language further by doing these proposals. In particular, we will be adding another syntactic form to switch statements that has some very different semantics to existing forms.

## Alternatives
[alternatives]: #alternatives

https://github.com/dotnet/csharplang/issues/2632: The original issue proposed that we allow C# 8.0 switch expressions as `expression_statement`s. We had a few initial problems with this proposal:

* We're uncomfortable making these a top-level statement without the ability to put more than 1 statement in an arm.
* There's some concern that making an infix expression a top-level statement is not very CSharpy.
* Requiring a semicolon at the end of a switch expression in an expression-statement context feels bad like a mistake, but we also don't want to convolute the grammar in such a way as to fix this issue.

## Unresolved questions
[unresolved]: #unresolved-questions

* Should enhanced switch statement arms be an all-or-nothing choice? ie, should you be able to use an old-style `case` label and a new-style arrow arm in the same statement? This proposal takes the opinion that this should be an exclusive choice: you use one or the other. This enables a debate about the second unresolved question for enhanced switch, whether they should be exhaustive. If we decide that enhanced switch should not be exhaustive, then this debate becomes largely a syntactic question.
  * An important note about modern switch expression arms is that you cannot have multiple `when` clauses with fallthrough, like you can with traditional switch statements today. If this is an all-or-nothing choice, this means that the moment you need multiple when clauses you fall off the rails and must convert the whole thing back to a traditional switch statement.

* Should enhanced switch be exhaustive? Switch expressions, where enhanced switch statements are inspired from, are exhaustive. However, while the exhaustivity makes sense in an expression context where something must be returned in all cases, this makes less sense in a statement context. Until we get discriminated unions in the language, the only times we can be truly exhaustive in C# is when operating on a closed type heirarchy that includes a catch-all case, or we are operating on a value type. And if the user is required to add a catch-all do nothing case, then purpose of exhaustivity has been largely obviated.
