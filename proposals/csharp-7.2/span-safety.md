# Compile time enforcement of safety for ref-like types

## Introduction

The main reason for the additional safety rules when dealing with types like `Span<T>` and `ReadOnlySpan<T>` is that such types must be confined to the execution stack.
 
There are two reasons why `Span<T>` and similar types must be a stack-only types.

1. `Span<T>` is semantically a struct containing a reference and a range - `(ref T data, int length)`. Regardless of actual implementation, writes to such struct would not be atomic. Concurrent "tearing" of such struct would lead to the possibility of `length` not matching the `data`, causing out-of-range accesses and type-safety violations, which ultimately could result in GC heap corruption in seemingly "safe" code.
2. Some implementations of `Span<T>` literally contain a managed pointer in one of its fields. Managed pointers are not supported as fields of heap objects and code that manages to put a managed pointer on the GC heap typically crashes at JIT time.

All the above problems would be alleviated if instances of `Span<T>` are constrained to exist only on the execution stack. 

An additional problem arises due to composition. It would be generally desirable to build more complex data types that would embed `Span<T>` and `ReadOnlySpan<T>` instances. Such composite types would have to be structs and would share all the hazards and requirements of `Span<T>`. As a result the safety rules described here should be viewed as applicable to the whole range of **_ref-like types_**.

The <a href="#draft-language-specification">draft language specification</a> is intended to ensure that values of a ref-like type occurs only on the stack.

## Generalized `ref-like` types in source code

`ref-like` structs are explicitly marked in the source code using `ref` modifier:

```csharp
ref struct TwoSpans<T>
{
	// can have ref-like instance fields
	public Span<T> first;
	public Span<T> second;
} 

// error: arrays of ref-like types are not allowed. 
TwoSpans<T>[] arr = null;

```

Designating a struct as ref-like will allow the struct to have ref-like instance fields and will also make all the requirements of ref-like types applicable to the struct. 

## Metadata representation or ref-like structs

Ref-like structs will be marked with **System.Runtime.CompilerServices.IsRefLikeAttribute** attribute.

The attribute will be added to common base libraries such as `mscorlib`. In a case if the attribute is not available, compiler will generate an internal one similarly to other embedded-on-demand attributes such as `IsReadOnlyAttribute`.

