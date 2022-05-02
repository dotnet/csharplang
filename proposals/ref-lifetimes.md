
# Ref lifetimes

## Defining safety rules

With the addition of ref fields into the language the safety rules need to be expanded to
encompass them. Fundamentally, the ref safety rules are have only one purpose: to ensure
that a ref to a location is not referencable after the location's lifetime ends.

All value locations (i.e., things to which you can take a `ref`) have an existing lifetime
already defined by the C# language. The only interesting augmentations are around ref
variables.

The basic rule can be defined quite simply: a ref variable's lifetime must never be longer
than the value it points to. For ref locals, this is trivial. A ref local is always initialized
to a value expression or another ref expression. For a value expression, its lifetime can be
assigned the lifetime of the value expression. For a ref expression, the lifetime is the same
as the ref expression, which is safe by definition.

Ref parameters and returns are more complicated because they do not have an initializing value
expression. In fact, different call sites may have completely different and incomparable lifetime
parameters. If we were to strictly follow the rule that the lifetime for a variable must be no
longer than the shortest lifetime, there would be no valid lifetime across all call sites.

Instead, we can introduce generic lifetimes, which allow each call site to substitute a given
lifetime for each lifetime parameter. To represent such a scheme, we must introduce a syntax for
specifying the lifetime of a ref variable. To differentiate from conventional generics, we will add
the generic parameters as belonging to the `ref` keyword for ref variables, instead of the type
syntax. We will also use the `'` token to prefix the names of each parameter. For instance,

```C#
ref<'a> T M<'a,'b, T>(ref<'a> T p1, ref<'b> int p2) { ... }
```

is a fully lifetime-annotated version of

```C#
ref T M<T>(ref T p1, ref int p2) { ... }
```

`'a` and `'b` are lifetime generics which are substituted on a call to `M`. Inside `M`, the exact
lifetimes of `'a` and '`b'` are not known, but because they are provided by the caller, they
must live at least as long as the method itself.

As it is, as long as we can express the C# lifetime conventions in the above syntax in a way
which type checks (according to conventional rules of generic substition and compatibility), safety
is guaranteed.

However, we must also account for ref structs and ref fields. In fact, the problem of ref structs
is almost fully analogous to methods. Ref fields, like ref parameters, do not have a single lifetime.
Instead, the lifetime is defined at the point of creation (or substitution). However, because ref 
structs do not end their life at the end of their constructor, they must themselves have a lifetime.
According to the basic rule of lifetime safety, the lifetime of a ref must be shorter than its target,
and therefore the lifetime of a ref struct must be the shortest of all the lifetime parameters passed.

We can use a similar syntax extension for ref structs and ref fields as well:

```C#
ref struct S<'a, 'b, T>
{
    ref<'a> T F1;
    ref<'b> int F2;
}
```

As with all generic variables, they must be compatible to support assignment. As stated before, only longer
lifetimes are assignable to shorter lifetimes. Notably, lifetimes like `'a` and `'b` provided above are
not comparable as given and therefore cannot be assigned to one another.

For example:

```C#
S<'a, 'b, T> M<'a, 'b, T>(ref<'a> T p1, ref<'b> int p2)
{
    var s1 = new S<'a, 'b, T>() { F1 = p1, F2 = p2 }; // OK, type checks as equal lifetimes
    return s1; // OK, types match

    int x = 0;
    var s2 = new S<'a, 'b, T>() { F1 = p1, F2 = ref x }; // Error, `x` is method-local lifetime, 'b is longer
    return s2; // OK, types match

    var s3 = new S<'a, 'b, T>() { F1 = p1, F2 = ref (new int[] { 2 })[0] }; // OK, arrays have global lifetime and can be assigned to any variable
    return s3; // OK types match

    var s4 = new S<T>() { F1 = p1, F2 = ref x }; // OK, lifetime of S.F2 is inferred as method-local lifetime
    return s4; // Error: s4 lifetime is method-local, shorter than 'a & 'b
}
```


## Syntax simplifactions