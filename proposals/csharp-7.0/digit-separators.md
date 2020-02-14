# Digit separators

This proposal specifies the changes required to the [C# 6.0 (draft) Language specification](../../spec/introduction.md) to support *literal digit separators*.

## Changes to [Lexical structure](../../spec/lexical-structure.md)

### Literals

#### Integer literals

> The grammar for [integer literals](../../spec/lexical-structure.md#Integer-literals) is modified to allow one or more `_` separators anywhere between the first and last digits.

Integer literals are used to write values of types `int`, `uint`, `long`, and `ulong`. Integer literals have three possible forms: decimal, hexadecimal, and binary.

```antlr
integer_literal
    : decimal_integer_literal
    | hexadecimal_integer_literal
    | binary_integer_literal
   ;

integer_type_suffix
    : 'U' | 'u' | 'L' | 'l' | 'UL' | 'Ul' | 'uL' | 'ul' | 'LU' | 'Lu' | 'lU' | 'lu'
    ;

decimal_integer_literal
    : decimal_digits integer_type_suffix?
    ;

decimal_digits
    : decimal_digit
    | decimal_digit decimal_digits_and_underscores? decimal_digit
    ;

decimal_digit
    : '0' | '1' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9'
    ;

decimal_digits_and_underscores
    : decimal_digit_or_underscore+
    ;

decimal_digit_or_underscore
    : decimal_digit
    | '_'
    ;

hexadecimal_integer_literal
    : '0x' hex_digits integer_type_suffix?
    | '0X' hex_digits integer_type_suffix?
    ;

hex_digits
    : hex_digit
    | hex_digit hex_digits_and_underscores? hex_digit
    ;

hex_digit
    : '0' | '1' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9'
    | 'A' | 'B' | 'C' | 'D' | 'E' | 'F' | 'a' | 'b' | 'c' | 'd' | 'e' | 'f'
    ;

hex_digits_and_underscores
    : hex_digit_or_underscore+
    ;

hex_digit_or_underscore
    : hex_digit
    | '_'
    ;

binary_integer_literal
    : '0b' binary_digits integer_type_suffix?
    | '0B' binary_digits integer_type_suffix?
    ;

binary_digits
    : binary_digit
    | binary_digit binary_digits_and_underscores? binary_digit
    ;

binary_digit
    : '0'
    | '1'
    ;

binary_digits_and_underscores
    : binary_digit_or_underscore+
    ;

binary_digit_or_underscore
    : binary_digit
    | '_'
    ;
```

The type of an integer literal is determined as follows:

*  If the literal has no suffix, it has the first of these types in which its value can be represented: `int`, `uint`, `long`, `ulong`.
*  If the literal is suffixed by `U` or `u`, it has the first of these types in which its value can be represented: `uint`, `ulong`.
*  If the literal is suffixed by `L` or `l`, it has the first of these types in which its value can be represented: `long`, `ulong`.
*  If the literal is suffixed by `UL`, `Ul`, `uL`, `ul`, `LU`, `Lu`, `lU`, or `lu`, it is of type `ulong`.

If the value represented by an integer literal is outside the range of the `ulong` type, a compile-time error occurs.

As a matter of style, it is suggested that "`L`" be used instead of "`l`" when writing literals of type `long`, since it is easy to confuse the letter "`l`" with the digit "`1`".

To permit the smallest possible `int` and `long` values to be written as decimal integer literals, the following two rules exist:

* When a *decimal_integer_literal* with the value 2147483648 (2^31) and no *integer_type_suffix* appears as the token immediately following a unary minus operator token ([Unary minus operator](expressions.md#unary-minus-operator)), the result is a constant of type `int` with the value -2147483648 (-2^31). In all other situations, such a *decimal_integer_literal* is of type `uint`.
* When a *decimal_integer_literal* with the value 9223372036854775808 (2^63) and no *integer_type_suffix* or the *integer_type_suffix* `L` or `l` appears as the token immediately following a unary minus operator token ([Unary minus operator](expressions.md#unary-minus-operator)), the result is a constant of type `long` with the value -9223372036854775808 (-2^63). In all other situations, such a *decimal_integer_literal* is of type `ulong`.

\[Example:
```csharp
123                  // decimal, int
10_543_765Lu         // decimal, ulong
1_2__3___4____5      // decimal, int

0xFf                 // hex, int
0X1b_a0_44_fEL       // hex, long
0x1ade_3FE1_29AaUL   // hex, ulong
0xabc_               // invalid; no trailing _ allowed

0b101                // binary, int
0B1001_1010u         // binary, uint
0b1111_1111_0000UL   // binary, ulong
0B__111              // invalid; no leading _ allowed
```
end example\]

#### Real literals

> The grammar for [real literals](../../spec/lexical-structure.md#Real-literals) is modified to allow `_` separators within decimals, fractions, and exponents, anywhere between the first and last digits.

Real literals are used to write values of types `float`, `double`, and `decimal`.

```antlr
real_literal
    : decimal_digits '.' decimal_digits exponent_part? real_type_suffix?
    | '.' decimal_digits exponent_part? real_type_suffix?
    | decimal_digits exponent_part real_type_suffix?
    | decimal_digits real_type_suffix
    ;

exponent_part
    : 'e' sign? decimal_digits
    | 'E' sign? decimal_digits
    ;

sign
    : '+'
    | '-'
    ;

real_type_suffix
    : 'F' | 'f' | 'D' | 'd' | 'M' | 'm'
    ;
```

If no *real_type_suffix* is specified, the type of the real literal is `double`. Otherwise, the real type suffix determines the type of the real literal, as follows:

*  A real literal suffixed by `F` or `f` is of type `float`. For example, the literals `1f`, `1.5f`, `1e10f`, and `123.456F` are all of type `float`.
*  A real literal suffixed by `D` or `d` is of type `double`. For example, the literals `1d`, `1.5d`, `1e10d`, and `123.456D` are all of type `double`.
*  A real literal suffixed by `M` or `m` is of type `decimal`. For example, the literals `1m`, `1.5m`, `1e10m`, and `123.456M` are all of type `decimal`. This literal is converted to a `decimal` value by taking the exact value, and, if necessary, rounding to the nearest representable value using banker's rounding ([The decimal type](types.md#the-decimal-type)). Any scale apparent in the literal is preserved unless the value is rounded or the value is zero (in which latter case the sign and scale will be 0). Hence, the literal `2.900m` will be parsed to form the decimal with sign `0`, coefficient `2900`, and scale `3`.

If the specified literal cannot be represented in the indicated type, a compile-time error occurs.

The value of a real literal of type `float` or `double` is determined by using the IEEE "round to nearest" mode.

\[Example:
```csharp
1.234_567              // double
.3e5f                  // float
2_345E-2_0             // double
15D                    // double
19.73M                 // decimal
1.F                    // invalid; ill-formed (parsed as "1." and "F")
1.234_                 // invalid; no trailing _ allowed in fraction
.3e5_F                 // invalid; no trailing _ allowed in exponent
```
end example\]
