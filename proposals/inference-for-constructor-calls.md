# Type inference for constructor calls

Champion issue: [dotnet/csharplang#9627](https://github.com/dotnet/csharplang/issues/9627)

## Summary
[summary]: #summary

Constructors of generic types can infer the type arguments of their containing type from the constructor arguments and, when present, the target type:

```csharp
class Pair<TFirst, TSecond>
{
    public Pair(TFirst first, TSecond second) { }
}

var pair = new Pair("answer", 42); // Pair<string, int>
```

This puts constructors on a more equal footing with generic factory methods. There should be no need to write a factory method just to get the type inference that method calls already enjoy.

This proposal is the object-creation part of a series. It builds on the sibling proposal for [generalized type inference and target-derived bounds](target-typed-generic-type-inference.md), and on a sibling type-groups proposal (link to be added) that looks up accessible types with the same name across arities. The corresponding normative changes are being developed in [csharpstandard#3](https://github.com/MadsTorgersen/csharpstandard/pull/3).

## Motivation
[motivation]: #motivation

Constructor arguments often say everything needed to construct the type:

```csharp
var pair = new Pair("answer", 42); // Pair<string, int>
```

Sometimes the target says it all:

```csharp
class Box<T>
{
    public Box() { }
}

Box<string> box = new Box();
```

Arguments and target can also contribute different pieces:

```csharp
class C<TElement, TCount> : IEnumerable<TElement>
{
    public C(TCount count) { }

    // IEnumerable<TElement> implementation
}

IEnumerable<string> l = new C(5); // C<string, int>
```

This is particularly helpful when constructing one type in a hierarchy through another:

```csharp
abstract class Option<T> { }
sealed class Some<T>(T value) : Option<T> { }

Option<string> option = new Some("Hello"); // Some<string>
```

That last example is a useful payoff, but not a special case. The feature applies the familiar inference and overload-resolution model of methods to object creation generally.

## Detailed design
[design]: #detailed-design

### Type groups in object creation

Object creation consumes the shared `type_group` abstraction from the sibling type-groups proposal. An explicitly bound type produces a singleton group. A type-group name such as `Pair` can produce accessible same-name types of different arities, such as `Pair`, `Pair<T>`, and `Pair<TFirst, TSecond>`. A target-typed `new()` expression likewise starts with its single implied type.

Lookup determines the group before constructor binding. In other words, lookup exposes the available arities, and then one constructor-binding operation selects from them. This is deliberately similar to method-group lookup followed by overload resolution.

### Inference for each constructor

Inference is attempted separately for every accessible constructor of every unbound generic type in the group. For a type `G<X₁, ..., Xᵥ>` with a constructor

```csharp
G(T₁ p₁, ..., Tₓ pₓ)
```

the inference inputs are:

- the type parameters `X₁, ..., Xᵥ` of `G`;
- the constructor parameter types `T₁, ..., Tₓ`;
- the corresponding object-creation arguments `E₁, ..., Eₓ`;
- the result type `G<X₁, ..., Xᵥ>`; and
- the target type of the object creation, if one exists.

This can be understood as if the constructor were represented by a generic method whose return type is the containing type:

```csharp
G<X₁, ..., Xᵥ> M<X₁, ..., Xᵥ>(T₁ p₁, ..., Tₓ pₓ)
```

and as if the object creation supplied the same arguments and target:

```csharp
M(E₁, ..., Eₓ)
Target t = M(E₁, ..., Eₓ);
```

For example, the `C` construction above is inferred as if from:

```csharp
C<TElement, TCount> M<TElement, TCount>(TCount count);

IEnumerable<string> l = M(5);
```

The generalized inference proposal defines how argument inputs and target-derived result bounds interact. This proposal only supplies the constructor-specific inputs.

Inference for a constructor fails if the supplied arguments cannot first be matched to its parameters, or if inference cannot determine all required containing-type arguments. Such a failure only removes that constructor from consideration.

### Substitution, constraints, and applicability

After inference succeeds, the inferred arguments are substituted for the containing type's parameters. The resulting constructed type and all constructed types in the constructor parameter list must satisfy their constraints, and the substituted parameter list must be applicable to the object-creation arguments. Otherwise that constructor is not a candidate.

Constructors of bound types do not need inference. They contribute candidates under the existing accessibility and applicability rules.

### One combined candidate set

**The design uses one candidate set across all bound and unbound types in the type group. It does not first try a non-generic type and fall back to generic types.**

This is the central selection choice in the proposal. Type-group lookup is method-group-like: it intentionally preserves same-name candidates of different arities for a later binding operation. Once lookup has produced that group, constructor binding should choose the best constructor across the whole group rather than let one arity hide the others procedurally.

All constructors that survive inference, substitution, constraint checking, and applicability form one candidate set. Normal overload resolution then selects the best constructor using the substituted parameter types. For the better-function-member rules, a constructor of a generic type is treated as the generic counterpart of a constructor of a non-generic type, with the containing type's parameters and inferred arguments standing in for method type parameters and arguments.

If there is no applicable candidate, or no unique best candidate, object creation is an error.

### Resulting type

The type of the object creation is the type containing the selected constructor, with inferred type arguments substituted. Thus:

```csharp
var pair = new Pair("answer", 42);
```

has type `Pair<string, int>`, because overload resolution selects a constructor of that constructed type.

The default constructor of a struct with no declared parameterless constructor participates in the same way. If selected, the result is the default value of the inferred constructed struct type.

### Specialized object-creation forms

- A target-typed `new()` already has an implied bound type and therefore uses a singleton type group.
- A constructor initializer already targets a known containing or base type; this feature does not infer a different containing type for it.
- Object and collection initializer contents are processed after constructor selection and do not currently contribute inference bounds.
- Attribute creation, nullable wrapping, anonymous object creation, and default construction through a type parameter keep their specialized bound-type behavior.
- Dynamically bound object creation currently requires the type group to contain exactly one bound class or struct type. Inference for a constructor of a generic type remains a compile-time operation.

### Specification impact

The normative draft updates the C# specification's [`expressions.md` type-inference section](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1263-type-inference) and [`expressions.md` object-creation section](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#128172-object-creation-expressions). The shared lookup and generalized inference algorithms are owned by the sibling proposals; this proposal specifies how object creation consumes them.

## Compatibility

This feature changes both name binding and candidate selection, so it has real compatibility risks:

- Adding an accessible same-name generic type can add constructors to an existing object creation. A different constructor may win, or the expression may become ambiguous.
- Type-group lookup can discover two same-name types of the same arity where existing arity-specific lookup found one. The type group is then ambiguous and object creation fails before constructor selection.
- Target-derived bounds can change an argument-only inference result, make a previously failing candidate succeed, or make inference fail when argument and target bounds conflict.
- Dynamically bound construction requires a singleton bound type group. A same-name generic type can therefore make a previously valid dynamic construction invalid.

The combined candidate set makes the first risk more visible than phased fallback would. That is the cost of giving type groups method-group-like semantics all the way through binding.

## Drawbacks
[drawbacks]: #drawbacks

Object creation becomes sensitive to other accessible same-name types, including types of different arities. The result can also depend on target typing, so inference may be less local than argument-only method inference. Finally, applying method-like overload resolution across constructors declared in different types requires a small constructor-specific interpretation of the better-function-member rules.

## Alternatives
[alternatives]: #alternatives

### Phased fallback

The principal compatibility alternative is to bind an existing non-generic or otherwise bound type first, and consider constructors of unbound generic types only if the first phase has no applicable constructor.

That preserves more existing winners and ambiguities. However, it gives special procedural priority to one member of a type group even though lookup deliberately returned all arities together. It also differs from the method-group intuition that later binding selects the best candidate from the complete lookup result. The current design therefore chooses one combined candidate set.

### Argument-only constructor inference

Inference could use constructor arguments but ignore the target type. That would cover `Pair` but not `Box`, and it would leave useful information on the table for examples such as `C<TElement, TCount>`. This proposal instead uses the common inference mechanism, including target-derived bounds.

### Keep factory methods

The language could continue requiring explicit type arguments on object creation and leave inference to factory methods. That avoids compatibility changes, but perpetuates APIs whose factory methods exist only to recover inference already available to methods.

## Open questions
[open]: #open-questions

### Dynamic support

Should dynamically bound object creation ever support inference for constructors of generic types? The current design says no and requires a singleton bound type group.

### Initializer bounds

Should object or collection initializer contents eventually contribute inference bounds? The current design selects the constructor first and treats initializer processing as a later step.

### Inherited target-type model

What is the final source and ref-kind model for the target type inherited from generalized inference? Constructor inference should consume that common model rather than define a competing one.

### Selection rules for future type-group consumers

Should every future consumer of a type group use one combined selection step, or can a consumer define different arity preference or fallback rules? Object creation chooses combined selection, but the abstraction may eventually be used elsewhere.
