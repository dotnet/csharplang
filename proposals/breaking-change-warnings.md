# Breaking change warnings

## Summary
[summary]: #summary

Allow very limited breaking changes in C# when this enables significantly simpler feature designs that are easier to learn, understand and use. 

Retroactively add warnings in previous language versions to help identify and fix user code that would be vulnerable to such breaks upon a language version upgrade.

## Motivation
[motivation]: #motivation

We currently restrict new C# language features from causing any breaks (errors or behavior changes) to existing code, occasionally leading to unnatural and unintuitive design choices that make the language harder than necessary to learn and reason about.

Examples include discards `_` (sometimes an identifier), `var` (sometimes a type name) and the upcoming `field` access in auto-properties.

Breaking changes _should_ be rare and have limited impact, but sometimes they are the right thing to do. This proposal creates a mechanism by which the effects of such breaks are mitigated for existing code.  

## Detailed design
[design]: #detailed-design

When a new language feature is added in C# version `n` that may cause existing code to error or work differently, such code is detected in C# versions `n - 1` and lower, and a warning is yielded with a suggestion for how to fortify the code against the future break.

It is customary that newer compilers are used to compile older versions of C#. The compiler that supports C# version `n` will implement these warnings when used to compiler older language versions.

### Example: field access in auto-properties

As an example, let's say we introduce [field access in auto-properties](https://github.com/dotnet/csharplang/blob/main/proposals/semi-auto-properties.md) in C# 12, with a design that introduces a new `field` parameter in scope within property accessors. This new `field` parameter would shadow access to any field etc. called `field` in existing property accessors, potentially altering its meaning. We would accompany that feature with a warning in C# 11 and lower for any code that uses the identifier `field` within a property accessor:

``` c#
public class Entity
{
    string field;
    public string Field
    {
        get { return field; }         // Warning in C# 11 and below
        set { field = value.Trim(); } // Warning in C# 11 and below
    }
    ...
}
```

In C# 12 the warning would go away, and `field` in the accessors would start referencing the underlying generated field for `Field`, which would now be considered an auto-property. 

## Drawbacks
[drawbacks]: #drawbacks

### New warnings

A small number of C# users will see new warnings on existing code after directly or indirectly upgrading their compiler. These warnings point out important problems with their code, should they want to move to a newer language version. 

Hopefully users will find these warnings useful, and will prefer this early warning to finding themselves broken at a later stage. Users that do not find them useful - e.g. because they never plan to upgrade - can simply turn them off.

Tooling might embrace more sophisticated models here - "turn off temporarily", "fix automatically", etc. - that further mitigate any inconvenience of the experience, but that is outside of the scope of this proposal, and can be developed independently over time.

### Missed warnings

Upgrading to a new compiler must happen *before* upgrading to the new language version that the compiler supports, so technically there will always be a window of time to observe the warnings. 

However, users might upgrade both without a single compile in between, or they may have turned off the warnings and forgot to turn them on again.

It seems tooling can be helpful in avoiding situations where breaking change warnings are missed. For instance it could look for explicit `#pragma warning disable` directives for breaking change warnings "from the past", or try to "check one last time" on explicit language version upgrade gestures in the tool. Such features are outside of the scope of this proposal, and can be developed independently over time.

## Alternatives
[alternatives]: #alternatives

### Don't break

An alternative is to stick to the current policy and continue to live with the feature contortions that result from avoiding breaking changes at all cost.

### Break but don't mitigate

At the other end of the scale, we could allow breaking changes to the same limited level as proposed here, but simply not provide any language-level mechanism around it. Wherever code changes meaning, people will have to use docs and testing to find, debug and understand where things went wrong. Breaks may not yield errors, and differences in behavior may be subtle, so this will likely be more disruptive to users.

### Upgrading tool

This is a variant of "break don't mitigate" where the tools people use to write C# code would direct them through a dedicated upgrade experience, which would do breaking change checking as one of its steps towards a new language version.

This seems like a "boil the ocean" approach, since everyone has to take that route, even though the vast majority of users won't be affected by the breaking change. Also, it suffers from the risk that people bypass the upgrade tool (e.g. by manually changing the version in their project file) and miss out on the checking.

## Unresolved questions
[unresolved]: #unresolved-questions

For every breaking change, there will be specific design considerations concerning the associated warnings: Which patterns in existing code will trigger them? Which fixes will they recommend?

## Design meetings

- [Mar 8, 2023](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-03-08.md#limited-breaking-changes-in-c): Based on discussion [#7033](https://github.com/dotnet/csharplang/discussions/7033) the LDM decided to continue to pursue the topic.
