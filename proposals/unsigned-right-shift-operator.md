# Unsigned right shift operator

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

### Shift operators

The https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#shift-operators section will be adjusted
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

For an operation of the form `x << count` or `x >> count` or `x >>> count`, binary operator overload resolution ([Binary operator overload resolution](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#binary-operator-overload-resolution)) is applied to select a specific operator implementation. The operands are converted to the parameter types of the selected operator, and the type of the result is the return type of the operator.

The predefined unsigned right shift operators are listed below.

*  Shift right:

   ```csharp
   int operator >>>(int x, int count);
   uint operator >>>(uint x, int count);
   long operator >>>(long x, int count);
   ulong operator >>>(ulong x, int count);
   ```

   The `>>>` operator shifts `x` right by a number of bits computed as described below.

   The low-order bits of `x` are discarded, the remaining bits are shifted right, and the high-order empty bit positions are set to zero.

For the predefined operators, the number of bits to shift is computed as follows:

*  When the type of `x` is `int` or `uint`, the shift count is given by the low-order five bits of `count`. In other words, the shift count is computed from `count & 0x1F`.
*  When the type of `x` is `long` or `ulong`, the shift count is given by the low-order six bits of `count`. In other words, the shift count is computed from `count & 0x3F`.

If the resulting shift count is zero, the shift operators simply return the value of `x`.

Shift operations never cause overflows and produce the same results in `checked` and `unchecked` contexts.

### Constant expressions

Operator `>>>` will be added to the set of constructs permitted in constant expressions at
https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#constant-expressions.

### Operator overloading

Operator `>>>` will be added to the set of overloadable binary operators at https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#operator-overloading.

### Lifted operators

Operator `>>>` will be added to the set of binary operators permitting a lifted form at https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#lifted-operators.

### Operator precedence and associativity

The https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#operator-precedence-and-associativity section will be adjusted to add `>>>` operator to the "Shift" category and `>>>=` operator to the "Assignment and lambda expression" category.

### Grammar ambiguities

The `>>>` operator is subject to the same grammar ambiguities described at https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#grammar-ambiguities as a regular `>>` operator.

### Operators

The https://github.com/dotnet/csharplang/blob/main/spec/classes.md#operators will be adjusted to include `>>>` operator.

```antlr
overloadable_binary_operator
    : '+'   | '-'   | '*'   | '/'   | '%'   | '&'   | '|'   | '^'   | '<<'
    | right_shift | unsigned_right_shift | '=='  | '!='  | '>'   | '<'   | '>='  | '<='
    ;
```

### Binary operators

The signature of a `>>>` operator is subject to the same rules as those at https://github.com/dotnet/csharplang/blob/main/spec/classes.md#binary-operators
for the signature of a `>>` operator.

### Metadata name

Section "I.10.3.2 Binary operators" of ECMA-335 already reserved the name for an unsigned right shift operator - op_UnsignedRightShift.

### Dynamic?

### Expression Tree?

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
