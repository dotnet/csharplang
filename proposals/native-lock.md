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
>        public sealed class Lock
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

Note that the shape might not be fully checked (e.g., there will be no errors nor warnings if the `Lock` type is not `sealed`),
but the feature might not work as expected (e.g., there will be no warnings when converting `Lock` to a derived type,
since the feature assumes there are no derived types).

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

- We could warn also when `Lock` is passed as a type parameter, because locking on a type parameter always uses monitor-based locking:

  ```cs
  M(new Lock()); // could warn here

  void M<T>(T x) // (specifying `where T : Lock` makes no difference)
  {
      lock (x) { } // because this uses Monitor
  }
  ```

  However, that would cause warnings when storing `Lock`s in a list which is undesirable:

  ```cs
  List<Lock> list = new();
  list.Add(new Lock()); // would warn here
  ```

- We could include static analysis to prevent usage of `System.Threading.Lock` in `using`s with `await`s.
  I.e., we could emit either an error or a warning for code like `using (lockVar.EnterLockScope()) { await ... }`.
  Currently, this is not needed since `Lock.Scope` is a `ref struct`, so that code is illegal anyway.
  However, if we ever allowed `ref struct`s in `async` methods or changed `Lock.Scope` to not be a `ref struct`, this analysis would become beneficial.
  (We would also likely need to consider for this all lock types matching the general pattern if implemented in the future.
  Although there might need to be an opt-out mechanism as some lock types might be allowed to be used with `await`.)
  Alternatively, this could be implemented as an analyzer shipped as part of the runtime.

- We could relax the restriction that value types cannot be `lock`ed
  - for the new `Lock` type (only needed if the API proposal changed it from `class` to `struct`),
  - for the general pattern where any type can participate when implemented in the future.

## Unresolved questions
[unresolved]: #unresolved-questions

- Can we allow the new `lock` statement in async methods?
  Since `await` is disallowed inside the `lock`, this would be safe.
  Currently, since `lock` is lowered to `using` with a `ref struct` as the resource, this results in a compile-time error.
  The workaround is to extract the `lock` into a separate non-async method.

## Design meetings

- [LDM 2023-05-01](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-05-01.md#lock-statement-improvements): initial decision to support a `lock` pattern
- [LDM 2023-10-16](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-10-16.md#lock-statement-pattern): triaged into the working set for .NET 9
- [LDM 2023-12-04](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-12-04.md#lock-statement-pattern): rejected the general pattern, accepted only special-casing the `Lock` type + adding static analysis warnings
