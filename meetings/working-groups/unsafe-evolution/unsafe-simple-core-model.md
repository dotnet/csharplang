# A simple core model for unsafe

There are many challenges and disagreements around unsafe v2. This proposal tries to establish a core framework that strikes a compromise between positions, leads to an elegant end state, allows incremental adoption and, crucially, is ruthlessly simple.

Several optional add-ons are then proposed, each of which addresses a downside with the core proposal but adds overall complexity. If we accept the core framework, then the trade-offs of each of these can be considered individually.

## Summary

1. There's a simple, assembly-wide opt-in to enable caller-unsafe enforcement and certify that non-caller-unsafe members are caller-safe.
2. `unsafe` continues to always introduce an unsafe context. With opt-in, however, `unsafe` on a member *also* marks it as caller-unsafe.
3. With opt-in, it is an error to call caller-unsafe members outside of an unsafe context.
4. Regardless of opt-in, most pointer operations are now safe. The only ones that remain unsafe are those that amount to dereferencing a pointer.

That's it for the core proposal!

There are several known drawbacks, and a set of individual optional add-ons are proposed to address those. Each trades off more complexity for mitigating perceived downsides.

## Motivation

There has been legitimate concern about changing the meaning of the existing `unsafe` keyword on members, but none of the alternatives that have emerged are attractive either. This proposal instead embraces the use of `unsafe` but tries to mitigate some of the negative consequences of choosing it.

Conceptually, it *expands* the notion of `unsafe` rather than change it. It is still the job of `unsafe` to establish a region of code that needs to be audited for safety. When put on the signature of a member, that responsibility extends to the caller. An analogy is local variables: When placed in a member body, the variables are scoped locally. When placed in a signature they become parameters, and the caller takes responsibility for initializing them. Hopefully this perspective somewhat mitigates the feeling that the meanings of `unsafe` are incompatible.

Adoption-wise, the unconditional embrace of pointers being safe means that before opt-in there is no longer a semantic difference between putting `unsafe` *on* a member versus wrapping it around its entire body. This means that as a developer you are free to move between the two notations. Which in turn means, before opt-in you can go through all your occurrences of `unsafe`, audit the body and determine whether the member they occur in should be caller-unsafe or not, then move the `unsafe` keyword accordingly - outside or inside the body. You get to put your code in the right shape, *before* you opt-in and make promises you are not yet ready to keep.

From a "greppability" standpoint, after opt-in the only keyword you really need to look for to find where to audit is `unsafe`. 

## Detailed design

### Opt-in

Opt-in is assembly wide via a binary flag to the compiler, that can be passed e.g. in a csproj file. Opt-in has the following effects:

- An attribute is added to the assembly to certify that 
    - all unsafe code within the assembly has been audited and found to be used in accordance with its documentation, and
    - every member published out of the assembly that is not marked caller-unsafe is in fact caller-safe.
- `unsafe` on a member is taken to mean that it is caller-unsafe (in addition to its existing meaning of establishing an unsafe context)
- It becomes an error to call a caller-unsafe member outside of an unsafe context.

### Pointers

Irrespective of opt-in, C# starts unconditionally treating most pointer operations as safe. This is unchanged from what's proposed in [Unsafe evolution](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md), except it applies regardless of opt-in:

> Pointer types, Fixed and moveable variables, all pointer expressions (except for pointer indirection, pointer member access, and pointer element access), and the fixed statement are all no longer considered unsafe, and exist in normal C# with no requirement to be used in an unsafe context. Similarly, declaring a fixed size buffer or an initialized stackalloc are also perfectly legal in safe C#. For all of these cases, it is only accessing the memory that is unsafe.

The operations that remain unsafe are ones that amount to dereferencing a pointer. Again from [Unsafe evolution](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md) - see there for further details:

> The following expressions require an `unsafe` context when used:
>
> * Pointer indirections
> * Pointer member access
> * Pointer element access
> * Function pointer invocation
> * Element access on a fixed-size buffer
> * `stackalloc` under certain conditions

A consequence of this change is that prior to opt-in, `unsafe` on a member becomes equivalent to `unsafe` wrapping the entire member body.

### The unsafe keyword

Regarless of opt-in, the `unsafe` keyword always introduces an unsafe context. 

Additionally, with opt-in: 
- `unsafe` on a member *also* marks it as caller-unsafe (in addition to making the member body an unsafe context).
- It becomes an error to call a caller-unsafe member outside of an unsafe context

Thus, opting in will never change the boundaries of unsafe contexts, but will restrict which members are allowed to be called outside of them.

