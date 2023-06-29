# `ref readonly` parameters

## Summary
[summary]: #summary

Allow parameter declaration-site modifier `ref readonly` and change callsite rules as follows:

| Callsite annotation  | `ref` parameter | `ref readonly` parameter | `in` parameter | `out` parameter |
|----------------------|-----------------|--------------------------|----------------|-----------------|
| `ref`                | Allowed         | **Allowed**              | **Warning**    | Error           |
| `in`                 | Error           | **Allowed**              | Allowed        | Error           |
| `out`                | Error           | **Error**                | Error          | Allowed         |
| No annotation        | Error           | **Warning**              | Allowed        | Error           |

(Note that there is one change to the existing rules: `in` parameter with `ref` callsite annotation produces a warning instead of an error.)

Change argument value rules as follows:

| Value kind | `ref` parameter | `ref readonly` parameter | `in` parameter | `out` parameter |
|------------|-----------------|--------------------------|----------------|-----------------|
| rvalue     | Error           | **Warning**              | Allowed        | Error           |
| lvalue     | Allowed         | **Allowed**              | Allowed        | Allowed         |

Where lvalue means a variable (i.e., a value with a location; does not have to be writable/assignable)
and rvalue means any kind of value.

## Motivation
[motivation]: #motivation

C# 7.2 [introduced `in` parameters](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/proposals/csharp-7.2/readonly-ref.md#solution-in-parameters) as a way to pass readonly references.
`in` parameters allow both lvalues and rvalues and can be used without any annotation at the callsite.
However, APIs which capture or return references from their parameters would like to disallow rvalues and also enforce some indication at the callsite that a reference is being captured.
`ref readonly` parameters are ideal in such cases as they warn if used with rvalues or without any annotation at the callsite.

Furthermore, there are APIs that need only read-only references but use
- `ref` parameters since they were introduced before `in` became available and changing to `in` would be a source and binary breaking change, e.g., `QueryInterface`, or
- `in` parameters to accept readonly references even though passing rvalues to them doesn't really make sense, e.g., `ReadOnlySpan<T>..ctor(in T value)`, or
- `ref` parameters to disallow rvalues even though they don't mutate the passed reference, e.g., `Unsafe.IsNullRef`.

These APIs could migrate to `ref readonly` parameters without breaking users.
For details on binary compatibility, see the proposed [metadata encoding][metadata].
Specifically, changing
- `ref` → `ref readonly` would only be a binary breaking change for virtual methods,
- `ref` → `in` would also be a binary breaking change for virtual methods, but not a source breaking change (because the rules change to only warn for `ref` arguments passed to `in` parameters),
- `in` → `ref readonly` would not be a breaking change (but no callsite annotation or rvalue would result in a warning),
  - note that this would be a source breaking change for users using older compiler versions (as they interpret `ref readonly` parameters as `ref` parameters, disallowing `in` or no annotation at the callsite) and new compiler versions with `LangVersion <= 11` (for consistency with older compiler versions, an error will be emitted that `ref readonly` parameters are not supported unless the corresponding arguments are passed with the `ref` modifier).

In the opposite direction, changing
- `ref readonly` → `ref` would be potentially a source breaking change (unless only `ref` callsite annotation was used and only readonly references used as arguments), and a binary breaking change for virtual methods,
- `ref readonly` → `in` would not be a breaking change (but `ref` callsite annotation would result in a warning).

## Detailed design
[design]: #detailed-design

No changes in grammar are necessary.
Specification will be extended to allow `ref readonly` modifiers for parameters with the same rules as specified for `in` parameters in [their proposal](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/proposals/csharp-7.2/readonly-ref.md), except where explicitly changed in this proposal.

Note that even though `ref` argument modifier is allowed for `ref readonly` parameters, nothing changes w.r.t. value kind checks, i.e.,
- `ref` can only be used with assignable values;
- to pass readonly references, one has to use the `in` argument modifier instead;
- to pass rvalues, one has to use no modifier (which results in a warning for `ref readonly` parameters as described in [the summary of this proposal][summary]).

### Interchangeability with `in`
[interchangeability]: #interchangeability-with-in

