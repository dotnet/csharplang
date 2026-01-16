# Alternative syntax for caller-unsafe

This proposal amends and references [Unsafe evolution](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md). Read that one first!

## Summary

Key differences from the [Unsafe evolution](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md) proposal:

- Mark caller-unsafe members directly with the `[RequiresUnsafe]` attribute instead of repurposing the `unsafe` keyword to generate that.
- Keep the existing meaning of `unsafe` as is: designating a code region as an unsafe context.
- Use the proposed opt-in mechanism only to control the enforcement against invoking caller-unsafe members outside of unsafe regions. It does not affect what `unsafe` means or whether a member can be marked caller-unsafe.

```csharp
public static class Unsafe
{
    [RequiresUnsafe] // Always allowed - even in older language versions
    [System.CLSCompliant(false)] 
    public static ref T AsRef<T>(void* source) where T : allows ref struct { ... }
    ...
}
void M()
{
    int i = 1;
    int* ptr = &i; // No longer unsafe in either proposal
    
    Console.WriteLine(*ptr); // Error outside of unsafe region
    ref int intRef = Unsafe.AsRef(ptr); // Error if enforcement is opted in
    
    unsafe
    {
        Console.WriteLine(*ptr); // Allowed in unsafe region
        ref int intRef = Unsafe.AsRef(ptr); // Allowed in unsafe region
    }
}
```

## Motivation

This proposal is primarily motivated by avoiding breaking changes in the language, but does provide additional benefits.

### Declaration vs consumption

The existing `unsafe` feature is all about where a user is allowed to *consume* unsafe operations. Both proposals add the ability to *declare* new unsafe operations - in the form of caller-unsafe members.

However, the original proposal repurposes the syntax of the *consumption* feature for *declaration* purposes, muddling the distinction between the two.

**With this proposal**, instead, declaration gets a separate syntax. Declaration is likely much more rare than consumption, and having a dedicated keyword seems unwarranted. An attribute seems just fine, especially since that's what the original proposal compiles down to anyway.

### Language breaking changes

Both proposals support users introducing *API breaking changes* (mitigated by an opt-in mechanism) by marking existing APIs caller-unsafe.

The original proposal additionally introduces two *language breaking changes*:

1. **Unsafe members**: Existing uses of `unsafe` on a member are generally intended only to make the member body an unsafe context. They may be used as a more convenient shorthand for wrapping the whole member body in an `unsafe` statement. However, with the new semantics of the original proposal, an `unsafe` member is now considered caller-unsafe, and existing calls to the member will break.
2. **Unsafe types**: Existing uses of `unsafe` on a type are intended to make the type body an unsafe context. They may be used as a more convenient shorthand for marking each member unsafe. However, with the new semantics of the original proposal, an `unsafe` type is no longer an unsafe context, and unsafe operations within the type will break.

When the language change is in effect, it is likely to break nearly every occurrence of `unsafe` on members and types! These breaks impose a hardship on users without accruing any additional safety benefits. Users need to do significant work just to get back to an equivalent state to what they had before.

We have never attempted a breaking change on even close to that scale in C#, and would need incredibly compelling arguments to change that stance. Arguments such as "we truly have no other viable option." However:

**With this proposal**, the language breaking changes go away. The `unsafe` modifier continues to mean what it has always meant: introducing an unsafe context.

### Time of effect

With the original proposal, all of these changes to semantics take effect together, when an opt-in is specified:

- The `unsafe` modifier on a member changes its meaning to an indicator that the member is caller-unsafe.
- The `unsafe` modifier on a type changes its meaning to a no-op.
- Some operations such as pointer types and many pointer expressions cease being considered unsafe.
- A call to a caller-unsafe member outside of an unsafe context becomes an error.

This all-or-nothing approach to opt-in effectively means adoption must happen in bulk. It raises the burden for someone keen to make their code more safe: They cannot get enforcement of caller-unsafe calls until they have gone through the work of mitigating the language breaking change.

Arguably this makes the impact of the language breaking change even worse, because it doesn't happen cleanly on a C# version boundary. In new C# versions going forward, the meaning of `unsafe` in code will not be clear in and of itself, but will depend on a separate opt-in mechanism.

**With this proposal**, on the other hand:

- There *is* no language breaking change: the `unsafe` modifier keeps its meaning.
- The relaxation of previously unsafe operations can happen cleanly on a language version boundary, and doesn't need to be tied to opt-in.
- Marking a member caller-unsafe is independent of opt-in (and even of language version). `[RequiresUnsafe]` is just an attribute.
- Only the *enforcement* of `[RequiresUnsafe]` is guarded by an opt-in.

### Opt-in granularity

