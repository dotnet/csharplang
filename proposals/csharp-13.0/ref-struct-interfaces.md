# Ref Struct Interfaces

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Champion issue: <https://github.com/dotnet/csharplang/issues/7608>

## Summary

This proposal will expand the capabilities of `ref struct` such that they can implement interfaces and participate as generic type arguments.

## Motivation

The inability for `ref struct` to implement interfaces means they cannot participate in fairly fundamental abstraction techniques of .NET. A `Span<T>`, even though it has all the attributes of a sequential list cannot participate in methods that take `IReadOnlyList<T>`, `IEnumerable<T>`, etc ... Instead specific methods must be coded for `Span<T>` that have virtually the same implementation. Allowing `ref struct` to implement interfaces will allow operations to be abstracted over them as they are for other types.

## Detailed Design

### ref struct interfaces

The language will allow for `ref struct` types to implement interfaces. The syntax and rules are the same as for normal `struct` with a few exceptions to account for the limitations of `ref struct` types.

The ability to implement interfaces does not impact the existing limitations against boxing `ref struct` instances. That means even if a `ref struct` implements a particular interface,  it cannot be directly cast to it as that represents a boxing action.

```csharp
ref struct File : IDisposable
{
    private SafeHandle _handle;
    public void Dispose()
    {
        _handle.Dispose();
    }
}

File f = ...;
// Error: cannot box `ref struct` type `File`
IDisposable d = f;
```

The ability to implement interfaces is only useful when combined with the ability for `ref struct` to participate in generic arguments (as [laid out later][ref-struct-generics]).

To allow for interfaces to cover the full expressiveness of a `ref struct` and the lifetime issues they can present, the language will allow `[UnscopedRef]` to appear on interface methods and properties. This is necessary as it allows for interfaces that abstract over `struct` to have the same flexibility as using a `struct` directly. Consider the following example:

```csharp
interface I1
{
    [UnscopedRef]
    ref int P1 { get; }
    ref int P2 { get; }
}

struct S1
{
    [UnscopedRef]
    internal ref int P1 { get {...} }

    internal ref int P2 { get {...} }
}

ref int M<T>(T t, S1 s)
    where T : I1, allows ref struct
{
    // Error: may return ref to t
    return ref t.P1;

    // Error: may return ref to t
    return ref s.P1;

    // Okay
    return ref t.P2;

    // Okay
    return ref s.P2;
}
```

When a `struct` / `ref struct` member implements an interface member with a `[UnscopedRef]` attribute, the implementing member may also be decorated with `[UnscopedRef]` but it is not required. However a member with `[UnscopedRef]` may not be used to implement a member that lacks the attribute ([details][unscoped-ref-impl]).

```csharp
interface I1
{
    [UnscopedRef]
    ref int P1 { get; }
    ref int P2 { get; }
}

struct S1
{
    internal ref int P1 { get {...} }
    internal ref int P2 { get {...} }
}

struct S2
{
    [UnscopedRef]
    internal ref int P1 { get {...} }
    internal ref int P2 { get {...} }
}

struct S3 : I1
{
    internal ref int P1 { get {...} }
    // Error: P2 is marked with [UnscopedRef] and cannot implement I1.P2 as is not marked 
    // with [UnscopedRef]
    [UnscopedRef]
    internal ref int P2 { get {...} }
}

class C1 : I1
{
    internal ref int P1 { get {...} }
    internal ref int P2 { get {...} }
}
```

Default interface methods pose a problem for `ref struct` as there are no protections against the default implementation boxing the `this` member.

```csharp
interface I1
{
    void M()
    {
        // Danger: both of these box if I1 is implemented by a ref struct
        I1 local1 = this;
        object local2 = this;
    }
}

// Error: I1.M cannot implement interface member I1.M() for ref struct S
ref struct S : I1 { }
```

To handle this a `ref struct` will be forced to implement all members of an interface, even if they have default implementations.
The runtime will also be updated to throw an exception if a default interface member is called on a `ref struct` type.

To avoid an exception at runtime the compiler will report an error for an invocation of a non-virtual instance method (or property)
on a type parameter that allows ref struct. Here is an example:
```csharp
public interface I1
{
    sealed void M3() {}
}

class C
{
    static void Test2<T>(T x) where T : I1, allows ref struct
    {
#line 100
        x.M3(); // (100,9): error: A non-virtual instance interface member cannot be accessed on a type parameter that allows ref struct.
    }
}
```

