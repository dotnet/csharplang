# Type Parameter Inference from Constraints

Champion issue: https://github.com/dotnet/csharplang/issues/9453

## Summary

Allow type inference to succeed in overload resolution scenarios by promoting generic constraints of inferred type parameters to "fake arguments" during type inference, enabling
the bounds of type variables to participate in the inference process. An example is:

```cs
List<int> l = [1, 2, 3];
M(l); // Today: TElement cannot be inferred. With this proposal, successful call.

void M<TEnumerable, TElement>(TEnumerable t) where TEnumerable : IEnumerable<T>
{
    Console.WriteLine(string.Join(",", t));
}
```

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

### Changes to Type Inference Algorithm

We modify the type inference process described in [§12.6.3](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#1263-type-inference) to include
constraint relationships in the dependence relationship between type variables.

#### Enhanced Dependence Relationship - Modified Spec Text

The following text from [§12.6.3.6 Dependence](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12636-dependence) is modified:

> **12.6.3.6 Dependence**
>
> An *unfixed* type variable `Xᵢ` *depends directly on* an *unfixed* type variable `Xₑ` if one of the following holds:
>
> - For some argument `Eᵥ` with type `Tᵥ` `Xₑ` occurs in an *input type* of `Eᵥ` with type `Tᵥ` and `Xᵢ` occurs in an *output type* of `Eᵥ` with type `Tᵥ`.
> - **`Xᵢ` occurs in a constraint for `Xₑ`.**
>
> `Xₑ` *depends on* `Xᵢ` if `Xₑ` *depends directly on* `Xᵢ` or if `Xᵢ` *depends directly on* `Xᵥ` and `Xᵥ` *depends on* `Xₑ`. Thus "*depends on*" is the transitive but not reflexive closure of "*depends directly on*".

#### Enhanced Fixing Process - Modified Spec Text

The following text from [§12.6.3.12 Fixing](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#126312-fixing) is modified:

> **12.6.3.12 Fixing**
>
> An *unfixed* type variable `Xᵢ` with a set of bounds is *fixed* as follows:
>
> - The set of *candidate types* `Uₑ` starts out as the set of all types in the set of bounds for `Xᵢ`.
> - Each bound for `Xᵢ` is examined in turn: For each exact bound U of `Xᵢ` all types `Uₑ` that are not identical to `U` are removed from the candidate set. For each lower bound `U` of `Xᵢ` all types `Uₑ` to which there is *not* an implicit conversion from `U` are removed from the candidate set. For each upper-bound U of `Xᵢ` all types `Uₑ` from which there is *not* an implicit conversion to `U` are removed from the candidate set.
> - If among the remaining candidate types `Uₑ` there is a unique type `V` to which there is an implicit conversion from all the other candidate types, then `Xᵢ` is fixed to `V` **and a lower-bound inference is performed from `V` to each of the types in `Xᵢ`'s constraints, if any**.
> - Otherwise, type inference fails.

## Drawbacks

The primary concern with this proposal is that it introduces potential breaking changes in overload resolution. Code that currently compiles and calls one overload might start
calling a different overload after this feature is implemented.

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

There are options to mitigate this break if we so choose; we could do two runs of overload resolution, first without constraint promotion, then if that fails to find a single
applicable overload we could rerun with constraint promotion. This would be significantly more complex, but it could be done, and would mitigate the breaking change.

## Alternatives

There are a couple of other options:

* Introduce a new keyword at generic parameter declaration, such as `void M<TEnumerable, infer TElement>(TEnumerable t) where TEnumerable : IEnumerable<TElement>`, and only
  perform the promotion for such parameters. While this is doable, and entirely mitigates the breaking change, it immediately because the "default" that everyone should use,
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
