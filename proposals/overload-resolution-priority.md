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
[§12.8.9.2](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12892-method-invocations) of the C# specification as
follows (change in **bold**):

> For a method invocation, the *primary_expression* of the *invocation_expression* shall be a method group. The method group identifies the one method to invoke or the set of overloaded methods from which to choose a specific method to invoke. In the latter case, determination of the specific method to invoke is based on the context provided by the types of the arguments in the *argument_list*.
>
> The binding-time processing of a method invocation of the form `M(A)`, where `M` is a method group (possibly including a *type_argument_list*), and `A` is an optional *argument_list*, consists of the following steps:
> 
> - The set of candidate methods for the method invocation is constructed. For each method `F` associated with the method group `M`:
>   - If `F` is non-generic, `F` is a candidate when:
>     - `M` has no type argument list, and
>     - `F` is applicable with respect to `A` ([§12.6.4.2](expressions.md#12642-applicable-function-member)).
>   - If `F` is generic and `M` has no type argument list, `F` is a candidate when:
>     - Type inference ([§12.6.3](expressions.md#1263-type-inference)) succeeds, inferring a list of type arguments for the call, and
>     - Once the inferred type arguments are substituted for the corresponding method type parameters, all constructed types in the parameter list of `F` satisfy their constraints ([§8.4.5](types.md#845-satisfying-constraints)), and the parameter list of `F` is applicable with respect to `A` ([§12.6.4.2](expressions.md#12642-applicable-function-member))
>   - If `F` is generic and `M` includes a type argument list, `F` is a candidate when:
>     - `F` has the same number of method type parameters as were supplied in the type argument list, and
>     - Once the type arguments are substituted for the corresponding method type parameters, all constructed types in the parameter list of `F` satisfy their constraints ([§8.4.5](types.md#845-satisfying-constraints)), and the parameter list of `F` is applicable with respect to `A` ([§12.6.4.2](expressions.md#12642-applicable-function-member)).
> - The set of candidate methods is reduced to contain only methods from the most derived types: For each method `C.F` in the set, where `C` is the type in which the method `F` is declared, all methods declared in a base type of `C` are removed from the set. Furthermore, if `C` is a class type other than `object`, all methods declared in an interface type are removed from the set.  
>   > *Note*: This latter rule only has an effect when the method group was the result of a member lookup on a type parameter having an effective base class other than `object` and a non-empty effective interface set. *end note*
> - If the resulting set of candidate methods is empty, then further processing along the following steps are abandoned, and instead an attempt is made to process the invocation as an extension method invocation ([§12.8.9.3](expressions.md#12893-extension-method-invocations)). If this fails, then no applicable methods exist, and a binding-time error occurs.
> - **The resulting set of candidate methods is grouped by ***overload_resolution_priority***. All candidates not in the highest (by standard integer comparison) ***overload_resolution_priority*** group are removed from the set.**
> - The best method of the set of candidate methods is identified using the overload resolution rules of [§12.6.4](expressions.md#1264-overload-resolution). If a single best method cannot be identified, the method invocation is ambiguous, and a binding-time error occurs. When performing overload resolution, the parameters of a generic method are considered after substituting the type arguments (supplied or inferred) for the corresponding method type parameters.

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

### `System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute`

We introduce the following attribute to the BCL:

```cs
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class OverloadResolutionPriorityAttribute(int priority)
{
    public int Priority => priority;
}
```

**Open question**: Should the attribute be inherited? If not, what is the priority of the overriding member?  
**Open question**: If the attribute is specified on a virtual member, should an override of that member be required to repeat the attribute?  
**Open question**: Should the attribute support constructors and indexer?

All methods in C# have a default ***overload_resolution_priority*** of 0, unless they are attributed with `OverloadResolutionPriorityAttribute`. If they are
attributed with that attribute, then their ***overload_resolution_priority*** is the integer value provided to the first argument of the attribute.

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


