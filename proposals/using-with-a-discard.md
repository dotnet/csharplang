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

[§13.14 The using statement](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/statements.md#1314-the-using-statement) is updated as follows.

```diff
 using_statement
     : 'await'? 'using' '(' resource_acquisition ')' embedded_statement
+    | using_discard_statement
     ;

 resource_acquisition
     : local_variable_declaration
     | expression
     ;

+using_discard_statement
+   : 'await'? 'using' '_' '=' expression ';'
+   ;
```

Removals in ~~strikeout~~, additions in **bold**:

> If the form of *resource_acquisition* is *local_variable_declaration* then the type of the *local_variable_declaration* shall be ~~either `dynamic` or~~ a type that can be implicitly converted to `System.IDisposable`. If the form of *resource_acquisition* is *expression* then this expression shall be implicitly convertible to `System.IDisposable`. **If the form of *using_statement* is *using_discard_statement* then its contained *expression* shall be implicitly convertible to `System.IDisposable`.**

(Removal of "either `dynamic` or" is a spec refactoring which makes the first sentence more obviously parallel to the second sentence. `dynamic` is already included in "a type that can be implicitly converted to `System.IDisposable`.")

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
> ~~has~~ **have** the same three possible expansions. In this case `ResourceType` is implicitly the compile-time type of the *expression*, if it has one. Otherwise the interface `IDisposable` itself is used as the `ResourceType`. The `resource` variable is inaccessible in, and invisible to, the embedded *statement* **(in the case of a *using_statement* which has an embedded statement) or the remainder of the current scope (in the case of a *using_discard_statement*)**.

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

### Conflict with using aliases named `_` in top-level statements

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
