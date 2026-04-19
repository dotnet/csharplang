# Chained relational comparison

* Championed issue: <https://github.com/dotnet/csharplang/issues/8861>
* Discussion: <https://github.com/dotnet/csharplang/discussions/8643>

## Summary

Allow *relational_expression*s such as `a < b < c`, `min <= x <= max`, and
`0 <= i < array.Length` to have the intuitive chained-comparison meaning,
without requiring the middle operand to be spelled twice. The semantics are
roughly those of `a < b && b < c`, `min <= x && x <= max`, and
`0 <= i && i < array.Length`, respectively. This extends naturally to `<`,
`<=`, `>`, `>=` combined in any order and at any length.

## Motivation

Today, writing a range check in C# requires the programmer to mention the middle
operand twice:

```csharp
if (min <= x && x <= max) { … }
if (0 <= i && i < array.Length) { … }
```

This is awkward in two distinct ways:

**It does not match mathematical notation.** Students commonly learn to write
`min ≤ x ≤ max`.

**It is a double-evaluation correctness trap.** The natural rewrite of
`Min < ComputeVal() < Max` as `Min < ComputeVal() && ComputeVal() < Max`
evaluates `ComputeVal()` *twice*. That is wrong when the middle expression has
side-effects, is non-deterministic, or is merely expensive. The typical
workaround is to bind the middle expression into a local, but this destroys
the expression-level naturalness of the condition:

```csharp
// What the user wants to write:
if (Min < ComputeVal() < Max) { … }

// What the user has to write today to avoid double-evaluation:
var tmp = ComputeVal();
if (Min < tmp && tmp < Max) { … }
```

Chained comparison addresses both concerns by construction: the middle operand
is named once, evaluated once, and participates in both comparisons.

Similar features exist in several mainstream languages:

