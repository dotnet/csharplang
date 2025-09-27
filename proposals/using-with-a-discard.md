# `using` with a discard

Champion issue: <https://github.com/dotnet/csharplang/issues/8606>

## Summary

Permit `using` variable declarations with a discard variable:

```cs
void M()
{
    using _ = GetDisposable();
}
```

## Motivation

This statement form adds the final corner to the following square:

|                  | **New variable**            | **No new variable** |
|------------------|-----------------------------|---------------------|
| **New scope**    | `using (var name = expr) {` | `using (expr) {`    |
| **No new scope** | `using var name = expr;`    | `using _ = expr;` 🆕 |

It has been wildly popular to remove or avoid nesting by preferring the bottom form inside the "New variable" column to the top form. However, there has been no corresponding opportunity in the other column. The absence of this corner has been painfully felt. When a language user doesn't want the extra nesting, but also has no use for the variable, this results in workarounds such as creating variables named `_`, `_1`, `_2`, and so on.

```cs
using var _1 = factory.Lease(out var widget);
using var _2 = widget.BeginScope();
```

Another place where this request comes up is when using ConfigureAwait for `await using`. The ConfigureAwait extension method on IAsyncDisposable returns a wrapper which is only good for disposal, and useless to place in a new variable. Yet users must place it in a new variable, or else they must increase nesting just because they're using ConfigureAwait.

```cs
var stream = await client.GetStreamAsync(...).ConfigureAwait(false);
await using var _3 = stream.ConfigureAwait(false);
```

