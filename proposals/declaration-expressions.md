# Declaration expressions

* [ ] Proposed
* [ ] Prototype
* [ ] Implementation
* [ ] Specification: see below

## Summary
[summary]: #summary

Support declaration assignments as expressions.

## Motivation
[motivation]: #motivation

Allow initialization at the point of declaration in more cases, simplifying code, and allowing `var` to be used.
```c#
SpecialType SpecialType =>
    (var st = type.SpecialType).IsValueType() ? SpecialType.None : st;
```

Extend `out var` declarations to allow `ref` values.
```c#
Convert(source, destination, ref HashSet<Diagnostic> diagnostics = null);
```

## Detailed design
[design]: #detailed-design

Expressions are extended to include declaration assignment. Precedence is the same as assignment.

```antlr
expression
    : non_assignment_expression
    | assignment
    | declaration_assignment_expression // new
    ;
declaration_assignment_expression // new
    : declaration_expression '=' local_variable_initializer
    ;
declaration_expression // C# 7.0
    | type variable_designation
    ;
```

The declaration assignment is of a single local.
```c#
F(var x, y = 2);        // error
F(var (x, y) = (1, 2)); // error
F(var x = (1, 2));      // ok
```

The type of a declaration assignment expression is the type of the declaration.
If the type is `var`, the inferred type is the type of the initializing expression. 

The declaration assignment expression may be an l-value, for `ref` argument values in particular.

If the declaration assignment expression declares a value type, and the expression is an r-value, the value of
the expression is a copy.

The scope of locals declared in declaration assignment expressions is the same the scope of corresponding declaration expressions from C#7.0.

It is a compile time error to refer to a local in text preceding the declaration expression.

## Alternatives
[alternatives]: #alternatives
No change. This feature is just syntactic shorthand after all.

More general sequence expressions: see [#377](https://github.com/dotnet/csharplang/issues/377).

To allow use of `var` in more cases, allow separate declaration and assignment of `var` locals,
and infer the type from assignments from all code paths.

## See also
[see-also]: #see-also
See Basic Declaration Expression in [#595](https://github.com/dotnet/csharplang/issues/595).

See Deconstruction Declaration in the [deconstruction](https://github.com/dotnet/roslyn/blob/master/docs/features/deconstruction.md) feature.