Members declared in a single type cannot differ in signature solely by `ref`/`out`/`in`/`ref readonly`.
For other purposes of signature matching (e.g., hiding or overriding), `ref readonly` can be interchanged with `in` modifier, but that results in a warning at the declaration site [[§7.6](https://github.com/dotnet/csharpstandard/blob/47912d4fdae2bb8c3750e6485bdc6509560ec6bf/standard/basic-concepts.md#76-signatures-and-overloading)].
Note that there is no change in overriding for `ref`/`in` and `ref readonly`/`ref` modifier pairs, they cannot be interchanged, because the signatures aren't binary compatible.
For consistency, the same is true for other signature matching purposes (e.g., hiding).

For the purpose of anonymous function [[§10.7](https://github.com/dotnet/csharpstandard/blob/47912d4fdae2bb8c3750e6485bdc6509560ec6bf/standard/conversions.md#107-anonymous-function-conversions)], method group [[§10.8](https://github.com/dotnet/csharpstandard/blob/47912d4fdae2bb8c3750e6485bdc6509560ec6bf/standard/conversions.md#108-method-group-conversions)], and [function pointer](https://github.com/dotnet/csharplang/blob/4b17ebb49654d21d4e96f415339c15c9f8a9ccde/proposals/csharp-9.0/function-pointers.md#function-pointer-conversions) conversions, these modifiers are considered compatible (but the conversion results in a warning):
- `ref readonly` can be interchanged with `in` or `ref` modifier,
- `in` can be interchanged with `ref` modifier.

### Overload resolution
[overload-resolution]: #overload-resolution

Overload resolution will allow mixing `ref`/`ref readonly`/`in` callsite annotations and parameter modifiers as denoted by the table in [the summary of this proposal][summary], i.e., all *allowed* and *warning* cases will be considered as possible candidates during overload resolution.
Specifically, there's a change in existing behavior where methods with `in` parameter will match calls with the corresponding argument marked as `ref`&mdash;this change will be gated on LangVersion.

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
Note that unlike `in` parameters, no `[IsReadOnly]` will be emitted for `ref readonly` parameters to avoid increasing metadata size and also to make older compiler versions interpret `ref readonly` parameters as `ref` parameters (and hence `ref` → `ref readonly` won't be a source breaking change even between different compiler versions).

The `RequiresLocationAttribute` will be matched by namespace-qualified name and synthesized by the compiler if not already included in the compilation.

Specifying the attribute in source will be an error, similarly to `ParamArrayAttribute`.

#### Function pointers
[funcptrs]: #function-pointers

In function pointers, `in` parameters are emitted with `modreq(System.Runtime.InteropServices.InAttribute)` (see [function pointers proposal](https://github.com/dotnet/csharplang/blob/0376b4cc500b1370da86d26be634c9acf9d60b71/proposals/csharp-9.0/function-pointers.md#metadata-representation-of-in-out-and-ref-readonly-parameters-and-return-types)).
`ref readonly` parameters will be emitted without that `modreq`, but instead with `modopt(System.Runtime.CompilerServices.RequiresLocationAttribute)`.
Older compiler versions will ignore the `modopt` and hence interpret `ref readonly` parameters as `ref` parameters (consistent with older compiler behavior for normal methods with `ref readonly` parameters as described above)
and new compiler versions aware of the `modopt` will use it to recognize `ref readonly` parameters to emit warnings during [conversions][interchangeability] and [invocations][overload-resolution].
For consistency with older compiler versions, new compiler versions with `LangVersion <= 11` will report errors that `ref readonly` parameters are not supported unless the corresponding arguments are passed with the `ref` modifier.

### Default parameter values
[default-params]: #default-parameter-values

Default parameter values are allowed for `ref readonly` parameters with a warning since they are equivalent to passing rvalues.

## Alternatives
[alternatives]: #alternatives

API authors could annotate `in` parameters designed to accept only lvalues with a custom attribute and provide an analyzer to flag incorrect usages.
This would not allow API authors to change signatures of existing APIs that opted to use `ref` parameters to disallow rvalues.
Callers of such APIs would still need to perform extra work to get a `ref` if they have access only to a `ref readonly` variable.
Changing these APIs from `ref` to `[RequiresLocation] in` would be a source breaking change (and in case of virtual methods, also a binary breaking change).

Instead of allowing the modifier `ref readonly`, the compiler could recognize when a special attribute (like `[RequiresLocation]`) is applied to a parameter.
This was discussed in [LDM 2022-04-25](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-04-25.md#ref-readonly-method-parameters), deciding this is a language feature, not an analyzer, so it should look like one.

Passing lvalues without any modifiers to `ref readonly` parameters could be permitted without any warnings, similarly to C++'s implicit byref parameters.
This was discussed in [LDM 2022-05-11](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-05-11.md#ref-readonly-method-parameters), noting that the primary motivation for `ref readonly` parameters are APIs which capture or return references from these parameters, so marker of some kind is a good thing.

Passing rvalue to a `ref readonly` could be an error, not a warning.
That was initially accepted in [LDM 2022-04-25](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-04-25.md#ref-readonly-method-parameters), but later e-mail discussions relaxed this because we would lose the ability to change existing APIs without breaking users.

`in` could be the "natural" callsite modifier for `ref readonly` parameters and using `ref` could result in warnings.
This would ensure a consistent code style and make it obvious at the callsite that the reference is readonly (unlike `ref`).
It was initially accepted in [LDM 2022-04-25](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-04-25.md#ref-readonly-method-parameters).
However, warnings could be a friction point for API authors to move from `ref` to `ref readonly`.
Also, `in` has been redefined as `ref readonly` + convenience features, hence this was rejected in [LDM 2022-05-11](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-05-11.md#ref-readonly-method-parameters).

### Pending LDM review
[to-review]: #pending-ldm-review

Inverse ordering of modifiers (`readonly ref` instead of `ref readonly`) could be allowed.
This would be inconsistent with how `readonly ref` returns and fields behave (inverse ordering is disallowed or means something different, respectively) and could clash with readonly parameters if implemented in the future.

Errors could be emitted instead of warnings when passing rvalues to `ref readonly` parameters or mismatching callsite annotations and parameter modifiers.
Similarly, special `modreq` could be used instead of an attribute to ensure `ref readonly` parameters are distinct from `in` parameters on the binary level.
This would provide stronger guarantees, so it would be good for new APIs, but prevent adoption in existing runtime APIs which cannot introduce breaking changes.

Overload resolution, overriding, and conversion could disallow interchangeability of `ref readonly` and `in` modifiers.

The overload resolution change for existing `in` parameters could be taken unconditionally (not considering LangVersion), but that would be a breaking change.

Default parameter values could be an error for `ref readonly` parameters.

Specifying the `RequiresLocationAttribute` in source could be allowed, similarly to `In` and `Out` attributes.

Function pointer `ref readonly` parameters could be emitted with different `modopt`/`modreq` combinations:

| Modifiers                             | Can be recognized across compilations | Old compilers see them as | `ref` → `ref readonly` | `in` → `ref readonly` |
|---------------------------------------|---------------------------------------|---------------------------|------------------------|-----------------------|
| `modreq(In) modopt(RequiresLocation)` | yes                                   | `in`                      | break                  | ok                    |
| `modreq(In)`                          | no                                    | `in`                      | break                  | ok                    |
| `modreq(RequiresLocation)`            | yes                                   | unsupported               | break                  | break                 |
| `modopt(RequiresLocation)`            | yes                                   | `ref`                     | ok                     | break                 |

We could emit both `[RequiresLocation]` and `[IsReadOnly]` attributes for `ref readonly` parameters.
Then `in` → `ref readonly` would not be a breaking change even for older compiler versions, but `ref` → `ref readonly` would become a source breaking change for older compiler versions (as they would interpret `ref readonly` as `in`, disallowing `ref` modifiers) and new compiler versions with `LangVersion <= 11` (for consistency).

We could make the behavior for `LangVersion <= 11` different from the behavior for older compiler versions.
For example, it could be an error whenever a `ref readonly` parameter is called (even when using the `ref` modifier at the callsite),
or it could be always allowed without any errors.

## Unresolved questions
[unresolved]: #unresolved-questions

- Emit errors for rvalues passed into `in`/`ref readonly` parameters of methods that could capture them? See https://github.com/dotnet/roslyn/pull/67955#discussion_r1178138561.

## Design meetings

- [LDM 2022-04-25](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-04-25.md#ref-readonly-method-parameters): feature accepted
- [LDM 2022-05-09](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-05-09.md#ref-readonly-parameters): discussion split into three parts
- [LDM 2022-05-11](https://github.com/dotnet/csharplang/blob/c8c1615fcad4ca016a97b7260ad497aad53ebc78/meetings/2022/LDM-2022-05-11.md#ref-readonly-method-parameters): allowed `ref` and no callsite annotation for `ref readonly` parameters
