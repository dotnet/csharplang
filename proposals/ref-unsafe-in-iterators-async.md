# Allow ref and unsafe in iterators and async

## Summary
[summary]: #summary

Unify behavior between iterators and async methods. Specifically:

- Allow `ref`/`ref struct` locals and `unsafe` blocks in iterators and async methods
  provided they are used in code segments without any `yield` or `await`.
- Warn about `yield` inside `lock`.

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

The following changes are tied to LangVersion, i.e., C# 12 and lower will continue to disallow
ref-like locals and `unsafe` blocks in async methods and iterators,
and C# 13 will lift these restrictions as described below.
However, spec clarifications which match the existing Roslyn implementation should hold across all LangVersions.

[§13.3.1 Blocks > General][blocks-general]:

> A *block* that contains one or more `yield` statements ([§13.15][yield-statement]) is called an iterator block,
> **even if those `yield` statements are contained only indirectly in nested blocks (excluding nested lambdas and local functions).**
>
> [...]
>
> ~~It is a compile-time error for an iterator block to contain an unsafe context ([§23.2][unsafe-contexts]).~~
> An iterator ~~block~~ **declaration ([§15.14][iterators])** always defines a safe context
> **(its signature included),** even when the declaration is nested in an unsafe context,
> **unless the iterator declaration is marked with the `unsafe` modifier ([§23.2][unsafe-contexts]).**

Note that this also means that a property or an indexer defines a safe context including its `set` accessor provided:
- it is an iterator (because its `get` accessor is implemented via an iterator block) and
- it does not have the `unsafe` modifier.

For example:

```cs
using System.Collections.Generic;
using System.Threading.Tasks;

class A : System.Attribute { }
unsafe class C
{ // unsafe context
    [/* safe context */ A]
    IEnumerable<int> M1(
        /* safe context */)
    { // safe context
        yield return 1;
    }
    IEnumerable<int> M2()
    { // safe context
        unsafe
        { // unsafe context
            { // unsafe context
                yield return 2; // error: `yield return` in unsafe context
            }
        }
    }
    [/* unsafe context */ A]
    unsafe IEnumerable<int> M3(
        /* unsafe context */)
    { // unsafe context
        yield return 3; // error: `yield return` in unsafe context
    }
    IEnumerable<int> this[
        /* safe context */ string x]
    { // safe context
        get
        { // safe context
            yield return 1;
        }
        set { /* safe context */ }
    }
    unsafe IEnumerable<int> this[
        /* unsafe context */ int x]
    { // unsafe context
        get
        { // unsafe context
            yield return 1; // error: `yield return` in unsafe context
        }
        set { /* unsafe context */ }
    }
    IEnumerable<int> M4()
    {
        yield return 4;
        var lam = async () =>
        { // safe context
          // note: in Roslyn, this is an unsafe context in LangVersion 12 and lower
            await Task.Yield();
        };
        async void local()
        { // safe context
          // note: in Roslyn, this is an unsafe context in LangVersion 12 and lower
            await Task.Yield();
        }
        local();
    }
}
```

Note that `yield return`s in unsafe contexts are compile-time errors (see below), so the only way
to have a successfully compiling unsafe iterator method is if it contains only `yield break`s.

[§13.6.2.4 Ref local variable declarations][ref-local]:

> ~~It is a compile-time error to declare a ref local variable, or a variable of a `ref struct` type,
> within a method declared with the *method_modifier* `async`, or within an iterator ([§15.14][iterators]).~~
> **It is a compile-time error to declare and use (even implicitly in compiler-synthesized code)
> a ref local variable, or a variable of a `ref struct` type across `await` expressions or `yield return` statements.
> More precisely, the error is driven by the following mechanism:
> after an `await` expression ([§12.9.8][await-expressions]) or a `yield return` statement ([§13.15][yield-statement]),
> all ref local variables and variables of a `ref struct` type in scope
> are considered definitely unassigned ([§9.4][definite-assignment]).**

Note that this error is not downgraded to a warning in `unsafe` contexts like [some other ref safety errors][ref-safety-unsafe-warnings].
That is because these ref-like locals cannot be manipulated in `unsafe` contexts without relying on implementation details of how the state machine rewrite works,
hence this error falls outside the boundaries of what we want to downgrade to warnings in `unsafe` contexts.

[§13.13 The lock statement][lock-statement]:

> [...]
> 
> **A warning is reported (as part of the next warning wave) when a `yield return` statement
> ([§13.15][yield-statement]) is used inside the body of a `lock` statement.**

Note that [the new `Lock`-object-based `lock`][lock-object] reports compile-time errors for `yield return`s in its body,
because such `lock` statement is equivalent to a `using` on a `ref struct` which disallows `yield return`s in its body.

[§15.14.1 Iterators > General][iterators]:

> When a function member is implemented using an iterator block,
> it is a compile-time error for the formal parameter list of the function member to specify any
> `in`, `ref readonly`, `out`, or `ref` parameters, or an parameter of a `ref struct` type **or a pointer type**.

No change in the spec is needed to allow `unsafe` blocks which do not contain `await`s in async methods,
because the spec has never disallowed `unsafe` blocks in async methods.
However, the spec should have always disallowed `await` inside `unsafe` blocks
(it had already disallowed `yield` in `unsafe` in [§13.3.1][blocks-general] as cited above),
so we propose the following change to the spec:

