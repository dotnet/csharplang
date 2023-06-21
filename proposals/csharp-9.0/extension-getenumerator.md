# Extension `GetEnumerator` support for `foreach` loops.

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
[summary]: #summary

Allow foreach loops to recognize an extension method GetEnumerator method that otherwise satisfies the foreach pattern, and loop over the expression when it would otherwise be an error.

## Motivation
[motivation]: #motivation

This will bring foreach inline with how other features in C# are implemented, including async and pattern-based deconstruction.

## Detailed design
[design]: #detailed-design

The spec change is relatively straightforward. We modify `The foreach statement` section to this text:

>The compile-time processing of a foreach statement first determines the ***collection type***, ***enumerator type*** and ***element type*** of the expression. This determination proceeds as follows:
>
>*  If the type `X` of *expression* is an array type then there is an implicit reference conversion from `X` to the `IEnumerable` interface (since `System.Array` implements this interface). The ***collection type*** is the `IEnumerable` interface, the ***enumerator type*** is the `IEnumerator` interface and the ***element type*** is the element type of the array type `X`.
>*  If the type `X` of *expression* is `dynamic` then there is an implicit conversion from *expression* to the `IEnumerable` interface ([§10.2.10](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#10210-implicit-dynamic-conversions)). The ***collection type*** is the `IEnumerable` interface and the ***enumerator type*** is the `IEnumerator` interface. If the `var` identifier is given as the *local_variable_type* then the ***element type*** is `dynamic`, otherwise it is `object`.
>*  Otherwise, determine whether the type `X` has an appropriate `GetEnumerator` method:
>    * Perform member lookup on the type `X` with identifier `GetEnumerator` and no type arguments. If the member lookup does not produce a match, or it produces an ambiguity, or produces a match that is not a method group, check for an enumerable interface as described below. It is recommended that a warning be issued if member lookup produces anything except a method group or no match.
>    * Perform overload resolution using the resulting method group and an empty argument list. If overload resolution results in no applicable methods, results in an ambiguity, or results in a single best method but that method is either static or not public, check for an enumerable interface as described below. It is recommended that a warning be issued if overload resolution produces anything except an unambiguous public instance method or no applicable methods.
>    * If the return type `E` of the `GetEnumerator` method is not a class, struct or interface type, an error is produced and no further steps are taken.
>    * Member lookup is performed on `E` with the identifier `Current` and no type arguments. If the member lookup produces no match, the result is an error, or the result is anything except a public instance property that permits reading, an error is produced and no further steps are taken.
>    * Member lookup is performed on `E` with the identifier `MoveNext` and no type arguments. If the member lookup produces no match, the result is an error, or the result is anything except a method group, an error is produced and no further steps are taken.
>    * Overload resolution is performed on the method group with an empty argument list. If overload resolution results in no applicable methods, results in an ambiguity, or results in a single best method but that method is either static or not public, or its return type is not `bool`, an error is produced and no further steps are taken.
>    * The ***collection type*** is `X`, the ***enumerator type*** is `E`, and the ***element type*** is the type of the `Current` property.
>
>*  Otherwise, check for an enumerable interface:
>    * If among all the types `Ti` for which there is an implicit conversion from `X` to `IEnumerable<Ti>`, there is a unique type `T` such that `T` is not `dynamic` and for all the other `Ti` there is an implicit conversion from `IEnumerable<T>` to `IEnumerable<Ti>`, then the ***collection type*** is the interface `IEnumerable<T>`, the ***enumerator type*** is the interface `IEnumerator<T>`, and the ***element type*** is `T`.
>    * Otherwise, if there is more than one such type `T`, then an error is produced and no further steps are taken.
>    * Otherwise, if there is an implicit conversion from `X` to the `System.Collections.IEnumerable` interface, then the ***collection type*** is this interface, the ***enumerator type*** is the interface `System.Collections.IEnumerator`, and the ***element type*** is `object`.
>*  Otherwise, determine whether the type 'X' has an appropriate `GetEnumerator` extension method:
>    * Perform extension method lookup on the type `X` with identifier `GetEnumerator`. If the member lookup does not produce a match, or it produces an ambiguity, or produces a match which is not a method group, an error is produced and no further steps are taken. It is recommended that a warning be issues if member lookup produces anything except a method group or no match.
>    * Perform overload resolution using the resulting method group and a single argument of type `X`. If overload resolution produces no applicable methods, results in an ambiguity, or results in a single best method but that method is not accessible, an error is produced an no further steps are taken.
>        * This resolution permits the first argument to be passed by ref if `X` is a struct type, and the ref kind is `in`.
>    * If the return type `E` of the `GetEnumerator` method is not a class, struct or interface type, an error is produced and no further steps are taken.
>    * Member lookup is performed on `E` with the identifier `Current` and no type arguments. If the member lookup produces no match, the result is an error, or the result is anything except a public instance property that permits reading, an error is produced and no further steps are taken.
>    * Member lookup is performed on `E` with the identifier `MoveNext` and no type arguments. If the member lookup produces no match, the result is an error, or the result is anything except a method group, an error is produced and no further steps are taken.
>    * Overload resolution is performed on the method group with an empty argument list. If overload resolution results in no applicable methods, results in an ambiguity, or results in a single best method but that method is either static or not public, or its return type is not `bool`, an error is produced and no further steps are taken.
>    * The ***collection type*** is `X`, the ***enumerator type*** is `E`, and the ***element type*** is the type of the `Current` property.
>*  Otherwise, an error is produced and no further steps are taken.

For `await foreach`, the rules are similarly modified. The only change that is required to that spec is removing the `Extension methods do not contribute.` line from the description, as the rest of that spec is based on the above rules with different names substituted for the pattern methods.

## Drawbacks
[drawbacks]: #drawbacks

Every change adds additional complexity to the language, and this potentially allows things that weren't designed to be `foreach`ed to be `foreach`ed, like `Range`.

## Alternatives
[alternatives]: #alternatives

Doing nothing.

## Unresolved questions
[unresolved]: #unresolved-questions

None at this point.
