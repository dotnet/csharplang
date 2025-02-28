# Unbound generic types in `nameof`

Champion issue: <https://github.com/dotnet/csharplang/issues/8662>

## Summary

Allows unbound generic types to be used with `nameof`, as in `nameof(List<>)` to obtain the string `"List"`,
rather than having to specify an unused generic type argument in order to obtain the same string.

## Motivation

This is a small feature that removes a common frustration: "why do I have to pick a generic type argument when
the choice has no effect on the evaluation of the expression?" It's very odd to require something to be specified
within an operand when it has no impact on the result. Notably, `typeof` does not suffer from this limitation.

This is also not simply about brevity and simplicity. Once some arbitrary type argument has been chosen in a
`nameof` expression, such as `object?`, changing a constraint on a type parameter can break uses of `nameof`
unnecessarily. Insult becomes added to injury in this scenario. Satisfying the type parameter can sometimes
require declaring a dummy class to implement an interface which is constraining the type parameter. Now there's
unused metadata and a strange name invented, all for the purpose of adding a type argument to the `nameof`
expression, a type argument which `nameof` will ultimately ignore even though it requires it.

In some rarer cases, with a generic class constraint, it's not even _possible_ to use `nameof` because it's not
possible to inherit from a base class which is used as a generic constraint, due to the base class having an
internal constructor or internal abstract member.

A simple tweak allows this to fall out in the language, with minimal implementation complexity in the compiler.

## Description

Unbound type names become available for use with `nameof`:

- `nameof(A<>)` evaluates to `"A"`
- `nameof(Dictionary<,>)` evaluates to `"Dictionary"`

Additionally, chains of members will be able to be accessed on unbound types, just like on bound types:

```cs
class A<T>
{
    public List<T> B { get; }
}
```

- `nameof(A<>.B)` evaluates to `"B"`
- `nameof(A<>.B.Count)` evaluates to `"Count"`

Even members of generic type parameters can be accessed, consistent with how `nameof` already works when the
type is not unbound. Since the type is unbound, there is no type information on these members beyond what
`System.Object` or any additional generic constraints provide.

```cs
class A<TItem, TCollection> where TCollection : IReadOnlyCollection<TItem>
{
    public TCollection B { get; }
}
```

- `nameof(A<,>.B)` evaluates to `"B"`
- `nameof(A<,>.B.Count)` evaluates to `"Count"`.

### Not supported

1. Support is not included for nesting an unbound type as a type argument to another generic type, such as
   `A<B<>>` or `A<B<>>.C.D`. Even though this could be logically implemented, such expressions have no precedent
   in the language, and there is not sufficient motivation to introduce it:

   - `A<B<>>` provides no benefits over `A<>`, and `A<B<>>.C` provides no benefits over `A<>.C`.

   - If `C` returns the `T` of `A<T>`, `A<B<>>.C.D` can be written more directly as `B<>.D`. If it
     returns some other type, then `A<B<>>.C.D` provides no benefits over `A<>.C.D`.

   - `typeof()` has the same restrictions.

2. Support is not included for partially unbound types, such as `Dictionary<int,>`. Similarly, such expressions
   have no precedent in the language, and there is not sufficient motivation. That form provides no benefits over
   `Dictionary<,>`, and accessing members of `T`-returning members can be written more directly without wrapping
   in a partially unbound type.

## Detailed design

### Grammar changes

```diff
nameof_expression
    : 'nameof' '(' named_entity ')'
    ;
    
named_entity
-   : named_entity_target ('.' identifier type_argument_list?)*
+   : named_entity_target ('.' identifier (type_argument_list | generic_dimension_specifier)?)*
    ;
    
named_entity_target
    : simple_name
    | 'this'
    | 'base'
    | predefined_type 
    | qualified_alias_member
    ;

simple_name
-   : identifier type_argument_list?
+   : identifier (type_argument_list | generic_dimension_specifier)?
    ;

generic_dimension_specifier
    : '<' ','* '>'
```

This now allows names like `X<>` or `X<,>` to be considered simple names, where they were previously only
supported as `unbound_type_name`s within a `typeof` expression.

### Semantic changes

It is an error to use `generic_dimension_specifier` outside of a `typeof` or `nameof` expression.  Within
either of those expressions it is an error to use `generic_dimension_specifier` within a `type_argument_list`,
`array_type`, `pointer_type` or `nullable_type`.  In other words the following are all illegal:

```c#
// Illegal, not inside `nameof` or `typeof`
var v = SomeType<>.StaticMember;
```

```c#
// All illegal
var v = typeof(List<>[]);
var v = typeof(List<>*);
var v = typeof((List<> a, int b));
```

Note: The above rules effectively serve to make it so that `generic_dimension_specifier` cannot show up within
another type.  However, at the top level, where the specifier is allowed, it is fine to mix and match with normal
`type_argument_list`s.  For example, the following are legal:

```c#
var v = (nameof(X<>.Y<int>));
var v = (nameof(X<int>.Y<>));
```

Member lookup on an unbound type expression within a `nameof` will be performed the same way as for a `this`
expression within that type declaration (modulo performing accessibility checks at the callsite).  In other
words lookup off of `List<>` in `nameof(List<>...)` works the same as lookup off of `this` within the `class List<T>`
type itself.

No change is needed for the cases listed in the [Not supported](#not-supported) section. They already provide
the same errors for `nameof` expressions as they do for `typeof`.

No change is needed when the syntax `nameof` binds to a method named 'nameof' rather than being the contextual
keyword `nameof`. Passing any type expression to a method results in "CS0119: '...' is a type, which is not
valid in the given context." This already covers unbound generic type expressions.
