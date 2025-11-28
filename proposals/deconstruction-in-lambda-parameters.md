# Deconstruction in lambda parameters

Champion issue: TODO

## Summary

Lambda parameters may be deconstructed within the parameter list of the lambda expression:

```cs
Action<(int, int)> action = ((a, b)) => { };

Action<(int, int)> action = ((int a, int _)) => { };

Action<(int, (int, int))> action = ((a, (b, _))) => { };
```

## Motivation

There has been steady interest in this feature since tuple deconstruction debuted in C# 7. When using LINQ methods, it is natural to bundle multiple variables into a tuple to be used in later stages. Here is one illustration provided by the community for the desired workflow:

```cs
var r = Enumerable
    .Range(1, 10)
    .Select(i => (i + 1, i * i))
    .Where(((a, b)) => 2 * a < b)
    .OrderBy(((a, b)) => b)
    .Last();
```

Another use case is when passing multiple variables through generic arguments which are provided in order to avoid allocations. Examples are the `TLocal` parameter on [`Parallel.For`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.parallel.for) and [`Parallel.ForEach`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.parallel.foreach), as well as [`ThreadPool.QueueUserWorkItem<TState>`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.threadpool.queueuserworkitem?view=net-10.0#system-threading-threadpool-queueuserworkitem-1(system-action((-0))-0-system-boolean)). The usage pattern would look like:

```cs
ThreadPool.QueueUserWorkItem(
    static ((varA, varB, varC)) => { ... },
    (varA, varB, varC),
    preferLocal: false);
```

In the prior examples, the introduction of tuples was a user decision. However, some APIs (including LINQ's `Zip` method) may always introduce tuples, and sometimes in a nested fashion. The user then has two options. One option is to dot into deeply nested tuples as in `tuple.left.right` below, where the tuple names are chosen by the API rather than by the user and are often less than meaningful. The other option is to add a deconstruction statement to the lambda which may entail converting to a block body:

```cs
var finalProvider = compilationAndGroupedNodesProvider.SelectMany((tuple, cancellationToken) =>
{
    // Then either:
    var ((syntaxTree, syntaxNodes), compilation) = tuple;

    // or:
    ProcessNodes(tuple.Left.Right);
```

Overall, this is a small feature aimed at reducing recurring papercuts when using tuples.

## Detailed design

Deconstruction is enabled for implicitly-typed and explicitly-typed lambda expressions. When deconstructing, tuple syntax is specified in place of one or more of the lambda parameters. The tuple syntax must have at least two elements.

A deconstructing tuple element may be one of three operations: variable declaration, discard, or recursive deconstruction. Each of the three operations works the same as it does in a deconstructing assignment or `foreach` iteration variable deconstruction; however, the syntaxes for these operations have some differences in lambda parameter deconstruction.

### Semantics

Code within the body of the lambda may use the declared variable names to access the deconstructed values. Deconstruction is performed before the lambda method body runs. It is performed the same way as in a deconstructing assignment or deconstructing `foreach`, to the extent needed to populate the declared variables, regardless of whether the variables are used in the body of the lambda.

Code within the body of the lambda may assign to the declared variables and take writable references to them. This closely mirrors ordinary lambda parameters.

It is an error if a declared variable name is the same as a lambda parameter name or the same as another declared variable name.

### Implicit and explicit typing

An implicitly-typed lambda does not specify parameter types (`(a, b) =>`), whereas an explicitly-typed lambda does (`(int a, int b) =>`). Variable declarations and discards in lambda parameters must follow suit, including nested deconstructions.

In an implicitly-typed lambda, types are never specified for variable declarations and discards:

```cs
Action<int, (int, (int, int))> action = (a, (b, (c, _))) => { };
```

Whereas in an explicitly-typed lambda, types are always specified for variable declarations and discards:

```cs
Action<int, (int, (int, int))> action = (int a, (int b, (int c, int _))) => { };
```

`var` is not permitted as a variable declaration type or discard type within the parameter list of either implicitly-typed or explicitly-typed lambdas.

### Discards

When exactly one lambda parameter is named `_`, it is considered a parameter name rather than a discard for backwards compatibility reasons. However, if there is any deconstruction in the lambda parameters, `_` becomes a discard at the top level.

This avoids the confusion that would occur if `_` was able to be referenced in the lambda body, despite there being two discards in the lambda parameter list (one top-level, one nested):

```cs
Action<int, (int, int)> action = (_, (a, _)) => { /* _ is not an identifier here */ };
```

This mirrors the effect of a second top-level discard as in the following example:

```cs
Action<int, (int, int)> action = (_, _) => { /* _ is not an identifier here */ };/
```

We go even further and cause `_` at the top level to be a discard if there is any deconstruction, even without a second discard, because this is not a breaking change for the language and it avoids spreading the dichotomy of `_`-as-discard-or-parameter-name any further:

```cs
Action<int, (int, int)> action = (_, (a, b)) => { /* _ is not an identifier here */ };
```


### Parameter list parentheses

A lambda's parameter list parentheses may not be omitted if any parameter in a lambda expression is deconstructed:

```cs
Action<(int, int)> action = (a, b) => { }; // ❌ INVALID
Action<(int, int)> action = ((a, b)) => { }; // Valid

Action<(int, int)> action = (int a, int b) => { }; // ❌ INVALID
Action<(int, int)> action = ((int a, int b)) => { }; // Valid
```

## Specification

TODO

## Expansions

Deconstruction could be allowed in LINQ clauses. Deconstruction in `from` and `let` is tracked by <https://github.com/dotnet/csharplang/issues/8875>.
