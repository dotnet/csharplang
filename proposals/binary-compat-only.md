# BinaryCompatOnlyAttribute

## Summary
[summary]: #summary

We introduce a new attribute, `System.BinaryCompatOnlyAttribute`, that causes the type or member to which it is applied to be entirely inaccessible from source code.
It will still be emitted as declared, with the accessibility declared, but will be treated as if it does not exist by all other compile-time C# mechanisms.

## Motivation
[motivation]: #motivation

API authors often run into an issue of what to do with a member after it has been obsoleted. For backwards compatibility purposes, many will keep the existing member around
with `ObsoleteAttribute` set to error in perpetuity, in order to avoid breaking consumers who upgrade binaries at runtime. This particularly hits plugin systems, where the
author of a plugin does not control the environment in which the plugin runs. The creator of the environment may want to keep an older method present, but block access to it
for any newly developed code. However, `ObsoleteAttribute` by itself is not enough. The type or member is still visible in overload resolution, and may cause unwanted overload
resolution failures when there is a perfectly good alternative, but that alternative is either ambiguous with the obsoleted member, or the presence of the obsoleted member causes
overload resolution to end early without ever considering the good member. For this purpose, we want to have a way to mark such members as technically being present in the DLL,
but not visible to any code, so that they will have no impact on member lookup, overload resolution, or any other similar elements.

## Detailed design
[design]: #detailed-design

### `System.BinaryCompatOnlyAttribute`

We introduce a new reserved attribute:

```cs
namespace System;

// Excludes Assembly, GenericParameter, Module, Parameter, ReturnValue
[AttributeUsage(AttributeTargets.Class
                | AttributeTargets.Constructor
                | AttributeTargets.Delegate
                | AttributeTargets.Enum
                | AttributeTargets.Event
                | AttributeTargets.Field
                | AttributeTargets.Interface
                | AttributeTargets.Method
                | AttributeTargets.Property
                | AttributeTargets.Struct,
                AllowMultiple = false,
                Inherited = false)]
public class BinaryCompatOnlyAttribute : Attribute {}
```

When applied to a type member, that member is treated as inaccessible in every location by the compiler, meaning that it does not contribute to member
lookup, overload resolution, or any other similar process.

### Accessibility Domains

We update [§7.5.3 Accessibility domains](https://github.com/dotnet/csharpstandard/blob/720d921c5688190ea544682cdbdf8874fa716f2b/standard/basic-concepts.md#753-accessibility-domains)
as **follows**:

 > The ***accessibility domain*** of a member consists of the (possibly disjoint) sections of program text in which access to the member is permitted. For purposes of defining the accessibility domain of a member, a member is said to be ***top-level*** if it is not declared within a type, and a member is said to be ***nested*** if it is declared within another type. Furthermore, the ***program text*** of a program is defined as all text contained in all compilation units of the program, and the program text of a type is defined as all text contained in the *type_declaration*s of that type (including, possibly, types that are nested within the type).
>
> The accessibility domain of a predefined type (such as `object`, `int`, or `double`) is unlimited.
>
> The accessibility domain of a top-level unbound type `T` ([§8.4.4](types.md#844-bound-and-unbound-types)) that is declared in a program `P` is defined as follows:
> 
> - **If `T` is marked with `BinaryCompatOnlyAttribute`, the accessibility domain of `T` is completely inaccessible to the program text of `P` and any program that references `P`.**
> - If the declared accessibility of `T` is public, the accessibility domain of `T` is the program text of `P` and any program that references `P`.
> - If the declared accessibility of `T` is internal, the accessibility domain of `T` is the program text of `P`.
> 
> *Note*: From these definitions, it follows that the accessibility domain of a top-level unbound type is always at least the program text of the program in which that type is declared. *end note*
> 
> The accessibility domain for a constructed type `T<A₁, ..., Aₑ>` is the intersection of the accessibility domain of the unbound generic type `T` and the accessibility domains of the type arguments `A₁, ..., Aₑ`.
> 
> The accessibility domain of a nested member `M` declared in a type `T` within a program `P`, is defined as follows (noting that `M` itself might possibly be a type):
> 
> - **If `M` is marked with `BinaryCompatOnlyAttribute`, the accessibility domain of `M` is completely inaccessible to the program text of `P` and any program that references `P`.**
> - If the declared accessibility of `M` is `public`, the accessibility domain of `M` is the accessibility domain of `T`.
> - If the declared accessibility of `M` is `protected internal`, let `D` be the union of the program text of `P` and the program text of any type derived from `T`, which is declared outside `P`. The accessibility domain of `M` is the intersection of the accessibility domain of `T` with `D`.
> - If the declared accessibility of `M` is `private protected`, let `D` be the intersection of the program text of `P` and the program text of `T` and any type derived from `T`. The accessibility domain of `M` is the intersection of the accessibility domain of `T` with `D`.
> - If the declared accessibility of `M` is `protected`, let `D` be the union of the program text of `T`and the program text of any type derived from `T`. The accessibility domain of `M` is the intersection of the accessibility domain of `T` with `D`.
> - If the declared accessibility of `M` is `internal`, the accessibility domain of `M` is the intersection of the accessibility domain of `T` with the program text of `P`.
> - If the declared accessibility of `M` is `private`, the accessibility domain of `M` is the program text of `T`.

The goal of these additions is to make it so that members marked with `BinaryCompatOnlyAttribute` are completely inaccessible to any location, they will
not participate in member lookup, and cannot affect the rest of the program. Consequentely, this means they cannot implement interface members, they cannot
call each other, and they cannot be overridden (virtual methods), hidden, or implemented (interface members). Whether this is too strict is the subject of
several open questions below.

## Drawbacks
[drawbacks]: #drawbacks

More complex attribute mechanisms make the language more complex.

## Alternatives
[alternatives]: #alternatives

No other alternatives have presented themselves

## Unresolved questions
[unresolved]: #unresolved-questions

### Virtual methods and overriding

What do we do when a virtual method is marked as `BinaryCompatOnly`? Overrides in a derived class may not even be in the current assembly, and it could
be that the user is looking to introduce a new version of a method that, for example, only differs by return type, something that C# does not normally
allow overloading on. What happens to any overrides of that previous method on recompile? Are they allowed to override the `BinaryCompatOnly` member if
they're also marked as `BinaryCompatOnly`?

### Use within the same DLL

This proposal states that `BinaryCompatOnly` members are not visible anywhere, not even in the assembly currently being compiled. Is that too strict, or
do `BinaryCompatAttribute` members need to possibly chain to one another?

### Implicitly implementing interface members

Should `BinaryCompatOnly` members be able to implement interface members? Or should they be prevented from doing so. This would require that, when a user
wants to turn an implicit interface implementation into `BinaryCompatOnly`, they would additionally have to provide an explicit interface implementation,
likely cloning the same body as the `BinaryCompatOnly` member as the explicit interface implementation would not be able to see the original member anymore.

### Implementing interface members marked `BinaryCompatOnly`

What do we do when an interface member has been marked as `BinaryCompatOnly`? The type still needs to provide an implementation for that member; it may be
that we must simply say that interface members cannot be marked as `BinaryCompatOnly`.

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.


