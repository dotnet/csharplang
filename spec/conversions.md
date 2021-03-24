# Conversions

A ***conversion*** enables an expression to be treated as being of a particular type. A conversion may cause an expression of a given type to be treated as having a different type, or it may cause an expression without a type to get a type. Conversions can be ***implicit*** or ***explicit***, and this determines whether an explicit cast is required. For instance, the conversion from type `int` to type `long` is implicit, so expressions of type `int` can implicitly be treated as type `long`. The opposite conversion, from type `long` to type `int`, is explicit and so an explicit cast is required.

```csharp
int a = 123;
long b = a;         // implicit conversion from int to long
int c = (int) b;    // explicit conversion from long to int
```

Some conversions are defined by the language. Programs may also define their own conversions ([User-defined conversions](conversions.md#user-defined-conversions)).

## Implicit conversions

The following conversions are classified as implicit conversions:

*  Identity conversions
*  Implicit numeric conversions
*  Implicit enumeration conversions
*  Implicit interpolated string conversions
*  Implicit nullable conversions
*  Null literal conversions
*  Implicit reference conversions
*  Boxing conversions
*  Implicit dynamic conversions
*  Implicit constant expression conversions
*  User-defined implicit conversions
*  Anonymous function conversions
*  Method group conversions

Implicit conversions can occur in a variety of situations, including function member invocations ([Compile-time checking of dynamic overload resolution](expressions.md#compile-time-checking-of-dynamic-overload-resolution)), cast expressions ([Cast expressions](expressions.md#cast-expressions)), and assignments ([Assignment operators](expressions.md#assignment-operators)).

The pre-defined implicit conversions always succeed and never cause exceptions to be thrown. Properly designed user-defined implicit conversions should exhibit these characteristics as well.

For the purposes of conversion, the types `object` and `dynamic` are considered equivalent.

However, dynamic conversions ([Implicit dynamic conversions](conversions.md#implicit-dynamic-conversions) and [Explicit dynamic conversions](conversions.md#explicit-dynamic-conversions)) apply only to expressions of type `dynamic` ([The dynamic type](types.md#the-dynamic-type)).

### Identity conversion

An identity conversion converts from any type to the same type. This conversion exists such that an entity that already has a required type can be said to be convertible to that type.

*  Because `object` and `dynamic` are considered equivalent there is an identity conversion between `object` and `dynamic`, and between constructed types that are the same when replacing all occurrences of `dynamic` with `object`.

### Implicit numeric conversions

The implicit numeric conversions are:

*  From `sbyte` to `short`, `int`, `long`, `float`, `double`, or `decimal`.
*  From `byte` to `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, or `decimal`.
*  From `short` to `int`, `long`, `float`, `double`, or `decimal`.
*  From `ushort` to `int`, `uint`, `long`, `ulong`, `float`, `double`, or `decimal`.
*  From `int` to `long`, `float`, `double`, or `decimal`.
*  From `uint` to `long`, `ulong`, `float`, `double`, or `decimal`.
*  From `long` to `float`, `double`, or `decimal`.
*  From `ulong` to `float`, `double`, or `decimal`.
*  From `char` to `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, or `decimal`.
*  From `float` to `double`.

Conversions from `int`, `uint`, `long`, or `ulong` to `float` and from `long` or `ulong` to `double` may cause a loss of precision, but will never cause a loss of magnitude. The other implicit numeric conversions never lose any information.

There are no implicit conversions to the `char` type, so values of the other integral types do not automatically convert to the `char` type.

### Implicit enumeration conversions

An implicit enumeration conversion permits the *decimal_integer_literal* `0` to be converted to any *enum_type* and to any *nullable_type* whose underlying type is an *enum_type*. In the latter case the conversion is evaluated by converting to the underlying *enum_type* and wrapping the result ([Nullable types](types.md#nullable-types)).

### Implicit interpolated string conversions

An implicit interpolated string conversion permits an *interpolated_string_expression* ([Interpolated strings](expressions.md#interpolated-strings)) to be converted to `System.IFormattable` or `System.FormattableString` (which implements `System.IFormattable`).

When this conversion is applied a string value is not composed from the interpolated string. Instead an instance of `System.FormattableString` is created, as further described in [Interpolated strings](expressions.md#interpolated-strings).

### Implicit nullable conversions

Predefined implicit conversions that operate on non-nullable value types can also be used with nullable forms of those types. For each of the predefined implicit identity and numeric conversions that convert from a non-nullable value type `S` to a non-nullable value type `T`, the following implicit nullable conversions exist:

*  An implicit conversion from `S?` to `T?`.
*  An implicit conversion from `S` to `T?`.

Evaluation of an implicit nullable conversion based on an underlying conversion from `S` to `T` proceeds as follows:

*  If the nullable conversion is from `S?` to `T?`:
    * If the source value is null (`HasValue` property is false), the result is the null value of type `T?`.
    * Otherwise, the conversion is evaluated as an unwrapping from `S?` to `S`, followed by the underlying conversion from `S` to `T`, followed by a wrapping ([Nullable types](types.md#nullable-types)) from `T` to `T?`.

*  If the nullable conversion is from `S` to `T?`, the conversion is evaluated as the underlying conversion from `S` to `T` followed by a wrapping from `T` to `T?`.

### Null literal conversions

An implicit conversion exists from the `null` literal to any nullable type. This conversion produces the null value ([Nullable types](types.md#nullable-types)) of the given nullable type.

### Implicit reference conversions

The implicit reference conversions are:

*  From any *reference_type* to `object` and `dynamic`.
*  From any *class_type* `S` to any *class_type* `T`, provided `S` is derived from `T`.
*  From any *class_type* `S` to any *interface_type* `T`, provided `S` implements `T`.
*  From any *interface_type* `S` to any *interface_type* `T`, provided `S` is derived from `T`.
*  From an *array_type* `S` with an element type `SE` to an *array_type* `T` with an element type `TE`, provided all of the following are true:
    * `S` and `T` differ only in element type. In other words, `S` and `T` have the same number of dimensions.
    * Both `SE` and `TE` are *reference_type*s.
    * An implicit reference conversion exists from `SE` to `TE`.
*  From any *array_type* to `System.Array` and the interfaces it implements.
*  From a single-dimensional array type `S[]` to `System.Collections.Generic.IList<T>` and its base interfaces, provided that there is an implicit identity or reference conversion from `S` to `T`.
*  From any *delegate_type* to `System.Delegate` and the interfaces it implements.
*  From the null literal to any *reference_type*.
*  From any *reference_type* to a *reference_type* `T` if it has an implicit identity or reference conversion to a *reference_type* `T0` and `T0` has an identity conversion to `T`.
*  From any *reference_type* to an interface or delegate type `T` if it has an implicit identity or reference conversion to an interface or delegate type `T0` and `T0` is variance-convertible ([Variance conversion](interfaces.md#variance-conversion)) to `T`.
*  Implicit conversions involving type parameters that are known to be reference types. See [Implicit conversions involving type parameters](conversions.md#implicit-conversions-involving-type-parameters) for more details on implicit conversions involving type parameters.

The implicit reference conversions are those conversions between *reference_type*s that can be proven to always succeed, and therefore require no checks at run-time.

Reference conversions, implicit or explicit, never change the referential identity of the object being converted. In other words, while a reference conversion may change the type of the reference, it never changes the type or value of the object being referred to.

### Boxing conversions

A boxing conversion permits a *value_type* to be implicitly converted to a reference type. A boxing conversion exists from any *non_nullable_value_type* to `object` and `dynamic`, to `System.ValueType` and to any *interface_type* implemented by the *non_nullable_value_type*. Furthermore an *enum_type* can be converted to the type `System.Enum`.

A boxing conversion exists from a *nullable_type* to a reference type, if and only if a boxing conversion exists from the underlying *non_nullable_value_type* to the reference type.

A value type has a boxing conversion to an interface type `I` if it has a boxing conversion to an interface type `I0` and `I0` has an identity conversion to `I`.

A value type has a boxing conversion to an interface type `I` if it has a boxing conversion to an interface or delegate type `I0` and `I0` is variance-convertible ([Variance conversion](interfaces.md#variance-conversion)) to `I`.

Boxing a value of a *non_nullable_value_type* consists of allocating an object instance and copying the *value_type* value into that instance. A struct can be boxed to the type `System.ValueType`, since that is a base class for all structs ([Inheritance](structs.md#inheritance)).

Boxing a value of a *nullable_type* proceeds as follows:

*  If the source value is null (`HasValue` property is false), the result is a null reference of the target type.
*  Otherwise, the result is a reference to a boxed `T` produced by unwrapping and boxing the source value.

Boxing conversions are described further in [Boxing conversions](types.md#boxing-conversions).

### Implicit dynamic conversions

An implicit dynamic conversion exists from an expression of type `dynamic` to any type `T`. The conversion is dynamically bound ([Dynamic binding](expressions.md#dynamic-binding)), which means that an implicit conversion will be sought at run-time from the run-time type of the expression to `T`. If no conversion is found, a run-time exception is thrown.

Note that this implicit conversion seemingly violates the advice in the beginning of [Implicit conversions](conversions.md#implicit-conversions) that an implicit conversion should never cause an exception. However it is not the conversion itself, but the *finding* of the conversion that causes the exception. The risk of run-time exceptions is inherent in the use of dynamic binding. If dynamic binding of the conversion is not desired, the expression can be first converted to `object`, and then to the desired type.

The following example illustrates implicit dynamic conversions:

```csharp
object o  = "object"
dynamic d = "dynamic";

string s1 = o; // Fails at compile-time -- no conversion exists
string s2 = d; // Compiles and succeeds at run-time
int i     = d; // Compiles but fails at run-time -- no conversion exists
```

The assignments to `s2` and `i` both employ implicit dynamic conversions, where the binding of the operations is suspended until run-time. At run-time, implicit conversions are sought from the run-time type of `d` -- `string` -- to the target type. A conversion is found to `string` but not to `int`.

### Implicit constant expression conversions

An implicit constant expression conversion permits the following conversions:

*  A *constant_expression* ([Constant expressions](expressions.md#constant-expressions)) of type `int` can be converted to type `sbyte`, `byte`, `short`, `ushort`, `uint`, or `ulong`, provided the value of the *constant_expression* is within the range of the destination type.
*  A *constant_expression* of type `long` can be converted to type `ulong`, provided the value of the *constant_expression* is not negative.

### Implicit conversions involving type parameters

The following implicit conversions exist for a given type parameter `T`:

*  From `T` to its effective base class `C`, from `T` to any base class of `C`, and from `T` to any interface implemented by `C`. At run-time, if `T` is a value type, the conversion is executed as a boxing conversion. Otherwise, the conversion is executed as an implicit reference conversion or identity conversion.
*  From `T` to an interface type `I` in `T`'s effective interface set and from `T` to any base interface of `I`. At run-time, if `T` is a value type, the conversion is executed as a boxing conversion. Otherwise, the conversion is executed as an implicit reference conversion or identity conversion.
*  From `T` to a type parameter `U`, provided `T` depends on `U` ([Type parameter constraints](classes.md#type-parameter-constraints)). At run-time, if `U` is a value type, then `T` and `U` are necessarily the same type and no conversion is performed. Otherwise, if `T` is a value type, the conversion is executed as a boxing conversion. Otherwise, the conversion is executed as an implicit reference conversion or identity conversion.
*  From the null literal to `T`, provided `T` is known to be a reference type.
*  From `T` to a reference type `I` if it has an implicit conversion to a reference type `S0` and `S0` has an identity conversion to `S`. At run-time the conversion is executed the same way as the conversion to `S0`.
*  From `T` to an interface type `I` if it has an implicit conversion to an interface or delegate type `I0` and `I0` is variance-convertible to `I` ([Variance conversion](interfaces.md#variance-conversion)). At run-time, if `T` is a value type, the conversion is executed as a boxing conversion. Otherwise, the conversion is executed as an implicit reference conversion or identity conversion.

If `T` is known to be a reference type ([Type parameter constraints](classes.md#type-parameter-constraints)), the conversions above are all classified as implicit reference conversions ([Implicit reference conversions](conversions.md#implicit-reference-conversions)). If `T` is not known to be a reference type, the conversions above are classified as boxing conversions ([Boxing conversions](conversions.md#boxing-conversions)).

### User-defined implicit conversions

A user-defined implicit conversion consists of an optional standard implicit conversion, followed by execution of a user-defined implicit conversion operator, followed by another optional standard implicit conversion. The exact rules for evaluating user-defined implicit conversions are described in [Processing of user-defined implicit conversions](conversions.md#processing-of-user-defined-implicit-conversions).

### Anonymous function conversions and method group conversions

Anonymous functions and method groups do not have types in and of themselves, but may be implicitly converted to delegate types or expression tree types. Anonymous function conversions are described in more detail in [Anonymous function conversions](conversions.md#anonymous-function-conversions) and method group conversions in [Method group conversions](conversions.md#method-group-conversions).

## Explicit conversions

The following conversions are classified as explicit conversions:

*  All implicit conversions.
*  Explicit numeric conversions.
*  Explicit enumeration conversions.
*  Explicit nullable conversions.
*  Explicit reference conversions.
*  Explicit interface conversions.
*  Unboxing conversions.
*  Explicit dynamic conversions
*  User-defined explicit conversions.

Explicit conversions can occur in cast expressions ([Cast expressions](expressions.md#cast-expressions)).

The set of explicit conversions includes all implicit conversions. This means that redundant cast expressions are allowed.

The explicit conversions that are not implicit conversions are conversions that cannot be proven to always succeed, conversions that are known to possibly lose information, and conversions across domains of types sufficiently different to merit explicit notation.

### Explicit numeric conversions

The explicit numeric conversions are the conversions from a *numeric_type* to another *numeric_type* for which an implicit numeric conversion ([Implicit numeric conversions](conversions.md#implicit-numeric-conversions)) does not already exist:

*  From `sbyte` to `byte`, `ushort`, `uint`, `ulong`, or `char`.
*  From `byte` to `sbyte` and `char`.
*  From `short` to `sbyte`, `byte`, `ushort`, `uint`, `ulong`, or `char`.
*  From `ushort` to `sbyte`, `byte`, `short`, or `char`.
*  From `int` to `sbyte`, `byte`, `short`, `ushort`, `uint`, `ulong`, or `char`.
*  From `uint` to `sbyte`, `byte`, `short`, `ushort`, `int`, or `char`.
*  From `long` to `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `ulong`, or `char`.
*  From `ulong` to `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, or `char`.
*  From `char` to `sbyte`, `byte`, or `short`.
*  From `float` to `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, or `decimal`.
*  From `double` to `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, or `decimal`.
*  From `decimal` to `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, or `double`.

Because the explicit conversions include all implicit and explicit numeric conversions, it is always possible to convert from any *numeric_type* to any other *numeric_type* using a cast expression ([Cast expressions](expressions.md#cast-expressions)).

The explicit numeric conversions possibly lose information or possibly cause exceptions to be thrown. An explicit numeric conversion is processed as follows:

*  For a conversion from an integral type to another integral type, the processing depends on the overflow checking context ([The checked and unchecked operators](expressions.md#the-checked-and-unchecked-operators)) in which the conversion takes place:
    * In a `checked` context, the conversion succeeds if the value of the source operand is within the range of the destination type, but throws a `System.OverflowException` if the value of the source operand is outside the range of the destination type.
    * In an `unchecked` context, the conversion always succeeds, and proceeds as follows.
        * If the source type is larger than the destination type, then the source value is truncated by discarding its "extra" most significant bits. The result is then treated as a value of the destination type.
        * If the source type is smaller than the destination type, then the source value is either sign-extended or zero-extended so that it is the same size as the destination type. Sign-extension is used if the source type is signed; zero-extension is used if the source type is unsigned. The result is then treated as a value of the destination type.
        * If the source type is the same size as the destination type, then the source value is treated as a value of the destination type.
*  For a conversion from `decimal` to an integral type, the source value is rounded towards zero to the nearest integral value, and this integral value becomes the result of the conversion. If the resulting integral value is outside the range of the destination type, a `System.OverflowException` is thrown.
*  For a conversion from `float` or `double` to an integral type, the processing depends on the overflow checking context ([The checked and unchecked operators](expressions.md#the-checked-and-unchecked-operators)) in which the conversion takes place:
    * In a `checked` context, the conversion proceeds as follows:
        * If the value of the operand is NaN or infinite, a `System.OverflowException` is thrown.
        * Otherwise, the source operand is rounded towards zero to the nearest integral value. If this integral value is within the range of the destination type then this value is the result of the conversion.
        * Otherwise, a `System.OverflowException` is thrown.
    * In an `unchecked` context, the conversion always succeeds, and proceeds as follows.
        * If the value of the operand is NaN or infinite, the result of the conversion is an unspecified value of the destination type.
        * Otherwise, the source operand is rounded towards zero to the nearest integral value. If this integral value is within the range of the destination type then this value is the result of the conversion.
        * Otherwise, the result of the conversion is an unspecified value of the destination type.
*  For a conversion from `double` to `float`, the `double` value is rounded to the nearest `float` value. If the `double` value is too small to represent as a `float`, the result becomes positive zero or negative zero. If the `double` value is too large to represent as a `float`, the result becomes positive infinity or negative infinity. If the `double` value is NaN, the result is also NaN.
*  For a conversion from `float` or `double` to `decimal`, the source value is converted to `decimal` representation and rounded to the nearest number after the 28th decimal place if required ([The decimal type](types.md#the-decimal-type)). If the source value is too small to represent as a `decimal`, the result becomes zero. If the source value is NaN, infinity, or too large to represent as a `decimal`, a `System.OverflowException` is thrown.
*  For a conversion from `decimal` to `float` or `double`, the `decimal` value is rounded to the nearest `double` or `float` value. While this conversion may lose precision, it never causes an exception to be thrown.

### Explicit enumeration conversions

The explicit enumeration conversions are:

*  From `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, `double`, or `decimal` to any *enum_type*.
*  From any *enum_type* to `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, `double`, or `decimal`.
*  From any *enum_type* to any other *enum_type*.

An explicit enumeration conversion between two types is processed by treating any participating *enum_type* as the underlying type of that *enum_type*, and then performing an implicit or explicit numeric conversion between the resulting types. For example, given an *enum_type* `E` with and underlying type of `int`, a conversion from `E` to `byte` is processed as an explicit numeric conversion ([Explicit numeric conversions](conversions.md#explicit-numeric-conversions)) from `int` to `byte`, and a conversion from `byte` to `E` is processed as an implicit numeric conversion ([Implicit numeric conversions](conversions.md#implicit-numeric-conversions)) from `byte` to `int`.

### Explicit nullable conversions

***Explicit nullable conversions*** permit predefined explicit conversions that operate on non-nullable value types to also be used with nullable forms of those types. For each of the predefined explicit conversions that convert from a non-nullable value type `S` to a non-nullable value type `T` ([Identity conversion](conversions.md#identity-conversion), [Implicit numeric conversions](conversions.md#implicit-numeric-conversions), [Implicit enumeration conversions](conversions.md#implicit-enumeration-conversions), [Explicit numeric conversions](conversions.md#explicit-numeric-conversions), and [Explicit enumeration conversions](conversions.md#explicit-enumeration-conversions)), the following nullable conversions exist:

*  An explicit conversion from `S?` to `T?`.
*  An explicit conversion from `S` to `T?`.
*  An explicit conversion from `S?` to `T`.

Evaluation of a nullable conversion based on an underlying conversion from `S` to `T` proceeds as follows:

*  If the nullable conversion is from `S?` to `T?`:
    * If the source value is null (`HasValue` property is false), the result is the null value of type `T?`.
    * Otherwise, the conversion is evaluated as an unwrapping from `S?` to `S`, followed by the underlying conversion from `S` to `T`, followed by a wrapping from `T` to `T?`.
*  If the nullable conversion is from `S` to `T?`, the conversion is evaluated as the underlying conversion from `S` to `T` followed by a wrapping from `T` to `T?`.
*  If the nullable conversion is from `S?` to `T`, the conversion is evaluated as an unwrapping from `S?` to `S` followed by the underlying conversion from `S` to `T`.

Note that an attempt to unwrap a nullable value will throw an exception if the value is `null`.

### Explicit reference conversions

The explicit reference conversions are:

*  From `object` and `dynamic` to any other *reference_type*.
*  From any *class_type* `S` to any *class_type* `T`, provided `S` is a base class of `T`.
*  From any *class_type* `S` to any *interface_type* `T`, provided `S` is not sealed and provided `S` does not implement `T`.
*  From any *interface_type* `S` to any *class_type* `T`, provided `T` is not sealed or provided `T` implements `S`.
*  From any *interface_type* `S` to any *interface_type* `T`, provided `S` is not derived from `T`.
*  From an *array_type* `S` with an element type `SE` to an *array_type* `T` with an element type `TE`, provided all of the following are true:
    * `S` and `T` differ only in element type. In other words, `S` and `T` have the same number of dimensions.
    * Both `SE` and `TE` are *reference_type*s.
    * An explicit reference conversion exists from `SE` to `TE`.
*  From `System.Array` and the interfaces it implements to any *array_type*.
*  From a single-dimensional array type `S[]` to `System.Collections.Generic.IList<T>` and its base interfaces, provided that there is an explicit reference conversion from `S` to `T`.
*  From `System.Collections.Generic.IList<S>` and its base interfaces to a single-dimensional array type `T[]`, provided that there is an explicit identity or reference conversion from `S` to `T`.
*  From `System.Delegate` and the interfaces it implements to any *delegate_type*.
*  From a reference type to a reference type `T` if it has an explicit reference conversion to a reference type `T0` and `T0` has an identity conversion `T`.
*  From a reference type to an interface or delegate type `T` if it has an explicit reference conversion to an interface or delegate type `T0` and either `T0` is variance-convertible to `T` or `T` is variance-convertible to `T0` ([Variance conversion](interfaces.md#variance-conversion)).
*  From `D<S1...Sn>` to `D<T1...Tn>` where `D<X1...Xn>` is a generic delegate type, `D<S1...Sn>` is not compatible with or identical to `D<T1...Tn>`, and for each type parameter `Xi` of `D` the following holds:
    * If `Xi` is invariant, then `Si` is identical to `Ti`.
    * If `Xi` is covariant, then there is an implicit or explicit identity or reference conversion from `Si` to `Ti`.
    * If `Xi` is contravariant, then `Si` and `Ti` are either identical or both reference types.
*  Explicit conversions involving type parameters that are known to be reference types. For more details on explicit conversions involving type parameters, see [Explicit conversions involving type parameters](conversions.md#explicit-conversions-involving-type-parameters).

The explicit reference conversions are those conversions between reference-types that require run-time checks to ensure they are correct.

For an explicit reference conversion to succeed at run-time, the value of the source operand must be `null`, or the actual type of the object referenced by the source operand must be a type that can be converted to the destination type by an implicit reference conversion ([Implicit reference conversions](conversions.md#implicit-reference-conversions)) or boxing conversion ([Boxing conversions](conversions.md#boxing-conversions)). If an explicit reference conversion fails, a `System.InvalidCastException` is thrown.

Reference conversions, implicit or explicit, never change the referential identity of the object being converted. In other words, while a reference conversion may change the type of the reference, it never changes the type or value of the object being referred to.

### Unboxing conversions

An unboxing conversion permits a reference type to be explicitly converted to a *value_type*. An unboxing conversion exists from the types `object`, `dynamic` and `System.ValueType` to any *non_nullable_value_type*, and from any *interface_type* to any *non_nullable_value_type* that implements the *interface_type*. Furthermore type `System.Enum` can be unboxed to any *enum_type*.

An unboxing conversion exists from a reference type to a *nullable_type* if an unboxing conversion exists from the reference type to the underlying *non_nullable_value_type* of the *nullable_type*.

A value type `S` has an unboxing conversion from an interface type `I` if it has an unboxing conversion from an interface type `I0` and `I0` has an identity conversion to `I`.

A value type `S` has an unboxing conversion from an interface type `I` if it has an unboxing conversion from an interface or delegate type `I0` and either `I0` is variance-convertible to `I` or `I` is variance-convertible to `I0` ([Variance conversion](interfaces.md#variance-conversion)).

An unboxing operation consists of first checking that the object instance is a boxed value of the given *value_type*, and then copying the value out of the instance. Unboxing a null reference to a *nullable_type* produces the null value of the *nullable_type*. A struct can be unboxed from the type `System.ValueType`, since that is a base class for all structs ([Inheritance](structs.md#inheritance)).

Unboxing conversions are described further in [Unboxing conversions](types.md#unboxing-conversions).

### Explicit dynamic conversions

An explicit dynamic conversion exists from an expression of type `dynamic` to any type `T`. The conversion is dynamically bound ([Dynamic binding](expressions.md#dynamic-binding)), which means that an explicit conversion will be sought at run-time from the run-time type of the expression to `T`. If no conversion is found, a run-time exception is thrown.

If dynamic binding of the conversion is not desired, the expression can be first converted to `object`, and then to the desired type.

Assume the following class is defined:
```csharp
class C
{
    int i;

    public C(int i) { this.i = i; }

    public static explicit operator C(string s) 
    {
        return new C(int.Parse(s));
    }
}
```

The following example illustrates explicit dynamic conversions:
```csharp
object o  = "1";
dynamic d = "2";

var c1 = (C)o; // Compiles, but explicit reference conversion fails
var c2 = (C)d; // Compiles and user defined conversion succeeds
```

The best conversion of `o` to `C` is found at compile-time to be an explicit reference conversion. This fails at run-time, because `"1"` is not in fact a `C`. The conversion of `d` to `C` however, as an explicit dynamic conversion, is suspended to run-time, where a user defined conversion from the run-time type of `d` -- `string` -- to `C` is found, and succeeds.

### Explicit conversions involving type parameters

The following explicit conversions exist for a given type parameter `T`:

*  From the effective base class `C` of `T` to `T` and from any base class of `C` to `T`. At run-time, if `T` is a value type, the conversion is executed as an unboxing conversion. Otherwise, the conversion is executed as an explicit reference conversion or identity conversion.
*  From any interface type to `T`. At run-time, if `T` is a value type, the conversion is executed as an unboxing conversion. Otherwise, the conversion is executed as an explicit reference conversion or identity conversion.
*  From `T` to any *interface_type* `I` provided there is not already an implicit conversion from `T` to `I`. At run-time, if `T` is a value type, the conversion is executed as a boxing conversion followed by an explicit reference conversion. Otherwise, the conversion is executed as an explicit reference conversion or identity conversion.
*  From a type parameter `U` to `T`, provided `T` depends on `U` ([Type parameter constraints](classes.md#type-parameter-constraints)). At run-time, if `U` is a value type, then `T` and `U` are necessarily the same type and no conversion is performed. Otherwise, if `T` is a value type, the conversion is executed as an unboxing conversion. Otherwise, the conversion is executed as an explicit reference conversion or identity conversion.

If `T` is known to be a reference type, the conversions above are all classified as explicit reference conversions ([Explicit reference conversions](conversions.md#explicit-reference-conversions)). If `T` is not known to be a reference type, the conversions above are classified as unboxing conversions ([Unboxing conversions](conversions.md#unboxing-conversions)).

The above rules do not permit a direct explicit conversion from an unconstrained type parameter to a non-interface type, which might be surprising. The reason for this rule is to prevent confusion and make the semantics of such conversions clear. For example, consider the following declaration:
```csharp
class X<T>
{
    public static long F(T t) {
        return (long)t;                // Error 
    }
}
```

If the direct explicit conversion of `t` to `int` were permitted, one might easily expect that `X<int>.F(7)` would return `7L`. However, it would not, because the standard numeric conversions are only considered when the types are known to be numeric at binding-time. In order to make the semantics clear, the above example must instead be written:
```csharp
class X<T>
{
    public static long F(T t) {
        return (long)(object)t;        // Ok, but will only work when T is long
    }
}
```

This code will now compile but executing `X<int>.F(7)` would then throw an exception at run-time, since a boxed `int` cannot be converted directly to a `long`.

### User-defined explicit conversions

A user-defined explicit conversion consists of an optional standard explicit conversion, followed by execution of a user-defined implicit or explicit conversion operator, followed by another optional standard explicit conversion. The exact rules for evaluating user-defined explicit conversions are described in [Processing of user-defined explicit conversions](conversions.md#processing-of-user-defined-explicit-conversions).

## Standard conversions

The standard conversions are those pre-defined conversions that can occur as part of a user-defined conversion.

### Standard implicit conversions

The following implicit conversions are classified as standard implicit conversions:

*  Identity conversions ([Identity conversion](conversions.md#identity-conversion))
*  Implicit numeric conversions ([Implicit numeric conversions](conversions.md#implicit-numeric-conversions))
*  Implicit nullable conversions ([Implicit nullable conversions](conversions.md#implicit-nullable-conversions))
*  Implicit reference conversions ([Implicit reference conversions](conversions.md#implicit-reference-conversions))
*  Boxing conversions ([Boxing conversions](conversions.md#boxing-conversions))
*  Implicit constant expression conversions ([Implicit constant expression conversions](conversions.md#implicit-constant-expression-conversions))
*  Implicit conversions involving type parameters ([Implicit conversions involving type parameters](conversions.md#implicit-conversions-involving-type-parameters))

The standard implicit conversions specifically exclude user-defined implicit conversions.

### Standard explicit conversions

The standard explicit conversions are all standard implicit conversions plus the subset of the explicit conversions for which an opposite standard implicit conversion exists. In other words, if a standard implicit conversion exists from a type `A` to a type `B`, then a standard explicit conversion exists from type `A` to type `B` and from type `B` to type `A`.

## User-defined conversions

C# allows the pre-defined implicit and explicit conversions to be augmented by ***user-defined conversions***. User-defined conversions are introduced by declaring conversion operators ([Conversion operators](classes.md#conversion-operators)) in class and struct types.

### Permitted user-defined conversions

C# permits only certain user-defined conversions to be declared. In particular, it is not possible to redefine an already existing implicit or explicit conversion.

For a given source type `S` and target type `T`, if `S` or `T` are nullable types, let `S0` and `T0` refer to their underlying types, otherwise `S0` and `T0` are equal to `S` and `T` respectively. A class or struct is permitted to declare a conversion from a source type `S` to a target type `T` only if all of the following are true:

*  `S0` and `T0` are different types.
*  Either `S0` or `T0` is the class or struct type in which the operator declaration takes place.
*  Neither `S0` nor `T0` is an *interface_type*.
*  Excluding user-defined conversions, a conversion does not exist from `S` to `T` or from `T` to `S`.

The restrictions that apply to user-defined conversions are discussed further in [Conversion operators](classes.md#conversion-operators).

### Lifted conversion operators

Given a user-defined conversion operator that converts from a non-nullable value type `S` to a non-nullable value type `T`, a ***lifted conversion operator*** exists that converts from `S?` to `T?`. This lifted conversion operator performs an unwrapping from `S?` to `S` followed by the user-defined conversion from `S` to `T` followed by a wrapping from `T` to `T?`, except that a null valued `S?` converts directly to a null valued `T?`.

A lifted conversion operator has the same implicit or explicit classification as its underlying user-defined conversion operator. The term "user-defined conversion" applies to the use of both user-defined and lifted conversion operators.

### Evaluation of user-defined conversions

A user-defined conversion converts a value from its type, called the ***source type***, to another type, called the ***target type***. Evaluation of a user-defined conversion centers on finding the ***most specific*** user-defined conversion operator for the particular source and target types. This determination is broken into several steps:

*  Finding the set of classes and structs from which user-defined conversion operators will be considered. This set consists of the source type and its base classes and the target type and its base classes (with the implicit assumptions that only classes and structs can declare user-defined operators, and that non-class types have no base classes). For the purposes of this step, if either the source or target type is a *nullable_type*, their underlying type is used instead.
*  From that set of types, determining which user-defined and lifted conversion operators are applicable. For a conversion operator to be applicable, it must be possible to perform a standard conversion ([Standard conversions](conversions.md#standard-conversions)) from the source type to the operand type of the operator, and it must be possible to perform a standard conversion from the result type of the operator to the target type.
*  From the set of applicable user-defined operators, determining which operator is unambiguously the most specific. In general terms, the most specific operator is the operator whose operand type is "closest" to the source type and whose result type is "closest" to the target type. User-defined conversion operators are preferred over lifted conversion operators. The exact rules for establishing the most specific user-defined conversion operator are defined in the following sections.

Once a most specific user-defined conversion operator has been identified, the actual execution of the user-defined conversion involves up to three steps:

*  First, if required, performing a standard conversion from the source type to the operand type of the user-defined or lifted conversion operator.
*  Next, invoking the user-defined or lifted conversion operator to perform the conversion.
*  Finally, if required, performing a standard conversion from the result type of the user-defined or lifted conversion operator to the target type.

Evaluation of a user-defined conversion never involves more than one user-defined or lifted conversion operator. In other words, a conversion from type `S` to type `T` will never first execute a user-defined conversion from `S` to `X` and then execute a user-defined conversion from `X` to `T`.

Exact definitions of evaluation of user-defined implicit or explicit conversions are given in the following sections. The definitions make use of the following terms:

*  If a standard implicit conversion ([Standard implicit conversions](conversions.md#standard-implicit-conversions)) exists from a type `A` to a type `B`, and if neither `A` nor `B` are *interface_type*s, then `A` is said to be ***encompassed by*** `B`, and `B` is said to ***encompass*** `A`.
*  The ***most encompassing type*** in a set of types is the one type that encompasses all other types in the set. If no single type encompasses all other types, then the set has no most encompassing type. In more intuitive terms, the most encompassing type is the "largest" type in the set—the one type to which each of the other types can be implicitly converted.
*  The ***most encompassed type*** in a set of types is the one type that is encompassed by all other types in the set. If no single type is encompassed by all other types, then the set has no most encompassed type. In more intuitive terms, the most encompassed type is the "smallest" type in the set—the one type that can be implicitly converted to each of the other types.

### Processing of user-defined implicit conversions

A user-defined implicit conversion from type `S` to type `T` is processed as follows:

*  Determine the types `S0` and `T0`. If `S` or `T` are nullable types, `S0` and `T0` are their underlying types, otherwise `S0` and `T0` are equal to `S` and `T` respectively.
*  Find the set of types, `D`, from which user-defined conversion operators will be considered. This set consists of `S0` (if `S0` is a class or struct), the base classes of `S0` (if `S0` is a class), and `T0` (if `T0` is a class or struct).
*  Find the set of applicable user-defined and lifted conversion operators, `U`. This set consists of the user-defined and lifted implicit conversion operators declared by the classes or structs in `D` that convert from a type encompassing `S` to a type encompassed by `T`. If `U` is empty, the conversion is undefined and a compile-time error occurs.
*  Find the most specific source type, `SX`, of the operators in `U`:
    * If any of the operators in `U` convert from `S`, then `SX` is `S`.
    * Otherwise, `SX` is the most encompassed type in the combined set of source types of the operators in `U`. If exactly one most encompassed type cannot be found, then the conversion is ambiguous and a compile-time error occurs.
*  Find the most specific target type, `TX`, of the operators in `U`:
    * If any of the operators in `U` convert to `T`, then `TX` is `T`.
    * Otherwise, `TX` is the most encompassing type in the combined set of target types of the operators in `U`. If exactly one most encompassing type cannot be found, then the conversion is ambiguous and a compile-time error occurs.
*  Find the most specific conversion operator:
    * If `U` contains exactly one user-defined conversion operator that converts from `SX` to `TX`, then this is the most specific conversion operator.
    * Otherwise, if `U` contains exactly one lifted conversion operator that converts from `SX` to `TX`, then this is the most specific conversion operator.
    * Otherwise, the conversion is ambiguous and a compile-time error occurs.
*  Finally, apply the conversion:
    * If `S` is not `SX`, then a standard implicit conversion from `S` to `SX` is performed.
    * The most specific conversion operator is invoked to convert from `SX` to `TX`.
    * If `TX` is not `T`, then a standard implicit conversion from `TX` to `T` is performed.

### Processing of user-defined explicit conversions

A user-defined explicit conversion from type `S` to type `T` is processed as follows:

*  Determine the types `S0` and `T0`. If `S` or `T` are nullable types, `S0` and `T0` are their underlying types, otherwise `S0` and `T0` are equal to `S` and `T` respectively.
*  Find the set of types, `D`, from which user-defined conversion operators will be considered. This set consists of `S0` (if `S0` is a class or struct), the base classes of `S0` (if `S0` is a class), `T0` (if `T0` is a class or struct), and the base classes of `T0` (if `T0` is a class).
*  Find the set of applicable user-defined and lifted conversion operators, `U`. This set consists of the user-defined and lifted implicit or explicit conversion operators declared by the classes or structs in `D` that convert from a type encompassing or encompassed by `S` to a type encompassing or encompassed by `T`. If `U` is empty, the conversion is undefined and a compile-time error occurs.
*  Find the most specific source type, `SX`, of the operators in `U`:
    * If any of the operators in `U` convert from `S`, then `SX` is `S`.
    * Otherwise, if any of the operators in `U` convert from types that encompass `S`, then `SX` is the most encompassed type in the combined set of source types of those operators. If no most encompassed type can be found, then the conversion is ambiguous and a compile-time error occurs.
    * Otherwise, `SX` is the most encompassing type in the combined set of source types of the operators in `U`. If exactly one most encompassing type cannot be found, then the conversion is ambiguous and a compile-time error occurs.
*  Find the most specific target type, `TX`, of the operators in `U`:
    * If any of the operators in `U` convert to `T`, then `TX` is `T`.
    * Otherwise, if any of the operators in `U` convert to types that are encompassed by `T`, then `TX` is the most encompassing type in the combined set of target types of those operators. If exactly one most encompassing type cannot be found, then the conversion is ambiguous and a compile-time error occurs.
    * Otherwise, `TX` is the most encompassed type in the combined set of target types of the operators in `U`. If no most encompassed type can be found, then the conversion is ambiguous and a compile-time error occurs.
*  Find the most specific conversion operator:
    * If `U` contains exactly one user-defined conversion operator that converts from `SX` to `TX`, then this is the most specific conversion operator.
    * Otherwise, if `U` contains exactly one lifted conversion operator that converts from `SX` to `TX`, then this is the most specific conversion operator.
    * Otherwise, the conversion is ambiguous and a compile-time error occurs.
*  Finally, apply the conversion:
    * If `S` is not `SX`, then a standard explicit conversion from `S` to `SX` is performed.
    * The most specific user-defined conversion operator is invoked to convert from `SX` to `TX`.
    * If `TX` is not `T`, then a standard explicit conversion from `TX` to `T` is performed.

## Anonymous function conversions

An *anonymous_method_expression* or *lambda_expression* is classified as an anonymous function ([Anonymous function expressions](expressions.md#anonymous-function-expressions)). The expression does not have a type but can be implicitly converted to a compatible delegate type or expression tree type. Specifically, an anonymous function `F` is compatible with a delegate type `D` provided:

*  If `F` contains an *anonymous_function_signature*, then `D` and `F` have the same number of parameters.
*  If `F` does not contain an *anonymous_function_signature*, then `D` may have zero or more parameters of any type, as long as no parameter of `D` has the `out` parameter modifier.
*  If `F` has an explicitly typed parameter list, each parameter in `D` has the same type and modifiers as the corresponding parameter in `F`.
*  If `F` has an implicitly typed parameter list, `D` has no `ref` or `out` parameters.
*  If the body of `F` is an expression, and either `D` has a `void` return type or `F` is async and `D` has the return type `Task`, then when each parameter of `F` is given the type of the corresponding parameter in `D`, the body of `F` is a valid expression (wrt [Expressions](expressions.md)) that would be permitted as a *statement_expression* ([Expression statements](statements.md#expression-statements)).
*  If the body of `F` is a statement block, and either `D` has a `void` return type or `F` is async and `D` has the return type `Task`, then when each parameter of `F` is given the type of the corresponding parameter in `D`, the body of `F` is a valid statement block (wrt [Blocks](statements.md#blocks)) in which no `return` statement specifies an expression.
*  If the body of `F` is an expression, and *either* `F` is non-async and `D` has a non-void return type `T`, *or* `F` is async and `D` has a return type `Task<T>`, then when each parameter of `F` is given the type of the corresponding parameter in `D`, the body of `F` is a valid expression (wrt [Expressions](expressions.md)) that is implicitly convertible to `T`.
*  If the body of `F` is a statement block, and *either* `F` is non-async and `D` has a non-void return type `T`, *or* `F` is async and `D` has a return type `Task<T>`, then when each parameter of `F` is given the type of the corresponding parameter in `D`, the body of `F` is a valid statement block (wrt [Blocks](statements.md#blocks)) with a non-reachable end point in which each `return` statement specifies an expression that is implicitly convertible to `T`.

For the purpose of brevity, this section uses the short form for the task types `Task` and `Task<T>` ([Async functions](classes.md#async-functions)).

A lambda expression `F` is compatible with an expression tree type `Expression<D>` if `F` is compatible with the delegate type `D`. Note that this does not apply to anonymous methods, only lambda expressions.

Certain lambda expressions cannot be converted to expression tree types: Even though the conversion *exists*, it fails at compile-time. This is the case if the lambda expression:

*  Has a *block* body
*  Contains simple or compound assignment operators
*  Contains a dynamically bound expression
*  Is async

The examples that follow use a generic delegate type `Func<A,R>` which represents a function that takes an argument of type `A` and returns a value of type `R`:
```csharp
delegate R Func<A,R>(A arg);
```

In the assignments
```csharp
Func<int,int> f1 = x => x + 1;                 // Ok

Func<int,double> f2 = x => x + 1;              // Ok

Func<double,int> f3 = x => x + 1;              // Error

Func<int, Task<int>> f4 = async x => x + 1;    // Ok
```
the parameter and return types of each anonymous function are determined from the type of the variable to which the anonymous function is assigned.

The first assignment successfully converts the anonymous function to the delegate type `Func<int,int>` because, when `x` is given type `int`, `x+1` is a valid expression that is implicitly convertible to type `int`.

Likewise, the second assignment successfully converts the anonymous function to the delegate type `Func<int,double>` because the result of `x+1` (of type `int`) is implicitly convertible to type `double`.

However, the third assignment is a compile-time error because, when `x` is given type `double`, the result of `x+1` (of type `double`) is not implicitly convertible to type `int`.

The fourth assignment successfully converts the anonymous async function to the delegate type `Func<int, Task<int>>` because the result of `x+1` (of type `int`) is implicitly convertible to the result type `int` of the task type `Task<int>`.

Anonymous functions may influence overload resolution, and participate in type inference. See [Function members](expressions.md#function-members) for further details.

### Evaluation of anonymous function conversions to delegate types

Conversion of an anonymous function to a delegate type produces a delegate instance which references the anonymous function and the (possibly empty) set of captured outer variables that are active at the time of the evaluation. When the delegate is invoked, the body of the anonymous function is executed. The code in the body is executed using the set of captured outer variables referenced by the delegate.

The invocation list of a delegate produced from an anonymous function contains a single entry. The exact target object and target method of the delegate are unspecified. In particular, it is unspecified whether the target object of the delegate is `null`, the `this` value of the enclosing function member, or some other object.

Conversions of semantically identical anonymous functions with the same (possibly empty) set of captured outer variable instances to the same delegate types are permitted (but not required) to return the same delegate instance. The term semantically identical is used here to mean that execution of the anonymous functions will, in all cases, produce the same effects given the same arguments. This rule permits code such as the following to be optimized.

```csharp
delegate double Function(double x);

class Test
{
    static double[] Apply(double[] a, Function f) {
        double[] result = new double[a.Length];
        for (int i = 0; i < a.Length; i++) result[i] = f(a[i]);
        return result;
    }

    static void F(double[] a, double[] b) {
        a = Apply(a, (double x) => Math.Sin(x));
        b = Apply(b, (double y) => Math.Sin(y));
        ...
    }
}
```

Since the two anonymous function delegates have the same (empty) set of captured outer variables, and since the anonymous functions are semantically identical, the compiler is permitted to have the delegates refer to the same target method. Indeed, the compiler is permitted to return the very same delegate instance from both anonymous function expressions.

### Evaluation of anonymous function conversions to expression tree types

Conversion of an anonymous function to an expression tree type produces an expression tree ([Expression tree types](types.md#expression-tree-types)). More precisely, evaluation of the anonymous function conversion leads to the construction of an object structure that represents the structure of the anonymous function itself. The precise structure of the expression tree, as well as the exact process for creating it, are implementation defined.

### Implementation example

This section describes a possible implementation of anonymous function conversions in terms of other C# constructs. The implementation described here is based on the same principles used by the Microsoft C# compiler, but it is by no means a mandated implementation, nor is it the only one possible. It only briefly mentions conversions to expression trees, as their exact semantics are outside the scope of this specification.

The remainder of this section gives several examples of code that contains anonymous functions with different characteristics. For each example, a corresponding translation to code that uses only other C# constructs is provided. In the examples, the identifier `D` is assumed by represent the following delegate type:
```csharp
public delegate void D();
```

The simplest form of an anonymous function is one that captures no outer variables:
```csharp
class Test
{
    static void F() {
        D d = () => { Console.WriteLine("test"); };
    }
}
```

This can be translated to a delegate instantiation that references a compiler generated static method in which the code of the anonymous function is placed:
```csharp
class Test
{
    static void F() {
        D d = new D(__Method1);
    }

    static void __Method1() {
        Console.WriteLine("test");
    }
}
```

In the following example, the anonymous function references instance members of `this`:
```csharp
class Test
{
    int x;

    void F() {
        D d = () => { Console.WriteLine(x); };
    }
}
```

This can be translated to a compiler generated instance method containing the code of the anonymous function:
```csharp
class Test
{
    int x;

    void F() {
        D d = new D(__Method1);
    }

    void __Method1() {
        Console.WriteLine(x);
    }
}
```

In this example, the anonymous function captures a local variable:
```csharp
class Test
{
    void F() {
        int y = 123;
        D d = () => { Console.WriteLine(y); };
    }
}
```

The lifetime of the local variable must now be extended to at least the lifetime of the anonymous function delegate. This can be achieved by "hoisting" the local variable into a field of a compiler generated class. Instantiation of the local variable ([Instantiation of local variables](expressions.md#instantiation-of-local-variables)) then corresponds to creating an instance of the compiler generated class, and accessing the local variable corresponds to accessing a field in the instance of the compiler generated class. Furthermore, the anonymous function becomes an instance method of the compiler generated class:
```csharp
class Test
{
    void F() {
        __Locals1 __locals1 = new __Locals1();
        __locals1.y = 123;
        D d = new D(__locals1.__Method1);
    }

    class __Locals1
    {
        public int y;

        public void __Method1() {
            Console.WriteLine(y);
        }
    }
}
```

Finally, the following anonymous function captures `this` as well as two local variables with different lifetimes:
```csharp
class Test
{
    int x;

    void F() {
        int y = 123;
        for (int i = 0; i < 10; i++) {
            int z = i * 2;
            D d = () => { Console.WriteLine(x + y + z); };
        }
    }
}
```

Here, a compiler generated class is created for each statement block in which locals are captured such that the locals in the different blocks can have independent lifetimes. An instance of `__Locals2`, the compiler generated class for the inner statement block, contains the local variable `z` and a field that references an instance of `__Locals1`.  An instance of `__Locals1`, the compiler generated class for the outer statement block, contains the local variable `y` and a field that references `this` of the enclosing function member. With these data structures it is possible to reach all captured outer variables through an instance of `__Local2`, and the code of the anonymous function can thus be implemented as an instance method of that class.

```csharp
class Test
{
    void F() {
        __Locals1 __locals1 = new __Locals1();
        __locals1.__this = this;
        __locals1.y = 123;
        for (int i = 0; i < 10; i++) {
            __Locals2 __locals2 = new __Locals2();
            __locals2.__locals1 = __locals1;
            __locals2.z = i * 2;
            D d = new D(__locals2.__Method1);
        }
    }

    class __Locals1
    {
        public Test __this;
        public int y;
    }

    class __Locals2
    {
        public __Locals1 __locals1;
        public int z;

        public void __Method1() {
            Console.WriteLine(__locals1.__this.x + __locals1.y + z);
        }
    }
}
```

The same technique applied here to capture local variables can also be used when converting anonymous functions to expression trees: References to the compiler generated objects can be stored in the expression tree, and access to the local variables can be represented as field accesses on these objects. The advantage of this approach is that it allows the "lifted" local variables to be shared between delegates and expression trees.

## Method group conversions

An implicit conversion ([Implicit conversions](conversions.md#implicit-conversions)) exists from a method group ([Expression classifications](expressions.md#expression-classifications)) to a compatible delegate type. Given a delegate type `D` and an expression `E` that is classified as a method group, an implicit conversion exists from `E` to `D` if `E` contains at least one method that is applicable in its normal form ([Applicable function member](expressions.md#applicable-function-member)) to an argument list constructed by use of the parameter types and modifiers of `D`, as described in the following.

The compile-time application of a conversion from a method group `E` to a delegate type `D` is described in the following. Note that the existence of an implicit conversion from `E` to `D` does not guarantee that the compile-time application of the conversion will succeed without error.

*  A single method `M` is selected corresponding to a method invocation ([Method invocations](expressions.md#method-invocations)) of the form `E(A)`, with the following modifications:
    * The argument list `A` is a list of expressions, each classified as a variable and with the type and modifier (`ref` or `out`) of the corresponding parameter in the *formal_parameter_list* of `D`.
    * The candidate methods considered are only those methods that are applicable in their normal form ([Applicable function member](expressions.md#applicable-function-member)), not those applicable only in their expanded form.
*  If the algorithm of [Method invocations](expressions.md#method-invocations) produces an error, then a compile-time error occurs. Otherwise the algorithm produces a single best method `M` having the same number of parameters as `D` and the conversion is considered to exist.
*  The selected method `M` must be compatible ([Delegate compatibility](delegates.md#delegate-compatibility)) with the delegate type `D`, or otherwise, a compile-time error occurs.
*  If the selected method `M` is an instance method, the instance expression associated with `E` determines the target object of the delegate.
*  If the selected method M is an extension method which is denoted by means of a member access on an instance expression, that instance expression determines the target object of the delegate.
*  The result of the conversion is a value of type `D`, namely a newly created delegate that refers to the selected method and target object.
*  Note that this process can lead to the creation of a delegate to an extension method, if the algorithm of [Method invocations](expressions.md#method-invocations) fails to find an instance method but succeeds in processing the invocation of `E(A)` as an extension method invocation ([Extension method invocations](expressions.md#extension-method-invocations)). A delegate thus created captures the extension method as well as its first argument.

The following example demonstrates method group conversions:
```csharp
delegate string D1(object o);

delegate object D2(string s);

delegate object D3();

delegate string D4(object o, params object[] a);

delegate string D5(int i);

class Test
{
    static string F(object o) {...}

    static void G() {
        D1 d1 = F;            // Ok
        D2 d2 = F;            // Ok
        D3 d3 = F;            // Error -- not applicable
        D4 d4 = F;            // Error -- not applicable in normal form
        D5 d5 = F;            // Error -- applicable but not compatible

    }
}
```

The assignment to `d1` implicitly converts the method group `F` to a value of type `D1`.

The assignment to `d2` shows how it is possible to create a delegate to a method that has less derived (contravariant) parameter types and a more derived (covariant) return type.

The assignment to `d3` shows how no conversion exists if the method is not applicable.

The assignment to `d4` shows how the method must be applicable in its normal form.

The assignment to `d5` shows how parameter and return types of the delegate and method are allowed to differ only for reference types.

As with all other implicit and explicit conversions, the cast operator can be used to explicitly perform a method group conversion. Thus, the example
```csharp
object obj = new EventHandler(myDialog.OkClick);
```
could instead be written
```csharp
object obj = (EventHandler)myDialog.OkClick;
```

Method groups may influence overload resolution, and participate in type inference. See [Function members](expressions.md#function-members) for further details.

The run-time evaluation of a method group conversion proceeds as follows:

*  If the method selected at compile-time is an instance method, or it is an extension method which is accessed as an instance method, the target object of the delegate is determined from the instance expression associated with `E`:
    * The instance expression is evaluated. If this evaluation causes an exception, no further steps are executed.
    * If the instance expression is of a *reference_type*, the value computed by the instance expression becomes the target object. If the selected method is an instance method and the target object is `null`, a `System.NullReferenceException` is thrown and no further steps are executed.
    * If the instance expression is of a *value_type*, a boxing operation ([Boxing conversions](types.md#boxing-conversions)) is performed to convert the value to an object, and this object becomes the target object.
*  Otherwise the selected method is part of a static method call, and the target object of the delegate is `null`.
*  A new instance of the delegate type `D` is allocated. If there is not enough memory available to allocate the new instance, a `System.OutOfMemoryException` is thrown and no further steps are executed.
*  The new delegate instance is initialized with a reference to the method that was determined at compile-time and a reference to the target object computed above.
