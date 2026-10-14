# Discriminated Unions and Enhanced Enums for C#

This proposal introduces enhanced enums as an elegant way to build discriminated unions in C#. It is a follow-up to the [type unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) proposal. While type unions combine existing types, enhanced enums define the enum and its named, data-carrying cases together, providing familiar, concise syntax for algebraic sum types where cases are known at declaration time.

It consolidates design feedback from many years of repository discussions, especially [#113](https://github.com/dotnet/csharplang/issues/113) and related issues.

<details>
  <summary>Key discussion threads...</summary>

  - [#75](https://github.com/dotnet/csharplang/issues/75) - Early comprehensive discussion exploring DU syntax options including enum class
  - [#2962](https://github.com/dotnet/csharplang/discussions/2962) - Major discussion on Andy Gocke's DU proposal, debating enum class vs enum struct
  - [#7016](https://github.com/dotnet/csharplang/issues/7016) - Fast, efficient unions proposal focusing on struct-based implementations
  - [#3760](https://github.com/dotnet/csharplang/discussions/3760) - Community "shopping list" of desired discriminated union features
  - [#7544](https://github.com/dotnet/csharplang/issues/7544) - Simple encoding of unions exploring type unions vs tagged unions
  - [#9010](https://github.com/dotnet/csharplang/discussions/9010) - "Closed" enum types that guarantee exhaustiveness
  - [#7854](https://github.com/dotnet/csharplang/issues/7854) - Exhaustiveness checking for ADT patterns using private constructors
  - [#8942](https://github.com/dotnet/csharplang/discussions/8942) - Track subtype exhaustiveness for closed hierarchies
  - [#8926](https://github.com/dotnet/csharplang/discussions/8926) - Extensive discussion on Option<T> as canonical DU use case
  - [#7010](https://github.com/dotnet/csharplang/discussions/7010) - Union types discussion heavily featuring Option<T> and Result<T>
  - [#274](https://github.com/dotnet/csharplang/discussions/274) - Java-style class-level enums with methods and constructors
  - [#8987](https://github.com/dotnet/csharplang/discussions/8987) - Champion "permit methods in enum declarations"
  - [#5937](https://github.com/dotnet/csharplang/discussions/5937) - Smart Enums In C# Like Java" (extra state!)
  - [#782](https://github.com/dotnet/csharplang/discussions/782) Sealed enums (completeness checking in switch statements)
  - [#2669](https://github.com/dotnet/csharplang/discussions/2669) Feature request: Partial enums
</details>

## 1. Declarations and Type Model

Use `enum class` for a reference type or `enum struct` for a value type, with named cases that can carry data:

```csharp
public enum class Result
{
    Success(string value),
    Failure(int code)
}

public enum struct Option<T>
{
    None,
    Some(T value)
}
```

Storage layout and switch emit are compiler implementation details. They can, and likely will, change, including to use new runtime capabilities, while preserving the specified API and observable semantics. No particular size, field layout, native ABI, or persistent byte representation is guaranteed. Candidate strategies are discussed under [Implementation Details](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#6-implementation-details).

### `enum class` Translation

An enum class is an abstract record base with sealed nested case records. Its values hold the actual case reference:

```csharp
// Conceptual type model for Result:
public abstract record class Result
{
    private Result() { }

    public sealed record class Success(string value) : Result;
    public sealed record class Failure(int code) : Result;
}
```

Only the declared cases may derive from the enum, including across assembly boundaries. The private constructor alone does not enforce this: ordinary records also have copy constructors that permit derivation. Consuming compilers must enforce the restriction through the [enum metadata](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#case-construction-expressions), along the lines of the [closed-hierarchy proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/closed-hierarchies.md#lowering).

### `enum struct` Translation

An enum struct contains its active case's data inline, with separate nested record-struct case types:

```csharp
// Conceptual type model for Option<T>:
public struct Option<T>
{
    public record struct None;
    public record struct Some(T value);

    // Storage and generated enum APIs omitted.
}
```

For both forms, case types are public, with effective accessibility bounded by the enum. They use its type parameters and constraints and can be named in signatures, casts, patterns, and `typeof`. Cases inherit the corresponding record behavior, except for the special [construction rules](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#case-construction-expressions). The sketches describe the type relationships; compiler-generated constructors and factories are implementation machinery.

### Declaration Rules

Enhanced enums cannot specify a base type or assign constant values to cases. The `= default` exception is described under [Default Values](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#default-values).

After the cases, a semicolon can introduce instance methods, properties, indexers, and events without additional instance state, as well as static members and nested types:

```csharp
enum class Result<T>
{
    Success(T value),
    Error(string message);

    public T GetValueOrDefault(T fallback) => this switch
    {
        Success(var value) => value,
        _ => fallback
    };
}
```

The formal syntax appears in [Appendix A](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#appendix-a-grammar-changes).

## 2. Construction and Case Access

### Case Construction Expressions

Ordinary enums use `Color.Red` to obtain a value. Enhanced enums extend this form to `E.Case(arguments)` for data-carrying cases:

```csharp
var option = Option<int>.Some(42); // Option<int>
option = Option<int>.None;
```

A case without a parameter list is a value expression in a value context. A case with a parameter list is invoked with the corresponding arguments, including `()` for a declaration such as `Empty()`. Argument binding follows the generated case constructor's rules. Either form produces an active case with the specified payload and has the enclosing enum type.

Object-creation expressions targeting an enhanced enum or a generated case type are errors, including explicit and target-typed `new`, with or without arguments:

```csharp
var invalid = new Option<int>.Some(42); // error
Option<int>.Some alsoInvalid = new(42); // error
Option<int> stillInvalid = new();       // error
```

A required compiler-recognized metadata marker identifies the enum, its cases, and their construction operations. Consuming C# compilers apply these rules across assembly boundaries. The marker could be an attribute; its name, form, and encoding remain open.

Conceptually, construction creates the case record and converts it to the enum. [Contextual construction](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#5-contextual-case-construction-and-inference) also permits `.None` and `.Some(arguments)` when the enum type is known.

### Generated APIs and Conversions

Class cases use ordinary reference conversions and type tests, including `as`.

For structs, the proposed minimum API adds:

- An implicit conversion from each case type to the enum.
- A boolean `IsCase` property for each case, such as `IsSome`, testing case identity.
- `HasValue`, which is false when uninitialized and true for any active case, including a designated default case.
- A `TryGetValue(out Case value)` overload for each case, returning true with its value if active; otherwise returning false and assigning `default(Case)` to the out parameter.
- An explicit conversion to each case type, returning its value if active or throwing `InvalidCastException` otherwise.

These operations do not require boxing. For example:

```csharp
Option<int> option = Option<int>.Some(42);
if (option.TryGetValue(out Option<int>.Some some))
{
    Console.WriteLine(some.value);
}
Option<int>.Some extracted = (Option<int>.Some)option;
```

The `as` operator does not invoke struct conversions; conditional extraction uses `TryGetValue` or a [case pattern](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#case-pattern-matching).

User members conflicting with generated members are errors, except for customization permitted by ordinary record rules. Final generated API names remain open.

### Default Values

For an enum class, `default(E)` is `null` and represents no case.

For an enum struct, discriminator zero denotes an *uninitialized value* unless a default case is designated. Ordinary cases have nonzero discriminators, including data-free cases. For the earlier `Option<T>` declaration:

```csharp
Option<int> uninitialized = default;   // no active case
Option<int> none = Option<int>.None;   // active None
Option<int> some = Option<int>.Some(0); // active Some with payload 0
```

An author may designate one data-free case as the default:

```csharp
enum struct Option<T>
{
    None = default,
    Some(T value)
}

Option<int> option = default;
bool isNone = option is Option<int>.None; // true
bool hasValue = option.HasValue;          // true
```

The designation is permitted only on an enum struct case without a parameter list, at most once per declaration. That case has discriminator zero; constructing or converting it produces `default(E)` without a payload. These rules also govern zero-initialized fields and array elements, independently of constructor execution. Apart from the meaning of zero, numeric discriminator values are implementation details.

## 3. Value Semantics

### Readonly, Copying, and `with`

An unmodified enum struct is mutable; `readonly enum struct` follows the ordinary [readonly struct rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1622-struct-modifiers). Its generated cases follow [record-struct property rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#properties): mutable enums generate mutable cases with `get`/`set` payload properties, and readonly enums generate readonly cases with `get`/`init` properties. User-declared nested types follow ordinary nested-type rules. Generated queries, extraction methods, and equality members are readonly instance members.

Assignment copies a reference for enum classes and the case identity and payload for enum structs. Copies are shallow. Extracting a struct case produces an independent value; changing it does not update the enum:

```csharp
enum struct Progress { Waiting, Running(int count) }

Progress progress = Progress.Running(1);
Progress.Running running = (Progress.Running)progress;
running.count = 2; // progress still contains count = 1
progress = running with { count = 3 }; // update a case copy, then convert back
```

Case types support ordinary record `with` expressions. On an enum-class receiver, `e with { }` clones the runtime case record, preserving case identity. On an enum struct, the ordinary [struct `with` rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#allow-with-expression-on-structs) copy its active case or uninitialized state. Initializers bind against the receiver's static type, so case-specific payload updates require a case-typed receiver.

### Equality and Hashing

Class cases use record equality, including case type identity. Struct enum equality requires the same discriminator and, for an active case, equality of the corresponding case records. Payload comparisons follow ordinary record rules, including `EqualityComparer<T>.Default`. Unused storage and padding do not participate.

Both enum forms expose strongly typed `Equals`, `IEquatable<E>`, `Equals(object)`, and `==`/`!=` for the enum type `E`. Struct hashing follows the active case's record hashing and may incorporate case identity; hash values and combining algorithms are implementation details.

Two uninitialized values of the same enum struct are equal, have equal hashes, and are unequal to any active case. A designated default case participates as that active case. Class null comparisons follow ordinary record reference equality.

### Recursive Payloads

Enum structs obey the existing [cyclic instance-field dependency rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1642-value-semantics). A case payload cannot create a direct or indirect recursive value-type layout, regardless of the storage strategy:

```csharp
enum struct Expr
{
    Number(int value),
    Add(Expr left, Expr right) // error
}
```

Using `enum class`, or introducing a class or array payload, breaks the layout dependency through reference indirection. Another value type does not itself provide indirection.

### Concurrent Access

Enum structs follow the existing [atomicity rules for struct reads and writes](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/variables.md#96-atomicity-of-variable-references). Unsynchronized accesses may tear, observing case identity and payload from different writes. Such observations are unspecified and accessors may throw, but must preserve memory and type safety under the usual safety assumptions for payload types and user code. Neither copying nor `readonly` makes a concurrent read atomic.

## 4. Matching and Evolution

### Case Pattern Matching

Class cases use reference type patterns and record deconstruction. For structs, a type or declaration pattern naming a declared case tests case identity and extracts its value without boxing. Property and positional patterns naming that case inspect the extracted value; patterns on the enum's own members inspect the enum.

```csharp
int GetOrDefault(Option<int> option, int fallback) => option switch
{
    Option<int>.Some(var value) => value,
    Option<int>.None => fallback,
    _ => fallback // uninitialized, or a future case
};
```

Struct case patterns require an input statically known to be the enum type; they do not establish CLR inheritance. Boxing the enum boxes the enum itself, so a case type test on an `object` input does not extract a case. Data-free cases match by type even if record operations produce another instance.

Extraction preserves declared payload types and nullability. `Option<string?>.Some(null)` remains an active case. Construction, successful case tests, and successful `TryGetValue` calls establish an active struct case. The `null` pattern matches a null class reference; whether it also matches an uninitialized enum struct remains open.

### Exhaustiveness

The [open/closed enum policy](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md) governs case-set evolution. Open enums allow future cases and require a fallback for unknown cases. Closed enums promise a fixed case set and allow exhaustive matching without a fallback.

Coverage must also account for the no-case state independently of payload nullability. A possibly-null class input needs a `null` arm or fallback. A struct without a designated default case needs to handle uninitialized values, for example with `{ HasValue: false }` or a fallback, unless analysis establishes an active case. Covering a designated default case covers zero-initialized values.

#### Known-Case Coverage with a Future-Case Fallback

A plain fallback hides omissions of known cases. We would like an opt-in fallback intended for future cases, following Swift's [`@unknown default`](https://github.com/swiftlang/swift-evolution/blob/main/proposals/0192-non-exhaustive-enums.md#unknown).

It would match all remaining values at runtime, but the compiler would warn when preceding patterns do not fully cover a case known at compilation. Guarded or partial payload patterns need not cover an entire case. Recompiling after a library adds a case would prompt review while retaining a working fallback. The syntax or mechanism remains an LDM decision.

### Library Evolution

Renaming or removing public cases, changing payload parameters or properties, or switching between class and struct representations affects existing APIs. Changing the designated default case changes the meaning of zero-initialized storage and is a behavioral breaking change. Adding a struct case can increase storage or alignment requirements.

Consumers embedding numeric case tags must retain compatible mappings across supported updates; alternatively, tests must obtain them from the defining type. Exposing a discriminator for switch lowering would make that mapping a stable API and ABI contract: reordering cases or inserting one anywhere could no longer simply renumber them in declaration order. Storage tags are otherwise implementation details. Previously compiled switches retain their normal fallback or unmatched-switch behavior for newly added cases.

## 5. Contextual Case Construction and Inference

Enhanced enums should compose with [target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md) and the proposed inference improvements:

- [Generalized generic type inference](https://github.com/dotnet/csharplang/pull/10311), letting target types contribute bounds alongside arguments.
- [Constructor-call inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md), tracked by [#9627](https://github.com/dotnet/csharplang/issues/9627), including the [updated proposal](https://github.com/dotnet/csharplang/pull/10312) and its [type-group lookup](https://github.com/dotnet/csharplang/pull/10310) foundation.
- [Type-pattern inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns.md).

### Use the Expected Enum Type

Leading-dot lookup uses the expected enum type to bind case construction as well as ordinary static access:

```csharp
void UseInt(Option<int> value) { /* ... */ }

UseInt(.None);
UseInt(.Some(42));
Option<int> Empty() => .None;
```

Target-typed patterns similarly use the input type, permitting `.Some(var value)` and `.None` in the earlier switch. Type-pattern inference separately addresses naming generic case types with omitted arguments.

The expected type also supplies generic arguments absent from the chosen payload:

```csharp
closed enum class Result<T, E> { Ok(T value), Error(E error) }

Result<U, E> Map<T, U, E>(Result<T, E> result, Func<T, U> mapper) => result switch
{
    .Ok(var value) => .Ok(mapper(value)),
    .Error(var error) => .Error(error) // target supplies U although Error has no U payload
};
```

### Infer Generic Factory Results from Context

Generalized inference can supply factory type arguments from a target:

```csharp
static class Option
{
    public static Option<T> CreateNone<T>() => Option<T>.None;
}

Option<int> empty = Option.CreateNone(); // target supplies T = int
UseInt(Option.CreateNone());             // parameter supplies T = int
UseInt(.CreateNone());                   // also omit the factory qualifier
Func<Option<int>> factory = Option.CreateNone; // delegate result supplies T
```

`.CreateNone()` uses [factory-container lookup](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#factory-containers) on the nongeneric sibling `Option`; these factories need not be generated by an enum. Arguments and targets, including delegate results, contribute bounds subject to ordinary constraints, applicability, conversions, and overload selection. The contexts and binding stages supplying target information remain part of the inference design.

### Limits

Another argument can determine a method type parameter and thereby provide the case argument's target:

```csharp
void Use<T>(Option<T> value, T fallback) { /* ... */ }

Use(.None, 42);    // T = int from the second argument
Use(.Some(42), 0); // T = int from the second argument
```

`var value = .None` has no target and is an error. A context-dependent case name alone cannot infer an unknown `T`. Nor does a case name disambiguate overloads taking different enum types: target-typed member access does not select overloads by member existence. Qualification or explicit type arguments remain available.

Inference of omitted containing-type arguments in `Option.Some(42)` remains open; leading-dot lookup instead obtains `Option<int>` from a target.

## 6. Implementation Details

Data-free class cases use shared instances. Case construction can use generated constructors or factories with distinct metadata names, avoiding a conflict with the nested case type's name.

### Struct Storage

A conservative candidate aims for compact, non-boxing storage. Its size, packing, and copying costs require performance validation; minimum size is not a requirement.

#### Safety Requirements

To uphold the [concurrent-access guarantees](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#concurrent-access), GC reference locations must be determined by the runtime layout independently of the discriminator, including during assignment. No location may alternate between managed references and unmanaged data, and arbitrary bits must never become references.

Shared reference slots require checked extraction: a reader testing a string case could observe an array from a concurrent write. Extraction must check the reference actually read, or use an equivalent safe mechanism; the discriminator alone cannot justify an unchecked cast.

#### Conservative Storage

One candidate uses a discriminator and three shared pools:

1. **Unmanaged data**: A byte region for the [predefined types](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#primitive-types-and-layout-assumptions) below, sized for the greatest combined requirement of any case, including padding and alignment. Cases share the region; components within a case occupy distinct locations.
2. **Managed references**: Enough slots for the case containing the most references. A slot is normally `object`, but can be more specific when that type accommodates every value assigned to it, such as a slot used only for strings.
3. **Typed values**: Slots for arbitrary value types and type parameters not known to be reference types. Each type needs enough slots for its maximum count in any case; distinct types cannot generally share a slot. Reference-constrained type parameters can use the reference pool.

A `struct` constraint does not rule out references. Even `where T : unmanaged` need not provide a declaration-time byte size, so such parameters can remain in typed slots. The pool maxima can come from different cases, making their combined size greater than any individual case.

#### Primitive Types and Layout Assumptions

The compiler may rely on the predefined types' unmanagedness and fixed managed sizes from the [existing `sizeof` rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12818-the-sizeof-operator):

| Types | Size in bytes |
| --- | --- |
| `bool`, `byte`, `sbyte` | 1 |
| `char`, `short`, `ushort` | 2 |
| `int`, `uint`, `float` | 4 |
| `long`, `ulong`, `double` | 8 |
| `decimal` | 16 |

These require no annotation and describe managed storage, rather than interop marshaling. Native-sized integers (`nint`/`IntPtr`, `nuint`/`UIntPtr`) are unmanaged but platform-dependent in size; portable output must accommodate that, for example through native-sized typed fields. No blanket layout assumption applies to other framework or user-defined structs.

#### External Types and Decomposition

By default, external structs remain opaque typed values. The runtime determines their size and GC layout, keeping storage safe if a dependency grows or acquires references. Compile-time `unmanaged` classification is insufficient: C# enforces that constraint but the [CLR does not](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.3/blittable.md#detailed-design). Recompilation against a reference-containing definition rejects an unmanaged-constrained use; replacing a binary dependency need not reject the instantiation. The [checked packing opt-in](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#possible-checked-packing-opt-in) could permit raw storage for such types.

The compiler can arrange generated cases' declared payload components directly. Decomposing a nested user struct must preserve all value state without invoking user getters, setters, or constructors, and respect accessibility, safety, and supported versioning. Otherwise it remains typed. Being in the current assembly is insufficient when layout dependencies cross assembly boundaries.

Unmanagedness alone does not establish byte layout. Raw packing must account for architecture, alignment, and padding; reference metadata does not guarantee execution-time layout.

#### Mixed-Data Example

```csharp
enum struct Message<T>
{
    Text(string text, int code),
    Buffer(byte[] data, int offset, int count),
    Pair(string first, string second),
    Timestamp(DateTime value),
    Values(T first, T second)
}

// Candidate storage; case types and APIs omitted:
public struct Message<T>
{
    private byte _discriminator;
    private UnmanagedData _unmanaged; // Buffer needs two ints
    private object? _reference0;      // Text, Buffer, or Pair
    private string? _reference1;      // Pair only
    private DateTime _timestamp;      // external struct kept opaque
    private T _value0, _value1;       // Values; T may contain references

    [InlineArray(8)]
    private struct UnmanagedData { private byte _element0; }
}
```

#### Packing and Extraction

Case-to-enum conversion packs components into their assigned slots and sets the discriminator. Extraction checks case identity and reconstructs the value. Mixed layouts require rearranging, or "swizzling," components rather than reinterpreting the shared storage as a complete case. For example, `Buffer` uses `_reference0` and both integers in `_unmanaged`; `Pair` uses both reference slots. Unused reference and typed slots are defaulted so inactive cases retain no references.

Keeping a separate complete field for every case avoids swizzling but costs their combined size, significant for inline, copied values. The compiler can choose shared pools, separate fields, or a hybrid. When every case is unmanaged and the required layout facts are established, complete case values can safely overlap in one region sized and aligned for the largest case, with a separate discriminator, avoiding field swizzling.

#### Unmanaged Classification

An enum struct's unmanaged classification is observable through constraints, pointers, and `stackalloc`. LDM must decide whether a compiler representation change may alter this classification in either direction, potentially breaking consumers, or must preserve unmanagedness when declared cases, payload types, and constraints are unchanged. The latter still permits managed-to-unmanaged changes but constrains future storage choices.

This concerns the enum's own classification; the opt-in below concerns assumptions about payload types.

#### Possible Checked Packing Opt-in

A compiler-recognized attribute could assert a payload's unmanagedness and fixed byte size, either at its declaration or at an enum case parameter use site:

```csharp
// In a referenced library:
[Unmanaged(size: 8)]
public readonly record struct Point(int X, int Y);

// Consumer assertions can also cover unannotated external types:
enum struct Position
{
    At(Point point),
    Timestamp([Unmanaged(size: 8)] DateTime value)
}
```

For either placement, the compiler must verify that the payload is a struct satisfying C#'s unmanaged rules; the annotation is not proof. It may diagnose size mismatches known statically. The name, syntax, and precise placement rules remain open.

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

A packing enum must validate its own recorded claims once per constructed enum type through a generated static constructor. Constructors and case-to-enum conversions must trigger this before packing, under the [struct static-constructor rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#16410-static-constructors). Non-default extraction can then rely on completed validation. Zero-initialized enums have no packed payload and need no extra validation; failed `TryGetValue` can assign a typed `default` to its out parameter.

The payload initializer alone is insufficient: defaults, arrays, layout queries, and whole-value copies can bypass it. A later library could also remove the annotation or validate a different size. A consumer reserving eight bytes must validate its own eight-byte expectation, rejecting updates that ordinary typed storage could accept.

The `DateTime` assertion uses its current eight-byte managed size; that is not a language guarantee. These cases could share an eight-byte region, separately from the discriminator. The annotation permits packing without requiring it and establishes only reference containment and total size. Field offsets, alignment, and accessibility need separate justification, or values can be copied opaquely. Adoption remains a joint LDM and runtime-team decision supported by performance measurements.

### Switch Translation

An initial strategy uses linear case tests. Local .NET 11 RC1 ARM64 checks with 16 sealed class cases and 16 struct cases found that exact-type and discriminator-predicate chains remained linear in fully optimized code and Tier 1 with dynamic PGO, even after predicate inlining.

Preliminary M4 Mac benchmarks indicate that linear dispatch performs well and appears sufficient through 32 cases in the tested patterns. Larger case sets may warrant better emit or JIT optimization without blocking initial implementation. Validation on x64 and other architectures, varied usage patterns and case distributions, guards, and both switch statements and expressions remains warranted.

The input is evaluated once. Reordering tests must preserve null handling, guards, deconstruction, source ordering where observable, and the original fallback behavior. The sketches below omit those details and use distinct unguarded cases.

#### `enum class` Case Tests

For cases `E.C1`, `E.C2`, and `E.C3`, compare the exact runtime type against each sealed case type, then extract the payload:

```csharp
var __case = e;
var __type = __case?.GetType();
if (__type == typeof(E.C1)) { HandleC1(((E.C1)__case).value); }
else if (__type == typeof(E.C2)) { HandleC2(((E.C2)__case).value); }
else if (__type == typeof(E.C3)) { HandleC3(((E.C3)__case).value); }
```

The runtime can compare method-table pointers for exact-type tests. Distinct case identities could support a balanced tree or profile-guided ordering, and a successful comparison establishes the type for extraction.

#### `enum struct` Case Tests

Use case predicates followed by extraction:

```csharp
var __value = e;
if (__value.IsC1) { HandleC1(((E.C1)__value).value); }
else if (__value.IsC2) { HandleC2(((E.C2)__value).value); }
else if (__value.IsC3) { HandleC3(((E.C3)__value).value); }
```

Inlining predicates such as `IsC1 => _discriminator == 1` exposes integral comparisons:

```csharp
if (__value._discriminator == 1) { /* C1 body */ }
else if (__value._discriminator == 2) { /* C2 body */ }
else if (__value._discriminator == 3) { /* C3 body */ }
```

The runtime could choose a dense jump table, sparse tree, or chain and eliminate checks dominated by a successful predicate. Marking struct conversions for inlining removed redundant extraction checks in the local tests.

#### Runtime Recognition and Alternatives

The [existing switch-recognition pass](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/switchrecognition.cpp#L28) runs after inlining, but handles comparison chains whose successful tests share a destination; different case bodies stop recognition. Extending it is one possible approach.

If general recognition raises correctness concerns, an attribute in `System.Runtime.CompilerServices` could opt well-behaved DUs into optimization. Its contract would cover stable case identity during dispatch, side-effect-free predicates agreeing with the case mapping, and extraction after a successful test. It could identify a struct's discriminator field for direct runtime access. This optional optimization contract is distinct from the required source-semantics marker; neither its name nor final contract is selected. Class enums could also use predicates and extraction if optimized efficiently.

A final fallback is exposing a discriminator and emitting an integral switch, using existing runtime optimizations directly. Classes would need a case-to-discriminator mapping too. We would prefer to avoid the stable API/ABI and case-numbering obligations described under [Library Evolution](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#library-evolution). Extraction must still follow the specified conversions, including reconstruction from shared storage.

## 7. Possible Connections to Type Unions

This section collects incomplete, exploratory thoughts for later discussion; it is not required for the feature. Neither enum representation is required to implement the union pattern, carry `[Union]`, or implement `IUnion`. LDM could choose full integration, reuse selected compiler machinery, or keep the features independent.

### Union Pattern and Runtime Access

The [type-unions proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) permits custom union classes or structs recognized through `[Union]` and creation/access members. Its `union` declaration selects an object-backed struct implementation; the custom pattern would let enhanced enums retain their own representation.

[`IUnion`](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/union-patterns-update.md#marking-union-types-with-an-attribute) separately exposes contents to runtime consumers; implementing it alone does not supply compiler union behavior.

### Possible Integration for `enum class`

A [union member provider](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-member-providers) could supply `Create` methods returning the case reference unchanged and `Value` returning `this`, optionally exposed through `IUnion`. This needs no wrapper allocation, though recursive runtime inspection must account for the self-reference.

The benefit is interoperability with other unions. The base/case relationship already supplies most language behavior, so integration adds APIs or provider metadata while still requiring the enum's special construction and hierarchy rules.

### Possible Integration for `enum struct`

Generated construction operations, `HasValue`, and `TryGetValue` fit much of the custom-union pattern. Integration would additionally need `[Union]` and an object-valued `Value`; `IUnion` could expose the same view at runtime.

Benefits include reusing union conversion, case-pattern, deconstruction, and nullable-matching machinery while retaining inline storage and non-boxing `TryGetValue`. Union recognition alone would not supply the enum's special construction rules; consumers must also honor its metadata.

The costs and choices are:

- `Value` boxes struct cases and requires object-convertible cases. Future ref-like cases would need a different contract.
- Public protocol names can conflict with domain meanings: `Option<T>.Value` might mean its payload and `HasValue` might mean `Some`, whereas the protocol exposes the active case and recognizes `None` too. A member provider avoids name conflicts but adds interface metadata and would require deciding where the proposed public `HasValue` belongs.
- Union type patterns inspect the contained case, while untyped property/positional patterns inspect the enum. Integration must preserve that distinction, following the [LDM decision on pattern targets](https://github.com/dotnet/csharplang/blob/main/meetings/2026/LDM-2026-08-12.md#union-pattern-matching-targets).
- Union null patterns would select the open [uninitialized-null matching option](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#case-pattern-matching), with nullable `Value`. A designated default case could use non-nullable `Value`; annotations and access members must agree.
- Union case-type exhaustiveness must accommodate the enum's separate open/closed evolution policy.

`[Union]` permits arbitrary implementations and identifies no discriminator mapping; it would not by itself provide the stronger guarantees needed for the proposed JIT optimization contract.

## 8. Open Questions

- Should cases support individual access modifiers?
- Should enhanced enums support `partial` for source generators?
- How should they interact with System.Text.Json?
- Should additional instance state be permitted?
- Should custom constructors be allowed to delegate to cases?
- Should cases support independent generic parameters?

## Appendix A: Grammar Changes

```antlr
shape_enum_declaration
    : attributes? shape_enum_modifier* 'enum' ('class' | 'struct') identifier type_parameter_list?
      type_parameter_constraints_clause* shape_enum_body ';'?
    ;

shape_enum_modifier
    : enum_modifier
    | 'readonly'
    ;

shape_enum_body
    : '{' shape_enum_case_declarations? '}'
    | '{' shape_enum_case_declarations ';' class_member_declarations '}'
    ;

shape_enum_case_declarations
    : shape_enum_case_declaration (',' shape_enum_case_declaration)*
    ;

shape_enum_case_declaration
    : attributes? identifier parameter_list?
    | attributes? identifier '=' 'default'
    ;
```

The `readonly` modifier is permitted only on enum structs; see [Readonly, Copying, and `with`](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#readonly-copying-and-with). For `= default` restrictions, see [Default Values](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/extended-enums.md#default-values).
