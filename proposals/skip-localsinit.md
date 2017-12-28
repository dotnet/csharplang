# Suppress emitting of `localsinit` flag.

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Allow suppressing emit of `localsinit` flag via `SkipLocalsInitAttribute` attribute. 

## Motivation
[motivation]: #motivation


### Background
Per CLR spec local variables that do not contain references are not initialized to a particular value by the VM/JIT. Reading from such variables without initialization is type-safe, but otherwise the behavior is undefined and implementation specific. Typically uninitialized locals contain whatever values were left in the memory that is now occupied by the stack frame. That could lead to nondeterministic behavior and hard to reproduce bugs. 

There are two ways to "assign" a local variable: 
- by storing a value or 
- by specifying `localsinit` flag which forces everything that is allocated form the local memory pool to be zero-initialized
NOTE: this includes both local variables and `stackalloc` data.    

Use of uninitialized data is discouraged and is not allowed in verifiable code. While it might be possible to prove that by the means of flow analysis, it is permitted for the verification algorithm to be conservative and simply require that `localsinit` is set.

Historically C# compiler emits `localsinit` flag on all methods that declare locals.

While C# employs definite-assignment analysis which is more strict than what CLR spec would require (C# also needs to consider scoping of locals), it is not strictly guaranteed that the resulting code would be formally verifiable:
- CLR and C# rules may not agree on whether passing a local as `out` argument is a `use`.
- CLR and C# rules may not agree on treatment of conditional branches when conditions are known (constant propagation).
- CLR could as well simply require `localinits`, since that is permitted.  

### Problem
In high-performance application the cost of forced zero-initialization could be noticeable. It is particularly noticeable when `stackalloc` is used.

In some cases JIT can elide initial zero-initialization of individual locals when such initialization is "killed" by subsequent assignments. Not all JITs do this and such optimization has limits. It does not help with `stackalloc`.

To illustrate that the problem is real - there is a known bug where a method not containing any `IL` locals would not have `localsinit` flag. The bug is already being exploited by users by putting `stackalloc` into such methods - intentionally to avoid initialization costs. That is despite the fact that absence of `IL` locals is an unstable metric and may vary depending on changes in codegen strategy. 
The bug should be fixed and users should get a more documented and reliable way of suppressing the flag. 

## Detailed design

Allow specifying `System.Runtime.CompilerServices.SkipLocalsInitAttribute` as a way to tell the compiler to not emit `localsinit` flag.
 
The end result of this will be that the locals may not be zero-initialized by the JIT, which is in most cases unobservable in C#.  
In addition to that `stackalloc` data will not be zero-initialized. That is definitely observable, but also is the most motivating scenario.

Permitted and recognized attribute targets are: `Method`, `Property`, `Module`, `Class`, `Struct`, `Interface`, `Constructor`. However compiler will not require that attribute is defined with the listed targets nor it will care in which assembly the attribute is defined. 

When attribute is specified on a container (`class`, `module`, containing method for a nested method, ...), the flag affects all methods contained within the container.

Synthesized methods "inherit" the flag from the logical container/owner. 

The flag affects only codegen strategy for actual method bodies. I.E. the flag has no effect on abstract methods and is not propagated to overriding/implementing methods.

This is explicitly a **_compiler feature_** and **_not a language feature_**.  
Similarly to compiler command line switches the feature controls implementation details of a particular codegen strategy and does not need to be required by the C# spec.

## Drawbacks
[drawbacks]: #drawbacks

- Old/other compilers may not honor the attribute.
Ignoring the attribute is compatible behavior. Only may result in a slight perf hit.

- The code without `localinits` flag may trigger verification failures.
Users that ask for this feature are generally unconcerned with verifiability. 
 
- Applying the attribute at higher levels than an individual method has nonlocal effect, which is observable when `stackalloc` is used. 
Yet, this is the most requested scenario.

## Alternatives
[alternatives]: #alternatives

- omit `localinits` flag when method is declared in `unsafe` context. 
That could cause silent and dangerous behavior change from deterministic to nonditerministic in a case of `stackalloc` .

- omit `localinits` flag always.
Even worse than above.

- omit `localinits` flag unless `stackalloc` is used in the method body.
Does not address the most requested scenario and may turn code unverifiable with no option to revert that back.

## Unresolved questions
[unresolved]: #unresolved-questions

- Should the attribute be actually emitted to metadata? 

## Design meetings

None yet. 