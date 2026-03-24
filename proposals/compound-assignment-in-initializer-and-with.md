# Compound assignment in object initializer and `with` expression

Champion issue: <https://github.com/dotnet/csharplang/issues/9896>

## Summary

Allow compound assignments in an object initializer:

```cs
var timer = new DispatcherTimer
{
    Interval = TimeSpan.FromSeconds(1d),
    Tick += (_, _) => { /*actual work*/ },
};
```

Or a `with` expression:

```cs
var newCounter = counter with { Value -= 1 };
```

## Motivation

It's not uncommon, especially in UI frameworks, to create objects that both have values assigned and need events hooked up as part of initialization. While object initializers addressed the first part with a nice shorthand syntax, the latter still requires additional statements to be made. This makes it impossible to simply create these sorts of objects as a simple declaration expression, preventing their use in expression-bodied members or in nested constructs like collection initializers or switch expressions. Spilling the object creation expression out to a variable declaration statement makes things more verbose for such a simple concept.

The declarative UI story can be made much more complete with a small change to the language. Windows Forms in particular can immediately gain a more appetizing story for dynamic or manual creation of UI controls, both in vanilla form and when using third-party vendor frameworks that build on Windows Forms.

The applies to more than just events though as objects created (esp. based off another object with `with`) may want their initialized values to be relative to a prior or default state.

## Detailed design

### Object initializer design

The existing <https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#object-initializers> will be updated to state:

```diff
 member_initializer
-    : initializer_target '=' initializer_value
+    : initializer_target assignment_operator initializer_value
     ;
```

The spec language will be changed to:

> If an initializer_target is followed by an equals (`'='`) sign, it can be followed by either an expression, an object initializer or a collection initializer.  If it is followed by any other assignment operator it can only be followed by an expression.
>
> If an initializer_target is followed by an equals (`'='`) sign it not possible for expressions within the object initializer to refer to the newly created object it is initializing.  If it is followed by any other assignment operator, the new value will be created by reading the value from the new created object and then writing back into it.
>
> A member initializer that specifies an expression after the assignment_operator is processed in the same way as an [assignment](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#assignment-operators) to the target.

### `with` expression design

The existing [with expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#with-expression) spec will be updated to state:

```diff
member_initializer
-    : identifier '=' expression
+    : identifier assignment_operator expression
    ;
```

The spec language will be changed to:

> First, receiver's "clone" method (specified above) is invoked and its result is converted to the receiver's type. Then, each member_initializer is processed the same way as a corresponding assignment operation [assignment](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#assignment-operators) to a field or property access of the result of the conversion. Assignments are processed in lexical order.

### Design notes

There is no concern that `new X() { a += b }` has meaning today (for example, as a collection initializer). That's because the spec mandates that a collection initializer's element_initializer is:

```antlr
element_initializer
    : non_assignment_expression
    | '{' expression_list '}'
    ;
```

By requiring that all collection elements are `non_assignment_expression`, `a += b` is already disallowed as that is an assignment_expression.

## Open questions

Is this feature needed? For example, users could support some of these scenarios doing something like the following:

```cs
var timer = new DispatcherTimer
{
    Interval = TimeSpan.FromSeconds(1d),
}.Init(t => t.Tick += (_, _) => { /*actual work*/ }),
```

That said, this would only work for non-init members, which seems unfortunate, and it is still clunky compared to the first-class experience of initializing all other members.
