# Type Parameter Inference from Constraints

Champion issue: https://github.com/dotnet/csharplang/issues/[TBD]

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

Credit to [@HellBrick](https://github.com/HellBrick) for the [proposed mechanics](https://github.com/dotnet/roslyn/issues/5023#issuecomment-154728796) of the design, which have been formalized here.

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
constraint promotion.

#### Enhanced Type Inference Process - Modified Spec Text

The following text from [§12.6.3.3 The second phase](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#126333-the-second-phase) is modified:

> **12.6.3.3 The second phase**
>
> The second phase proceeds as follows:
>
> - All *unfixed* type variables `Xᵢ` which do not *depend on* ([§12.6.3.6](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12636-dependence)) any `Xₑ` are fixed ([§12.6.3.12](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#126312-fixing)).
> - If no such type variables exist, all *unfixed* type variables `Xᵢ` are *fixed* for which all of the following hold:
>   - There is at least one type variable `Xₑ` that *depends on* `Xᵢ`
>   - `Xᵢ` has a non-empty set of bounds
> - **If any type variables were fixed in the previous steps, for each newly fixed type variable `Xᵢ` with fixed type `Tᵢ`, and for each constraint `C` on `Xᵢ` that has not previously been promoted, add a synthetic argument to the argument list where the argument type is `Tᵢ`, and the corresponding parameter type is `C`. No conversions from expression shall apply to this synthetic argument. Mark constraint `C` as promoted for `Xᵢ`. Then restart the inference process from the first phase ([§12.6.3.2](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12632-the-first-phase)).**
> - If no such type variables exist and there are still *unfixed* type variables, type inference fails.
> - Otherwise, if no further *unfixed* type variables exist, type inference succeeds.
> - Otherwise, for all arguments `Eᵢ` with corresponding parameter type `Tᵢ` where the *output types* ([§12.6.3.5](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12635-output-types)) contain *unfixed* type variables `Xₑ` but the *input types* ([§12.6.3.4](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12634-input-types)) do not, an *output type inference* ([§12.6.3.7](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12637-output-type-inferences)) is made *from* `Eᵢ` *to* `Tᵢ`. Then the second phase is repeated.

### Algorithm Implementation Details

The constraint promotion algorithm integrates seamlessly into the existing type inference process through a goto-based loop mechanism.

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
