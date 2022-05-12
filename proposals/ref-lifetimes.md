
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
as the ref expression.

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

### Implicit lifetimes

While the above construction is very flexible and can represent almost any lifetime construction existing
or being considered for C#, it's worthwhile to consider other syntaxes and how they could map to the above.
The main advantage of this is that we can reduce the safety verification to a question of whether the
proposed translation type checks according to the above rules. If it does, and the above rules are safe, then
the alternate syntax is sound.

First, let's consider existing C#, which doesn't have any lifetime annotations. We can work out the implicit
annotations by examining the existing C# safety rules.

For the simplest cases, only one ref parameter or return type, there is only one possible translation:

```C#
// Case 1
ref int M1() { ... }
// Case 2
void M2(ref int x) { ...}
// Case 3
Span<int> M3() { ... }
// Case 4
void M4<'a>(Span<'a, int> s) { ... }

// Map to
// Case 1
ref<'global> int M1() { ... }
// Case 2
void M2<'a>(ref<'a> int x) { ... }
// Case 3
// Span<T> is actually Span<'a, T>
Span<'global, int> M3() { ... }
// Case 4
void M4<'a>(Span<'a, int> s) { ...}
```

This is sound and presents no problems. 

Cases with multiple parameters or return types are more complicated, as there are multiple possible choices.
We can discover which one C# chose by what is legal and what is an error.

```C#
// Case 1
ref int M1(ref int x)
{
    return ref x; // legal
}
// Case 2
ref int M2(ref int x, ref int y)
{
    x = ref y; // legal
    if (...)
    {
        return ref x; // legal
    }
    else
    {
        return ref y; // legal
    }
}
// Case 3
public ref int Outer()
{
    long y = 0;
    return ref M3(ref new[] { 0}[0], ref y); // error
}
public ref int M3(ref int x, ref long y)
{
    return ref x;
}
// Case 4
Span<int> M4(ref int x, Span<int> y)
{
    return y;
}
Span<int> Outer()
{
    int x = 0;
    return M4(ref x, new int[] { 0 }); // legal
}

// Map to

// Case 1
// The parameter is assignable to the return value (is returnable) so
// the lifetime of the parameter must be compatible with the return. The
// simplest choice is for their lifetimes to match.
ref<'a> int M1<'a>(ref<'a> int x) { ... }
// Case 2
// The parameters are assignable to each other, and the return type, so once
// again, they should match.
ref<'a> int M2<'a>(ref<'a> int x, ref<'a> int y) { ... }
// Case 3
// Looks like the above cases, but note the error. Despite the mismatched types,
// it is illegal to return the result of a call with the only matching parameter
// type having a global lifetime. This implies that types don't affect lifetime
// and that, once again, the lifetime of the return is the narrowest of all the
// input lifetimes, i.e. the lifetime generic variable is the same.
ref<'a> int M3<'a>(ref<'a> int x, ref<'a> long y) { ... }
// Case 4
// This is tricker than it seems. The ref variable and the Span variables don't
// directly interact, but because we can return the result of `M3` in `Outer`, we
// know that the variables must not share a lifetime, as the lifetime of `x` is
// narrower than the return value of `Outer`.
Span<'a, int> M4<'a, 'b>(ref<'b> int x, Span<'a, int> y) { ... }
```

The above is sound, and follows a mostly simple rule: all ref parameters and return
values share a single lifetime. The exception is ref parameters and ref structs. In
this case, ref structs have a different lifetime from the ref parameters. 

## Proposed expansion

There are a few additional syntaxes proposed for the addition of ref fields.

```C#
Span<int> CreateSpan(scoped ref int parameter)

// Maps to

// There is no direct mapping. This is more restrictive than the above proposal
// because there is no syntax for limiting a ref parameter lifetime to a local
// scope. We could introduce one, though (e.g., 'local scope)
Span<'a, int> CreateSpan<'a>(ref<'local> int parameter) { ...}
```

This is one case the lifetime syntax above can't describe without a new `'local`
lifetime annotation. However, we can satisfy the goals of the proposal without
matching the behavior exactly:

```C#
Span<int> CreateSpan(scoped ref int parameter)
Span<int> BadUseExamples(int parameter)
{
    // Legal in C# 10 and legal in C# 11 due to scoped ref
    return CreateSpan(ref parameter);

    // Legal in C# 10 and legal in C# 11 due to scoped ref
    int local = 42;
    return CreateSpan(ref local);

    // Legal in C# 10 and legal in C# 11 due to scoped ref
    Span<int> span = stackalloc int[42];
    return CreateSpan(ref span[0]);
}

// Maps to

// Introduce a new lifetime for the parameter, but don't constrain it to the
// local scope.
Span<'a, int> CreateSpan<'a, 'b>(ref<'b> int parameter)
Span<'global, int> BadUseExamples(int parameter)
{
    return CreateSpan(ref parameter);

    // Legal in C# 10 and legal in C# 11 due to scoped ref
    int local = 42;
    return CreateSpan(ref local);

    // Legal in C# 10 and legal in C# 11 due to scoped ref
    Span<int> span = stackalloc int[42];
    return CreateSpan(ref span[0]);
}
```

In the above translation, it's enough to make the lifetime parameters not convertible
to prevent inadvertant escape. There is no reason to limit the given ref parameter
to local-only scope, and it doesn't properly make sense, given that the input value
must come from outside the method and therefore the lifetime must be longer than the
current method.

Next is `unscoped`. This is an annotation for structs (not just ref structs), and support in this
translation implies that we must allow generic lifetimes on regular structs as well, which is a
small addition.

```C#
struct S
{
    int field; 

    // Error: `field` has the ref-safe-to-escape of `this` which is *current method* because 
    // it is a `scoped ref`
    ref int Prop1 => ref field;

    // Okay: `field` has the ref-safe-to-escape of `this` which is *calling method* because 
    // it is a `ref`
    unscoped ref int Prop1 => ref field;
}

// Maps to

struct S<'a> // 'a is the implicit lifetime of `this`
{
    int field;

    // Property returns implicitly define a new lifetime, like methods
    ref<'b> int Prop1<'b> => ref field; // error, `this.field` has lifetime `<'a>`

    // `unscoped` simply maps to the implicit `this` lifetime
    ref<'a> int Prop1 => field; // OK, matching lifetimes
}
```

The above is sound, and should match the intended semantics in the proposed design.