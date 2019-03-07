# Non-trailing named arguments

## Summary
[summary]: #summary
Allow named arguments to be used in non-trailing position, as long as they are used in their correct position. For example: `DoSomething(isEmployed:true, name, age);`.

## Motivation
[motivation]: #motivation

The main motivation is to avoid typing redundant information. It is common to name an argument that is a literal (such as `null`, `true`) for the purpose of clarifying the code, rather than of passing arguments out-of-order.
That is currently disallowed (`CS1738`) unless all the following arguments are also named.

```csharp
DoSomething(isEmployed:true, name, age); // currently disallowed, even though all arguments are in position
// CS1738 "Named argument specifications must appear after all fixed arguments have been specified"
```

Some additional examples:
```csharp
public void DoSomething(bool isEmployed, string personName, int personAge) { ... }

DoSomething(isEmployed:true, name, age); // currently CS1738, but would become legal
DoSomething(true, personName:name, age); // currently CS1738, but would become legal
DoSomething(name, isEmployed:true, age); // remains illegal
DoSomething(name, age, isEmployed:true); // remains illegal
DoSomething(true, personAge:age, personName:name); // already legal
```

This would also work with params:
```csharp
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

The proposal is to remove this error and update the rules for finding the corresponding parameter for an argument (§7.5.1.1):

Arguments in the argument-list of instance constructors, methods, indexers and delegates:
- [existing rules]
- An unnamed argument corresponds to no parameter when it is after an out-of-position named argument or a named params argument.

In particular, this prevents invoking `void M(bool a = true, bool b = true, bool c = true, );` with `M(c: false, valueB);`. The first argument is used out-of-position (the argument is used in first position, but the parameter named "c" is in third position), so the following arguments should be named.

In other words, non-trailing named arguments are only allowed when the name and the position result in finding the same corresponding parameter.

## Drawbacks
[drawbacks]: #drawbacks

This proposal exacerbates existing subtleties with named arguments in overload resolution. For instance:

```csharp
void M(int x, int y) { }
void M<T>(T y, int x) { }

void M2()
{
    M(3, 4);
    M(y: 3, x: 4); // Invokes M(int, int)
    M(y: 3, 4); // Invokes M<T>(T, int)
}
```

You could get this situation today by swapping the parameters:

```csharp
void M(int y, int x) { }
void M<T>(int x, T y) { }

void M2()
{
    M(3, 4);
    M(x: 3, y: 4); // Invokes M(int, int)
    M(3, y: 4); // Invokes M<T>(int, T)
}
```

Similarly, if you have two methods `void M(int a, int b)` and `void M(int x, string y)`, the mistaken invocation `M(x: 1, 2)` will produce a diagnostic based on the second overload ("cannot convert from 'int' to 'string'"). This problem already exists when the named argument is used in a trailing position.

## Alternatives
[alternatives]: #alternatives

There are a couple of alternatives to consider:

- The status quo
- Providing IDE assistance to fill-in all the names of trailing arguments when you type specific a name in the middle.

Both of those suffer from more verbosity, as they introduce multiple named arguments even if you just need one name of a literal at the beginning of the argument list.

## Unresolved questions
[unresolved]: #unresolved-questions

## Design meetings
[ldm]: #ldm
The feature was briefly discussed in LDM on May 16th 2017, with approval in principle (ok to move to proposal/prototype). It was also briefly discussed on June 28th 2017.

Relates to initial discussion https://github.com/dotnet/csharplang/issues/518
Relates to championed issue https://github.com/dotnet/csharplang/issues/570
