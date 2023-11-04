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
zero or more arguments specified.

Parameter collections are described further in *[Parameter collections](#parameter-collections)*.

### Parameter collections

The [Parameter arrays](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/classes.md#15626-parameter-arrays) section is renamed and adjusted as follows.

A parameter declared with a `params` modifier is a parameter collection. If a formal parameter list includes a parameter collection,
it shall be the last parameter in the list and it shall be of type specified in *[Method parameters](#method-parameters)* section.

> *Note*: It is not possible to combine the `params` modifier with the modifiers `in`, `out`, or `ref`. *end note*

A parameter collection permits arguments to be specified in one of two ways in a method invocation:

- The argument given for a parameter collection can be a single expression that is implicitly convertible to the parameter collection type.
  In this case, the parameter collection acts precisely like a value parameter.
- Or, the invocation can specify zero arguments for the parameter collection. In this case, the invocation creates an instance of the
  parameter collection type with no items according to the rules specified in [Collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md)
  for an empty literal ```[]```, and uses the newly created collection instance as the actual argument.
- Finally, the invocation can specify one or more arguments for the parameter collection, assuming the given parameter type
  is a valid target type for a collection expression that consists of these arguments used as expression elements in it,
  according to the rules specified in [Collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md).
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

### Overload resolution

## Open questions

### Opting out of implicit allocation on the call stack

Should we allow opt-ing out of _implicit allocation_ on the call stack?
Perhaps an attribute that can be applied to a method, type, or assembly.

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