There is also an open design question about reporting a [warning][warn-DIM] for an invocation of a virtual (not abstract) instance method (or property)
on a type parameter that allows ref struct.

Detailed Notes:

- A `ref struct` can implement an interface
- A `ref struct` cannot participate in default interface members
- A `ref struct` cannot be cast to interfaces it implements as that is a boxing operation

### ref struct Generic Parameters

```ANTLR
type_parameter_constraints_clause
    : 'where' type_parameter ':' type_parameter_constraints
    ;

type_parameter_constraints
    : restrictive_type_parameter_constraints
    | allows_type_parameter_constraints_clause
    | restrictive_type_parameter_constraints ',' allows_type_parameter_constraints_clause

restrictive_type_parameter_constraints
    : primary_constraint
    | secondary_constraints
    | constructor_constraint
    | primary_constraint ',' secondary_constraints
    | primary_constraint ',' constructor_constraint
    | secondary_constraints ',' constructor_constraint
    | primary_constraint ',' secondary_constraints ',' constructor_constraint
    ;

primary_constraint
    : class_type
    | 'class'
    | 'struct'
    | 'unmanaged'
    ;

secondary_constraints
    : interface_type
    | type_parameter
    | secondary_constraints ',' interface_type
    | secondary_constraints ',' type_parameter
    ;

constructor_constraint
    : 'new' '(' ')'
    ;

allows_type_parameter_constraints_clause
    : 'allows' allows_type_parameter_constraints

allows_type_parameter_constraints
    : allows_type_parameter_constraint
    | allows_type_parameter_constraints ',' allows_type_parameter_constraint

allows_type_parameter_constraint
    : ref_struct_clause

ref_struct_clause
    : 'ref' 'struct'
```

The language will allow for generic parameters to opt into supporting `ref struct` as arguments by using the `allows ref struct` syntax inside a `where` clause:

```csharp
T Identity<T>(T p)
    where T : allows ref struct
    => p;

// Okay
Span<int> local = Identity(new Span<int>(new int[10]));
```

This is similar to other items in a `where` clause in that it specifies the capabilities of the generic parameter. The difference is other syntax items limit the set of types that can fulfill a generic parameter while `allows ref struct` expands the set of types. This is effectively an anti-constraint as it removes the implicit constraint that `ref struct` cannot satisfy a generic parameter. As such this is given a new syntax prefix, `allows`, to make that clearer.

A type parameter bound by `allows ref struct` has all of the behaviors of a `ref struct` type:

1. Instances of it cannot be boxed
2. Instances participate in lifetime rules like a normal `ref struct`
3. The type parameter cannot be used in `static` fields, elements of an array, etc ...
4. Instances can be marked with `scoped`

Examples of these rules in action:

```csharp
interface I1 { }
I1 M1<T>(T p)
    where T : I1, allows ref struct
{
    // Error: cannot box potential ref struct
    return p;
}

T M2<T>(T p)
    where T : allows ref struct
{
    Span<int> span = stackalloc int[42];

    // The safe-to-escape of the return is current method because one of the inputs is
    // current method
    T t = M3<int, T>(span);

    // Error: the safe-to-escape is current method.
    return t;

    // Okay
    return default;
    return p;
}

R M3<T, R>(Span<T> span)
    where R : allows ref struct
{
    return default;
}
```

The anti-constraint is not "inherited" from a type parameter type constraint.
For example, `S` in the code below cannot be substituted with a ref struct:
```csharp
class C<T, S>
    where T : allows ref struct
    where S : T
{}
```
Detailed notes:

- A `where T : allows ref struct` generic parameter cannot
  - Have `where T : U` where `U` is a known reference type
  - Have `where T : class` constraint
  - Cannot be used as a generic argument unless the corresponding parameter is also `where T: allows ref struct`
- The `allows ref struct` must be the last constraint in the `where` clause
- A type parameter `T` which has `allows ref struct` has all the same limitations as a `ref struct` type.

### Representation in metadata

