# Block-bodied switch expression arms

## Summary
[summary]: #summary

This proposal is an enhancement to the new switch expressions added in C# 8.0: allowing multiple statements in a switch expression arm. We permit braces after the arrow, and use `break value;` to return a value from the switch expression arm.

## Motivation
[motivation]: #motivation

This addresses a common complaint we've heard since the release of switch expressions: users would like to execute multiple things in a switch-expression arm before returning a value. We knew that this would be a top request after initial release, and this is a proposal to address that. This is not a fully-featured proposal to replace [`sequence expressions`](https://github.com/dotnet/csharplang/issues/377). Rather, it is constrained to just address the complaints around switch expressions specifically. It could serve as a prototype for adding sequence expressions to the language at a later date in a similar manner, but isn't intended to support or replace them.

## Detailed design
[design]: #detailed-design

We allow users to put brackets after the arrow in a switch expression, instead of a single statement. These brackets contain a standard statement list, and the user must use a `break` statement to "return" a value from the block. The end of the block must not be reachable, as in a non-void returning method body. In other words, control is not permitted to flow off the end of this block. Any switch arm can choose to either have a block body, or a single expression body as currently. As an example:

```cs
void M(List<object> myObjects)
{
    var stringified = myObjects switch {
        List<string> strings => string.Join(strings, ","),
        List<MyType> others => {
            string result = string.Empty;
            foreach (var other in others)
            {
                if (other.IsFaulted) return;
                else if (other.IsLastItem) break; // This breaks the foreach, not the switch

                result += other.ToString();
            }

            break result;
        },
        _ => {
            var message = $"Unexpected type {myObjects.GetType()}";
            Logger.Error(message);
            throw new InvalidOperationException(message);
        }
    };

    Console.WriteLine(stringified);
}
```

We make the following changes to the  grammar:

```antlr
switch_expression_arm
    : pattern case_guard? '=>' expression
    | pattern case_guard? '=>' block
    ;

break_statement
    : 'break' expression? ';'
    ;
```

It is an error for the endpoint of a switch expression arm's block to be reachable. `break` with an expression is only allowed when the nearest enclosing `switch`, `while`, `do`, `for`, or `foreach` statement is a block-bodied switch expression arm. Additionally, when the nearest enclosing `switch`, `while`, `do`, `for`, or `foreach` statement is a block-bodied switch expression arm, an expressionless `break` is a compile-time error. When a pattern and case guard evaluate to true, the block is executed with control entering at the first statement of the block. The type of the switch expression is determined with the same algorithm as it does today, except that, for every block, all expressions used in a `break expression;` statement are used in determining the _best common type_ of the switch. As an example:

```cs
bool b = ...;
var o = ...;
_ = o switch {
    1 => (byte)1,
    2 => {
        if (b) break (short)2;
        else break 3;
    }
    _ => 4L;
};
```

The arms contribute `byte`, `short`, `int`, and `long` as possible types, and the best common type algorithm will choose `long` as the resulting type of the switch expression.

## Drawbacks
[drawbacks]: #drawbacks

As with any proposals, we will be complicating the language further by doing these proposals. With this proposal, we will effectively lock ourselves into a design for sequence expressions (should we ever decide to do them), or be left with an ugly wart on the language where we have two different syntax for similar end results.

## Alternatives
[alternatives]: #alternatives

An alternative is the more general-purpose sequence expressions proposal, https://github.com/dotnet/csharplang/issues/377. This (as currently proposed) would enable a more restrictive, but also more widely usable, feature that could be applied to solve the problems this proposal is addressing. Even if we don't do general purpose sequence expressions at the same time as this proposal, doing this form of block-bodied switch expressions would essentially serve as a prototype for how we'd do sequence expressions in the future (if we decide to do them at all), so we likely need to design ahead and ensure that we'd either be ok with this syntax in a general-purpose scenario, or that we're ok with rejecting general purpose sequence expressions as a whole.

## Unresolved questions
[unresolved]: #unresolved-questions

* Should we allow labels/gotos in the body? We need to make sure that any branches out of block bodies clean up the stack appropriately and that labels inside the body are scoped appropriately.

* In a similar vein, should we allow return statements in the block body? The example shown above has these, but there might be unresolved questions around stack spilling, and this will be the first time we would introduce the ability to return from _inside_ an expression.
