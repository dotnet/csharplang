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

**It does not match mathematical notation.** Students learn to write
`min ≤ x ≤ max` early on. The same form is a near-universal convention
across many walks of life, and nearly any reader can reason about it at
first glance.

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

Similar features exist in many languages, with particularly strong support
among languages oriented toward mathematical notation and among the Lisp
family. The earliest designed instance of the feature dates back to
[CPL](https://retrocomputing.stackexchange.com/questions/20216/what-was-the-first-programming-language-to-support-operator-chaining)
in the 1960s.

Math-oriented and scientific languages:

- **Mathematica / Wolfram Language**: [native support](https://reference.wolfram.com/language/ref/Less.html).
- **Julia**: [native support](https://docs.julialang.org/en/v1/manual/mathematical-operations/#Chaining-comparisons).
- **Python**: [native support](https://docs.python.org/3/reference/expressions.html#comparisons).

Lisp family (variadic comparison operators):

- **Common Lisp**: [native support](http://www.lispworks.com/reference/HyperSpec/Body/f_eq_sle.htm).
- **Clojure**: [native support](https://www.clojure.org/guides/comparators).
- **Scheme** and **Racket**: native support, specified in R5RS/R6RS/R7RS.

Other languages with native chaining:

- **Raku / Perl 6**: [native support](https://docs.raku.org/language/operators#Chained_comparisons).
- **CoffeeScript**: [native support](https://coffeescript.org/#comparisons).
- **Icon**: [native support](https://www2.cs.arizona.edu/icon/refernce/exprlist.htm),
  which [historically inspired Python's implementation](https://stackoverflow.com/a/2650109) of the feature.
- **BCPL** (1967) and its unimplemented predecessor **CPL** (1960s): the
  [original designed instances of the feature](https://retrocomputing.stackexchange.com/questions/20216/what-was-the-first-programming-language-to-support-operator-chaining).

Other modern languages have also taken the feature seriously without
shipping it. Rust's [RFC 558](https://rust-lang.github.io/rfcs/0558-require-parentheses-for-chained-comparisons.html)
(accepted in 2015) explicitly banned the unparenthesized form `a < b < c`
at compile time to reserve the syntax for a future chain feature;
[RFC issue #2083](https://github.com/rust-lang/rfcs/issues/2083) tracks the
ongoing "allow chaining of comparisons" proposal, and the third-party
[`cmpchain`](https://docs.rs/cmpchain/latest/cmpchain/macro.chain.html)
crate provides the feature today via a macro. The ISO C++ working group
has [P0893: Chained comparisons](https://www.open-std.org/jtc1/sc22/wg21/docs/papers/2018/p0893r1.html)
on the C++26 roadmap.

Within the C# ecosystem itself, a third-party NuGet workaround (referenced
in the linked discussion thread for this proposal) uses operator-overload
trickery to approximate the feature, which is evidence of real user demand.

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

*Note*: This refactoring recognizes the same set of programs as before. *end note*

### [§11.11.1 General](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11111-general)

Insert the following paragraph immediately after the existing paragraph that
begins *"For an operation of the form `x «op» y`, where «op» is a comparison
operator, overload resolution ([§11.4.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1145-binary-operator-overload-resolution))
is applied to select a specific operator implementation. ..."*:

**An operation of the form `A op B`, where `A` is a *relational_expression*,
`op` is a *relational_op*, and `B` is a *shift_expression*, is a *chained
relational comparison* when `A` is itself an operation of the form `X op' Y`
with `op'` a *relational_op*. Chained relational comparisons are specified
in [§11.11.13](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#111113-chained-relational-comparisons).
Because *relational_expression* is left-associative, §11.11.13 applies at
each such *relational_expression* node.**

### §11.11.13 Chained relational comparisons (new subsection)

Add the following new subsection after [§11.11.12 The as operator](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#111112-the-as-operator):

**For an expression `e₀ op₁ e₁ op₂ e₂ … opₙ eₙ` where each `opᵢ` is a
*relational_op* and each `eᵢ` is a *shift_expression*, the
left-associativity of *relational_expression* yields the structure
`((…(e₀ op₁ e₁) op₂ e₂)… opₙ eₙ)`. The rules that follow apply at each
*relational_expression* node of such a chain, and therefore describe chains
of any length.**

#### Binding

**Let `E` be an operation of the form `A op B`, where `A` is a
*relational_expression*, `op` is a *relational_op*, and `B` is a
*shift_expression*.**

**Binary operator overload resolution
([§11.4.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1145-binary-operator-overload-resolution))
is first applied to `A op B`. If overload resolution succeeds, `E` has the
meaning determined by §11.4.5 together with the relevant subsection of
§11.11, and this subclause has no further effect on `E`.**

**Otherwise, if `A` is itself an operation of the form `X op' Y` where
`op'` is a *relational_op*, `E` is a *chained relational comparison*, and
the following shall hold; otherwise, a compile-time error occurs:**

- **`A` shall be classified as a value of type `bool`. This is automatic
  when `A` is itself a chained relational comparison (which is always
  classified as a value of type `bool`), or when `op'` resolves to a
  predefined relational operator or a user-defined relational operator
  returning `bool`.**

- **Binary operator overload resolution
  ([§11.4.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1145-binary-operator-overload-resolution))
  applied to `Y op B` as an isolated binary operation shall select an
  operator whose result type is `bool`.**

**When both conditions are satisfied, `E` is classified as a value of type
`bool`.**

**If `A` is not an operation of the form `X op' Y` with `op'` a
*relational_op*, the binding-time error reported by overload resolution
above is the result.**

***Note***: These rules apply at each *relational_expression* node using
only the node's own operator, its left operand's existing classification,
and its right operand. A node whose overload resolution succeeded is not
reconsidered. Chains of arbitrary length follow from the recursive
structure of *relational_expression*. **end note***

***Note***: Parentheses around the left-hand operand prevent the chain
interpretation. Although parentheses normally affect only operator
precedence, they also affect binding here: chained relational comparison
requires `A` to be an operation of the form `X op' Y`, and when `A` is a
*parenthesized_expression* this is not the case, regardless of what is
written inside the parentheses. Therefore an expression written as
`(a op₁ b) op₂ c` is not a chained relational comparison; it is bound only
by binary operator overload resolution ([§11.4.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1145-binary-operator-overload-resolution)),
which for typical operand types fails because no applicable operator
exists for `bool op₂ c`. **end note***

***Note***: Nullable forms of the standard value types (for example `int?`,
`DateTime?`, or a nullable user-defined comparable struct) participate in
chained relational comparisons without any additional rule, because the
lifted relational operators of
[§11.4.8](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1148-lifted-operators)
return `bool` rather than `bool?`, producing `false` when either operand
is `null`. The requirements above are therefore satisfied for such
operands, and a `null` operand causes the corresponding comparison to
produce `false`; subsequent operands and comparisons are then not
evaluated. **end note***

***Note***: When any operand of `A op B` has compile-time type `dynamic`,
the expression is dynamically bound ([§11.11.1](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11111-general)).
Dynamic binding does not produce a binding-time error, so overload
resolution of `A op B` succeeds and this subclause does not apply. Each
comparison is instead resolved at run time according to the rules of
dynamic binding. **end note***

#### Run-time evaluation

**At run-time, a chained relational comparison of the form `A op B` is
evaluated as follows. `A` is first evaluated; because `A` is of the form
`X op' Y`, `Y` is evaluated during the evaluation of `A`. If `A` yields
`false`, `B` is not evaluated, and the result of `A op B` is `false`.
Otherwise, `B` is evaluated, and the result of `A op B` is obtained by
applying the operator resolved for `Y op B` above to the value of `Y`
produced during `A`'s evaluation and the value of `B`.**

**Each *shift_expression* in a chained relational comparison is evaluated
at most once. When a *shift_expression* appears as the right operand of
one comparison in the chain and as the left operand of the following
comparison, it is evaluated only once; the value so produced is used by
both comparisons. Operands are evaluated in left-to-right order. After the
first comparison that yields `false`, no further operands are evaluated
and no further comparisons are performed.**

***Note***: When each *relational_expression* node in a chain
`e₀ op₁ e₁ op₂ e₂ … opₙ eₙ` is a chained relational comparison, the chain
is equivalent in result to `(e₀ op₁ e₁) && (e₁ op₂ e₂) && … && (eₙ₋₁ opₙ eₙ)`,
with each `eᵢ` evaluated at most once. **end note***

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
  disambiguator in the C# grammar. This proposal does not change the
  grammar or parsing behaviour; §11.11.13 applies only to expressions that
  already parse as *relational_expression*s.

## Back-compat analysis

This is a pure extension. At every *relational_expression* node, ordinary
binary operator overload resolution per §11.4.5 is attempted first, exactly
as it is today; the chained-comparison rules in §11.11.13 only apply when
that overload resolution would otherwise have produced a binding-time
error. Any expression that compiled before this feature continues to
compile with the same meaning:

- If an expression does not involve a *relational_op* both at an outer
  *relational_expression* node and at its left operand, §11.11.13 does not
  apply at all.

- If an expression does have that shape but ordinary overload resolution
  succeeds at every node (for example, because a user has defined custom
  `operator <` overloads that give the left-associative reading a valid
  result type), §11.11.13 does not apply. This preserves the semantics of
  the widely-referenced NuGet package that emulates chained comparisons via
  operator-overload trickery, and of any similar ad-hoc patterns already in
  the wild.

- The only expressions whose meaning changes are those that were
  compile-time errors before this feature, and that now become
  well-formed chained relational comparisons.

## Drawbacks

As with any language feature, the additional specification complexity must be
weighed against the clarity and correctness improvements it offers users.
The feature is localized to one subsection of the spec and reuses
§11.4.5's existing machinery at every step, so the marginal complexity is
small.

A minor conceptual hazard is that `a < b < c` now has two possible
bindings in principle: the ordinary §11.4.5 binding, when it succeeds, and
the chained-comparison binding of §11.11.13 when §11.4.5 would otherwise
report a binding-time error. The ordinary binding takes priority wherever
it is legal. Readers who are accustomed to C#'s existing behaviour of
`a < b < c` (which almost always produces a compile-time error today) will
need to internalize that the expression now has a meaning in the common
case.

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

### Why not warn when ordinary binding resolves a would-be chain?

Warning on any `a < b < c` that binds via §11.4.5 would false-positive on
existing code that intentionally uses user-defined operators to make that
binding succeed, including the third-party NuGet chained-comparison package
and similar patterns. §11.4.5 already takes priority over §11.11.13
whenever it succeeds; adding a warning at that point would second-guess the
programmer.

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
