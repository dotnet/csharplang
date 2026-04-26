# Extension members on typeless receivers

## Summary

Allow extension members to be invoked on a receiver expression that has no type:

```cs
// All errors today.
var a = [1, 2, 3].ToImmutableArray();
var memoized = SomeMethod.Memoize();
var x = (cond ? null : GetInt()).SomeNullableExtension();
```

Resolution treats the receiver as the first argument of each candidate extension member, then picks the best applicable candidate by the existing overload-resolution rules. Receivers that already have a type bind by the existing rules, unchanged.

## Motivation

Collection expressions ([proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md)) are the primary motivating scenario. A collection expression has no type, so it cannot be the receiver of any extension member, even where the conversion from the collection expression to the extension's first parameter type is well-defined and the extension member is the canonical way to express the operation:

```cs
// All of these are binding-time errors today.
var a = [1, 2, 3].ToImmutableArray();
var b = [1, 2, 3].ToList();
var c = ["one", "two"].ToHashSet();
```

The workaround today is to spell out the target type with a cast (or with a typed declaration target):

```cs
var a = (ImmutableArray<int>)[1, 2, 3];
```

That works, but it forces the developer to write the full target type at the point of construction even when generic inference on the called method would otherwise determine it. The cost grows with the genericity of the type:

```cs
// Today.
var values = (ImmutableArray<Some<Complex*[], (Set, Of?, Types)>>)[x, y, z];

// Proposed; the type argument of ToImmutableArray<T> is inferred from x, y, z.
var values = [x, y, z].ToImmutableArray();
```

The same blocker affects LINQ query syntax. A query like `from x in source ...` is rewritten as a member-access invocation on `source` (`source.Where(...)`, `source.Select(...)`, and so on). For most sources, the methods that satisfy the rewrite are extension methods on `IEnumerable<T>`, but member lookup is gated on `source` having a type, so query syntax over a typeless source is rejected before extension lookup can run:

```cs
// Error: [1, 2, 3, 4] has no type, so the rewritten .Where(...).Select(...) chain fails to bind.
var bigDoubled = from x in [1, 2, 3, 4]
                 where x > 1
                 select x * 2;
```

Under this proposal the same query binds to the existing LINQ extensions on `IEnumerable<T>`, with the source target-typed as it would be in the equivalent fluent chain `[1, 2, 3, 4].Where(x => x > 1).Select(x => x * 2)`.

The same shape arises for other expressions that have no type. These are not the headline driver, but the rule we propose is uniform across all such expressions, so they fall out at no additional spec cost. LDM can dial individual categories back via the [open question](#which-receiver-categories-are-supported) below if any prove unwanted.

```cs
// Lambdas whose parameter types are not specified.
var memoized1 = ((x, y) => Compute(x, y)).Memoize();

// Method groups.
var memoized2 = SomeMethod.Memoize();

// Conditional expressions whose arms do not have a common type.
var x = (cond ? null : GetInt()).SomeNullableExtension();
```

Method groups in particular cover a broad set of real-world APIs. The natural-type rule from [method group natural type improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/method-group-natural-type-improvements.md) only applies when all candidates in the group share a single signature, so most overloaded methods (`Console.WriteLine`, `Math.Max`, `string.Format`) have no natural type and become reachable for extension dispatch under this rule.

LDM has previously identified this as a separate track to explore, distinct from solving the collection-expression case in isolation ([LDM-2023-07-12](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-12.md)). This proposal is that track.

## Detailed design

The following updates are presented as a diff against [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) of the C# 7 standard, and against the [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) section of the [C# 14 extension members](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md) proposal.

Throughout this section, ~~strikethrough~~ indicates text being removed from the existing specification, and **bold** indicates text being added. Unchanged prose is quoted verbatim for context.

Examples in this section assume the merged language formed by standard-v7 plus the relevant post-v7 feature specifications, notably the [C# 12 collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md). The diffs themselves introduce no new conversion form; whichever implicit conversion the merged language defines from a typeless receiver to a parameter type is the one this proposal relies on.

### Extension method invocations

The receiver-side eligibility constraint in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) is relaxed to admit a receiver expression that does not have a type. The existing identity / reference / boxing requirement is preserved verbatim for receivers that do have a type.

The prose in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) is updated as follows.

