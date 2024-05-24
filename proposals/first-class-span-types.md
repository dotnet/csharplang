# First-class Span Types

## Summary

We introduce first-class support for `Span<T>` and `ReadOnlySpan<T>` in the language, including new implicit conversion types and consider them in more places,
allowing more natural programming with these integral types.

## Motivation

Since their introduction in C# 7.2, `Span<T>` and `ReadOnlySpan<T>` have worked their way into the language and base class library (BCL) in many key ways. This is great for
developers, as their introduction improves performance without costing developer safety. However, the language has held these types at arm's length in a few key ways,
which makes it hard to express the intent of APIs and leads to a significant amount of surface area duplication for new APIs. For example, the BCL has added a number of new
[tensor primitive APIs](https://github.com/dotnet/runtime/issues/94553) in .NET 9, but these APIs are all offered on `ReadOnlySpan<T>`. Because C# doesn't recognize the
relationship between `ReadOnlySpan<T>`, `Span<T>`, and `T[]`, it means that any developers looking to use those APIs with anything other than a `ReadOnlySpan<T>` have to explicitly
convert to a `ReadOnlySpan<T>`. Further, it also means that they don't have IDE tooling guiding them to use these APIs, since nothing will indicate to the IDE that it is valid
to pass them after conversion. There are also issues with generic inference in these scenarios. In order to provide maximum usability for this style of API, the BCL will have to
define an entire set of `Span<T>` and `T[]` overloads, which is a lot of duplicate surface area to maintain for no real gain. This proposal seeks to address the problem by
having the language more directly recognize these types and conversions.

## Detailed Design

The changes in this proposal will be tied to `LangVersion >= 13`.

### Implicit Span Conversions

We add a new type of implicit conversion to the list in [§10.2.1](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#1021-general), an
_implicit span conversion_. This conversion is defined as follows:

------

An implicit span conversion permits `array_types`, `System.Span<T>`, `System.ReadOnlySpan<T>`, and `string` to be converted between each other as follows:
* From any single-dimensional `array_type` with element type `Ei` to `System.Span<Ei>`
* From any single-dimensional `array_type` with element type `Ei` to `System.ReadOnlySpan<Ui>`, provided that `Ei` is covariance-convertible ([§18.2.3.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/interfaces.md#18233-variance-conversion)) to `Ui`
* From `System.Span<Ti>` to `System.ReadOnlySpan<Ui>`, provided that `Ti` is covariance-convertible ([§18.2.3.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/interfaces.md#18233-variance-conversion)) to `Ui`
* From `System.ReadOnlySpan<Ti>` to `System.ReadOnlySpan<Ui>`, provided that `Ti` is covariance-convertible ([§18.2.3.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/interfaces.md#18233-variance-conversion)) to `Ui`
* From `string` to `System.ReadOnlySpan<char>`

------

We also add _implicit span conversion_ to the list of standard implicit conversions
([§10.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#1042-standard-implicit-conversions)). This allows overload resolution to consider
them when performing argument resolution, as in the previously-linked API proposal.

There are no additional _explicit conversions_ ([§10.3.1](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/conversions.md#103-explicit-conversions)) other than the _implicit span conversions_.

We also add _implicit span conversion_ to the list of acceptable implicit conversions on the first parameter of an extension method when determining applicability
([12.8.9.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12893-extension-method-invocations)) (change in bold):

> An extension method `Cᵢ.Mₑ` is ***eligible*** if:
> 
> - `Cᵢ` is a non-generic, non-nested class
> - The name of `Mₑ` is *identifier*
> - `Mₑ` is accessible and applicable when applied to the arguments as a static method as shown above
> - An implicit identity, reference ~~or boxing~~ **, boxing, or span** conversion exists from *expr* to the type of the first parameter of `Mₑ`.

#### Variance

The goal of the variance section in _implicit span conversion_ is to replicate some amount of covariance for `System.ReadOnlySpan<T>`. Runtime changes would be required to fully
implement variance through generics here (see https://github.com/dotnet/csharplang/blob/main/proposals/ref-struct-interfaces.md for using `ref struct` types in generics), but we can
allow a limited amount of covariance through use of a proposed .NET 9 API: https://github.com/dotnet/runtime/issues/96952. This will allow the language to treat `System.ReadOnlySpan<T>`
as if the `T` was declared as `out T` in some scenarios. We do not, however, plumb this variant conversion through _all_ variance scenarios, and do not add it to the definition of
variance-convertible in [§18.2.3.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/interfaces.md#18233-variance-conversion). If in the future, we change the runtime
to more deeply understand the variance here, we can take the minor breaking change to fully recognize it in the language.

#### Patterns

Note that when `ref struct`s are used as a type in any pattern, only identity conversions are allowed:

```cs
class C<T> where T : allows ref struct
{
    void M1(T t) { if (t is T x) { } } // ok (T is T)
    void M2(R r) { if (r is R x) { } } // ok (R is R)
    void M3(T t) { if (t is R x) { } } // error (T is R)
    void M4(R r) { if (r is T x) { } } // error (R is T)
}
ref struct R { }
```

From the specification of *the is-type operator* ([§12.12.12.1][is-type-operator]):

> The result of the operation `E is T` [...] is a Boolean value indicating whether `E` is non-null and can successfully be converted to type `T`
> by a reference conversion, a boxing conversion, an unboxing conversion, a wrapping conversion, or an unwrapping conversion.
>
> [...]
>
> If `T` is a non-nullable value type, the result is `true` if `D` and `T` are the same type.

This behavior does not change with this feature, hence it will not be possible to write patterns for `Span`/`ReadOnlySpan`,
although similar patterns are possible for arrays (including variance):

```cs
using System;

M1<object[]>(["0"]); // prints
M1<string[]>(["1"]); // prints

void M1<T>(T t)
{
    if (t is object[] r) Console.WriteLine(r[0]); // ok
}

void M2<T>(T t) where T : allows ref struct
{
    if (t is ReadOnlySpan<object> r) Console.WriteLine(r[0]); // error
}
```

#### Code generation

The conversions will always exist, regardless of whether any runtime helpers used to implement them are present.
If the helpers are not present, attempting to use the conversion will result in a compile-time error that a compiler-required member is missing.

The compiler expects to use the following helpers or equivalents to implement the conversions:

| Conversion | Helpers |
|---|---|
| array to Span | `static implicit operator Span<T>(T[])` (defined in `Span<T>`) |
| array to ReadOnlySpan | `static implicit operator ReadOnlySpan<T>(T[])` (defined in `ReadOnlySpan<T>`) |
| Span to ReadOnlySpan | `static implicit operator ReadOnlySpan<T>(Span<T>)` (defined in `Span<T>`) and `static ReadOnlySpan<T>.CastUp<TDerived>(ReadOnlySpan<TDerived>)` |
| ReadOnlySpan to ReadOnlySpan | `static ReadOnlySpan<T>.CastUp<TDerived>(ReadOnlySpan<TDerived>)` |
| string to ReadOnlySpan | `static ReadOnlySpan<char> MemoryExtensions.AsSpan(string)` |

### Type inference

We update the type inferences section of the specification as follows (changes in **bold**).

> #### 12.6.3.9 Exact inferences
> 
> An *exact inference* *from* a type `U` *to* a type `V` is made as follows:
> 
> - If `V` is one of the *unfixed* `Xᵢ` then `U` is added to the set of exact bounds for `Xᵢ`.
> - Otherwise, sets `V₁...Vₑ` and `U₁...Uₑ` are determined by checking if any of the following cases apply:
>   - `V` is an array type `V₁[...]` and `U` is an array type `U₁[...]` of the same rank
>   - **`V` is a `Span<V₁>` and `U` is an array type `U₁[]` or a `Span<U₁>`**
>   - **`V` is a `ReadOnlySpan<V₁>` and `U` is an array type `U₁[]` or a `Span<U₁>` or `ReadOnlySpan<U₁>`**
>   - `V` is the type `V₁?` and `U` is the type `U₁`
>   - `V` is a constructed type `C<V₁...Vₑ>` and `U` is a constructed type `C<U₁...Uₑ>`  
>   If any of these cases apply then an *exact inference* is made from each `Uᵢ` to the corresponding `Vᵢ`.
> - Otherwise, no inferences are made.
> 
> #### 12.6.3.10 Lower-bound inferences
> 
> A *lower-bound inference from* a type `U` *to* a type `V` is made as follows:
> 
> - If `V` is one of the *unfixed* `Xᵢ` then `U` is added to the set of lower bounds for `Xᵢ`.
> - Otherwise, if `V` is the type `V₁?` and `U` is the type `U₁?` then a lower bound inference is made from `U₁` to `V₁`.
> - Otherwise, sets `U₁...Uₑ` and `V₁...Vₑ` are determined by checking if any of the following cases apply:
>   - `V` is an array type `V₁[...]` and `U` is an array type `U₁[...]`of the same rank
>   - **`V` is a `Span<V₁>` and `U` is an array type `U₁[]` or a `Span<U₁>`**
>   - **`V` is a `ReadOnlySpan<V₁>` and `U` is an array type `U₁[]` or a `Span<U₁>` or `ReadOnlySpan<U₁>`**
>   - `V` is one of `IEnumerable<V₁>`, `ICollection<V₁>`, `IReadOnlyList<V₁>>`, `IReadOnlyCollection<V₁>` or `IList<V₁>` and `U` is a single-dimensional array type `U₁[]`
>   - `V` is a constructed `class`, `struct`, `interface` or `delegate` type `C<V₁...Vₑ>` and there is a unique type `C<U₁...Uₑ>` such that `U` (or, if `U` is a type `parameter`, its effective base class or any member of its effective interface set) is identical to, `inherits` from (directly or indirectly), or implements (directly or indirectly) `C<U₁...Uₑ>`.
>   - (The “uniqueness” restriction means that in the case interface `C<T>{} class U: C<X>, C<Y>{}`, then no inference is made when inferring from `U` to `C<T>` because `U₁` could be `X` or `Y`.)  
>   If any of these cases apply then an inference is made from each `Uᵢ` to the corresponding `Vᵢ` as follows:
>   - If `Uᵢ` is not known to be a reference type then an *exact inference* is made
>   - Otherwise, if `U` is an array type then ~~a *lower-bound inference* is made~~ **inference depends on the type of `V`**:
>     - **If `V` is a `Span<Vᵢ>`, then an *exact inference* is made**
>     - **If `V` is an array type or a `ReadOnlySpan<Vᵢ>`, then a *lower-bound inference* is made**
>   - **Otherwise, if `U` is a `Span<Uᵢ>` then inference depends on the type of `V`**:
>     - **If `V` is a `Span<Vᵢ>`, then an *exact inference* is made**
>     - **If `V` is a `ReadOnlySpan<Vᵢ>`, then a *lower-bound inference* is made**
>   - **Otherwise, if `U` is a `ReadOnlySpan<Uᵢ>` and `V` is a `ReadOnlySpan<Vᵢ>` a *lower-bound inference* is made**:
>   - Otherwise, if `V` is `C<V₁...Vₑ>` then inference depends on the `i-th` type parameter of `C`:
>     - If it is covariant then a *lower-bound inference* is made.
>     - If it is contravariant then an *upper-bound inference* is made.
>     - If it is invariant then an *exact inference* is made.
> - Otherwise, no inferences are made.
> 
> #### 12.6.3.11 Upper-bound inferences
> 
> An *upper-bound inference from* a type `U` *to* a type `V` is made as follows:
> 
> - If `V` is one of the *unfixed* `Xᵢ` then `U` is added to the set of upper bounds for `Xᵢ`.
> - Otherwise, sets `V₁...Vₑ` and `U₁...Uₑ` are determined by checking if any of the following cases apply:
>   - `U` is an array type `U₁[...]` and `V` is an array type `V₁[...]` of the same rank
>   - **`U` is a `Span<U₁>` and `V` is an array type `V₁[]` or a `Span<V₁>`**
>   - **`U` is a `ReadOnlySpan<U₁>` and `V` is an array type `V₁[]` or a `Span<V₁>` or `ReadOnlySpan<V₁>`**
>   - `U` is one of `IEnumerable<Uₑ>`, `ICollection<Uₑ>`, `IReadOnlyList<Uₑ>`, `IReadOnlyCollection<Uₑ>` or `IList<Uₑ>` and `V` is a single-dimensional array type `Vₑ[]`
>   - `U` is the type `U1?` and `V` is the type `V1?`
>   - `U` is constructed class, struct, interface or delegate type `C<U₁...Uₑ>` and `V` is a `class, struct, interface` or `delegate` type which is `identical` to, `inherits` from (directly or indirectly), or implements (directly or indirectly) a unique type `C<V₁...Vₑ>`
>   - (The “uniqueness” restriction means that given an interface `C<T>{} class V<Z>: C<X<Z>>, C<Y<Z>>{}`, then no inference is made when inferring from `C<U₁>` to `V<Q>`. Inferences are not made from `U₁` to either `X<Q>` or `Y<Q>`.)  
>   If any of these cases apply then an inference is made from each `Uᵢ` to the corresponding `Vᵢ` as follows:
>   - If `Uᵢ` is not known to be a reference type then an *exact inference* is made
>   - Otherwise, if `V` is an array type then ~~an *upper-bound inference* is made~~ **inference depends on the type of `U`**:
>     - **If `U` is a `Span<Uᵢ>`, then an *exact inference* is made**
>     - **If `U` is an array type or a `ReadOnlySpan<Uᵢ>`, then a *upper-bound inference* is made**
>   - **Otherwise, if `V` is a `Span<Vᵢ>` then inference depends on the type of `U`**:
>     - **If `U` is a `Span<Uᵢ>`, then an *exact inference* is made**
>     - **If `U` is a `ReadOnlySpan<Uᵢ>`, then an *upper-bound inference* is made**
>   - **Otherwise, if `V` is a `ReadOnlySpan<Vᵢ>` and `U` is a `ReadOnlySpan<Uᵢ>` an *upper-bound inference* is made**:
>   - Otherwise, if `U` is `C<U₁...Uₑ>` then inference depends on the `i-th` type parameter of `C`:
>     - If it is covariant then an *upper-bound inference* is made.
>     - If it is contravariant then a *lower-bound inference* is made.
>     - If it is invariant then an *exact inference* is made.
> - Otherwise, no inferences are made.

### Breaking changes

As any proposal that changes conversions of existing scenarios, this proposal does introduce some new breaking changes. Here's a few examples:

#### User-defined conversions through inheritance

By adding _implicit span conversions_ to the list of standard implicit conversions, we can potentially change behavior when user-defined conversions are involved in a type hierarchy.
This example shows that change, in comparison to an integer scenario that already behaves as the new C# 13 behavior will.

```cs
Span<string> span = [];
var d = new Derived();
d.M(span); // Base today, Derived tomorrow
int i = 1;
d.M(i); // Derived today, demonstrates new behavior

class Base
{
    public void M(Span<string> s)
    {
        Console.WriteLine("Base");
    }

    public void M(int i)
    {
        Console.WriteLine("Base");
    }
}

class Derived : Base
{
    public static implicit operator Derived(ReadOnlySpan<string> r) => new Derived();
    public static implicit operator Derived(long l) => new Derived();

    public void M(Derived s)
    {
        Console.WriteLine("Derived");
    }
}
```

#### Extension method lookup

By allowing _implicit span conversions_ in extension method lookup, we can potentially change what extension method is resolved by overload resolution.

```cs
namespace N1
{
    using N2;

    public class C
    {
        public static void M()
        {
            Span<string> span = new string[0];
            span.Test(); // Prints N2 today, N1 tomorrow
        }
    }

    public static class N1Ext
    {
        public static void Test(this ReadOnlySpan<string> span)
        {
            Console.WriteLine("N1");
        }
    }
}

namespace N2
{
    public static class N2Ext
    {
        public static void Test(this Span<string> span)
        {
            Console.WriteLine("N2");
        }
    }
}
```

## Open questions

### Delegate signature matching (answered)

Should we allow variance conversion in delegate signature matching? For example:

```cs
using System;

Span<string> M1() => throw null!;
void M2(ReadOnlySpan<object> r) {}

delegate ReadOnlySpan<string> D1();
delegate void D2(ReadOnlySpan<string> r);

// Should these work?
D1 d1 = M1; // Convert Span<string>() to ReadOnlySpan<string>()
D2 d2 = M2; // Convert void(ReadOnlySpan<object>) to void(ReadOnlySpan<string>)

// These work today
string[] M3() => throw null!;
void M4(object[] a) {}

delegate object[] D3();
delegate void D4(string[] a);

D3 d3 = M3; // Convert string[]() to object[]()
D4 d4 = M4; // Convert void(object[]) to void(string[])
```

These conversions may not be possible to do without creating a wrapper lambda without runtime changes; the existing variant delegate conversions are possible to emit
without needing to create wrappers. We don't have precedent in the language for silent wrappers like this, and generally require users to create such wrapper lambdas themselves.

#### Answer

We will not allow variance in delegate conversions here. `D1 d1 = M1;` and `D2 d2 = M2;` will not compile. We could reconsider at a later point if use cases are discovered.

## Alternatives

Keep things as they are.

[is-type-operator]: https://github.com/dotnet/csharpstandard/blob/8c5e008e2fd6057e1bbe802a99f6ce93e5c29f64/standard/expressions.md#1212121-the-is-type-operator
