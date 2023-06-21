# Target-Typed Conditional Expression

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Conditional Expression Conversion

For a conditional expression `c ? e1 : e2`, when

1. there is no common type for `e1` and `e2`, or
2. for which a common type exists but one of the expressions `e1` or `e2` has no implicit conversion to that type

we define a new implicit *conditional expression conversion* that permits an implicit conversion from the conditional expression to any type `T` for which there is a conversion-from-expression from `e1` to `T` and also from `e2` to `T`.  It is an error if a conditional expression neither has a common type between `e1` and `e2` nor is subject to a *conditional expression conversion*.

## Better Conversion from Expression

We change

> #### Better conversion from expression
> 
> Given an implicit conversion `C1` that converts from an expression `E` to a type `T1`, and an implicit conversion `C2` that converts from an expression `E` to a type `T2`, `C1` is a ***better conversion*** than `C2` if `E` does not exactly match `T2` and at least one of the following holds:
> 
> * `E` exactly matches `T1` ([§11.6.4.4](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11644-better-conversion-from-expression))
> * `T1` is a better conversion target than `T2` ([§11.6.4.6](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11646-better-conversion-target))

to

> #### Better conversion from expression
> 
> Given an implicit conversion `C1` that converts from an expression `E` to a type `T1`, and an implicit conversion `C2` that converts from an expression `E` to a type `T2`, `C1` is a ***better conversion*** than `C2` if `E` does not exactly match `T2` and at least one of the following holds:
> 
> * `E` exactly matches `T1` ([§11.6.4.4](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11644-better-conversion-from-expression))
> * **`C1` is not a *conditional expression conversion* and `C2` is a *conditional expression conversion***.
> * `T1` is a better conversion target than `T2` ([§11.6.4.6](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11646-better-conversion-target)) **and either `C1` and `C2` are both *conditional expression conversions* or neither is a *conditional expression conversion***.

## Cast Expression

The current C# language specification says

> A *cast_expression* of the form `(T)E`, where `T` is a *type* and `E` is a *unary_expression*, performs an explicit conversion ([§10.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#103-explicit-conversions)) of the value of `E` to type `T`.

In the presence of the *conditional expression conversion* there may be more than one possible conversion from `E` to `T`. With the addition of *conditional expression conversion*, we prefer any other conversion to a *conditional expression conversion*, and use the *conditional expression conversion* only as a last resort.

## Design Notes

The reason for the change to *Better conversion from expression* is to handle a case such as this:

```csharp
M(b ? 1 : 2);

void M(short);
void M(long);
```

This approach does have two small downsides.  First, it is not quite the same as the switch expression:

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

The reason for the notes on the cast expression is to handle a case such as this:

```csharp
_ = (short)(b ? 1 : 2);
```

This program currently uses the explicit conversion from `int` to `short`, and we want to preserve the current language meaning of this program.  The change would be unobservable at runtime, but with the following program the change would be observable:

```csharp
_ = (A)(b ? c : d);
```

where `c` is of type `C`, `d` is of type `D`, and there is an implicit user-defined conversion from `C` to `D`, and an implicit user-defined conversion from `D` to `A`, and an implicit user-defined conversion from `C` to `A`. If this code is compiled before C# 9.0, when `b` is true we convert from `c` to `D` then to `A`. If we use the *conditional expression conversion*, then when `b` is true we convert from `c` to `A` directly, which executes a different sequence of user code. Therefore we treat the *conditional expression conversion* as a last resort in a cast, to preserve existing behavior.
