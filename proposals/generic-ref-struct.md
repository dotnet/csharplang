Unconstraining our generics
=====

## Summary

## Motivation

## Detailed Design 

### ref struct generic arguments

### ref struct interfaces

### delegates

## Considerations

### Treat it like array
Why have an attribute when we could just special case it like arrays

### Why not attribute per type parameter
Can't really encode type parameters in attributes

### Generalize the delegate support
The use of any type in `delegate` may cause readers to desire a more general feature that could be applied to any generic parameter. Essentially could we design a syntax for type parameters that could be any type (pointer, `ref`, etc ...) and allow it on any generic parameter? That would leave `delegate` instances as effectively having an implicit version of this syntax.

It is possible to design such a syntax. Let's use `where T : any` for discussion purposes.

```c#
// Developer writes
public delegate T Func<T>();

// Developer effectively writes
public delegate T Func<T>() where T : any;
```

The problem with this feature is that it's only useful in signature only scenarios. Those work becase the there is no action associated with the generic parameters. The compiler only needs to consider the standard constraint checking. Effectively the compiler needs to ensure that a `T` with `where T : any` isn't passed to an incompatible generic type parameter.

```c#
interface I1<U> { }
interface I2<U> where U : IFormattable { }

// Error: T is more permissive than U
interface IImpl1<T> : I1<T> where T : any { }

// Error: T doesn't implement IFormattable
interface IImpl2<T> : I2<T> where T : any { }
```

Once the feature is applied to scenarios where code can execute on `T` it becomes clear that virtually nothing can be done with `T`. That is because the langauge, and runtime, have to consider all the different permutations that `T` can take. Lets look at a few simple examples: 

```c#
void M<T>(T parameter)
    where T : any
{
    T local = parameter;
    ...
}
```

This, assigning a parameter to a local, seems simple enough but what are the semantics here? Consider that most C# developers would believe this to be simple value assignment but what happens when this method is instantiated as `ref int`? That effectively translates the assignment to `ref int local = parameter` which is an error. This is a hard error to reconcile because it violates basic `ref` local semantics. 

Attempts to be clever here by saying assignment auto expands to taking the `ref` of the right hand side when the left is `ref` quickly fall part once you expand out the scenarios a bit more: 

```c# 
T GetValue<T>() where T : any { ... }

void Use<T>(T parameter) where T : any, new()
{
    parameter = GetValue<T>(); // Okay: taking ref of RHS is legal when T is ref 
    parameter = new T(); // Error: can't take ref of non-location
    parameter = default; // Error: can't take ref of non-location
}
```

Effectively rules meant to make assignment work just break it in other cases. It does not appear to be generally solvable. This means basic value manipulation is not possible.

The problems go even deeper though. Consider the following:

```c#
void M2<T>(T parameter)
    where T : any
{
    // Error: T doesn't necessarily inherit from object
    Console.WriteLine(parameter.ToString());
}
```

This example must be an error. The compiler must consider the case where `T` is instantiated as a pointer, say `int*`. That type does not derive from `object` and hence has no `ToString` to invoke.

The other issue is that a syntax like `where T : any` needs to satisfy *any* type. That includes type combinations that are not allowed in C# today, or added to the CLI in the future. Consider as a concrete example that `ref ref` is not allowed in CIL or C# today but that it's reasonable for this to be added in [the future](https://github.com/dotnet/runtime/pull/63659#discussion_r784986222). It seems implausible that the semantics for these types of features could be correctly predicted in advance.

Effectively the only item that can be taken with `T where T : any` is to instantiate other generics. No concrete actions can be taken on instances of the type itself. Doing so would require significant changes to the runtime and hard to explain C# semantics. There are some discussions around making runtime changs, [example](https://github.com/dotnet/runtime/issues/13627), but those aren't general enough for this to work.

This also means that the use of `where T : any` on interfaces is not consequence free. Implicitly adding this to an interface will break any default implementated method on that interface. Hence unlike `delegate` it cannot be retroactively added to our existing types. It is possible as an opt-in for `interface` but doing so effectively removes the ability for an `interface` to participate in DIM. 

It is not believed that the `interface` scenario is worth the extra syntax here. If more evidence arrives that demonstrates it to be worth it we should consider this in a future version of the language.

## Open Issues

## Future Considerations

### Issues

### Related Work

- https://github.com/dotnet/runtime/issues/13627
