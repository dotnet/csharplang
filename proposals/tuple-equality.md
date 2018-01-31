# Support for == and != on tuple types

Allow expressions `t1 == t2` where `t1` and `t2` are tuple or nullable tuple types of same cardinality, and evaluate them as `temp1.Item1 == temp2.Item1 && temp1.Item2 == temp2.Item2` (assuming `var temp1 = t1; var temp2 = t2;`).

Conversely it would allow `t1 != t2` and evaluate it as `temp1.Item1 != temp2.Item1 || temp1.Item2 != temp2.Item2`.

In the nullable case, additional checks for `temp1.HasValue` and `temp2.HasValue` are used. For instance, `nullableT1 == nullableT2` evaluates as `temp1.HasValue == temp2.HasValue ? (temp1.HasValue ? ... : true) : false`.

As of C# 7.2, such code produces an error (`error CS0019: Operator '==' cannot be applied to operands of type '(...)' and '(...)'`).

## Details

When binding the `==` (or `!=`) operator and existing binding rules failed, then if both operands of a comparison operator are tuples (have tuple types or are tuple literals) and have matching cardinality, then the comparison is performed element-wise. If either or both sides are nullable, then whether the nullables have values is checked before comparing elements.

Both operands (and, in the case of tuple literals, their elements) are evaluated in order from left to right. Each pair of elements is then used as operands to bind the operator `==` (or `!=`), recursively. Any elements with compile-time type `dynamic` cause an error. The results of those element-wise comparisons are used as operands in a chain of conditional AND (or OR) operators.

For instance, in the context of `(int, (int, int)) t1, t2;`, `t1 == (1, (2, 3))` would evaluate as `temp1.Item1 == temp2.Item1 && temp1.Item2.Item1 == temp2.Item2.Item1 && temp2.Item2.Item2 == temp2.Item2.Item2`.

When a tuple literal is used as operand (on either side), it receives a converted tuple type formed by the element-wise conversions which are introduced when binding the operator `==` (or `!=`) element-wise. 

For instance, in `(1L, 2, "hello") == (1, 2L, null)`, the converted type for both tuple literals is `(long, long, string)` and the second literal has no natural type.

## Evaluation order
The left-hand-side value is evaluated first, then the right-hand-side value, then the element-wise comparisons from left to right (including conversions, and with early exit based on existing rules for conditional AND/OR operators).

For instance, if there is a conversion from type `A` to type `B` and a method `(A, A) GetTuple()`, evaluating `(new A(1), (new B(2), new B(3))) == (new B(4), GetTuple())` means:
- `new A(1)`
- `new B(2)`
- `new B(3)`
- `new B(4)`
- `GetTuple()`
- then the element-wise conversions and comparisons and conditional logic is evaluated (convert `new A(1)` to type `B`, then compare it with `new B(4)`, and so on).

## Compatibility

Although the initial proposal had a compatiblity issue if someone wrote their own `ValueTuple` types with  an implementation of the comparison operator, the current proposal maintains the existing behavior because the new rule comes into play last.

----

Relates to https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#relational-and-type-testing-operators
Relates to https://github.com/dotnet/csharplang/issues/190
