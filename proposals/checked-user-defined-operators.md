# Checked user-defined operators

## Summary
[summary]: #summary

C# should support defining `checked` variants of the following user-defined operators so that users can opt into or out of overflow behavior as appropriate:
*  The `-` unary operator (https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#unary-minus-operator).
*  The `+`, `-`, `*`, and `/` binary operators (https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#arithmetic-operators).

## Motivation
[motivation]: #motivation

There is no way for a user to declare a type and support both checked and unchecked versions of an operator. This will make it hard to port various algorithms to use the proposed `generic math` interfaces exposed by the libraries team. Likewise, this makes it impossible to expose a type such as `Int128` or `UInt128` without the language simultaneously shipping its own support to avoid breaking changes.

## Detailed design
[design]: #detailed-design

Grammar at https://github.com/dotnet/csharplang/blob/main/spec/classes.md#operators will be adjusted to allow
`checked` keyword after the `operator` keyword right before the operator token:
```antlr
overloadable_unary_operator
    : '+' | 'checked'? '-' | '!' | '~' | '++' | '--' | 'true' | 'false'
    ;

overloadable_binary_operator
    : 'checked'? '+'   | 'checked'? '-'   | 'checked'? '*'   | 'checked'? '/'   | '%'   | '&'   | '|'   | '^'   | '<<'
    | right_shift | '=='  | '!='  | '>'   | '<'   | '>='  | '<='
    ;
```

For example:
``` C#
public static T operator checked -(T x) {...}
public static T operator checked +(T lhs, T rhs) {...}
public static T operator checked -(T lhs, T rhs) {...}
public static T operator checked *(T lhs, T rhs) {...}
public static T operator checked /(T lhs, T rhs) {...}
```

For brevity below, an operator with the `checked` keyword is referred to as a `checked operator` and an operator without it is referred to as a `regular operator`.

Section "I.10.3.1 Unary operators" of ECMA-335 will be adjusted to include *op_CheckedUnaryNegation* as the name for a method implementing checked unary `-`.

Section "I.10.3.2 Binary operators" of ECMA-335 will be adjusted to include *op_CheckedAddition*, *op_CheckedSubtraction*,
*op_CheckedMultiply*, *op_CheckedDivision* as the names for methods implementing checked `+`, `-`, `*`, and `/` binary operators.


Expression trees? 

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

## Alternatives
[alternatives]: #alternatives

### Placement of the `checked` keyword

Alternatively the `checked` keyword could be moved to the place right before the `operator` keyword:  
``` C#
public static T checked operator -(T x) {...}
public static T checked operator +(T lhs, T rhs) {...}
public static T checked operator -(T lhs, T rhs) {...}
public static T checked operator *(T lhs, T rhs) {...}
public static T checked operator /(T lhs, T rhs) {...}
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
public static checked T operator -(T x) {...}
public static checked T operator +(T lhs, T rhs) {...}
public static checked T operator -(T lhs, T rhs) {...}
public static checked T operator *(T lhs, T rhs) {...}
public static checked T operator /(T lhs, T rhs) {...}
```
    
### `unchecked` keyword

There were suggestions to support `unchecked` keyword at the same position as the `checked` keyword
with the following possible meanings:
- Simply to explicitly reflect the ragular nature of the operator, or
- Perhaps to designate a distinct flavor of an operator that is supposed to be used in an `unchecked` context.

### Operator names in ECMA-335

Alternatively the operator names could be *op_UnaryNegationChecked*, *op_AdditionChecked*, *op_SubtractionChecked*,
*op_MultiplyChecked*, *op_DivisionChecked*, with *Checked* at the end. However, it looks like there is already a pattern
established to end the names with the operator word. For example, there is a *op_UnsignedRightShift* operator rather than
*op_RightShiftUnsigned* operator.

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

Conversion operators?
Dynamic?

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->
