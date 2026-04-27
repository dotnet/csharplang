# null-conditional await

Champion issue: <https://github.com/dotnet/csharplang/issues/8631>

## Summary
[summary]: #summary

Support an expression of the form `await? e`, which awaits `e` if it is non-null, otherwise it results in `null`.

## Motivation
[motivation]: #motivation

This feature is not intended to encourage code that returns a `null` task from `async`-shaped methods. We are *not* trying to make patterns like `Task DoSomethingAsync() { return null; }` easier to consume; returning a `null` task from such a method would remain discouraged.

The feature exists to deal with the case where a task-returning expression happens to be null because some earlier step in the expression was null. For example:

```csharp
await GetX()?.DoSomethingAsync();
```

Here `DoSomethingAsync` itself always returns a non-null `Task`; the `Task` reference is null because `GetX()` returned null and the `?.` short-circuited. Today this expression compiles, evaluates to `null`, and then throws a `NullReferenceException` from `await`. The feature allows the `?` to carry through to the await:

```csharp
await? GetX()?.DoSomethingAsync();
```

which does nothing if `GetX()` was null, and otherwise awaits the task. This composes naturally with the existing null-propagating and null-coalescing operators.

## Detailed design
[design]: #detailed-design

### `await? t` builds on `?.` and `await`

The semantics of `await? t` are, both intuitively and literally, the composition of two existing language features:

- the *null_conditional_member_access* `?.` of [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access), gating the rest of the expression on the operand's non-nullness, and
- the ordinary `await` of [§12.9.8.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12984-run-time-evaluation-of-await-expressions), evaluating the awaiter on the non-null branch.

Concretely, `await? t` is the *null_conditional_member_access*

```csharp
(t)?.GetAwaiter().GetResult()
```

per §12.8.8, with `await` semantics (§12.9.8.4) applied on the non-null branch. When `GetResult()` returns `void`, the same shape is a *null_conditional_invocation_expression* per [§12.8.10](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12810-null-conditional-invocation-expression) instead.

Equivalently, `await? t` has the meaning of:

- `((object)t == null) ? default(X) : await t`, when `t`'s static type is not `Nullable<V>`, or
- `(!t.HasValue) ? default(X) : await t.Value`, when `t` has type `Nullable<V>`,

where `X` is the type of `await? t` (computed by §12.9.8.3 below) and `t` is evaluated only once. This is the most direct operational mental model, and it is sufficient to read the rest of the proposal without consulting the formal subsections.

The `Nullable<V>` case above is supplied automatically by §12.8.8's existing nullable-value-type-receiver rule (its `P.Value.A` substitution); `await?` does not introduce a separate "underlying type" concept.

Each subsection below identifies which piece of §12.8.8 / §12.8.10 it inherits, and what `await?` plugs in.

### Grammar

[§12.9.8.1 General](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12981-general) is updated as follows.

```diff
 await_expression
     : 'await' unary_expression
+    | 'await' '?' unary_expression
     ;
```

The null-conditional form of *await_expression* (`'await' '?' unary_expression`, hereafter written `await? t`) is subject to the same placement restrictions as the existing form. For example, it is only allowed in the body of an async function.

### Applicability

A new subsection is added to [§12.9.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1298-await-expressions):

> #### 12.9.8.5 Applicability of null-conditional await
>
> An *await_expression* of the form `await? t` is well-typed when all of:
>
> - The static type `S` of `t` is admissible as the operand `P` of a *null_conditional_member_access* `P?.A` per [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access). The runtime null-test on `t` is the one §12.8.8 defines for that `P` — `!t.HasValue` when `S = Nullable<V>`, `(object)t == null` otherwise; for a type-parameter operand whose runtime instantiation is a non-nullable value type, the test is trivially false, exactly as for `P?.A`.
> - `S` is not a non-nullable value type, nor a type parameter known to be a non-nullable value type (e.g. `where S : struct`, `where S : unmanaged`); such an operand can never be null at runtime, and the `?` is therefore meaningless.
> - The awaitable pattern of [§12.9.8.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12982-awaitable-expressions) is satisfied for `(t)?.GetAwaiter()`.
>
> *Note*: As in `P?.A`, the spec's type-parameter restriction falls on the **result** of the access rather than the **operand** receiver. An unconstrained, interface-constrained, or `notnull`-constrained type parameter is therefore permitted as `t`; the corresponding restriction on the awaiter's result type `R` is handled in §12.9.8.3 below. *end note*

### Awaitable expressions

[§12.9.8.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12982-awaitable-expressions) is updated as follows.

Additions in **bold**:

