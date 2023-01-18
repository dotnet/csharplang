# Target-typed `new` expressions

## Summary
[summary]: #summary

Do not require type specification for constructors when the type is known. 

## Motivation
[motivation]: #motivation

Allow field initialization without duplicating the type.
```cs
Dictionary<string, List<int>> field = new() {
    { "item1", new() { 1, 2, 3 } }
};
```

Allow omitting the type when it can be inferred from usage.
```cs
XmlReader.Create(reader, new() { IgnoreWhitespace = true });
```

Instantiate an object without spelling out the type.
```cs
private readonly static object s_syncObj = new();
```

## Specification
[design]: #detailed-design

A new syntactic form, *target_typed_new* of the *object_creation_expression* is accepted in which the *type* is optional.

```antlr
object_creation_expression
    : 'new' type '(' argument_list? ')' object_or_collection_initializer?
    | 'new' type object_or_collection_initializer
    | target_typed_new
    ;
target_typed_new
    : 'new' '(' argument_list? ')' object_or_collection_initializer?
    ;
```

A *target_typed_new* expression does not have a type. However, there is a new *object creation conversion* that is an implicit conversion from expression, that exists from a *target_typed_new* to every type.

Given a target type `T`, the type `T0` is `T`'s underlying type if `T` is an instance of `System.Nullable`. Otherwise `T0` is `T`. The meaning of a *target_typed_new* expression that is converted to the type `T` is the same as the meaning of a corresponding *object_creation_expression* that specifies `T0` as the type.

It is a compile-time error if a *target_typed_new* is used as an operand of a unary or binary operator, or if it is used where it is not subject to an *object creation conversion*.

> **Open Issue:** should we allow delegates and tuples as the target-type?

The above rules include delegates (a reference type) and tuples (a struct type). Although both types are constructible, if the type is inferable, an anonymous function or a tuple literal can already be used.
```cs
(int a, int b) t = new(1, 2); // "new" is redundant
Action a = new(() => {}); // "new" is redundant

(int a, int b) t = new(); // OK; same as (0, 0)
Action a = new(); // no constructor found
```

### Miscellaneous

The following are consequences of the specification:

- `throw new()` is allowed (the target type is `System.Exception`)
- Target-typed `new` is not allowed with binary operators.
- It is disallowed when there is no type to target: unary operators, collection of a `foreach`, in a `using`, in a deconstruction, in an `await` expression, as an anonymous type property (`new { Prop = new() }`), in a `lock` statement, in a `sizeof`, in a `fixed` statement, in a member access (`new().field`), in a dynamically dispatched operation (`someDynamic.Method(new())`), in a LINQ query, as the operand of the `is` operator, as the left operand of the `??` operator,  ...
- It is also disallowed as a `ref`.
- The following kinds of types are not permitted as targets of the conversion
  - **Enum types:** `new()` will work (as `new Enum()` works to give the default value), but `new(1)` will not work as enum types do not have a constructor.
  - **Interface types:** This would work the same as the corresponding creation expression for COM types.
  - **Array types:** arrays need a special syntax to provide the length.	
  - **dynamic:** we don't allow `new dynamic()`, so we don't allow `new()` with `dynamic` as a target type.
  - **tuples:** These have the same meaning as an object creation using the underlying type.
  - All the other types that are not permitted in the *object_creation_expression* are excluded as well, for instance, pointer types.	

## Drawbacks
[drawbacks]: #drawbacks

There were some concerns with target-typed `new` creating new categories of breaking changes, but we already have that with `null` and `default`, and that has not been a significant problem.

## Alternatives
[alternatives]: #alternatives

Most of complaints about types being too long to duplicate in field initialization is about *type arguments* not the type itself, we could infer only type arguments like `new Dictionary(...)` (or similar) and infer type arguments locally from arguments or the collection initializer.

## Questions
[questions]: #questions

- Should we forbid usages in expression trees? (no)
- How the feature interacts with `dynamic` arguments? (no special treatment)
- How IntelliSense should work with `new()`? (only when there is a single target-type)

## Design meetings

- [LDM-2017-10-18](https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-10-18.md#100)
- [LDM-2018-05-21](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-05-21.md)
- [LDM-2018-06-25](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-06-25.md)
- [LDM-2018-08-22](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-08-22.md#target-typed-new)
- [LDM-2018-10-17](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-10-17.md)
- [LDM-2020-03-25](https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-03-25.md)