- **Python**: [Comparisons](https://docs.python.org/3/reference/expressions.html#comparisons) (`a < b <= c`).
- **Julia**: [Chaining comparisons](https://docs.julialang.org/en/v1/manual/mathematical-operations/#Chaining-comparisons).
- **Raku / Perl 6**: [Chained comparisons](https://docs.raku.org/language/operators#Chained_comparisons).
- **CoffeeScript**: [Chained comparisons](https://coffeescript.org/#comparisons).
- **C++26 (proposed)**: [P0893: Chained comparisons](https://www.open-std.org/jtc1/sc22/wg21/docs/papers/2018/p0893r1.html).
- **Rust** explicitly reserved this syntactic space for a future chained-comparison feature: [RFC 558](https://rust-lang.github.io/rfcs/0558-require-parentheses-for-chained-comparisons.html) (accepted in 2015) banned the unparenthesised form `a < b < c` at compile time specifically to leave room for later adding Python-style chaining. [RFC issue #2083](https://github.com/rust-lang/rfcs/issues/2083) tracks the open "allow chaining of comparisons" proposal, and the third-party [`cmpchain`](https://docs.rs/cmpchain/latest/cmpchain/macro.chain.html) crate provides the feature via a macro today.

There is even a third-party NuGet workaround (referenced in the linked
discussion thread for this proposal) that uses operator-overload trickery to
approximate the feature, which is evidence of real demand in the ecosystem.

## Detailed design

The following updates are presented as a diff against the corresponding
sections of the C# 6 standard
([expressions.md](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md)).
Throughout this section, ~~strikethrough~~ indicates text being removed from
the existing specification, and **bold** indicates text being added. Unchanged
prose is quoted verbatim for context.

### Grammar

The following diff is applied to the grammar in
[§11.11.1](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11111-general):

```diff
+relational_op
+    : '<'
+    | '>'
+    | '<='
+    | '>='
+    ;
+
 relational_expression
     : shift_expression
-    | relational_expression '<' shift_expression
-    | relational_expression '>' shift_expression
-    | relational_expression '<=' shift_expression
-    | relational_expression '>=' shift_expression
+    | relational_expression relational_op shift_expression
     | relational_expression 'is' type
     | relational_expression 'is' type identifier
     | relational_expression 'as' type
     ;
```

*Note*: This grammar refactor accepts exactly the same programs as before.
The four operator alternatives are collapsed into a single `relational_op`
production so that later sections can say "a *relational_op*" instead of
spelling out `<`, `<=`, `>`, `>=` at every reference.

### [§11.11.1 General](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11111-general)

Insert the following paragraph immediately after the existing paragraph that
begins *"For an operation of the form `x «op» y`, where «op» is a comparison
operator, overload resolution ([§11.4.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1145-binary-operator-overload-resolution))
is applied to select a specific operator implementation. ..."*:

**A *relational_expression* whose top-level operator is a *relational_op*,
and whose left operand is itself a *relational_expression* whose top-level
operator is a *relational_op*, may be a *chained relational comparison*, bound
per
[§11.11.13](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#111113-chained-relational-comparison).
Because *relational_expression* is left-associative, the chain structure is
implicit in the parse tree; the rule in §11.11.13 applies recursively at each
such node.**

### §11.11.13 Chained relational comparison (new subsection)

Add the following new subsection at the end of
[§11.11](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1111-relational-and-type-testing-operators),
after [§11.11.12 The as operator](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#111112-the-as-operator):

**This subclause defines the binding and run-time evaluation of
*relational_expression*s whose top-level operator is a *relational_op* and
whose left operand is itself such a *relational_expression*. Because
*relational_expression* is left-associative, an expression of the surface form
`e₀ op₁ e₁ op₂ e₂ … opₙ eₙ` (with each `opᵢ` a *relational_op*
and each `eᵢ` a *shift_expression*) is parsed as
`((…(e₀ op₁ e₁) op₂ e₂)… opₙ eₙ)`. The rules below apply at each such
relational-operator node and therefore extend naturally to chains of any
length.**

#### Binding

**Let `E` be a *relational_expression* of the form `A op B`, where `op` is a
*relational_op*, `A` is a *relational_expression*, and `B` is a
*shift_expression*. Binding of `E` proceeds as follows, using only information
local to this node (`A` has already been bound recursively by these same
rules):**

1. **Binary operator overload resolution
   ([§11.4.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1145-binary-operator-overload-resolution))
   is applied to `A op B` using `A`'s compile-time classification and `B`'s
   compile-time type. If overload resolution does not produce a binding-time
   error (as defined by the final paragraph of §11.4.5), `E` has the meaning
   given by that overload resolution together with the relevant subsection of
   §11.11, and this subclause has no further effect at this node.**

2. **Otherwise, `E` is a *chained relational comparison*, provided `A` is
   itself a *relational_expression* of the form `X op' Y` where `op'` is a
   *relational_op*. If `A` has any other top-level form, the binding-time
   error from step 1 is the result.**

**When `A` has the required form, `E` is well-typed iff both:**

- **(a) `A` is classified as `bool`. This is automatic when `A` was itself
  bound as a chained relational comparison by step 2 (which always produces
  `bool`), or when `op'` resolves to a predefined relational operator or
  any user-defined relational operator returning `bool`.**

- **(b) Binary operator overload resolution
  ([§11.4.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1145-binary-operator-overload-resolution))
  applied to `Y op B` as an isolated binary operation selects an operator
  whose result type is `bool`.**

**When both (a) and (b) hold, `E` is classified as a value of type `bool`.
Otherwise a compile-time error occurs, with a diagnostic identifying which
condition failed.**

***Note***: The rule is strictly local: it uses only `E`'s own `op`, `A`'s
already-determined classification, and `B`. No node is re-bound; a node whose
step 1 succeeded is never revisited because step 2 fires at a later node.
Chains of any length follow directly from the recursive structure of
*relational_expression*. **end note***

***Note***: Parentheses around the left-hand operand suppress chain formation.
Although programmers commonly think of parentheses as affecting only operator
precedence, here they also affect binding. Step 2 requires `A` to be a
*relational_expression* whose top-level production is a relational-operator
node, and a parenthesized expression's top-level production is a
*parenthesized_expression*, not a relational-operator node. Therefore an
expression of the surface form `(a op₁ b) op₂ c` is *not* a chained relational
comparison: it is bound only by step 1, and compiles only when classical
binding succeeds (which for typical operand types it does not, because
`bool op₂ c` has no applicable predefined operator). Writers who want a chain
must leave the left-hand operand unparenthesised. **end note***

***Note***: Nullable forms of the standard value types (for example `int?`,
`DateTime?`, a nullable user-defined comparable struct) participate in chained
relational comparisons without any extra rule, because the lifted relational
operators described in
[§11.4.8](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1148-lifted-operators)
return `bool` (not `bool?`), producing `false` if either operand is `null`.
Conditions (a) and (b) are therefore satisfied for such operands, and a `null`
operand simply causes the affected link to be `false`, short-circuiting the
remainder of the chain. **end note***

***Note***: If any operand of `A op B` has compile-time type `dynamic`, the
expression is dynamically bound per
[§11.11.1](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11111-general),
which is not a binding-time error. Step 1 therefore succeeds and the
expression is a classical left-associative tree with dynamic binding at each
node, not a chained relational comparison. **end note***

#### Run-time evaluation

**By construction, a chained relational comparison `E = A op B` bound per
step 2 has `A` of the form `X op' Y`, and `A`'s own evaluation already
evaluates `Y` exactly once as the right operand of its top-level operator.
Evaluation of `E` proceeds:**

1. **`A` is evaluated, producing a `bool` value and the value of `Y`.**
2. **If `A` produced `false`, the result of `E` is `false` and `B` is not
   evaluated.**
3. **Otherwise, `B` is evaluated and the result of `E` is the result of
   applying the operator selected in step 2(b) to the already-evaluated
   value of `Y` and the evaluated value of `B`.**

**Each *shift_expression* in a chained relational comparison is evaluated at
most once. A middle operand that appears in two adjacent links, as the right
operand of the inner link and the left operand of the outer link, is still
evaluated only once; its evaluated value is used in both links. Evaluation
order of operands is strictly left-to-right, and the chain short-circuits on
the first link that produces `false`.**

***Note***: By induction on the depth of `A`, a fully-chained
`e₀ op₁ e₁ op₂ e₂ … opₙ eₙ` evaluates as if written
`(e₀ op₁ e₁) && (e₁ op₂ e₂) && … && (eₙ₋₁ opₙ eₙ)`, with each `eᵢ` evaluated
at most once. **end note***

> ***Example***: *Range checks and mathematical inequalities:*
>
> ```csharp
> if (0 <= i < array.Length) // bounds check
>     array[i] = ...;
>
> if (min <= ComputeValue() <= max) // ComputeValue invoked once
>     Accept();
>
> bool isAscending = a < b < c < d; // chains extend naturally
> ```
>
> ***end example***

> ***Example***: *A chained relational comparison and its ordinary-`&&`
> equivalent, for `int` operands:*
>
> ```csharp
> // These two conditions produce identical results, but the chained form
> // evaluates F() once instead of twice:
> min <= F() <= max
> min <= F() && F() <= max
> ```
>
> ***end example***

### Interactions with other features

These interactions all fall out of the single-node rule above; no additional
spec text is required.

- **Definite assignment** of operands follows the equivalent short-circuit
  `&&`-chain: each `eᵢ` is definitely assigned by its point of use, and the
  expression as a whole contributes the "always `bool`" rules from
  [§9.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/variables.md#94-definite-assignment).

- **Constant expressions**: if every operand is a constant expression and
  every link is a predefined relational operator over constant-expression
  operand types, the whole chain is a constant expression, because each link
  is constant-foldable per [§11.20](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1120-constant-expressions)
  and the short-circuit `&&` form preserves constant-ness.

- **Null-coalescing (`??`), null-conditional (`?.`), and nullable reference
  types**: operand expressions of these forms are evaluated to a single value
  per the rules of their respective subclauses, and that value participates
  in the chain exactly like any other operand. The chain itself introduces
  no new NRT or null-tracking behaviour.

- **Parser ambiguities that superficially resemble a chain, such as
  `A < B < C > D > F`** (where the operands are themselves types, so the
  expression may instead be a declaration `A<B<C>D> F`): these are purely
  syntactic and are resolved by the existing generic-vs-expression
  disambiguator in the C# grammar. The parse tree is unchanged by this
  proposal, and §11.11.13 applies only to parses that are already
  *relational_expression*s.

## Back-compat analysis

This is a pure extension. Step 1 of the binding rule is unconditional and is
attempted at every *relational_expression* node before step 2 is ever
considered. Any expression that compiled before this feature continues to
compile with the same meaning:

- If the expression does not involve a *relational_op* as the outer operator
  of two or more left-spine nodes, it is not a candidate for step 2 at all.

- If it *is* a left-spine of *relational_op* nodes but classical overload
  resolution binds every node successfully (for example, because the user has
  defined custom `operator <` overloads that make the classical left-associative
  reading well-typed), step 1 succeeds at every node and step 2 never fires.
  This preserves the semantics of the widely-referenced NuGet package that
  emulates chained comparisons via operator-overload trickery, and of any
  similar ad-hoc patterns already in the wild.

- The only expressions whose meaning changes are those that were previously
  compile-time errors, which now compile under step 2 when both conditions
  (a) and (b) are satisfied.

## Drawbacks

As with any language feature, the additional specification complexity must be
weighed against the clarity and correctness improvements it offers users.
The feature is localized to one subsection of the spec and reuses
§11.4.5's existing machinery at every step, so the marginal complexity is
small.

A minor conceptual hazard is that `a < b < c` now has two possible bindings
in principle, classical and chained, with the classical reading taking
priority when it is legal. Readers who are accustomed to C#'s existing
behaviour of `a < b < c` (which almost always produces a compile-time error
today) will need to internalize that the expression now has a meaning in the
common case.

## Alternatives

- **Do nothing.** Users continue to duplicate the middle operand, with the
  associated readability and double-evaluation risks.

- **Introduce a dedicated range-check syntax (`min..max` contains `x`, or
  similar).** This addresses only the specific `min <= x <= max` case, not
  mixed-direction chains like `a < b > c`, and introduces a new language
  concept rather than giving existing syntax its intuitive meaning.

- **Restrict chained comparisons to same-direction (all `<`/`<=` or all
  `>`/`>=`).** C++26's proposal takes this line. The approach here is more
  permissive, on the grounds that once the semantics are fixed as
  `a op₁ b op₂ c ⇔ a op₁ b && b op₂ c`, there is no additional cognitive
  load in allowing `a < b > c`: each link is independently understandable.

## Design decisions

### Why not include `==` and `!=`?

Expressions mixing equality and relational operators are legal today and
have a useful meaning. `a < b == c < d` parses as `(a < b) == (c < d)`
under the precedence rules of
[§11.4.2](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1142-operator-precedence-and-associativity),
asking whether two comparisons have the same truth value. That is a
reasonable pattern found in real code, and it is clearly preferable to any
`((a < b) == c) < d` form that a chain-style reinterpretation would
produce. Users who want an equality chain can write it directly as
`a == b && b == c`.

### Why not include `is` and `as`?

`is` and `as` are type-testing operators, not value comparisons. The
*relational_op* production names only `<`, `<=`, `>`, `>=`, so chain
formation never triggers on `is` or `as`.

### Why not warn when classical binding wins?

Warning on any `a < b < c` that resolves classically would false-positive
on existing code that intentionally uses user-defined operators to make the
classical binding succeed, including the third-party NuGet
chained-comparison package and similar patterns. The two-rule design
already prefers classical binding whenever it succeeds; adding a warning
at that point would second-guess the programmer.

### Why not require explicit opt-in syntax (e.g. `chain(a < b < c)`)?

The goal of this proposal is to give the natural, mathematically-motivated
syntax its natural meaning. Users reasonably expect `a < b < c` to just
work, and any opt-in wrapper would defeat that purpose.

## Related discussions

- [Discussion #8643: Proposal: Ternary comparison operator](https://github.com/dotnet/csharplang/discussions/8643) (primary).
- [Issue #4108: [Proposal] Ternary comparison operator](https://github.com/dotnet/csharplang/issues/4108) (the original issue that became discussion #8643).
- [roslyn#136: Chained comparison operator](https://github.com/dotnet/roslyn/issues/136).

## Design meetings

TBD