> The task of an *await_expression* is required to be ***awaitable***. An expression `t` is awaitable if one of the following holds:
>
> - `t` is of compile-time type `dynamic`
> - ...
>
> **For an *await_expression* of the form `await? t`, the awaitable pattern (including extension-method `GetAwaiter` resolution) is required to be satisfied at the receiver of the dependent `GetAwaiter()` access in `(t)?.GetAwaiter().GetResult()` per [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access) — that is, on `t.Value` when `t` has type `Nullable<V>`, and on `t` otherwise.**

### Classification

[§12.9.8.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12983-classification-of-await-expressions) is updated as follows.

Additions in **bold**:

> The expression `await t` is classified the same way as the expression `(t).GetAwaiter().GetResult()`. Thus, if the return type of `GetResult` is `void`, the *await_expression* is classified as nothing. If it has a non-`void` return type `T`, the *await_expression* is classified as a value of type `T`.
>
> **The classification of `await? t` is the classification — and the type, in the non-`void` case — of the *null_conditional_member_access* `(t)?.GetAwaiter().GetResult()` per [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access), or the *null_conditional_invocation_expression* of the same shape per [§12.8.10](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12810-null-conditional-invocation-expression) when `GetResult()` returns `void`. Inheriting §12.8.8 / §12.8.10 here gives `await?` the lift to `Nullable<R>` for non-nullable value-type results, the unchanged classification of an already-`Nullable<V>` result, the reference-type result with its nullable-reference-type annotation, the pointer-type result case, the compile-time error when `R` is a type parameter not known to be a reference type or non-nullable value type, and the *nothing* / statement-position rule for `void` — all without restatement.**
>
> **Regardless of branch, `t` is evaluated only once.**

> *Note*: `await?` inherits the asymmetry of `P?.A`: an unconstrained, interface-constrained, or `notnull`-constrained type parameter is permitted as the *operand* `t` (with the runtime null-test from §12.9.8.5), but the same type parameter appearing as the awaiter's *result type* `R` is a compile-time error per §12.8.8. *end note*

### Run-time evaluation

[§12.9.8.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12984-run-time-evaluation-of-await-expressions) is updated as follows.

Additions in **bold**:

> At run-time, the expression `await t` is evaluated as follows:
>
> - An awaiter `a` is obtained by evaluating the expression `(t).GetAwaiter()`.
> - ...
>
> **At run-time, `await? t` is evaluated as the *null_conditional_member_access* `(t)?.GetAwaiter().GetResult()` per [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access), or the *null_conditional_invocation_expression* of the same shape per [§12.8.10](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12810-null-conditional-invocation-expression) when `GetResult()` returns `void`, with `await` semantics (§12.9.8.4) applied on the non-null branch. The null-test on `t`, the short-circuit (with `t` evaluated only once), §12.8.8's handling of `t : Nullable<V>` (where the dependent access runs against `t.Value`), and the implicit conversion of the non-null branch's value to the static type of `await? t` are all inherited from §12.8.8 / §12.8.10. In particular, when `t` is null no awaiter is obtained, `IsCompleted`/`OnCompleted`/`UnsafeOnCompleted`/`GetResult` are not invoked, and the enclosing async function is not suspended at this expression — directly because §12.8.8's short-circuit does not evaluate the dependent accesses.**
>
> **Equivalently, `await? t` has the meaning of:**
>
> - **`((object)t == null) ? default(X) : await t`, when `t`'s static type is not `Nullable<V>`, or**
> - **`(!t.HasValue) ? default(X) : await t.Value`, when `t` has type `Nullable<V>`,**
>
> **where `X` is the type of `await? t` per §12.9.8.3, with `t` evaluated only once and the implicit conversion from the non-null branch's value to `X` following the standard conditional-expression rules.**

### Worked examples

The following tables are non-normative. They illustrate how §12.8.8 / §12.8.10 inheritance plays out for `await? t`.

`await?` is defined in terms of the awaitable pattern ([§12.9.8.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12982-awaitable-expressions)) and not in terms of any specific BCL types. The framework types `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>` appear below purely as illustrative examples of, respectively, reference-type and value-type awaitables; an arbitrary user-defined `class RefAwaitable` or `struct StructAwaitable<T>` that satisfies §12.9.8.2 behaves identically to `Task` or `ValueTask<T>` with the same `GetResult()` return type. The rules apply uniformly.

**Table A. How the operand is null-tested and where the awaitable pattern is resolved (§12.9.8.5 + §12.9.8.2, both inheriting §12.8.8):**