It is an open design question whether and how opt-in affects the meaning of `unsafe` on a type.

## Optional add-on features

The simple core model has some well-known downsides, some of which can be addressed, at the cost of additional language complexity, with the additional feature proposals below. Some of the proposals could be added independently later, whereas others would need to be part of the initial release to avoid breaking changes.

### Extern and LibraryImport members

Interop methods marked `extern` or `LibraryImport` are almost always caller-unsafe. Furthermore they probably make up the majority of all members that are caller-unsafe. 

As such, it might make sense to flip the defaults and make those members caller-unsafe by default, especially because of the risk of missing some of them during migration.

However, a small percentage of such members *are* in fact caller-safe, and we would then need a way - a `safe` keyword or a `[Safe]` attribute perhaps - to mark them as such.

Flipping the default would mean that searching for `unsafe` would no longer find all caller-unsafe members.

A middle ground might be to keep `unsafe` as the way these are marked unsafe, but give a diagnostic in its absence - *unless* silenced by an attribute explicitly marking the member `[Safe]`. This would remove the risk of forgetting to mark these interop members, while retaining the regularity of the language. It would also retain the *toil* of adding `unsafe` to all these members, but that seems largely automatable.

### Required annotations for safe members

It has been argued that, when opted in, all members with unsafe contexts should be explicitly marked either `unsafe` or `safe`, or an error is issued. The motivation is to certify that the obligation implied by the `unsafe` keyword has truly been dispatched.

This would be simple to implement, but I'd argue against it. In this model, `unsafe` is how you sign off on the use of any unsafe operations. No need to sign off that you signed off! 

### Members with pointers in their signature

It's been observed that the vast majority of members that have pointers in their signature should be caller-unsafe. In turn, the vast majority of existing calls to such members are *already* in unsafe contexts, because they use pointers.

When pointer types become safe, until the caller is opted-in *and* the member is annotated as caller-unsafe, it becomes temporarily possible to call the member outside of a safe context. This creates an effective "dip" in safety, which is unfortunate.

To mitigate that, we could add a rule (regardless of opt-in) that any member that has pointers in its signature must be called from inside an unsafe context. The only way to exempt a member is for both the caller to be opted-in, and for the member to be caller-safe (i.e. opted-in and not caller-unsafe).

This would be a slight breaking change, since there are fringe ways of calling members-with-pointers outside of unsafe contexts today, e.g. by passing `null` for pointer arguments. Such code would be broken by this proposal, and would have to be mitigated by placing the calls in an unsafe context. This feels like a reasonable level of breaking change, and the mitigation can easily be tooled and automated.

### Finer-grained opt-in

Opting in at a whole-assembly level is very coarse-rained. In comparison, NRTs come with a whole mechanism for opting source code in and out at arbitrary granularity.

For NRTs this feature turned out to be expensive and unwieldy, and it runs the risk of letting people remain in a half-adopted state.

This is a feature direction that we could revisit later if the need is evident from user scenarios. It would not be breaking to add later.

### More gradual opt-in

Opt-in is binary, and gives you two things:

1. Enforcement against caller-unsafe members being called outside of unsafe contexts
2. Certification to consumers (in the form of a compiler-generated attribute) that all members are correctly annotated.

It's quite possible that one would want to turn on 1. before 2., so as to chase down bugs revealed by the enforcement *before* promising the world that you have done so!

It would be easy to add and implement such an in-between opt-in, either right away or as a result of user demand.

### Unsafe blocks in unsafe members

In this proposal, `unsafe` on members put the whole member declaration in an unsafe context. This does mean that `unsafe` blocks cannot be used to further scope the area of scrutiny within the member body. For comparison, Rust started out with a similar design and moved away from it to enable higher granularity on audit boundaries.

It is unclear whether this need is worth the complexity of allowing two levels of `unsafe` within each other. It does feel like something we could add later, although with some restrictions in the design space, if user feedback demands it.

### Unsafe expressions

With this feature, it will be more common to put `unsafe` on entire member bodies. Often, such member bodies are expressions. It would be a simple, helpful and likely uncontroversial piece of syntactic sugar to introduce an `unsafe(...)` expression form.

This could easily be added later based on user feedback.

## Drawbacks

- This still introduces a new additional meaning to `unsafe`, including in places where it is already valid. It is likely to lead to some amount of confusion, including among AI models.

## Open questions

This proposal does not address:

- How to handle `unsafe` on types. This seems downstream from most other decisions here, but does need to be addressed.
- How to handle calls from opted-in code to non-opted-in members in dependencies. This seems orthogonal to the details of this proposal, and needs to be addressed in any design.