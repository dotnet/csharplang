# Migration process

This document describes migration frameworks that are plausible each in the context of a design outcome for `unsafe` modifier in signatures.

My overall conclusions:
- broadly speaking the migration can be driven either by **diagnostics** (start with lots of errors and get them to zero) or **marker-comments** (start with lots of marker-comments and drive them to zero). A **hybrid** approach is also possible.
- those approaches are possible regardless of where we land on question of semantics of `unsafe` modifier in signatures. That can be achieved by running a fix-all that sets the code in a **minimally** or a **maximally requires-unsafe state**


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
6. Review all signatures: some of the innocuous signatures may need to be declared requires-unsafe
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

Here's what "push down" fix (C above) looks like:
```cs
// original
unsafe class C
{
  void M() { }
  void M2() { }
}

// updated: after push-down
class C
{
  unsafe void M() { }
  unsafe void M2() { }
}
```

### Punt all

Instead of starting in maximally requires-unsafe state and staying in error state for the migration period, it may make sense to start by applying a punt-all fixer first.  
It ensures the code is in **minimally requires-unsafe** initial state (no new errors).  
It applies push-down to all types with `unsafe` modifier and punt-encapsulate to all members with `unsafe` modifier.  
The process above is modified to iterate over marker comments instead of diagnostics.

Pros/cons:  
+incremental migration with passing build, so code can be periodically checked in while migration is underway  
+code reviewers get to see all the caller-safe vs. caller-unsafe decisions (diff from punt-all commit instead of from baseline), because every `unsafe` signature modifier initially gets a marker comment at least  
+removes need for step 5 (single pass forces review of all `unsafe` modifiers in signatures)
+affords an opportunity to eliminate `unsafe` modifier and side-step the language/semantics question
+the follow-ups are on signatures (which need to be reviewed) as opposed to diagnostics which are on call-sites
-one extra step to initial the migration process
-one commit of (mechanical) churn and marker comments

When the code starts in a minimally requires-unsafe state with marker comments, the migration process is:
1. Enable new memory safety rules
2. Builds sucessfully
3. Deal with marker comments iteratively choosing between
  - A. encapsulate (remove marker comment)
  - B. propagate (mark as requires-unsafe and deal with diagnostics fallout)
  - Builds successfully between iterations
4. Optional: Review all `unsafe` blocks and types to shrink them in accordance with new guidelines

Note: a fix-all code action/fixer is an automatic code fixer that applies to the whole codebase. It does not mean "fix all my migration choices".

## Assuming spec'ed semantics for `unsafe` modifier in signature

With `unsafe` in signatures keeping the meaning of unsafe-context, the code starts neither in minimally nor in maximally requires-unsafe state.
A fix-all must be applies to put us in minimally or in maximally requires-unsafe state.  

To put the code in **minimally requires-unsafe** state and ready to drive the migration process with marker comments, the fix-all applies the following changes:
1. a marker comment is added to members with `unsafe` modifier or in `unsafe` type
2. all `extern` methods are marked as not requires-unsafe (however language allows) and a marker comment
Note: no need for push-down since `unsafe` modifier on type only affects unsafe-context (and those semantics remain).

To put the code in **maximally requires-unsafe** state and ready to drive the migration process with diagnostics, the fix-all applies the following changes:
1. all members with `unsafe` modifier or in `unsafe` type are marked as requires-unsafe
2. all `extern` methods are marked as requires-unsafe

A **hybrid** approach is also possible for the fix-all. It aims to get the code closer to final form with a simple heuristic and relies on diagnostics and marker-comments to ensure proper follow-up.  
This fix-all approach applies the following changes:  
1. if the `unsafe` signature has pointers, then mark the method as requires-unsafe (that means the user will follow-up on resulting diagnostics)
2. otherwise, convert `unsafe` signature to `unsafe` block and leave a marker comment to revisit

Note: it is possible for someone to turn on the new rules and get no diagnostic. Without diagnostics, there is no forcing mechanism to run a fix-all. This is leaving the user in a worse state.
Some options to address this:
1. signatures with pointer as implicitly requires-unsafe
2. `unsafe` in signature means requires-unsafe
3. disallow `unsafe` in signature (avoids contextual magic)
4. error unless certain methods aren't explicitly annotated one way or another

Note: how do you know when migration is complete? What is the gesture?
