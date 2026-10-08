# Target-typed inference for type patterns

Champion issue: [#9630](https://github.com/dotnet/csharplang/issues/9630)

## Summary
[summary]: #summary

Generic type inference is extended to types in patterns, allowing type arguments to be omitted when they can be inferred from the static type of the pattern input. For instance:

```csharp
abstract record class Base<T>();
sealed record class Derived<T>(T Value) : Base<T>();

int Use(Base<int> input)
{
    if (input is Derived(var value))
    {
        return value; // 'Derived<int>' inferred from the type of 'input'
    }

    return 0;
}
```

Inference is available in declaration, type, positional, and property patterns.

## Motivation
[motivation]: #motivation

Patterns involving generic types can get unwieldy when the type arguments must be repeated, even though the static type of the input already contains enough information to infer them.

This is particularly noticeable in generic hierarchies, where a pattern often tests for a derived type whose type arguments follow directly from those of the input type.

## Detailed design
[design]: #detailed-design

A full draft specification is at [MadsTorgersen/csharpstandard#7](https://github.com/MadsTorgersen/csharpstandard/pull/7).

### Pattern types

Every pattern form that contains a type uses a shared `pattern_type` production, which wraps `type_group`. This covers declaration patterns, type patterns, typed positional patterns, and typed property patterns.

The `type_group` is resolved through the shared type-group machinery. Its bound types are considered directly, while each unbound generic type `C<X₁...Xᵥ>` is considered by applying [generalized type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md) with:

- the type parameters and constraints of `C<X₁...Xᵥ>`;
- empty parameter and argument lists;
- `C<X₁...Xᵥ>` as the result type; and
- the static type of the pattern input as the target type.

Each bound type or successfully inferred constructed type is a candidate only if it is permitted in the containing pattern form and is pattern-compatible with the input type. Candidate selection then proceeds as follows:

- If exactly one candidate remains, it is selected.
- If multiple candidates remain and exactly one came directly from the type group without inference, that candidate is selected.
- Otherwise, the pattern is an error and its type must be specified more fully.

In the example above, resolving `Derived` finds `Derived<T>`. Inference uses `Base<int>` as the target type and `Derived<T>` as the result type, producing the candidate `Derived<int>`. The same inference occurs for a property pattern such as `input is Derived { Value: var value }`.

### Bare `is` compatibility

The existing interpretation of a bare `is` expression is preserved. If the right-hand side of `input is C` resolves to an accessible type, it is treated as an is-type expression and no pattern inference is performed.

If an omitted-argument name only identifies generic types, it does not resolve as an ordinary type. The expression can then be interpreted as an is-pattern, where `pattern_type` resolution may infer the missing type arguments.

## Compatibility
[compatibility]: #compatibility

This proposal makes previously invalid patterns with omitted type arguments valid. It can also make a type name ambiguous when generic types of different arities infer to viable candidates.

Declaration and recursive patterns use the candidate-resolution rules above. In contrast, a bare `is C` that already resolves to an accessible type retains its legacy is-type interpretation and does not perform inference.
