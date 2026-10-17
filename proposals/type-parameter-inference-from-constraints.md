# Type Parameter Inference from Constraints

Champion issue: https://github.com/dotnet/csharplang/issues/9453

## Summary

Allow method type inference to infer a type parameter from the constraints of another type parameter. After the type parameters selected by a fixing step are fixed, a constraint
inference is made from each fixed type to its constraint types to infer bounds for type parameters that remain unfixed.

Constraint bounds are used only when ordinary argument and output type inference produced no bounds for the type parameter. When more than one type parameter is fixed by the same
step of type inference, all results are determined from the same inference state before any constraint bounds are inferred.

For example:

```csharp
List<int> values = [1, 2, 3];
M(values); // Infers TEnumerable as List<int> and TElement as int.

void M<TEnumerable, TElement>(TEnumerable value)
    where TEnumerable : IEnumerable<TElement>
{
    Console.WriteLine(string.Join(",", value));
}
```

`TEnumerable` is first fixed to `List<int>`. Since `List<int>` implements `IEnumerable<int>`, constraint inference from `List<int>` to `IEnumerable<TElement>` produces a bound of
`int` for `TElement`.

## Motivation

Currently, C# type inference can fail in scenarios where the compiler has all the information it needs to determine the correct type parameters through constraint relationships.
This leads to verbose code requiring explicit type arguments or prevents valid overloads from being considered. This has long been a thorn in the side of C# users: no less than
9 different issues/discussions on it have come up over the past decade on csharplang.

* https://github.com/dotnet/roslyn/issues/5023
* https://github.com/dotnet/roslyn/issues/15166
* https://github.com/dotnet/csharplang/discussions/478
* https://github.com/dotnet/csharplang/discussions/741
* https://github.com/dotnet/csharplang/discussions/997
* https://github.com/dotnet/csharplang/discussions/1018
* https://github.com/dotnet/csharplang/discussions/6930
* https://github.com/dotnet/csharplang/discussions/7262
* https://github.com/dotnet/csharplang/discussions/8767

There was even one implementation of a proposed change, https://github.com/dotnet/roslyn/pull/7850, but LDM looked at this in 2016 and decided that it would be too potentially
breaking. Since then, C# has taken larger breaking change steps; most notably for this proposal, adding natural types to lambdas and method groups in overload resolution, but
also adding things like target-typing for ternary expressions, adding span conversions as first-class conversions in the language, the `field` keyword, and others. Given this,
now is an excellent time to re-examine the concern on the breaking change here, and potentially move forward with the proposal.

