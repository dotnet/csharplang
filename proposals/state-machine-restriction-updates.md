# State machine restriction updates

## Summary
[summary]: #summary

- Allow `using (ref struct)` and `unsafe` blocks in iterators and async methods provided there is no `yield` or `await` inside the blocks.
- Warn about `yield` inside `lock`.

## Motivation
[motivation]: #motivation

It is not necessary to disallow `using` blocks with `ref struct` resources and `unsafe` blocks in async/iterator methods
if there is no `yield` or `await` inside the blocks, because nothing from the blocks needs to be hoisted.

On the other hand, having `yield` inside a `lock` means the caller also holds the lock while iterating which might lead to unexpected behavior.
This is even more problematic in async iterators where the caller can `await` between iterations, but `await` is not allowed in `lock`.
See also https://github.com/dotnet/roslyn/issues/72443.

## Detailed design
[design]: #detailed-design

[§13.3.1 Blocks > General][blocks-general]:

> It is a compile-time error for an iterator block to contain an unsafe context ([§23.2][unsafe-contexts])
> **unless the unsafe context does not contain any iterator blocks**.
> An iterator block always defines a safe context, even when its declaration is nested in an unsafe context.

[§13.6.2.4 Ref local variable declarations][ref-local]:

> It is a compile-time error to declare a ref local variable, or a variable of a `ref struct` type,
> within a method declared with the *method_modifier* `async`, or within an iterator ([§15.14][iterators])
> **unless the variable is the resource of a `using` statement ([§13.14][using-statement])
> which does not contain any `await` or `yield` inside**.

[§13.13 The lock statement][lock-statement]:

> [...]
> 
> **A warning is reported (as part of the next warning wave) when a `yield` statement
> ([§13.15][yield-statement]) is used inside a `lock` statement.**

Note that no change in the spec is needed to allow `unsafe` blocks which do not contain `await`s in async methods,
because the spec has never disallowed `unsafe` blocks in async methods.
However, the spec should have always disallowed `await` inside `unsafe` blocks
(it already disallows `yield` in `unsafe` in [§13.3.1][blocks-general] as cited above), for example:

[§15.15.1 Async Functions > General][async-funcs-general]:

> It is a compile-time error for the formal parameter list of an async function to specify
> any `in`, `out`, or `ref` parameters, or any parameter of a `ref struct` type.
>
> **It is a compile-time error for an unsafe context ([§23.2][unsafe-contexts]) to contain `await`.**

[blocks-general]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1331-general
[ref-local]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#13624-ref-local-variable-declarations
[lock-statement]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1313-the-lock-statement
[using-statement]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1314-the-using-statement
[yield-statement]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1315-the-yield-statement
[iterators]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/classes.md#1514-iterators
[async-funcs-general]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/classes.md#15151-general
[unsafe-contexts]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/unsafe-code.md#232-unsafe-contexts