Type parameters allowing ref structs will be encoded in metadata as described in the [byref-like generics doc][byref-like-generics].
Specifically by using the `CorGenericParamAttr.gpAllowByRefLike(0x0020)` or `System.Reflection.GenericParameterAttributes.AllowByRefLike(0x0020)` flag value.
Whether runtime supports the feature can be determined by checking presence of `System.Runtime.CompilerServices.RuntimeFeature.ByRefLikeGenerics` field.
The APIs were added in https://github.com/dotnet/runtime/pull/98070.

### `using` statement

A `using` statement will recognize and use implementation of `IDisposable` interface when resource is a ref struct.
```csharp
ref struct S2 : System.IDisposable
{
    void System.IDisposable.Dispose()
    {
    }
}

class C
{
    static void Main()
    {
        using (new S2())
        {
        } // S2.System.IDisposable.Dispose is called
    }
}
```

Note that preference is given to a `Dispose` method that implements the pattern, and only if one is not found, `IDisposable`
implementation is used.

A `using` statement will recognize and use implementation of `IDisposable` interface when resource is a type parameter that 
`allows ref struct` and `IDisposable` is in its effective interfaces set.
```csharp
class C
{
    static void Test<T>(T t) where T : System.IDisposable, allows ref struct
    {
        using (t)
        {
        }
    }
}
```

Note that a pattern `Dispose` method will not be recognized on a type parameter that `allows ref struct` because
an interface (and this is the only place where we could possibly look for a pattern) is not a ref struct.
```csharp
interface IMyDisposable
{
    void Dispose();
}
class C
{
    static void Test<T>(T t, IMyDisposable s) where T : IMyDisposable, allows ref struct
    {
        using (t) // Error, the pattern is not recognized
        {
        }

        using (s) // Error, the pattern is not recognized
        {
        }
    }
}
```

### `await using` statement

Currently language disallows using ref structs as resources in `await using` statement. The same limitation will be
applied to a type parameter that `allows ref struct`.

There is a proposal to lift general restrictions around usage of ref structs in async methods - https://github.com/dotnet/csharplang/pull/7994.
The remainder of the section describes behavior after the general limitation for `await using` statement will be lifted, if/when that will happen. 

An `await using` statement will recognize and use implementation of `IAsyncDisposable` interface when resource is a ref struct.
```csharp
ref struct S2 : IAsyncDisposable
{
    ValueTask IAsyncDisposable.DisposeAsync()
    {
    }
}

class C
{
    static async Task Main()
    {
        await using (new S2())
        {
        } // S2.IAsyncDisposable.DisposeAsync
    }
}
```

Note that preference is given to a `DisposeAsync` method that implements the pattern, and only if one is not found, `IAsyncDisposable`
implementation is used.

A pattern `DisposeAsync` method will be recognized on a type parameter that `allows ref struct` as it is recognized on
type parameters without that constraint today.

```csharp
interface IMyAsyncDisposable
{
    ValueTask DisposeAsync();
}

class C
{
    static async Task Test<T>() where T : IMyAsyncDisposable, new(), allows ref struct
    {
        await using (new T())
        {
        } // IMyAsyncDisposable.DisposeAsync
    }
}
```

A `using` statement will recognize and use implementation of `IAsyncDisposable` interface when resource is a type parameter that 
`allows ref struct`, the process of looking for `DisposeAsync` pattern method failed, and `IAsyncDisposable` is in type parameter's effective interfaces set.
```csharp
interface IMyAsyncDisposable1
{
    ValueTask DisposeAsync();
}

interface IMyAsyncDisposable2
{
    ValueTask DisposeAsync();
}

class C
{
    static async Task Test<T>() where T : IMyAsyncDisposable1, IMyAsyncDisposable2, IAsyncDisposable, new(), allows ref struct
    {
        await using (new T())
        {
            System.Console.Write(123);
        } // IAsyncDisposable.DisposeAsync
    }
}
```

### `foreach` statement

The https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement section should be updated accordingly
to incorporate the following.

A `foreach` statement will recognize and use implementation of ```IEnumerable<T>```/```IEnumerable``` interface when collection is a ref struct.
```csharp
ref struct S : IEnumerable<int>
{
    IEnumerator<int> IEnumerable<int>.GetEnumerator() {...}
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {...}
}

class C
{
    static void Main()
    {
        foreach (var i in new S()) // IEnumerable<int>.GetEnumerator
        {
        }
    }
}
```

A pattern `GetEnumerator` method will be recognized on a type parameter that `allows ref struct` as it is recognized on
type parameters without that constraint today.

