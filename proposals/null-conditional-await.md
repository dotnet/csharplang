# null-conditional await

Champion issue: <https://github.com/dotnet/csharplang/issues/8631>

## Summary
[summary]: #summary

Support an expression of the form `await? e`, which awaits `e` if it is non-null, otherwise it results in `null`.

## Motivation
[motivation]: #motivation

This is a common coding pattern, and this feature would have nice synergy with the existing null-propagating and null-coalescing operators.

## Detailed design
[design]: #detailed-design

### Two independent rules

The semantics of `await? e` are described by two orthogonal rules that are applied independently. Every case in the worked-examples table at the end of this section is a mechanical cross-product of these two rules; the behaviors of `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, and arbitrary user-defined awaitables are all consequences of them, not normative cases of their own.

- **Operand-nullability rule** (governs applicability / well-typedness): what does the static type `S` of `e` tell us about whether `e` can be null at runtime? This drives the compile-time error cases and the shape of the runtime null-test. It is entirely independent of what the awaitable produces.
- **Result-type rule** (governs classification): given the awaitable's `GetResult()` return type `R`, what is the type of the whole `await? e` expression? This mirrors the shape of the null-conditional member-access rule in [§11.7.7](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1177-null-conditional-member-access) — "lift to `Nullable<T>` for non-nullable value types, keep as-is otherwise" — updated for modern C#.

The rest of this section specifies the grammar change, then the operand-nullability rule, then the awaitable-pattern resolution over the underlying type, then the result-type rule, then the run-time semantics that tie the two rules together, then the worked-examples table, and finally a set of notes on interactions with existing features.

### Grammar

[§11.8.8.1 General](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11881-general) is updated as follows.

```diff
 await_expression
     : 'await' unary_expression
+    | 'await' '?' unary_expression
     ;
