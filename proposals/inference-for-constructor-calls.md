# Type inference for constructor calls

Champion issue: [dotnet/csharplang#9627](https://github.com/dotnet/csharplang/issues/9627)

## Summary
[summary]: #summary

This proposal allows a generic type's type argument list to be omitted in an object creation expression and inferred from the constructor arguments:

```csharp
class Pair<TFirst, TSecond>
{
    public Pair(TFirst first, TSecond second) { }
}

var pair = new Pair("answer", 42); // Pair<string, int>
```

Inference can also use the target type, as defined by [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md).

## Motivation
[motivation]: #motivation

Generic case types in a hierarchy are often constructed through their generic base type:

```csharp
abstract class Option<T> { }
sealed class Some<T>(T value) : Option<T> { }

Option<string> option = new Some("Hello"); // Some<string>
```

Today `Some<string>` must be written explicitly or hidden behind a factory method even though the constructor argument and target contain the required type information.

## Detailed design
[design]: #detailed-design

The normative design is in [MadsTorgersen/csharpstandard#3](https://github.com/MadsTorgersen/csharpstandard/pull/3), which updates [`standard/expressions.md`](https://github.com/dotnet/csharpstandard/blob/alpha-v12/standard/expressions.md), primarily [§12.6.3 Type inference](https://github.com/dotnet/csharpstandard/blob/alpha-v12/standard/expressions.md#1263-type-inference) and [§12.8.17.2 Object creation expressions](https://github.com/dotnet/csharpstandard/blob/alpha-v12/standard/expressions.md#128172-object-creation-expressions).

### Syntax and lookup

Object creation accepts the `type_group` defined by the [type groups proposal](https://github.com/dotnet/csharplang/pull/10310). Lookup may therefore produce accessible same-name types of different arities. An explicitly bound type, including the implied type of `new()`, produces a singleton group.

Lookup is complete before constructors are considered. Constructor binding operates on the resulting group.

### Constructor inference and candidates

For each accessible constructor of each unbound generic type in the group, inference uses:

- the containing type's type parameters;
- the constructor parameter types and corresponding object-creation arguments;
- the constructed containing type as the result type; and
- the target type of the object creation expression, if any.

The shared inference algorithm is defined by the [generalized generic type inference proposal](https://github.com/dotnet/csharplang/pull/10311). Target-derived behavior comes from [target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md).

Inference is performed separately for each constructor. Failure to match arguments to parameters or infer all containing-type arguments removes that constructor from consideration.

After substitution, the constructed containing type and constructed types in the parameter list must satisfy their constraints, and the substituted parameter list must be applicable to the arguments. Constructors of bound types require no inference and are candidates when accessible and applicable. The default constructor of a struct with no declared parameterless constructor participates on the same basis.

### Selection and result

Candidates from every type in the group form one set. A bound non-generic type is not tried before unbound generic types.

Normal overload resolution selects the best constructor using substituted parameter types. For better-function-member rules, a constructor of a generic or non-generic type is treated like a generic or non-generic method, respectively; the containing type's parameters and inferred arguments stand in for method type parameters and arguments.

No applicable candidate or no unique best candidate is a binding-time error. The result type is the type containing the selected constructor, with inferred arguments substituted. Selecting the implicit default constructor of a struct produces the default value of that constructed struct type.

Object and collection initializer contents are processed after constructor selection and do not contribute inference bounds. Dynamically bound object creation continues to require a singleton group containing a bound class or struct type.

## Compatibility

- An accessible same-name generic type can add candidates to an existing object creation, changing the selected constructor or making the expression ambiguous.
- Type-group lookup can find same-arity types that previous arity-specific lookup did not consider together, making lookup ambiguous before constructor selection.
- Target-derived bounds can change an argument-only inference result, allow a new candidate, or conflict with argument-derived bounds.
- Adding a same-name generic type can make dynamic construction invalid because dynamic construction requires a singleton bound type group.

## Alternative: phased fallback

Constructor binding could try an existing bound type first and consider unbound generic types only if that produces no applicable constructor. This would preserve more existing selections, but it would discard candidates from a lookup result that deliberately groups types across arities. This proposal instead performs one selection over the complete group.

## Open questions
[open]: #open-questions

- Should dynamically bound object creation ever support inference for constructors of generic types? The current design does not.
- Should object or collection initializer contents ever contribute inference bounds? The current design selects a constructor first.