```csharp
interface IMyEnumerable<T>
{
    IEnumerator<T> GetEnumerator();
}

class C
{
    static void Test<T>(T t) where T : IMyEnumerable<int>, allows ref struct
    {
        foreach (var i in t) // IMyEnumerable<int>.GetEnumerator
        {
        }
    }
}
```

A `foreach` statement will recognize and use implementation of ```IEnumerable<T>```/```IEnumerable``` interface when collection is a type parameter that 
`allows ref struct`, the process of looking for `GetEnumerator` pattern method failed, and ```IEnumerable<T>```/```IEnumerable``` is in type parameter's effective interfaces set.
```csharp
interface IMyEnumerable1<T>
{
    IEnumerator<int> GetEnumerator();
}

interface IMyEnumerable2<T>
{
    IEnumerator<int> GetEnumerator();
}

class C
{
    static void Test<T>(T t) where T : IMyEnumerable1<int>, IMyEnumerable2<int>, IEnumerable<int>, allows ref struct
    {
        foreach (var i in t) // IEnumerable<int>.GetEnumerator
        {
        }
    }
}
```

An `enumerator` pattern will be recognized on a type parameter that `allows ref struct` as it is recognized on
type parameters without that constraint today.

```csharp
interface IGetEnumerator<TEnumerator> where TEnumerator : allows ref struct 
{
    TEnumerator GetEnumerator();
}

class C
{
    static void Test1<TEnumerable, TEnumerator>(TEnumerable t)
        where TEnumerable : IGetEnumerator<TEnumerator>, allows ref struct
        where TEnumerator : IEnumerator, IDisposable, allows ref struct 
    {
        foreach (var i in t) // IEnumerator.MoveNext/Current
        {
        }
    }

    static void Test2<TEnumerable, TEnumerator>(TEnumerable t)
        where TEnumerable : IGetEnumerator<TEnumerator>, allows ref struct
        where TEnumerator : IEnumerator<int>, allows ref struct 
    {
        foreach (var i in t) // IEnumerator<int>.MoveNext/Current
        {
        }
    }

    static void Test3<TEnumerable, TEnumerator>(TEnumerable t)
        where TEnumerable : IGetEnumerator<TEnumerator>, allows ref struct
        where TEnumerator : IMyEnumerator<int>, allows ref struct 
    {
        foreach (var i in t) // IMyEnumerator<int>.MoveNext/Current
        {
        }
    }
}

interface IMyEnumerator<T> : System.IDisposable
{
    T Current {get;}
    bool MoveNext();
}
```

A `foreach` statement will recognize and use implementation of `IDisposable` interface when enumerator is a ref struct.
```csharp
struct S1
{
    public S2 GetEnumerator()
    {
        return new S2();
    }
}

ref struct S2 : System.IDisposable
{
    public int Current {...}
    public bool MoveNext() {...}
    void System.IDisposable.Dispose() {...}
}

class C
{
    static void Main()
    {
        foreach (var i in new S1())
        {
        } // S2.System.IDisposable.Dispose()
    }
}
```

Note that preference is given to a `Dispose` method that implements the pattern, and only if one is not found, `IDisposable`
implementation is used.

A `foreach` statement will recognize and use implementation of `IDisposable` interface when enumerator is a type parameter that 
`allows ref struct` and `IDisposable` is in its effective interfaces set.
```csharp
interface ICustomEnumerator
{
    int Current {get;}
    bool MoveNext();
}

interface IGetEnumerator<TEnumerator> where TEnumerator : allows ref struct 
{
    TEnumerator GetEnumerator();
}

class C
{
    static void Test<TEnumerable, TEnumerator>(TEnumerable t)
        where TEnumerable : IGetEnumerator<TEnumerator>
        where TEnumerator : ICustomEnumerator, System.IDisposable, allows ref struct 
    {
        foreach (var i in t)
        {
        } // System.IDisposable.Dispose()
    }
}
```

