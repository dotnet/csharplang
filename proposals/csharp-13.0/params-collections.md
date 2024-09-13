# `params Collections`

[!INCLUDE[Specletdisclaimer](speclet-disclaimer.md)]

## Summary

In C# 12 language added support for creating instances of collection types beyond just arrays.
See [collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md).
This proposal extends `params` support to all such collection types.

## Motivation

A `params` array parameter provides a convenient way to call a method that takes an arbitrary length list of arguments.
Today `params` parameter must be an array type. However, it might be beneficial for a developer to be able to have the same
convenience when calling APIs that take other collection types. For example, an `ImmutableArray<T>`, `ReadOnlySpan<T>`, or 
plain `IEnumerable`. Especially in cases when compiler is able to avoid an implicit array allocation for the purpose of
creating the collection (`ImmutableArray<T>`, `ReadOnlySpan<T>`, etc).

Today, in situations when an API takes a collection type, developers usually add `params` overload that takes an array,
construct the target collection and call the original overload with that collection, thus consumers of the API have to
trade an extra array allocation for convenience.

Another motivation is ability to add a params span overload and have it take precedence over the array version,
just by recompiling existing source code.

## Detailed design

### Method parameters

The [Method parameters](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/classes.md#1562-method-parameters) section is adjusted as follows.

```diff ANTLR
formal_parameter_list
    : fixed_parameters
-    | fixed_parameters ',' parameter_array
+    | fixed_parameters ',' parameter_collection
-    | parameter_array
+    | parameter_collection
    ;

-parameter_array
+parameter_collection
-    : attributes? 'params' array_type identifier
+    : attributes? 'params' 'scoped'? type identifier
    ;
```

A *parameter_collection* consists of an optional set of *attributes*, a `params` modifier, an optional `scoped` modifier,
a *type*, and an *identifier*. A parameter collection declares a single parameter of the given type with the given name.
The *type* of a parameter collection shall be one of the following valid target types for a collection expression
(see https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#conversions):
- A single dimensional *array type* `T[]`, in which case the *element type* is `T`
- A *span type*
  - `System.Span<T>`
  - `System.ReadOnlySpan<T>`  
  in which cases the *element type* is `T`
- A *type* with an appropriate *[create method](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#create-methods)*,
  which is at least as accessible as the declaring member, and with a corresponding *element type* resulting from that determination
- A *struct* or *class type* that implements `System.Collections.IEnumerable` where:
  - The *type* has a constructor that can be invoked with no arguments, and the constructor is at least as accessible as the declaring member.
  - The *type* has an instance (not an extension) method `Add` where:
    - The method can be invoked with a single value argument.
    - If the method is generic, the type arguments can be inferred from the argument.
    - The method is at least as accessible as the declaring member.

    In which case the *element type* is the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/statements.md#1395-the-foreach-statement) of the *type*.
- An *interface type*
  - `System.Collections.Generic.IEnumerable<T>`,
  - `System.Collections.Generic.IReadOnlyCollection<T>`,
  - `System.Collections.Generic.IReadOnlyList<T>`,
  - `System.Collections.Generic.ICollection<T>`,
  - `System.Collections.Generic.IList<T>`  
  in which cases the *element type* is `T`

In a method invocation, a parameter collection permits either a single argument of the given parameter type to be specified, or
it permits zero or more arguments of the collection's *element type* to be specified. 
Parameter collections are described further in *[Parameter collections](#parameter-collections)*.

A *parameter_collection* may occur after an optional parameter, but cannot have a default value – the omission of arguments for a *parameter_collection*
would instead result in the creation of an empty collection.

### Parameter collections

The [Parameter arrays](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/classes.md#15626-parameter-arrays) section is renamed and adjusted as follows.

A parameter declared with a `params` modifier is a parameter collection. If a formal parameter list includes a parameter collection,
it shall be the last parameter in the list and it shall be of type specified in *[Method parameters](#method-parameters)* section.

> *Note*: It is not possible to combine the `params` modifier with the modifiers `in`, `out`, or `ref`. *end note*

A parameter collection permits arguments to be specified in one of two ways in a method invocation:

- The argument given for a parameter collection can be a single expression that is implicitly convertible to the parameter collection type.
  In this case, the parameter collection acts precisely like a value parameter.
- Alternatively, the invocation can specify zero or more arguments for the parameter collection, where each argument is an expression
  that is implicitly convertible to the parameter collection's *element type*.
  In this case, the invocation creates an instance of the parameter collection type according to the rules specified in
  [Collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md)
  as though the arguments were used as expression elements in a collection expression in the same order,
  and uses the newly created collection instance as the actual argument.
  When constructing the collection instance, the original *unconverted* arguments are used.

Except for allowing a variable number of arguments in an invocation, a parameter collection is precisely equivalent to
a value parameter of the same type.

When performing overload resolution, a method with a parameter collection might be applicable, either in its normal form or
in its expanded form. The expanded form of a method is available only if the normal form of the method is not applicable and
only if an applicable method with the same signature as the expanded form is not already declared in the same type.

A potential ambiguity arises between the normal form and the expanded form of the method with a single parameter collection
argument when it can be used as the parameter collection itself and as the element of the parameter collection at the same time.
The ambiguity presents no problem, however, since it can be resolved by inserting a cast or using a collection expression,
if needed.

### Signatures and overloading

All the rules around `params` modifier in [Signatures and overloading](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/basic-concepts.md#76-signatures-and-overloading)
remain as is.

### Applicable function member

The [Applicable function member](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12642-applicable-function-member) section is adjusted as follows.

If a function member that includes a parameter collection is not applicable in its normal form, the function member might instead be applicable in its ***expanded form***:

- If parameter collection is not an array, an expanded form is not applicable for language versions C# 12 and below.
- The expanded form is constructed by replacing the parameter collection in the function member declaration with
  zero or more value parameters of the parameter collection's *element type*
  such that the number of arguments in the argument list `A` matches the total number of parameters.
  If `A` has fewer arguments than the number of fixed parameters in the function member declaration,
  the expanded form of the function member cannot be constructed and is thus not applicable.
- Otherwise, the expanded form is applicable if for each argument in `A`, one of the following is true:
  - the parameter-passing mode of the argument is identical to the parameter-passing mode of the corresponding parameter, and
    - for a fixed value parameter or a value parameter created by the expansion, an implicit conversion exists from
      the argument expression to the type of the corresponding parameter, or
    - for an `in`, `out`, or `ref` parameter, the type of the argument expression is identical to the type of the corresponding parameter.
  - the parameter-passing mode of the argument is value, and the parameter-passing mode of the corresponding parameter is input,
    and an implicit conversion exists from the argument expression to the type of the corresponding parameter

### Better function member

The [Better function member](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12643-better-function-member) section is adjusted as follows.

Given an argument list `A` with a set of argument expressions `{E₁, E₂, ..., Eᵥ}` and two applicable function members `Mᵥ` and `Mₓ` with parameter types `{P₁, P₂, ..., Pᵥ}` and `{Q₁, Q₂, ..., Qᵥ}`, `Mᵥ` is defined to be a ***better function member*** than `Mₓ` if

- for each argument, the implicit conversion from `Eᵥ` to `Qᵥ` is not better than the implicit conversion from `Eᵥ` to `Pᵥ`, and
- for at least one argument, the conversion from `Eᵥ` to `Pᵥ` is better than the conversion from `Eᵥ` to `Qᵥ`.

In case the parameter type sequences `{P₁, P₂, ..., Pᵥ}` and `{Q₁, Q₂, ..., Qᵥ}` are equivalent (i.e., each `Pᵢ` has an identity conversion to the corresponding `Qᵢ`), the following tie-breaking rules are applied, in order, to determine the better function member.

- If `Mᵢ` is a non-generic method and `Mₑ` is a generic method, then `Mᵢ` is better than `Mₑ`.
- Otherwise, if `Mᵢ` is applicable in its normal form and `Mₑ` has a params collection and is applicable only in its expanded form, then `Mᵢ` is better than `Mₑ`.
- Otherwise, if both methods have params collections and are applicable only in their expanded forms,
  and if the params collection of `Mᵢ` has fewer elements than the params collection of `Mₑ`,
  then `Mᵢ` is better than `Mₑ`.
- Otherwise, if `Mᵥ` has more specific parameter types than `Mₓ`, then `Mᵥ` is better than `Mₓ`. Let `{R1, R2, ..., Rn}` and `{S1, S2, ..., Sn}` represent the uninstantiated and unexpanded parameter types of `Mᵥ` and `Mₓ`. `Mᵥ`’s parameter types are more specific than `Mₓ`s if, for each parameter, `Rx` is not less specific than `Sx`, and, for at least one parameter, `Rx` is more specific than `Sx`:
  - A type parameter is less specific than a non-type parameter.
  - Recursively, a constructed type is more specific than another constructed type (with the same number of type arguments) if at least one type argument is more specific and no type argument is less specific than the corresponding type argument in the other.
  - An array type is more specific than another array type (with the same number of dimensions) if the element type of the first is more specific than the element type of the second.
- Otherwise if one member is a non-lifted operator and the other is a lifted operator, the non-lifted one is better.
- If neither function member was found to be better, and all parameters of `Mᵥ` have a corresponding argument whereas default arguments need to be substituted for at least one optional parameter in `Mₓ`, then `Mᵥ` is better than `Mₓ`.
- If for at least one parameter `Mᵥ` uses the ***better parameter-passing choice*** ([§12.6.4.4](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12644-better-parameter-passing-mode)) than the corresponding parameter in `Mₓ` and none of the parameters in `Mₓ` use the better parameter-passing choice than `Mᵥ`, `Mᵥ` is better than `Mₓ`.
- **Otherwise, if both methods have params collections and are applicable only in their expanded forms then
   `Mᵢ` is better than `Mₑ` if the same set of arguments corresponds to the collection elements for both methods, and one of the following holds
   (this corresponds to https://github.com/dotnet/csharplang/blob/main/proposals/collection-expressions-better-conversion.md):**
  - **both params collections are not *span_type*s, and an implicit conversion exists from params collection of `Mᵢ` to params collection of `Mₑ`**  
  - **params collection of `Mᵢ` is `System.ReadOnlySpan<Eᵢ>`, and params collection of `Mₑ` is `System.Span<Eₑ>`, and an identity conversion exists from `Eᵢ` to `Eₑ`**
  - **params collection of `Mᵢ` is `System.ReadOnlySpan<Eᵢ>` or `System.Span<Eᵢ>`, and params collection of `Mₑ` is
    an *[array_or_array_interface__type](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#overload-resolution)*
    with *element type* `Eₑ`, and an identity conversion exists from `Eᵢ` to `Eₑ`**
- Otherwise, no function member is better.

The reason why the new tie-breaking rule is placed at the end of the list is the last sub item
> - **both params collections are not *span_type*s, and an implicit conversion exists from params collection of `Mᵢ` to params collection of `Mₑ`** 

it is applicable to arrays and, therefore, performing the tie-break earlier will introduce a behavior change for existing scenarios.

For example:
``` C#
class Program
{
    static void Main()
    {
        Test(1);
    }

    static void Test(in int x, params C2[] y) {} // There is an implicit conversion from `C2[]` to `C1[]`
    static void Test(int x, params C1[] y) {} // Better candidate because of "better parameter-passing choice"
}

class C1 {}
class C2 : C1 {}
```

If any of the previous tie-breaking rules apply (including the "better arguments conversions" rule), the overload resolution result
can be different by comparison to the case when an explicit collection expression is used as an argument instead.

For example:
``` C#
class Program
{
    static void Test1()
    {
        M1(['1', '2', '3']); // IEnumerable<char> overload is used because `char` is an exact match
        M1('1', '2', '3');   // IEnumerable<char> overload is used because `char` is an exact match
    }

    static void M1(params IEnumerable<char> value) {}
    static void M1(params System.ReadOnlySpan<MyChar> value) {}

    class MyChar
    {
        private readonly int _i;
        public MyChar(int i) { _i = i; }
        public static implicit operator MyChar(int i) => new MyChar(i);
        public static implicit operator char(MyChar c) => (char)c._i;
    }

    static void Test2()
    {
        M2([1]); // Span overload is used
        M2(1);   // Array overload is used, not generic
    }

    static void M2<T>(params System.Span<T> y){}
    static void M2(params int[] y){}

    static void Test3()
    {
        M3("3", ["4"]); // Ambiguity, better-ness of argument conversions goes in opposite directions.
        M3("3", "4");   // Ambiguity, better-ness of argument conversions goes in opposite directions.
                        // Since parameter types are different ("object, string" vs. "string, object"), tie-breaking rules do not apply
    }

    static void M3(object x, params string[] y) {}
    static void M3(string x, params Span<object> y) {}
}
```

However, our primary concern are scenarios where overloads differ only by params collection type,
but the collection types have the same element type. The behavior should be consistent with 
explicit collection expressions for these cases.


The "**if the same set of arguments corresponds to the collection elements for both methods**" condition is important for scenarios like:
``` C#
class Program
{
    static void Main()
    {
        Test(x: 1, y: 2); // Ambiguous
    }

    static void Test(int x, params System.ReadOnlySpan<int> y) {}
    static void Test(int y, params System.Span<int> x) {}
}
```

It doesn't feel reasonable to "compare" collections that are built from different elements.

>This section was reviewed at [LDM](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-29.md#better-function-member-changes)
>and was approved.

One effect of these rules is that when `params` of different element types are exposed, these will be ambiguous when called with an empty argument list.
For example:

```cs
class Program
{
    static void Main()
    {
        // Old scenarios
        C.M1(); // Ambiguous since params arrays were introduced
        C.M1([]); // Ambiguous since params arrays were introduced

        // New scenarios
        C.M2(); // Ambiguous in C# 13
        C.M2([]); // Ambiguous in C# 13
        C.M3(); // Ambiguous in C# 13
        C.M3([]); // Ambiguous in C# 13
    }

    public static void M1(params int[] a) {
    }
    
    public static void M1(params int?[] a) {
    }
    
    public static void M2(params ReadOnlySpan<int> a) {
    }
    
    public static void M2(params Span<int?> a) {
    }
    
    public static void M3(params ReadOnlySpan<int> a) {
    }
    
    public static void M3(params ReadOnlySpan<int?> a) {
    }
}
```

Given that we prioritize element type over all else, this seems reasonable; there's nothing to tell the language whether the user would prefer `int?`
over `int` in this scenario.

### Dynamic Binding

Expanded forms of candidates utilizing non-array params collections won't be considered as valid candidates by the current C# runtime binder.

If the *primary_expression* does not have compile-time type `dynamic`, then the method invocation undergoes a limited
compile-time check as described in [§12.6.5 Compile-time checking of dynamic member invocation](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#1265-compile-time-checking-of-dynamic-member-invocation).

If only a single candidate passes the test, the invocation of the candidate is statically bound when all the following conditions are met:
- the candidate is a local function
- the candidate is either not generic, or its type arguments are explicitly specified;
- there is no ambiguity between normal and expanded forms of the candidate that cannot be resolved at compile time. 

Otherwise, the *invocation_expression* is dynamically bound.

If only a single candidate passed the test above:
- if that candidate is a local function, a compile-time error occurs;
- if that candidate is applicable only in expanded form utilizing non-array params collections, a compile-time error occurs.

We also should consider reverting/fixing spec violation that affects local functions today, see https://github.com/dotnet/roslyn/issues/71399. 

>[LDM](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-29.md#dynamic-and-ref-local-function-bugfixing)
>confirmed that we want to fix this spec violation. 

### Expression trees

Collection expressions are not supported in expression trees. Similarly, expanded forms of non-array params collections
will not be supported in expression trees. We will not be changing how the compiler binds lambdas for expression trees
with the goal to avoid usage of APIs utilizing expanded forms of non-array params collections.

### Order of evaluation with non-array collections in non-trivial scenarios

> This section was reviewed at [LDM](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-31.md#params-collections-evaluation-orders)
> and was approved. Despite the fact that array cases deviate from other collections, the official language specification
> doesn't have to specify different rules for arrays. The deviations could simply be treated as an implementation artifact.
> At the same time we do not intend to change the existing behavior around arrays.

#### Named arguments

A collection instance is created and populated after the lexically previous argument is evaluated, 
but before the lexically following argument is evaluated.

For example:
``` C#
class Program
{
    static void Main()
    {
        Test(b: GetB(), c: GetC(), a: GetA());
    }

    static void Test(int a, int b, params MyCollection c) {}

    static int GetA() => 0;
    static int GetB() => 0;
    static int GetC() => 0;
}
```
The order of evaluation is the following:
1. `GetB` is called
2. `MyCollection` is created and populated, `GetC` is called in the process
3. `GetA` is called
4. `Test` is called

Note, in params array case, the array is created right before the target methos is invoked, after all
arguments are evaluated in their lexical order.

#### Compound assignment

A collection instance is created and populated after the lexically previous index is evaluated, 
but before the lexically following index is evaluated. The instance is used to invoke getter
and setter of the target indexer.

For example:
``` C#
class Program
{
    static void Test(Program p)
    {
        p[GetA(), GetC()]++;
    }

    int this[int a, params MyCollection c] { get => 0; set {} }

    static int GetA() => 0;
    static int GetC() => 0;
}
```
The order of evaluation is the following:
1. `GetA` is called and cached
2. `MyCollection` is created, populated and cached, `GetC` is called in the process
3. Indexer's getter is invoked with cached values for indexes
4. Result is incremented
5. Indexer's setter is invoked with cached values for indexes and the result of the increment

An example with an empty collection:
``` C#
class Program
{
    static void Test(Program p)
    {
        p[GetA()]++;
    }

    int this[int a, params MyCollection c] { get => 0; set {} }

    static int GetA() => 0;
}
```
The order of evaluation is the following:
1. `GetA` is called and cached
2. An empty `MyCollection` is created and cached
3. Indexer's getter is invoked with cached values for indexes
4. Result is incremented
5. Indexer's setter is invoked with cached values for indexes and the result of the increment

#### Object Initializer

A collection instance is created and populated after the lexically previous index is evaluated, 
but before the lexically following index is evaluated. The instance is used to invoke indexer's
getter as many times as necessary, if any.

For example:
``` C#
class C1
{
    public int F1;
    public int F2;
}

class Program
{
    static void Test()
    {
        _ = new Program() { [GetA(), GetC()] = { F1 = GetF1(), F2 = GetF2() } };
    }

    C1 this[int a, params MyCollection c] => new C1();

    static int GetA() => 0;
    static int GetC() => 0;
    static int GetF1() => 0;
    static int GetF2() => 0;
}
```
The order of evaluation is the following:
1. `GetA` is called and cached
2. `MyCollection` is created, populated and cached, `GetC` is called in the process
3. Indexer's getter is invoked with cached values for indexes
4. `GetF1` is evaluated and assigned to `F1` field of `C1` retuned on the previous step
5. Indexer's getter is invoked with cached values for indexes
6. `GetF2` is evaluated and assigned to `F2` field of `C1` retuned on the previous step

Note, in params array case, its elements are evaluated and cached, but a new instance of an array (with the same values inside)
is used for each invocation of indexer's getter instead. For the example above, the order of evaluation is the following:
1. `GetA` is called and cached
2. `GetC` is called and cached
3. Indexer's getter is invoked with cached `GetA` and new array populated with cached `GetC`
4. `GetF1` is evaluated and assigned to `F1` field of `C1` retuned on the previous step
5. Indexer's getter is invoked with cached `GetA` and new array populated with cached `GetC`
6. `GetF2` is evaluated and assigned to `F2` field of `C1` retuned on the previous step


An example with an empty collection:
``` C#
class C1
{
    public int F1;
    public int F2;
}

class Program
{
    static void Test()
    {
        _ = new Program() { [GetA()] = { F1 = GetF1(), F2 = GetF2() } };
    }

    C1 this[int a, params MyCollection c] => new C1();

    static int GetA() => 0;
    static int GetF1() => 0;
    static int GetF2() => 0;
}
```
The order of evaluation is the following:
1. `GetA` is called and cached
2. An empty `MyCollection` is created and cached
3. Indexer's getter is invoked with cached values for indexes
4. `GetF1` is evaluated and assigned to `F1` field of `C1` retuned on the previous step
5. Indexer's getter is invoked with cached values for indexes
6. `GetF2` is evaluated and assigned to `F2` field of `C1` retuned on the previous step

 
### Ref safety

The [collection expressions ref safety section](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#ref-safety) is applicable to
the construction of parameter collections when APIs are invoked in their expanded form.

Params parameters are implicitly `scoped` when their type is a ref struct. UnscopedRefAttribute can be used to override that.

### Metadata

In metadata we could mark non-array `params` parameters with `System.ParamArrayAttribute`, as `params` arrays are marked today.
However, it looks like we will be much safer to use a different attribute for non-array `params` parameters.
For example, the current VB compiler will not be able to consume them decorated with `ParamArrayAttribute` neither in normal, nor in expanded form. Therefore, an addition of 'params' modifier is likely to break VB consumers, and very likely consumers from other languages or tools.

Given that, non-array `params` parameters are marked with a new `System.Runtime.CompilerServices.ParamCollectionAttribute`.
```
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
    public sealed class ParamCollectionAttribute : Attribute
    {
        public ParamCollectionAttribute() { }
    }
}
```

> This section was reviewed at [LDM](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-02-21.md#metadata-format)
> and was approved.

## Open questions

### Stack allocations 

Here is a quote from https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#unresolved-questions:
"Stack allocations for huge collections might blow the stack.  Should the compiler have a heuristic for placing this data on the heap?
Should the language be unspecified to allow for this flexibility?
We should follow the spec for [`params Span<T>`](https://github.com/dotnet/csharplang/issues/1757)." It sounds like we have to answer
the questions in context of this proposal.

### [Resolved] Implicitly `scoped` params 

There was a suggestion that, when `params` modifies a `ref struct` parameter, it should be considered as declared `scoped`.
The argument is made that number of cases where you want the parameter to be scoped is virtually 100% when looking through
the BCL cases. In a few cases that need that, the default could be overwritten with `[UnscopedRef]`.

However, it might be undesirable to change the default simply based on presence of `params` modifier. Especially, that
in overrides/implements scenarios `params` modifier doesn't have to match.

#### Resolution:
Params parameters are implicitly scoped - https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-11-15.md#params-improvements.

### [Resolved] Consider enforcing `scoped` or `params` across overrides

We've previously stated that `params` parameters should be `scoped` by default. However, this introduces odd behavior in overridding, due
to our existing rules around restating `params`:

```cs
class Base
{
    internal virtual Span<int> M1(scoped Span<int> s1, params Span<int> s2) => throw null!;
}

class Derived : Base
{
    internal override Span<int> M1(Span<int> s1, // Error, missing `scoped` on override
                                   Span<int> s2  // No error: parameter is implicitly params, and therefore implicitly scoped
                                  ) => throw null!;
}
```

We have a difference in behavior between carrying the `params` and carrying the `scoped` across overrides here: `params` is inherited implicitly,
and with it `scoped`, while `scoped` by itself is _not_ inherited implicitly and must be repeated at every level.

**Proposal**: We should enforce that overrides of `params` parameters must explicitly state `params` or `scoped` if the original definition is a
`scoped` parameter. In other words, `s2` in `Derived` must have `params`, `scoped`, or both.

#### Resolution:

We will require explicitly stating `scoped` or `params` on override of a `params` parameter when a non-`params` parameter would be required to do so -
https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-02-21.md#params-and-scoped-across-overrides.

### [Resolved] Should presence of required members prevent declaration of `params` parameter?

Consider the following example:
``` C#
using System.Collections;
using System.Collections.Generic;

public class MyCollection1 : IEnumerable<long>
{
    IEnumerator<long> IEnumerable<long>.GetEnumerator() => throw null;
    IEnumerator IEnumerable.GetEnumerator() => throw null;
    public void Add(long l) => throw null;

    public required int F; // Collection has required member and constructor doesn't initialize it explicitly
}

class Program
{
    static void Main()
    {
        Test(2, 3); // error CS9035: Required member 'MyCollection1.F' must be set in the object initializer or attribute constructor.
    }

    // Should an error be reported for the parameter indicating that the constructor that is required
    // to be available doesn't initialize required members? In other words, should one be able
    // to declare such a parameter under the specified conditions?
    static void Test(params MyCollection1 a)
    {
    }
}
```
#### Resolution:

We will validate `required` members against the constructor that is used to determine eligibility to be a `params` parameter at the declaration site -
https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-02-21.md#required-members-and-params-parameters.

## Alternatives 

There is an alternative [proposal](https://github.com/dotnet/csharplang/blob/main/proposals/params-span.md) that extends
`params` only for `ReadOnlySpan<T>`.

Also, one might say, that with [collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md)
now in the language, there is no need to extend `params` support at all. For any collection type. To consume an API with collection type, a developer
simply needs to add two characters, `[` before the expanded list of arguments, and `]` after it. Given that, extending `params` support might be an overkill,
especially that other languages are unlikely to support consumption of non-array `params` parameters any time soon.

## Related proposals
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/blob/main/proposals/format.md#extending-params
 
## Related design meetings

- https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-11-15.md#params-improvements
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-08.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-10.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-29.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-01-31.md#params-collections-evaluation-orders
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-02-21.md#params-collections
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-04-22.md#effect-of-language-version-on-overload-resolution-in-presence-of-params-collections
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-04-24.md#adjust-dynamic-binding-rules-for-a-situation-of-a-single-applicable-candidate
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-05-01.md#adjust-binding-rules-in-the-presence-of-a-single-candidate
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-03.md#params-collections-and-dynamic
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-12.md#params-span-breaks
- https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-17.md#params-span-breaks

