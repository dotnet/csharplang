# State machine restriction updates

## Summary
[summary]: #summary

- Allow `ref`/`ref struct` locals and `unsafe` blocks in iterators and async methods
  provided they are used in code segments without any `yield` or `await`.
- Warn about `yield` inside `lock`.
- Consolidate spec: disallow pointers to hoisted variables
  (pointers to captured variables are already disallowed).

## Motivation
[motivation]: #motivation

It is not necessary to disallow `ref`/`ref struct` locals and `unsafe` blocks in async/iterator methods
if they are not used across `yield` or `await`, because they do not need to be hoisted.

```cs
async void M()
{
    await ...;
    ref int x = ...; // error previously, proposed to be allowed
    x.ToString();
    await ...;
    // x.ToString(); // still error
}
```

On the other hand, having `yield` inside a `lock` means the caller also holds the lock while iterating which might lead to unexpected behavior.
This is even more problematic in async iterators where the caller can `await` between iterations, but `await` is not allowed in `lock`.
See also https://github.com/dotnet/roslyn/issues/72443.

```cs
lock (this)
{
    yield return 1; // warning proposed
}
```

## Detailed design
[design]: #detailed-design

[§13.3.1 Blocks > General][blocks-general]:

> ~~It is a compile-time error for an iterator block to contain an unsafe context ([§23.2][unsafe-contexts]).~~
> An iterator block always defines a safe context, even when its declaration is nested in an unsafe context.

[§13.6.2.4 Ref local variable declarations][ref-local]:

> ~~It is a compile-time error to declare a ref local variable, or a variable of a `ref struct` type,
> within a method declared with the *method_modifier* `async`, or within an iterator ([§15.14][iterators]).~~
> **It is a compile-time error to declare and use a ref local variable, or a variable of a `ref struct` type
> across `await` or `yield` statements.**

[§13.13 The lock statement][lock-statement]:

> [...]
> 
> **A warning is reported (as part of the next warning wave) when a `yield` statement
> ([§13.15][yield-statement]) is used inside the body of a `lock` statement.**

No change in the spec is needed to allow `unsafe` blocks which do not contain `await`s in async methods,
because the spec has never disallowed `unsafe` blocks in async methods.
However, the spec should have always disallowed `await` inside `unsafe` blocks
(it had already disallowed `yield` in `unsafe` in [§13.3.1][blocks-general] as cited above), for example:

[§15.15.1 Async Functions > General][async-funcs-general]:

> It is a compile-time error for the formal parameter list of an async function to specify
> any `in`, `out`, or `ref` parameters, or any parameter of a `ref struct` type.
>
> **It is a compile-time error for an unsafe context ([§23.2][unsafe-contexts]) to contain `await` or `yield`.**

Furthermore, variables inside async or iterator methods should not be "fixed" but rather "moveable"
if they need to be hoisted to fields of the state machine (similarly to captured variables).
Note that this is a pre-existing bug in the spec independent of the rest of the proposal
because `unsafe` blocks inside `async` methods were always allowed.

[§23.4 Fixed and moveable variables][fixed-vars]:

> In precise terms, a fixed variable is one of the following:
>
> - A variable resulting from a *simple_name* ([§12.8.4][simple-names]) that refers to a local variable, value parameter, or parameter array,
> unless the variable is captured by an anonymous function ([§12.19.6.2][captured-vars]) **or a local function ([§13.6.4][local-funcs])
> or the variable needs to be hoisted as part of an async ([§15.15][async-funcs]) or an iterator ([§15.14][iterators]) method**.
> - [...]

Note that more constructs can work thanks to `ref` allowed inside segments without `await` and `yield` in async/iterator methods
even though no spec change is needed specifically for them as it all falls out from the aforementioned spec changes:

```cs
using System.Threading.Tasks;

ref struct R
{
    public int Current => 0;
    public bool MoveNext() => false;
    public void Dispose() { }
}
class C
{
    public R GetEnumerator() => new R();
    async void M()
    {
        await Task.Yield();
        using (new R()) { } // allowed under this proposal
        foreach (var x in new C()) { } // allowed under this proposal
        foreach (ref int x in new int[0]) { } // allowed under this proposal
        lock (new System.Threading.Lock()) { } // allowed under this proposal
        await Task.Yield();
    }
}
```

## Alternatives
[alternatives]: #alternatives

- `ref`/`ref struct` locals could be allowed only in blocks ([§13.3.1][blocks-general])
  which do not contain `await`/`yield`:

  ```cs
  // error always since `x` is declared/used both before and after `await`
  {
      ref int x = ...;
      await Task.Yield();
      x.ToString();
  }
  // allowed as proposed (`x` does not need to be hoisted as it is not used after `await`)
  // but alternatively could be an error (`await` in the same block)
  {
      ref int x = ...;
      x.ToString();
      await Task.Yield();
  }
  ```

- It could be possible to use `fixed` to get the address of a hoisted or captured variable
  although the fact that those are fields is an implementation detail
  so in other implementations it might not be possible to use `fixed` on them.
  Note that we only propose to consider also hoisted variables as "moveable",
  but captured variables were already "moveable" and `fixed` was not allowed for them.

- We could allow `await`/`yield` inside `unsafe` except inside `fixed` statements (compiler cannot pin variables across method boundaries).
  That might result in some unexpected behavior, for example around `stackalloc` as described below.
  - We could disallow the unsafe variant of `stackalloc` in async/iterator methods,
    because the stack-allocated buffer does not live across `await`/`yield` statements.
    It does not feel necessary because unsafe code by design does not prevent "use after free".
    Note that we could also allow unsafe `stackalloc` provided it is not used across `await`/`yield`, but
    that might be difficult to analyze (the resulting pointer can be passed around in any pointer variable).
    Or we could require it being `fixed` in async/iterator methods. That would *discourage* using it across `await`/`yield`
    but would not match the semantics of `fixed` because the `stackalloc` expression is not a moveable value.
    (Note that it would not be *impossible* to use the `stackalloc` result across `await`/`yield` similarly as
    you can save any `fixed` pointer today into another pointer variable and use it outside the `fixed` block.)

[simple-names]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/expressions.md#1284-simple-names
[captured-vars]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/expressions.md#121962-captured-outer-variables
[blocks-general]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1331-general
[ref-local]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#13624-ref-local-variable-declarations
[local-funcs]: https://github.com/dotnet/csharpstandard/blob/d11d5a1a752bff9179f8207e86d63d12782c31ff/standard/statements.md#1364-local-function-declarations
[lock-statement]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1313-the-lock-statement
[using-statement]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1314-the-using-statement
[yield-statement]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1315-the-yield-statement
[iterators]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/classes.md#1514-iterators
[async-funcs]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/classes.md#1515-async-functions
[async-funcs-general]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/classes.md#15151-general
[unsafe-contexts]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/unsafe-code.md#232-unsafe-contexts
[fixed-vars]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/unsafe-code.md#234-fixed-and-moveable-variables
