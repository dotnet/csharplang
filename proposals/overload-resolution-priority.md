# Overload Resolution Priority

## Summary
[summary]: #summary

We introduce a new attribute, `System.Runtime.CompilerServices.OverloadResolutionPriority`, that can be used by API authors to adjust the relative priority of
overloads within a single type as a means of steering API consumers to use specific APIs, even if those APIs would normally be considered ambiguous or otherwise
not be chosen by C#'s overload resolution rules.

## Motivation
[motivation]: #motivation

API authors often run into an issue of what to do with a member after it has been obsoleted. For backwards compatibility purposes, many will keep the existing member around
with `ObsoleteAttribute` set to error in perpetuity, in order to avoid breaking consumers who upgrade binaries at runtime. This particularly hits plugin systems, where the
author of a plugin does not control the environment in which the plugin runs. The creator of the environment may want to keep an older method present, but block access to it
for any newly developed code. However, `ObsoleteAttribute` by itself is not enough. The type or member is still visible in overload resolution, and may cause unwanted overload
resolution failures when there is a perfectly good alternative, but that alternative is either ambiguous with the obsoleted member, or the presence of the obsoleted member causes
overload resolution to end early without ever considering the good member. For this purpose, we want to have a way for API authors to guide overload resolution on resolving the
ambiguity, so that they can evolve their API surface areas and steer users towards performant APIs without having to compromise the user experience.

## Detailed Design
[detailed-design]: #detailed-design

### Overload resolution priority

