# Conditional member access and null-coalescing for pointers

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Below

## Summary
[summary]: #summary

Extends the use of null-conditional operators to expressions of pointer types, and enables using a null-conditional version of the pointer member access operator (`->`).

As part of this proposal, type requirements will be loosened accordingly to support pointer types being handled by those operators.

## Motivation
[motivation]: #motivation

The operators `?.`, `?[` and `??` are already supported for reference types and nullable structs, where `null` is a commonly handled case.

Pointers can also be assigned `null`, which can be treated equally, thus enabling the case of `null`-checking the variable before performing a `->`, a `??` or an indexing operation on them.

## Detailed design
[design]: #detailed-design

We permit usage of `?` before `->` to access a member of the value pointed by the pointer in the context of conditional member access. That feature requires adding a new grammar rule, similar to `member_binding_expression`:

```antlr
pointer_member_binding_expression
  : '->' simple_name
  ;
```

This new `pointer_member_binding_expression` will then be added to the viable list of expressions in the `expression` rule:

```diff
 expression
   : anonymous_function_expression
   | anonymous_object_creation_expression
   | array_creation_expression
   | assignment_expression
   | await_expression
   ...
   | member_access_expression
   | member_binding_expression
   | omitted_array_size_expression
   | parenthesized_expression
+  | pointer_member_binding_expression
   | postfix_unary_expression
   ...
```

The other cases (`?[`, `??`, `??=`) are already considered valid by the grammar, and are only expanded for usage with pointer types in mind.

The specific supported expressions are the following:

The expression `a?->b`, where `a` is a pointer-typed expression, and `b` is a member of the dereference of `a`, is equivalent to the following:
```csharp
a != null ? a->b : null
```

---

The expression `a?[b]`, where `a` is a pointer-typed expression, and `b` is a single expression representing an index, and `a[b]` is a valid expression, is equivalent to the following:
```csharp
a != null ? a[b] : null
```

It should be noted that all rules applied to the normal indexing with `[` are applied to `?[` as well.

---

The expression `a ?? b`, where `a` and `b` are pointer-typed expressions, is equivalent to the following:
```csharp
a != null ? a : b
```

Assuming `A`, `B` the pointer types of `a`, `b` respectively, `A` and `B` must either:
- match, or
- any of the two being implicitly convertible to the other, which is only feasible if any of `A` and `B` is `void*`, and the other being any other pointer type.

Similarly, the expression `a ??= b`, where `a` and `b` are pointer-typed expressions, produces the same results as the following:
```csharp
a = (a != null ? a : b)
```

Similarly to the `??` case, `B` must be implicitly convertible to `A`, requiring that either:
- `A` and `B` are the same pointer type, or
- `A` is `void*` and `B` is any other pointer type.

### Requirement relaxation
[requirements]: #requirement-relaxation

The type requirements of `??` also need to be relaxed, by updating the spec where it currently states that, given `a ?? b`, where `A` is the type of `a`:

> - If `A` exists and is not a nullable value type or a reference type, a compile-time error occurs.

We relax this requirement to:

> - If `A` exists and is not a nullable value type, **or a pointer type, or a function pointer type,** or a reference type, a compile-time error occurs.

This explicitly states that the `??` and `??=` operators can be used on pointers and function pointers. Previously, that could be considered an undisclosed case, which was prohibited in C#.

## Drawbacks
[drawbacks]: #drawbacks

For `?->`, it's one of the rare cases where a 3-symbol operator is introduced and permitted as valid syntax in the language.

## Alternatives
[alternatives]: #alternatives

The user can write the equivalent versions of the proposed operators that are to be supported for pointers, as presented above. This proposal aims to shorten the currently only available way to write these expressions and statements.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Requires LDM review

## Design meetings

None.
