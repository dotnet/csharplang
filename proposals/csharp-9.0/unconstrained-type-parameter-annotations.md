# Unconstrained type parameter annotations

## Summary

Allow nullable annotations for type parameters that are not constrained to value types or reference types: `T?`.
```C#
static T? FirstOrDefault<T>(this IEnumerable<T> collection) { ... }
```

## `?` annotation

In C#8, `?` annotations could only be applied to type parameters that were explicitly constrained to value types or reference types.
In C#9, `?` annotations can be applied to any type parameter, regardless of constraints.

If a type parameter `T` is substituted with a reference type, then `T?` represents a nullable instance of that reference type.
If `T` is substituted with a value type, then `T?` is represents an instance of `T`.
```C#
var s1 = new string[0].FirstOrDefault();  // string? s1
var s2 = new string?[0].FirstOrDefault(); // string? s2
var i1 = new int[0].FirstOrDefault();     // int i1
var i2 = new int?[0].FirstOrDefault();    // int? i2
```

For return values, `T?` is equivalent to `[MaybeNull]T`.
For argument values, `T?` is equivalent to `[AllowNull]T`.

## `default` constraint

For compatibility with existing code where overridden and explicitly implemented generic methods could not include explicit constraint clauses, `T?` in an overridden or explicitly implemented method is treated as `Nullable<T>` where `T` is a value type.

To allow annotations for type parameters constrained to reference types, C#8 allowed explicit `where T : class` and `where T : struct` constraints on the overridden or explicitly implemented method.
```C#
class A1
{
    public virtual void F1<T>(T? t) where T : struct { }
    public virtual void F1<T>(T? t) where T : class { }
}

class B1 : A1
{
    public override void F1<T>(T? t) /*where T : struct*/ { }
    public override void F1<T>(T? t) where T : class { }
}
```

To allow annotations for type parameters that are not constrained to reference types or value types, C#9 allows a new `where T : default` constraint.
```C#
class A2
{
    public virtual void F2<T>(T? t) where T : struct { }
    public virtual void F2<T>(T? t) { }
}

class B2 : A2
{
    public override void F2<T>(T? t) /*where T : struct*/ { }
    public override void F2<T>(T? t) where T : default { }
}
```

It is an error to use a `default` constraint other than on a method override or explicit implementation.
It is an error to use a `default` constraint when the corresponding type parameter in the overridden or interface method is constrained to a reference type or value type.

## Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2019/LDM-2019-11-25.md
- https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-17.md#t
