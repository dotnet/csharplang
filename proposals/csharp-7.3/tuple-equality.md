# Support for == and != on tuple types

This proposal specifies the changes required to the [C# 7.2 (draft) Language specification](../../spec/introduction.md) to support equality and inequality operations on tuples.

## Relational and type-testing operators

...

> Add the following section after [Enumeration comparison operators](../../spec/expressions.md#enumeration-comparison-operators):

### Tuple comparison operators

The predefined tuple equality operators are:

```csharp
bool operator ==(Tup1 t1, Tup2 t2);
bool operator !=(Tup1 t1, Tup2 t2);
```

For any tuple or nullable tuple types `Tup1` and `Tup2`, `t1` and `t2` shall have the same number of elements, and operators `==` and `!=` shall be defined for the types of each corresponding element pair. Consider the following:

```csharp
(0, "abc") == (1, "xy")     // OK; same number of elements with == defined for int and string
(0, "abc") == (1.0, "xy")   // OK; same number of elements with == defined for int/double
(0, "abc") != (0L, "xy")    // OK; same number of elements with == defined for int/long
(0, "abc") != ("xy", 2)     // Error; == not defined for int/string or string/int
(0, "abc") == (1, "xy", 10) // Error; different number of elements
```

The result of `==` is `true` if the values in each corresponding element pair compare equal using the operator `==` for their types. Otherwise, the result is `false`.

The result of `!=` is `true` if any comparison of the values in each corresponding element pair compare unequal using the operator `!=` for their types. Otherwise, the result is `false`.

Both operands are evaluated in order from left-to-right. Each pair of elements is then used as operands to bind the operator `==` (or `!=`), recursively. Any elements with compile-time type `dynamic` cause an error.

Element names are ignored during tuple comparison.

When a tuple literal is used as an operand, it takes on a converted tuple type formed by the element-wise conversions that are introduced when binding the operator `==` (or `!=`) element-wise. For instance, in `(1L, 2, "hello") == (1, 2L, null)`, the converted type for both tuple literals is `(long, long, string)` and the second literal has no natural type.

In the nullable tuple case, additional checks for `t1.HasValue` and/or `t2.HasValue` shall be performed.

When an element-wise comparison returns a non-`bool` result, if that comparison is dynamic in a tuple equality, a dynamic invocation of the operator `false` shall be used with the result being negated to get a `bool`. 

If an element-wise comparison returns some other non-`bool` type in a tuple equality, there are two cases:

- if the non-bool type converts to `bool`, that conversion is applied,
- if there is no such conversion, but the type has an operator `false`, that is used the result is negated.

In a tuple inequality, the same rules apply except that the operator `true` is used without negation.

When binding the `==` (or `!=`) operator, the usual rules are: (1) dynamic case, (2) overload resolution, and (3) fail. However, in the case of tuple comparison, a new rule is inserted between (1) and (2): if both operands of a comparison operator are tuples, the comparison is performed element-wise. This tuple equality is also lifted onto nullable tuples. If prior to the addition of tuple comparison to C#, a program defined `ValueTuple` types with `==` or `!=` operators, those operators would have been chosen by overload resolution. However, with the addition of comparison support and the new rule above, the comparison is handled by tuple comparison instead of the user-defined comparison.

Regarding the order of evaluation of `t1` and `t2`, `t1` is evaluated first followed by `t2`, then the element-wise comparisons going from left-to-right. Consider the following.

If there is a conversion from type `A` to type `B` and a method `(A, A) GetTuple()`, the comparison 

```csharp
(new A(1), (new B(2), new B(3))) == (new B(4), GetTuple())
```

is evaluated thus:

- `new A(1)`
- `new B(2)`
- `new B(3)`
- `new B(4)`
- `GetTuple()`
- then the element-wise conversions and comparisons and conditional logic is evaluated (convert `new A(1)` to type `B`, then compare it with `new B(4)`, and so on).


> Add the following to the end of section [Equality operators and null](../../spec/expressions.md#Equality-operators-and-null):

### Equality operators and null

...

In tuple equality, expressions such as `(0, null) == (0, null)` and `(0, null) != (0, null)` are allowed with neither `null` nor the tuple literals having a type.

Consider the case of a type `struct S` without `operator==`. The comparison `(S?)x == null` is allowed, and it is interpreted as `((S?).x).HasValue`. In tuple equality, the same rule is applied, so `(0, (S?)x) == (0, null)` is also allowed.
