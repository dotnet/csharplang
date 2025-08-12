# Case types: To nest or not to nest?

Both [closed hierarchies](https://github.com/dotnet/csharplang/blob/main/proposals/closed-hierarchies.md) and [nominal type unions](https://github.com/dotnet/csharplang/blob/main/proposals/nominal-type-unions.md) allow the declaration of "case types" (derived types of closed classes or listed case types of unions) to be either nested or not - both approaches are perfectly valid for a programmer to choose.

With the [case declarations](https://github.com/dotnet/csharplang/blob/main/proposals/case-declarations.md) proposal in its current form we are implicitly encouraging a style where case types are nested within their closed class or union type. This is further supported by the [Target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-lookup.md) proposal, which eliminates the drudgery of fishing such nested members out again, at least when a target type is present.

But is nesting case types the right thing to do - or at least to encourage? Is the answer different for closed hierarchies vs union types? Should we use nesting ourselves if we define e.g. `Option` and `Result` types in our libraries?

Let's compare nested and non-nested approaches on different parameters. 

## Running example: Option types

`Option<T>` is a canonical example of a "union type" representing that there may or may not be a value of type `T`. We can define `Option<T>` either as a closed class or a union, and with either nested or top-level case types:

```csharp
// Closed hierarchy, non-nested case types
public closed record Option<T>;
public record None<T>() : Option<T>;
public record Some<T>(T value) : Option<T>;

// Closed hierarchy, nested case types
public closed record Option<T>
{
    public record None() : Option<T>;
    public record Some(T value) : Option<T>;
}

// Union, non-nested case types
public record None();
public record Some<T>(T value);
public union Option<T>(None, Some<T>);

// Union, nested case types
public union Option<T>(Option<T>.None, Option<T>.Some)
{
    public record None();
    public record Some(T Value);
}
```

We'll refer back to these declarations in nearly every section below.

## Existing types

For closed classes, the "case types" - the derived classes - are always declared in context of their base class. There are no "existing case types". You are always free to choose whether to nest them in the closed base class or declare them at the top level next to it.

Unions, on the other hand, are designed to allow existing types as case types. This means that case types may be declared independently elsewhere, unaware that anyone is using them as a case type in a union:

```csharp
public union Pet(Cat, Dog); // 'Cat' and 'Dog' are existing types
```

In those situations, you don't have the option of nesting the case types in the union type. Anything we do in the language to improve the nested-case-types experience won't help existing-types scenarios.

## Analogy with enums 

Unions/closed classes are somewhat analogous to enums, in that there's a list of possible contents. For enums that list is nested:

```csharp
public enum Color
{
    Red,
    Green, 
    Blue,
}
```

The analogy only goes so far: Enums list *values*, unions list *types*. And enums aren't actually listing *all* possible values, as by default they are not exhaustive (although [Closed Enums](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/Closed%20Enums.md) aim to help with that).

That said, leaning into a syntactic analogy with enums through nesting of case types may help developers better connect with the new concepts of closed hierarchies and unions.

## Pollution of declaration space

When nested, case types won't "pollute" the enclosing declaration space. This argument definitely has merit for enums, where "cases" are often many, and their naming is often somewhat contextual to the enclosing enum type.

It is not clear that this argument applies to the same degree to closed hierarchies and unions. Perhaps they will tend to have fewer cases with more self-explanatory names? 

If we look to other languages, case types generally are *not* nested. F# for example puts the case names into the enclosing scope: You can (and do) directly use e.g. the `Some` name to create or match an `Option` value without any target typing. In TypeScript there is also no notion of nesting case types in union types. Both seem to do fine - insofar as we're looking to address similar scenarios with unions in C#, it seems unlikely that we would have different needs.

In general, in C# it is fairly rare for types to be nested in other types if their primary use is intended to be outside of those types. Is there reason to believe that this would be different for unions?

## Avoiding explicit type parameters

Nested types implicitly have the same type parameters - if any - as the enclosing type. There's no need to repeat them. Looking at the nested `Option<T>` declarations above, the case types don't need explicit type parameters.

By contrast, top-level case types do need to explicitly take the type parameters they need and either pass them to a closed base class or instantiate them in a union case type list.

## Avoiding repetition in the declaration

The [case declarations](https://github.com/dotnet/csharplang/blob/main/proposals/case-declarations.md) feature provides a much abbreviated way of declaring nested case types for both closed classes and unions:

```csharp
// Case declarations in a closed hierarchy
public closed class Option<T>
{
    case None();
    case Some(T Value);
}

// Case declarations in a union
public union Option<T>
{
    case None();
    case Some(T Value);
}
```

Compared to the nested `Option<T>` declarations above, it uses the context of the outer declaration to fill in missing information that links the cases to the "union" type: For the closed class it fills in the base class of each of the case types, whereas for the union it fills in the list of case types. In both it avoids one explicit mention of `Option<T>` for each case type.

Would it be possible to have similar abbreviations for top-level case types?

For closed hierarchies it is hard to see how. The "linking information" between derived and closed type is in the derived class, in the form of a base class specification. If you're not nested in the intended base class, how do you imply what *should* be the base class, short of saying it explicitly?

For unions, however, the "linking" information is in the union declaration itself, in the form of the case type list. It would be entirely possible to allow syntax in that list that causes the case types to be implicitly declared, e.g.:

```csharp
public union Option<T>(case None(), case Some<T>(T Value));

// or simply

public union Option<T>(None(), Some<T>(T Value));
```

Where either the `case` keyword or the `(...)` parameter list itself is the syntactic signal that the case type doesn't refer to an existing type, but should be declared as a record of the given name.

Such a scheme may be great for simple situations like this, but may become unwieldy if you want to e.g. specify explicit bodies on the case types. It is possible that we can come up with better syntax.

## Avoiding type arguments

When referencing a case type from the "outside", you need to specify type arguments, regardless of whether it's nested or not. Which features could help with that?

First a scenario without useful target types:

```csharp
// non-nested, no target type
object o = new Some<int>(5); 
if (o is Some<int>(var value)) ...;

// nested, no target type
object o = new Option<int>.Some(5); 
if (o is Option<int>.Some(var value)) ...;
```

Here the only hope of doing a bit of type inference seems to be in the `new` expression. Long ago we discussed, but rejected, a [proposal](https://github.com/dotnet/roslyn/issues/2319) for allowing generic type inference in object creation expressions, similar to how it works in generic methods:

```csharp
// non-nested, no target type
object o = new Some(5); // '<int>' inferred from constructor argument

// nested, no target type
object o = new Option.Some(5); // '<int>' inferred from constructor argument?
```

The type inference for the non-nested example seems a straightforward application of existing type inference logic. For the nested case it gets more complicated; we'd essentially have to figure out how to allow type arguments for enclosing types to be inferred as well.

Turning to scenarios with a potentially useful target type:

```csharp
// non-nested, target type
Option<int> option = new Some<int>(5); 
if (option is Some<int>(var value)) ...;

// nested, target type
Option<int> option = new Option<int>.Some(5); 
if (option is Option<int>.Some(var value)) ...;
```

In the nested case, [target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-lookup.md) can abbreviate away not only the type argument but also the enclosing type:

```csharp
// nested
Option<int> option = new .Some(5);   // 'Option<int>' inferred
if (option is .Some(var value)) ...; // 'Option<int>' inferred
```

For the non-nested case, there are now proposals to:

- Allow target types to be taken into account in generic type inference: [Target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference).
- Allow generic type inference in `new` expressions: [Inference for constructor calls](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls).
- Allow generic type inference in type patterns, treating the incoming type (`Option<int>` in this case) as the "target type": [Inference for type patterns](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns).

Between them, they would enable:

```csharp
// non-nested
Option<int> option = new Some(5);   // '<int>' inferred
if (option is Some(var value)) ...; // '<int>' inferred
```

In summary, various auxiliary features have been proposed that would help avoid specifying type arguments when consuming the case types of closed classes or unions, both in nested and non-nested scenarios.

### Too many type parameters

When it comes to type parameters, there's a fundamental and significant difference in expressiveness between closed hierarchies and type unions. Looking back at the top-level versions of declarations in the running `Option<T>` example, the closed hierarchy version of `None` needs a type parameter of its own, whereas the union version does not:

```csharp
public record None<T> : Option<T>(); // closed hierarchy
public record None();                // union
```

The closed hierarchy `None<T>` has to be generic, even though it doesn't use `T` for anything! That's because the relationship between `None` and `Option` is expressed through inheritance,  the derived class needs to pass *some* `T` to its base class, and there is no other good option (no pun intended). As a result, each generic instantiation of `Option<T>` has its own `None` type. In a sense, every type parameter of the closed base class is forced upon all case types.

By contrast, in the union version, all generic instantiations of `Option<T>` share the same `None` type:

```csharp
var none = new None();
Option<string> stringOption = none; // Fine
Option<int> intOption = none; // Also fine
```

The upshot is that a generic union type, unlike a closed generic class, is free to have non-generic (or less-generic) case types that are shared between multiple generic instantiations of the union type.

Nesting changes this, however: Nested types in general are *also* forced to "inherit" all type parameters of the enclosing type - it happens implicitly in the language. So union types with nested case types are in the same situation as (nested or non-nested) closed hierarchies: They cannot have non-generic or less-generic case types. In the running example, there is no sharing of `None` values:

```csharp
var none = new Option<int>.None();  // Why do I need to specify `T` when `None` doesn't use it?
Option<string> stringOption = none; // Error: 'Option<int>.None' cannot be assigned to 'Option<string>.None'
```

This loss of expressiveness for nested union types may not be that big of a problem for our `Option<T>` example: `None` values carry no data, so people probably won't be too upset that they can't reuse `None` values across different content types. But that changes if we look at another common union example, the `Result` type. Let's try a nested version first:

```csharp
public union Result<TValue, TError>(Result<TValue, TError>.Success, Result<TValue, TError>.Failure)
{
    public record Success(TValue Value);
    public record Failure(TError Error);
}
```

Result types are often brought up in connection with "pipelining" architectures; where a function in a pipeline may look like this:

```csharp
// Produce a string from an incoming int, or propagate a failure
Result<string, string> M<T>(Result<int, string> result)
{
    if (result is Result<TValue, TError>.Failure failure) return failure; // Error
    ...
}
```

Even though the incoming and outgoing `Result` both have `TError = string`, a `Failure` value cannot just be passed through because it embeds an implicit `TValue` type argument that doesn't match! Not being able to propagate a `Failure` directly without deconstructing and reconstructing at every step seems unreasonably cumbersome.

(Of course the same issue applies the other way around: You cannot propagate a `Success` through a function that changes only the `TError` type).

By contrast, with top-level case type declarations:

```csharp
public record Success<TValue>(TValue Value);
public record Failure<TError>(TError Error);
public union Result<TValue, TError>(Success<TValue>, Failure<TError>);

Result<string, string> M<T>(Result<int, string> result)
{
    if (result is Failure<string> failure) return failure; // Fine! Both have 'Failure<string>' as a case.
}
```

Non-nested failures don't have to lug around which kind of value they would have had if they'd succeeded, and non-nested successes don't have to be distinguished by what kind of error they would have had if they had failed.

In summary, if case types need only some of the type parameters of their union, nesting them adds significant generic complexity for consuming code.

## Summary

- *Existing types*: Not all case types *can* be nested in a closed class or union type. For that reason alone it's important to consider features that improve the experience with non-nested case types.
- *Enum analogy*: Some union scenarios are probably closer to enum scenarios than others. Where applicable, the enum analogy can probably help users embrace closed classes and unions.
- *Declaration space pollution*: Enums use nesting to protect the wider code from being "polluted" by all the enum value names. On the other hand, union features in e.g. F# and TypeScript do fine without such nesting.
- *Repetition in declarations*: "Case declarations" propose a shorthand for declaring nested case types. Features could probably also be designed to abbreviate non-nested case types.
- *Avoiding type arguments*: "Target-typed static member access" sidesteps having to provide type arguments *when* there's a target type *and* the case type declaration is nested. For other scenarios we can imagine various extensions to generic type inference.
- *Too many type parameters*: Closed classes as well as nesting of case types force case types to have as many type parameters as the base type or union, even when they don't need them. Only unions with top-level case types escape this problem.

## Questions

This write-up is not in and of itself a proposal. But it brings up questions the answers to which can help guide which future concrete proposals to consider, and how to think about them:

- As we add convenience features to the language and concrete union types to the core libraries, should we adopt a bias for or against nesting of case types? Is one more important to support and encourage than the other?
- As we start thinking about guidance around these new features, can we identify scenarios that lend themselves more to nested or to non-nested approaches?