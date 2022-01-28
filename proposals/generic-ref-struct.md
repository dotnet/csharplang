Unconstraining our generics
=====

## Summary
This proposal is an aggregation of several different proposals for making `ref struct` more generally usable in our type system. The goal is to unify code paths today that have to specialize on `ref struct` and other types due to the inability of `ref struct` to participate in generics and implement interfaces. This proposal will allow us to generalize on `ref struct` as naturally as other types. 

## Motivation
This feature will allow developers to remove `ref struct` specific code by instead letting them implement interfaces that are used in generic contexts to generalize algorithms. For example an explicit goal is to allow `ref struct` to implement `ISpanFormattable`. The `ISpanFormattable` interface and `ref struct` are used in low level perf sensitive areas but our current language limitations prevent them from being used together.

This will also allow for combinations like `Span<Span<T>>`. This has long been a desire of the runtime team, who often want to represent collections of `Span<T>` but are unable to do so due to our current limitations.

It also will enable the creation of generic collections of `ref struct`. For instance having a `List<T>` style [data structure](#ref-list) with a hard capacity limit will be possible with the features in this proposal.

## Detailed Design 

### ref struct generic arguments
The compiler will allow type parameters to be (un)constrained such that they can accept `ref struct` as arguments using the `where T : ref struct` syntax. 

```c#
void Swap<T>(ref T left, ref T right)
    where T : ref struct
{
    T temp = left;
    left = right;
    right = temp;
}

Span<byte> span1 = new byte[4];
Span<byte> span2 = default;

// Can use ref struct type arguments 
Swap<Span<byte>>(ref span1, ref span2);

// Can also use normal type arguments
Swap<byte>(ref span1[0], ref span1[1]);
```

At the call site this relaxes the set of legal type arguments to included `ref struct` *in addition* to the types already allowed. Even though this appears in the constraint position it serves more of an anti-constraint because it expands the set of possible types. 

As an expansion it means that `where T : ref struct` is not compatible with type parameters lacking the same annotation:

```c#
void M1<U>() { }
void M2<T>()
    where T : ref struct
{
    M1<T>(); // Error: type parameter T is not compatible with U because it does not 
             // allow `ref struct`
}
```

At the implementation all type parameters having the `where T : ref struct` annotation are treated as if the type is a `ref struct`. This means the type parameter has all of the limitations that would be expected a `ref struct` type. For example:

- Instances of `T` cannot be boxed or appear on the heap
- Instances of `T` can only appear as fields inside a `ref struct`
- Instances of `T` are considered `ref struct`s for the purposes of the [span safety rules](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md) 
- `T` cannot be used as the element of an array
- Members on `object` such as `ToString` cannot be invoked as that implicitly boxes
- etc ...

This also means that `T : where ref struct` cannot be used where `ref struct` would be used in type positions: 

```c#
class C<T> where T : ref struct 
{
    // Error: field cannot be a `ref struct` inside a class
    T field; 

    // Error: array elements cannot be of type `ref struct`
    void M(T[] array)
    {

    }
}
```

Positionally in syntax the `ref struct` annotation must come after the type kind, such as `struct`, but before the interface list or `new()` constraint (interfaces are covered [later in the proposal](#interfaces)). The combination with type kinds has the following effects:

- `where T : struct, ref struct`: describes a type parameter that can be a `struct` or `ref struct` but not other types like `class` or `interface`.
- `where T : unmanaged, ref struct`: similar to `struct, ref struct` except that the type also meets the `unmanaged` constraint. This is believed to have limited use though as `ref struct` containing any type of `ref` data will not meet the `unmanaged` constraint and hence are likely better defined as simply `struct`.
- `where T : notnull, ref struct`: this is the same as the `notnull` constraint but also allows for `ref struct` types to satisfy it.
- `where T : class, ref struct`: is an error as the `ref struct` portion is meaningless when combined with `class`. 
- `where T : default, ref struct`: is an error as the declarations are contradictory.
- `where T : ref struct, BaseType` where `BaseType` denotes a reference type. This is an error because the `ref struct` portion is meaningless in this context.

The `where T : ref struct` annotation will be encoded by attributing the type parameter with the attribute `System.Runtime.CompilerServices.SupportsByRefLike`. This will not result in a `modreq` on the containing member nor will `[Obsolete]` tricks be added to the containing type. The intent is for this to be a binary compatible change. The explicit intent is for us to allow types in libraries to relax their type parameters to accept `ref struct` without breaking consumers.

### Supporting Span<T>
A key motivation of this proposal is to allow for instantiations like `Span<Span<T>>`, effectively moving the `Span<T>` type to have the `where T : ref struct` annotation. This is a problem because a number of the APIs on `Span<T>` violate the rules of `where T : ref struct` as `T[]` is a common type in the APIs. 

```c# 
// Existing Span<T> definition
public readonly ref struct Span<T>
{
    public Span(T[]? array) { ... }

    public Span(T[]? array, int index, int length) { ... }

    public static implicit operator Span<T>(T[]? array) { ... }
}
```

If the `Span<T>` type was being designed from scratch in the presence of `where T : ref struct` it's possible different choices would've been made here. Possibly leveraging `static` factories and pushing implicit conversions into the compiler. Unfortunately this is not being designed from scratch and in order to meet the desire to have `Span<Span<T>>` these existing APIs need to be rationalized.

The compiler will introduce the type `System.Runtime.CompilerServices.SupportsOnlyNonByRefLikeAttribute` that can be applied to methods and operators. When applied to a member it has the effect of removing `where T : ref struct` from the constraints on all type parameters on the *immediate* containing type. All other constraints remain in place.

This allows us to rationalize our `Span<T>` definition as well as allowing third party customers rationalize their existing APIs. 

```c#
// Existing Span<T> definition
public readonly ref struct Span<T>
{
    [SupportsOnlyNonByRefLikeAttributes]
    public Span(T[]? array) { ... }

    [SupportsOnlyNonByRefLikeAttributes]
    public Span(T[]? array, int index, int length) { ... }

    [SupportsOnlyNonByRefLikeAttributes]
    public static implicit operator Span<T>(T[]? array) { ... }
}
```

This does not impact the binding rules for invocations of such members. These symbols will continue to be fully bindable. In the case they are bound when any of the type parameters on the containing type are potentially a `ref struct` then use of a member annotated with `[SupportsOnlyNonByRefLike]` will be considered an error.

```c#
void M<T, U>()
    where T : ref struct
{
    // Error: cannot access member as `T` is potentially a `ref struct`
    new Span<T>(null);

    // Okay
    new Span<U>(null);
}
```

The runtime will recognize this attribute and ensure that any use of these members at runtime with a type that is a `ref struct` will result in a runtime exception.

Note: the [alternative](#special-case) here is simply special casing `Span<T>` as array is special cased today.

### ref struct interfaces
<a name="interfaces"></a>

The language will allow `ref struct`s to implement interfaces. The rules for implementation will be the same as for normal `struct`s. This includes allowing the implementations to be implicit or explicit.

**TODO: Need better example than animals but feeling lazy**

```c#
interface IAnimal
{
    void Speak(); 
}

ref struct Lab : IAnimal
{
    public void Speak() => Console.WriteLine("FEED ME");
}
```

The one exception is when `ref struct` intersects with default implementation methods. When interfaces containing default implemented methods are used on `struct` types there is an implicit boxing operation for the invocation. That means effectively a DIM call when the implementing type was a `ref struct` would violate our rules on them appearing in the heap. As such this will be disallowed by both the language and runtime. 

- A `ref struct` that implements an `interface` must provide implementations for all methods, even those with a default implementation
- A DIM invocation on a `ref struct` will result in a runtime exception

The ability to implement interfaces does not change the rules around conversions though. A `ref struct` cannot be boxed, and since conversion from a `struct` to an `interface` has an implicit box associated with it, conversion from a `ref struct` to its implemented interfaces remains an error: 

```c#
ref struct S : IUtil { ... }

S local = ...;
IUtil util = local; // Error: a ref struct cannot be boxed 
```

The interfaces implemented by a `ref struct` can be used to satisfy constraints on a generic type parameter. Invocation of the interface methods on the type parameters is allowed because such invocations use the `.constrained` prefix for execution which eliminates boxing when the underlying type is a `struct`. 

```c# 
void Write<T>(T value)
    where T : ref struct, ISpanFormattable
{
    var buffer = stackalloc char[100];

    // Constrained interface call which does not box
    if (value.TryFormat(buffer, out var written, default, null))
    {
        this.Write(buffer);
    }
}
```

### Delegates
The features described thus far allow developers the flexibility to opt existing generic types into `where T : ref struct` where legal. However, this requires developers modify existing types and deal with any incompatibilities that may occur. Essentially it is not automatic, it requires work. 

There is one class of types for which no human inpsection is needed: delegates. Delegates represent types that are signature only, there is no implementation to consider. That means the compiler itself can infer whether a type parameter could be validly `where T : ref struct` or not. It can do so by examining the places where type parameters are used and validating if the constraints are compatible with `where T : ref struct`. For example: 

```c#
// T is never used inside another generic hence implicitly supports ref struct
delegate T Delegate1<T>(); 
delegate void Delegate2<T>(T p);
delegate void Delegate3<T>(ref T p);

// T is used in a generic param that does not support ref struct hence it does not 
// support it
delegate void Delegate4<T>(List<T> list);
```

The compiler will automatically mark type parameters on such `delegate` declarations as having the `where T : ref struct` annotation. This will expedite the adoption of this feature and not force a lot of extra annotations by developers. Consider that this will automatically make all known `Action` and `Func` delegates available for `ref struct` usage.

## Open Issues

- Special case `Span<T>` like array vs. using an attribute and making it end user consumable
- Use constraint syntax vs. a newly defined allow style syntax

## Considerations

### Treat it like array
<a name="special-case"></a>

Rather than `[SupportsOnlyNonByRefLike]` attribute why not just special case `Span<T>` and other types as the language special cases arrays today. 

Arrays today conditionally implement interfaces based on the element type of the array. For example a `T[]` implements `IEnumerable<T>` when `T` is a non-pointer type. That means `int*[]` does not but `int[]` does. The issues around generics and arrays is very good analogy for `where T : ref struct` and `Span<T>`. Effectively there is a set of APIs that need to be illegal based on the element type of `Span<T>` hence why not apply the same rules as array.

In discussion with the runtime team there was agreement that the analogy is good. However under the hood the runtime was going to need some way to track the methods in question as invalid when the element type of `Span<T>` was a `ref struct`: an internal list, attribute, etc ... Given that some level of book keeping was needed and attributes are cheap it made more sense to expose this as a general feature. This way third parties can push `where T : ref struct` through types similar to `Span<T>` where they have APIs they can't walk away from.

### Why not attribute per type parameter
Rather than applying `[SupportsOnlyNonByRefLike]` at the member level, it could be applied more granularly at a type parameter level. Consider for example: 

```c#
struct S<T, U> 
{
    where T : ref struct
    where U : ref struct 

    [SupportsOnlyNonByRefLike(nameof(T))]
    void M(T[] array)
    {

    }
}
```

In this example the method `M` is only invalid when `T` is a `ref struct`. The type of `U` is immaterial to the legality of the method as it's not used in a manner incompatible with `ref struct`. In this case it would be desirable to have a more granular opt out. 

At this time though there are no practical examples of this pattern. Adding this granularity will add cost to the runtime, as well as to the language as it's hard to reference type parameters in attributes. Going for simplicity at the moment but can reconsider in future versions if evidence for the scenario appears.

### Constraints vs. anti constraints
The syntax `where T : ref struct` appears in the space C# refers to as constraints. That is the syntax is meant to constraint or limit the set of types that are legal for the generic type parameter.

However, this feature expands the set of types available. The set of types applicable to `T where T : ref struct` is larger than simply `T`. This means the feature is an anti-constraint yet it occupies a constraint location. 

It is reasonable to consider a new syntax here that cleanly differentiates our anti-constraints from actual constraints. At the time of this writing though there are only two proposed anti-constraints and one is lacking supporting evidence. This makes it questionable if it meets the bar for a new syntax or not.

If a new syntax is desired to separate anti-constraints from constraints then `allow` is one to consider: 

```c#
readonly ref struct Span<T> 
    allow T : ref struct
{

}
```

This makes the support for `[SupportsOnlyNonByRefLike]` cleaner as the implementation is to simply ignore the set of `allow` items. It may even lead to a better name such as `[RemoveAllows]`. 

The only downside is that it means type parameter declarations get more complex because there are now both `where` and `allow` lists on them.

```c#
void Write<T>(T value)
    where T : ISpanFormattable
    allow T : ref struct
{

}
```

At the same time it's possible this will read clearer to developers. It also opens the door for future `allow`s such as pointers. 

### Generalize the delegate support
The use of any type in `delegate` may cause readers to desire a more general feature that could be applied to any generic parameter. Essentially could we design a syntax for type parameters that could be any type (pointer, `ref`, etc ...) and allow it on any generic parameter? That would leave `delegate` instances as effectively having an implicit version of this syntax.

It is possible to design such a syntax. Let's use `where T : any` for discussion purposes.

```c#
// Developer writes
public delegate T Func<T>();

// Developer effectively writes
public delegate T Func<T>() where T : any;
```

The problem with this feature is that it's only useful in signature only scenarios. Those work because the there is no action associated with the generic parameters. The compiler only needs to consider the standard constraint checking. Effectively the compiler needs to ensure that a `T` with `where T : any` isn't passed to an incompatible generic type parameter.

```c#
interface I1<U> { }
interface I2<U> where U : IFormattable { }

// Error: T is more permissive than U
interface IImpl1<T> : I1<T> where T : any { }

// Error: T doesn't implement IFormattable
interface IImpl2<T> : I2<T> where T : any { }
```

Once the feature is applied to scenarios where code can execute on `T` it becomes clear that virtually nothing can be done with `T`. That is because the language, and runtime, have to consider all the different permutations that `T` can take. Lets look at a few simple examples: 

```c#
void M<T>(T parameter)
    where T : any
{
    T local = parameter;
    ...
}
```

This, assigning a parameter to a local, seems simple enough but what are the semantics here? Consider that most C# developers would believe this to be simple value assignment but what happens when this method is instantiated as `ref int`? That effectively translates the assignment to `ref int local = parameter` which is an error. This is a hard error to reconcile because it violates basic `ref` local semantics. 

Attempts to be clever here by saying assignment auto expands to taking the `ref` of the right hand side when the left is `ref` quickly fall apart once you expand out the scenarios a bit more: 

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

This also means that the use of `where T : any` on interfaces is not consequence free. Implicitly adding this to an interface will break any default implemented method on that interface. Hence unlike `delegate` it cannot be retroactively added to our existing types. It is possible as an opt-in for `interface` but doing so effectively removes the ability for an `interface` to participate in DIM. 

It is not believed that the `interface` scenario is worth the extra syntax here. If more evidence arrives that demonstrates it to be worth it we should consider this in a future version of the language.

### Take delegates further
The [low level hackathon](https://github.com/dotnet/runtime/tree/feature/lowlevelhackathon) demonstrated that at the `delegate` level, where there are only signatures, generic instantiation can go beyond even `ref struct`. It is possible to support generic arguments with all types not supported today, like pointers, as well as items like `ref T` which are more type modifiers than types in the language.

```c# 
int i = 0;
Action<ref int> a = (ref int j) => j++;
a(ref i);
Console.WriteLine(i); // 1
```

It is unlikely that this support will be added to the language in C# 11 or runtime in .NET 7. But support is a realistic possibility in future releases. It is another reason to consider leaving delegates us implicitly opt in. That would mean if the support is extended in the runtime in the future then delegates could continue to automatically get better at the same time.

## Examples 

### RefList<T>
<a name="ref-list"></a>
This is an example `List<T>`-style data structure with a hard capacity that can store `ref struct`s in addition to normal types:

```c#
sealed class RefList<T> where T : ref struct
{
    private Span<T> _storage;
    private int _length;

    public ref T this[int index]
    {
        get
        {
            if (index >= _length)
                throw new ArgumentException(null, nameof(index));

            return ref _storage[index];
        }
    }

    public int Length => _length;

    public RefList<T>(Span<T> storage, int? length = default)
    {
        Reset(storage, length);
    }

    [SupportsOnlyNonByRefLike]
    public RefList<T>(T[] array)
    {
        Reset(array.AsSpan());
    }

    public void Add(T value)
    {
        if (_length + 1 >= _storage.Length)
            throw new InvalidOperationException();

        _span[_length] = value;
        _length++;
    }

    public void Reset(Span<T> storage, int? length = default)
    {
        _storage = storage;
        _length = length ?? storage.Length;
    }
}
```

### Issues

### Related Work

- https://github.com/dotnet/runtime/issues/13627
- https://github.com/dotnet/csharplang/issues/1148
-
