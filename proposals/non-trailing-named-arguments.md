# Non-trailing named arguments

## Summary
[summary]: #summary
Allow named arguments to be used in non-trailing position, as long as they are used in their correct position. For example: `DoSomething(isEmployed:true, name, age);`.

## Motivation
[motivation]: #motivation

The main motivation is to avoid typing redundant information. It is common to name an argument that is a literal (such as `null`, `true`) for the purpose of clarifying the code, rather than of passing arguments out-of-order.
That is currently disallowed (`CS1738`) unless all the following arguments are also named.

```C#
DoSomething(isEmployed:true, name, age); // currently disallowed, even though all arguments are in position
// CS1738 "Named argument specifications must appear after all fixed arguments have been specified"
```

Some additional examples:
```C#
public void DoSomething(bool isEmployed, string personName, int personAge) { ... }

DoSomething(isEmployed:true, name, age); // currently CS1738, but would become legal
DoSomething(true, personName:name, age); // currently CS1738, but would become legal
DoSomething(name, isEmployed:true, age); // remains illegal
DoSomething(name, age, isEmployed:true); // remains illegal
DoSomething(true, personAge:age, personName:name); // already legal
```

This would also work with params:
```C#
public class Task
{
    public static Task When(TaskStatus all, TaskStatus any, params Task[] tasks);
}
Task.When(all: TaskStatus.RanToCompletion, any: TaskStatus.Faulted, task1, task2)
```

## Detailed design
[design]: #detailed-design

In §7.5.1 (Argument lists), the spec currently says:
> An *argument* with an *argument-name* is referred to as a __named argument__, whereas an *argument* without an *argument-name* is a __positional argument__. It is an error for a positional argument to appear after a named argument in an *argument-list*.

The proposal is to remove this error and update the rules for checking if a function member is applicable (§7.5.3.1):

A function member is said to be an applicable function member with respect to an argument list A when all of the following are true:
* [... existing rules ...]
* For each named argument in A, if its corresponding parameter (as described in §7.5.1.1) does not have the same position as that argument, then all the following arguments must be named.

In particular, this prevents invoking `void M(bool a = true, bool b = true, bool c = true, );` with `M(c: false, valueB);`. The first argument is used non-positionally (the argument is used in first position, but the parameter named "c" is in third position), so the following arguments should be named.

In other words, non-trailing named arguments are only allowed when the name and the position result in finding the same corresponding parameter.

## Drawbacks
[drawbacks]: #drawbacks

## Alternatives
[alternatives]: #alternatives

There are a couple of alternatives to consider:

- The status quo
- Providing IDE assistance to fill-in all the names of trailing arguments when you type specific a name in the middle.

Both of those suffer from more verbosity, as they introduce multiple named arguments even if you just need one name of a literal at the beginning of the argument list.

## Unresolved questions
[unresolved]: #unresolved-questions

- VB?

## Design meetings
[ldm]: #ldm
The feature was briefly discussed in LDM on May 16th 2017, with approval in principle (ok to move to proposal/prototype).

Relates to initial discussion https://github.com/dotnet/csharplang/issues/518
Relates to championed issue https://github.com/dotnet/csharplang/issues/570