An additional measure will be taken to prevent the use of ref-like structs in compilers not familiar with the safety rules (this includes C# compilers prior to the one in which this feature is implemented). 

Having no other good alternatives that work in old compilers without servicing, an `Obsolete` attribute with a known string will be added to all ref-like structs. Compilers that know how to use ref-like types will ignore this particular form of `Obsolete`.

A typical metadata representation:

```csharp
    [IsRefLike]
    [Obsolete("Types with embedded references are not supported in this version of your compiler.")]
    public struct TwoSpans<T>
    {
       // . . . .
    }
```

NOTE: it is not the goal to make it so that any use of ref-like types on old compilers fails 100%. That is hard to achieve and is not strictly necessary. For example there would always be a way to get around the `Obsolete` using dynamic code or, for example, creating an array of ref-like types through reflection.

In particular, if user wants to actually put an `Obsolete` or `Deprecated` attribute on a ref-like type, we will have no choice other than not emitting the predefined one since `Obsolete` attribute cannot be applied more than once..  

## Examples:

```csharp
SpanLikeType M1(ref SpanLikeType x, Span<byte> y)
{
    // this is all valid, unconcerned with stack-referring stuff
    var local = new SpanLikeType(y);
    x = local;
    return x;
}

void Test1(ref SpanLikeType param1, Span<byte> param2)
{
    Span<byte> stackReferring1 = stackalloc byte[10];
    var stackReferring2 = new SpanLikeType(stackReferring1);

    // this is allowed
    stackReferring2 = M1(ref stackReferring2, stackReferring1);

    // this is NOT allowed
    stackReferring2 = M1(ref param1, stackReferring1);

    // this is NOT allowed
    param1 = M1(ref stackReferring2, stackReferring1);

    // this is NOT allowed
    param2 = stackReferring1.Slice(10);

    // this is allowed
    param1 = new SpanLikeType(param2);

    // this is allowed
    stackReferring2 = param1;
}

ref SpanLikeType M2(ref SpanLikeType x)
{
    return ref x;
}

ref SpanLikeType Test2(ref SpanLikeType param1, Span<byte> param2)
{
    Span<byte> stackReferring1 = stackalloc byte[10];
    var stackReferring2 = new SpanLikeType(stackReferring1);

    ref var stackReferring3 = M2(ref stackReferring2);

    // this is allowed
    stackReferring3 = M1(ref stackReferring2, stackReferring1);

    // this is allowed
    M2(ref stackReferring3) = stackReferring2;

    // this is NOT allowed
    M1(ref param1) = stackReferring2;

    // this is NOT allowed
    param1 = stackReferring3;

    // this is NOT allowed
    return ref stackReferring3;

    // this is allowed
    return ref param1;
}

```

----------------

## <a name="draft-language-specification"></a>Draft language specification

Below we describe a set of safety rules for ref-like types (`ref struct`s) to ensure that values of these types occur only on the stack. A different, simpler set of safety rules would be possible if locals cannot be passed by reference. This specification would also permit the safe reassignment of ref locals.

### Overview

We associate with each expression at compile-time the concept of what scope that expression is permitted to escape to, "safe-to-escape". Similarly, for each lvalue we maintain a concept of what scope a reference to it is permitted to escape to, "ref-safe-to-escape". For a given lvalue expression, these may be different.

These are analogous to the "safe to return" of the ref locals feature, but it is more fine-grained. Where the "safe-to-return" of an expression records only whether (or not) it may escape the enclosing method as a whole, the safe-to-escape records which scope it may escape to (which scope it may not escape beyond). The basic safety mechanism is enforced as follows. Given an assignment from an expression E1 with a safe-to-escape scope S1, to an (lvalue) expression E2 with safe-to-escape scope S2, it is an error if S2 is a wider scope than S1. By construction, the two scopes S1 and S2 are in a nesting relationship, because a legal expression is always safe-to-return from some scope enclosing the expression.

For the time being it is sufficient, for the purpose of the analysis, to support just two scopes - external to the method, and top-level scope of the method. That is because ref-like values with inner scopes cannot be created and ref locals do not support re-assignment. The rules, however, can support more than two scope levels.

The precise rules for computing the *safe-to-return* status of an expression, and the rules governing the legality of expressions, follow.

### ref-safe-to-escape

The *ref-safe-to-escape* is a scope, enclosing an lvalue expression, to which it is safe for a ref to the lvalue to escape to. If that scope is the entire method, we say that a ref to the lvalue is *safe to return* from the method.

### safe-to-escape

The *safe-to-escape* is a scope, enclosing an expression, to which it is safe for the value to escape to. If that scope is the entire method, we say that a the value is *safe to return* from the method.

An expression whose type is not a `ref struct` type is *safe-to-return* from the entire enclosing method. Otherwise we refer to the rules below.

#### Parameters

An lvalue designating a formal parameter is *ref-safe-to-escape* (by reference) as follows:
- If the parameter is a `ref`, `out`, or `in` parameter, it is *ref-safe-to-escape* from the entire method (e.g. by a `return ref` statement); otherwise
- If the parameter is the `this` parameter of a struct type, it is *ref-safe-to-escape* to the top-level scope of the method (but not from the entire method itself);
- Otherwise the parameter is a value parameter, and it is *ref-safe-to-escape* to the top-level scope of the method (but not from the method itself).

An expression that is an rvalue designating the use of a formal parameter is *safe-to-escape* (by value) from the entire method (e.g. by a `return` statement). This applies to the `this` parameter as well.

#### Locals

An lvalue designating a local variable is *ref-safe-to-escape* (by reference) as follows:
- If the variable is a `ref` variable, then its *ref-safe-to-escape* is taken from the *ref-safe-to-escape* of its initializing expression; otherwise
- The variable is *ref-safe-to-escape* the scope in which it was declared.

An expression that is an rvalue designating the use of a local variable is *safe-to-escape* (by value) as follows:
- But the general rule above, a local whose type is not a `ref struct` type is *safe-to-return* from the entire enclosing method.
- If the variable is an iteration variable of a `foreach` loop, then the variable's *safe-to-escape* scope is the same as the *safe-to-escape* of the `foreach` loop's expression.
- A local of `ref struct` type and uninitialized at the point of declaration is *safe-to-return* from the entire enclosing method.
- Otherwise the variable's type is a `ref struct` type, and the variable's declaration requires an initializer. The variable's *safe-to-escape* scope is the same as the *safe-to-escape* of its initializer.

#### Field reference

An lvalue designating a reference to a field, `e.F`, is *ref-safe-to-escape* (by reference) as follows:
- If `e` is of a reference type, it is *ref-safe-to-escape* from the entire method; otherwise
- If `e` is of a value type, its *ref-safe-to-escape* is taken from the *ref-safe-to-escape* of `e`.

An rvalue designating a reference to a field, `e.F`, has a *safe-to-escape* scope that is the same as the *safe-to-escape* of `e`.

#### Operators including `?:`

The application of a user-defined operator is treated as a method invocation.

For an operator that yields an rvalue, such as `e1 + e2` or `c ? e1 : e2`, the *safe-to-escape* of the result is the narrowest scope among the *safe-to-escape* of the operands of the operator.  As a consequence, for a unary operator that yields an rvalue, such as `+e`, the *safe-to-escape* of the result is the *safe-to-escape* of the operand.

For an operator that yields an lvalue, such as `c ? ref e1 : ref e2`
- the *ref-safe-to-escape* of the result is the narrowest scope among the *ref-safe-to-escape* of the operands of the operator.
- the *safe-to-escape* of the operands must agree, and that is the *safe-to-escape* of the resulting lvalue.

#### Method invocation

An lvalue resulting from a ref-returning method invocation `e1.M(e2, ...)` is *ref-safe-to-escape* the smallest of the following scopes:
- The entire enclosing method
- the *ref-safe-to-escape* of all `ref` and `out` argument expressions (excluding the receiver)
- For each `in` parameter of the method, if there is a corresponding expression that is an lvalue, its *ref-safe-to-escape*, otherwise the nearest enclosing scope
- the *safe-to-escape* of all argument expressions (including the receiver)

> Note: the last bullet is necessary to handle code such as
> ```csharp
> var sp = new Span(...)
> return ref sp[0];
> ```
> or
> ```csharp
> return ref M(sp, 0);
> ```

An rvalue resulting from a method invocation `e1.M(e2, ...)` is *safe-to-escape* from the smallest of the following scopes:
- The entire enclosing method
- the *safe-to-escape* of all argument expressions (including the receiver)

#### An Rvalue
An rvalue is *ref-safe-to-escape* from the nearest enclosing scope. This occurs for example in an invocation such as `M(ref d.Length)` where `d` is of type `dynamic`. It is also consistent with (and perhaps subsumes) our handling of arguments corresponding to `in` parameters.*

#### Property invocations

A property invocation (either `get` or `set`) it treated as a method invocation of the underlying method by the above rules.

#### `stackalloc`

A stackalloc expression is an rvalue that is *safe-to-escape* to the top-level scope of the method (but not from the entire method itself).

#### Constructor invocations

A `new` expression that invokes a constructor obeys the same rules as a method invocation that is considered to return the type being constructed.

In addition *safe-to-escape* is no wider than the smallest of the *safe-to-escape* of all arguments/operands of the object initializer expressions, recursively, if initializer is present. 

#### Span constructor
The language relies on `Span<T>` not having a constructor of the following form:

```csharp
void Example(ref int x)
{
    // Create a span of length one
    var span = new Span<int>(ref x); 
}
```

Such a constructor makes `Span<T>` which are used as fields indistinguishable from a `ref` field. The safety rules described in this document
depend on `ref` fields not being a valid construct in C#, or .NET.

#### `default` expressions

A `default` expression is *safe-to-escape* from the entire enclosing method.

## Language Constraints

We wish to ensure that no `ref` local variable, and no variable of `ref struct` type, refers to stack memory or variables that are no longer alive. We therefore have the following language constraints:

- Neither a ref parameter, nor a ref local, nor a parameter or local of a `ref struct` type can be lifted into a lambda or local function.

- Neither a ref parameter nor a parameter of a `ref struct` type may be an argument on an iterator method or an `async` method.

- Neither a ref local, nor a local of a `ref struct` type may be in scope at the point of a `yield return` statement or an `await` expression.

- A `ref struct` type may not be used as a type argument, or as an element type in a tuple type.

- A `ref struct` type may not be the declared type of a field, except that it may be the declared type of an instance field of another `ref struct`.

- A `ref struct` type may not be the element type of an array.

- A value of a `ref struct` type may not be boxed:
  - There is no conversion from a `ref struct` type to the type `object` or the type `System.ValueType`.
  - A `ref struct` type may not be declared to implement any interface
  - No instance method declared in `object` or in `System.ValueType` but not overridden in a `ref struct` type may be called with a receiver of that `ref struct` type.
  - No instance method of a `ref struct` type may be captured by method conversion to a delegate type.

- For a ref reassignment `ref e1 = ref e2`, the *ref-safe-to-escape* of `e2` must be at least as wide a scope as the *ref-safe-to-escape* of `e1`.

- For a ref return statement `return ref e1`, the *ref-safe-to-escape* of `e1` must be *ref-safe-to-escape* from the entire method. (TODO: Do we also need a rule that `e1` must be *safe-to-escape* from the entire method, or is that redundant?)

- For a return statement `return e1`, the *safe-to-escape* of `e1` must be *safe-to-escape* from the entire method.

- For an assignment `e1 = e2`, if the type of `e1` is a `ref struct` type, then the *safe-to-escape* of `e2` must be at least as wide a scope as the *safe-to-escape* of `e1`.

- In a method invocation, the following constraints apply:
  - If there is a `ref` or `out` argument to a `ref struct` type (including the receiver), with *safe-to-escape* E1, then
  - no argument (including the receiver) may have a narrower *safe-to-escape* than E1.

- A local function or anonymous function may not refer to a local or parameter of `ref struct` type declared in an enclosing scope.

> ***Open Issue:*** We need some rule that permits us to produce an error when needing to spill a stack value of a `ref struct` type at an await expression, for example in the code
> ```csharp
> Foo(new Span<int>(...), await e2);
> ```

## Future Considerations

### Length one Span<T> over ref values
Though not legal today there are cases where creating a length one `Span<T>` instance over a value would be beneficial:

```csharp
void RefExample()
{
    int x = ...;

    // Today creating a length one Span<int> requires a stackalloc and a new 
    // local
    Span<int> span1 = stackalloc [] { x };
    Use(span1);
    x = span1[0]; 

    // Simpler to just allow length one span
    var span2 = new Span<int>(ref x);
    Use(span2);
}
```

This feature gets more compelling if we lift the restrictions on [fixed sized buffers](https://github.com/dotnet/csharplang/blob/master/proposals/fixed-sized-buffers.md) as it would
allow for `Span<T>` instances of even greater length. 

If there is ever a need to go down this path then the language could accommodate this by ensuring such `Span<T>` instances
were downward facing only. That is they were only ever *safe-to-escape* to the scope in which they were created. This ensure
the language never had to consider a `ref` value escaping a method via a `ref struct` return or field of `ref struct`. This
would likely also require further changes to recognize such constructors as capturing a `ref` parameter in this way though.
