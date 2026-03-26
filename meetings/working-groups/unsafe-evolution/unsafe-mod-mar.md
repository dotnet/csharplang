# Proposed modifications to unsafe spec

## Intro

As a reminder, we want to address all of the following goals in our design:

1) clear, simple rules on which methods are caller unsafe vs. use unsafe
2) users annotate their code based on the rules of unsafev2, not unsafev1
3) annotation is easily auditable, meaning we can see whether a given project has aligned their code with unsafev2
4) support for multitargeting with unsafev1-only TFMs
This feature will introduce compilation errors in existing unsafe code when opted into. High-confidence AI-assisted automation of the migration process flow is a part of the feature design.
## Proposal

After working with the current unsafe model in dotnet/runtime, we've found two problems.

The first is related to `extern` methods. Right now all extern methods are considered `RequiresUnsafe`. This means that the unsafety from the extern method propagates out to its callers. This is the correct default behavior, but this is a problem because some extern methods do not propagate. For example, a correctly-written P/Invoke into a safe Rust function should be considered safe -- the Rust and C# rules are 'compatible' in the sense that all C# safety requirements are satisfied by the Rust safety rules. 

We would like to be able to easily annotate some extern methods as "safe". The current default of `RequiresUnsafe` behavior is still desired because it is not known from C# whether or not the implementation of the extern method is safe (in the sense defined by our safety model). It is up to the user to declare that the method is safe, in the same way that users can declare through unsafe blocks that methods are safe but may be implemented with unsafe code.

The current way of doing this is defining a new method to wrap the extern method in an unsafe block. This could have significant metadata size implications for assemblies with lots of extern method definitions.

There are two potential syntaxes for the proposed modification:
1. Keep the `RequiresUnsafe` attribute but add a `bool` parameter. `RequiresUnsafe(false)` would mean "safe" and would effectively reverse the default for extern methods. For all other methods, it would be equivalent to not having `RequiresUnsafe` at all.
2. Go back to having an `unsafe` keyword at the method level for caller-unsafe code, and add a new `safe` keyword "caller-safe" extern methods.

The decision here should be made with knowledge of the second concern.

The second problem is the large number of useless and potentially confusing changes being made while annotating existing methods that contain pointers. All such methods, by all existing language rules, must have the `unsafe` keyword. While annotating we found that 97% of all such methods should be considered `RequiresUnsafe` under the new rules. In order to adopt the feature we have had to do very large code migrations to add `RequiresUnsafe` to these methods. This is busywork -- if the `unsafe` keyword changed its meaning to mean "caller-unsafe" when placed on a signature, almost all changes would be unnecessary. We believe the same is likely true for customers. In addition to the large number of useless modifications, the resulting method signatures are confusing and will eventually be against guidelines. Having both `RequiresUnsafe` and `unsafe` on a method is confusing. Without consulting the language specification, these terms appear to be duplicated and it's not clear why both are needed or desired. In addition, if our guidance (as proposed) is to narrowly scope `unsafe` blocks when possible, the use of `unsafe` on a method will itself be a violation of good practice, necessitating removal. 

Effectively, the use of the `[RequiresUnsafe]` attribute is not saving work in adopting the feature, nor is it making the language clearer. The best path is to go back to the proposed `unsafe` (and now `safe`) keywords.