By using throwaway variable names as seen in community discussions and the Roslyn codebase itself ([one example](https://github.com/dotnet/roslyn/blob/6b2b0e4c7c3e0470c44ad22653996231a013d8e1/src/Workspaces/Remote/Core/AbstractAssetProviderExtensions.cs#L54-L60)), language users are already attempting to use the language as though this feature existed.

## Detailed design

*using_statement* syntax is expanded to include a new *using_discard_statement* syntax, `'await'? 'using' '_' '=' expression ';'` (see [Specification](#specification)).

The lifetime of the resources acquired in a *using_discard_statement*, and the allowed locations for a *using_discard_statement*, are defined in the same way as for C# 8's [`using_declaration` syntax](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/using.md#using-declaration).

This proposal does not create support for multiple discards in a single using statement:

```cs
❌ NOT supported:
using _ = expr1, _ = expr2;
using _ = expr1, expr2;
```

*using_declaration* syntax is not expanded or unified. *using_declaration* is based on *implicitly_typed_local_variable_declaration* and *explicitly_typed_local_variable_declaration* which do not include *simple discards*. A *simple discard* is a special case of a *declaration_expression* ([§12.17](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#1217-declaration-expressions)). (Specification work for *using_declaration* is [ongoing](https://github.com/dotnet/csharpstandard/pull/672/files#diff-20796c21eeccfd2c773f2969d23440cbc04e7439f4777729aad5d602477c232fR2057) at the time of writing.)

## Specification

[§13.14 The using statement](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/statements.md#1314-the-using-statement) is updated as follows.

```diff
 using_statement
     : 'await'? 'using' '(' resource_acquisition ')' embedded_statement
+    | using_discard_statement
     ;

 resource_acquisition
     : non_ref_local_variable_declaration
     | expression
     ;

 non_ref_local_variable_declaration
     : implicitly_typed_local_variable_declaration
     | explicitly_typed_local_variable_declaration
     ;

+using_discard_statement
+    : 'await'? 'using' '_' '=' expression ';'
+    ;
```

Additions in **bold**:

> If the form of *resource_acquisition* is *local_variable_declaration* then the type of the *local_variable_declaration* shall be either `dynamic` or a resource type. If the form of *resource_acquisition* is *expression*, **or the form of *using_statement* is *using_discard_statement* with its constituent *expression*,** then this expression shall have a resource type. If `await` is present, the resource type shall implement `System.IAsyncDisposable`.  A `ref struct` type cannot be the resource type for a `using` statement with the `await` modifier.

Removals in ~~strikeout~~, additions in **bold**:

> ~~A `using` statement of the form:~~ **`using` statements of the other two forms:**
>
> ```csharp
> using («expression») «statement»
> ```
>
> **and:**
>
> ```csharp
> using _ = «expression»;
> ```
>
> ~~has~~ **have** the same possible formulations.

A new subsection is added:

> ### Using discard statements
>
> A *using discard statement* has the same semantics as, and can be rewritten as, the corresponding parenthesized form of the using statement, as follows:
>
> ```csharp
> using _ = «expression»;
> // statements
> ```
>
> is semantically equivalent to
>
> ```csharp
> using («expression»)
> {
>     // statements
> }
> ```
>
> and
>
> ```csharp
> await using _ = «expression»;
> // statements
> ```
>
> is semantically equivalent to
>
> ```csharp
> await using («expression»)
> {
>     // statements
> }
> ```
>
> A using discard statement shall not appear directly inside a `case` label, but, instead, may be within a block inside a `case` label.

## Alternatives

`using expr;` would be a consistent extrapolation from the existing constructs, where you simply remove the curly braces and parentheses and add a semicolon. However, this statement form would conflict with using directives, particularly in top-level statements.

`using _ = expr;` *also* conflicts with using alias directives, but `_` has a strong sense of "discard" and is discouraged as an identifier name. (There is an [open question](#conflict-with-using-aliases-named-_-in-top-level-statements) on this point.)

## Open questions

### Simpler form versus standing out

In most cases, the expression in a using statement will not be of the form `A.B`. It is more common to call a constructor or method. In these common scenarios, there would be very little visual confusion:

```cs
using someLock.BeginScope();
using new SomeResource(...);
```

The compiler could disambiguate by checking whether the expression binds to a namespace or type versus to some other kind of expression. The human reader would only be confused if this feature was used in a misleading way. Even when the underlying syntax is the same, as in the following deliberately-contrived example, it is obvious from context that the first `using` below involves a namespace or type, and that the second `using` does not:

```cs
using System.ComponentModel;

var result = TryXyz();
using result.Value;
```

If this syntax was available, users could *also* write `using _ = expr;` as well in scenarios where they felt it improved clarity. It would work the same way as `using (_ = expr) {` does today. This option optimizes for the user's flexibility in expressiveness. Community feedback is already beginning to show a desire for this flexibility.

Shall we proceed to design the simpler form, allowing `using new SomeResource();` and getting `using _ = new SomeResource();` as a corollary, or shall we design the form that stands out visually with `_ =` as the only form?

### Disallowed top-level expressions

This open question assumes the `using expr;` syntax.

If the top-level expression is a parenthesized expression, this syntax would conflict with `using (expr);` which compiles successfully with a warning about an empty statement. Shall we disallow the expression to be a parenthesized expression?

If the top-level expression is a simple assignment expression, this syntax would conflict in top-level statements with using alias directives: `using existingVariable = expr;`. Shall we disallow the expression to be a simple assignment expression, or disambiguate based on whether expr is a type or namespace?

### Conflict with using aliases named `_` in top-level statements

This open question assumes the `using _ = expr;` syntax.

A legal program may exist as follows:

```cs
using _ = SomeNamespace;

var xyz = new _.SomeType();
```

Disallowing the use of `_` as an identifier has been previously discussed. Is there appetite for a tiny subset of this breaking change, disallowing `_` as an identifier *only* for using aliases and *only* in files with top-level statements?

Other alternatives:

- Disallow usings with discards in top-level statements
- Only permit usings with discards in top-level statements when nested inside a block statement
- Only permit usings with discards in top-level statements when following some other top-level statement
- Always allow both forms, and prefer the existing meaning if the expression binds to a namespace or type, and prefer the new meaning if the expression binds to a value.
