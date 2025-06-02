
# Lifetime variance

This doc is meant as an addendum to https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/low-level-struct-improvements.md.

The primary purpose is to more formally ground the variance of lifetime parameters and the corresponding correctness rules.

In the above doc we treat ref variables as their own class of variables. However, by using a struct syntax we can unify the treatment of ref structs and by-ref variables.

First, we will define a primitive type to represent a by-ref variable:

```csharp
ref struct ByRef<$a, T>
{
    public T Value { get; set; }
}
```

This is the syntactic equivalent of `ref<$a> T`. One problem with this formulation is that C# currently defines all struct type parameters as invariant. However, this is not necessary. Structs are merely aggregations of their field data. That is, simple use of any struct type parameter `T` as a field type should not carry any additional variance restrictions. We do not currently have a syntax in C# to represent bivariance, but we can invent one: `inout`.

With this syntax we can alter the definition to

```csharp
ref struct ByRef<inout $a, T>
{
    public T Value { get; set; }
}
```

Note that `T` still remains invariant. This is for two reasons. First, because the intent of this doc is only to change struct variance for lifetimes, not for non-lifetime type parameters. Second, `ByRef` is a very special type of struct: it contains a pointer. Variance restrictions are due to a combination of two factors: mutability and aliasing. When a variable may be aliased but only allows reading, it is covariant. When it may be aliased but only allows writing, it is contravariant. When it may be aliased and it allows both reading and writing, it is invariant. When it does not allow aliasing, it is bivariant, regardless of access. Because `T` exists behind a read/write pointer, it is invariant.

However, `$a` is not behind the pointer -- it is a property of the `ByRef`, not the target. This means it remains `inout`.

We can also extend this definition to handle `ref readonly`:

```csharp
ref struct ByRefReadonly<inout $a, out T>
{
    public T Value { get; }
}
```

Note that, once again, the lifetime is behind the pointer and therefore the variance of the lifetime is unchanged. The only change is to the `T` variable -- `out` instead of invariant.

Having defined the base case of `ByRef`, we can extend the lifetime variance rules to all ref structs. Ref structs don't differ from structs except in allow ref structs as fields, so the only change to variance is in their ref-struct fields. Specifically, after inference every ref struct will be of the form

```csharp
ref struct S<$a, $a2, ... $an, T1, T2, ... Tn> { ... }
```

All lifetime variables should also be used by at least one of the fields. We can then proceed to assign variance annotations based on the usage of the variable with the most restrictive variance. That is, for each lifetime variable `$an`, its lifetime variance is:

1. `inout`, if every field that references it does so in an `inout` context
2. `out`, if every field that references it does so in either an `inout` or `out` context
3. `in`, if every field that references it does so in either an `inout` or `in` context
3. invariant, if any field references it in an invariant context, or appears in both `out` and `in` contexts

Here are some examples of each of these situations:

```csharp
ref struct S<inout $a>
{
    public ByRef<$a, int> Field;
}
```

In the above, `$a` only appears as a lifetime argument to `ByRef<inout $a, T>`. Because that is an `inout` context, the `$a` variable in `S` is also in an `inout` context.

```csharp
ref struct S1<inout $a, T>
{
    public ByRef<$a, T> Field;
}
ref struct S2<$a, $b>
{
    public ByRef<$a, S1<$b, int>> Field;
}
```

In this case, `S2.$a` is invariant. This is because it appears in the _second_ type argument of `ByRef<inout $a, T>`, which is invariant.

```csharp
ref struct S1<inout $a, T>
{
    public ByRef<$a, T> Field;
}
ref struct S2<out $a>
{
    public Task<S1<$a, int>> Field;
}
```

Here `$a` appears in a type argument to `Task<out T>`, meaning `$a` must also be restricted to `out`.

```csharp
ref struct S1<inout $a, T>
{
    public ByRef<$a, T> Field;
}
ref struct S2<in $a>
{
    public Action<S1<$a, int>> Field;
}
```

Here `$a` appears in a type argument to `Action<in T>`, meaning `$a` must also be restricted to `in`.


Note that this formulation differs slightly from the existing one, in that there is no special lifetime variable for `$this`. There are two reasons:

1. Self-referential variables are harder to analyze
2. There is no specific corresponding field, which also makes things more complicated

Overall, it's simpler to not have a special `$this` variable. Instead, we'll move the handling to instance methods themselves. Rather than hiding the receiver, we'll instead write it out explicitily:

```csharp
ref struct S<...>
{
    void M() { ... }
}
```

becomes

```csharp
ref struct S<...>
{
    static void M(ref S<...> this) { ... }
}
```

It's now clear that the receiver type is a ref-variable. Therefore, we will rewrite it like we have for other refs, using the `ByRef` syntax:

```csharp
ref struct S<...>
{
    static void M<$this>(ByRef<$this, S<...>> this) { ... }
}
```

On the type-checking side, this is very simple -- there are no special cases. On the inference side, since every instance method is invoked with an obvious receiver, we can always automatically infer the lifetime variable using the receiver lifetime.