> An extension method `Cᵢ.Mₑ` is ***eligible*** if:
>
> - `Cᵢ` is a non-generic, non-nested class
> - The name of `Mₑ` is *identifier*
> - `Mₑ` is accessible and applicable when applied to the arguments as a static method as shown above
> - ~~An implicit identity, reference or boxing conversion exists from *expr* to the type of the first parameter of `Mₑ`.~~ **One of the following holds:**
>   - **An implicit identity, reference or boxing conversion exists from *expr* to the type of the first parameter of `Mₑ`.**
>   - ***expr* does not have a type.**

The first sub-bullet is the existing eligibility rule, preserved verbatim. Although its prose names *expr* rather than the type of *expr*, each of the three named conversions in the standard is defined to require a source type: [§10.2.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1022-identity-conversion) describes the identity conversion as one that holds for a type, or for an expression of that type; [§10.2.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1028-implicit-reference-conversions) lists the implicit reference conversions as a closed set of clauses each starting from a source type; and [§10.2.9](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1029-boxing-conversions) defines the boxing conversion as one from a value type to a reference type. An expression that does not have a type does not satisfy the source-side prerequisite of any of these forms, so the first sub-bullet does not apply when *expr* has no type.

The second sub-bullet does not need to restate any constraint on *expr* beyond the absence of a type. The third eligibility bullet (*"`Mₑ` is accessible and applicable when applied to the arguments as a static method"*) already invokes the applicability rules of [§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12642-applicable-function-member), which in turn require that "*an implicit conversion exists from the argument expression to the type of the corresponding parameter*". For a typeless first argument that test is satisfied (or not) by the standard's existing expression-to-type implicit conversions: [§10.2.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1027-null-literal-conversions) (null literal), [§10.2.13](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10213-implicit-tuple-conversions) (tuple expression), [§10.2.15](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10215-anonymous-function-conversions-and-method-group-conversions) (anonymous function and method group), [§10.2.16](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10216-default-literal-conversions) (default literal), [§10.2.17](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10217-implicit-throw-conversions) (implicit throw). No new conversion form is introduced by this proposal; the fourth bullet's narrowing to identity, reference, or boxing simply does not apply when *expr* has no type, and removing it for that case is the entirety of the change.

The remaining prose of [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) is unchanged. In particular, the rule that extension methods do not apply when *expr* or any of the *args* has compile-time type `dynamic` is unaffected, because an expression that does not have a type also does not have compile-time type `dynamic`.

> *Example*: Given the standard library declaration
>
> ```csharp
> public static class Enumerable
> {
>     public static ImmutableArray<T> ToImmutableArray<T>(this IEnumerable<T> source) { ... }
> }
> ```
>
> the expression
>
> ```csharp
> [1, 2, 3].ToImmutableArray()
> ```
>
> is processed as follows. The receiver `[1, 2, 3]` is not a namespace, is not classified as a type, and is not a property access, indexer access, variable, or value with a type, so member access in [§12.8.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1287-member-access) falls through to extension method invocation ([§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations)). In [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations), the candidate `Enumerable.ToImmutableArray<T>` is eligible:
>
> - `Enumerable` is a non-generic, non-nested class.
> - The name of the candidate is `ToImmutableArray`.
> - The candidate is accessible and applicable as the static call `Enumerable.ToImmutableArray([1, 2, 3])`: type inference yields `T` = `int`, and an implicit conversion from `[1, 2, 3]` to `IEnumerable<int>` exists.
> - `[1, 2, 3]` does not have a type, satisfying the second branch of the receiver bullet.
>
> Overload resolution then selects this candidate, and the result is a value of type `ImmutableArray<int>`.
>
> *end example*

### Extension member consumption

The receiver-side compatibility test in [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) is updated analogously: a candidate whose receiver type is `T` is no longer discarded merely because the receiver expression has no type. The same identity / reference / boxing restriction is preserved for receivers that do have a type.

The prose in [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) is updated as follows.

