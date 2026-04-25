# Extension members on typeless receivers

## Summary

Allow extension members to be invoked on a receiver expression that has no type:

```cs
// All errors today.
ImmutableArray<int> a = [1, 2, 3].ToImmutableArray();
var memoized = SomeMethod.Memoize();
var x = (cond ? null : GetInt()).SomeNullableExtension();
```

Resolution treats the receiver as the first argument of each candidate extension member, then picks the best applicable candidate by the existing overload-resolution rules. Receivers that already have a type bind exactly as today.

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

LDM has previously identified this as a separate track to explore, distinct from solving the collection-expression case in isolation ([LDM-2023-07-12](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-12.md)). This proposal is that track.

## Detailed design

The following updates are presented as a diff against [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) of the C# 7 standard, and against the [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) section of the [C# 14 extension members](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md) proposal.

Throughout this section, ~~strikethrough~~ indicates text being removed from the existing specification, and **bold** indicates text being added. Unchanged prose is quoted verbatim for context.

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

The first sub-bullet is the existing eligibility rule, preserved verbatim. Although its prose names *expr* rather than the type of *expr*, the three named conversions are each defined in the standard as relations between two types: [§10.2.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1022-identity-conversion) defines the identity conversion as one that "*converts from any type to the same type*", [§10.2.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1028-implicit-reference-conversions) lists the implicit reference conversions as a closed set of clauses each starting from a source type, and [§10.2.9](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1029-boxing-conversions) defines the boxing conversion as a conversion from a value type to a reference type. None of the three is defined for a source expression that does not have a type, so the first sub-bullet is unreachable when *expr* has no type.

The second sub-bullet does not need to restate any constraint on *expr* beyond the absence of a type. The third eligibility bullet (*"`Mₑ` is accessible and applicable when applied to the arguments as a static method"*) already invokes the applicability rules of [§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12642-applicable-function-member), which in turn require that "*an implicit conversion exists from the argument to the type of the corresponding parameter*". For a typeless first argument that test is satisfied (or not) by the standard's existing expression-to-type implicit conversions: [§10.2.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1027-null-literal-conversions) (null literal), [§10.2.13](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10213-implicit-tuple-conversions) (tuple expression), [§10.2.15](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10215-anonymous-function-conversions-and-method-group-conversions) (anonymous function and method group), [§10.2.16](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10216-default-literal-conversions) (default literal), [§10.2.17](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10217-implicit-throw-conversions) (implicit throw). No new conversion form is introduced by this proposal; the fourth bullet's narrowing to identity, reference, or boxing simply does not apply when *expr* has no type, and removing it for that case is the entirety of the change.

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
> is processed as follows. Member access in [§12.8.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1287-member-access) falls through to extension method invocation ([§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations)), because `[1, 2, 3]` is not classified as a namespace, a type, or a value, variable, property access, or indexer access whose type is a *type*. In [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations), the candidate `Enumerable.ToImmutableArray<T>` is eligible:
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

The added compatibility sentence preserves the identity / reference / boxing restriction that classic extension method invocation already imposes ([§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations)), so a typed receiver continues to bind exactly as today. When the receiver expression does not have a type, the receiver-type compatibility check imposes no further constraint of its own; type inference and applicability proceed using the receiver expression directly, supported by the standard's existing expression-to-type implicit conversions ([§10.2.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#1027-null-literal-conversions) null literal, [§10.2.13](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10213-implicit-tuple-conversions) tuple expression, [§10.2.15](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10215-anonymous-function-conversions-and-method-group-conversions) anonymous function and method group, [§10.2.16](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10216-default-literal-conversions) default literal, [§10.2.17](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10217-implicit-throw-conversions) implicit throw), as for any other typeless argument.

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

- **Null-conditional member access (`?.`).** This proposal adds no rule for `expr?.M(args)` or `expr?.P` when `expr` does not have a type. The receiver-typing rules of [§12.8.8](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1288-null-conditional-member-access) and [§12.8.10](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12810-null-conditional-invocation-expression) require the receiver to be a nullable value type or a reference type. Typeless receivers do not satisfy that requirement, so `?.` continues to be rejected on a typeless receiver, exactly as today.

