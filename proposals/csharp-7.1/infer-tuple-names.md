# Infer tuple names (aka. tuple projection initializers)

## Summary
[summary]: #summary

Inferring tuple names is a shorter form variation of tuple syntax. 
It is similar to the inferring of member names on anonymous types and offers similar benefits.

## Motivation
[motivation]: #motivation

The main motivation is to avoid typing redundant information.
For instance, instead of typing `(f1: x.f1, f2: x?.f2)`, the names "f1" and "f2" can be inferred from `(x.f1, x?.f2)`.

This is particularily handy when using tuples in LINQ:

```
// "c" and "result" have element names "f1" and "f2"
var result = list.Select(c => (c.f1, c.f2)).Where(t => t.f2 == 1); 
```

## Detailed design
[design]: #detailed-design

There are two parts to the change:

1.	Try to infer a candidate name for each tuple element which does not have an explicit name:
    -	Using same rules as name inference for anonymous types.
        - In C#, this allows three cases: `y` (identifier), `x.y` (simple member access) and `x?.y` (conditional access).
        - In VB, this allows for additional cases, such as `x.y()`.
    -	Rejecting reserved tuple names (case-sensitive in C#, case-insensitive in VB), as they are either forbidden or already implicit. For instance, such as `ItemN`, `Rest`, and `ToString`.
    -	If any candidate names are duplicates (case-sensitive in C#, case-insensitive in VB) within the entire tuple, we drop those candidates,
2.	During conversions (which check and warn about dropping names from tuple literals), inferred names would not produce any warnings. This avoids breaking existing tuple code.

Note that the rule for handling duplicates is different than that for anonymous types. For instance, `new { x.f1, x.f1 }` produces an error, but `(x.f1, x.f1)` would still be allowed (just without any inferred names). This avoids breaking existing tuple code.

For consistency, the same would apply to tuples produced by deconstruction-assignments (in C#):
```C#
// tuple has element names "f1" and "f2" 
var tuple = ((x.f1, x?.f2) = (1, 2));
```

The same would also apply to VB tuples, using the VB-specific rules for inferring name from expression and case-insensitive name comparisons.

## Drawbacks
[drawbacks]: #drawbacks

The main drawback is that this introduces a compatibility break from C# 7.0:

```C#
Action y = () => ();
var t = (x: x, y);
t.y(); // this might have previously picked up and extension method called “y”, but would now call the lambda.
```

The compat council found this break acceptable, given that it is limited and the time window since tuples shipped (in C# 7.0) is small.

## References
- [LDM April 4th 2017](https://github.com/dotnet/csharplang/blob/master/meetings/2017/LDM-2017-04-05.md#tuple-names)
- [Github discussion](https://github.com/dotnet/csharplang/issues/370) (thanks @alrz for bringing this issue up)
- [Tuples design](https://github.com/dotnet/roslyn/blob/master/docs/features/tuples.md)