> When an extension member lookup is attempted, all extension declarations within static classes that are `using`-imported contribute their members as candidates, regardless of receiver type. Only as part of resolution are candidates with incompatible receiver types discarded. **A candidate with receiver type `T` is compatible if either the receiver expression has a type and an implicit identity, reference, or boxing conversion exists from that type to `T`, or the receiver expression does not have a type.**
>
> A full generic type inference is attempted between the type of the arguments (including the actual receiver) and any type parameters (combining those in the extension declaration and in the extension member declaration).
>
> When explicit type arguments are provided, they are used to substitute the type parameters of the extension declaration and the extension member declaration.

The added compatibility sentence preserves the identity / reference / boxing restriction that classic extension method invocation already imposes ([§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations)), so a typed receiver continues to bind under the existing rules, unchanged. When the receiver expression does not have a type, the receiver-type compatibility check imposes no further constraint of its own; type inference and applicability proceed using the receiver expression directly, supported by the standard's existing expression-to-type implicit conversions ([§10.2.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1027-null-literal-conversions) null literal, [§10.2.13](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10213-implicit-tuple-conversions) tuple expression, [§10.2.15](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10215-anonymous-function-conversions-and-method-group-conversions) anonymous function and method group, [§10.2.16](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10216-default-literal-conversions) default literal, [§10.2.17](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10217-implicit-throw-conversions) implicit throw), as for any other typeless argument.

This rule applies uniformly to every form of extension member declared inside an `extension(T) { ... }` block: extension methods, extension properties, and extension indexers. The receiver expression form is unchanged in each case (`expr.M(args)` for a method, `expr.P` for a property, `expr[args]` for an indexer); the only change is which receiver expressions are admitted.

> *Example*: Given the extension declaration
>
> ```csharp
> public static class ListExtensions
> {
>     extension<T>(IReadOnlyList<T> list)
>     {
>         public T First => list[0];
>     }
> }
> ```
>
> the expression
>
> ```csharp
> var first = [1, 2, 3].First;
> ```
>
> is processed as follows. The receiver `[1, 2, 3]` does not have a type, so the typeless branch of the compatibility test admits the candidate `First` declared on the extension type with receiver type `IReadOnlyList<T>`. Generic type inference yields `T` = `int`, and an implicit conversion from `[1, 2, 3]` to `IReadOnlyList<int>` exists. Resolution selects this candidate, and the result is a value of type `int`.
>
> *end example*

### Interactions with other features

