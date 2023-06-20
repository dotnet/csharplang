# Generic Attributes

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
[summary]: #summary

When generics were introduced in C# 2.0, attribute classes were not allowed to participate. We can make the language more composable by removing (rather, loosening) this restriction. The .NET Core runtime has added support for generic attributes. Now, all that's missing is support for generic attributes in the compiler.

## Motivation
[motivation]: #motivation

Currently attribute authors can take a `System.Type` as a parameter and have users pass a `typeof` expression to provide the attribute with types that it needs. However, outside of analyzers, there's no way for an attribute author to constrain what types are allowed to be passed to an attribute via `typeof`. If attributes could be generic, then attribute authors could use the existing system of type parameter constraints to express the requirements for the types they take as input.

## Detailed design
[design]: #detailed-design

The following section is amended: [§14.2.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/classes.md#14242-base-classes)
> The direct base class of a class type must not be any of the following types: System.Array, System.Delegate, System.MulticastDelegate, System.Enum, or System.ValueType. ~~Furthermore, a generic class declaration cannot use System.Attribute as a direct or indirect base class.~~

One important note is that the following section of the spec is *unaffected* when referencing the point of usage of an attribute, i.e. within an attribute list: Type parameters - [§8.5](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/types.md#85-type-parameters).

> A type parameter cannot be used anywhere within an attribute.

This means that when a generic attribute is used, its construction needs to be fully "closed", i.e. not containing any type parameters, which means the following is still disallowed:

```cs
using System;
using System.Collections.Generic;

public class Attr<T1> : Attribute { }

public class Program<T2>
{
    [Attr<T2>] // error
    [Attr<List<T2>>] // error
    void M() { }
}
```

When a generic attribute is used in an attribute list, its type arguments have the same restrictions that `typeof` has on its argument. For example, `[Attr<dynamic>]` is an error. This is because "attribute-dependent" types like `dynamic`, `List<string?>`, `nint`, and so on can't be fully represented in the final IL for an attribute type argument, because there isn't a symbol to "attach" the `DynamicAttribute` or other well-known attribute to.

## Drawbacks
[drawbacks]: #drawbacks

Removing the restriction, reasoning out the implications, and adding the appropriate tests is work.

## Alternatives
[alternatives]: #alternatives

Attribute authors who want users to be able to discover the requirements for the types they provide to attributes need to write analyzers and guide their users to use those analyzers in their builds.

## Unresolved questions
[unresolved]: #unresolved-questions

- [x] What does `AllowMultiple = false` mean on a generic attribute? If we have `[Attr<string>]` and `[Attr<object>]` both used on a symbol, does that mean "multiple" of the attribute are in use?
    - For now we are inclined to take the more restrictive route here and consider the attribute class's original definition when deciding whether multiple of it have been applied. In other words, `[Attr<string>]` and `[Attr<object>]` applied together is incompatible with `AllowMultiple = false`.

## Design meetings

- https://github.com/dotnet/csharplang/blob/main/meetings/2017/LDM-2017-02-21.md#generic-attributes
    - At the time there was a concern that we would have to gate the feature on whether the target runtime supports it. (However, we now only support C# 10 on .NET 6. It would be nice to for the implementation to be aware of what minimum target framework supports the feature, but seems less essential today.)
