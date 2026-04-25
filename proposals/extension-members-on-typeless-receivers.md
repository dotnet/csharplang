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

- **Null-conditional member access (`?.`).** No rule is added for `expr?.M(args)` or `expr?.P` when `expr` does not have a type, and the exclusion is intentional. `?.` exists to short-circuit member access when the receiver may be null. For typeless receivers that always produce a non-null value (collection expressions, lambdas and anonymous methods, tuple expressions, target-typed `new()`), `?.` would be equivalent to `.` and adds nothing. For typeless receivers that may evaluate to null (the `null` literal, a `default` literal target-typed to a reference or nullable value type, or a conditional or switch expression whose evaluated arm is `null`), `?.` short-circuits before any extension member could be invoked, making the call itself unreachable. Either way, the existing receiver-typing rules of [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access) and [§12.8.10](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12810-null-conditional-invocation-expression), which require the receiver to be a nullable value type or a reference type, continue to reject `?.` on a typeless receiver, which is the correct outcome.

- **Instance vs. extension precedence.** The rule that instance members take precedence over extension members is unchanged. Instance member lookup ([§12.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#125-member-lookup)) is defined in terms of a type and is not performed when the receiver expression has no type. Extension member resolution then runs without competition from an instance candidate, exactly as for any other receiver expression that already falls through to the extension path in [§12.8.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1287-member-access).

- **Method group receivers.** A method group that has a natural type per the [method group natural type improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/method-group-natural-type-improvements.md) is a typed receiver and binds via the existing path. A method group that does not have a natural type is a typeless receiver and enters this proposal's rule. The latter is the common case in practice: any method group whose candidates do not all share a single signature, including most overloaded API surfaces such as `Console.WriteLine`, `Math.Max`, and `string.Format`, has no natural type. The single-signature case (which does have a natural type) is unchanged by this proposal.

- **Type inference and target typing.** No new type-inference machinery is introduced. Inference proceeds via [§12.6.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1263-type-inference), invoked from the applicability check on the third eligibility bullet. When the receiver expression is a tuple expression with one or more typeless elements, a conditional expression whose arms do not have a common type, or a switch expression whose arms do not have a common type, the standard's existing target-typing rules ([§10.2.13](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10213-implicit-tuple-conversions) for tuples, plus the target-typed conditional and target-typed switch rules for arms) resolve the typeless components against each candidate's first parameter type. Target-typing is performed per candidate, as it already is for any other target-typed argument in overload resolution.

- **Dynamic.** Extension method invocation already does not apply when *expr* or any *args* has compile-time type `dynamic` ([§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations)). A receiver that has no type also does not have compile-time type `dynamic`, so the existing exclusion is unchanged.

- **Throw expressions.** A *throw_expression* has no type ([§12.16](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1216-the-throw-expression-operator)) and would otherwise be admitted by the typeless branch of the new rule. This proposal excludes throw expressions from the accepted receiver forms because evaluation of a throw expression terminates control flow before any extension member could be invoked, making the call unreachable. See [Which receiver categories are supported?](#which-receiver-categories-are-supported) for the full per-category list.

## Back-compat analysis

This is a pure extension. The new branch of the receiver eligibility test in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) and the corresponding update to [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) admit only receiver expressions that have no type, which under the existing rules of those sections are already binding-time errors. No expression that compiles today changes meaning. Any program that compiled before this feature continues to compile, with identical resolution and semantics.

## Drawbacks

The proposal expands the candidate set considered for member access on a typeless receiver. For users, this can convert what is currently a clear "expression has no type" error into a less direct overload-resolution error when more than one in-scope extension is applicable. For implementations, applicability of each candidate may require target-typing the receiver expression against that candidate's first parameter type, which is the same machinery already used for typeless arguments in non-receiver positions but is invoked more often under this rule. Both costs are small relative to the ergonomic gain in the motivating cases, but they are not zero.

## Alternatives

- **Do nothing.** Users continue to either spell out the target type with a cast (`var a = (ImmutableArray<int>)[1, 2, 3];`) or call the extension as a static method (`var a = Enumerable.ToImmutableArray([1, 2, 3]);`). Both work today, both are uglier than the dotted form, and neither helps the typeless-lambda or method-group cases.

- **Postfix cast.** A separate proposal to allow a postfix cast spelling could let users write a less awkward variation on the cast form for the collection-expression case, but does not enable extension dispatch on typeless receivers in general and therefore does not address the lambda, method-group, or conditional-expression cases.

- **Partial generic inference.** A separate proposal to allow partial generic inference (for example, omitting some type arguments while supplying others) could relieve the verbosity of the static-method form for the collection-expression case. As with postfix cast, it is narrower than this proposal and does not help typeless lambdas, method groups, or conditional expressions.

- **Target-typed static member access ([dotnet/csharplang#9138](https://github.com/dotnet/csharplang/issues/9138)).** A separate ergonomic for invoking static or extension members of a known target type without naming the type. Different machinery, applicable only when the call site already supplies a target type, and orthogonal to the present proposal.

## Design decisions

### Why apply the rule uniformly across all typeless receiver categories?

Collection expressions are the headline driver for this feature, but the rule that admits them, namely *"the receiver has no type, so the existing identity / reference / boxing requirement does not apply"*, is not specific to collection expressions. The same rule, written once in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) and once in [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption), automatically admits null literals, default literals, target-typed `new()`, anonymous functions without a natural type, method groups without a natural type, conditional expressions without a common arm type, switch expressions without a common arm type, and tuple expressions with at least one typeless element. Carving the rule down to one or two categories would require additional spec text to enumerate the excluded forms, and would have to be revisited when the next typeless expression form is introduced. The uniform rule is the smaller, more durable spec.

The categories that LDM might prefer to dial back are surfaced individually in [Which receiver categories are supported?](#which-receiver-categories-are-supported); the proposal's structure makes any per-category exclusion a single-clause edit.

### Why reuse the existing extension resolution machinery rather than introduce a new path?

The existing applicability check in [§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12642-applicable-function-member), invoked from the third eligibility bullet of [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations), already produces the correct answer for typeless first arguments via the standard's existing expression-to-type implicit conversions ([§10.2.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1027-null-literal-conversions), [§10.2.13](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10213-implicit-tuple-conversions), [§10.2.15](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10215-anonymous-function-conversions-and-method-group-conversions), [§10.2.16](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10216-default-literal-conversions), [§10.2.17](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10217-implicit-throw-conversions)). Once the fourth eligibility bullet's identity / reference / boxing narrowing is removed for the typeless case, the rest falls out at no cost. Inventing a separate resolution path for typeless receivers would duplicate inference, applicability, and overload resolution logic, and would risk drifting from the typed-receiver path in subtle ways. Reusing the existing machinery keeps the two paths convergent by construction.

### Why frame the rule as "receiver as first argument"?

The classic extension method invocation in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) already specifies eligibility in terms of the static-method form `C.identifier(expr, args)`, where `expr` is the first argument. The C# 14 [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) rule similarly uses the actual receiver as one of the arguments to its full generic type inference. The "receiver as first argument" framing is therefore not an invention of this proposal; it is the existing model in both spec sources, and the proposal simply lifts the precondition that the first argument have a type.

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
