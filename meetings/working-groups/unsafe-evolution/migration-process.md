# Migration process

This document describes migration frameworks that are plausible each in the context of a design outcome for `unsafe` modifier in signatures.

My overall conclusions:
- broadly speaking the migration can be driven either by diagnostics (start with lots of errors and get them to zero) or marker-comments (start with lots of marker-comments and drive them to zero)
- both approaches are possible, regardless of where we land on question of semantics of `unsafe` modifier in signatures. That can be achieved by running a fix-all that sets the code in a minimally or a maximally requires-unsafe state

## Assuming new semantics for `unsafe` modifier in signature

With  `unsafe` in signature meaning requires-unsafe, the migration process is primarily driven by diagnostics, because the code starts in maximally requires-unsafe state.

1. Enable new memory safety rules
2. Build is broken
3. Deal with the diagnostics by iteratively choosing between:
  - A. encapsulate:
    - A1. convert some `unsafe` signatures to `unsafe` blocks to signify "safe" (applies to `unsafe` modifier in signature)*
    - A2. mark some externs as safe however the language allows (applies to `extern` methods)*
    - A3. add `unsafe` blocks*
  - B. punt-encapsulate: same options as above, but leave a marker comment to revisit*
  - C. push-down: push unsafe down from type to members*
  - D. propagate: marks a member as caller-unsafe  
  - Note: One-click code actions/fixers could simplify the edits marked with asterisks, as long as they just insert top-level (ie. non-minimal) `unsafe` blocks. Minimal `unsafe` blocks would need authoring (human or AI).
4. Builds successfully
5. Review all remaining `unsafe` signatures to confirm whether to encapsulate or propagate (diagnostic-driven approach of step 2 may not have flagged)
6. Optional: Review all `unsafe` blocks to shrink them in accordance with new guidelines


Here's what "encapsulate on a method signature" fix (A1 above) looks like:
```cs
unsafe void* M() { ... body ... } // original
void* M() { unsafe { ... body ... } } // updated: after encapsulate for method
```

Here's what "encapsulate on an extern method" fix (A2 above) looks like:
```cs
extern void M(); // original
safe extern void M(); // updated: after encapsulate for extern method, using `safe` keyword
[RequiresUnsafe(false)] extern void M(); // updated: after encapsulate for extern method, using attribute (alternative language design option)
```

### Punt all

Instead of staying in error state for the migration period, it may make sense to start by applying a punt-all fixer first.  
It ensures the code is in minimally requires-unsafe state (no new errors).  
It applies push-down to all types with `unsafe` modifier and punt-encapsulate to all members with `unsafe` modifier.  
The process above is modified to iterate over marker comments instead of diagnostics.

Pros/cons:  
+incremental migration with passing build, so code can be periodically checked in while migration is underway  
+code reviewers get to see all the caller-safe vs. caller-unsafe decisions (diff from punt-all commit instead of from baseline), because every `unsafe` signature modifier initially gets a marker comment at least  
+removes need for step 5 (single pass forces review of all `unsafe` modifiers in signatures)
-one commit of churn and marker comments

When the code starts in a minimally requires-unsafe state with marker comments, the migration process is:
1. Enable new memory safety rules
2. Builds sucessfully
3. Deal with marker comments iteratively choosing between
  - A. encapsulate (remove marker comment)
  - B. propagate (mark as requires-unsafe and deal with diagnostics fallout)
  - Builds successfully between iterations
4. Optional: Review all `unsafe` blocks and types to shrink them in accordance with new guidelines

## Assuming spec'ed semantics for `unsafe` modifier in signature

With `unsafe` in signatures keeping the meaning of unsafe-context, the code starts neither in minimally nor in maximally requires-unsafe state.
A fix-all must be applies to put us in minimally or in maximally requires-unsafe state.  

To put the code in minimally requires-unsafe state and ready to drive the migration process with marker comments, the fix-all applies the following changes:
1. a marker comment is added to members with `unsafe` modifier or in `unsafe` type
2. all `extern` methods are marked as not requires-unsafe (however language allows) and a marker comment
Note: no need for push-down since `unsafe` modifier on type only affects unsafe-context (and those semantics remain).

To put the code in maximally requires-unsafe state and ready to drive the migration process with diagnostics, the fix-all applies the following changes:
1. all members with `unsafe` modifier or in `unsafe` type are marked as requires-unsafe
2. all `extern` methods are marked as requires-unsafe
