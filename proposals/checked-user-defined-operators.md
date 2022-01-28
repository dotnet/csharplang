# Checked user-defined operators

## Summary
[summary]: #summary

C# should support defining `checked` variants of the following user-defined operators so that users can opt into or out of overflow behavior as appropriate:
*  The `++` and `--` unary operators (https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#postfix-increment-and-decrement-operators and https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#prefix-increment-and-decrement-operators).
*  The `-` unary operator (https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#unary-minus-operator).
*  The `+`, `-`, `*`, and `/` binary operators (https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#arithmetic-operators).
*  Explicit conversion operators.

## Motivation
[motivation]: #motivation

There is no way for a user to declare a type and support both checked and unchecked versions of an operator. This will make it hard to port various algorithms to use the proposed `generic math` interfaces exposed by the libraries team. Likewise, this makes it impossible to expose a type such as `Int128` or `UInt128` without the language simultaneously shipping its own support to avoid breaking changes.

## Detailed design
[design]: #detailed-design

### Syntax

Grammar at https://github.com/dotnet/csharplang/blob/main/spec/classes.md#operators will be adjusted to allow
`checked` keyword after the `operator` keyword right before the operator token:
```antlr
overloadable_unary_operator
    : '+' | 'checked'? '-' | '!' | '~' | 'checked'? '++' | 'checked'? '--' | 'true' | 'false'
    ;

overloadable_binary_operator
    : 'checked'? '+'   | 'checked'? '-'   | 'checked'? '*'   | 'checked'? '/'   | '%'   | '&'   | '|'   | '^'   | '<<'
    | right_shift | '=='  | '!='  | '>'   | '<'   | '>='  | '<='
    ;
    
conversion_operator_declarator
    : 'implicit' 'operator' type '(' type identifier ')'
    | 'explicit' 'operator' 'checked'? type '(' type identifier ')'
    ;    
```

For example:
``` C#
public static T operator checked ++(T x) {...}
public static T operator checked --(T x) {...}
public static T operator checked -(T x) {...}
public static T operator checked +(T lhs, T rhs) {...}
public static T operator checked -(T lhs, T rhs) {...}
public static T operator checked *(T lhs, T rhs) {...}
public static T operator checked /(T lhs, T rhs) {...}
public static explicit operator checked U(T x) {...}
```

``` C#
public static T I1.operator checked ++(T x) {...}
public static T I1.operator checked --(T x) {...}
public static T I1.operator checked -(T x) {...}
public static T I1.operator checked +(T lhs, T rhs) {...}
public static T I1.operator checked -(T lhs, T rhs) {...}
public static T I1.operator checked *(T lhs, T rhs) {...}
public static T I1.operator checked /(T lhs, T rhs) {...}
public static explicit I1.operator checked U(T x) {...}
```

For brevity below, an operator with the `checked` keyword is referred to as a `checked operator` and an operator without it is referred to as a `regular operator`. These terms are not applicable to operators that don't have a `checked` form.

### Semantics

A user-defined `checked operator` is expected to throw an exception when the result of an operation is too large to represent in the destination type. What does it mean to be too large actually depends on the nature of the destination type and is not prescribed by the language. Typically the exception thrown is a `System.OverflowException`, but the language doesn't have any specific requirements regarding this.

A user-defined `regular operator` is expected to not throw an exception when the result of an operation is too large to represent in the destination type. Instead, it is expected to return an instance representing a truncated result. What does it mean to be too large and to be truncated actually depends on the nature of the destination type and is not prescribed by the language. 

All existing user-defined operators out there that will have `checked` form supported fall into the category of `regular operators`. It is understood that many of them are likely to not follow the semantics specified above, but for the purpose of semantic analysis, compiler will assume that they are.

### Checked vs. unchecked context within a `checked operator`

Checked/unchecked context within the body of a `checked operator` is not affected by the presence of the `checked` keyword. In other words, the context is the same as immediately at the beginning of the operator declaration. The developer would need to explicitly switch the context if part of their algorithm cannot rely on default context.

### Names in metadata

Section "I.10.3.1 Unary operators" of ECMA-335 will be adjusted to include *op_CheckedIncrement*, *op_CheckedDecrement*, *op_CheckedUnaryNegation* as the names for methods implementing checked `++`, `--` and `-` unary operators.

Section "I.10.3.2 Binary operators" of ECMA-335 will be adjusted to include *op_CheckedAddition*, *op_CheckedSubtraction*,
*op_CheckedMultiply*, *op_CheckedDivision* as the names for methods implementing checked `+`, `-`, `*`, and `/` binary operators.

Section "I.10.3.3 Conversion operators" of ECMA-335 will be adjusted to include *op_CheckedExplicit* as the name for a method
implementing checked explicit conversion operator.

### Unary operators

Unary `checked operators` follow the rules from https://github.com/dotnet/csharplang/blob/main/spec/classes.md#unary-operators.

### Unary operator overload resolution

Assuming that `regular operator` matches `unchecked` evaluation context, `checked operator` matches `checked` evaluation context
and an operator that doesn't have `checked` form matches either context, the first bullet in https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#unary-operator-overload-resolution:
>*  The set of candidate user-defined operators provided by `X` for the operation `operator op(x)` is determined using the rules of [Candidate user-defined operators](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#candidate-user-defined-operators).

will be replaced with the following two bullet points:
*  The set of candidate user-defined operators provided by `X` for the operation `operator op(x)` **matching the current checked/unchecked context** is determined using the rules of [Candidate user-defined operators](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#candidate-user-defined-operators).
*  If the set of candidate user-defined operators is not empty, then this becomes the set of candidate operators for the operation. Otherwise, the set of candidate user-defined operators provided by `X` for the operation `operator op(x)` **matching the opposite checked/unchecked context** is determined using the rules of [Candidate user-defined operators](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#candidate-user-defined-operators).

The https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#the-checked-and-unchecked-operators section will be adjusted to reflect the effect that the checked/unchecked context has on unary operator overload resolution.

### Binary operators

Binary `checked operators` follow the rules from https://github.com/dotnet/csharplang/blob/main/spec/classes.md#binary-operators.

### Binary operator overload resolution

Assuming that `regular operator` matches `unchecked` evaluation context, `checked operator` matches `checked` evaluation context
and an operator that doesn't have `checked` form matches either context, the first bullet in https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#binary-operator-overload-resolution:
>*  The set of candidate user-defined operators provided by `X` and `Y` for the operation `operator op(x,y)` is determined. The set consists of the union of the candidate operators provided by `X` and the candidate operators provided by `Y`, each determined using the rules of [Candidate user-defined operators](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#candidate-user-defined-operators). If `X` and `Y` are the same type, or if `X` and `Y` are derived from a common base type, then shared candidate operators only occur in the combined set once.

will be replaced with the following two bullet points:
*  The set of candidate user-defined operators provided by `X` and `Y` for the operation `operator op(x,y)` **matching the current checked/unchecked context** is determined. The set consists of the union of the candidate operators provided by `X` and the candidate operators provided by `Y`, each determined using the rules of [Candidate user-defined operators](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#candidate-user-defined-operators). If `X` and `Y` are the same type, or if `X` and `Y` are derived from a common base type, then shared candidate operators only occur in the combined set once.
*  If the set of candidate user-defined operators is not empty, then this becomes the set of candidate operators for the operation. Otherwise, the set of candidate user-defined operators provided by `X` and `Y` for the operation `operator op(x,y)` **matching the opposite checked/unchecked context** is determined. The set consists of the union of the candidate operators provided by `X` and the candidate operators provided by `Y`, each determined using the rules of [Candidate user-defined operators](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#candidate-user-defined-operators). If `X` and `Y` are the same type, or if `X` and `Y` are derived from a common base type, then shared candidate operators only occur in the combined set once.

The https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#the-checked-and-unchecked-operators section will be adjusted to reflect the effect that the checked/unchecked context has on binary operator overload resolution.

``` C#
public class MyClass
{
    public static void Add(Int128 lhs, Int128 rhs)
    {
        // Resolves to `op_CheckedAddition`
        Int128 r1 = checked(lhs + rhs);

        // Resolves to `op_Addition`
        Int128 r2 = unchecked(lhs + rhs);

        // Resolve to `op_Subtraction`
        Int128 r3 = checked(lhs - rhs);

        // Resolve to `op_Subtraction`
        Int128 r4 = unchecked(lhs - rhs);

        // Resolves to `op_CheckedMultiply`
        Int128 r5 = checked(lhs * rhs);

        // Resolves to `op_CheckedMultiply`
        Int128 r5 = unchecked(lhs * rhs);
    }

    public static void Divide(Int128 lhs, byte rhs)
    {
        // Resolves to `op_CheckedDivision`
        Int128 r4 = checked(lhs / rhs);
    }
}

public struct Int128
{
    public static Int128 operator checked +(Int128 lhs, Int128 rhs);
    public static Int128 operator +(Int128 lhs, Int128 rhs);

    public static Int128 operator -(Int128 lhs, Int128 rhs);

    public static Int128 operator checked *(Int128 lhs, Int128 rhs);

    public static Int128 operator checked /(Int128 lhs, int rhs);
    public static Int128 operator /(Int128 lhs, byte rhs);
}
```

### Conversion operators

Conversion `checked operators` follow the rules from https://github.com/dotnet/csharplang/blob/main/spec/classes.md#conversion-operators.

### Processing of user-defined explicit conversions 

Assuming that `regular operator` matches `unchecked` evaluation context and `checked operator` matches `checked` evaluation context,
the third bullet in https://github.com/dotnet/csharplang/blob/main/spec/conversions.md#processing-of-user-defined-explicit-conversions:
>*  Find the set of applicable user-defined and lifted conversion operators, `U`. This set consists of the user-defined and lifted implicit or explicit conversion operators declared by the classes or structs in `D` that convert from a type encompassing or encompassed by `S` to a type encompassing or encompassed by `T`. If `U` is empty, the conversion is undefined and a compile-time error occurs.

will be replaced with the following bullet points:
*  Find the set of applicable user-defined and lifted explicit conversion operators **matching the current checked/unchecked context**, `U0`. This set consists of the user-defined and lifted explicit conversion operators declared by the classes or structs in `D` that **match the current checked/unchecked context** and convert from a type encompassing or encompassed by `S` to a type encompassing or encompassed by `T`.
*  Find the set of applicable user-defined and lifted explicit conversion operators **matching the opposite checked/unchecked context**, `U1`. If `U0` is not empty, `U1` is empty. Otherwise, this set consists of the user-defined and lifted explicit conversion operators declared by the classes or structs in `D` that **match the opposite checked/unchecked context** and convert from a type encompassing or encompassed by `S` to a type encompassing or encompassed by `T`.
*  Find the set of applicable user-defined and lifted conversion operators, `U`. This set consists of operators from `U0`, `U1`, and the user-defined and lifted implicit conversion operators declared by the classes or structs in `D` that convert from a type encompassing or encompassed by `S` to a type encompassing or encompassed by `T`. If `U` is empty, the conversion is undefined and a compile-time error occurs.

The https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#the-checked-and-unchecked-operators section will be adjusted to reflect the effect that the checked/unchecked context has on processing of user-defined explicit conversions.

### Implementing operators

A `checked operator` does not implement a `regular operator` and vice versa.

### Linq Expression Trees 

`Checked operators` will be supported in Linq Expression Trees. A UnaryExpression/BinaryExpression node will be created with corresponding `MethodInfo`.

### Dynamic

It should be possible to adjust runtime binder to support `checked operators` in `dynamic` invocations.

## Drawbacks
[drawbacks]: #drawbacks

This adds additional complexity to the language and allows users to introduce more kinds of breaking changes to their types.

## Alternatives
[alternatives]: #alternatives

The generic math interfaces that the libraries plans to expose could expose named methods (such as `AddChecked`). The primary drawback is that this is less readable/maintainable and doesn't get the benefit of the language precedence rules around operators.

### Placement of the `checked` keyword

Alternatively the `checked` keyword could be moved to the place right before the `operator` keyword:  
``` C#
public static T checked operator ++(T x) {...}
public static T checked operator --(T x) {...}
public static T checked operator -(T x) {...}
public static T checked operator +(T lhs, T rhs) {...}
public static T checked operator -(T lhs, T rhs) {...}
public static T checked operator *(T lhs, T rhs) {...}
public static T checked operator /(T lhs, T rhs) {...}
public static explicit checked operator U(T x) {...}
```

``` C#
public static T checked I1.operator ++(T x) {...}
public static T checked I1.operator --(T x) {...}
public static T checked I1.operator -(T x) {...}
public static T checked I1.operator +(T lhs, T rhs) {...}
public static T checked I1.operator -(T lhs, T rhs) {...}
public static T checked I1.operator *(T lhs, T rhs) {...}
public static T checked I1.operator /(T lhs, T rhs) {...}
public static explicit checked I1.operator U(T x) {...}
```

Or it could be moved into the set of operator modifiers:
```antlr
operator_modifier
    : 'public'
    | 'static'
    | 'extern'
    | 'checked'
    | operator_modifier_unsafe
    ;
```

``` C#
public static checked T operator ++(T x) {...}
public static checked T operator --(T x) {...}
public static checked T operator -(T x) {...}
public static checked T operator +(T lhs, T rhs) {...}
public static checked T operator -(T lhs, T rhs) {...}
public static checked T operator *(T lhs, T rhs) {...}
public static checked T operator /(T lhs, T rhs) {...}
public static checked explicit operator U(T x) {...}
```

``` C#
public static checked T I1.operator ++(T x) {...}
public static checked T I1.operator --(T x) {...}
public static checked T I1.operator -(T x) {...}
public static checked T I1.operator +(T lhs, T rhs) {...}
public static checked T I1.operator -(T lhs, T rhs) {...}
public static checked T I1.operator *(T lhs, T rhs) {...}
public static checked T I1.operator /(T lhs, T rhs) {...}
public static checked explicit I1.operator U(T x) {...}
```
    
### `unchecked` keyword

There were suggestions to support `unchecked` keyword at the same position as the `checked` keyword
with the following possible meanings:
- Simply to explicitly reflect the regular nature of the operator, or
- Perhaps to designate a distinct flavor of an operator that is supposed to be used in an `unchecked` context. The language could support `op_Addition`, `op_CheckedAddition`, and `op_UncheckedAddition` to help limit the number of breaking changes. This adds another layer of complexity that is likely not necessary in most code.

### Operator names in ECMA-335

Alternatively the operator names could be *op_UnaryNegationChecked*, *op_AdditionChecked*, *op_SubtractionChecked*,
*op_MultiplyChecked*, *op_DivisionChecked*, with *Checked* at the end. However, it looks like there is already a pattern
established to end the names with the operator word. For example, there is a *op_UnsignedRightShift* operator rather than
*op_RightShiftUnsigned* operator.

### Require definition of corresponding `regular operator`

The compiler could require that if a `checked operator` is defined, a corresponding `regular operator` is also defined.
For example:
``` C#
public struct Int128
{
    // This is fine, both a checked and regular operator are defined
    public static Int128 operator checked +(Int128 lhs, Int128 rhs);
    public static Int128 operator +(Int128 lhs, Int128 rhs);

    // This is fine, only a regular operator is defined
    public static Int128 operator -(Int128 lhs, Int128 rhs);

    // This should error, a regular operator must also be defined
    public static Int128 operator checked *(Int128 lhs, Int128 rhs);
}
```

However, it feels like it should be fine to define only a checked flavor.

### `Checked operators` are inapplicable in an `unchecked` context

The compiler, when performing member lookup to find candidate user-defined operators within an `unchecked` context, could ignore `checked operators`. If metadata is encountered that only defines a `checked operator`, then a compilation error will occur.
``` C#
public class MyClass
{
    public static void Add(Int128 lhs, Int128 rhs)
    {
        // Resolves to `op_CheckedMultiply`
        Int128 r5 = checked(lhs * rhs);

        // Error: Operator '*' cannot be applied to operands of type 'Int128' and 'Int128'
        Int128 r5 = unchecked(lhs * rhs);
    }
}

public struct Int128
{
    public static Int128 operator checked *(Int128 lhs, Int128 rhs);
}
```

### More complicated operator lookup and overload resolution rules in a `checked` context

The compiler, when performing member lookup to find candidate user-defined operators within a `checked` context will also consider applicable operators ending with `Checked`. That is, if the compiler was attempting to find applicable function members for the binary addition operator, it would look for both `op_Addition` and `op_AdditionChecked`. If the only applicable function member is a `checked operator`, it will be used. If both a `regular operator` and `checked operator` exist and are equally applicable the `checked operator` will be preferred. If both a `regular operator` and a `checked operator` exist but the `regular operator` is an exact match while the `checked operator` is not, the compiler will prefer the `regular operator`.
``` C#
public class MyClass
{
    public static void Add(Int128 lhs, Int128 rhs)
    {
        // Resolves to `op_CheckedAddition`
        Int128 r1 = checked(lhs + rhs);

        // Resolves to `op_Addition`
        Int128 r2 = unchecked(lhs + rhs);

        // Resolve to `op_Subtraction`
        Int128 r3 = checked(lhs - rhs);

        // Resolve to `op_Subtraction`
        Int128 r4 = unchecked(lhs - rhs);
    }

    public static void Multiply(Int128 lhs, byte rhs)
    {
        // Resolves to `op_Multiply` even though `op_CheckedMultiply` is also applicable
        Int128 r4 = checked(lhs * rhs);
    }
}

public struct Int128
{
    public static Int128 operator checked +(Int128 lhs, Int128 rhs);
    public static Int128 operator +(Int128 lhs, Int128 rhs);

    public static Int128 operator -(Int128 lhs, Int128 rhs);

    public static Int128 operator checked *(Int128 lhs, int rhs);
    public static Int128 operator *(Int128 lhs, byte rhs);
}
```

### Checked vs. unchecked context within a `checked operator`

The compiler could treat the default context of a `checked operator` as checked. The developer would need to explicitly use `unchecked` if part of their algorithm should not participate in the `checked context`. However, this might not work well in the future if we start allowing `checked`/`unchecked` tokens as modifiers on operators to set the context within the body. The modifier and the keyword could contradict each other. Also, we wouldn't be able to do the same (treat default context as unchecked) for a `regular operator` because that would be a breaking change.

## Unresolved questions
[unresolved]: #unresolved-questions

Should the language allow `checked` and `unchecked` modifiers on methods (e.g. `static checked void M()`)?
This would allow removing nesting levels for methods that require it.

### Should we support implicit checked conversion operators?

In general, implicit conversion operators are not supposed to throw.

*Proposal:*
No.

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