```

The null-conditional form of *await_expression* (`'await' '?' unary_expression`, hereafter written `await? t`) is subject to the same placement restrictions as the existing form: it is only allowed in the body of an async function, and shall not occur inside a nested (non-async) anonymous function, inside the block of a *lock_statement*, in an anonymous function conversion to an expression tree type, or in an unsafe context.

### Applicability — operand-nullability rule

A new subsection is added to [§11.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1188-await-expressions):

> #### 11.8.8.5 Applicability of null-conditional await
>
> An *await_expression* of the form `await? t` is well-typed only if the static type `S` of `t` is one of the following:
>
> - A reference type (class, interface, delegate, or array type), nullable-annotated or not. The runtime null-test is `(object)t == null`.
> - A type of the form `Nullable<V>` for some non-nullable value type `V`. The runtime null-test is `!t.HasValue`; where the awaitable pattern of [§11.8.8.2](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11882-awaitable-expressions) is applied, the receiver on the non-null branch is `t.Value`.
> - A type parameter without a `struct` constraint. The runtime null-test is `(object)t == null`; for runtime instantiations where the type argument happens to be a non-nullable value type, the test is trivially false.
> - `dynamic`. The null-test is performed on the runtime value of `t`.
>
> The third bullet covers every type parameter whose instantiations may include a reference type — e.g. a type parameter with a `class` constraint, a base-class constraint, an interface constraint, a `notnull` constraint, or no constraints at all.
>
> In all other cases, `await? t` is a compile-time error, because `t` can never be null and the `?` is therefore meaningless. For example: `S` is a concrete non-nullable value type (such as `ValueTask` or `ValueTask<int>`); `S` is a type parameter with a `struct` constraint; `S` is a type parameter with an `unmanaged` constraint (which itself implies a value type).
>
> *Note*: The existing rule in [§11.7.7](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1177-null-conditional-member-access) for *null_conditional_member_access* phrases its type-parameter compile-time error in terms of the **result type** of `P.A`, not the **receiver type** `P`. The rule above follows the same pattern: it places no constraint on type-parameter *operands* of `await?` beyond excluding those known to be non-nullable value types; any constraint on type-parameter *results* is handled in §11.8.8.3 below. *end note*

### Awaitable expressions

[§11.8.8.2](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11882-awaitable-expressions) is updated as follows.

Additions in **bold**:

> The task of an *await_expression* is required to be ***awaitable***. An expression `t` is awaitable if one of the following holds:
>
> - `t` is of compile-time type `dynamic`
> - `t` has an accessible instance or extension method called `GetAwaiter` with no parameters and no type parameters, and a return type `A` for which all of the following hold:
>   - `A` implements the interface `System.Runtime.CompilerServices.INotifyCompletion` (hereafter known as `INotifyCompletion` for brevity)
>   - `A` has an accessible, readable instance property `IsCompleted` of type `bool`
>   - `A` has an accessible instance method `GetResult` with no parameters and no type parameters
>
> **For an *await_expression* of the form `await? t`, the awaitability check above is performed against the *underlying type* `U` of `t`, where `U = V` when `t`'s static type is `Nullable<V>`, and `U = S` (the static type of `t`) otherwise. The awaitable pattern — including extension-method `GetAwaiter` resolution — is applied to `U` in place of `t`'s static type.**

### Classification

[§11.8.8.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11883-classification-of-await-expressions) is updated as follows.

Additions in **bold**:

> The expression `await t` is classified the same way as the expression `(t).GetAwaiter().GetResult()`. Thus, if the return type of `GetResult` is `void`, the *await_expression* is classified as nothing. If it has a non-`void` return type `T`, the *await_expression* is classified as a value of type `T`.
>
> **For an *await_expression* of the form `await? t`, let `R` be the return type of `(u).GetAwaiter().GetResult()`, where `u` has the underlying type `U` determined per §11.8.8.2. The classification of `await? t` is determined from `R` as follows:**
>
> - **If `R` is `void`, `await? t` is classified as *nothing*, and may appear only where a *null_conditional_invocation_expression* ([§11.7.9](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1179-null-conditional-invocation-expression)) is permitted — i.e. as a *statement_expression*, *anonymous_function_body*, or *method_body*.**
> - **Otherwise, if `R` is a non-nullable value type (either a concrete struct type, or a type parameter with a `struct` constraint), `await? t` is classified as a value of type `Nullable<R>`.**
> - **Otherwise, if `R` is already a nullable value type (i.e. `R = Nullable<V>` for some non-nullable value type `V`), `await? t` is classified as a value of type `R` (unchanged).**
> - **Otherwise, if `R` is a reference type, or a type parameter known to be a reference type, `await? t` is classified as a value of type `R` with nullable-reference-type annotation `R?`.**
> - **Otherwise, `R` is an unconstrained type parameter, and `await? t` is classified as a value of type `R?`, where `?` is the default-able type-parameter annotation introduced in C# 9.**
>
> **Regardless of branch, `t` is evaluated only once.**

> *Note*: The last bullet is a deliberate departure from the corresponding rule in §11.7.7, which predates C# 9 and produces a compile-time error when the result type of `P?.A` is an unconstrained type parameter. Modern C# permits `T?` on unconstrained type parameters as a default-able annotation, and `await?` follows suit. *end note*

> *Note*: This classification rule is concerned solely with computing the *result type* from the awaiter's `R`. Constraints on the *operand* type `S` of `t` are handled by §11.8.8.5 above; in particular, any type-parameter operand that §11.8.8.5 admits receives a runtime null-check, exactly as the receiver of `P?.A` does. *end note*

### Run-time evaluation

[§11.8.8.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11884-run-time-evaluation-of-await-expressions) is updated as follows.

Additions in **bold**:

> At run-time, the expression `await t` is evaluated as follows:
>
> - An awaiter `a` is obtained by evaluating the expression `(t).GetAwaiter()`.
> - A `bool` `b` is obtained by evaluating the expression `(a).IsCompleted`.
> - If `b` is `false` then evaluation depends on whether `a` implements the interface `System.Runtime.CompilerServices.ICriticalNotifyCompletion` (hereafter known as `ICriticalNotifyCompletion` for brevity). This check is done at binding time; i.e., at run-time if `a` has the compile-time type `dynamic`, and at compile-time otherwise. Let `r` denote the resumption delegate ([§14.15](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/classes.md#1415-async-functions)):
>   - If `a` does not implement `ICriticalNotifyCompletion`, then the expression `((a) as INotifyCompletion).OnCompleted(r)` is evaluated.
>   - If `a` does implement `ICriticalNotifyCompletion`, then the expression `((a) as ICriticalNotifyCompletion).UnsafeOnCompleted(r)` is evaluated.
>   - Evaluation is then suspended, and control is returned to the current caller of the async function.
> - Either immediately after (if `b` was `true`), or upon later invocation of the resumption delegate (if `b` was `false`), the expression `(a).GetResult()` is evaluated. If it returns a value, that value is the result of the *await_expression*. Otherwise, the result is nothing.
>
> **At run-time, the expression `await? t` is evaluated as follows:**
>
> - **`t` is evaluated.**
> - **The null-test appropriate to `t`'s static type is applied: `!t.HasValue` when `t` has type `Nullable<V>`; otherwise `(object)t == null` (reference types, type parameters, and `dynamic`). For a type-parameter operand whose runtime instantiation is a non-nullable value type, the test is trivially false.**
> - **If the test indicates that `t` is null, the result of `await? t` is `default(X)`, where `X` is the result type of `await? t` determined by §11.8.8.3 — that is, a null reference when `X` is a nullable-annotated reference type, a `Nullable<R>` with `HasValue == false` when `X = Nullable<R>`, or *nothing* when `X` is *nothing* (the `void` case). No awaiter is obtained; `IsCompleted`, `OnCompleted`/`UnsafeOnCompleted`, and `GetResult` are not invoked; and the enclosing async function is not suspended at this expression.**
> - **Otherwise, run-time evaluation proceeds as for the ordinary `await` expression above, applied to the non-null value of `t` (when `t` has type `Nullable<V>`, the awaitable pattern is applied to `t.Value`), producing a value of type `R`, which is then implicitly converted to the result type `X`.**
>
> **Equivalently, `await? t` is semantically equivalent to:**
>
> - **`((object)t == null) ? default(X) : await t` — when `t`'s static type is not `Nullable<V>` (i.e., a reference type, a type parameter, or `dynamic`), or**
> - **`(!t.HasValue) ? default(X) : await t.Value` — when `t` has type `Nullable<V>`,**
>
> **with `t` evaluated only once and with `X` the result type determined by §11.8.8.3. The conversion from `R` (the type of `await t` / `await t.Value`) to `X` follows the ordinary conditional-expression type rules: when `R` is a non-nullable value type, `X = Nullable<R>` and the non-null branch is implicitly converted via the standard `R`-to-`Nullable<R>` conversion; when `R` is already a nullable value type, a reference type, or a type parameter, `X` and `R` agree (up to nullable-reference-type annotation) and no conversion is needed; when `R` is `void` the whole expression is classified as *nothing* and the equivalence is phrased at statement level rather than expression level.**

### Worked examples

The following tables are non-normative. They illustrate how the two rules above combine.

`await?` is defined in terms of the awaitable pattern ([§11.8.8.2](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11882-awaitable-expressions)) and not in terms of any specific BCL types. The framework types `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>` appear below purely as illustrative examples of, respectively, reference-type and value-type awaitables; an arbitrary user-defined `class RefAwaitable` or `struct StructAwaitable<T>` that satisfies §11.8.8.2 behaves identically to `Task` or `ValueTask<T>` with the same `GetResult()` return type. The rules apply uniformly.

**Table A — how the operand is null-tested and how the awaitable pattern is resolved (operand-nullability rule, §11.8.8.5 + §11.8.8.2):**

| Static type of `t` | Null test | Awaitable pattern applied to |
|---|---|---|
| Reference-type awaitable (e.g. `Task`, `Task<X>`, user `class RefAwaitable`) | `(object)t == null` | `t` |
| `Nullable<V>` where `V` is a value-type awaitable (e.g. `Nullable<ValueTask>`, `Nullable<ValueTask<X>>`, `Nullable<StructAwaitable<X>>`) | `!t.HasValue` | `t.Value` |
| Non-nullable value-type awaitable (e.g. `ValueTask`, `ValueTask<X>`, user `struct StructAwaitable<X>`) | — | — **(compile-time error)** |
| Type parameter `S` without a `struct` constraint whose underlying type is awaitable (includes `where S : class`, `where S : SomeBaseClass`, `where S : ISomething`, `where S : notnull`, unconstrained, …) | `(object)t == null` (trivially false at runtime for non-nullable value-type instantiations; the JIT is expected to elide it) | `t` |
| Type parameter `S` known to be a non-nullable value type (e.g. `where S : struct`, `where S : unmanaged`) | — | — **(compile-time error)** |
| `dynamic` | Runtime null-test on `t` | `t` |

**Table B — how the result type of `await? t` is computed from `R = GetResult()`'s return type (result-type rule, §11.8.8.3):**

| `R` | Classification of `await? t` |
|---|---|
| `void` | *nothing* — permitted only at statement position (as for *null_conditional_invocation_expression*, [§11.7.9](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1179-null-conditional-invocation-expression)) |
| Non-nullable value type (including a type parameter `where T : struct`) | `Nullable<R>` |
| `Nullable<V>` for some non-nullable value type `V` (e.g. `GetResult()` returns `Nullable<int>`) | `R` unchanged (e.g. `Nullable<int>`) |
| Reference type (including a type parameter `where T : class`) | `R` with nullable-reference-type annotation `R?` |
| Unconstrained type parameter | `R?` (default-able type-parameter annotation, C# 9+) |
| `dynamic` | `dynamic` |

The Task/ValueTask behaviors readers typically think about — `Task` → *nothing*; `Task<int>?` → `Nullable<int>`; `Task<Nullable<int>>?` → `Nullable<int>` via the already-nullable row; `Task<string>?` → `string?`; `Task<T>` where `T : struct` → `Nullable<T>`; unconstrained `Task<T>` → `T?`; `Nullable<ValueTask<int>>` → `Nullable<int>` — are all mechanical cross-products of the two tables above.

### Interaction and edge cases

- **Extension `GetAwaiter`** is supported; the awaitable-pattern resolution in §11.8.8.2 runs against the underlying type `U` exactly as for ordinary `await`, including the existing rules for extension-method `GetAwaiter` resolution.
- **`ConfigureAwait(false)`** returns a struct awaitable type (e.g. `ConfiguredTaskAwaitable`). Consequently, `await? task.ConfigureAwait(false)` is a compile-time error per §11.8.8.5 (non-nullable value-type operand). The intended spelling when `task` itself is nullable is `await? task?.ConfigureAwait(false)`: the inner `?.` produces a `Nullable<ConfiguredTaskAwaitable>`, which is a valid `await?` operand.
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

- [LDM 2017-02-21 — Null-coalescing assignments and awaits](https://github.com/dotnet/csharplang/blob/main/meetings/2017/LDM-2017-02-21.md#null-coalescing-assignments-and-awaits)
- [LDM 2022-08-31 — `await?`](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-08-31.md#await)