- **Instance vs. extension precedence.** Unchanged. Instance member lookup ([§12.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#125-member-lookup)) requires a type and so does not run on a typeless receiver; extension resolution then runs without competition, just as it already does for any receiver that falls through via [§12.8.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1287-member-access).

- **Method group receivers.** A method group with a natural type per the [method group natural type improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/method-group-natural-type-improvements.md) binds via the existing path. A method group without a natural type (the common case in practice, including most overloaded API surfaces such as `Console.WriteLine`, `Math.Max`, and `string.Format`) enters this proposal's rule. The single-signature case is unchanged.

- **Type inference and target typing.** No new machinery. The receiver is target-typed against each candidate's first parameter type by the existing applicability check ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12642-applicable-function-member)) and inference ([§12.6.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1263-type-inference)); typeless tuples, conditionals, and switches use the standard's existing per-element rules. The proposal lifts only the receiver-typing precondition.

- **Dynamic.** Extension method invocation already does not apply when *expr* or any *args* has compile-time type `dynamic` ([§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations)). A typeless receiver does not have type `dynamic` either, so the existing exclusion is unchanged.

- **Throw expressions.** A *throw_expression* has no type and would otherwise be admitted by the typeless branch, but is excluded because evaluation terminates control flow before any extension member could be invoked. See [Which receiver categories are supported?](#which-receiver-categories-are-supported).

- **Ref expressions.** No interaction. Every ref-flavored expression has a type: a ref conditional `a ? ref b : ref c` requires identity-convertible arms; switch has no ref form; and ref-returning method calls, ref locals, ref properties, ref indexers, and ref fields are all typed.

## Back-compat analysis

This is a pure extension. The new branch of the receiver eligibility test in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) and the corresponding update to [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) admit only receiver expressions that have no type, which under the existing rules of those sections are already binding-time errors. No expression that compiles today changes meaning. Any program that compiled before this feature continues to compile, with identical resolution and semantics.

## Alternatives

- **Do nothing.** Users continue to either spell out the target type with a cast (`var a = (ImmutableArray<int>)[1, 2, 3];`) or call the extension as a static method (`var a = Enumerable.ToImmutableArray([1, 2, 3]);`). Both work today, both are uglier than the dotted form, and neither helps the typeless-lambda or method-group cases.

- **Partial generic inference.** A separate proposal to allow partial generic inference (for example, `((ImmutableArray<_>)[a, b, c])` where the element type is inferred) could relieve some of the verbosity of the explicit-cast form for the collection-expression case. It is narrower than this proposal and does not help typeless lambdas, method groups, or conditional expressions.

## Design decisions

### Why apply the rule uniformly across all typeless receiver categories?

Collection expressions are the headline driver for this feature, but the rule that admits them, namely *"the receiver has no type, so the existing identity / reference / boxing requirement does not apply"*, is not specific to collection expressions. The same rule, written once in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) and once in [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption), automatically admits null literals, default literals, target-typed `new()`, anonymous functions without a natural type, method groups without a natural type, conditional expressions without a common arm type, switch expressions without a common arm type, and tuple expressions with at least one typeless element. Carving the rule down to one or two categories would require additional spec text to enumerate the excluded forms, and would have to be revisited when the next typeless expression form is introduced. The uniform rule is the smaller, more durable spec.

The categories that LDM might prefer to dial back are surfaced individually in [Which receiver categories are supported?](#which-receiver-categories-are-supported); the proposal's structure makes any per-category exclusion a single-clause edit.

## Open LDM questions

### Which receiver categories are supported?

This proposal admits any receiver expression that has no type. Concretely, the set is:

- The null literal (`null`).
- The default literal (`default`).
- A target-typed object creation (`new()` or `new(args)`, that is, the form without an explicit type name).
- An anonymous function (lambda or anonymous method) that does not have a natural type, including lambdas with at least one parameter whose type is omitted.
- A method group that does not have a natural type per the [method group natural type improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/method-group-natural-type-improvements.md).
- A collection expression.
- A conditional expression (`cond ? a : b`) whose arms do not have a common type.
- A switch expression whose arms do not have a common type.
- A tuple expression with at least one element that itself has no type.

A throw expression (`throw ...`) was excluded from the set because evaluation of a throw expression terminates control flow before any extension member could be invoked, making the call unreachable. The exclusion is reconsiderable.

LDM is asked to confirm or veto each category individually. The proposal's body is structured so that any subset can be carved out without changing the rule itself: a category-specific exclusion is a single clause added to the receiver eligibility text in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) and the corresponding compatibility sentence in [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption).

Recommendation: admit all categories except throw expressions. Each individual category falls out of the new rule at no additional spec cost, and the dial-back-on-demand structure means LDM can revisit any of them later with minimal disruption.

## Optional follow-on: null-conditional access on typeless receivers

This section is not part of the present proposal. It is offered for LDM to decide whether to schedule as a separate, follow-on feature. The "Interactions with other features" bullet on `?.` already gestures at this; the section below makes the design concrete enough to discuss.

### Motivation

The present proposal admits typeless receivers for `expr.M(args)` but leaves `expr?.M(args)` untouched. That is correct given the existing receiver-typing rules of [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access), which require the receiver to be a nullable value type or a reference type. Yet there are realistic cases where the typeless receiver would, under the rules of this proposal, acquire a type that is in fact a nullable value type or a reference type, and `?.` is the natural way to write the call:

```cs
// ExtensionMethod is declared as `extension(int? x) { public void ExtensionMethod() { ... } }`
(a ? null : this.SomeValueTypeProp)?.ExtensionMethod();
```

The receiver here has no type. If the same expression were used as a regular argument, as in `ExtensionMethod(a ? null : this.SomeValueTypeProp)`, overload resolution and the target-typed conditional rules would yield the receiver type `int?`. With that type in hand, `?.` is well-defined and means exactly what the user intends.

### Conceptual model

For `P?.A` where `P` does not have a type, the receiver type `Tp` is the type that `P` would acquire under the corresponding non-null-conditional access `P.A` per the rules of this proposal (i.e., the first parameter type of the candidate that overload resolution selects for `.A`).

- If no candidate is selected, the access is a binding-time error, exactly as for `P.A`.
- If `Tp` is a non-nullable value type, the access is a binding-time error. `?.` is not a meaningful operation against a non-null receiver.
- Otherwise (`Tp` is a nullable value type, a reference type, or a type parameter not known to be a non-nullable value type), the meaning of `P?.A` is the same as that of the existing rules in [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access), with `P` treated as if it had been written with type `Tp`.

### Spec change shape

Add a third top-level branch to [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access), placed after the existing "Otherwise":

> *Otherwise, if `P` does not have a type and is a typeless receiver form admitted by [extension members on typeless receivers](https://github.com/dotnet/csharplang/blob/main/proposals/extension-members-on-typeless-receivers.md):*
>
> Let `Tp` be the receiver type of the candidate selected by overload resolution for the corresponding non-null-conditional access `P.A` per the rules of that proposal. If no candidate is selected, a compile-time error occurs.
>
> - If `Tp` is a non-nullable value type, a compile-time error occurs.
> - Otherwise, the meaning of `E` is determined by applying the rules of this section as if `P` were of type `Tp`, with every occurrence of `P` in those rules replaced by the conversion of the typeless `P` to `Tp`.

[§12.8.10](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12810-null-conditional-invocation-expression) inherits the new branch via composition and needs no direct change.

The bullet on `?.` in [Interactions with other features](#interactions-with-other-features) above would be updated to point at this branch instead of describing the behavior as out of scope.

### Which typeless receiver forms should be admitted?

The receiver forms admitted by the present proposal divide cleanly into two groups for the purposes of `?.`:

- **May evaluate to null.** Conditional expressions whose arms do not have a common type (e.g., `(a ? null : SomeIntMethod())`), switch expressions whose arms do not have a common type, the `null` literal, and the `default` literal target-typed to a nullable value type or reference type. Here `?.` does real work: the short-circuit can fire at runtime.
- **Cannot evaluate to null.** Collection expressions, lambdas and anonymous methods, method groups, target-typed `new()`, and tuple expressions. Here `?.` is identity to `.`. The user gains nothing, and the form invites confusion (a reader might assume the receiver could be null).

Recommendation: admit the may-evaluate-to-null forms only, and limit the initial scope further to **conditional and switch expressions**. The `null` and `default` literals are accepted by the spec change above as a side effect of the uniform rule, but the resulting calls always short-circuit and are dead code. They can be left in (the rule is uniform), excluded explicitly (a single clause carve-out), or admitted with a warning. LDM should decide.

The cannot-evaluate-to-null forms should be excluded by an explicit clause. The carve-out is a single sentence in the new branch ("typeless receiver forms ... excluding *collection_expression*, *anonymous_function*, *method_group*, *object_creation_expression* with no type, and *tuple_expression*").

### Edge cases worth flagging for LDM

- **Multi-link chains.** `P?.A?.B` where `P` is typeless. The outer `?.` resolves under the new rule and produces a typed result; the inner `?.B` then binds against that type with no novelty. The new branch is entered only at the outermost typeless link.

- **Element access (`P?[i]`).** The same conceptual model applies (target-type `P` from the candidate's first parameter type), but extension indexers are a separate path in the existing spec and would need the analogous addition to [§12.8.12](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12812-null-conditional-element-access). LDM should decide whether to land in lockstep with `?.` or as an additional follow-on.

- **Null-conditional assignment (C# 14: `P?.X = v`, `P?.X += v`).** The rule composes naturally: `Tp` is the receiver type of the selected candidate property/event accessor, and `?.` short-circuits before the assignment is performed. Worth confirming explicitly so the C# 14 feature and this follow-on are known to compose.

### Backward compatibility

Pure extension. Today every typeless `?.` is a binding-time error, so no code that compiles today changes meaning.

### Recommendation to LDM

Land the present (non-`?.`) proposal first. Treat this section as a separate decision: schedule it as a follow-on if and when the non-`?.` form ships and there is field evidence that conditional and switch receivers in `?.` position are a real ergonomic pain. The initial scope should be conditional and switch expressions; the `null`/`default` literal cases and element access can follow once that lands.

## Optional follow-on: `foreach` and spread on typeless receivers via extension `GetEnumerator`

This section is not part of the present proposal. It is offered for LDM to decide whether to schedule as a separate, follow-on feature, in the same spirit as the `?.` follow-on above.

### Motivation

The present proposal admits typeless receivers for `expr.M(args)` but leaves the iteration forms `foreach (var v in expr)` and `[.. expr]` untouched. With a single extension `GetEnumerator` on `IEnumerable<T>` in scope, the natural way to write the loop or the spread on a typeless source is the typeless form:

```cs
extension<T>(IEnumerable<T> source)
{
    public IEnumerator<T> GetEnumerator() { ... }
}

// Today: error - [a, b, c] has no type, so the foreach algorithm rejects it
// before any extension GetEnumerator lookup runs.
foreach (var v in [a, b, c]) { ... }

// Today: error - same reason; the spread defers to foreach for iteration-type
// determination, and foreach rejects the typeless source.
List<int> sink = [x, y, .. cond ? [a, b] : [c, d]];
```

The natural symmetry argument is the same as for the present proposal's headline cases: `expr.M()` works on a typeless `expr` under this proposal, so `foreach (var v in expr)` (which is, modulo syntactic sugar, `expr.GetEnumerator()` plus a loop) and `[.. expr]` (which is `foreach` plus an `Add`) ought to work too whenever `GetEnumerator` is found by the same lookup.

### The problem

The C# 9 [extension `GetEnumerator`](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/extension-getenumerator.md) feature added an extension fallback to the `foreach` algorithm in [§13.9.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/statements.md#1395-the-foreach-statement), but every branch of that algorithm keys on *the type `X` of the expression*. With no type `X`, no branch fires, and the algorithm never reaches the C# 9 extension lookup. Spread elements inherit the same restriction: the C# 12 [collection-expressions speclet](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md) defines the spread's iteration type by deferring to the `foreach` algorithm, so as long as `foreach` rejects typeless sources, spread does too.

The present proposal does not, on its own, fix either case. It modifies the receiver-eligibility text in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) and the C# 14 [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) rule for member access, but `foreach` and spread reach extension `GetEnumerator` through their own algorithm, not through member access on the source expression.

### Conceptual model

Add a final fallback to the `foreach` algorithm: when the source expression has no type but is a typeless receiver form admitted by the present proposal, run extension method lookup for the identifier `GetEnumerator` against the typeless source using the present proposal's machinery. Each candidate's first parameter type is the prospective receiver target type; the typeless source is tried for implicit conversion against it via the same per-candidate target-typing the present proposal already performs. If overload resolution selects a unique best candidate, the *collection type* is that candidate's first parameter type, the source is target-typed to it, and the rest of the `foreach` algorithm proceeds against that type.

Spread inherits the change for free. The collection-expressions speclet defines the iteration type of `..s` by reference to the `foreach` algorithm, so as soon as `foreach` admits the typeless fallback, `..` does too.

### Spec change shape

Add a final fallback bullet to [§13.9.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/statements.md#1395-the-foreach-statement), placed after the C# 9 extension-`GetEnumerator` fallback:

> *Otherwise, if the expression does not have a type and is a typeless receiver form admitted by [extension members on typeless receivers](https://github.com/dotnet/csharplang/blob/main/proposals/extension-members-on-typeless-receivers.md):*
>
> Run extension method lookup for the identifier `GetEnumerator` with the typeless source expression treated as the first argument, per the rules of that proposal. If overload resolution selects no single best candidate, a compile-time error occurs. Otherwise, the *collection type* is the candidate's first parameter type `T`, the source expression is target-typed to `T`, and the algorithm continues with the chosen candidate as the `GetEnumerator` method.

The C# 12 [collection-expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md) speclet needs no direct change: the spread's iteration-type rule already defers to `foreach`, so any change to §13.9.5 is inherited at the spread site.

### Pros and cons

- **Pro: slots cleanly into the present proposal.** No new lookup machinery and no new conversions. The existing per-candidate target-typing the present proposal introduces is reused unchanged. The spec change is one paragraph in §13.9.5; spread follows by deferral.
- **Pro: handles the typeless-arm spread case.** The case `[a, b, .. cond ? [c, d] : [e, f]]` is explicitly deferred to "future considerations" by the [immediately-enumerated-collection-expressions proposal](https://github.com/dotnet/csharplang/blob/main/proposals/immediately-enumerated-collection-expressions.md), which keys on the source expression already being a collection expression. The present follow-on subsumes that case whenever an applicable extension `GetEnumerator` is in scope, because the conditional source is admitted as a typeless receiver in its own right.
- **Con: requires a user-supplied (or BCL-supplied) extension.** The fallback only finds something if there is an extension `GetEnumerator` whose first parameter the typeless source can be implicitly converted to. A single declaration on `IEnumerable<T>` (in the BCL or in user code) is enough to make the bare `foreach (var v in [1, 2, 3])` and the typeless-arm spread cases work, but in the absence of any such extension this follow-on does nothing on its own. By contrast, the [immediately-enumerated-collection-expressions proposal](https://github.com/dotnet/csharplang/blob/main/proposals/immediately-enumerated-collection-expressions.md) bakes `IEnumerable<T>` in at the language level for collection-expression sources specifically, so it works without any extension declaration.

### Relationship to immediately-enumerated-collection-expressions

The two proposals are complementary, not redundant. The [immediately-enumerated-collection-expressions proposal](https://github.com/dotnet/csharplang/blob/main/proposals/immediately-enumerated-collection-expressions.md) bakes in `IEnumerable<T>` as the target type for typeless collection expressions specifically in `foreach` and spread positions, and requires no user-defined extension. This follow-on instead adds extension `GetEnumerator` lookup against the typeless source, requiring a user-defined extension but covering every typeless receiver form admitted by the present proposal (not just collection expressions) and subsuming the conditional and switch arm spread cases that the immediately-enumerated proposal explicitly defers. Either could ship without the other; if both ship, the immediately-enumerated conversion is checked first and this follow-on's fallback runs only when it doesn't apply, so the two paths compose cleanly.

### Backward compatibility

Pure extension. Today every typeless `foreach` source and every typeless spread source is a binding-time error, so no code that compiles today changes meaning.

### Recommendation to LDM

Land the present (member-access) proposal first. Treat this section as a separate decision: schedule it as a follow-on if and when LDM wants the typeless-receivers theme to extend symmetrically to `foreach` and spread. The narrow scope (extension `GetEnumerator` only) is the cheapest spec change consistent with the present proposal's framing. A broader option that target-types typeless sources to types with *instance* `GetEnumerator` shapes is a different shape of feature, overlaps substantially with the immediately-enumerated proposal's core, and is best discussed there.

## Related discussions

- [Proposal: Collection expressions (champion #5354)](https://github.com/dotnet/csharplang/issues/5354). The C# 12 collection-expressions champion issue.
- [Collection expressions: Extension methods](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md). Speclet text noting that a collection expression cannot be used directly as the first parameter for an extension method invocation under the current rules.
- [Dictionary expressions: Extension methods](https://github.com/dotnet/csharplang/blob/main/proposals/dictionary-expressions.md). Same restriction for dictionary expressions; this proposal removes both in one rule.
- [Collection Expressions Next (champion #7913)](https://github.com/dotnet/csharplang/issues/7913) and [discussion #8660](https://github.com/dotnet/csharplang/discussions/8660). Umbrella that lists "Extension methods on collections" as a backlog item.
- [Extension members (champion #8697)](https://github.com/dotnet/csharplang/issues/8697) and [discussion #8696](https://github.com/dotnet/csharplang/discussions/8696). The C# 14 extension members work that this proposal updates.
- [dotnet/csharplang#9688](https://github.com/dotnet/csharplang/issues/9688). Separate question about extension methods participating in collection-expression construction; different binding site, out of scope here.

## Design meetings

This proposal has not yet been reviewed by LDM. Prior LDM and working-group discussions that inform the design:

- [LDM 2023-07-12](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-12.md). Identified *"allow extension methods on expressions with no natural type"* as a separate orthogonal track to the collection-expressions-only ergonomic; this proposal is that track.
- [Collection literals working group, 2023-06-26](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2023-06-26.md). The `[complex, type, examples].AsImmutableArray()` motivating scenario, with the broader claim that null, lambda, and method-group receivers should also participate.
- [Collection literals working group, 2024-01-23](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2024-01-23.md). Concrete `(x => true).ExtensionOnStringPredicate()` example and explicit framing of the feature as extension methods on target-typed constructs.
