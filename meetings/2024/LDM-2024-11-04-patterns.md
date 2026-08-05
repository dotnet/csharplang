Levi [pointed out](https://github.com/dotnet/roslyn/issues/75506) that many users misunderstand the precedence of `not` and `or` pattern combinators and write faulty code as a result.
He found many instances on GitHub and internally.

For example: `is not 42 or 43` or `is not null or Type`.  
In such cases, the `or` pattern is superfluous.  
The user most likely intended to write `is not (42 or 43)` or `is not (null or Type)` instead.

We considered two possible solutions:  
1. a syntax analyzer that would require parens on either the `not` or around the `or`, so that precedence is explicit
2. a compiler diagnostic when we can detect that the `or` was redundant in a `not ... or ...` pattern (the goal would be to catch the most common cases, not necessarily 100%)

I'm pursuing the latter (PR).  
The diagnostic is implemented as a regular warning, so it introduces a compat break.  
For example: `is not 42 or 43 // warning: the pattern 43 is redundant. Did you mean to parenthesize the `or` pattern?`

Note: we're okay with catching specific/known bad patterns, and do not require to catch every possible bad pattern.

Few questions:  
1. confirm we prefer this compiler-based detection over an analyzer
2. confirm that a regular warning (ie. compat break) is preferrable to a warning-wave warning or a LangVer-gated warning


Note: here are some examples where the `or` pattern is not redundant, which Levi saw in the wild and internally:  
`is not BaseType or DerivedType`  
`is not bool or true`  
`is not object or { Prop: condition }`  
`is not { Prop1: condition } or { Prop2: condition }`  
`is not Type or Type { Prop: condition }`  
