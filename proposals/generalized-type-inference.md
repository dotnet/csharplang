# Generalized generic type inference

Champion issue: https://github.com/dotnet/csharplang/issues/9626

This proposal generalizes and is intended to supersede the mechanism in the earlier
[target-typed generic type inference](target-typed-generic-type-inference.md) draft.
That draft remains useful motivation, but it should not be read as defining a second,
competing form of target inference.

## Summary
[summary]: #summary

Generic type inference is made independent of the construct that asks for it, and a
target type may contribute bounds along with the arguments. For instance, given:

```csharp
static IEnumerable<T> Create<T>() => default!;
```

the type argument can be inferred from the target:

```csharp
IEnumerable<string> names = Create(); // T is string
```

The result type `IEnumerable<T>` must fit in the target type
`IEnumerable<string>`, so the target contributes an upper bound of `string` for
`T`.

Method invocations and method-group conversions are the first consumers of the
generalized algorithm. Other proposals can use the same foundation for constructs
such as constructor calls, without having to disguise everything as a method call.

## Motivation
[motivation]: #motivation

Type inference is currently described mostly as a property of method invocation.
But the underlying job is more general: combine information from a generic
declaration with information from the context where it is used, and infer its type
arguments.

Making that job construct-neutral matters for two reasons. First, even method
inference is leaving information on the table today. A result often flows into a
known target, as in the example above, and method groups flow into delegates with
known return types. Second, other constructs need the same operation. Constructor
inference and type-group lookup should be consumers of one inference mechanism, not
each invent a method-shaped approximation of it.

Arguments and targets constrain inference in opposite directions. A value argument
must fit into its parameter, so it generally contributes a lower bound. A by-value
result must fit into its target, so the target contributes an upper bound. A
by-reference result aliases storage rather than merely producing a value, so its
target contributes an exact bound instead.

## Detailed design
[design]: #detailed-design

