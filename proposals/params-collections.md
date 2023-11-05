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
creating the collection (`ImmutableArray<T>`, `ReadOnlySpan<T>`, etc). Today, in situations when an API takes a collection type,
developers add `params` overload that takes an array, construct the target collection and call the original overload with that
collection, thus consumers of the API have to trade an extra array allocation for convenience.

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
- A single dimensional *array type* `T[]`
- A *span type* `System.Span<T>` or `System.ReadOnlySpan<T>`
- A *type* with a valid [create method](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#create-methods)
  with *parameter type* `System.ReadOnlySpan<T>` and the method is at least as accessible as the *type*.
- A *struct* or *class type* that implements `System.Collections.Generic.IEnumerable<T>`
- A *struct* or *class type* that implements `System.Collections.IEnumerable` and *does not implement* `System.Collections.Generic.IEnumerable<T>`.
- An *interface type* `System.Collections.Generic.IEnumerable<T>`, `System.Collections.Generic.IReadOnlyCollection<T>`,
  `System.Collections.Generic.IReadOnlyList<T>`, `System.Collections.Generic.ICollection<T>`, or `System.Collections.Generic.IList<T>`

In a method invocation, a parameter collection permits either a single argument of the given parameter type to be specified, or
it permits zero or more arguments of the collection [iteration type](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/statements.md#1395-the-foreach-statement)
to be specified. Parameter collections are described further in *[Parameter collections](#parameter-collections)*.

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
  that is implicitly convertible to the parameter collection [iteration type](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/statements.md#1395-the-foreach-statement).
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
  zero or more value parameters of the parameter collection [iteration type](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/statements.md#1395-the-foreach-statement)
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

In case the parameter type sequences `{P₁, P₂, ..., Pᵥ}` and `{Q₁, Q₂, ..., Qᵥ}` are equivalent (i.e., each `Pᵢ` has an identity conversion to the corresponding `Qᵢ`), the following tie-breaking rules are applied, in order, to determine the better function member.

- If `Mᵢ` is a non-generic method and `Mₑ` is a generic method, then `Mᵢ` is better than `Mₑ`.
- Otherwise, if `Mᵢ` is applicable in its normal form and `Mₑ` has a params collection and is applicable only in its expanded form, then `Mᵢ` is better than `Mₑ`.
- **Otherwise, if both methods have params collections and are applicable only in their expanded forms then
   `Mᵢ` is better than `Mₑ` if one of the following holds
   (this corresponds to https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#overload-resolution):**
  - **params collections of `Mᵢ` is `System.ReadOnlySpan<Eᵢ>`, and params collection of `Mₑ` is `System.Span<Eₑ>`, and an implicit conversion exists from `Eᵢ` to `Eₑ`**
  - **params collections of `Mᵢ` is `System.ReadOnlySpan<Eᵢ>` or `System.Span<Eᵢ>`, and params collection of `Mₑ` is
    an *[array_or_array_interface_or_string_type](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#overload-resolution)*
    with *[iteration type](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/statements.md#1395-the-foreach-statement)* `Eₑ`, and an implicit conversion exists from `Eᵢ` to `Eₑ`**
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
- Otherwise, no function member is better.

### Ref safety

The [collection expressions ref safety section](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#ref-safety) is applicable to
the construction of parameter collections when APIs are invoked in their expanded form.

## Open questions

### Stack allocations 

Here is a quote from https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#unresolved-questions:
"Stack allocations for huge collections might blow the stack.  Should the compiler have a heuristic for placing this data on the heap?
Should the language be unspecified to allow for this flexibility?
We should follow the spec for [`params Span<T>`](https://github.com/dotnet/csharplang/issues/1757)." It sounds like we have to answer
the questions in context of this proposal.

## Alternatives 

There is an alternative [proposal](https://github.com/dotnet/csharplang/blob/main/proposals/params-span.md) that extends
`params` only for `ReadOnlySpan<T>`.

Also, one might say, that with [collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md)
now in the language, there is no need to extend `params` support at all. Foe any collection type. To consume an API with collection type, a developer
simply needs to add two characters, `[` before the expanded list of arguments, and `]` after it. Given that, extending `params` support might be an overkill,
especially that other languages are unlikely to support consumption of non-array `params` parameters any time soon.

## Related proposals
- https://github.com/dotnet/csharplang/issues/1757
- https://github.com/dotnet/csharplang/blob/main/proposals/format.md#extending-params
