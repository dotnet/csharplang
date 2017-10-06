# Support for == and != on tuple types

Allow expressions `t1 == t2` where `t1` and `t2` are tuple types of same cardinality, and evaluate them as `temp1.Item1 == temp2.Item1 && temp1.Item2 == temp2.Item2` (assuming `var temp1 = t1; var temp2 = t2;`).

Conversely it would allow `t1 != t2` and evaluate it as `temp1.Item1 != temp2.Item1 || temp1.Item2 != temp2.Item2`.

As of C# 7.2, such code produces an error (`error CS0019: Operator '==' cannot be applied to operands of type '(...)' and '(...)'`).

## Details

When binding the `==` (or `!=`) operator, if both operands of a comparison operator are tuples (have tuple types or are tuple literals) and have matching cardinality, then the comparison is performed element-wise.

Both operands (and, in the case of tuple literals, their elements) are evaluated in order from left to right. Each pair of elements is then used as operands to bind the operator `==` (or `!=`), recursively. Any elements with compile-time type `dynamic` cause an error. The results of those element-wise comparisons are used as operands in a chain of conditional AND (or OR) operators.

For instance, in the context of `(int, (int, int)) t1, t2;`, `t1 == (1, (2, 3))` would evaluate as `temp1.Item1 == temp2.Item1 && temp1.Item2.Item1 == temp2.Item2.Item1 && temp2.Item2.Item2 == temp2.Item2.Item2`.

When a tuple literal is used as operand (on either side), it receives a converted tuple type formed by the element-wise conversions which are introduced when binding the operator `==` (or `!=`) element-wise. 

For instance, in `(1L, 2, "hello") == (1, 2L, null)`, the converted type for both tuple literals is `(long, long, string)` and the second literal has no natural type.

## Evaluation order
The left-hand-side value is evaluated first (including conversions), then the right-hand-side value (also including conversions), then the element-wise comparisons from left to right (with early exit based on existing rules for conditional AND/OR operators).

For instance, if there is a conversion from type `A` to type `B` and a method `(A, A) GetTuple()`, evaluating `(new A(1), (new B(2), new B(3))) == (new B(4), GetTuple())` means:
- `new A(1)` and convert to `B`
- `new B(2)`
- `new B(3)`
- `new A(1)` and convert to `B`
- `GetTuple()` and convert to `(B, B)`
- then the element-wise comparisons and conditional logic is evaluated

## Compatibility

If someone wrote their own `ValueTuple` types, including an implementation of the comparison operator, the proposed rule would cause their operator to be ignored.

----

Relates to https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#relational-and-type-testing-operators
Relates to https://github.com/dotnet/csharplang/issues/190
