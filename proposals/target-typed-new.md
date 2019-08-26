
# Target-typed `new` expressions

* [x] Proposed
* [x] [Prototype](https://github.com/alrz/roslyn/tree/features/target-typed-new)
* [ ] Implementation
* [ ] Specification

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
## Detailed design
[design]: #detailed-design

The *object_creation_expression* syntax would be modified to make the *type* optional when parentheses are present. This is required to address the ambiguity with *anonymous_object_creation_expression*.
```antlr
object_creation_expression
    : 'new' type? '(' argument_list? ')' object_or_collection_initializer?
    | 'new' type object_or_collection_initializer
    ;
```
A target-typed `new` is convertible to any type. As a result, it does not contribute to overload resolution. This is mainly to avoid unpredictable breaking changes.

The argument list and the initializer expressions will be bound after the type is determined.

The type of the expression would be inferred from the target-type which would be required to be one of the following:

- **Any struct type**
- **Any reference type**
- **Any type parameter** with a constructor or a `struct` constraint

with the following exceptions:

- **Enum types:** not all enum types contain the constant zero, so it should be desirable to use the explicit enum member.
- **Interface types:** this is a niche feature and it should be preferable to explicitly mention the type.
- **Array types:** arrays need a special syntax to provide the length.
- **Struct default constructor**: this rules out all primitive types and most value types. If you wanted to use the default value of such types you could write `default` instead.

All the other types that are not permitted in the *object_creation_expression* are excluded as well, for instance, pointer types.

> **Open Issue:** should we allow delegates and tuples as the target-type?

The above rules include delegates (a reference type) and tuples (a struct type). Although both types are constructible, if the type is inferable, an anonymous function or a tuple literal can already be used.
```cs
(int a, int b) t = new(1, 2); // "new" is redundant
Action a = new(() => {}); // "new" is redundant

(int a, int b) t = new(); // ruled out by "use of struct default constructor"
Action a = new(); // no constructor found

var x = new() == (1, 2); // ruled out by "use of struct default constructor"
var x = new(1, 2) == (1, 2) // "new" is redundant
```


> **Open Issue:** should we allow `throw new()` with `Exception` as the target-type?

We have `throw null` today, but not `throw default` (though it would have the same effect). On the other hand, `throw new()` could be actually useful as a shorthand for `throw new Exception(...)`. Note that it is already allowed by the current specification. `Exception` is a reference type, and the specification for the throw statement says that the expression is converted to `Exception`.

> **Open Issue:** should we allow usages of a target-typed `new` with user-defined comparison and arithmetic operators?

For comparison, `default` only supports equality (user-defined and built-in) operators. Would it make sense to support other operators for `new()` as well?

## Drawbacks
[drawbacks]: #drawbacks

None.

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
