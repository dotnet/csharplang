# Target-typed generic type inference

Champion issue: [#9626](https://github.com/dotnet/csharplang/issues/9626)

## Summary
[summary]: #summary

Generic type inference may take a target type into account when ordinary binding cannot select a method or constructor. For instance, given:

```csharp
public static class MyCollection
{
    public static MyCollection<T> Create<T>() { ... }
}
public class MyCollection<T> : IEnumerable<T>
{
    public MyCollection() { ... }
}
```

We would allow the `Create` method to be called without type arguments when they can be inferred from a target type:

```csharp
// 'T' = 'string' inferred from target type
IEnumerable<string> c = MyCollection.Create();
```

The proposal also combines with [inference for constructor calls](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md) to provide target types to type-inferred constructor calls:

```csharp
// 'T' = 'string' inferred from target type
IEnumerable<string> c = new MyCollection();
```

## Motivation
[motivation]: #motivation

Generic constructors and factory methods often need explicit type arguments, even when the information is clear from the target type. That's because generic type inference only takes arguments into account, not target types.

## Detailed design
[design]: #detailed-design

A full draft specification is at [MadsTorgersen/csharpstandard#5](https://github.com/MadsTorgersen/csharpstandard/pull/5).

The proposal introduces a notion of ***target-aware binding*** to constructs that support type inference. If regular candidate selection fails, later conversion to a type `T` can try binding again with `T` as a target type, flowing it to type inference and allowing other candidates to become applicable.

The approach has the following components:

- Method invocations and object creation expressions where regular candidate selection fails get the new [classification](https://github.com/MadsTorgersen/csharpstandard/blob/d63468e7fb9a9e1da5ae7284a1d738068421ce2f/standard/expressions.md#122-expression-classifications) of ***target-dependent expression***.
- A new ***target-typing conversion*** from a target-dependent expression `E` to a type `T` attempts to do target-aware binding of `E` with the target type `T` and convert a successfully bound value to `T`. A similar path exists for reference targets.
- Only method invocations and object creation expressions offer a route for ***target-aware binding***. It differs from regular binding only in that the target type `T` is used to give generic candidates another chance at successful inference.
- When a target type is given to generic type inference, inferences are made from it to introduce additional bounds on type parameters.
- No other uses are made of a target type - specifically it is not used to pre-filter candidates based on return type.
- A target-dependent expression produces a binding-time error unless a target-typing conversion or binding to a reference target succeeds.


### Example

In the `MyCollection` example above, binding `Create()` without a target fails because there are no bounds for `T`, so the invocation is classified as target-dependent. A target-typing conversion to `IEnumerable<string>` then attempts target-aware binding, where, for the `Create<T>` candidate an *upper-bound inference* is made from `IEnumerable<string>` to the method's result type, `MyCollection<T>`. Because `IEnumerable<out T>` is covariant in `T`, this leads to an upper bound of `string` for `T`. Inference chooses `string`, the invocation produces `MyCollection<string>`, and that result converts successfully to `IEnumerable<string>`.

### Nested inference

Target-typing does not create a "loop" between outer and inner calls that both use type inference. An inner expression contributes a type to the outer expression's type inference *only* if it has a type through regular binding. Otherwise, the outer inference needs to complete without input from the inner expression, thus ensuring that there is a resolved *target type* for the inner expression, should it need it for its own type inference.

```csharp
static T Create<T>() => default!;
static T Choose<T>(T first, T second) => first;

var value = Choose("", Create());          // T is string for both calls
var error = Choose(Create(), Create());    // Error
```

In the first call, `Create()` has no type and contributes nothing to inference for `Choose<T>`. The first argument infers `string` for the outer `T`; the resulting parameter type then supplies the target that lets the inner call infer `string` as well. In the second call, neither inner invocation contributes a type, so the outer inference cannot determine `T`. There is no target for either level to start from.

## Drawbacks
[drawbacks]: #drawbacks

### Existing binding wins

The proposal only attempts target-aware binding if regular binding fails - i.e. gets classified as target-dependent. It doesn't help when regular binding succeeds, but produces an unusable result:

```csharp
static object Create() => "ordinary";
static T Create<T>() => default!;

// Error: Create() is successfully bound as the non-generic method
string value = Create();
```

### All candidates participate

Target-aware binding supplies the target to inference, but does not discard candidates whose result type is incompatible with it:

```csharp
class Pair<T1, T2> { }

static Pair<T, int> Pick<T>(string value) => new();
static Pair<T, string> Pick<T>(object value) => new();

Pair<string, string> pair = Pick(""); // Error
```

The target type allows both candidates to infer `T = string` and become candidates, but the `string` overload is better, and is picked even though its result does not convert to the target type.

## Alternatives
[alternatives]: #alternatives

### Broader target-aware binding

Should the existing binding always win? We could broaden the set of situations where target-aware binding is attempted to, say, situations where the existing binding succeeds, but its result does not match the target. We would essentially allow ourselves to *reinterpret* an expression, even though it was successfully bound! The exact change in mechanics would have to be worked out.

That would not "save" the `Create()` example above - the non-generic method would still be better, even when the generic candidate became applicable! In order to benefit from the extra chance at binding, we would *also* need to filter candidates against the target type, essentially making the proposal about target-typed *binding* more broadly, not just target-typed *inference*.

Each of these changes would increase the scope of the feature, and also likely the set of breaking changes it would incur. But it *would* make more scenarios "just work".

### Target-typing unification

Many expression forms and classifications are already "target-typed" today, in that they get their meaning through specific conversions, not primarily their intrinsic information. Examples include anonymous functions and (non-invoked) method groups, tuple literals, conditional expressions, `null` and `default` literals. Even another syntactic form of object creation expressions, `new(...)` without a type, is target typed! Each of these is "ad-hoc", in that it doesn't participate in a shared spec-level concept or mechanism of "target typing".

It's possible that we could gain simplification and generality by attempting a broader unification of the new target-typing scheme proposed here with the existing ones. However, I tried very hard to do this, and found that the differences were too significant, the benefits of sharing outweighed by an overload of new concepts and numerous exceptions and special cases. That does not rule this out as a future endeavor, but I found that it hindered more than helped in specifying this particular proposal.

## Open questions
[open]: #open-questions

### Breaking changes?

Whenever more conversions to a type are allowed to succeed, more candidate methods having that type as an argument type may become applicable, possibly changing the outcome of overload resolution.

However, because of the conservative nature of the feature design, I haven't been able to come up with a concrete breaking scenario. It's worth keeping an eye out for, though!

### Method group conversion

As currently specified, conversion of a method group to a delegate type immediately uses the delegate's return type as a target type. That has not really been thought through, though. A more conscious decision should be made.
