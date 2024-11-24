# Overload Resolution Priority

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

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

The Base Class Libraries (BCL) team has several examples of where this can prove useful. Some (hypothetical) examples are:
* Creating an overload of `Debug.Assert` that uses `CallerArgumentExpression` to get the expression being asserted, so that it can be included in the message, and make it preferred
  over the existing overload.
* Making `string.IndexOf(string, StringComparison = Ordinal)` preferred over `string.IndexOf(string)`. This would have to be discussed as a potential breaking change, but there
  is some thought that it is the better default, and more likely to be what the user intended.
* A combination of this proposal and [`CallerAssemblyAttribute`](https://github.com/dotnet/csharplang/issues/4984) would allow methods that have an implicit caller identity to
  avoid expensive stack walks. `Assembly.Load(AssemblyName)` does this today, and it could be much more efficient.
* `Microsoft.Extensions.Primitives.StringValues` exposes an implicit conversion to both `string` and `string[]`. This means that it is ambiguous when passed to a method with both
  `params string[]` and `params ReadOnlySpan<string>` overloads. This attribute could be used to prioritize one of the overloads to prevent the ambiguity.

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
> - First, the set of candidate function members is reduced to those function members that are applicable with respect to the given argument list ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12642-applicable-function-member)). If this reduced set is empty, a compile-time error occurs.
> - **Then, the reduced set of candidate members is grouped by declaring type. Within each group:**
>     - **Candidate function members are ordered by ***overload_resolution_priority***. If the member is an override, the ***overload_resolution_priority*** comes from the least-derived declaration of that member.**
>     - **All members that have a lower ***overload_resolution_priority*** than the highest found within its declaring type group are removed.**
> - **The reduced groups are then recombined into the final set of applicable candidate function members.**
> - Then, the best function member from the set of applicable candidate function members is located. If the set contains only one function member, then that function member is the best function member. Otherwise, the best function member is the one function member that is better than all other function members with respect to the given argument list, provided that each function member is compared to all other function members using the rules in [§12.6.4.3](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/expressions.md#12643-better-function-member). If there is not exactly one function member that is better than all other function members, then the function member invocation is ambiguous and a binding-time error occurs.

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

The **overload_resolution_priority** of a member comes from the least-derived declaration of that member. **overload_resolution_priority** is not
inherited or inferred from any interface members a type member may implement, and given a member `Mx` that implements an interface member `Mi`, no
warning is issued if `Mx` and `Mi` have different **overload_resolution_priorities**.
> NB: The intent of this rule is to replicate the behavior of the `params` modifier.

### `System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute`

We introduce the following attribute to the BCL:

```cs
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OverloadResolutionPriorityAttribute(int priority) : Attribute
{
    public int Priority => priority;
}
```

All methods in C# have a default ***overload_resolution_priority*** of 0, unless they are attributed with `OverloadResolutionPriorityAttribute`. If they are
attributed with that attribute, then their ***overload_resolution_priority*** is the integer value provided to the first argument of the attribute.

It is an error to apply `OverloadResolutionPriorityAttribute` to the following locations:

* Non-indexer properties
* Property, indexer, or event accessors
* Conversion operators
* Lambdas
* Local functions
* Destructors
* Static constructors

Attributes encountered on these locations in metadata are ignored by C#.

It is an error to apply `OverloadResolutionPriorityAttribute` in a location it would be ignored, such as on an override of a base method, as the priority is read
from the least-derived declaration of a member.
> NB: This intentionally differs from the behavior of the `params` modifier, which allows respecifying or adding when ignored.

### Callability of members

An important caveat for `OverloadResolutionPriorityAttribute` is that it can make certain members effectively uncallable from source. For example:

```cs
using System.Runtime.CompilerServices;

int i = 1;
var c = new C3();
c.M1(i); // Will call C3.M1(long), even though there's an identity conversion for M1(int)
c.M2(i); // Will call C3.M2(int, string), even though C3.M1(int) has less default parameters

class C3
{
    public void M1(int i) {}
    [OverloadResolutionPriority(1)]
    public void M1(long l) {}

    [Conditional("DEBUG")]
    public void M2(int i) {}
    [OverloadResolutionPriority(1), Conditional("DEBUG")]
    public void M2(int i, [CallerArgumentExpression(nameof(i))] string s = "") {}

    public void M3(string s) {}
    [OverloadResolutionPriority(1)]
    public void M3(object o) {}
}
```

For these examples, the default priority overloads effectively become vestigal, and only callable through a few steps that take some extra effort:
* Converting the method to a delegate, and then using that delegate.
    * For some reference type variance scenarios, such as `M3(object)` that is prioritized over `M3(string)`, this strategy will fail.
    * Conditional methods, such as `M2`, would also not be callable with this strategy, as conditional methods cannot be converted to delegates.
* Using the `UnsafeAccessor` runtime feature to call it via matching signature.
* Manually using reflection to obtain a reference to the method and then invoking it.
* Code that is not recompiled will continue to call old methods.
* Handwritten IL can specify whatever it chooses.

## Open Questions

### Extension method grouping (answered)

As currently worded, extension methods are ordered by priority _only within their own type_. For example:

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

#### Answer

We will always group. The above example will print `Ext2 ReadOnlySpan`

### Attribute inheritance on overrides (answered)

Should the attribute be inherited? If not, what is the priority of the overriding member?  
If the attribute is specified on a virtual member, should an override of that member be required to repeat the attribute?  

#### Answer

The attribute will not be marked as inherited. We will look at the least-derived declaration of a member to determine its overload resolution priority.

### Application error or warning on override (answered)

```cs
class Base
{
    [OverloadResolutionPriority(1)] public virtual void M() {}
}
class Derived
{
    [OverloadResolutionPriority(2)] public override void M() {} // Warn or error for the useless and ignored attribute?
}
```

Which should we do on the application of a `OverloadResolutionPriorityAttribute` in a context where it is ignored, such as an override:

1. Do nothing, let it silently be ignored.
2. Issue a warning that the attribute will be ignored.
3. Issue an error that the attribute is not allowed.

3 is the most cautious approach, if we think there may be a space in the future where we might want to allow an override to specify this attribute.

#### Answer

We will go with 3, and block application on locations it would be ignored.

### Implicit interface implementation (answered)

What should the behavior of an implicit interface implementation be? Should it be required to specify `OverloadResolutionPriority`? What should the behavior of the compiler be when it encounters
an implicit implementation without a priority? This will nearly certainly happen, as an interface library may be updated, but not an implementation. Prior art here with `params` is to not specify,
and not carry over the value:

```cs
using System;

var c = new C();
c.M(1, 2, 3); // error CS1501: No overload for method 'M' takes 3 arguments
((I)c).M(1, 2, 3);

interface I
{
    void M(params int[] ints);
}

class C : I
{
    public void M(int[] ints) { Console.WriteLine("params"); }
}
```

Our options are:

1. Follow `params`. `OverloadResolutionPriorityAttribute` will not be implicitly carried over or be required to be specified.
2. Carry over the attribute implicitly.
3. Do not carry over the attribute implicitly, require it to be specified at the call site.
   1. This brings an extra question: what should the behavior be when the compiler encounters this scenario with compiled references?

#### Answer

We will go with 1.

### Further application errors (Answered)

There are a few more locations like [this](#application-error-or-warning-on-override-answered) that need to be confirmed. They include:

* Conversion operators - The spec never says that conversion operators go through overload resolution, so the implementation blocks application on these members.
  Should that be confirmed?
* Lambdas - Similarly, lambdas are never subject to overload resolution, so the implementation blocks them. Should that be confirmed?
* Destructors - again, currently blocked.
* Static constructors - again, currently blocked.
* Local functions - These are not currently blocked, because they _do_ undergo overload resolution, you just can't overload them. This is similar to how we don't
  error when the attribute is applied to a member of a type that is not overloaded. Should this behavior be confirmed?

#### Answer

All of the locations listed above are blocked.

### Langversion Behavior (Answered)

The implementation currently only issues langversion errors when `OverloadResolutionPriorityAttribute` is applied, _not_ when it actually influences anything. This
decision was made because there are APIs that the BCL will add (both now and over time) that will start using this attribute; if the user manually sets their
language version back to C# 12 or prior, they may see these members and, depending our langversion behavior, either:

* If we ignore the attribute in C# <13, run into an ambiguity error because the API is truly ambiguous without the attribute, or;
* If we error when the attribute affected the outcome, run into an error that the API is unconsumable. This will be especially bad because `Debug.Assert(bool)`
  is being de-prioritized in .NET 9, or;
* If we silently change resolution, encounter potentially different behavior between different compiler versions if one understands the attribute and another doesn't.

The last behavior was chosen, because it results in the most forward-compatibility, but the changing result could be surprising to some users. Should we confirm
this, or should we choose one of the other options?

#### Answer

We will go with option 1, silently ignoring the attribute in previous language versions.

## Alternatives
[alternatives]: #alternatives

A [previous](https://github.com/dotnet/csharplang/pull/7707) proposal tried to specify a `BinaryCompatOnlyAttribute` approach, which was very heavy-handed
in removing things from visibility. However, that has lots of hard implementation problems that either mean the proposal is too strong to be useful (preventing
testing old APIs, for example) or so weak that it missed some of the original goals (such as being able have an API that would otherwise be considered ambiguous
call a new API). That version is replicated below.

<details>

<summary>BinaryCompatOnlyAttribute Proposal (obsolete)</summary>

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
> The accessibility domain of a top-level unbound type `T` ([§8.4.4](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/types.md#844-bound-and-unbound-types)) that is declared in a program `P` is defined as follows:
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
</details>
