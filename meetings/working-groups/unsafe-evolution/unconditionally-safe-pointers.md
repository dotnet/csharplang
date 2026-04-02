# Unconditionally safe pointers

## Summary

Unsafe v2 moves pointer types and some of their uses from unsafe to safe when opted in to the new rules.

This proposal makes this change unconditional - it is not gated on opt-in.

On its own, this would have the downside that calling members with pointers in the signature would no longer be considered unsafe, despite the fact that the vast majority of such members should in fact be considered caller-unsafe.

This proposal addresses that downside by making it an error to call members with pointers in their signature outside of a safe context.

This is a slight breaking change, because such calls are in fact very occasionally allowed today (when no pointer-related syntax occurs). We argue that the break is well within reasonable limits.

## Motivation

Having the safety of pointer type use depend on opt-in is unfortunate for multiple reasons:

- It prevents us from universally discouraging `unsafe` on members (including through analyzers or warnings).
- It contributes unnecessarily to the gap between opted-in and non-opted-in scenarios.
- It carries pointer unsafety forward into future C# versions and compilers.

Reducing the number of things that depend on an opt-in flag is a meaningful simplification for users, for source generators and for designers and implementers of the language.

The proposal's restriction on calling members-with-pointers addresses the "dip" in safety in some proposals when new unsafe rules are adopted.

This proposal is independent of other design choices related to unsafe evolution, including around syntax.

## Detailed design

The [Unsafe evolution](https://github.com/dotnet/csharplang/blob/main/proposals/unsafe-evolution.md) specification states that under the new rules:

> Pointer types, Fixed and moveable variables, all pointer expressions (except for pointer indirection, pointer member access, and pointer element access), and the fixed statement are all no longer considered unsafe, and exist in normal C# with no requirement to be used in an unsafe context. Similarly, declaring a fixed size buffer or an initialized stackalloc are also perfectly legal in safe C#. For all of these cases, it is only accessing the memory that is unsafe.

This proposal states that this relaxation applies unconditionally in C# going forward.

It does however add a new restriction in its stead: Any member that has pointers in its signature must be called from inside an unsafe context.

This is not saying that members-with-pointers are "caller-unsafe" in the meaning of the new unsafe evolution. Any opt-ins or opt-outs that apply to those do not apply here.

## Drawbacks

In the general case this is not a breaking change, because code calling members-with-pointers today usually needs pointers to do so, and therefore is already restricted to unsafe contexts. However, because today's enforcement is syntactic, it is possible to e.g. pass `null` to such a call in a safe context without error. 

Such calls are inadvisable and likely rare, but this proposal would break them. The mitigating fix (which tools can suggest and automate) is to wrap the call in an unsafe context.

## Open questions

Members that are marked "caller-safe" should probably be allowed to be called outside of unsafe contexts even if they have pointers in their signature - at least under *some* conditions of opt-in or opt-out. The details of this will have to depend on the design of the wider Unsafe feature.