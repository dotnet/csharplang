# Unconstrained type parameter annotation: `T??`

## Summary

Support `??` as a nullable annotation for type parameter references when the type parameters are not constrained
to a value type or reference type.

## Design

### Syntax
The parser is extended to accept `??` in addition to `?` for nullable types.
`??` is a single token with no characters separating the two `?` characters.
```
nullable_type
    : type '?'
    | type '??'
    ;
```

`T?` is not allowed as the type in an `is` expression and so neither is `T??`.
```C#
_ = a is T?? b;     // (a is T) ?? b
_ = a is T?? b : c; // syntax error
```

`??` is not allowed for `class` constraint.
```C#
interface I<T>
    where T : class?? // syntax error
{
}
```

`T?` is not allowed for a type parameter `T` that is not constrained to a value or reference type.
```C#
T? F1<T>() { ... }                   // error: T must be value type or non-nullable reference type
T? F2<T>() where T : notnull { ... } // error: T must be value type or non-nullable reference type
```

`T??` is not allowed for a type parameter `T` that is constrained to a value or reference type.
```C#
T?? F3<T>() where T : class { ... }  // error: T is a reference type
T?? F4<T>() where T : struct { ... } // error: T is a value type
```

`??` is not allowed for annotating types other than type parameters.
```C#
int?? F5() { ... }  // error: int is not a type parameter
T[]?? F6<T> { ... } // error: T[] is not a type parameter
```

_Should `??` be allowed or required for type parameters with inherited constraints that constrain the type parameter sufficiently?_
```C#
abstract class A<T>
{
    internal abstract U?? F<U>() where U : T;
}
class B1 : A<string>
{
    internal override U?? F<U>() => default; // Is ?? allowed or required?
}
class B2 : A<int>
{
    internal override U?? F<U>() => default; // Is ?? allowed or required?
}
class B3 : A<int?>
{
    internal override U?? F<U>() => default; // Is ?? allowed or required?
}
```

`T??` may be used interchangably with `[MaybeNull]T` in cases where `T` is not constrained to a value type or reference type.
`[MaybeNull]T??` is redundant but allowed.

### Overriding, hiding, implementing
Overriding, hiding, and implementing use the same rules with respect to `T` and `T??` for unconstrained type parameters
as are used for `T` and `T?` for type parameters that are constrained to value or reference types:
similar variance rules apply and similar warnings are reported.

### Method and best type inference
Method and best type inference use the same rules with respect to `T` and `T??` for unconstrained type parameters
as are used for `T` and `T?` for type parameters that are constrained to value or reference types:
similar inferences for annotated and unannotated types are made.

### W warnings
Previously, no warnings were reported for assignment or explicit cast of a nullable value to an unconstrained
type parameter `T` because there was no syntax to represent a nullable version of `T`.
Now those cases result in W warnings:
```C#
T x = default;   // warning: converting to non-nullable type
T?? y = default; // ok

var item = (new T[0]).FirstOrDefault();
y = (T)item;     // warning: converting to non-nullable type
y = (T??)item;   // ok
```

### Metadata
In metadata `T??` is represented as `[Nullable]T`, indistinguishable from `T?`.

_There is a potential for cycles decoding a `[Nullable]T` type reference, particularly when the type parameter
reference appears in the constraints of `T` which are used in determining if the type parameter is a
value type or reference type._

### Public API
There are no changes to the compiler API.

`ITypeParameterSymbol.NullableAnnotation` will return `NullableAnnotation.Annotated` for `T??`, indistinquisable from `T?`.
To determine if that type parameter annotation would be represented in source as `??` would require checking the
constraints of the `ITypeParameterSymbol` which might involve checking constraints inherited from base types.

## Design meetings

https://github.com/dotnet/csharplang/blob/master/meetings/2019/LDM-2019-11-25.md
https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-01-08.md#unconstrained-type-parameter-annotation-t
