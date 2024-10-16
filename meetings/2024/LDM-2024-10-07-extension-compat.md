# Extension compat

1. To what extent can new extension methods replace old ones without a change in behavior?
2. What are your options when they can't?


## Kinds of compat

1. Binary compat - code compiled against old extension methods works the same way against new ones
2. Source compat - code that compiled against old extension methods compiles and works the same way against new ones
    a. When used as extension methods
    b. When used as static methods


## Priority between old and new extension methods

We have three options for how to handle lookup between competing old and new extension methods in a given scope, giving preference to either the former, the latter or neither.

### Neither is preferred
This is only an option if we can somehow get old and new extension methods into the same candidate set. If we do this, old extension methods must be removed as soon as new replacements are made - coexistence would result in ambiguities.

Compat breaks would arise when the new method doesn't apply in the same circumstances, or behaves differently when it does.

It is possible that we could mitigate this with an attribute like `[OverloadResolutionPriority]`, deferring to one only when the other doesn't apply.

### New extensions are preferred
This would allow old extension methods to stay around as backup methods. 

Compat breaks would arise whenever a new extension method does not - or cannot - behave exactly as the old one. It is possible for migrated extension methods to start silently shadowing unrelated old ones that would have previously been the best fit.

### Old extensions are preferred
This would allow new versions of extension methods to be declared alongside other extension members in the new style, even as old ones would be preferred for compat reasons, if kept around. This might allow a mitigation period while compat breaks are identified and addressed: old ones could be brought into scope in a given file until compat issues have been dealt with.


## Preference/disambiguation syntax

New extension methods will likely have a different disambiguation approach that the old ones, where you just call the static method. 

Any such calls would possibly be broken when using the new declaration syntax, both from a source and binary compat point of view.

It's possible that we could promise to generate static methods with exactly the same signature from new extension methods, at least in a large and well-defined number of cases. Alternatively, the old extension methods could be kept around as ordinary static methods for this purpose.

## Type inference

The current design of new extensions splits generic type inference into two parts: one for a generic underlying type (which needs to be improved somewhat) and one for a generic extension method. 

This leads to different results in corner cases, including whether the extension method applies to a given receiver, and which type arguments are inferred when it does. Those situations constitute source compat breaks.

There is a proposal to rejoin the two phases into one in the new approach. Chances are that if we did, few people would notice a difference. For any non-method extension members there probably wouldn't *be* a difference, since only the underlying type would be potentially generic anyway. So this would be a possible mitigation.

## Name of enclosing class

Old extension methods of different receiver types can be grouped together in one static class. The current design for extension types requires a different extension type declaration for each underlying type. In such situations, when porting old extension methods, some will end up in a generated class with a different name than before.

This compromises binary compat of callsites, as well as source compat when used directly as static methods.

It is possible that attributes could be used to force generated extension methods onto a different class than the one they are declared in. Or extension members could have a syntax to override the underlying type specified by the extension type.


## Refness of receiver

Old extension methods allow receivers to be `ref` or `in` parameters only on value types. New extensions automatically manage ref-ness so that it matches the behavior of `this` in type declarations: In reference types it is a value parameter, and value types it is a `ref` parameter. This is the case even when the underlying type is an unconstrained type parameter - callsites are generated to manage this at runtime! The generated method itself in these cases takes a ref parameter as the receiver, and the callsite will take a copy when the type argument is a reference type.

This means that there is no compatible way to port an extension method that has a value-parameter receiver of a value type or unconstrained generic type, and unconstrained generic extension methods would be generated with a ref instead of a value parameter.

We could potentially mitigate this with an attribute on the member to prevent ref generation, or as part of a feature to allow extension members to "override" their underlying type.


## Nullability of receiver

Old extension methods can - and very occasionally do - have nullable reference types as receiver types, whereas the current design for new extension methods does not allow for that. Even if `for C?` syntax was allowed for underlying reference types, it's unlikely that you would want the underlying type to be nullable for every extension member on that type.

We could mitigate this through a memberwise attribute, or as part of a feature to allow extension members to "override" their underlying type.