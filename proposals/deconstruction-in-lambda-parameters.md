# Deconstruction in lambda parameters

Champion issue: <https://github.com/dotnet/csharplang/issues/9848>

## Summary

Lambda parameters may be deconstructed within the parameter list of the lambda expression:

```cs
Action<(int, int)> action = ((a, b)) => { };

Action<(int, int)> action = ((int a, int _)) => { };

Action<(int, SomeRecord)> action = ((a, (b, _))) => { };
```

## Motivation

There has been steady interest in this feature since tuple deconstruction debuted in C# 7. When using LINQ methods, it is natural to bundle multiple variables into a tuple to be used in later stages. Here is one illustration provided by the community for the desired workflow:

```cs
var item = Enumerable
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

Overall, this is a small feature aimed at expanding the goodness started in v7 with deconstructible types, and at reducing recurring papercuts when using tuples.

## Detailed design

Any lambda parameter may be a deconstructing parameter if its type is deconstructible. A deconstructing lambda parameter consists entirely of tuple syntax with either implicitly-typed or explicitly-typed tuple elements, corresponding to whether the lambda is implicitly-typed or explicitly-typed. The tuple syntax must have at least two elements. Parameter modifiers and attributes are not permitted inside or outside the tuple.

A deconstructing tuple element may be one of three operations: variable declaration, discard, or recursive deconstruction. Each of the three operations works the same as it does in a deconstructing assignment or `foreach` iteration variable deconstruction; however, the syntaxes for these operations have some differences in lambda parameter deconstruction.

(Examples of deconstructible types are tuples, positional records, and types with user-defined `Deconstruct` instance or extension methods. This proposal does not change the set of types that the language considers deconstructible.)

### Semantics

A compile-time error is produced in the same way as in a deconstructing assignment or deconstructing `foreach` if the type is not deconstructible with the given arity and any given explicit types.

Code within the body of the lambda may use the declared variable names to access the deconstructed values. Deconstruction is performed before the lambda method body runs. It is performed the same way as in a deconstructing assignment or deconstructing `foreach`, to the extent needed to populate the declared variables, regardless of whether the variables are used in the body of the lambda.

Code within the body of the lambda may assign to the declared variables and take writable references to them. As with by-value lambda parameters, changes may be written to these declared variables, and such changes are not observable outside the lambda.

If any tuple type is deconstructed, including nested deconstructions, the compiler may decide not to introduce a new local variable for each element, but rather to read and write and take references directly to the fields of the tuple type. This does not produce changes which are observable outside the lambda because the tuple was passed by value. In all other cases, it is necessary to declare new local variables to pass to a Deconstruct call.

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

### Method type inference

A deconstructed lambda parameter can be used to infer a tuple type (see [inferred tuple type](#inferred-tuple-type)) for a method type parameter:

```cs
M(((int a, string b)) => { });          // Success: T is (int a, string b)

M(((int a, (string _, byte _))) => { }); // Success: T is (int a, (string, byte))

void M<T>(Action<T> action) { }
```

A deconstructed lambda parameter may also be used to infer individual element types for the tuple:

```cs
M(((int a, string b)) => { });        // Success: T1 is int, T2 is string

void M<T1, T2>(Action<(T1, T2)> action) { }
```

```cs
M(((int a, string b, byte c)) => { }); // Success: T1 is int, T2 is string, T3 is byte

void M<T1, T2, T3>(Action<(T1, (T2, T3))> action) { }
```

Here is an example that combines inferring individual element types for a tuple from the method signature with inferring an additional type type for a method type parameter:

```cs
M(((int a, (string b, byte c))) => { }); // Success: T1 is int, T2 is (string b, byte c)

void M<T1, T2>(Action<(T1, T2)> action) { }
```

A deconstructed lambda parameter may not be used to infer type parameters of types besides tuples. This would require mapping Deconstruct methods back to type parameters and is not expected to be an essential scenario.

```cs
M(((int a, int b)) => { }); // ❌ INVALID

void M<T1, T2>(Action<R<T1, T2>> action) { }

record R<T1, T2>(T1 Prop1, T2 Prop2);
```

### Overload resolution

Deconstructed lambda parameters can resolve ambiguities in overload resolution if there is exactly one candidate that allows the deconstructed lambda parameters to all map to tuple types:

```cs
M(((int a, int b)) => { }); // Succeeds: calls M(Action<(int A, int B)>)

void M(Action<(int A, int B)> action) { }
void M(Action<R> action) { }

record R(int Prop1, int Prop2);
```

This may combine with [method type inference](#method-type-inference). If the deconstructed lambda parameter maps to a type parameter for which a tuple type may be inferred, and no other overload maps the deconstructed lambda parameter to a tuple type, overload resolution succeeds:

```cs
M2(((int a, int b)) => { }); // Succeeds: calls M<(int a, int b)>(Action<T>)

void M2<T>(Action<T> action) { }
void M2(Action<R> action) { }

record R(int Prop1, int Prop2);
```

However, no attempt is made to resolve ambiguities between non-tuple types. A deconstructed lambda parameter may become valid on any parameter type if that type declares a new Deconstruct instance method or if a Deconstruct extension method is imported. This takes a page from target-typed new: `new(...)` expressions also contribute no information to overload resolution for similar reasons.

```cs
M(((int a, int b)) => { }); // ❌ INVALID: Ambiguous invocation

void M(Action<object> action) { }
void M(Action<R> action) { }

record R(int Prop1, int Prop2);
```

### Lambda natural types

An explicitly typed lambda with a deconstructing lambda parameter has a natural type. The deconstructing lambda parameter contributes a tuple type for the corresponding parameter in the lambda natural type as described by [inferred tuple type](#inferred-tuple-type).

Lambda natural types do not make use of lambda parameter names, so the lack of a specified parameter name in a deconstructing lambda parameter is of no consequence.

### Inferred tuple type

A tuple type may be inferred from a deconstructed lambda parameter for [method type inference](#method-type-inference) or [lambda natural types](#lambda-natural-types).

A tuple type is determined for a given deconstructed lambda parameter as follows:

1. Its arity is the arity of the deconstructing tuple syntax.
1. For each deconstructing tuple element:
   1. If the element is a variable declaration, the corresponding tuple type element is of the same type as the variable declaration and has the same name as the variable declaration.¹
   1. If the element is a discard, the corresponding tuple type element is of the same type as the discard and has no name.
   1. If the element is a nested deconstruction, a nested tuple type is formed from the nested deconstruction using the same containing algorithm as for the top level. The corresponding tuple type element is of the resulting nested tuple type and has no name.

The rationale for using the deconstructed variable name as the tuple element name is the same as the rationale for inferring the variable names as tuple element names in the following example:

```cs
var a = 1;
var b = 2;
var x = (a, b);
x.a++;
x.b++;
```

## Specification

TODO

## Expansions

Deconstruction could be allowed in LINQ clauses. Deconstruction in `from` and `let` is tracked by <https://github.com/dotnet/csharplang/issues/8875>.