Credit to [@HellBrick](https://github.com/HellBrick) for the [original proposed mechanics](https://github.com/dotnet/roslyn/issues/5023#issuecomment-154728796) of the design.
This proposal has been further refined from their original starting point.

### Use cases this supports:

```csharp
private static void M<T, X>(T Object) where T : IEnumerable<X>, IComparable<X> 
{
}

private class MyClass : IComparable<String>, IEnumerable<String> 
{
}

private static void CallMyFunction() 
{
    var c = new MyClass();
    M(c);
}
```

## Detailed design

### Changes to the type inference algorithm

The method type inference process described in [§12.6.3](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#1263-type-inference) is modified as follows.

#### Bounds

The following text in §12.6.3.1 (*General*) is replaced:

> During the process of inference each type parameter `Xᵢ` is either *fixed* to a particular type `Sᵢ` or *unfixed* with an associated set of *bounds*. Each of the bounds is some
> type `T`. Initially each type variable `Xᵢ` is unfixed with an empty set of bounds.

with:

> During the process of inference, each type variable `Xᵢ` is either *fixed* to a particular type `Sᵢ`, or *unfixed* with two associated sets of *bounds*: *ordinary bounds* and
> *constraint bounds*. Each bound is an exact, lower, or upper bound whose value is some type `T`. Initially, each type variable `Xᵢ` is unfixed and both sets of bounds are empty.
>
> Bounds produced by the inference operations otherwise described in this section are ordinary bounds. Bounds produced by a *constraint inference* are constraint bounds.
>
> The *effective bounds* of an unfixed type variable `Xᵢ` are determined as follows:
>
> - If `Xᵢ` has one or more ordinary bounds, its effective bounds are its ordinary bounds.
> - Otherwise, its effective bounds are its constraint bounds.
>
> When §12.6.3.3 determines whether `Xᵢ` has a non-empty set of bounds, and when §12.6.3.13 fixes `Xᵢ`, the bounds of `Xᵢ` are its effective bounds. The operations in §12.6.3.10
> through §12.6.3.12 that add bounds add ordinary bounds, except when those operations are performed as part of a constraint inference.

> *Note*: Constraint bounds are not combined with ordinary bounds. Constraint inference can therefore supply a type argument that ordinary inference could not determine, but does
> not refine or replace a type argument inferred from an argument or an output type. Generic constraints are still checked after type inference in the usual manner. *end note*

#### Dependence

The first paragraph of §12.6.3.6 (*Dependence*) is replaced with:

> An *unfixed* type variable `Xᵢ` *depends directly on* an *unfixed* type variable `Xₑ` if either of the following is true:
>
> - For some argument `Eᵥ` with type `Tᵥ`, `Xₑ` occurs in an *input type* of `Eᵥ` with type `Tᵥ`, and `Xᵢ` occurs in an *output type* of `Eᵥ` with type `Tᵥ`.
> - `Xᵢ` and `Xₑ` are distinct, and `Xᵢ` occurs in a constraint type of `Xₑ`.

The remainder of §12.6.3.6 is unchanged.

#### Constraint inference

A *constraint inference from* a type `U` *to* a type `V` is performed in the same manner as a lower-bound inference from `U` to `V`, except that every bound produced by the
inference is a constraint bound. This applies both to bounds produced directly and to bounds produced by any exact, lower-bound, or upper-bound inference performed recursively as
part of the constraint inference. If such an inference would add a bound for a type variable that already has one or more ordinary bounds, no bound is added.

#### The second phase

The following text is inserted after “The second phase proceeds as follows:” in §12.6.3.3 (*The second phase*):

> Whenever a step below directs that a set of unfixed type variables be fixed, the result of fixing each type variable in that set is first determined independently, using the
> effective bounds of all type variables as they existed before any type variable in the set was fixed.
>
> If fixing any type variable in the set fails, type inference fails. Otherwise, all type variables in the set are fixed to their respective results. After all type variables in
> the set have been fixed, for each type variable `Xᵢ` fixed to a type `V`, a constraint inference is made from `V` to each constraint type of `Xᵢ`, if any.

> *Note*: Constraint bounds produced after a set of type variables is fixed do not participate in fixing another type variable in that same set. They are available in a later
> repetition of the second phase. Consequently, the result does not depend on the order in which the type variables in the set are processed, including their declaration order.
> *end note*

> *Note*: Each successful repetition of the second phase fixes at least one previously unfixed type variable, and a fixed type variable never becomes unfixed. Therefore, for a
> method with `N` type parameters, the second phase is repeated at most `N` times before type inference succeeds or fails. *end note*

#### Fixing

The first sentence of §12.6.3.13 (*Fixing*) is replaced with:

> An *unfixed* type variable `Xᵢ` with a non-empty set of effective bounds is *fixed* as follows:

The remainder of §12.6.3.13 is unchanged. In particular, fixing determines a type from the effective bounds of a single type variable; constraint inference is performed only
after all type variables selected by the same step of the second phase have been fixed.

### Examples

Constraint inference fills a type variable for which ordinary inference produced no bounds:

```csharp
static void M<TEnumerable, TElement>(TEnumerable value)
    where TEnumerable : IEnumerable<TElement>
{
}

M(new List<int>()); // TEnumerable is List<int>; TElement is int.
```

An ordinary bound takes precedence over a constraint bound:

```csharp
static void M<TEnumerable, TElement>(TEnumerable sequence, TElement element)
    where TEnumerable : IEnumerable<TElement>
{
}

M(new List<int>(), ""); // Infers TElement as string.
                        // The constructed method then fails its constraint check.
```

The following example requires two repetitions of the second phase:

```csharp
interface Pair<out T, out U> { }
interface I<T> { }
interface J<T> { }

sealed class A : I<B> { }
sealed class B : J<A> { }
sealed class Seed : Pair<A, B> { }

static void M<S, T, U>(S value)
    where S : Pair<T, U>
    where T : I<U>
    where U : J<T>
{
}

M(new Seed());
```

The first repetition fixes `S` to `Seed`, which produces constraint bounds of `A` for `T` and `B` for `U`. Since `T` and `U` depend on one another, they are selected together in
the next repetition. Each is fixed from the bounds available before either is fixed. Neither can contribute a new bound used to fix the other during the same step, and reversing
their declaration order does not change the result.

## Drawbacks

The primary concern with this proposal is that it can change overload resolution. A generic candidate for which inference previously failed may now become applicable, which can
select a different overload or introduce an ambiguity.

The precedence of ordinary bounds limits this change: constraint inference does not alter a type argument for which argument or output type inference already produced a bound. It
can nevertheless make additional generic candidates participate in overload resolution.

**Example breaking change:**

```cs
void M(object obj) 
{
    Console.WriteLine("Called non-generic overload");
}

void M<T, U>(T t) where T : IEnumerable<U> 
{
    Console.WriteLine("Called generic overload");
}

// Call site:
M("test"); // Currently prints "Called non-generic overload", would print "Called generic overload"
```

This is somewhat similar to the breaks that occurred with lambda and method group natural types; the most common change there was that type inference failed on an instance
method, and then fell back to an extension method instead. And, similarly to that proposal, the likelihood is that the new overload chosen is actually the more "correct" one;
it's more likely to be what the user intended.

There are options to mitigate this break if we so choose; we could do two runs of overload resolution, first without constraint inference, then if that fails to find a single
applicable overload we could rerun with constraint inference. This would be significantly more complex, but it could be done, and would mitigate the breaking change.

## Alternatives

There are a couple of other options:

* Introduce a new keyword at generic parameter declaration, such as `void M<TEnumerable, infer TElement>(TEnumerable t) where TEnumerable : IEnumerable<TElement>`, and only
  perform constraint inference for such parameters. While this is doable, and entirely mitigates the breaking change, it immediately becomes the "default" that everyone should use,
  and not doing so is a bug on the author's part, and thus is not good for the future of the language.
* Partial type inference - https://github.com/dotnet/csharplang/issues/8968 covers partial type inference, so that users could use an `_` or an empty identifier to avoid
  restating what can be inferred from the signature, and only state what cannot be inferred. While this is a decent idea, it doesn't fully solve this issue, as we want to avoid
  needing to specify _any_ type parameters in this case when they can be inferred.
* Associated types - https://github.com/dotnet/csharplang/issues/8712 covers this. Another very related proposal, and one this proposal does not rule out. But partial type
  inference will cover more scenarios around inference that won't be fixed by associated types, for scenarios where the input is truly not an associated type but has constraints
  based on other type parameters.

## Open questions
[open]: #open-questions

TBD
