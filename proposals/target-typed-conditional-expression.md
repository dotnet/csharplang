# Target-Typed Conditional Expression

For a conditional expression `c ? e1 : e2`, when

1. there is no common type for `e1` and `e2`, or
2. for which a common type exists but one of the expressions `e1` or `e2` has no implicit conversion to that type

we define a new *conditional expression conversion* that permits an implicit conversion from the conditional expression to any type `T` for which there is a conversion-from-expression from `e1` to `T` and also from `e2` to `T`.  It is an error if a conditional expression neither has a common type between `e1` and `e2` nor is subject to a *conditional expression conversion*.

### Open Issues

We would like to extend this target typing to cases in which the conditional expression has a common type for `e1` and `e2` but there is no conversion from that common type to the target type. That would bring target typing of the conditional expression into alignment of target typing of the switch expression. However we are concerned that would be a breaking change:

```csharp
M(b ? 1 : 2); // calls M(long) without this feature; calls M(short) with this feature

void M(short);
void M(long);
```

We could reduce the scope of the breaking change by modifying the rules for *better conversion from expression*: the conversion from a conditional expression to T1 is a better conversion from expression than the conversion to T2 if the conversion to T1 is not a *conditional expression conversion* and the conversion to T2 is a *conditional expression conversion*.  That resolves the breaking change in the above program (it calls `M(long)` with or without this feature). This approach does have two small downsides.  First, it is not quite the same as the switch expression:

```csharp
M(b ? 1 : 2); // calls M(long)
M(b switch { true => 1, false => 2 }); // calls M(short)
```

This is still a breaking change, but its scope is less likely to affect real programs:

```csharp
M(b ? 1 : 2, 1); // calls M(long, long) without this feature; ambiguous with this feature.

M(short, short);
M(long, long);
```

This becomes ambiguous because the conversion to `long` is better for the first argument (because it does not use the *conditional expression conversion*), but the conversion to `short` is better for the second argument (because `short` is a *better conversion target* than `long`). This breaking change seems less serious because it does not silently change the behavior of an existing program.

Should we elect to make this change to the proposal, we would change

> #### Better conversion from expression
> 
> Given an implicit conversion `C1` that converts from an expression `E` to a type `T1`, and an implicit conversion `C2` that converts from an expression `E` to a type `T2`, `C1` is a ***better conversion*** than `C2` if `E` does not exactly match `T2` and at least one of the following holds:
> 
> * `E` exactly matches `T1` ([Exactly matching Expression](expressions.md#exactly-matching-expression))
> * `T1` is a better conversion target than `T2` ([Better conversion target](expressions.md#better-conversion-target))

to

> #### Better conversion from expression
> 
> Given an implicit conversion `C1` that converts from an expression `E` to a type `T1`, and an implicit conversion `C2` that converts from an expression `E` to a type `T2`, `C1` is a ***better conversion*** than `C2` if `E` does not exactly match `T2` and at least one of the following holds:
> 
> * `E` exactly matches `T1` ([Exactly matching Expression](expressions.md#exactly-matching-expression))
> * `T1` is a better conversion target than `T2` ([Better conversion target](expressions.md#better-conversion-target)) **and either `C1` is not a *conditional expression conversion* or `C2` is a *conditional expression conversion***.
