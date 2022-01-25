Unconstraining our generics
=====

## Summary
This proposal is an aggregation of several different proposals for making `ref struct` more generally usable in our type system. The goal being to unify code paths today that have to specialize on `ref struct` and other types do to the inability of `ref struct` to participate in generics and implement interfaces. This proposal will allow us to generalize on `ref struct` as naturally as other types. 

## Motivation
This feature will allow developers to remove `ref struct` specific code by instead letting them implement interfaces that are used in generic contexts to generalize algorithms. For example an explicit goal is to allow `ref struct` to implement `ISpanFormattable`. The `ISpanFormattable` interface and `ref struct` are used in low level perf sensitive areas but our current language limitations prevent them from being used together.

This will also allow for combinations like `Span<Span<T>>`. This is a long desire of the runtime team who often want to represent collections of `Span<T>` but are unable to do so due to our current limitations.

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

At the call site this relaxes the set of legal type arguments to inclued `ref struct` *in addition* to the types already allowed. Even though this appears in the constraint position it serves more of an anti-constraint because it expands the set of possible types. 

As an expansion though it means that `T where T : ref struct` is not compatible with type parameters lacking the same annotation:

```c#
void M1<U>() { }
void M2<T>()
    where T : ref struct
{
    M1<T>(); // Error: type parameter T is not compatible with U because it does not 
             // allow `ref sturct`
}
```

At the implementation all type parameters having the `where T : ref struct` annotation are treated as if the type is a `ref struct`. Most importantly it has all of the limitations that would be expected from a `ref struct`. For example: 

- Instances of `T` cannot be boxed
- Instances of `T` can only appear as fields inside a `ref struct`
- Instances of `T` are considered `ref struct` for the purposes of the [span safety rules](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md). 
- `T` cannot be used as the element of an array
- Members on `object` such as `ToString` cannot be invoked as that implicitly boxes
- etc ...

Positionally the `ref struct` annotation must come after the type kind, such as `struct`, but before the interface list or `new()` constraint (interfaces are covered [later in the proposal](#interfaces)). The combination with type kinds has the following effects:

- `where T : struct, ref struct`: describes a type parameter that can be a `struct` or `ref struct` but not other types like `class` or `interface`.
- `where T : unmanaged, ref struct`: similar to `struct, ref struct` except that the type also meets the `unmanaged` constraint. This is believed to have limited use though as `ref struct` containing any type of `ref` data will not meet the `unmanaged` contraint and hence are likely better defined as simply `struct`.
- `where T : notnull, ref struct`: this is the same as the `notnull` constraint but also allows for `ref struct` types to satisfy it.
- `where T : class, ref struct`: is an error as the `ref struct` portion is meaningless when combined with `class`. 
- `where T : default, ref struct`: is an error as the declarations are contradictory.
- `where T : ref struct, BaseType` where `BaseType` denotes a reference type. This is an error because the `ref sruct` portion is meaningless in this context.

The `where T : ref struct` annotation will be encoded by attributing the type parameter with the attribute `System.Runtime.CompilerServices.SupportsByRefLike`. This will not result in a `modreq` on the containing member nor will `[Obsolete]` tricks be added to the containing type. The intent is for this to be a binary compatible change. The explicit intent is for us to allow types in libraries to relax their type parameters to accept `ref struct` without breaking consumers.

### Supporting Span<T>

### ref struct interfaces
<a name="interfaces"></a>

**TODO** look up the definition of the constrained prefix for low level rules on where this is legal

### delegates

## Considerations

### Treat it like array
**TODO** Use the name `SupportsOnlyNonByRefLike`

Rather than `[IgnoreForRefStruct]` attribute why not just special case `Span<T>` and other types as the language special cases arrays today. 

Arrays today conditionally implement interfaces based on the element type of the array. For example a `T[]` implements `IEnumerable<T>` when `T` is a non-pointer type. That means `int*[]` does not but `int[]` does. The issues around generics and arrays is very good analogy for `where T : ref struct` and `Span<T>`. Effectively there is a set of APIs that need to be illegal based on the element type of `Span<T>` hence why not apply the same rules as array.

In discussion with the runtime team there was agreement that the analogy is good. However under the hood the runtime was going to need some way to track the methods in question as invalid when the element type of `Span<T>` was a `ref struct`: an internal list, attribute, etc ... Given that some level of book keeping was needed and attributes were cheap it made more sense to expose this as a general feature. This way third parties can push `where T : ref struct` through types similar to `Span<T>` where they have APIs they can't walk away from.

### Why not attribute per type parameter
Rather than applying `[IgnoreForRefStruct]` at the member level, it could be applied more granularly at a type parameter level. Consider for example: 

```c#
struct S<T, U> 
{
    where T : ref struct
    where U : ref struct 

    [IgnoreForRefStruct(nameof(T))]
    void M(T[] array)
    {

    }
}
```

In this example the method `M` is only invalid when `T` is a `ref struct`. The type of `U` is immaterial to the legality of the method as it's not used in a manner incompatible with `ref struct`. In this case it would be desirable to have a more granular opt out. 

At this time though there are no practical examples of this pattern. Adding this granularity will add cost to the runtime, as well as to the language as it's hard to reference type parameters in attributes. Going for simplicity at the moment but can reconsider in future versions if evidence for the scenario appears.

**TODO** is IgnoreForRefStruct the right name (no)

### Constraints vs. anti constraints
The syntax `where T : ref struct` appears in the space C# refers to as constraints. That is the syntax is meant to constraint or limit the set of types that are legal for the generic type parameter.

This feature though expands the set of types available. The set of types applicable to `T where T : ref struct` is larger than simply `T`. This means the feature is an anti-constraint yet it occupies a constraint location. 

It is reasonable to consider a new syntax here that cleanly differentiates our anti-constraints from actual constraints. At the time of this writing though there are only two proposed anti-constraints, one is lacking supporting evidence, and no obvious better syntax for them. Hence the proposal is written using the familiair `where T :` style of syntax. If an intuitive syntax for anti-constraints is proposed it is likely to get consideration in the proposal.

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

## Examples 

### RefList<T>
<a name="ref-list"></a>
This is an eample a `List<T>` style data structure with a hard capacity that can store `ref struct` in addition to normal types:

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
