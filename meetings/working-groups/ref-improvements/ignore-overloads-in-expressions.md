# Ignore overloads with ref struct parameters within Expression lambdas

Ignore overloads with `ref struct` parameters when binding within `Expression` lambdas.

## Motivation

C#13 adds support for *`params` span*, and C# preview adds *first-class span types*. Both of these features allow overload resolution to prefer overloads with *span type* parameters in more cases than with earlier language versions.

However, within an `Expression`, binding to a member that requires a `ref struct` instance may result in a compile-time or runtime error, and is therefore a breaking change.

Example 1: `params` span, see [#110592](https://github.com/dotnet/runtime/issues/110592)

```csharp
Expression<Func<string, string, string>> expr =
    (x, y) => string.Join("", x, y); // String.Join(string?, params ReadOnlySpan<string?>)
```

Example 2: First-class span types, see [#109757](https://github.com/dotnet/runtime/issues/109757)

```csharp
Expression<Func<string[], string, bool>> expr =
    (x, y) => x.Contains(y); // System.MemoryExtensions.Contains<T>(this ReadOnlySpan<T>, T)

var f = expr.Compile(preferInterpretation: true); // Exception
```

## Design

[*12.6.4.2 Applicable function member*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12642-applicable-function-member) could be updated as follows:

> - A static method is only applicable if the method group results from a *simple_name* or a *member_access* through a type.
> - ...
> - **Within an `Expression`, if any parameters of the candidate method, other than the implicit instance method receiver, may have a `ref struct` type, the candidate is not applicable.**

Note that the disqualifying parameter:
- May be a `params` parameter
- May be an optional parameter
- May have a *generic parameter* type with `allows ref struct`

## Drawbacks

Ignoring certain overloads may be a breaking change itself, in limited scenarios where `ref struct` instances are supported within an `Expression` currently. *Examples?*

## Alternatives

### No compiler change; add IDE fixer

Make no additional compiler changes. Instead, require callers to rewrite `Expression` instances to work around the overload resolution changes. An IDE fixer could be provided to rewrite calls for these cases.

### Support `ref struct` arguments within `Expression` lambdas

The [`Expression` interpreter](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1.compile?view=net-9.0#system-linq-expressions-expression-1-compile(system-boolean)) relies on *reflection*, so supporting these cases would require supporting `ref struct` arguments in reflection or rewriting parts of the interpreter.