[§15.15.1 Async Functions > General][async-funcs-general]:

> It is a compile-time error for the formal parameter list of an async function to specify
> any `in`, `out`, or `ref` parameters, or any parameter of a `ref struct` type.
>
> **It is a compile-time error for an unsafe context ([§23.2][unsafe-contexts]) to contain
> an `await` expression ([§12.9.8][await-expressions]) or a `yield return` statement ([§13.15][yield-statement]).**

[§23.6.5 The address-of operator][address-of]:

> **A compile-time error will be reported for taking an address of a local or a parameter in an iterator.**

Currently, taking an address of a local or a parameter in an async method is [a warning in C# 12 warning wave][async-pointer].

---

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

- `yield` inside `lock` could be an error (like `await` inside `lock` is) but that would be a breaking change.

- Variables inside async or iterator methods should not be "fixed" but rather "moveable"
  if they need to be hoisted to fields of the state machine (similarly to captured variables).
  Note that this is a pre-existing bug in the spec independent of the rest of the proposal
  because `unsafe` blocks inside `async` methods were always allowed.
  There is currently [a warning for this in C# 12 warning wave][async-pointer]
  and making it an error would be a breaking change.

  [§23.4 Fixed and moveable variables][fixed-vars]:
  
  > In precise terms, a fixed variable is one of the following:
  >
  > - A variable resulting from a *simple_name* ([§12.8.4][simple-names]) that refers to a local variable, value parameter, or parameter array,
  > unless the variable is captured by an anonymous function ([§12.19.6.2][captured-vars]) **or a local function ([§13.6.4][local-funcs])
  > or the variable needs to be hoisted as part of an async ([§15.15][async-funcs]) or an iterator ([§15.14][iterators]) method**.
  > - [...]

  - Currently, we have an existing warning in C# 12 warning wave for address-of in async methods and a proposed error for address-of in iterators
    reported for LangVersion 13+ (does not need to be reported in earlier versions because it was impossible to use unsafe code in iterators).
    We could relax both of these to apply only to variables that are actually hoisted, not all locals and parameters. 

  - It could be possible to use `fixed` to get the address of a hoisted or captured variable
    although the fact that those are fields is an implementation detail
    so in other implementations it might not be possible to use `fixed` on them.
    Note that we only propose to consider also hoisted variables as "moveable",
    but captured variables were already "moveable" and `fixed` was not allowed for them.

- We could allow `await`/`yield` inside `unsafe` except inside `fixed` statements (compiler cannot pin variables across method boundaries).
  That might result in some unexpected behavior, for example around `stackalloc` as described in the nested bullet point below.
  Otherwise, hoisting pointers is supported even today in some scenarios (there is an example below related to pointers as arguments),
  so there should be no other limitations in allowing this.

  - We could disallow the unsafe variant of `stackalloc` in async/iterator methods,
    because the stack-allocated buffer does not live across `await`/`yield` statements.
    It does not feel necessary because unsafe code by design does not prevent "use after free".
    Note that we could also allow unsafe `stackalloc` provided it is not used across `await`/`yield`, but
    that might be difficult to analyze (the resulting pointer can be passed around in any pointer variable).
    Or we could require it being `fixed` in async/iterator methods. That would *discourage* using it across `await`/`yield`
    but would not match the semantics of `fixed` because the `stackalloc` expression is not a moveable value.
    (Note that it would not be *impossible* to use the `stackalloc` result across `await`/`yield` similarly as
    you can save any `fixed` pointer today into another pointer variable and use it outside the `fixed` block.)

  - Note that allowing this would make the following scenario more interesting (allowing pointer arguments of iterator/async methods).

- Iterator and async methods could be allowed to have pointer parameters.
  They would need to be hoisted, but that should not be a problem as
  hoisting pointers is supported even today, for example:

  ```cs
  unsafe public void* M(void* p)
  {
      var d = () => p;
      return d();
  }
  ```

  However, such iterator/async methods would not be very useful since they could not contain `await`/`yield return` anyway
  (as it is disallowed to have `await`/`yield return` in an unsafe context).

- The proposal currently keeps (and extends/clarifies) the pre-existing spec that
  iterator methods begin a safe context even if they are in an unsafe context.
  For example, an iterator method is not an unsafe context even if it is defined in a class which has the `unsafe` modifier.
  Without this, iterators inside unsafe classes would not be very useful as they could not contain `yield return` statements.
  On the other hand, this adds complexity and if iterators could not be defined in `unsafe` class declarations,
  a workaround would be to define them in a separate `partial` class declaration without `unsafe` modifier, for example.

[definite-assignment]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/variables.md#94-definite-assignment
[simple-names]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/expressions.md#1284-simple-names
[await-expressions]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/expressions.md#1298-await-expressions
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
[address-of]: https://github.com/dotnet/csharpstandard/blob/ee38c3fa94375cdac119c9462b604d3a02a5fcd2/standard/unsafe-code.md#2365-the-address-of-operator
[lock-object]: ./lock-object.md
[ref-safety-unsafe-warnings]: https://github.com/dotnet/csharplang/issues/6476
[async-pointer]: https://github.com/dotnet/roslyn/pull/66915
