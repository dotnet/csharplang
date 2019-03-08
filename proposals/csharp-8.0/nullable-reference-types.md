# Nullable reference types in C# #

The goal of this feature is to:

* Allow developers to express whether a variable, parameter or result of a reference type is intended to be null or not.
* Provide warnings when such variables, parameters and results are not used according to that intent.

## Expression of intent

The language already contains the `T?` syntax for value types. It is straightforward to extend this syntax to reference types.

It is assumed that the intent of an unadorned reference type `T` is for it to be non-null.

## Checking of nullable references

A flow analysis tracks nullable reference variables. Where the analysis deems that they would not be null (e.g. after a check or an assignment), their value will be considered a non-null reference.

A nullable reference can also explicitly be treated as non-null with the postfix `x!` operator (the "dammit" operator), for when flow analysis cannot establish a non-null situation that the developer knows is there.

Otherwise, a warning is given if a nullable reference is dereferenced, or is converted to a non-null type.

A warning is given when converting from `S[]` to `T?[]` and from `S?[]` to `T[]`.

A warning is given when converting from `C<S>` to `C<T?>` except when the type parameter is covariant (`out`), and when converting from `C<S?>` to `C<T>` except when the type parameter is contravariant (`in`).

A warning is given on `C<T?>` if the type parameter has non-null constraints. 

## Checking of non-null references

A warning is given if a null literal is assigned to a non-null variable or passed as a non-null parameter.

A warning is also given if a constructor does not explicitly initialize non-null reference fields.

We cannot adequately track that all elements of an array of non-null references are initialized. However, we could issue a warning if no element of a newly created array is assigned to before the array is read from or passed on. That might handle the common case without being too noisy.

We need to decide whether `default(T)` generates a warning, or is simply treated as being of the type `T?`.

## Metadata representation

Nullability adornments should be represented in metadata as attributes. This means that downlevel compilers will ignore them.

We need to decide if only nullable annotations are included, or there's also some indication of whether non-null was "on" in the assembly.

## Generics

If a type parameter `T` has non-nullable constraints, it is treated as non-nullable within its scope.

If a type parameter is unconstrained or has only nullable constraints, the situation is a little more complex: this means that the corresponding type argument could be *either* nullable or non-nullable. The safe thing to do in that situation is to treat the type parameter as *both* nullable and non-nullable, giving warnings when either is violated. 

It is worth considering whether explicit nullable reference constraints should be allowed. Note, however, that we cannot avoid having nullable reference types *implicitly* be constraints in certain cases (inherited constraints).

The `class` constraint is non-null. We can consider whether `class?` should be a valid nullable constraint denoting "nullable reference type".

## Type inference

In type inference, if a contributing type is a nullable reference type, the resulting type should be nullable. In other words, nullness is propagated.

We should consider whether the `null` literal as a participating expression should contribute nullness. It doesn't today: for value types it leads to an error, whereas for reference types the null successfully converts to the plain type. 

```csharp
string? n = "world";
var x = b ? "Hello" : n; // string?
var y = b ? "Hello" : null; // string? or error
var z = b ? 7 : null; // Error today, could be int?
```

## Breaking changes

Non-null warnings are an obvious breaking change on existing code, and should be accompanied with an opt-in mechanism.

Less obviously, warnings from nullable types (as described above) are a breaking change on existing code in certain scenarios where the nullability is implicit:

* Unconstrained type parameters will be treated as implicitly nullable, so assigning them to `object` or accessing e.g. `ToString` will yield warnings.
* if type inference infers nullness from `null` expressions, then existing code will sometimes yield nullable rather than non-nullable types, which can lead to new warnings.

So nullable warnings also need to be optional

Finally, adding annotations to an existing API will be a breaking change to users who have opted in to warnings, when they upgrade the library. This, too, merits the ability to opt in or out. "I want the bug fixes, but I am not ready to deal with their new annotations"

In summary, you need to be able to opt in/out of:
* Nullable warnings
* Non-null warnings
* Warnings from annotations in other files

The granularity of the opt-in suggests an analyzer-like model, where swaths of code can opt in and out with pragmas and severity levels can be chosen by the user. Additionally, per-library options ("ignore the annotations from JSON.NET until I'm ready to deal with the fall out") may be expressible in code as attributes.

The design of the opt-in/transition experience is crucial to the success and usefulness of this feature. We need to make sure that:

* Users can adopt nullability checking gradually as they want to
* Library authors can add nullability annotations without fear of breaking customers
* Despite these, there is not a sense of "configuration nightmare"

## Tweaks

We could consider not using the `?` annotations on locals, but just observing whether they are used in accordance with what gets assigned to them. I don't favor this; I think we should uniformly let people express their intent.

We could consider a shorthand `T! x` on parameters, that auto-generates a runtime null check.

Certain patterns on generic types, such as `FirstOrDefault` or `TryGet`, have slightly weird behavior with non-nullable type arguments, because they explicitly yield default values in certain situations. We could try to nuance the type system to accommodate these better. For instance, we could allow `?` on unconstrained type parameters, even though the type argument could already be nullable. I doubt that it is worth it, and it leads to weirdness related to interaction with nullable *value* types. 

## Nullable value types

We could consider adopting some of the above semantics for nullable value types as well.

We already mentioned type inference, where we could infer `int?` from `(7, null)`, instead of just giving an error.

Another opportunity is to apply the flow analysis to nullable value types. When they are deemed non-null, we could actually allow using as the non-nullable type in certain ways (e.g. member access). We just have to be careful that the things that you can *already* do on a nullable value type will be preferred, for back compat reasons.
