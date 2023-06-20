# Unsigned right shift operator

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
[summary]: #summary

An unsigned right shift operator will be supported by C# as a built-in operator (for primitive integral types) and as a user-defined operator. 

## Motivation
[motivation]: #motivation

When working with signed integral value, it is not uncommon that you need to shift bits right without replicating
the high order bit on each shift. While this can be achieved for primitive integral types with a regular shift
operator, a cast to an unsigned type before the shift operation and a cast back after it is required. Within the
context of the generic math interfaces the libraries are planning to expose, this is potentially more problematic
as the type might not necessary have an unsigned counterpart defined or known upfront by the generic math code,
yet an algorithm might rely on ability to perform an unsigned right shift operation.

## Detailed design
[design]: #detailed-design

### Operators and punctuators

Section [§6.4.6](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/lexical-structure.md#646-operators-and-punctuators) will be adjusted
to include `>>>` operator - the unsigned right shift operator:

```antlr
unsigned_right_shift
    : '>>>'
    ;

unsigned_right_shift_assignment
    : '>>>='
    ;
```

No characters of any kind (not even whitespace) are allowed between the tokens in *unsigned_right_shift* and *unsigned_right_shift_assignment* productions. These productions are treated specially in order to enable the correct  handling of *type_parameter_list*s.

### Shift operators

Section [§11.10](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1110-shift-operators) will be adjusted
to include `>>>` operator - the unsigned right shift operator:

The `<<`, `>>` and `>>>` operators are used to perform bit shifting operations.

```antlr
shift_expression
    : additive_expression
    | shift_expression '<<' additive_expression
    | shift_expression right_shift additive_expression
    | shift_expression unsigned_right_shift additive_expression
    ;
```

For an operation of the form `x << count` or `x >> count` or `x >>> count`, binary operator overload resolution ([§11.4.5](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1145-binary-operator-overload-resolution)) is applied to select a specific operator implementation. The operands are converted to the parameter types of the selected operator, and the type of the result is the return type of the operator.

The predefined unsigned shift operators will support the same set of signatures
that predefined signed shift operators support today in the current implementation.

*  Shift right:

   ```csharp
   int operator >>>(int x, int count);
   uint operator >>>(uint x, int count);
   long operator >>>(long x, int count);
   ulong operator >>>(ulong x, int count);
   nint operator >>>(nint x, int count);
   nuint operator >>>(nuint x, int count);
   ```

   The `>>>` operator shifts `x` right by a number of bits computed as described below.

   The low-order bits of `x` are discarded, the remaining bits are shifted right, and the high-order empty bit positions are set to zero.

For the predefined operators, the number of bits to shift is computed as follows:

*  When the type of `x` is `int` or `uint`, the shift count is given by the low-order five bits of `count`. In other words, the shift count is computed from `count & 0x1F`.
*  When the type of `x` is `long` or `ulong`, the shift count is given by the low-order six bits of `count`. In other words, the shift count is computed from `count & 0x3F`.

If the resulting shift count is zero, the shift operators simply return the value of `x`.

Shift operations never cause overflows and produce the same results in `checked` and `unchecked` contexts.

### Assignment operators

Section [§11.18](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1118-assignment-operators) will be adjusted to include
*unsigned_right_shift_assignment* as follows:

```antlr
assignment_operator
    : '='
    | '+='
    | '-='
    | '*='
    | '/='
    | '%='
    | '&='
    | '|='
    | '^='
    | '<<='
    | right_shift_assignment
    | unsigned_right_shift_assignment
    ;
```

### Integral types

The Integral types [§8.3.6](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/types.md#836-integral-types) section will be adjusted to include information about `>>>` operator. The relevant bullet point is the following:

*  For the binary `<<`, `>>` and `>>>` operators, the left operand is converted to type `T`, where `T` is the first of `int`, `uint`, `long`, and `ulong` that can fully represent all possible values of the operand. The operation is then performed using the precision of type `T`, and the type of the result is `T`.

### Constant expressions

Operator `>>>` will be added to the set of constructs permitted in constant expressions at
[§11.20](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1120-constant-expressions).

### Operator overloading

Operator `>>>` will be added to the set of overloadable binary operators at [§11.4.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1143-operator-overloading).

### Lifted operators

Operator `>>>` will be added to the set of binary operators permitting a lifted form at [§11.4.8](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1148-lifted-operators).

### Operator precedence and associativity

Section [§11.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1142-operator-precedence-and-associativity) will be adjusted to add `>>>` operator to the "Shift" category and `>>>=` operator to the "Assignment and lambda expression" category.

### Grammar ambiguities

The `>>>` operator is subject to the same grammar ambiguities described at [§6.2.5](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/lexical-structure.md#625-grammar-ambiguities) as a regular `>>` operator.

### Operators

The [§14.10](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/classes.md#1410-operators) section will be adjusted to include `>>>` operator.

```antlr
overloadable_binary_operator
    : '+'   | '-'   | '*'   | '/'   | '%'   | '&'   | '|'   | '^'   | '<<'
    | right_shift | unsigned_right_shift | '=='  | '!='  | '>'   | '<'   | '>='  | '<='
    ;
```

### Binary operators

The signature of a `>>>` operator is subject to the same rules as those at [§14.10.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/classes.md#14103-binary-operators)
for the signature of a `>>` operator.

### Metadata name

Section "I.10.3.2 Binary operators" of ECMA-335 already reserved the name for an unsigned right shift operator - op_UnsignedRightShift.

### Linq Expression Trees

The `>>>` operator will not be supported in Linq Expression Trees because semantics of predefined `>>>` operators on signed types cannot be accurately represented without adding conversions to an unsigned type and back. See https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-09.md#unsigned-right-shift-operator for more information.

### Dynamic Binding 

It looks like dynamic binding uses values of System.Linq.Expressions.ExpressionType enum to communicate
binary operator kind to the runtime binder. Since we don't have a member specifically representing
an unsigned right shift operator, dynamic binding for `>>>` operator will not be supported and the
static and dynamic binding ([§11.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#113-static-and-dynamic-binding)) section 
will be adjusted to reflect that.

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

## Alternatives
[alternatives]: #alternatives

### Linq Expression Trees

The `>>>` operator will be supported in Linq Expressioin Trees.
- For a user-defined operator, a BinaryExpression node pointing to the operator method will be created.
- For predefined operators
  -  when the first operand is an ansigned type, a BinaryExpression node will be created.
  -  when the first operand is a signed type, a conversion for the first operand to an unsigned type will be added, a BinaryExpression node will be created and conversion for the result back to the signed type will be added.

For example:
``` C#
Expression<System.Func<int, int, int>> z = (x, y) => x >>> y; // (x, y) => Convert((Convert(x, UInt32) >> y), Int32)
```

`Resolution:`

Rejected, see https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-09.md#unsigned-right-shift-operator for more information.


## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

## Design meetings

https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-09.md
