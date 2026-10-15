# Common Design for Discriminated Unions and Data-Carrying Enums

This document is not intended to be read on its own. It holds design material referenced by the [union-based discriminated-union proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md) and the [data-carrying enum proposal](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md) to avoid repetition. Differences between the two approaches are called out where they affect the shared analysis.

## Contextual Construction and Inference

Qualified case construction and patterns work without target-typed access or improved generic inference. The examples below use the `Option<T>` cases from either walkthrough: `None` and `Some(T Value)`. The traces identify which rules each shortened form needs, so compatibility can be checked as the related proposals evolve.

[Target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md) defines leading-dot lookup and its conversions. [Generalized generic type inference](https://github.com/dotnet/csharplang/blob/madstorgersen-generalized-type-inference/proposals/generalized-type-inference.md) describes target-derived bounds. The [revised target-typed generic inference draft](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md) specifies when binding retries with a target and how nested inference proceeds. These drafts remain in progress. The analysis below uses the revised draft's rule that successful ordinary binding takes precedence.

Inference starting from case arguments, including `Use(.Some(42))` without another source for the outer type argument, remains an open question in both the [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#inference-from-case-arguments) and [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md#inference-from-case-arguments) designs.

### Case Access with a Known Target

We expect these forms to work with target-typed static member access and either proposal's case-construction rules:

```csharp
Option<int> none = .None;
Option<int> some = .Some(42);
```

1. Each initializer has the target type `Option<int>`.
2. The [target-typed member binding conversion](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#basic-expression) qualifies `.None` as `Option<int>.None`. The [invocation target-typing conversion](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#target-typing-with-invocations) similarly qualifies `.Some(42)` as `Option<int>.Some(42)`.
3. Lookup finds the nested case types on the already constructed `Option<int>`. Its type arguments are known, just as they would be for lookup of a static property on that type. No inference through the nested member is needed.
4. Case construction produces the corresponding case values without `new`. Each result then uses the [case-to-enum conversion](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#generated-apis-and-conversions) or the existing [union conversion](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md#union-conversions) to satisfy the target.

Without a target, `var value = .None` is an error. The leading-dot rules need to recognize generated cases under the construction rules of the [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#construction-and-case-access) and [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md#construction-and-case-access) designs, rather than requiring a static factory with the same name as the nested type.

### Inference from Another Argument

We expect this call to work with target-typed static member access and ordinary argument inference. It needs no target-derived generic inference:

```csharp
void Use<T>(Option<T> value, T fallback) { /* ... */ }

Use(.None, 42);
```

1. Method lookup finds `Use<T>`. Type inference starts with `T` unfixed.
2. `.None` has no independent type, so [input type inference](https://github.com/dotnet/csharpstandard/blob/alpha-v12/standard/expressions.md#12637-input-type-inferences) gathers no bound from it.
3. `42` has type `int`. Input type inference against the second parameter, `T`, contributes a lower bound of `int`.
4. Fixing selects `T = int`. Substitution gives parameter types `Option<int>` and `int`.
5. The target-typed member binding conversion makes `.None` acceptable for the first parameter without using the member name to select an overload. The second argument has an identity conversion. With this sole overload, `Use<int>` is selected.
6. The first parameter supplies the target `Option<int>`. Binding `.None` then follows the known-target lookup above, producing the case value and converting it to the enclosing type.

This ordering agrees with the revised draft's [nested-inference rules](https://github.com/dotnet/csharplang/blob/madstorgersen-target-typing-proposal/proposals/target-typed-generic-type-inference.md#nested-inference).

### Inference from a Known Case Type

We expect this call to infer `T = int` for both `union` and `union struct` if [union-case type inference](https://github.com/dotnet/csharplang/pull/10355) is adopted.

For `enum struct`, the corresponding inference remains an [open question](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#inference-from-case-arguments). Existing method inference does not follow its generated user-defined conversion.

```csharp
void Use<T>(Option<T> value) { /* ... */ }

Use(Option<int>.Some(42));
```

1. Qualified construction produces an `Option<int>.Some`, independently of the parameter's target type.
2. Lower-bound inference against `Option<T>` finds the unique matching case, `Option<T>.Some`.
3. The proposed rule recurses from `Option<int>.Some` to `Option<T>.Some`. Ordinary inference through the nested type's containing generic type supplies `T = int`.
4. The argument then has a union conversion to the fixed parameter type, `Option<int>`.

### Target-Typed Patterns and Switch Arms

We expect these patterns and case constructions to work with target-typed static member access. The type parameters are declared on `Map`, so its body needs no generic inference. Here `Result<T, E>` is a `union` or a `closed enum class` declaring `Ok(T Value)` and `Error(E Value)` cases:

```csharp
Result<U, E> Map<T, U, E>(Result<T, E> result, Func<T, U> mapper)
    => result switch
    {
        .Ok(var value) => .Ok(mapper(value)),
        .Error(var error) => .Error(error)
    };
```

1. The [target-typed pattern rules](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md#pattern-matching) use the input `Result<T, E>` to qualify the pattern case names.
2. The `Ok` pattern extracts a `T`, while the `Error` pattern extracts an `E`.
3. The method's return type `Result<U, E>` target-types the switch arms and qualifies their construction expressions.
4. `mapper(value)` supplies a `U` for `Result<U, E>.Ok`, while `error` supplies an `E` for `Result<U, E>.Error`. Each case value converts to the enclosing result type using that proposal's conversion rules.

[Type-pattern inference](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns.md) separately addresses written type names with omitted generic arguments.

## Metadata and Compatibility

The compiler emits metadata identifying generated case types and their construction operations. For data-carrying enums, it also identifies the enclosing enum. Supporting C# compilers use it to enforce the [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#construction-and-case-access) and [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md#construction-and-case-access) construction rules across assembly boundaries. The encoding remains an open question in the [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#metadata-encoding) and [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md#metadata-encoding) designs.

The compiler adds a nonoptional [CompilerFeatureRequired](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.compilerfeaturerequiredattribute) attribute to:

- The data-carrying enum and its generated case types, with a feature name such as `"DataCarryingEnums"` (TBD).
- Generated union case types, with a feature name such as `"DiscriminatedUnionCases"` (TBD).

Compilers that recognize the attribute but lack feature support reject use of these types. This prevents acceptance of `new E.Case(...)` that would become invalid after a compiler upgrade. An error-level `ObsoleteAttribute` guard covers compilers predating `CompilerFeatureRequired`, following the [required-members compatibility pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/required-members.md#metadata-representation). Supporting compilers ignore that guard.

## Readonly, Copying, and `with`

Generated reference-type cases follow record-class rules. Generated value-type cases follow the containing declaration's readonlyness and the [`record struct` property rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#properties): mutable cases have `get`/`set` payload properties, while readonly cases have `get`/`init`.

`enum struct` and `union struct` follow ordinary [`readonly struct` rules](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1622-struct-modifiers): unmodified declarations are mutable, and the `readonly` modifier makes them readonly. Whether to make these types always readonly remains open in the [enum-based](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md#always-readonly-enum-struct-types) and [union-based](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md#always-readonly-union-struct-types) designs. Generated queries, extraction methods, and equality members on `enum struct` are `readonly` instance members.

Assignment follows ordinary class and struct rules. An `enum class` assignment copies the case reference, while a bare `union` assignment copies a struct holding that reference. Assignments of `enum struct` and `union struct` values copy their stored payloads shallowly. Extracting a struct case produces an independent value, so changing it does not update the enclosing enum or union. For example:

```csharp
enum struct Progress { Waiting, Running(int Count) }

Progress progress = Progress.Running(1);
Progress.Running running = (Progress.Running)progress;
running.Count = 2; // progress still contains Count = 1
progress = running with { Count = 3 }; // update a case copy, then convert back
```

Generated case types support ordinary record `with` expressions. On an `enum class` receiver, `e with { }` clones the runtime case record, preserving case identity. On an `enum struct`, `union struct`, or bare `union` receiver, ordinary [struct `with` rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#allow-with-expression-on-structs) copy the enclosing struct, preserving its active case or uninitialized state. Initializers bind against the receiver's static type, so case-specific payload updates require a case-typed receiver.
