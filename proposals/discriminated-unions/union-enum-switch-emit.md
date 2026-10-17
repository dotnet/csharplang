# Switch Emission for Unions and Data-Carrying Enums

Switch emission is an implementation detail of the [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md) and [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md) designs. The compiler and JIT may change their strategies while preserving the specified API and observable semantics.

## Motivation

Pattern matching should be efficient without requiring a public discriminator or boxing struct cases. This exploration asks how far existing type tests and non-boxing accessors take us, and which compiler or JIT optimizations could improve dispatch. The declaration syntax need not depend on those implementation choices.

## Local Measurements

Preliminary local .NET 11 benchmarks on an M4 Mac indicate that linear dispatch performs well through 32 cases in the tested patterns. These patterns resemble union dispatch, including linear chains of type tests and discriminator comparisons. The results suggest that straightforward lowering is sufficient for initial implementation. Larger case sets may warrant better compiler emission or JIT optimization, but those relatively extreme cases need not block the feature.

These measurements need validation on x64 and other architectures, with varied usage patterns and case distributions, guards, and both switch statements and expressions.

Earlier inspection of generated code with .NET 11 RC1 on ARM64, using 16 sealed class cases and 16 struct cases, found that chains of exact type tests and discriminator predicates remained linear in fully optimized code and Tier 1 with dynamic PGO, even after predicate inlining. Marking struct conversions for inlining removed redundant extraction checks in those tests.

## Candidate Lowerings

The following sketches identify the case before extracting its payload. `E` denotes the enclosing enum or union. They show distinct, unguarded cases and omit fallback handling.

### Reference-Type Cases

For an `enum class`, evaluate the input once as `__case`. For a `union` without the `struct` modifier, evaluate the input and read its stored `Value` once as `__case`. Either can then use a chain of type tests:

```csharp
if (__case is E.C1 c1) { HandleC1(c1.Value); }
else if (__case is E.C2 c2) { HandleC2(c2.Value); }
else if (__case is E.C3 c3) { HandleC3(c3.Value); }
```

Because the declared case types are sealed, the runtime can reduce these `is` tests to method-table pointer comparisons.

### Non-Boxed Cases

With the [non-boxed layout](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md), switch lowering should use typed case-access operations. Reading an object-valued `Value` to inspect a struct case forces boxing and can defeat the purpose of the optimized layout on operations expected to be fast.

Both `enum struct` and `union struct` support non-boxing extraction through `TryGetValue`:

```csharp
var __value = value;
if (__value.TryGetValue(out E.C1 c1)) { HandleC1(c1.Value); }
else if (__value.TryGetValue(out E.C2 c2)) { HandleC2(c2.Value); }
else if (__value.TryGetValue(out E.C3 c3)) { HandleC3(c3.Value); }
```

An `enum struct` can alternatively use its `IsCase` predicates followed by case conversions. Inlining these access operations can expose the internal discriminator comparisons:

```csharp
if (__value._discriminator == 1) { /* C1 body */ }
else if (__value._discriminator == 2) { /* C2 body */ }
else if (__value._discriminator == 3) { /* C3 body */ }
```

## Potential Optimizations

The compiler could select tests that inline efficiently and avoid repeating checks during extraction. The JIT could recognize discriminator comparisons and choose a dense jump table, balanced tree, or linear chain. Runtime support could likewise organize sealed-type tests as a balanced tree or order them using profile data. These are possible optimizations rather than guarantees about current JIT behavior.

Any lowering must evaluate the input once and preserve guards, deconstruction, null handling, observable source ordering, and fallback behavior. For unions of existing types, [union well-formedness](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#well-formedness) and matching rules still apply when the listed types overlap. Dispatch cannot depend on which creation overload was called, and reference values must retain their object identity.

### Runtime Recognition and Alternatives

The [existing switch-recognition pass](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/switchrecognition.cpp#L28) runs after inlining, but handles comparison chains whose successful tests share a destination. Different case bodies stop recognition. Extending it is one possible approach.

If general recognition raises correctness concerns, an attribute in `System.Runtime.CompilerServices` could opt compiler-generated unions and enums into optimization. The contract would require the case represented by a value to remain stable during dispatch. Case tests would have no observable side effects and would agree with extraction. The attribute could identify a struct's discriminator field for direct runtime access. This optional optimization contract is separate from the [metadata required to enforce construction rules](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-common.md#metadata-and-compatibility). Its name and final contract remain undecided. The ordinary `[Union]` marker does not identify a discriminator or provide this stronger contract.

A final fallback is exposing a discriminator and emitting an integral switch, using existing runtime optimizations directly. `enum class` and `union` without `struct` would also need a case-to-discriminator mapping. We would prefer to avoid this: the mapping would become a stable API and ABI contract. Reordering cases or inserting a case anywhere could no longer simply renumber them in declaration order. Case numbering and library evolution would need a durable design. Extraction must still follow each proposal's specified access semantics, including reconstruction from shared storage.
