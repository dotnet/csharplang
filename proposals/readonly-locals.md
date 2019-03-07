# readonly locals and parameters

* [x] Proposed
* [ ] Prototype
* [ ] Implementation
* [ ] Specification

## Summary
[summary]: #summary

Allow locals and parameters to be annotated as readonly in order to prevent shallow mutation of those locals and parameters.

## Motivation
[motivation]: #motivation

Today, the `readonly` keyword can be applied to fields; this has the effect of ensuring that a field can only
be written to during construction (static construction in the case of a static field, or instance construction in the case of an instance field),
which helps developers avoid mistakes by accidentally overwriting state which should not be modified. But fields aren't the only places developers
want to ensure that values aren't mutated. In particular, it's common to create a local variable to store temporary state, and accidentally updating
that temporary state can result in erroneous calculations and other such bugs, especially when such "locals" are captured in lambdas, at which point
they are lifted to fields, but there's no way today to mark such lifted fields as `readonly`.

## Detailed design
[design]: #detailed-design

Locals will be annotatable as `readonly` as well, with the compiler ensuring that they're only set at the time of declaration (certain locals in C# are
already implicitly readonly, such as the iteration variable in a 'foreach' loop or the used variable in a 'using' block, but currently a developer has
no ability to mark other locals as `readonly`). Such `readonly` locals must have an initializer:

```csharp
readonly long maxBytesToDelete = (stream.LimitBytes - stream.MaxBytes) / 10;
...
maxBytesToDelete = 0; // Error: can't assign to readonly locals outside of declaration
```

And as shorthand for `readonly var`, the existing contextual keyword `let` may be used, e.g.

```csharp
let maxBytesToDelete = (stream.LimitBytes - stream.MaxBytes) / 10;
...
maxBytesToDelete = 0; // Error: can't assign to readonly locals outside of declaration
```

There are no special constraints for what the initializer can be, and can be anything currently valid as an initializer for locals, e.g.

```csharp
readonly T data = arg1 ?? arg2;
```

`readonly` on locals is particularly valuable when working with lambdas and closures. When an anonymous method or lambda accesses local state from the enclosing scope,
that state is captured into a closure by the compiler, which is represented by a "display class."  Each "local" that's captured is a field in this class, yet
because the compiler is generating this field on your behalf, you have no opportunity to annotate it as `readonly` in order to prevent the lambda from erroneously
writing to the "local" (in quotes because it's really not a local, at least not in the resulting MSIL). With `readonly` locals, the compiler can prevent the lambda
from writing to local, which is particularly valuable in scenarios involving multithreading where an erroneous write could result in a dangerous but rare and
hard-to-find concurrency bug.

```csharp
readonly long index = ...;
Parallel.ForEach(data, item => {
    T element = item[index];
    index = 0; // Error: can't assign to readonly locals outside of declaration
});
```

As a special form of local, parameters will also be annotatable as `readonly`. This would have no effect on what the caller of the method is able to pass to the
parameter (just as there's no constraint on what values may be stored into a `readonly` field), but as with any `readonly` local, the compiler would prohibit code
from writing to the parameter after declaration, which means the body of the method is prohibited from writing to the parameter.

```csharp
public void Update(readonly int index = 0) // Default values are ok though not required
{
    ...
    index = 0; // Error: can't assign to readonly parameters
    ...
}
```

`readonly` parameters do not affect the signature/metadata emitted by the compiler for that method, and simply affect how the compiler handles the compilation of
the method's body. Thus, for example, a base virtual method could have a `readonly` parameter, and that parameter could be writable in an override.

As with fields, `readonly` for locals and parameters is shallow, affecting the storage location but not transitively affecting the object graph. However, also
as with fields, calling a method on a `readonly` local/parameter struct will actually make a copy of the struct and call the method on the copy, in order to avoid
internal mutation of `this`.

`readonly` locals and parameters can't be passed as `ref` or `out` arguments, unless/until `ref readonly` is also supported.

## Alternatives
[alternatives]: #alternatives

- `val` could be used as an alternative shorthand to `let`.

## Unresolved questions
[unresolved]: #unresolved-questions

- `readonly ref` / `ref readonly` / `readonly ref readonly`: I've left the question of how to handle `ref readonly` as separate from this proposal.
- This proposal does not tackle readonly structs / immutable types. That is left for a separate proposal.

## Design meetings

- Briefly discussed on Jan 21, 2015 (<https://github.com/dotnet/roslyn/issues/98>)
