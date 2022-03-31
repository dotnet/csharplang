# Auto-default structs

https://github.com/dotnet/csharplang/issues/5737

## Summary

This feature makes it so that in struct constructors, we identify fields which were not explicitly assigned by the user before returning or before use, and initialize them implicitly to `default` instead of giving definite assignment errors.

## Motivation

This proposal is raised as a possible mitigation for usability issues found in dotnet/csharplang#5552 and dotnet/csharplang#5635, as well as addressing #5563 (all fields must be definitely assigned, but `field` is not accessible within the constructor).

---

Since C# 1.0, struct constructors have been required to definitely assign `this` as if it were an `out` parameter.

```cs
public struct S
{
    public int x, y;
    public S() // error: Fields 'S.x' and 'S.y' must be fully assigned before control is returned to the caller
    {
    }
}
```

This presents issues when setters are manually defined on semi-auto properties, since the compiler can't treat assignment of the property as equivalent to assignment of the backing field.

```cs
public struct S
{
    public int X { get => field; set => field = value; }
    public S() // error: struct fields aren't fully assigned. But caller can only assign 'this.field' by assigning 'this'.
    {
    }
}
```
We assume that introducing finer-grained restrictions for setters, such as a scheme where the setter doesn't take `ref this` but rather takes `out field` as a parameter, is going to be too niche and incomplete for some use cases.

One fundamental tension we are struggling with is that when struct properties have manually implemented setters, users often have to do some form of "repetition" of either repeatedly assigning or repeating their logic:
```cs
struct S
{
    private int _x;
    public int X
    {
        get => _x;
        set => _x = value >= 0 ? value : throw new ArgumentOutOfRangeException();
    }

    // Solution 1: assign some value in the constructor before "really" assigning through the property setter.
    public S(int x)
    {
        _x = default;
        X = x;
    }

    // Solution 2: assign the field once in the constructor, repeating the implementation of the setter.
    public S(int x)
    {
        _x = x >= 0 ? x : throw new ArgumentOutOfRangeException();
    }
}
```

## Previous discussion
A small group has looked at this issue and considered a few possible solutions:
1. Require users to assign `this = default` when semi-auto properties have manually implemented setters. We agree this is the wrong solution since it blows away values set in field initializers.
2. Implicitly initialize all backing fields of auto/semi-auto properties.
    - This solves the "semi-auto property setters" problem, and it squarely places explicitly declared fields under different rules: "don't implicitly initialize my fields, but do implicitly initialize my auto-properties."
3. Provide a way to assign the backing field of a semi-auto property and require users to assign it.
    - This could be cumbersome compared to (2). An auto property is supposed to be "automatic", and perhaps that includes "automatic" initialization of the field. It could introduce confusion as to when the underlying field is being assigned by an assignment to the property, and when the property setter is being called.

We've also received [feedback](https://github.com/dotnet/csharplang/discussions/5635) from users who want to, for example, include a few field initializers in structs without having to explicitly assign everything. We can solve this issue as well as the "semi-auto property with manually implemented setter" issue at the same time.
```cs
struct MagnitudeVector3d
{
    double X, Y, Z;
    double Magnitude = 1;
    public MagnitudeVector3d() // error: must assign 'X', 'Y', 'Z' before returning
    {
    }
}
```

## Adjusting definite assignment
Instead of performing a definite assignment analysis to give errors for unassigned fields on `this`, we do it to determine *which fields need to be initialized implicitly*. Such initialization is inserted at the *beginning of the constructor*.

```cs
struct S
{
    int x, y;

    // Example 1
    public S()
    {
        // ok. Compiler inserts an assignment of `this = default`.
    }

    // Example 2
    public S()
    {
        // ok. Compiler inserts an assignment of `y = default`.
        x = 1;
    }

    // Example 3
    public S()
    {
        // valid since C# 1.0. Compiler inserts no implicit assignments.
        x = 1;
        y = 2;
    }

    // Example 4
    public S(bool b)
    {
        // ok. Compiler inserts assignment of `this = default`.
        if (b)
            x = 1;
        else
            y = 2;
    }

    // Example 5
    void M() { }
    public S(bool b)
    {
        // ok. Compiler inserts assignment of `y = default`.
        x = 1;
        if (b)
            M();

        y = 2;
    }
}
```

In examples (4) and (5), the resulting codegen sometimes has "double assignments" of fields. This is generally fine, but for users who are concerned with such double assignments, we can emit what used to be definite assignment error diagnostics as *disabled-by-default* warning diagnostics.

```cs
struct S
{
    int x;
    public S() // warning: 'S.x' is implicitly initialized to 'default'.
    {
    }
}
```

Users who set the severity of this diagnostic to "error" will opt in to the pre-C# 11 behavior. Such users are essentially "shut out" of semi-auto properties with manually implemented setters.

```cs
struct S
{
    public int X
    {
        get => field;
        set => field = field < value ? value : field;
    }

    public S() // error: backing field of 'S.X' is implicitly initialized to 'default'.
    {
        X = 1;
    }
}
```

At first glance, this feels like a "hole" in the feature, but it's **actually the right thing to do**. By enabling the diagnostic, the user is telling us that they don't want the compiler to implicitly initialize their fields in the constructor. There's no way to avoid the implicit initialization here, so the solution for them is to use a different way of initializing the field than a manually implemented setter, such as manually declaring the field and assigning it, or by including a field initializer.

Currently, the JIT does not eliminate dead stores through refs, which means that these implicit initializations do have a real cost. But that might be fixable. https://github.com/dotnet/runtime/issues/13727

It's worth noting that initializing individual fields instead of the entire instance is really just an optimization. The compiler should probably be free to implement whatever heuristic it wants, as long as it meets the invariant that fields which are not definitely assigned at all return points or before any non-field member access of `this` are implicitly initialized.

For example, if a struct has 100 fields, and just one of them is explicitly initialized, it might make more sense to do an `initobj` on the entire thing, than to implicitly emit `initobj` for the 99 other fields. However, an implementation which implicitly emits `initobj` for the 99 other fields would still be valid.

## Changes to language specification

We adjust the following section of the standard:

https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11712-this-access
> If the constructor declaration has no constructor initializer, the `this` variable behaves exactly the same as an `out` parameter of the struct type. In particular, this means that the variable shall be definitely assigned in every execution path of the instance constructor.

We adjust this language to read:

If the constructor declaration has no constructor initializer, the `this` variable behaves similarly to an `out` parameter of the struct type, except that it is not an error when the definite assignment requirements ([§9.4.1](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/variables.md#941-general)) are not met. Instead, we introduce the following behaviors:

  1. When the `this` variable itself does not meet the requirements, then all unassigned instance variables within `this` at all points where requirements are violated are implicitly initialized to the default value ([§9.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/variables.md#93-default-values)) in an *initialization* phase before any other code in the constructor runs.
2. When an instance variable *v* within `this` does not meet the requirements, or any instance variable at any level of nesting within *v* does not meet the requirements, then *v* is implicitly initialized to the default value in an *initialization* phase before any other code in the constructor runs.

## Design meetings

https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-14.md#definite-assignment-in-structs
