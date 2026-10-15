# Non-Boxed Storage for Unions and Data-Carrying Enums

The `struct` keyword in [`enum struct`](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md) and [`union struct`](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md) selects an opinionated layout that holds generated cases' data inline, avoiding boxing during their ordinary creation and extraction. This proposal describes a shared implementation approach. Each language proposal defines its own case types, construction, conversions, defaults, matching, and equality.

The layout is an implementation detail. Compilers may change it, including to use future runtime capabilities, while preserving the specified API and observable semantics. No particular size, field layout, native ABI, or persistent byte representation is guaranteed.

## Scope

Bare `union` retains the existing object-backed struct representation. `enum class` retains its case-reference representation. Neither uses the inline storage described here.

For a non-ref-like `union struct`, the union protocol's object-valued `Value` property can box an inline case. Typed creation, `HasValue`, and `TryGetValue` avoid that access. Ref-like forms require typed storage for ref-like payloads and remain subject to ordinary ref-safety rules. The union proposal separately identifies the unresolved object-valued `Value` requirement for `ref union struct`.

Existing type-list cases can require reference-backed or hybrid storage. For example, a struct case may overlap an interface case implemented by that struct. Preserving the [union creation-equivalence rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#well-formedness) and the identity of an incoming interface reference can require retaining a box. The non-boxing goal for generated cases does not override these requirements.

Packing and extraction below implement the creation and access operations defined by the two language proposals. They do not introduce additional language conversions or a shared public API.

## Storage Strategy

The simplest layout is a discriminator followed by a separate field for each case type. The containing struct's size is roughly the sum of all case sizes plus the discriminator and padding. Conversions can copy the selected case directly into or out of its field.

Our initial expectation is that this approach would often make inline union and enum types too large to be practical. Even a handful of cases could substantially increase storage and copying costs. This assessment needs performance validation and sign-off from LDM and the runtime team. The compact layout below aims to minimize storage safely with only a small runtime cost for rearranging data during case creation and extraction.

We aim to provide a default that balances space and speed. We are also open to letting authors configure that balance, most likely through attributes, similar to existing .NET layout controls such as [StructLayout](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute). Whether to offer customization, its form and scope, and its availability in the initial feature are all undecided.

### Concurrent Access and Safety

Inline union and enum values follow the existing [atomicity rules for struct reads and writes](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/variables.md#96-atomicity-of-variable-references). Unsynchronized accesses may tear, observing case identity and payload from different writes. Such observations are unspecified and accessors may throw, but must preserve memory and type safety under the usual safety assumptions for payload types and user code. Neither copying nor `readonly` makes a concurrent read atomic.

To preserve memory safety, GC reference locations must be determined by the runtime layout independently of the discriminator, including during assignment. No location may alternate between managed references and unmanaged data, and arbitrary bits must never become references.

Shared reference slots require checked extraction: a reader testing a string case could observe an array from a concurrent write. Extraction must check the reference actually read or use an equivalent safe mechanism. The discriminator alone cannot justify an unchecked cast.

### Conservative Storage

A candidate inline layout uses a discriminator and three shared pools:

1. **Unmanaged data**: A byte region for the [predefined types](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#primitive-types-and-layout-assumptions) below, sized for the greatest combined requirement of any case, including padding and alignment. Cases share the region, with distinct locations for components within each case.
2. **Managed references**: Enough slots for the case containing the most references. A slot is normally `object`, but can be more specific when that type accommodates every value assigned to it, such as a slot used only for strings.
3. **Typed values**: Slots for arbitrary value types and type parameters not known to be reference types. Each type needs enough slots for its maximum count in any case. Distinct types cannot generally share a slot. Reference-constrained type parameters can use the reference pool.

Compatibility of a container's own unmanaged classification is an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#unmanaged-classification).

A `struct` constraint does not rule out references. Even `where T : unmanaged` need not provide a declaration-time byte size, so such parameters can remain in typed slots. The pool maxima can come from different cases, making their combined size greater than any individual case.

### Primitive Types and Layout Assumptions

The compiler may rely on the predefined types' unmanagedness and fixed managed sizes from the [existing `sizeof` rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12818-the-sizeof-operator):

| Types | Size in bytes |
| --- | --- |
| `bool`, `byte`, `sbyte` | 1 |
| `char`, `short`, `ushort` | 2 |
| `int`, `uint`, `float` | 4 |
| `long`, `ulong`, `double` | 8 |
| `decimal` | 16 |

These require no annotation and describe managed storage, rather than interop marshaling. Native-sized integers (`nint`/`IntPtr`, `nuint`/`UIntPtr`) are unmanaged but platform-dependent in size. Portable output must accommodate that, for example through native-sized typed fields. No blanket layout assumption applies to other framework or user-defined structs.

### External Types and Decomposition

In the conservative layout, structs from referenced assemblies remain opaque typed values. The runtime determines their size and GC layout, keeping storage safe if a dependency grows or acquires references. Compile-time `unmanaged` classification is insufficient: C# enforces that constraint but the [CLR does not](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.3/blittable.md#detailed-design). Recompilation against a reference-containing definition rejects an unmanaged-constrained use. Replacing a binary dependency need not reject the instantiation. The [checked packing opt-in](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#possible-checked-packing-opt-in) could permit raw storage for such types.

The compiler can arrange generated cases' declared payload components directly. Decomposing a nested user struct must preserve all value state without invoking user getters, setters, or constructors, and respect accessibility, safety, and supported versioning. Otherwise it remains typed. Being in the current assembly is insufficient when layout dependencies cross assembly boundaries.

Unmanagedness alone does not establish byte layout. Raw packing must account for architecture, alignment, and padding. Reference metadata does not guarantee execution-time layout.

### Mixed-Data Example

These declarations use the same payloads and can use the same storage strategy:

```csharp
enum struct Message<T>
{
    Text(string Content, int Code),
    Buffer(byte[] Data, int Offset, int Count),
    Pair(string First, string Second),
    Timestamp(DateTime Value),
    Values(T First, T Second)
}

union struct Message<T>
{
    case Text(string Content, int Code);
    case Buffer(byte[] Data, int Offset, int Count);
    case Pair(string First, string Second);
    case Timestamp(DateTime Value);
    case Values(T First, T Second);
}
```

The candidate fields below apply to either declaration. Generated case types, construction, and access APIs differ as specified by their respective proposals:

```csharp
// Candidate storage; case types and APIs omitted:
public struct Message<T>
{
    private byte _discriminator;
    private UnmanagedData _unmanaged; // Buffer needs two ints
    private object? _reference0;      // Text, Buffer, or Pair
    private string? _reference1;      // Pair only
    private DateTime _timestamp;      // external struct kept opaque
    private T _value0, _value1;       // Values; T may contain references

    [InlineArray(2)]
    private struct UnmanagedData { private int _element0; }
}
```

### Packing and Extraction

In the shared-pool layout, creation from a case packs components into their assigned slots and sets the discriminator. Extraction checks case identity and reconstructs the value. Mixed layouts require rearranging, or "swizzling," components rather than reinterpreting the shared storage as a complete case. For example, `Buffer` uses `_reference0` and both integers in `_unmanaged`, while `Pair` uses both reference slots. Unused reference and typed slots are defaulted so inactive cases retain no references.

When every case is unmanaged and the required layout facts are established, complete case values can safely overlap in one region sized and aligned for the largest case, with a separate discriminator, avoiding field swizzling.

### Possible Checked Packing Opt-in

An optional packing contract could let a consumer store otherwise opaque structs in raw storage. A compiler-recognized attribute would assert the payload's unmanagedness and fixed byte size, at its declaration or at a case parameter use site:

```csharp
// In a referenced library:
[Unmanaged(size: 8)]
public readonly record struct Point(int X, int Y);

// Consumer assertions can also cover unannotated external types:
enum struct Position
{
    At(Point Point),
    Timestamp([Unmanaged(size: 8)] DateTime Value)
}
```

For either placement, the compiler must verify that the payload is a struct satisfying C#'s unmanaged rules. The annotation is not proof. It may diagnose size mismatches known statically. The name, syntax, and precise placement rules remain open.

For a declaration-site assertion, the compiler prepends runtime checks to the type's static constructor, preserving user initialization:

```csharp
static Point()
{
    __ValidateNoReferences<Point>();
    __ValidateSize<Point>(8);
}

private static void __ValidateNoReferences<T>() where T : struct
{
    if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        throw new InvalidOperationException("The unmanaged reference-containment assertion failed.");
}

private static void __ValidateSize<T>(int expectedSize) where T : struct
{
    if (Unsafe.SizeOf<T>() != expectedSize)
        throw new InvalidOperationException("The unmanaged size assertion failed.");
}
```

Both checks use actual managed representation, not marshaling size. The illustrative helpers can be shared across types. Failure must prevent raw access with a managed exception, such as a type-initialization failure, and raw copies must remain in bounds, as with [`MemoryMarshal.Read` and `Write`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/MemoryMarshal.cs). These guarantees would remove the need for `unsafe`.

A container using raw packing must validate its own recorded claims once per constructed container type through a generated static constructor. Constructors and creation from case values must trigger this before packing, under the [struct static-constructor rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#16410-static-constructors). Extraction must likewise ensure validation has completed before reconstructing payloads from raw storage, including for a designated `default` case. Returning typed `default` payloads or a failed `TryGetValue` result requires no raw access.

The payload initializer alone is insufficient: defaults, arrays, layout queries, and whole-value copies can bypass it. A later library could also remove the annotation or validate a different size. A consumer reserving eight bytes must validate its own eight-byte expectation, rejecting updates that ordinary typed storage could accept.

The `DateTime` assertion uses its current eight-byte managed size. The language does not guarantee that size. These cases could share an eight-byte region, separately from the discriminator. The annotation permits packing without requiring it and establishes only reference containment and total size. Field offsets, alignment, and accessibility need separate justification, or values can be copied opaquely. Adoption remains a joint LDM and runtime-team decision supported by performance measurements.

## Case Dispatch

An inline container can expose a case test backed by its discriminator, allowing the runtime to optimize dispatch after inlining. The enum proposal uses `IsCase` predicates and case conversions:

```csharp
var __value = value;
if (__value.IsC1) { HandleC1(((E.C1)__value).Value); }
else if (__value.IsC2) { HandleC2(((E.C2)__value).Value); }
else if (__value.IsC3) { HandleC3(((E.C3)__value).Value); }
```

The union proposal uses its non-boxing access pattern:

```csharp
var __value = value;
if (__value.TryGetValue(out E.C1 c1)) { HandleC1(c1.Value); }
else if (__value.TryGetValue(out E.C2 c2)) { HandleC2(c2.Value); }
else if (__value.TryGetValue(out E.C3 c3)) { HandleC3(c3.Value); }
```

Inlining exposes the discriminator comparisons:

```csharp
if (__value._discriminator == 1) { /* C1 body */ }
else if (__value._discriminator == 2) { /* C2 body */ }
else if (__value._discriminator == 3) { /* C3 body */ }
```

With additional recognition, the runtime could select a dense jump table, sparse tree, or chain and eliminate redundant extraction checks. These are candidate optimizations, not guarantees about current JIT behavior. The [runtime-recognition discussion](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/non-boxed-union-enum-layout.md#runtime-recognition-and-alternatives) covers possible contracts and the API cost of exposing a discriminator.

The input must be evaluated once. Optimization must preserve guards, deconstruction, null handling, observable source ordering, and fallback behavior. For type-list unions, existing [union well-formedness](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#well-formedness) and matching rules still apply, including when case types overlap. A storage discriminator cannot make matching depend on which creation overload was called. Reference-type cases remain references, preserving the identity of the contained object.

### Performance and Current Runtime Behavior

Earlier local .NET 11 RC1 ARM64 checks of candidate enum dispatch patterns with 16 sealed class cases and 16 struct cases found that exact-type and discriminator-predicate chains remained linear in fully optimized code and Tier 1 with dynamic PGO, even after predicate inlining.

Preliminary M4 Mac benchmarks indicate that linear dispatch performs well and appears sufficient through 32 cases in the tested patterns. Larger case sets may warrant better emit or JIT optimization without blocking initial implementation. Validation on x64 and other architectures, varied usage patterns and case distributions, guards, and both switch statements and expressions remains warranted.

Marking struct conversions for inlining removed redundant extraction checks in the local tests.

### Runtime Recognition and Alternatives

The [existing switch-recognition pass](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/switchrecognition.cpp#L28) runs after inlining, but handles comparison chains whose successful tests share a destination. Different case bodies stop recognition. Extending it is one possible approach.

If general recognition raises correctness concerns, an attribute in `System.Runtime.CompilerServices` could opt compiler-generated unions and enums into optimization. Its contract would cover stable case identity during dispatch, side-effect-free predicates agreeing with the case mapping, and extraction after a successful test. It could identify a struct's discriminator field for direct runtime access. This optional optimization contract is distinct from the required source-semantics marker. Its name and final contract remain undecided. The ordinary `[Union]` marker does not identify a discriminator or provide this stronger contract.

A final fallback is exposing a discriminator and emitting an integral switch, using existing runtime optimizations directly. Reference-backed representations would need a case-to-discriminator mapping too. We would prefer to avoid this: the mapping would become a stable API and ABI contract. Reordering cases or inserting a case anywhere could no longer simply renumber them in declaration order. Case numbering and library evolution would need a durable design. Extraction must still follow each proposal's specified access semantics, including reconstruction from shared storage.

## Open Questions

### Unmanaged Classification

An inline union or enum's unmanaged classification is observable through constraints, pointers, and `stackalloc`, even though its physical representation is an implementation detail. What compatibility guarantee should apply when a compiler changes that representation?

LDM could allow classification to change in either direction, accepting potential source breaks, or require an unmanaged container to remain unmanaged when its cases, payload types, and constraints are unchanged. The latter still allows managed-to-unmanaged changes while restricting future storage choices. Changes to declared cases or payload dependencies are separate library-evolution concerns.
