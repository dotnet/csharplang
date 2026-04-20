# Proposed modifications to unsafe spec

## Intro

As a reminder, we want to address all of the following goals in our design:

1) clear, simple rules on which methods are caller unsafe vs. use unsafe
2) users annotate their code based on the rules of unsafev2, not unsafev1
3) annotation is easily auditable, meaning we can see whether a given project has aligned their code with unsafev2


This feature will introduce compilation errors in existing unsafe code when opted into. High-confidence AI-assisted automation of the migration process flow is a part of the feature design.

## Proposal

After working with the current unsafe model in dotnet/runtime, we've found two problems.

The first is related to `extern` methods. Right now all extern methods are considered `RequiresUnsafe`. This means that the unsafety from the extern method propagates out to its callers. This is the correct default behavior, but this is a problem because some extern methods do not propagate. For example, a correctly-written P/Invoke into a safe Rust function should be considered safe -- the Rust and C# rules are 'compatible' in the sense that all C# safety requirements are satisfied by the Rust safety rules. 

We would like to be able to easily annotate some extern methods as "safe". The current default of `RequiresUnsafe` behavior is still necessary because it is not known from C# whether or not the implementation of the extern method is safe (in the sense defined by our safety model). It is up to the user to declare that the method is safe, in the same way that users can declare through unsafe blocks that methods are safe but may be implemented with unsafe code.

The current way of doing this is defining a new method to wrap the extern method in an unsafe block. This could have significant metadata size implications for assemblies with lots of extern method definitions.

There are two potential syntaxes for the proposed modification:
1. Keep the `RequiresUnsafe` attribute but add a `bool` parameter. `RequiresUnsafe(false)` would mean "safe" and would effectively reverse the default for extern methods. For all other methods, it would be equivalent to not having `RequiresUnsafe` at all.
2. Go back to having an `unsafe` keyword at the method level for caller-unsafe code, and add a new `safe` keyword "caller-safe" extern methods.

There is a further option where we require either safe/unsafe be specified on every extern method. This could increase clarity in two ways:

1. The reader could clearly identify whether any given extern is safe/unsafe.
2. It would help identify whether an extern method has been audited.
 
The second problem is the process of unsafev2 adoption. The current language feature status has the following problems:

1. After the feature has been enabled, but before any `[RequiresUnsafe]` attributes have been applied, the code is in a more unsafe than when it started. Methods taking pointers, by all existing language rules, must have the `unsafe` keyword. While annotating we found that 97% of all such methods should be considered `RequiresUnsafe` under the new rules. In unsafev2, methods will no longer produce warnings on their own, but they will have also have all their warnings suppressed due to unsafe on the method/type level. Most of these methods should eventually end up with `RequiresUnsafe`, but there is no assistance or forcing function to reach correct annotation.
2. The current feature status is confusing when both `RequiresUnsafe` and `unsafe` are applied to the same method. Even experienced C# users have found this combination surprising.
3. Based on the goals (1) and (2), the application of `unsafe` on methods and types is undesirable. These annotations represent broad suppression -- especially the application on a type. The correct long-term annotation in unsafev2 will only feature narrow unsafe suppresions, not broad.

Based on the above there are some options:

1. Move back to `unsafe` at the signature level meaning `RequiresUnsafe`. For types, this would imply all nested members that can be marked `unsafe` would be treated as `unsafe`. This is a conservative approach: all existing methods, types, and fields that use pointers will be `RequiresUnsafe`. This effectively assumes that any existing unsafety propagates upwards. If this is not true, the code owner can narrow their unsafe scope. By producing errors at the call site, it will encourage owners to accurately identify the minimum necessary scope.
2. Disallow `unsafe` at the method, type, and field level. This will immediately force code to be adapted to the unsafev2 rules, either through automated or manual modification.

## Rejected designs

### Safe extern methods

For extern methods, any design which makes extern methods safe-by-default is in direct contradiction with the soundness requirements of the unsafe model. All extern methods must be marked safe only after direct examination of the implementation or documentation of the corresponding external method.
