# Ref struct closures

Champion issue: <link to the champion issue>

## Summary
[summary]: #summary

The proposal is to allow C# to convert lambda expressions to generic type parameters constrained to specific `IFunc/IAction` interfaces. Furthermore, the type parameter should be marked `allows ref struct` and the backing closure should be a ref struct.

For instance, the following code

```C#

List<int> list = [1, 2, 3];

Console.Write(FirstOrNull(list, x => x % 2 == 0));

static int? FirstOrNull<TPred>(List<int> list, TPred pred)
    where TPred : allows ref struct, IFunc<int, bool>
{
    var filtered = new List<int>();
    foreach (var item in list)
    {
        if (pred.Invoke(item))
        {
            return item;
        }
    }
    return null;
}
```

would compile and produce the output "2". The generated code for the lambda would look equivalent to

```C#
ref struct __Closure : IFunc<int, bool>
{
    public bool Invoke(int x) => x % 2 == 0;
}
```

## Motivation
[motivation]: #motivation

This is one of the building blocks for making it possible to write libraries like https://github.com/agocke/NLinq. That is a LINQ-replacement library designed to produce optimizable code patterns for the .NET runtime.

One of the biggest problems with the existing LINQ API is that the delegates (`Func<T>` et. al) and the backing synthesized closures are hard to inline, and thus hard to optimize, for the .NET Runtime. One way to avoid this is to not use delegates or class closures at all, and instead pass around generic type parameters that are substituted with ref structs.

## Detailed design
[design]: #detailed-design

The proposal requires a new set of well-known interfaces that match the shape of the `Func/Action` delegates, i.e.

```C#
namespace System
{
    interface IAction
    {
        void Invoke();
    }

    interface IFunc<out T> where T : allows ref struct
    {
        T Invoke();
    }

    interface IFunc<in TIn, out TResult>
        where TIn : allows ref struct
        where TOut : allows ref struct
    {
        TResult Invoke(TIn input);
    }
}

// ... remaining definitions
```

We will refer to a generic type constrained to one of the above interfaces as a _function-interface_. Its _invocation signature_ is the signature of the `Invoke` method.

The spec will then have the following changes

### §10.7.1 — Anonymous function compatibility (conversions.md:849)

Current text:

> Specifically, an anonymous function F is compatible with a delegate type D provided: …

Change: _F_ is also compatible with a _function-interface_ type _I_, applying the existing bullets (§10.7.1) with _I_'s invocation signature substituted for _D_'s parameters/return. Add a new conversion to §10.2's table: an implicit anonymous-function-to-function-interface conversion. Unlike the delegate case, this conversion materializes a compiler-synthesized ref struct implementing _I_'s interface (the captures become fields) rather than allocating a delegate.

### §12.6.3.4 / .5 — Input & output types

Current:

> If E is a method group or implicitly typed anonymous function and T is a delegate type or expression tree type then all the parametertypes of T are input types…
> If E is … an anonymous function and T is a delegate type or expression tree type then the return type of T is an output type…

Change: append "or a function-interface type" to both. When T is the bare type parameter TFunc, the parameter/return types are taken from its constraint's invocation signature.

### §12.6.3.7 / .8 / .9 — The inference rules

These already special-case anonymous functions, but the sub-rules (explicit parameter type inference §12.6.3.9, output type inference §12.6.3.8) again say "T is a delegate type or expression tree type." Add "or a function-interface type" there too, so:

 - §12.6.3.9: an explicitly-typed lambda makes an exact inference from each lambda parameter type to the corresponding parameter type ofthe constraint's method.
 - §12.6.3.8: the lambda's inferred return type (§12.6.3.14) makes a lower-bound inference to the constraint method's return type.

Here `TFunc` itself is the unfixed variable being inferred (in classic LINQ, T is the already-constructed `Func<TSource,TResult>` and only its type-args are unfixed). So we must add a fixing rule (§12.6.3.13): a _function-interface_ type, with a lambda argument, is fixed to the synthesized anonymous-function ref-struct type. This is the analogue of the C# 10 "natural delegate type" feature, but producing a struct type instead of `Func<>`.

### §12.6.4.2 — Applicable function member

 for a value parameter …, an implicit conversion exists from the argument expression to the type of the corresponding parameter

Once the §10.7 conversion above exists, the IFunc overload becomes applicable. Also note §12.6.4.2:1065 — "A generic method whose type arguments do not satisfy their constraints is not applicable." The synthesized struct must satisfy TFunc : IFunc<…>, allows ref struct,which it does by construction.

### §12.6.4.5 / .6 — Overload priority

No betterness changes. Both overloads will be considered equal (a lambda "exactly matches" a function-interface target). If multiple overloads are present, it's expected that authors will use `[OverloadResolutionPriority]` to pick their preference.

### Compiler implementation

Closure conversion would operate similar to existing lowering. One major change is that closure environments would no longer need to be created at the start of a scope, nor would local variables need to be hoisted into fields. Instead, the closure environment can create by-ref variables pointing to all hoisted variables precisely at lambda creation.

## Drawbacks
[drawbacks]: #drawbacks

Generic constraints have their own runtime costs. For CoreCLR, this means increased JIT time, increased type load cost, and increased memory use. For Native AOT, these costs tend to be paid at compile time, except for increased code size. However, the https://github.com/agocke/NLinq project is specifically designed to avoid these costs. By aggressively encouraging inlining, the LINQ calls collapse down to code similar to a for-loop.

Unfortunately, this means that this feature is best suited for CoreCLR users who care more about throughput than all other costs, or Native AOT users. Further CoreCLR optimization is worth exploring.

## Alternatives
[alternatives]: #alternatives

Theoretically, CoreCLR could try to reverse-engineer lambda construction. The problem is that Roslyn attempts to produce optimal closures, which can get quite complicated. The runtime then tries to optimize around the closures Roslyn produces. This cycle repeats, and we end up in a local maximum.

This work is an attempt to break out to a global maximum. There may be other features or optimization layouts that could produce the same results.

## Open questions
[open]: #open-questions

- [ ] The lambda conversion is specific to type parameters with specific constraints. Lambdas assigned to local variables could theoretically have similar lowering, but there would need to be some way to decide to change the target, since the code is not equivalent.

