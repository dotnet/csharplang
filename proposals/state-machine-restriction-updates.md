# State machine restriction updates

## Summary
[summary]: #summary

- Allow `ref`/`ref struct` locals and `unsafe` blocks in iterators and async methods
  provided they are used in code blocks without any `yield` or `await`.
- Warn about `yield` inside `lock`.

## Motivation
[motivation]: #motivation

It is not necessary to disallow `ref`/`ref struct` locals and `unsafe` blocks in async/iterator methods
if they are not used across `yield` or `await`, because they do not need to be hoisted.

```cs
async void M()
{
    await ...;
    {
        ref int x = ...; // error previously, proposed to be allowed
        // await ...; // still disallowed if await is here
        x.ToString();
    }
    await ...;
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

> It is a compile-time error for an iterator block to contain an unsafe context ([§23.2][unsafe-contexts])
> **unless the unsafe context does not contain any iterator blocks**.
> An iterator block always defines a safe context, even when its declaration is nested in an unsafe context.

[§13.6.2.4 Ref local variable declarations][ref-local]:

> It is a compile-time error to declare a ref local variable, or a variable of a `ref struct` type,
> within a method declared with the *method_modifier* `async`, or within an iterator ([§15.14][iterators])
> **unless the variable is declared and used only in a block ([§13.3.1][blocks-general])
> which does not have any `await` or `yield` statements inside itself and inside any of its nested blocks
> (excluding declarations like anonymous methods and local functions)**.

[§13.13 The lock statement][lock-statement]:

> [...]
> 
> **A warning is reported (as part of the next warning wave) when a `yield` statement
> ([§13.15][yield-statement]) is used inside the body of a `lock` statement.**

Note that no change in the spec is needed to allow `unsafe` blocks which do not contain `await`s in async methods,
because the spec has never disallowed `unsafe` blocks in async methods.
However, the spec should have always disallowed `await` inside `unsafe` blocks
(it already disallows `yield` in `unsafe` in [§13.3.1][blocks-general] as cited above), for example:

[§15.15.1 Async Functions > General][async-funcs-general]:

> It is a compile-time error for the formal parameter list of an async function to specify
> any `in`, `out`, or `ref` parameters, or any parameter of a `ref struct` type.
>
> **It is a compile-time error for an unsafe context ([§23.2][unsafe-contexts]) to contain `await`.**

Note that more constructs can work thanks to `ref` allowed inside blocks without `await` and `yield` in async/iterator methods
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
        {
            using (new R()) { } // allowed under this proposal
            foreach (var x in new C()) { } // allowed under this proposal
            foreach (ref int x in new int[0]) { } // allowed under this proposal
            lock (new System.Threading.Lock()) { } // allowed under this proposal
        }
        await Task.Yield();
    }
}
```

## Alternatives
[alternatives]: #alternatives

- `ref`/`ref struct` locals could be allowed even in blocks that contain `await`/`yield`
  provided the locals are not declared and used across `await`/`yield`:

  ```cs
  // error always since `x` is declared/used both before and after `await`
  {
      ref int x = ...;
      await ...;
      x.ToString();
  }
  // error as proposed (`await` in the same block) but alternatively could be allowed
  // (`x` does not need to be hoisted as it is not used after `await`)
  {
      ref int x = ...;
      x.ToString();
      await ...;
  }
  ```

[blocks-general]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1331-general
[ref-local]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#13624-ref-local-variable-declarations
[lock-statement]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1313-the-lock-statement
[using-statement]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1314-the-using-statement
[yield-statement]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/statements.md#1315-the-yield-statement
[iterators]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/classes.md#1514-iterators
[async-funcs-general]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/classes.md#15151-general
[unsafe-contexts]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/unsafe-code.md#232-unsafe-contexts
