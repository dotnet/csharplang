# Migration process

This document does not assume consensus on all aspects of Unsafe Evolution. Instead, it describes migration frameworks that are plausible each in the context of a design outcome for `unsafe` modifier in signatures.

## Assuming new semantics for `unsafe` modifier in signature

With  `unsafe` in signature meaning requires-unsafe, the migration process is primarily driven by diagnostics:

1. Enable new memory safety rules
2. Build is broken
3. Deal with the diagnostics by iteratively choosing between:
  - A. encapsulate:
    - convert some `unsafe` signatures to `unsafe` blocks to signify "safe" (applies to `unsafe` modifier in signature)*
    - mark some externs as safe however the language allows (applies to `extern` methods)*
    - add `unsafe` blocks*
  - B. punt-encapsulate: same options as above, but leave a marker comment to revisit*
  - C. push-down: push unsafe down from type to members*
  - D. propagate: marks a member as caller-unsafe  
  - Note: One-click code actions/fixers could simplify the edits marked with asterisks, as long as they just insert top-level (ie. non-minimal) `unsafe` blocks. Minimal `unsafe` blocks would need authoring (human or AI).
4. Builds successfully
5. Review all remaining `unsafe` signatures to confirm whether to encapsulate or propagate (diagnostic-driven approach of step 2 may not have flagged)
6. Optional: Review all `unsafe` blocks to shrink them in accordance with new guidelines

## Punt all

Instead of staying in error state for the migration period, it may make sense to start by applying a punt-all fixer first.  
It would apply push-down to all types with `unsafe` modifier and punt-encapsulate to all members with `unsafe` modifier.  
This ensures no member is requires-unsafe initially (no new errors).  
The process above is modified to iterate over marker comments instead of diagnostics.

Pros/cons:  
+incremental migration with passing build  
+code reviewers get to see all the caller-safe vs. caller-unsafe decisions (diff from punt-all commit instead of from baseline), because every `unsafe` signature modifier initially gets a marker comment at least  
+removes need for step 5 (single pass forces review of all `unsafe` modifiers in signatures)
-one commit of churn and marker comments

## Assuming spec'ed semantics for `unsafe` modifier in signature

With `unsafe` in signatures keeping the meaning of unsafe-context, we cannot rely on diagnostics to drive the migration.  
The process is driven by marker comments created at the initial step (punt-all).  

1. punt-all fix-all: punt-encapsulate to all members with `unsafe` modifier.
  Note: no need for push-down since `unsafe` modifier on type only affects unsafe-context (and those semantics remain).
2. Enable new memory safety rules
3. Builds sucessfully
4. Deal with marker comments iteratively choosing between
  - A. encapsulate (remove marker comment)
  - B. propagate (mark as requires-unsafe and deal with diagnostics fallout)
  - Builds successfully between iterations
5. Optional: Review all `unsafe` blocks and types to shrink them in accordance with new guidelines

Note: if we prefer a diagnostic-driven model, we can have the initial fix-all put us in maximally requires-unsafe state. It would mark all members with `unsafe` modifier or in `unsafe` type as requires-unsafe.  
