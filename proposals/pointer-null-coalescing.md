# Extend null-coalescing (??) and null coalescing assignment (??=) operators to pointers

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: In Progress

## Summary
[summary]: #summary

This proposal extends support of a commonly used feature in C# (?? and ??=) to unsafe code and pointer types specifcally. 

## Motivation
[motivation]: #motivation


There is no reason for pointer types to be excluded from this feature as they semantically fit the feature's use case. Supporting this extends the feature's scope to what one would expect it to logically support. This concept is already supported in ternary expressions EG:

 `T *foo = bar != null ? bar : baz;`

So the same syntactic options that are offered to non-pointer types should be extended to pointer types.

## Detailed design
[design]: #detailed-design

For this addition to the language, no grammar changes are required. We are merely adding supported types to an existing operator. The current conversion rules will then determine the resulting type of the ?? expression.
Other rules will remain the same with the exception of the relaxed type rules which will need to be modified.

The  C# spec will need to be updated to reflect this addition with the change of the line.
> A null coalescing expression of the form `a` ?? `b` requires `a` to be of a nullable type or reference type.

to

> A null coalescing expression of the form `a` ?? `b` requires `a` to be of a nullable type, reference type or pointer type.

as well as 
> If `A` exists and is not a nullable type or a reference type, a compile-time error occurs.

to

> If `A` exists and is not a nullable type, reference type or pointer type, a compile-time error occurs.



## Drawbacks
[drawbacks]: #drawbacks

Unsafe code can be considered confusing and a vector for bugs that would not happen otherwise. Increasing the available syntactic options for pointer types could possibly lead to bugs where the developer was unaware of the semantic meaning of what was written. 

## Alternatives
[alternatives]: #alternatives

No other designs have been considered as this is not a new feature, but it is rather an extension of an already implemented feature.

## Unresolved questions
[unresolved]: #unresolved-questions

A possible question is if support should be extended to implicit dereferencing in the same vein as implicit conversions for `Nullable<T>` are supported.

EG:

    int* foo = null;
    int bar = foo ?? 3;

## Design meetings

https://github.com/dotnet/csharplang/issues/418