Note that a pattern `Dispose` method will not be recognized on a type parameter that `allows ref struct` because
an interface (and this is the only place where we could possibly look for a pattern) is not a ref struct.
Also, since runtime doesn't provide a way to check whether at runtime a type parameter that `allows ref struct`
implements `IDisposable` interface, a type parameter enumerator that `allows ref struct` will be disallowed,
unless `IDisposable` is in its effective interfaces set.
```csharp
interface ICustomEnumerator
{
    int Current {get;}
    bool MoveNext();
}

interface IMyDisposable
{
    void Dispose();
}

interface IGetEnumerator<TEnumerator> where TEnumerator : allows ref struct 
{
    TEnumerator GetEnumerator();
}

class C
{
    static void Test<TEnumerable, TEnumerator>(TEnumerable t)
        where TEnumerable : IGetEnumerator<TEnumerator>
        where TEnumerator : ICustomEnumerator, IMyDisposable, allows ref struct 
    {
        // error CS9507: foreach statement cannot operate on enumerators of type 'TEnumerator'
        //               because it is a type parameter that allows ref struct and
        //               it is not known at compile time to implement IDisposable.
        foreach (var i in t)
        {
        }
    }
}
```

### `await foreach` statement

The https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement section should be updated accordingly
to incorporate the following.

An `await foreach` statement will recognize and use implementation of ```IAsyncEnumerable<T>``` interface when collection is a ref struct.
```csharp
ref struct S : IAsyncEnumerable<int>
{
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken token) {...}
}

class C
{
    static async Task Main()
    {
        await foreach (var i in new S()) // S.IAsyncEnumerable<int>.GetAsyncEnumerator
        {
        }
    }
}
```

A pattern `GetAsyncEnumerator` method will be recognized on a type parameter that `allows ref struct` as it is recognized on
type parameters without that constraint today.

```csharp
interface IMyAsyncEnumerable<T>
{
    IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default);
}

class C
{
    static async Task Test<T>() where T : IMyAsyncEnumerable<int>, allows ref struct
    {
        await foreach (var i in default(T)) // IMyAsyncEnumerable<int>.GetAsyncEnumerator
        {
        }
    }
}
```

An `await foreach` statement will recognize and use implementation of ```IAsyncEnumerable<T>``` interface when collection is a type parameter that 
`allows ref struct`, the process of looking for `GetAsyncEnumerator` pattern method failed, and ```IAsyncEnumerable<T>``` is in type parameter's effective interfaces set.
```csharp
interface IMyAsyncEnumerable1<T>
{
    IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default);
}

interface IMyAsyncEnumerable2<T>
{
    IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default);
}

class C
{
    static async Task Test<T>() where T : IMyAsyncEnumerable1<int>, IMyAsyncEnumerable2<int>, IAsyncEnumerable<int>, allows ref struct
    {
        await foreach (var i in default(T)) // IAsyncEnumerable<int>.GetAsyncEnumerator
        {
            System.Console.Write(i);
        }
    }
}
```

An `await foreach` statement will continue disallowing a ref struct enumerator and a type parameter enumerator that `allows ref struct`. The reason
is the fact that the enumerator must be preserved across `await MoveNextAsync()` calls.

### Delegate type for the anonymous function or method group

The https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/lambda-improvements.md#delegate-types section states:
>The compiler may allow more signatures to bind to `System.Action<>` and `System.Func<>` types in the future (if `ref struct` types are allowed type arguments for instance).

`Action<>` and `Func<>` types with `allows ref struct` constraints on their type parameters will be used in more scenarios
involving ref struct types in the delegate's signature.

If target runtime supports `allows ref struct` constraints, generic anonymous delegate types will include `allows ref struct` constraint for
their type parameters. This will enable substitution of those type parameters with ref struct types and other type parameters with
 `allows ref struct` constraint.

### Inline arrays

The https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/inline-arrays.md#detailed-design section states:
>Language will provide a type-safe/ref-safe way for accessing elements of inline array types. The access will be span based.
>This limits support to inline array types with element types that can be used as a type argument.

When span types are changed to support spans of ref structs, the limitation should be lifted for inline arrays of ref structs. 

## Soundness

We would like to verify the soundness of both the `ref struct` anti-constraint in particular and the anti-constraint concept in general. To do so we'd like to take advantage of the existing soundness proofs provided for the C# type system. This task is made easier by defining a new language that is similar to C#, but more regular in construction. We will verify the safety of that model, and then specify a sound translation to this language. Because this new language is centered around constraints, we'll call this language "constraint-C#".

