# Allowing caller-safe `extern` methods

## Intro

Two problems were encountered by the runtime team while adopting the new unsafe memory rules:
1. `extern` is correctly treated as requires-unsafe, but the only way to signal that this unsafety is encapsulated is burdensome (requires wrapper method)
2. concern over code churn during migration (adding `[RequiresUnsafe]`) for many methods that currently have an `unsafe` modifier

They are described [here]([url](https://github.com/dotnet/csharplang/pull/10058)) in more details with some accompanying proposed changes to the language.

For background, LDM previously landed on the following semantics:
1. `unsafe` is used for unsafe contexts (ie. encapsulate unsafety) on blocks and signatures, 
2. we reluctantly accept that `unsafe` in a signature reads funny but already has a long-established meaning of setting unsafe context, 
3. `[RequiresUnsafe]` is used to propagate unsafety.

## Proposal

The issue with `extern` is that they are considered requires-unsafe by default, which propagates unsafety to the caller.  
Given the current semantics for the feature, the way to encapsulate unsafety is by using an unsafe context, ie. the `unsafe` keyword.

The proposal is to only treat `extern` methods as implicitly requires-unsafe when they lack unsafe context. That means `unsafe extern` methods are not implicitly requires-unsafe.

Note: neither proposals effectively addresses the second problem. If `unsafe` on a method were to signal requires-unsafe as opposed to unsafe-context, then `[RequiresUnsafe]` churn is merely replaced with `unsafe` block churn.
