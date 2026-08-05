# Allowing caller-safe `extern` methods

## Intro

Two problems were encountered by the runtime team while adopting the new unsafe memory rules:
1. `extern` is correctly treated as requires-unsafe, but the only way to signal that this unsafety is encapsulated is burdensome (requires wrapper method)
2. turning on the update memory safety rules and doing nothing else leaves the code in a worse state

They are described [here](https://github.com/dotnet/csharplang/pull/10058) in more details with some accompanying proposed changes to the language.

For background, LDM previously landed on the following semantics:
1. `unsafe` is used for unsafe contexts (ie. encapsulate unsafety) on blocks and signatures, 
2. we reluctantly accept that `unsafe` in a signature reads funny but already has a long-established meaning of setting unsafe context, 
3. `[RequiresUnsafe]` is used to propagate unsafety.

## Proposal

The issue with `extern` is that they are considered requires-unsafe by default, which propagates unsafety to the caller.  
Given the current semantics for the feature, the way to encapsulate unsafety is by using an unsafe context, ie. the `unsafe` keyword.

The proposal is to only treat `extern` methods as implicitly requires-unsafe when they lack unsafe context. That means `unsafe extern` methods are not implicitly requires-unsafe.

Note: the second problem is explored in the migration process proposal.
