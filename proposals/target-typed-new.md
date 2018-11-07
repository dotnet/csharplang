
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

## Detailed design
[design]: #detailed-design

The *object_creation_expression* syntax would be modified to make the *type* optional when parentheses are present. This is required to address the ambiguity with *anonymous_object_creation_expression*.
```antlr
object_creation_expression
    : 'new' type? '(' argument_list? ')' object_or_collection_initializer?
    | 'new' type object_or_collection_initializer
    ;
```
The target-type is resolved through a conversion that only exists if and only if there exists a single best accessible constructor with regard to provided arguments. Target of the initialization is determined after the conversion succeeded.

The type of the expression would be inferred from the target-type which would be required to be one of the following:

- **Any struct type** (including tuple types)
- **Any reference type**
- **Any type parameter** with a constructor or a `struct` constraint

with the following exceptions:

- **Enum types:** not all enum types contain the constant zero, so it should be desirable to use the explicit enum member.
- **Delegate types:** if the type is inferrable, an anonymous function can already be used.
- **Interface types:** this is a niche feature and it should be preferable to explicitly mention the type.
- **Array types:** arrays need a special syntax to provide the length.
- **dynamic:** we don't allow `new dynamic()`, so we don't allow `new()` with `dynamic` as a target type.

All the other types that are not permitted in the *object_creation_expression* are excluded as well, for instance, pointer types.

Note that any restriction on permitted types would raise the success rate of the overload resolution. For example, the following would successfully compile considering the restriction on the default constructor for value types.
```cs
class C {}
void M(C c) {}
void M(int i) {}

M(new());
```
Otherwise it would fail with an ambiguous call error.

When the target type is a nullable value type, the target-typed `new` will be converted to the underlying type instead of the nullable type.

### Overload resolution

A target-typed `new` contributes nothing to the filtering of overload candidates. It behaves like `default`, even if it has named arguments (`new (arg1: 1)`) or initializers (`new () { property = 2 }`) that might have helped resolve ambiguities.
We only start looking at the arguments and initializers once overload resolution is done and the target-typed `new` is given a type via conversion.

### Miscellaneous

`throw new()` is disallowed.

Target-typed `new` is allowed with user-defined comparison and arithmetic operators.

## Drawbacks
[drawbacks]: #drawbacks

Since we're relying on overload resolution of the target-type members, there is a high chance that any change in one of the participating members (target-type's constructors or call-site method overloads, if any) would result in breaking the compilation. For instance,
```cs
class Foo { }
class Bar { }

void M(Foo foo) {}

M(new());
```
The invocation compiles until an overload like `void M(Bar bar) {}` is added alongside of the existing method.
This scenario already exists with `null` and `default`, but hasn't been a significant problem.

## Alternatives
[alternatives]: #alternatives

Most of complaints about types being too long to duplicate in field initialization is about *type arguments* not the type itself, we could infer only type arguments like `new Dictionary(...)` (or similar) instead of the whole type, in which case, it would be a lot less likely to break the compilation with adding a constructor or an overloaded member, because we're not relying on the target-type, rather, we infer type arguments locally from arguments or the collection initializer.

## Questions
[quesions]: #questions

- Should we forbid usages in expression trees? `new(...)` is allowed in expression trees and represented like an object creation.
- How the feature interacts with `dynamic` arguments? We disallow this
- It's not clear how IntelliSense should behave when there are multiple target-types, specially in the nested case `M(new(new()));`.


## Design meetings

- [LDM-2017-10-18](https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-10-18.md#100)
- [LDM-2018-05-21](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-05-21.md)
- [LDM-2018-06-25](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-06-25.md)
- [LDM-2018-08-22](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-08-22.md#target-typed-new)
- [LDM-2018-10-17](https://github.com/dotnet/csharplang/blob/master/meetings/2018/LDM-2018-10-17.md)
