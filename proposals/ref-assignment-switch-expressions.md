# `ref` assignment on `switch` expressions

* [x] Proposed
* [ ] Prototype: None
* [ ] Implementation: None
* [ ] Specification: Started, below

## Summary
[summary]: #summary

Provide the ability to return `ref` values in `switch` expressions.

## Motivation
[motivation]: #motivation

The `switch` expression is meant to provide a quicker way to return values based on a given expression, but does not support returning `ref` expressions. This limitation doesn't prevent any harm, and can be lifted.

## Detailed design
[design]: #detailed-design

A `switch` expression may return a `ref` value. The returning expression, if not a `throw` expression, must be a `ref` expression.

A `switch` expression that returns a `ref` value needs an extra `ref` in front of it when assigned/returned to a `ref` local. This design aligns with the current design in the ternary operator.

`throw` expressions can still be used normally, and pose no effect on the return type of the `switch` expression.

Once a single switch arm returns a `ref` expression, all other switch arms must return `ref` expressions, unless they are `throw` expressions. Not providing a `ref` expression in a switch arm causes a compiler error.

Since all ref expressions are LValues, and the switch arms are all ref expressions, the entire result of the switch expression is also an LValue. Therefore, the entire switch expression can be passed by reference, assigned to, and returned by reference. Note that for directly assigning to the switch expression's ref result, the `ref` keyword must not be used, like normal ref locals are assigned. For example:

```csharp
// Returning by reference
private ref int GetDirectionField(Direction direction) => ref direction switch
{
     Direction.North => ref NorthField,
     Direction.South => ref SouthField,
     Direction.East => ref EastField,
     Direction.West => ref WestField,
     _ => throw new NotImplementedException(),
};

// Assigning to a ref local
private void RefSwitchAssignLocal()
{
    ref int dimension = ref axis switch
    {
        Axis.X => ref X,
        Axis.Y => ref Y,
    };
}

// Assigning to the ref of the switch expression
private void RefSwitchAssignDirectly(int value)
{
    axis switch
    {
        Axis.X => ref X,
        Axis.Y => ref Y,
    } = value;

    // Equivalent to:
    ref int dimension = ref axis switch
    {
        Axis.X => ref X,
        Axis.Y => ref Y,
    };
    dimension = value;
}
```

Note that, a switch arm can only return a `ref` to a read-only reference if the switch expression is assigned to a `ref readonly` symbol. Consider the example:
```csharp
void M(Axis axis, ref int x, in int y)
{
    ref int dimension = ref axis switch
    {
        Axis.X => ref x,
        Axis.Y => ref y, // Error: y is readonly and cannot be passed by reference to a non-readonly reference
    };
}
```

To pass `ref y`, the declaration of `dimension` must be `ref readonly int`, demoting all assigned references to readonly:
```csharp
void M(Axis axis, ref int x, in int y)
{
    ref readonly int dimension = ref axis switch
    {
        Axis.X => ref x,
        Axis.Y => ref y,
    };
}
```

Likewise, the same error would be thrown if the above expression were to be directly assigned a value, like in the example:
```csharp
void M(Axis axis, ref int x, in int y)
{
    axis switch
    {
        Axis.X => ref x,
        Axis.Y => ref y, // Error: y is readonly and cannot be assigned by reference
    } = 412;
}
```

When not in the context of assigning the ref result of the `switch` expression, not using the `ref` keyword in front of the switch expression is considered a logical error. Thus, it is advised to show a warning covering that case, like in the example:
```csharp
private void RefSwitchAssignLocal()
{
    // Warning:
    // the switch statement returns by reference but the result is immediately dereferenced
    int dimension = axis switch
    {
        Axis.X => ref X,
        Axis.Y => ref Y,
    };
}
```

Ref return safety relies on the individual switch arms. If any of the switch arms that does not throw is unsafe to return, the entire switch statement is unsafe to return. Otherwise, the entire `switch` statement's expression is safe to return.

## Drawbacks
[drawbacks]: #drawbacks

None.

## Alternatives
[alternatives]: #alternatives

Currently, this may only be achieved with a `switch` statement, or a sequence of `if`-`else` statements.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Requires LDM review

## Design meetings
[meetings]: #design-meetings

None.