The primary ref struct safety invariant that must be preserved is that variables of ref struct type must not appear on the heap. We can encode this restriction via a constraint. Because constraints permit substitution, not forbid it, we will technically define the inverse constraint: `heap`. The `heap` constraint specifies that a type may appear on the heap. In "constraint-C#" all types satisfy the `heap` constraint except for ref-structs. Moreover, all existing type parameters in C# will be lowered to type parameters with the `heap` constraint in "constraint-C#".

Now, assuming that existing C# is safe, we can transfer the C# ref-struct rules to "constraint-C#".

  1. Fields of classes cannot have a ref-struct type.
  2. Static fields cannot have a ref-struct type.
  3. Variables of ref-struct type cannot be converted to non-ref structs.
  4. Variables of ref-struct type cannot be substituted as type arguments.
  5. Variables of ref-struct type cannot implement interfaces.

The new rules apply to the `heap` constraint:

  1. Fields of classes must have types that satisfy the `heap` constraint.
  2. Static fields must have types that satisfy the `heap` constraint.
  3. Types with the `heap` constraint have only the identity conversion.
  4. Variables of ref-struct type can only be substituted for type parameters without the `heap` constraint.
  5. Ref-struct types may only implement interfaces without default-interface-members.

Rules (4) and (5) are slightly altered. Note that rule (4) does not need to be transferred exactly because we have a notion of type parameters without the `heap` contraint. Rule (5) is complicated. Implementing interfaces is not universally unsound, but default interface methods imply a receiver of interface type, which is a non-value type and violates rule (3). Thus, default-interface-members are disallowed.

With these rules, "constraint-C#" is ref-struct safe, supports type substitution, and supports interface implementation. The next step is to translate the language defined in this proposal, which we may call "allow-C#", into "constraint-C#". Fortunately, this is trivial. The lowering is a straightforward syntactic transformation. The syntax `where T : allows ref struct` in "allow-C#" is equivalent in "constraint-C#" to no constraint and the absence of "allow clauses" is equivalent to the `heap` constraint. Since the abstract semantics and typing are equivalent, "allow-C#" is also sound.

There is one last property which we might consider: whether all typed terms in C# are also typed in "constraint-C#". In other words, we want to know if, for all terms `t` in C#, whether the corresponding term `t'` after lowering to "constraint-C#" is well-typed. This is not a soundness constraint -- making terms ill-typed in our target language would never allow unsafety -- rather, it concerns backwards-compatibility. If we decide to use the typing of "constraint-C#" to validate "allow-C#", we would like to confirm that we are not making any existing C# code illegal.

Since all C# terms start as valid "constraint-C#" terms, we can validate preservation by examining each of our new "constraint-C#" restrictions. First, the addition of the `heap` constraint. Since all type parameters in C# would acquire the `heap` constraint, all existing terms must satisfy said constraint. This is true for all concrete types except ref structs, which is appropriate since ref structs may not appear as type arguments today. It is also true for all type parameters, since they would all themselves acquire the `heap` constraint. Moreover, since the `heap` constraint is a valid combination with all other constraints, this would not present any problems. Rules (1-5) would not present any problems since they directly correspond to existing C# rules, or are relaxations thereof. Therefore, all typeable terms in C# should be typeable in "constraint-C#" and we should not introduce any typing breaking changes.

## Open Issues

### Anti-Constraint syntax

**Decision**: use `where T: allows ref struct`

This proposal chose to expose the `ref struct` anti-constraint by augmenting the existing `where` syntax to include `allows ref struct`. This both succinctly describes the feature and is also expandable to include other anti-constraints in the future like pointers. There are other solutions considered that are worth discussing.

The first is simply picking another syntax to use within the `where` clause. Other proposed options included:

- `~ref struct`: the `~` serves as a marker that the syntax that follows is an anti-constraint.
- `include ref struct`: using `includes` instead of `allows`

```csharp
void M<T>(T p)
    where T : IDisposable, ~ref struct
{
    p.Dispose();
}
```

The second is to use a new clause entirely to make it clear that what follows is expanding the set of allowed types. Proponents of this feel that using syntax within `where` could lead to confusion when reading. The initial proposal used the following syntax: `allow T: ref struct`:

```csharp
void M<T>(T p)
    where T : IDisposable
    allow T : ref struct
{
    p.Dispose();
}
```

The `where T: allows ref struct` syntax had a slightly stronger preference in LDM discussions.

### Co and contra variance

**Decision**: no new issues