- **Element access (`expr[args]`).** An extension indexer is reached via the element-access form `expr[args]`, and eligibility under this proposal follows the same rule introduced in [Extension member consumption](#extension-member-consumption): a typeless `expr` is admitted when the extension indexer's receiver type is compatible by the new rule. Element access on a typeless receiver that does not resolve to an extension indexer remains a binding-time error, as today.

- **Instance vs. extension precedence.** The rule that instance members take precedence over extension members is unchanged. Instance member lookup ([§12.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#125-member-lookup)) is defined in terms of a type and is therefore vacuous when the receiver expression has no type. Extension member resolution then runs without competition from an instance candidate, exactly as for receivers that fall through to the extension path in [§12.8.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1287-member-access) today.

- **Method group receivers.** A method group that has a natural type per the [method group natural type improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/method-group-natural-type-improvements.md) is a typed receiver and binds via the existing path. A method group that does not have a natural type is a typeless receiver and enters this proposal's rule. The existing precedence between instance and extension dispatch is preserved in both cases.

- **Type inference and target typing.** No new type-inference machinery is introduced. Inference proceeds via [§12.6.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1263-type-inference), invoked from the applicability check on the third eligibility bullet. When the receiver expression is a tuple expression with one or more typeless elements, a conditional expression whose arms do not have a common type, or a switch expression whose arms do not have a common type, the standard's existing target-typing rules ([§10.2.13](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/conversions.md#10213-implicit-tuple-conversions) for tuples, plus the target-typed conditional and target-typed switch rules for arms) resolve the typeless components against each candidate's first parameter type. Target-typing is performed per candidate, as it already is for any other target-typed argument in overload resolution.

- **Dynamic.** Extension method invocation already does not apply when *expr* or any *args* has compile-time type `dynamic` ([§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations)). A receiver that has no type also does not have compile-time type `dynamic`, so the existing exclusion is unchanged.

- **Throw expressions.** A *throw_expression* has no type ([§12.16](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1216-the-throw-expression-operator)) and would otherwise be admitted by the typeless branch of the new rule. This proposal excludes throw expressions from the accepted receiver forms because evaluation of a throw expression terminates control flow before any extension member could be invoked, making the call unreachable. See [Which receiver categories are supported?](#which-receiver-categories-are-supported) for the full per-category list.

- **Construction-side extension methods on collection expressions.** This proposal applies only to *member access* on a typeless receiver. It is distinct from the separate question tracked at [dotnet/csharplang#9688](https://github.com/dotnet/csharplang/issues/9688), of whether a user-defined `Add` extension method should participate in the synthesis of a collection from a collection expression. The two questions both involve extensions and collection expressions, but apply to disjoint binding sites; this proposal does not change collection-expression construction.

## Back-compat analysis

This is a pure extension. The new branch of the receiver eligibility test in [§12.8.9.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12893-extension-method-invocations) and the corresponding update to [*Consumption*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#consumption) admit only receiver expressions that have no type, which under the existing rules of those sections are already binding-time errors. No expression that compiles today changes meaning. Any program that compiled before this feature continues to compile, with identical resolution and semantics.

## Drawbacks

The proposal expands the candidate set considered for member access on a typeless receiver. For users, this can convert what is today a clear "expression has no type" error into a less direct overload-resolution error when more than one in-scope extension is applicable. For implementations, applicability of each candidate may require target-typing the receiver expression against that candidate's first parameter type, which is the same machinery already used for typeless arguments in non-receiver positions but is invoked more often under this rule. Both costs are small relative to the ergonomic gain in the motivating cases, but they are not zero.

## Alternatives

- **Do nothing.** Users continue to either spell out the target type with a cast (`var a = (ImmutableArray<int>)[1, 2, 3];`) or call the extension as a static method (`var a = Enumerable.ToImmutableArray([1, 2, 3]);`). Both work today, both are uglier than the dotted form, and neither helps the typeless-lambda or method-group cases.

- **Postfix cast.** A separate proposal to allow a postfix cast spelling could let users write a less awkward variation on the cast form for the collection-expression case, but does not enable extension dispatch on typeless receivers in general and therefore does not address the lambda, method-group, or conditional-expression cases.

- **Partial generic inference.** A separate proposal to allow partial generic inference (for example, omitting some type arguments while supplying others) could relieve the verbosity of the static-method form for the collection-expression case. As with postfix cast, it is narrower than this proposal and does not help typeless lambdas, method groups, or conditional expressions.

- **Target-typed static member access ([dotnet/csharplang#9138](https://github.com/dotnet/csharplang/issues/9138)).** A separate ergonomic for invoking static or extension members of a known target type without naming the type. Different machinery, applicable only when the call site already supplies a target type, and orthogonal to the present proposal.

## Design decisions

## Open LDM questions

### Which receiver categories are supported?

## Related discussions

## Design meetings