The corresponding standards work updates
[§12.6.3 Type inference](https://github.com/dotnet/csharpstandard/blob/alpha-v12/standard/expressions.md#1263-type-inference)
and
[§10.8 Method group conversions](https://github.com/dotnet/csharpstandard/blob/alpha-v12/standard/conversions.md#108-method-group-conversions).
The current standards-layer design is in
[MadsTorgersen/csharpstandard#1](https://github.com/MadsTorgersen/csharpstandard/pull/1).
This proposal explains the design rather than duplicating that full specification
diff.

### Construct-neutral inference

Type inference takes inputs from both a generic construct and the context in which
it is used.

From the generic construct it gets:

- Type parameters and their constraints.
- Parameter types.
- Optionally, a result type and whether the result is by-value or has a specific
  ref kind.

From the context it gets:

- Argument expressions corresponding to the parameter types.
- Optionally, a target type and whether the target is by-value or has a specific
  ref kind.

If inference succeeds, it produces a type argument for each type parameter. A
construct-specific *adapter* says where each input comes from. The core algorithm
does not know whether it is serving a method, a constructor, or some future generic
construct.

Here *result* means the type produced by the generic construct. *Target* means the
type expected by the context consuming that result. Both carry a passing mode:
by-value, or by-reference with a ref kind. Carrying the ref kind lets the consuming
context decide whether a result and target are compatible; the inference rule
below only says which type bounds to gather when they can be paired.

### Target-derived bounds

Argument-derived inference continues as today. In addition, when both a result and
a target are supplied, the first phase gathers bounds from them:

- If both are by-value, perform an upper-bound inference from the target type to
  the result type.
- If both are by-reference, perform an exact inference from the target type to the
  result type.
- Otherwise, the result and target do not contribute bounds to each other.

The direction is worth calling out. The construct produces its result and the
context consumes it. Inference must therefore choose type arguments that make the
result fit the target. This is the reverse of the usual argument-to-parameter flow,
where the parameter must be able to receive the argument.

By-reference flow is deliberately exact. A `ref` result and its target designate
the same storage; covariance or contravariance of the referred-to type would not
preserve that identity.

These rules gather bounds. They do not by themselves decide whether the eventual
result is convertible to the target, whether ref kinds are compatible, or which
overload wins. Those checks remain with the construct and context that requested
inference.

### Method-invocation adapter

For a generic method invocation:

- Type parameters, constraints, parameter types, result type, and result ref kind
  come from the method declaration.
- A `void` method has no result input.
- Argument expressions come from the invocation.
- The target type and target ref kind, if any, come from the context of the
  invocation.

Inference is still attempted separately for each generic candidate before overload
resolution. The inferred type arguments are substituted as today, and the
resulting method must still satisfy constraints and applicability rules.

The precise language-level definition of which contexts supply an invocation
target and ref kind is intentionally still open. The assignment in the
introductory example is the motivating target-producing context, not a complete
definition; the full set belongs to the broader target-typing model.

### Method-group-conversion adapter

Method-group conversion already has a compact form of type inference based on the
parameter types of the destination delegate. That inference is extended to include
the return side:

- Each delegate parameter type contributes a lower-bound inference to the
  corresponding method parameter type, as today.
- If both delegate and method return by-value, the delegate return type contributes
  an upper-bound inference to the method return type.
- If both return by-reference, the delegate return type contributes an exact
  inference to the method return type.

If either returns `void`, there is no return-side bound. Here "return
by-reference" covers any ref-returning form; whether the particular ref kinds are
compatible is a separate question for method-group conversion.

There are still no argument expressions in this form of inference, and therefore
no anonymous-function dependencies or repeated second phase. The delegate simply
provides the context inputs that an invocation would otherwise get from its
arguments and target.

For example:

```csharp
static T CreateOne<T>() => default!;

Func<string> factory = CreateOne; // T is string
```

The delegate return type `string` is the target for the method result type `T`, so
it supplies the bound that was previously missing.

### Mixed argument and target inference

Arguments and targets participate in the same inference, so each can determine
different type parameters:

```csharp
static IEnumerable<TOut> Transform<TIn, TOut>(TIn value) => default!;

IEnumerable<string> result = Transform(42);
// TIn is int from the argument.
// TOut is string from the target.
```

The argument `42` contributes a lower bound of `int` for `TIn`. The target
`IEnumerable<string>` contributes an upper bound of `string` for `TOut` through
the result type `IEnumerable<TOut>`. Neither source of information is privileged;
they are bounds gathered for the same fixing process.

### By-reference results

The corresponding method-group case uses exact inference:

```csharp
static class Slot<T>
{
    public static T Value = default!;
}

static ref T GetSlot<T>() => ref Slot<T>.Value;

delegate ref int RefFactory();

RefFactory getSlot = GetSlot; // T is exactly int
```

The destination delegate returns `ref int`, while the method returns `ref T`.
Exact inference reflects that both refer to the same storage type. Whether two
particular ref kinds are compatible is a separate check, not something to smooth
over with variance.

### Relationship to neighboring proposals

This proposal owns the construct-neutral inference inputs, result and target
terminology, ref-kind modeling, target-derived bounds, and the adapters for method
invocation and method-group conversion.

It does **not** define how an omitted generic type name is looked up, how
constructor candidates are formed, or how overload resolution chooses among those
candidates. Those are sibling design problems. Constructor inference and
type-group lookup can supply their own adapters and candidate sets while reusing
the algorithm defined here.

The earlier
[target-typed generic type inference](target-typed-generic-type-inference.md)
proposal described the by-value method-invocation slice of this design. This
proposal carries that feature forward under the same
[champion issue](https://github.com/dotnet/csharplang/issues/9626), generalizes the
inference contract, adds ref-kind modeling, and covers method-group return
inference. If this proposal is adopted, the older draft should be treated as
superseded rather than as another active owner of target-derived bounds.

## Benefits

- Generic APIs no longer need redundant type arguments when the result's
  destination already contains the information.
- Arguments and targets can cooperate, which helps APIs where inputs and outputs
  determine different type parameters.
- Generic method groups can use delegate return types as well as parameter types.
- New inference sites can reuse one vocabulary and algorithm instead of being
  specified through fictional method calls.
- The standards refactoring creates a stable seam for future inference work,
  including constructor inference, without committing this proposal to those
  constructs' lookup and overload-resolution rules.

## Compatibility

This is not a purely additive change. Target-derived bounds can make a generic
candidate succeed where it previously failed, but they can also change the type
arguments of an already successful candidate or make inference fail because the
new bounds conflict. That can add or remove candidates, introduce or remove
ambiguity, change overload selection, and move an error from conversion to
inference or vice versa. Method-group return inference has the same effects on
method-group conversions: a conversion that previously failed can become valid,
and an existing conversion can acquire a different candidate set or selection.

Some of these changes rescue code whose old inference result could not have flowed
to the target anyway, but that does not make the compatibility risk disappear.
Implementation experiments and corpus testing are needed before settling whether
any narrower rule or compatibility boundary is warranted.

## Drawbacks
[drawbacks]: #drawbacks

Inference becomes sensitive to more context, so a call can be harder to understand
in isolation. The generalized presentation is also more abstract than the current
method-oriented specification. Finally, the feature puts pressure on C#'s
target-typing model to say precisely when a target exists and what its ref kind is.

## Alternatives
[alternatives]: #alternatives

### Method-invocation-only target inference

The earlier target-typed proposal could be adopted directly by adding one rule to
method invocation. That is a smaller textual change, but it leaves method-group
conversion inconsistent and gives every new inference site a choice between
duplicating the rule and pretending to be a method call.

### Specify every construct as an imaginary method

Constructor and other inference sites can be translated into synthetic generic
methods for specification purposes. This reuses familiar words, but makes methods
the accidental center of the design and becomes strained as constructs acquire
different lookup, candidate, result, or ref-kind rules.

### Add partial type-argument syntax instead

A placeholder syntax could let programmers provide only the type arguments they
know. That can address some call sites, but it does not use information already
present in targets, does not help an unadorned method group, and adds syntax where
the compiler already has the necessary bound machinery.

### Leave method-group return inference separate

Invocation target inference could ship without changing method groups. This
reduces the initial compatibility surface, but means two uses of the same generic
method observe different inference information even when both have an explicit
result destination.

## Implementation considerations (skippable)

This section is only an intuition for implementers and can be skipped when
evaluating the language design.

The upper-bound rule is not new machinery. Imagine that a result were passed to a
contravariant receptacle:

```csharp
delegate void Receptacle<in T>(T value);
```

A `Receptacle<IEnumerable<string>>` receiving an `IEnumerable<T>` causes existing
variance-aware inference to turn the direction around and perform the same
upper-bound inference from `IEnumerable<string>` to `IEnumerable<T>`. In that
sense, target-derived inference exposes a relationship the inference engine
already knows how to express; the proposal gives it a direct, construct-neutral
input instead of specifying the receptacle fiction.

## Open questions
[open]: #open-questions

### What supplies the target type and ref kind of an invocation?

Assignments provide an obvious motivating case, but the standards layer still
needs a precise account of every context that supplies a target, when target
information is unavailable, and how the target's ref kind is obtained. This should
come from a coherent target-typing model rather than an ad hoc list local to type
inference.

### Where are the by-reference compatibility boundaries?

The inference rule uses exact bounds when result and target are both by-reference,
while carrying their actual ref kinds as inputs. We still need to settle which
combinations of `ref`, `ref readonly`, and related contexts may be paired, and
exactly where incompatibility is rejected. Inference should not accidentally make
an incompatible by-reference flow look like an ordinary by-value conversion.

### Is method-group return inference one feature decision?

Method-group return inference follows directly from the generalized model and
makes conversion and invocation more consistent. It also has its own compatibility
surface. The LDM should decide whether it is inseparable from target-derived method
inference or may be staged independently.

### What is user-facing design and what is standards refactoring?

Target-derived bounds and method-group return inference change programs. Making
the inference inputs construct-neutral can preserve behavior by itself, while
creating a reusable foundation for sibling features. We should be explicit about
whether those pieces are approved, implemented, and tested as one feature or as a
behavior-preserving standards refactoring followed by separate language changes.