We define a new concept, ***overload_resolution_priority***, which is used during the process of resolving a method group. ***overload_resolution_priority*** is a 32-bit integer
value. All methods have an ***overload_resolution_priority*** of 0 by default, and this can be changed by applying
[`OverloadResolutionPriorityAttribute`](#systemruntimecompilerservicesoverloadresolutionpriorityattribute) to a method. We update section 
[§12.6.4.1](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12641-general) of the C# specification as
follows (change in **bold**):

> Once the candidate function members and the argument list have been identified, the selection of the best function member is the same in all cases:
> 
> - First, the set of candidate function members is reduced to those function members that are applicable with respect to the given argument list ([§12.6.4.2](expressions.md#12642-applicable-function-member)). If this reduced set is empty, a compile-time error occurs.
> - **Then, the reduced set of candidate members is grouped by declaring type. Within each group:**
>     - **Candidate function members are ordered by ***overload_resolution_priority***.
>     - **All members that have a lower ***overload_resolution_priority*** than the highest found within its declaring type group are removed.**
> - **The reduced groups are then recombined into the final set of applicable candidate function members.**
> - Then, the best function member from the set of applicable candidate function members is located. If the set contains only one function member, then that function member is the best function member. Otherwise, the best function member is the one function member that is better than all other function members with respect to the given argument list, provided that each function member is compared to all other function members using the rules in [§12.6.4.3](expressions.md#12643-better-function-member). If there is not exactly one function member that is better than all other function members, then the function member invocation is ambiguous and a binding-time error occurs.

As an example, this feature would cause the following code snippet to print "Span", rather than "Array":

```cs
using System.Runtime.CompilerServices;

var d = new C1();
int[] arr = [1, 2, 3];
d.M(arr); // Prints "Span"

class C1
{
    [OverloadResolutionPriority(1)]
    public void M(ReadOnlySpan<int> s) => Console.WriteLine("Span");
    // Default overload resolution priority
    public void M(int[] a) => Console.WriteLine("Array");
}
```

The effect of this change is that, like pruning for most-derived types, we add a final pruning for overload resolution priority. Because this pruning occurs at the very end of the overload resolution
process, it does mean that a base type cannot make its members higher-priority than any derived type. This is intentional, and prevents an arms-race from occuring where a base type may try to always
be better than a derived type. For example:

```cs
using System.Runtime.CompilerServices;

var d = new Derived();
d.M([1, 2, 3]); // Prints "Derived", because members from Base are not considered due to finding an applicable member in Derived

class Base
{
    [OverloadResolutionPriority(1)]
    public void M(ReadOnlySpan<int> s) => Console.WriteLine("Base");
}

class Derived : Base
{
    public void M(int[] a) => Console.WriteLine("Derived");
}
```

Negative numbers are allowed to be used, and can be used to mark a specific overload as worse than all other default overloads.

**Open Question**: As currently worded, extension methods are ordered by priority _only within their own type_. For example:

```cs
new C2().M([1, 2, 3]); // Will print Ext2 ReadOnlySpan

static class Ext1
{
    [OverloadResolutionPriority(1)]
    public static void M(this C2 c, Span<int> s) => Console.WriteLine("Ext1 Span");
    [OverloadResolutionPriority(0)]
    public static void M(this C2 c, ReadOnlySpan<int> s) => Console.WriteLine("Ext1 ReadOnlySpan");
}

static class Ext2
{
    [OverloadResolutionPriority(0)]
    public static void M(this C2 c, ReadOnlySpan<int> s) => Console.WriteLine("Ext2 ReadOnlySpan");
}

class C2 {}
```

When doing overload resolution for extension members, should we not sort by declaring type, and instead consider all extensions within the same scope?

### `System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute`

We introduce the following attribute to the BCL:

```cs
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OverloadResolutionPriorityAttribute(int priority)
{
    public int Priority => priority;
}
```

**Open question**: Should the attribute be inherited? If not, what is the priority of the overriding member?  
**Open question**: If the attribute is specified on a virtual member, should an override of that member be required to repeat the attribute?  

All methods in C# have a default ***overload_resolution_priority*** of 0, unless they are attributed with `OverloadResolutionPriorityAttribute`. If they are
attributed with that attribute, then their ***overload_resolution_priority*** is the integer value provided to the first argument of the attribute.

It is an error to apply `OverloadResolutionPriorityAttribute` to a non-indexer property, or to property, indexer, or event accessors. Attributes encountered on
these locations in metadata are ignored by C#.

## Alternatives
[alternatives]: #alternatives

A [previous](https://github.com/dotnet/csharplang/pull/7707) proposal tried to specify a `BinaryCompatOnlyAttribute` approach, which was very heavy-handed
in removing things from visibility. However, that has lots of hard implementation problems that either mean the proposal is too strong to be useful (preventing
testing old APIs, for example) or so weak that it missed some of the original goals (such as being able have an API that would otherwise be considered ambiguous
call a new API). That version is replicated below.

### BinaryCompatOnlyAttribute

#### Detailed design
[design]: #bco-detailed-design

##### `System.BinaryCompatOnlyAttribute`

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

##### Accessibility Domains

We update [§7.5.3 Accessibility domains](https://github.com/dotnet/csharpstandard/blob/720d921c5688190ea544682cdbdf8874fa716f2b/standard/basic-concepts.md#753-accessibility-domains)
as **follows**:

 > The ***accessibility domain*** of a member consists of the (possibly disjoint) sections of program text in which access to the member is permitted. For purposes of defining the accessibility domain of a member, a member is said to be ***top-level*** if it is not declared within a type, and a member is said to be ***nested*** if it is declared within another type. Furthermore, the ***program text*** of a program is defined as all text contained in all compilation units of the program, and the program text of a type is defined as all text contained in the *type_declaration*s of that type (including, possibly, types that are nested within the type).
>
> The accessibility domain of a predefined type (such as `object`, `int`, or `double`) is unlimited.
>
> The accessibility domain of a top-level unbound type `T` ([§8.4.4](types.md#844-bound-and-unbound-types)) that is declared in a program `P` is defined as follows:
> 
> - **If `T` is marked with `BinaryCompatOnlyAttribute`, the accessibility domain of `T` is completely inaccessible to the program text of `P` and any program that references `P`.**
> - If the declared accessibility of `T` is public, the accessibility domain of `T` is the program text of `P` and any program that references `P`.
> - If the declared accessibility of `T` is internal, the accessibility domain of `T` is the program text of `P`.
> 
> *Note*: From these definitions, it follows that the accessibility domain of a top-level unbound type is always at least the program text of the program in which that type is declared. *end note*
> 
> The accessibility domain for a constructed type `T<A₁, ..., Aₑ>` is the intersection of the accessibility domain of the unbound generic type `T` and the accessibility domains of the type arguments `A₁, ..., Aₑ`.
> 
> The accessibility domain of a nested member `M` declared in a type `T` within a program `P`, is defined as follows (noting that `M` itself might possibly be a type):
> 
> - **If `M` is marked with `BinaryCompatOnlyAttribute`, the accessibility domain of `M` is completely inaccessible to the program text of `P` and any program that references `P`.**
> - If the declared accessibility of `M` is `public`, the accessibility domain of `M` is the accessibility domain of `T`.
> - If the declared accessibility of `M` is `protected internal`, let `D` be the union of the program text of `P` and the program text of any type derived from `T`, which is declared outside `P`. The accessibility domain of `M` is the intersection of the accessibility domain of `T` with `D`.
> - If the declared accessibility of `M` is `private protected`, let `D` be the intersection of the program text of `P` and the program text of `T` and any type derived from `T`. The accessibility domain of `M` is the intersection of the accessibility domain of `T` with `D`.
> - If the declared accessibility of `M` is `protected`, let `D` be the union of the program text of `T`and the program text of any type derived from `T`. The accessibility domain of `M` is the intersection of the accessibility domain of `T` with `D`.
> - If the declared accessibility of `M` is `internal`, the accessibility domain of `M` is the intersection of the accessibility domain of `T` with the program text of `P`.
> - If the declared accessibility of `M` is `private`, the accessibility domain of `M` is the program text of `T`.

The goal of these additions is to make it so that members marked with `BinaryCompatOnlyAttribute` are completely inaccessible to any location, they will
not participate in member lookup, and cannot affect the rest of the program. Consequentely, this means they cannot implement interface members, they cannot
call each other, and they cannot be overridden (virtual methods), hidden, or implemented (interface members). Whether this is too strict is the subject of
several open questions below.

#### Unresolved questions
[unresolved]: #unresolved-questions

##### Virtual methods and overriding

What do we do when a virtual method is marked as `BinaryCompatOnly`? Overrides in a derived class may not even be in the current assembly, and it could
be that the user is looking to introduce a new version of a method that, for example, only differs by return type, something that C# does not normally
allow overloading on. What happens to any overrides of that previous method on recompile? Are they allowed to override the `BinaryCompatOnly` member if
they're also marked as `BinaryCompatOnly`?

##### Use within the same DLL

This proposal states that `BinaryCompatOnly` members are not visible anywhere, not even in the assembly currently being compiled. Is that too strict, or
do `BinaryCompatAttribute` members need to possibly chain to one another?

##### Implicitly implementing interface members

Should `BinaryCompatOnly` members be able to implement interface members? Or should they be prevented from doing so. This would require that, when a user
wants to turn an implicit interface implementation into `BinaryCompatOnly`, they would additionally have to provide an explicit interface implementation,
likely cloning the same body as the `BinaryCompatOnly` member as the explicit interface implementation would not be able to see the original member anymore.

##### Implementing interface members marked `BinaryCompatOnly`

What do we do when an interface member has been marked as `BinaryCompatOnly`? The type still needs to provide an implementation for that member; it may be
that we must simply say that interface members cannot be marked as `BinaryCompatOnly`.


