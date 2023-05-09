# `ref readonly` parameters

## Summary
[summary]: #summary

Allow parameter declaration-site modifier `ref readonly` and change call-site rules as follows:

| Call-site annotation | `ref` parameter | `ref readonly` parameter | `in` parameter | `out` parameter |
|----------------------|-----------------|--------------------------|----------------|-----------------|
| `ref`                | Allowed         | **Allowed**              | **Warning**    | Error           |
| `in`                 | Error           | **Allowed**              | Allowed        | Error           |
| `out`                | Error           | **Error**                | Error          | Allowed         |
| No annotation        | Error           | **Warning**              | Allowed        | Error           |

Note that there is one change to the existing rules: `in` parameter with `ref` call-site annotation produces a warning instead of an error.

| Value kind | `ref` parameter | `ref readonly` parameter | `in` parameter | `out` parameter |
|------------|-----------------|--------------------------|----------------|-----------------|
| rvalue     | Error           | **Warning**              | Allowed        | Error           |
| lvalue     | Allowed         | **Allowed**              | Allowed        | Allowed         |

## Motivation
[motivation]: #motivation

C# 7.2 [introduced `in` parameters](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/proposals/csharp-7.2/readonly-ref.md#solution-in-parameters) as a way to pass readonly references.
`in` parameters allow both lvalues and rvalues and can be used without any annotation at the callsite.
However, APIs which capture or return references from their parameters would like to disallow rvalues and also enforce some indication at the callsite that a reference is being captured.
`ref readonly` parameters are ideal in such cases as they allow only lvalues and warn if used without any annotation.

Furthermore, there are APIs that need only read-only references but use
- `ref` parameters since they were introduced before `in` became available and changing to `in` would be a source and binary breaking change, e.g., `QueryInterface`, or
- `in` parameters to accept readonly references even though passing rvalues to them doesn't really make sense, e.g., `ReadOnlySpan<T>..ctor(in T value)`, or
- `ref` parameters to disallow rvalues even though they don't mutate the passed reference, e.g., `Unsafe.IsNullRef`.

These APIs could migrate to `ref readonly` parameters without breaking users.
For details on binary compatibility, see the proposed [metadata encoding](#metadata).
Specifically, changing
- `ref` → `ref readonly` would only be a binary breaking change for virtual methods,
- `ref` → `in` would also be a binary breaking change for virtual methods, but not a source breaking change (because the rules change to only warn for `ref` arguments passed to `in` parameters),
- `in` → `ref readonly` would not be a breaking change (but no callsite annotation would result in a warning).

In the opposite direction, changing
- `ref readonly` → `ref` would be potentially a source breaking change (unless only `ref` callsite annotation was used), and a binary breaking change for virtual methods,
- `ref readonly` → `in` would not be a breaking change (but `ref` callsite annotation would result in a warning).

## Detailed design
[design]: #detailed-design

No changes in grammar are necessary.
Specification will be extended to allow `ref readonly` modifiers for parameters with the same rules as specified for `in` parameters in [their proposal](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/proposals/csharp-7.2/readonly-ref.md), except where explicitly changed in this proposal.

### Metadata encoding
[metadata]: #metadata-encoding

As a reminder,
- `ref` parameters are emitted as plain byref types (`T&` in IL),
- `in` parameters are like `ref` plus they are annotated with `System.Runtime.CompilerServices.IsReadOnlyAttribute`.
  In C# 7.3 and later, they are also emitted with `[in]` and if virtual, `modreq(System.Runtime.InteropServices.InAttribute)`.

`ref readonly` parameters will be emitted as `[in] T&`, plus annotated with the following attribute:

```cs
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class RequiresLocationAttribute : Attribute
    {
    }
}
```

Furthermore, if virtual, they will be emitted with `modreq(System.Runtime.InteropServices.InAttribute)` to ensure binary compatibility with `in` parameters.
Note that unlike `in` parameters, no `[IsReadOnly]` is emitted for `ref readonly` parameters to avoid increasing metadata size.

The `RequiresLocationAttribute` will be matched by namespace-qualified name and synthesized by the compiler if not already included in the compilation.

## Alternatives
[alternatives]: #alternatives

API authors could annotate `in` parameters designed to accept only lvalues with a custom attribute and provide an analyzer to flag incorrect usages.
This would not allow API authors to change signatures of existing APIs that opted to use `ref` parameters to disallow rvalues.
Callers of such APIs would still need to perform extra work to get a `ref` if they have access only to a `ref readonly` variable.
Changing these APIs from `ref` to `[RequiresLocation] in` would be a source breaking change (and in case of virtual methods, also a binary breaking change).

Instead of allowing the modifier `ref readonly`, the compiler could recognize when a special attribute (like `[RequiresLocation]`) is applied to a parameter.
This was discussed in [LDM 2022-04-25](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-04-25.md#ref-readonly-method-parameters), deciding this is a language feature, not an analyzer, so it should look like one.

Passing lvalues without any modifiers to a `ref readonly` parameters could be permitted without any warnings, similarly to C++'s implicit byref parameters.
This was discussed in [LDM 2022-05-11](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-05-11.md#ref-readonly-method-parameters), noting that the primary motivation for `ref readonly` parameters are APIs which capture or return references from these parameters, so marker of some kind is a good thing.

## Unresolved questions
[unresolved]: #unresolved-questions

- Allow default values for `ref readonly` parameters? They are allowed for `in` parameters, but `ref readonly` parameters shouldn't be used for rvalues (that's normally a warning).
- Disallow temps for both `in` and `ref readonly` parameters? See https://github.com/dotnet/roslyn/pull/67955#discussion_r1178138561.

## Design meetings

- [LDM 2022-04-25](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-04-25.md#ref-readonly-method-parameters): feature accepted
- [LDM 2022-05-09](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-05-09.md#ref-readonly-parameters): discussion split into three parts
- [LDM 2022-05-11](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-05-11.md#ref-readonly-method-parameters): allowed `ref` and no callsite annotation for `ref readonly` parameters