| Static type of `t` | Null test | Awaitable pattern applied to |
|---|---|---|
| Reference-type awaitable (e.g. `Task`, `Task<X>`, user `class RefAwaitable`) | `(object)t == null` | `t` |
| `Nullable<V>` where `V` is a value-type awaitable (e.g. `Nullable<ValueTask>`, `Nullable<ValueTask<X>>`, `Nullable<StructAwaitable<X>>`) | `!t.HasValue` | `t.Value` |
| Type parameter `S` without a `struct` constraint (includes `where S : class`, `where S : SomeBaseClass`, `where S : ISomething`, `where S : notnull`, unconstrained, …) | `(object)t == null` (trivially false at runtime for non-nullable value-type instantiations; the JIT is expected to elide it) | `t` |
| `dynamic` | Runtime null-test on `t` | `t` |
| Non-nullable value-type awaitable (e.g. `ValueTask`, `ValueTask<X>`, user `struct StructAwaitable<X>`) | compile-time error | |
| Type parameter `S` known to be a non-nullable value type (e.g. `where S : struct`, `where S : unmanaged`) | compile-time error | |

**Table B. How the result type of `await? t` is computed from `R = GetResult()`'s return type (§12.9.8.3 inheriting §12.8.8 / §12.8.10):**

| `R` | Classification of `await? t` |
|---|---|
| `void` | *nothing*. Permitted only at statement position (as for *null_conditional_invocation_expression*, [§12.8.10](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12810-null-conditional-invocation-expression)) |
| Non-nullable value type (including a type parameter `where T : struct`) | `Nullable<R>` |
| `Nullable<V>` for some non-nullable value type `V` (e.g. `GetResult()` returns `Nullable<int>`) | `R` unchanged (e.g. `Nullable<int>`) |
| Reference type (including a type parameter `where T : class`) | `R` with nullable-reference-type annotation `R?` |
| `dynamic` | `dynamic` |
| Type parameter not known to be a reference type or a non-nullable value type (unconstrained, interface-constrained, `notnull`-constrained, …) | compile-time error (mirrors §12.8.8 type-parameter restriction) |

The Task/ValueTask behaviors readers typically think about are all mechanical cross-products of the two tables above:

- `Task` → *nothing*
- `Nullable<ValueTask>` → *nothing*
- `Task<int>?` → `Nullable<int>`
- `Task<Nullable<int>>?` → `Nullable<int>` (via the already-nullable row of Table B)
- `Task<string>?` → `string?`
- `Task<T>` where `T : struct` → `Nullable<T>`
- `Task<T>` where `T : class` → `T?` (nullable reference)
- `Task<T>`, `T` unconstrained → compile-time error (result type is a type parameter not known to be a reference type or a non-nullable value type)
- `Nullable<ValueTask<int>>` → `Nullable<int>`

### Interaction and edge cases

- **Extension `GetAwaiter`** is supported. §12.9.8.2's awaitable-pattern check (which includes extension-method `GetAwaiter` resolution) runs against the receiver of `(t)?.GetAwaiter()` per §12.8.8 — `t.Value` for `t : Nullable<V>`, `t` otherwise — exactly as for ordinary `await` on those receivers.
- **`ConfigureAwait(false)`** returns a struct awaitable type (e.g. `ConfiguredTaskAwaitable`). Consequently, `await? task.ConfigureAwait(false)` is a compile-time error per §12.9.8.5 (non-nullable value-type operand). The intended spelling when `task` itself is nullable is `await? task?.ConfigureAwait(false)`: the inner `?.` produces a `Nullable<ConfiguredTaskAwaitable>`, which is a valid `await?` operand.
- **`await? t` is a *unary_expression***, not a *null_conditional_member_access*. It does not continue a `?.` chain from its left: to apply `await?` to the result of `x?.GetTaskAsync()` the spelling is `await? x?.GetTaskAsync()` (`await?` consumes the result of the entire `?.` chain), not a continuation of that chain.
- **Statement position** is permitted regardless of whether `GetResult()` returns `void` or a value: `await? task;` where `task` has type `Task<int>?` is a valid statement (the `Nullable<int>` result is discarded), exactly as `x?.IntReturningMethod()` is a valid statement today.

## Drawbacks
[drawbacks]: #drawbacks

As with any language feature, we must question whether the additional complexity to the language is repaid in the additional clarity offered to the body of C# programs that would benefit from the feature.

## Alternatives
[alternatives]: #alternatives

Although it requires some boilerplate code, uses of this operator can often be replaced by an expression something like `(e == null) ? null : await e` or a statement like `if (e != null) await e`.

## Unresolved questions
[unresolved]: #unresolved-questions

- `await? t` where the static type of `t` is `dynamic`: the result type is `dynamic` (there is no distinct `dynamic?` type in the language). Listed for LDM confirmation rather than as a genuinely open design question.

## Design meetings

- [LDM 2017-02-21: Null-coalescing assignments and awaits](https://github.com/dotnet/csharplang/blob/main/meetings/2017/LDM-2017-02-21.md#null-coalescing-assignments-and-awaits)
- [LDM 2022-08-31: `await?`](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-08-31.md#await)