To be maximally useful type parameters that are `allows ref struct` must be compatible with generic variance. Specifically it must be legal for a parameter to be both co/contravariant and also `allows ref struct`. Lacking that they would not be usable in many of the most popular `delegate` and `interface` types in .NET like `Func<T>`, `Action<T>`, `IEnumerable<T>`, etc ...

After discussion it was concluded this is a non-issue. The `allows ref struct` constraint is just another way that `struct` can be used as generic arguments. Just as a normal `struct` argument removes the variance of an API so will a `ref struct`. 

### Auto-applying to delegate members

**Decision**: do not auto-apply

For many generic `delegate` members the language could automatically apply `allows ref struct` as it's purely an upside change. Consider that for `Func<> / Action<>` style delegates and most interface definitions there is no downside to expanding to allowing `ref struct`. The language can outline rules where it is safe to automatically apply this anti-constraint. This removes the manual process and would speed up the adoption of this feature.

This auto application of `allows ref struct` poses a few problems though. The first is in multi-targeted scenarios. Code would compile in one target framework but fail in another and there is no syntactic indicator of why the APIs should behave differently.

```csharp
// Works in net9.0 but fails in all other TF
Func<Span<char>> func;
```

This is likely to lead to customer confusion and looking at changes in `Func<T>` in the `net9.0` source wouldn't give customers any clue as to what changed.

The other issue is that very subtle changes in code can cause _spooky action at a distance_ problems. Consider the following code:

```csharp
interface I1<T>
{
}
```

This interface would be eligible for auto-application of `allows ref struct`. If a developer comes around later though and adds a default interface method then suddenly it would not be and it would break any consumers that had already created invocations like `I1<Span<char>>`. This is a very subtle change that would be hard to track down.

### Binary breaking change

Adding `allows ref struct` to an existing API is not a source breaking change. It is purely expanding the set of allowed types for an API. Need to track down if this is a binary breaking change or not. Unclear if updating the attributes of a generic parameter constitute a binary breaking change.

### Warn on DIM invocation

Should the compiler warn on the following invocation of `M` as it creates the opportunity for a runtime exception?

```csharp
interface I1
{
    // Virtual method with default implementation
    void M() { }
}

// Invocation of a virtual instance method with default implementation in a generic method that has the `allows ref struct`
// anti-constraint
void M<T>(T p)
    where T : allows ref struct, I1
{
    p.M(); // Warn?
}
```

This, however, could be noisy and not very helpful in majority of scenarios. C# will require ref structs to implement all virtual APIs.
Therefore, assuming that other players follow the same rule, the only situation when this might cause an exception is when the method
is added after the fact. The author of the consuming code often has no knowledge of all these details and often has no control over 
ref structs that will be consumed by the code. Therefore, the only action the author can really take is to suppress the warning.

## Considerations

### Runtime support

This feature requires several pieces of support from the runtime / libraries team:

- Preventing default interface methods from applying to `ref struct`
- API in `System.Reflection.Metadata` for encoding the `gpAcceptByRefLike` value
- Support for generic parameters being a `ref struct`

Most of this support is likely already in place. The general `ref struct` as generic parameter support is already implemented as described [here][byref-like-generics]. It's possible the DIM implementation already account for `ref struct`. But each of these items needs to be tracked down.

### API versioning

#### allows ref struct anti-constraint

The `allows ref struct` anti-constraint can be safely applied to a large number of generic definitions that do not have implementations. That means most delegates, interfaces and `abstract` methods can safely apply `allows ref struct` to their parameters. These are just API definitions without implementations and hence expanding the set of allowed types is only going to result in errors if they're used as type arguments where `ref struct` are not allowed.

API owners can rely on a simple rule of "if it compiles, it's safe". The compiler will error on any unsafe uses of `allows ref struct`, just as it does for other `ref struct` uses.

At the same time though there are versioning considerations API authors should consider. Essentially API owners should avoid adding `allows ref struct` to type parameters where the owning type / member may change in the future to be incompatible with `allows ref struct`. For example:

- An `abstract` method which may later change to a `virtual` method
- An `abstract` type which may later add implementations

In such cases an API author should be careful about adding `allows ref struct` unless they are certain the type / member evolution will not using `T` in a way that breaks `ref struct` rules.

Removing the `allows ref struct` anti-constraint is always a breaking change: source and binary.

#### Default Interface Methods

