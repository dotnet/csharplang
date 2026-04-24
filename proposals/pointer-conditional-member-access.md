# Conditional member access and null-coalescing for pointers

* [x] Proposed
* [ ] Prototype: In Progress ([null-conditional access](https://github.com/AlFasGD/roslyn/tree/conditional-access-pointers))
* [ ] Implementation: In Progress ([null-coalescing](https://github.com/AlFasGD/roslyn/tree/pointer-null-coalescing))
* [ ] Specification: Below

## Summary
[summary]: #summary

Simplifies common coding patterns regarding null-checking pointer variables.

As part of this proposal, type requirements will be loosened accordingly to support pointer types being handled by those operators.

## Motivation
[motivation]: #motivation

The operators `?.`, `?[` and `??` are already supported for reference types and nullable structs, where `null` is a commonly handled case.

Pointers can also be assigned `null`, which should be treated equally, thus enabling the case of `null`-checking the variable before performing a `->`, a `??` or an indexing operation on them.

## Detailed design
[design]: #detailed-design

We permit usage of the `?` conditional member access operator before `->` to access a member of the value pointed by the pointer. That feature requires adding a new grammar rule, similar to `member_binding_expression`:

```antlr
pointer_member_binding_expression
  : '->' simple_name
  ;
```

The other cases (`?.`, `?[`, `??`, `??=`) are already considered valid by the grammar, and are only expanded for usage with pointer types in mind.

The specific supported expressions are the following:

The expression `a?->b`, where `a` is a pointer-typed expression, is equivalent to the following:
```csharp
a is null ? null : a->b
```

---

The expression `a?[b]`, where `a` is a pointer-typed expression, and `b` is a single expression representing an index, is equivalent to the following:
```csharp
a is null ? null : a[b]
```

It should be noted that indexing a pointer with `?[` follows the same rules as indexing it normally with `[`, meaning that only exactly one non-named argument is accepted. Additionally, indexing cannot occur for the `void*` pointer type. Generally, all rules applied to the normal indexing with `[` are applied to `?[` as well.

---

The expression `a?.b`, where `a` is a *non*-pointer-typed expression, and `b` is a pointer-typed expression, is equivalent to the following:
```csharp
a is null ? null : a.b
```

The result of the expression remains a pointer, and is permitted since pointer types accept `null` values.

---

The expression `a ?? b`, where `a` and `b` are pointer-typed expressions, is equivalent to the following:
```csharp
a is null ? b : a
```

Assuming `A`, `B` the pointer types of `a`, `b` respectively, `A` and `B` must either match, or any of the two being implicitly convertible to the other, meaning that any of `A` and `B` must be `void*`, with the other being any (function) pointer type.

Similarly, the expression `a ??= b`, where `a` and `b` are pointer-typed expressions, is equivalent to the following:
```csharp
a = (a is null ? b : a)
```

Similarly to the `??` case, `B` must be implicitly convertible to `A`, which only leaves the possibility of `A` being a `void*` and `B` being any (function) pointer type.

### Requirement relaxation

There also has to be performed a relaxation of the type requirements of `??`, where we update the spec where it currently states that, given `a ?? b`, where `A` is the type of `a`:

> 1. If A exists and is a non-nullable value type, a compile-time error occurs.

We relax this requirement to:

> 1. If A exists and is a non-nullable value type, **but not a pointer or function pointer**, a compile-time error occurs.

This explicitly states that the `??` and `??=` operators can be used on pointers and function pointers. Previously, that could be considered an undisclosed case, which was not supported in C#.

## Drawbacks
[drawbacks]: #drawbacks

For `?->`, it's one of the rare cases where a 3-symbol operator is introduced and permitted as valid syntax in the language.

As per the feature in general, it could be argued that a `null` in a pointer type could be handled differently than a `null` in a reference type, much like how `null` for nullable value types is, resulting in additional complexity for the language.

## Alternatives
[alternatives]: #alternatives

The programmer can write the equivalent versions of the proposed operators that are to be supported for pointers, as presented above. This proposal aims to shorten the currently only available way to write these expressions and statements.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Requires LDM review
- [ ] In the future, if nullable pointers (`T*?`) are supported as an extension to nullable reference types, will `!->` and `![` be supported?

## Design meetings

None.
