# Native lock

## Summary
[summary]: #summary

Special-case how `System.Threading.Lock` interacts with the `lock` keyword (calling its `EnterLockScope` method under the hood).
Add static analysis warnings to prevent accidental misuse of the type where possible.

## Motivation
[motivation]: #motivation

.NET 9 is introducing a new [`System.Threading.Lock` type](https://github.com/dotnet/runtime/issues/34812)
as a better alternative to existing monitor-based locking.
The presence of the `lock` keyword in C# might lead developers to think they can use it with this new type.
Doing so wouldn't lock according to the semantics of this type but would instead treat it as any other object and would use monitor-based locking.

## Detailed design
[design]: #detailed-design

Semantics of the lock statement ([§13.13](https://github.com/dotnet/csharpstandard/blob/9af5bdaa7af535f34fbb7923e5406e01db8489f7/standard/statements.md#1313-the-lock-statement))
are changed to special-case the `System.Threading.Lock` type:

> A `lock` statement of the form `lock (x) { ... }`
>
> 1. **where `x` is an expression of type `System.Threading.Lock`, is precisely equivalent to:**
>    ```cs
>    using (x.EnterLockScope())
>    {
>        ...
>    }
>    ```
>    **and `System.Threading.Lock` must have the following shape:**
>    ```cs
>    namespace System.Threading
>    {
>        class Lock
>        {
>            public Scope EnterLockScope();
>    
>            public ref struct Scope
>            {
>                public void Dispose();
>            }
>        }
>    }
>    ```
> 2. where `x` is an expression of a *reference_type*, is precisely equivalent to: [...]

Additionally, new warnings are added to implicit reference conversions ([§10.2.8](https://github.com/dotnet/csharpstandard/blob/9af5bdaa7af535f34fbb7923e5406e01db8489f7/standard/conversions.md#1028-implicit-reference-conversions))
when upcasting the `System.Threading.Lock` type:

> The implicit reference conversions are:
>
> - From any *reference_type* to `object` and `dynamic`.
>   - **A warning is reported when the *reference_type* is known to be `System.Threading.Lock`.**
> - From any *class_type* `S` to any *class_type* `T`, provided `S` is derived from `T`.
>   - **A warning is reported when `S` is known to be `System.Threading.Lock`.**
> - From any *class_type* `S` to any *interface_type* `T`, provided `S` implements `T`.
>   - **A warning is reported when `S` is known to be `System.Threading.Lock`.**
> - [...]

```cs
object l = new System.Threading.Lock(); // warning
lock (l) { } // monitor-based locking is used here
```

Note that this warning occurs even for equivalent explicit conversions.
To escape out of the warning and force use of monitor-based locking, one can use
- the usual warning suppression means (`#pragma warning disable`),
- `Monitor` APIs directly,
- indirect casting like `object AsObject<T>(T l) => (object)l;`.

## Alternatives
[alternatives]: #alternatives

- Support a general pattern that other types can also use to interact with the `lock` keyword.
  This is a future work that might be implemented when `ref struct`s can participate in generics.
  Discussed in [LDM 2023-12-04](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-12-04.md#lock-statement-pattern).

- To avoid ambiguity between the existing monitor-based locking and the new `Lock` (or pattern in the future), we could:
  - Introduce a new syntax instead of reusing the existing `lock` statement.
  - Require the new lock types to be `struct`s (since the existing `lock` disallows value types).
    There could be problems with default constructors and copying if the structs have lazy initialization.

- The codegen could be hardened against thread aborts (which are themselves obsoleted).

## Unresolved questions
[unresolved]: #unresolved-questions

- Do we want to include static analysis to prevent usage of `System.Threading.Lock` in `using`s with `await`?
  We could emit either an error or a warning for code like `using (lockVar.EnterLockScope()) { await ... }`.
  (We would also likely need to consider for this all lock types matching the general pattern if implemented in the future.
  Although there might need to be an opt-out mechanism as some lock types might be allowed to be used with `await`.)
  Alternatively, this could be an analyzer shipped as part of the runtime.

- Should the `Lock` type have an `Obsolete` attribute to prevent people with older compilers from accidentally misusing it?
  (The attribute would be recognized and ignored by compilers supporting the feature.)

## Design meetings

- [LDM 2023-05-01](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-05-01.md#lock-statement-improvements): initial decision to support a `lock` pattern
- [LDM 2023-10-16](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-16.md#lock-statement-pattern): triaged into the working set for .NET 9
- [LDM 2023-12-04](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-12-04.md#lock-statement-pattern): rejected the general pattern, accepted only special-casing the `Lock` type + adding static analysis warnings