API authors need to be aware that adding DIMS will break `ref struct` implementors until they are recompiled. This is similar to [existing DIM behavior][dim-diamond] where by adding a DIM to an interface will break existing implementations until they are recompiled. That means API authors need to consider the likelihood of `ref struct` implementations when adding DIMs.

There are three code components that are needed to create this situation:

```csharp
interface I1
{
    // 1. The addition of a DIM method to an _existing_ interface
    void M() { }
}

// 2. A ref struct implementing the interface but not explicitly defining the DIM 
// method
ref struct S : I1 { }

// 3. The invocation of the DIM method in a generic method that has the `allows ref struct`
// anti-constraint
void M<T>(T p)
    where T : allows ref struct, I1
{
    p.M();
}
```

All of three of these components are needed to create this particular issue. Further at least (1) and (2) must be in different assemblies. If they were in the same assembly then a compilation error would occur.

#### UnscopedRef

Adding or removing `[UnscopedRef]` from `interface` members is a source breaking change (and potentially creating runtime issues). The attribute should be applied when defining an interface member and not added or removed later.

### Span&lt;Span&lt;T&gt;&gt;

This combination of features does not allow for constructs such as `Span<Span<T>>`. This is made a bit clearer by looking at the definition of `Span<T>`:

```csharp
readonly ref struct Span<T>
{
    public readonly ref T _data;
    public readonly int _length;

    public Span(T[] array) { ... }

    public static implicit operator Span<T>(T[]? array) { }
 
    public static implicit operator Span<T>(ArraySegment<T> segment) { }
}
```

If this type definition were to include `allows ref struct` then all `T` instances in the definition would need be treated as if they were potentially a `ref struct` type. That presents two classes of problems.

The first is for APIs like `Span(T[] array)` and the implicit operators the `T` cannot be a `ref struct`: it's either used as an array element or as generic parameter which cannot be `allows ref struct`. There are a handful of public APIs on `Span<T>` that have uses of `T` that cannot be compatible with a `ref struct`. These are public API that cannot be deleted and hence must be rationalized by the language. The most likely path forward is the compiler will special case `Span<T>` and issue an error code ever bound to one of these APIs when the argument for `T` is _potentially_ a `ref struct`.

The second is that the language does not support `ref` fields that are `ref struct`. There is a [design proposal][ref-struct-ref-fields] for allowing that feature. It's unclear if that will be accepted into the language or if it's expressive enough to handle the full set of scenarios around `Span<T>`.

Both of these issues are beyond the scope of this proposal.

### UnscopedRef Implementation Logic

The rationale behind the `[UnscopedRef]` rules for interface implementation is easiest to understand when visualizing the `this` parameter as an explicit, rather than implicit, argument to the methods. Consider for example the following `struct` where `this` is visualized as an implicit parameter (similar to how Python handles it):

```csharp
struct S
{
    public void M(scoped ref S this) { }
}
```

The `[UnscopedRef]` on an interface member is specifying that `this` lacks `scoped` for lifetime purposes at the call site. Allowing `[UnscopedRef]` to be ommitted on the implementing member is effectively allowing a parameter that is `ref T` to be implemented by a parameter that is `scoped ref T`. The language already allows this:

```csharp
interface I1
{
    void M(ref Span<char> span);
}

struct S : I1
{
    public void M(scoped ref Span<char> span) { }
}
```

## Related Items

Related Items:

- https://github.com/dotnet/csharplang/issues/7608
- https://github.com/dotnet/csharplang/pull/7555
- https://github.com/dotnet/runtime/blob/main/docs/design/features/byreflike-generics.md
- https://github.com/dotnet/runtime/pull/67783
- https://github.com/dotnet/runtime/issues/27229#issuecomment-1537274804
- https://github.com/dotnet/runtime/issues/68002

[ref-struct-ref-fields]: https://github.com/dotnet/csharplang/blob/main/proposals/expand-ref.md
[ref-struct-generics]: #ref-struct-generic-parameters
[byref-like-generics]: https://github.com/dotnet/runtime/blob/main/docs/design/features/byreflike-generics.md
[dim-diamond]: https://github.com/dotnet/csharplang/blob/main/meetings/2018/LDM-2018-10-17.md#diamond-inheritance
[unscoped-ref-impl]: #unscopedref-implementation-logic
[warn-DIM]: #warn-on-dim-invocation
