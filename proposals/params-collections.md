# `params Collections`

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
  - The *type* has a constructor that can be invoked with no arguments, and the constructor is at least as accessible as the declaring member, and
  - The *type* has an instance (not an extension) method `Add` that can be invoked with a single argument of
    the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/statements.md#1395-the-foreach-statement),
    and the method is at least as accessible as the declaring member,
  in which case the *element type* is the *iteration type*
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
- If for at least one parameter `Mᵥ` uses the ***better parameter-passing choice*** ([§12.6.4.4](expressions.md#12644-better-parameter-passing-mode)) than the corresponding parameter in `Mₓ` and none of the parameters in `Mₓ` use the better parameter-passing choice than `Mᵥ`, `Mᵥ` is better than `Mₓ`.
- **Otherwise, if both methods have params collections and are applicable only in their expanded forms then
   `Mᵢ` is better than `Mₑ` if the same set of arguments corresponds to the collection elements for both methods, and one of the following holds
   (this corresponds to https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#overload-resolution):**
  - **params collection of `Mᵢ` is `System.ReadOnlySpan<Eᵢ>`, and params collection of `Mₑ` is `System.Span<Eₑ>`, and an implicit conversion exists from `Eᵢ` to `Eₑ`**
  - **params collection of `Mᵢ` is `System.ReadOnlySpan<Eᵢ>` or `System.Span<Eᵢ>`, and params collection of `Mₑ` is
    an *[array_or_array_interface__type](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#overload-resolution)*
    with *element type* `Eₑ`, and an implicit conversion exists from `Eᵢ` to `Eₑ`**
  - **both params collections are not *span_type*s, and an implicit conversion exists from params collection of `Mᵢ` to params collection of `Mₑ`**  
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
        M1(['1', '2', '3']); // Span overload is used
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
        M3("3", ["4"]); // Span overload is used, better on the first argument conversion, none is better on the second
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

### Dynamic vs. Static Binding

Expanded forms of candidates utilizing non-array params collections won't be considered as valid candidates by the current C# runtime binder.

#### Recap of the current rules

From [Static and Dynamic Binding](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#123-static-and-dynamic-binding):
> When no dynamic expressions are involved, C# defaults to static binding, which means that the compile-time types of subexpressions are used in the selection process.
> However, when one of the subexpressions in the operations listed above is a dynamic expression, **the operation is instead dynamically bound**.
>
> It is a compile time error if a method invocation is dynamically bound and any of the parameters, including the receiver, has the `in` modifier.

However, an exception to this rule exists for local functions.
From [Local function declarations](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/statements.md#1364-local-function-declarations):
> If the type of the argument to a local function is `dynamic`, **the function to be called must be resolved at compile time, not runtime**.

From [Compile-time checking of dynamic member invocation](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#1265-compile-time-checking-of-dynamic-member-invocation):
> Even though overload resolution of a dynamically bound operation takes place at run-time, it is sometimes possible at compile-time to know
> the list of function members from which an overload will be chosen:
>
> - For a delegate invocation ([§12.8.9.4](expressions.md#12894-delegate-invocations)), the list is a single function member with the same parameter list as the *delegate_type* of the invocation
> - For a method invocation ([§12.8.9.2](expressions.md#12892-method-invocations)) on a type, or on a value whose static type is not dynamic, the set of accessible methods in the method group
>   is known at compile-time.
> - For an object creation expression ([§12.8.16.2](expressions.md#128162-object-creation-expressions)) the set of accessible constructors in the type is known at compile-time.
> - For an indexer access ([§12.8.11.3](expressions.md#128113-indexer-access)) the set of accessible indexers in the receiver is known at compile-time.
>
> In these cases a limited compile-time check is performed on each member in the known set of function members, to see if it can be known for certain never to be invoked at run-time.
> For each function member `F` a modified parameter and argument list are constructed:
>
> - First, if `F` is a generic method and type arguments were provided, then those are substituted for the type parameters in the parameter list.
>   However, if type arguments were not provided, no such substitution happens.
> - Then, any parameter whose type is open (i.e., contains a type parameter; see [§8.4.3](types.md#843-open-and-closed-types)) is elided, along with its corresponding parameter(s).
>
> For `F` to pass the check, all of the following shall hold:
>
> - The modified parameter list for `F` is applicable to the modified argument list in terms of [§12.6.4.2](expressions.md#12642-applicable-function-member).
> - All constructed types in the modified parameter list satisfy their constraints ([§8.4.5](types.md#845-satisfying-constraints)).
> - If the type parameters of `F` were substituted in the step above, their constraints are satisfied.
> - If `F` is a static method, the method group shall not have resulted from a *member_access* whose receiver is known at compile-time to be a variable or value.
> - If `F` is an instance method, the method group shall not have resulted from a *member_access* whose receiver is known at compile-time to be a type.
> 
> If no candidate passes this test, a compile-time error occurs.

From [Invocation expressions](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12891-general):
> An *invocation_expression* is dynamically bound ([§12.3.3](expressions.md#1233-dynamic-binding)) if at least one of the following holds:
>
> - The *primary_expression* has compile-time type `dynamic`.
> - At least one argument of the optional *argument_list* has compile-time type `dynamic`.
>
> In this case, the compiler classifies the *invocation_expression* as a value of type `dynamic`. The rules below to determine
> the meaning of the *invocation_expression* are then applied at run-time, using the run-time type instead of the compile-time
> type of those of the *primary_expression* and arguments that have the compile-time type `dynamic`.
> If the *primary_expression* does not have compile-time type `dynamic`, then the method invocation undergoes a limited
> compile-time check as described in [§12.6.5 Compile-time checking of dynamic member invocation](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#1265-compile-time-checking-of-dynamic-member-invocation).

Similar wording exists for [Element access](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#128111-general) and 
[Object creation expressions](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#128162-object-creation-expressions).

#### New rules

As mentioned above, there is already an exception around binding invocations of local functions involving dynamic arguments.
A comment in the code implies that the exception is needed due to limitations of C# runtime binder. Apparently it cannot
handle invocations of local functions due to the way compiler emits them.  
Here are the precise rules for these cases:
- Invocations of local functions are bound statically
- If local function is generic and its type arguments are not specified explicitly, an error is reported
- If there is an ambiguity between normal and expanded forms of the function that cannot be resolved at compile time,
  an error is reported. Such ambiguity occurs when a single argument corresponds to params parameter,
  and the argument has type dynamic.   

New rules generalize and expand this behavior to [Invocation expressions](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12891-general), 
[Element access](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#128111-general), and 
[Object creation expressions](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#128162-object-creation-expressions)

If the *primary_expression* does not have compile-time type `dynamic`, then the method invocation undergoes a limited
compile-time check as described in [§12.6.5 Compile-time checking of dynamic member invocation](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#1265-compile-time-checking-of-dynamic-member-invocation).

If no candidate passes this test, a compile-time error occurs.

If only a single candidate passes the test, the invocation of the candidate is statically bound when all the following conditions are met:
- the candidate is either not generic, or its type arguments are explicitly specified;
- there is no ambiguity between normal and expanded forms of the candidate that cannot be resolved at compile time. 

Otherwise, the *invocation_expression* is dynamically bound.
- If only a single candidate passed the test above:
    - if that candidate is a local function, a compile-time error occurs;
    - if that candidate has a non-array params parameter, a compile-time error occurs.
- Otherwise, if any candidate passing the test has non-array params parameter and it could possibly be applicable only in an expanded form,
  a compile-time warning occurs.

We also should consider reverting/fixing spec violation that affects local functions today, see https://github.com/dotnet/roslyn/issues/71399. 

### Order of evaluation with non-array collections in non-trivial scenarios

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