The original proposal envisions an opt-in mechanism as a mitigation for the fact that *newly marking an existing member caller-unsafe is an API breaking change*. It would be harsh to mark a number of existing members caller-unsafe in a new version of .NET if there weren't also a mechanism for people to manage the break to their consuming code.

What about *new* caller-unsafe members, though? Presumably we and others will keep adding such members in significant numbers. Why should a modern C# user be allowed to avoid enforcement when calling those? After all, they won't have existing code already calling them outside an unsafe context.

It seems like the original proposal leaves safety on the table by treating new and existing members equally. While we may initially see a lot of existing members being annotated, over time the majority would shift to newly added members.

**With this proposal** the caller-unsafe marker is an attribute. As such, it *could* use attribute arguments to specify whether a caller-unsafe member should be subject to opt-in or not. For instance, existing members could be annotated with something like `[RequiresUnsafe(optional:true)]` which lets their enforcement be controlled by an opt-in flag. New caller-unsafe members would just use `[RequiresUnsafe]` which defaults to `optional:false`. Over time, after an initial transition period, this would become the common case.

Libraries could also use this to "tighten the screws" over several releases, limiting their users' ability to evade enforcement. 

*Note:* Such arguments on `RequiresUnsafe` are not part of this proposal. We're merely pointing out that the proposal, unlike the original one, *allows* for such a design.

### Unsafe context in caller-unsafe members

In the original proposal, annotating a member as caller-unsafe *also* makes the whole member an unsafe context.

This means that the member's author doesn't get more fine-grained control over which parts of the member body may use unsafe operations. That's unfortunate, as it increases the surface area that needs to be manually audited for safety issues.

**With this proposal** the `[RequiresUnsafe]` attribute does not imply that the member is an unsafe context. The author is free to mark the boundaries that make the most sense.

## Detailed design

- Keep the `unsafe` keyword with the same meaning in the same places as today: In every location it simply denotes a region of code as an unsafe context.

- Remove certain operations from the set of language-defined unsafe operations, exactly as in the original proposal. This takes effect unconditionally for the C# version where it is introduced.

- Designate caller-unsafe members directly with the `[RequiresUnsafe]` attribute instead of repurposing the `unsafe` keyword. The attribute does *not* automatically make member bodies unsafe contexts; an explicit `unsafe` region is needed for that.

- Introduce a compiler opt-in mechanism that controls whether caller-unsafe methods are prevented from being called outside of unsafe contexts. When opted-in, the assembly gets marked with `[MemorySafetyRules]` just as in the original proposal.

- Consider augmenting `[RequiresUnsafe]` with arguments to distinguish new vs existing caller-unsafe members and regulate the impact of the opt-in mechanism on the members' allowed use.

- Other aspects remain unchanged from the original proposal, e.g.:
    - Placement of `[RequiresUnsafe]` needs to obey the same restrictions as the original proposal lays out for `unsafe` on members.
    - Delegates and lambdas must obey equivalent rules to the original proposal.
    - Members marked `extern` are implicitly caller-unsafe.

There is an open question around "compat mode" below.

## Drawbacks

- **No dedicated syntax:** Unlike the original proposal, this version falls back to attributes to express the intended semantics. It wouldn't be the only place in the language where member annotations are by attribute, but maybe there are arguments why this situation is more common or important and deserves a keyword.

## Alternatives

- **New keyword:** A dedicated keyword (or keyword combination) different from `unsafe` could be used instead of an attribute. This would still address many of the problems in the original proposal, in particular the language breaking changes.

- **Different attribute name:** This proposal uses `[RequiresUnsafe]` because it is descriptive of the meaning and is what the original proposal compiles down to. But it's possible that another name might be better.

## Open questions
    
### Compat mode

The original proposal suggests a "compat mode": When a compilation is "opted-in" to the new semantics but depends on an assembly that is not, a heuristic is applied to members of that assembly to decide whether they should be considered caller-unsafe. The heuristic involves the presence of pointer or function pointer types in the member's signature.

This allows some freedom in the order that compilations are opted-in, while preserving a measure of safety when consuming assemblies that are not.

The original proposal uses the presence of `[MemorySafetyRules]` in an assembly to determine whether it was annotated, which has some validity because if it didn't have the attribute it *couldn't* have been annotated. Essentially there's an assumption that when an assembly opts in it takes responsibility for appropriately annotating its members.

In this alternative proposal, is it still reasonable to use `[MemorySafetyRules]` as the only indication that members have been properly annotated and compat mode should not be used? After all they could have been annotated with `[RequiresUnsafe]` even *without* the assembly having been opted in. Perhaps any assembly with at least one `[RequiresUnsafe]` should also be considered annotated?

This seems a topic for further discussion.