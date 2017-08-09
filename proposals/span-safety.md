# Compile time enforcement of safety for ref-like types #

The main reason for the additional safety rules when dealing with types like `Span<T>` and `ReadonlySpan<T>` is that such types must be confined to the execution stack.
 
There are two reasons why `Span<T>` and similar types must be a stack-only types.

1. `Span<T>` is semantically a struct containing a reference and a range - `(ref T data, int length)`. Regardless of actual implementation, writes to such struct would not be atomic. Concurrent "tearing" of such struct would lead to the possibility of `length` not matching the `data`, causing out-of-range accesses and type-safety violations, which ultimately could result in GC heap corruption in seemingly "safe" code.
2. Some implementations of `Span<T>` literally contain a managed pointer in one of its fields. Managed pointers are not supported as fields of heap objects and code that manages to put a managed pointer on the GC heap typically crashes at JIT time.

All the above problems would be alleviated if instances of `Span<T>` are constrained to exist only on the execution stack. 

An additional problem arises due to composition. It would be generally desirable to build more complex data types that would embed `Span<T>` and `ReadonlySpan<T>` instances. Such composite types would have to be structs and would share all the hazards and requirements of `Span<T>`. As a result the safety rules described here should be viewed as applicable to the whole range of **_ref-like types_**

## `ref-like` types must be stack-only. ##

C# compiler already has a concept of types with stack-only requirements that covers special platform types such as `TypedReference`. In some cases those types are referred as ref-like types as well. We should probably use some different term to refer to `TypedReference` and similar types - like `restricted types` to avoid confusion.

In this document the "ref-like" types include only `Span<T>` and related types. And the ref-like variables mean variables of "ref-like" types.  

In order to force ref-like variables to be stack only, we need the following restrictions: 

- ref-like type cannot be a type of an array element
- ref-like type cannot be used as a generic type argument
- ref-like variable cannot be boxed
- ref-like type cannot be a static field
- ref-like type cannot be an instance field of ordinary not ref-like type
- indirect restrictions, such as disallowed use of ref-like typed parameters and locals in async methods, which are really a result of disallowing ref-like typed fields.

Note:

- it is ok to return ref-like values from methods/lambdas, including by reference.
- it is ok to pass ref-like values to methods/lambdas, including by reference.
- it is ok for a ref-like type be a field of another struct as long as the struct itself is ref-like.
- it is ok for an intermediate span values to be used in lambdas/async methods. 


*Possible incremental relaxations*
- It may be possible to allow span locals be defined and used in a block inside an async method as long as the block does not contain `await` expressions. 

## Generalized `ref-like` types in source code.

`ref-like` structs will have to be explicitly marked in the source code using `ref` modifier:

```c#
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

An alternative proposal of "inferring" the ref-like property from the fact that a struct contains ref-like fields was also considered and rejected for the following reasons:

1) Implicit cascading of ref-like through containing structs appears to be dangerous. 
Since the implicit ref-like would work recursively, it would be too easy to turn multiple structs, possibly not in the current project, to become ref-like with one change.  

1) Span<T> and ReadOnlySpan<T> themselves do not contain ref-like fields. These types will have to be special-cased, which will be an issue if another trivial ref-like type needs to be added in the future.  

## Metadata representation or ref-like structs.

Ref-like structs will be marked with **System.Runtime.CompilerServices.IsRefLikeAttribute** attribute.

The attribute will be added to common base libraries such as `mscorlib`. In a case if the attribute is not available, compiler will generate an internal one similarly to other embedded-on-demand attributes such as `IsReadOnlyAttribute`.

An additional measure will be taken to prevent the use of ref-like structs in compilers not familiar with the safety rules (this includes C# compilers prior to the one in which this feature is implemented). 

Having no other good alternatives that work in old compilers without servicing, an `Obsolete` attribute with a known string will be added to all ref-like structs. Compilers that know how to use ref-like types will ignore this particular form of `Obsolete`.

A typical matadata representation: 

```C#
    [IsRefLike]
    [Obsolete("Types with embedded references are not supported in this version of your compiler.")]
    public struct TwoSpans<T>
    {
       . . . . 
    }
```

NOTE: it is not the goal to make it so that any use of ref-like types on old compilers fails 100%. That is hard to achieve and is not strictly necessary. For example there would always be away to get around the `Obsolete` using dynamic code or, for example, creating an array of ref-like types through reflection.

In particular, if user wants to actually put an `Obsolete` attribute on a ref-like type, we will have no choice other than not emitting the predefined one since `Obsolete` attribute cannot be applied more than once..  
(TODO: We should consider giving a warning in such scenario, informing user of the danger.)

## Stack-referring spans

An additional desirable enhancement for `Span<T>` is to allow referring to data in the local stack frame - individual local variables or `stackalloc`-ed arrays.

The tricky part here is that a scenario when an instance of a `Span<T>` outlives the referred data would lead to undefined behavior, including type-safety violations and heap corruptions. Therefore, in order to allow stack-referring spans in safe code, we need additional rules. 

The design goal here is to make rules for stack-referring spans "pay-for-play".  
**A person who does not intend to trade in stack-referring spans should be able to stop reading right here.**
 
NOTE: nothing will prevent a possibility to use Spans that wrap pointers to stack-allocated arrays in _unsafe_ code as long as it is possible to wrap a pointer in a span. As per usual rules of _unsafe_, it would be responsibility of the user to not cause GC holes.

## Stack-referring spans in safe code.

There are two main scenarios where stack-referring spans could come into being.  
- Stackalloc spans.
  To enable safe wrapping of stack allocated memory in a span, we will need a special syntax that would not allow intermediate pointer to escape.
  A preliminary solution is to use target-typing.

```cs
  //This usage of stackalloc does not require unsafe 
  Span<int> sp = stackalloc int[100]; 
```
  
- Spans wrapping individual locals or parameters. [Suggest disallowing]
 
```cs
  int x = 42;

  //This is currently not possible
  Span<int> sp = new Span<int>(ref x);
```

  This is currently not possible in Span API. I suggest to never allow. 

  While in theory possible, one-element spans are not as practically appealing as stackalloc ones.

  One problem is that in addition to being tied to the stack frame, local variables have scopes, so we would need extra rules to prevent mixing locals of different life-times when passing by reference or assigning to other locals.
 
  The main problem is the "pay-for-play" rule. If we have to assume that any span value returned from a member that takes `ref/out/in` parameters, including `this` in struct members might be stack-referring. People that do not use stack-referring spans will be affected by this.

```cs
  int x = 42; 

  //Do I really have a stack-referring span here?
  Span<int> sp = SomeMethod(ref x); 

```

## Safety rules for stack-referring spans.
Note: all these rules equally apply to ref-like types in general. I just use span for brevity.

- stack-referring span values cannot be returned from a method.

- local variable initialized using a stack-referring expression is considered a stack-referring local.

- stack-referring local is conservatively considered as always returning a stack-referring value.

- stack-referring values cannot be assigned to any variable with exception of stack-referring locals.

- "What comes in is what comes out" rule for span-returning expressions:
  If any operand of an expression is a stack-referring value, the result is a stack-referring value.
  This applies equally to ternary expressions, indexers, method calls, and so on. 
  Receiver of an invocation/property/indexer is considered an operand here.
 
  In particular: _Slice of a stack-referring value is a stack-referring value._  

- "No Mixing with refs to ordinary" rule: 
  Stack-referring spans can be passed as an argument as long as no other argument is an ordinary span variable (not a stack-referring local), and:
  a) is passed via a writeable reference `ref/out` or 
  b) is a receiver, and the containing span-like struct is not a readonly struct. 


==
- [_only if allow referring to regular locals_] stack-referring value cannot be passed via a writeable reference.
- [_only if allow referring to regular locals_] stack-referring value cannot be assigned to a local except at initialization.


Examples:

```c#

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

Draft language specification
==========================

Below we describe a set of safety rules for ref-like types (`ref struct`s), which includes the ability to pass locals by reference to constructors or instance methods of the type, suitable for use in the language specification. A different, simpler set of safety rules are possible if locals cannot be passed by reference. This specification would also permit the safe reassignment of ref locals.

Overview
========

We associate with each expression at compile-time the concept of what scope that expression is permitted to escape to, "safe-to-escape". Similarly, for each lvalue we maintain a concept of what scope its ref is permitted to escape to, "ref-safe-to-escape". For a given lvalue expression, these may be different.

These are analogous to the "safe to return" of the ref locals feature, but it is more fine-grained. Where the "safe-to-return" of an expression records only whether (or not) it may escape the enclosing method as a whole, the safe-to-escape records which scope it may escape to (which scope it may not escape beyond). The basic safety mechanism is enforced as follows. Given an assignment from an expression E1 with a safe-to-escape scope S1, to an (lvalue) expression E2 with safe-to-escape scope S2, it is an error if S2 is a wider scope than S1. By construction, the two scopes S1 and S2 are in a nesting relationship, because a legal expression is always safe-to-return from some scope enclosing the expression.

The reason the safe-to-escape needs to be a scope rather than a boolean is to handle situations such as the following

``` c#
{
    int i = 0;

    // make a span wrapping local variable i
    var s1 = new Span<int>(ref i);

    {
        int j = 0;

        // make a span wrapping a further nested local variable j
        var s2 = new Span<int>(ref j);

        // error: captures a reference to j into an enclosing scope
        s1 = s2;
    }

    // If permitted, here is where a problem would occur, as we'd be assigning into
    // a local whose lifetime has ended.
    s1[0] = 12; // assign to whichever local the span references
}
```

The precise rules for computing the *safe-to-return* status of an expression, and the rules governing the legality of expressions, follows.

# ref-safe-to-escape

The *ref-safe-to-escape* is a scope, enclosing an lvalue expression, to which it is safe for a ref to the lvalue to escape to. If that scope is the entire method, we say that a ref to the lvalue is *safe to return* from the method.

# safe-to-escape

The *safe-to-escape* is a scope, enclosing an expression, to which it is safe for the value to escape to. If that scope is the entire method, we say that a the value is *safe to return* from the method.

An expression whose type is not a `ref struct` type is *safe-to-return* from the entire enclosing method. Otherwise we refer to the rules below.

## Parameters

An lvalue designating a formal parameter is *ref-safe-to-escape* (by reference) as follows:
- If the parameter is a ref or out parameter, it is *ref-safe-to-escape* from the entire method (e.g. by a `return ref` statement); otherwise
- If the parameter is the `this` parameter of a struct type, it is *ref-safe-to-escape* to the top-level scope of the method (but not from the entire method itself);
- Otherwise the parameter is a value parameter, and it is *ref-safe-to-escape* to the top-level scope of the method (but not from the method itself). This includes parameters of `ref struct` type.

An expression that is an rvalue designating the use of a formal parameter is *safe-to-escape* (by value) from the entire method (e.g. by a `return` statement). This applies to the `this` parameter as well.

## Locals

An lvalue designating a local variable is *ref-safe-to-escape* (by reference) as follows:
- If the variable is a `ref` variable, then its *ref-safe-to-escape* is taken from the *ref-safe-to-escape* of its initializing expression; otherwise
- The variable is *ref-safe-to-escape* the scope in which it was declared.

An expression that is an rvalue designating the use of a local variable is *safe-to-escape* (by value) as follows:
- If the variable's type is a `ref struct` type, then the variable's declaration requires an initializer, and the variable's *safe-to-escape* scope is taken from that initializer.

> ***Open Issue:*** can we permit locals of `ref struct` type to be uninitialized at the point of declaration? If so, what would we record as the variable's *safe-to-escape* scope?

## Field reference

An lvalue designating a reference to a field, `e.F`, is *ref-safe-to-escape* (by reference) as follows:
- If `e` is of a reference type, it is *ref-safe-to-escape* from the entire method; otherwise
- If `e` is of a value type, its *ref-safe-to-escape* is taken from the *ref-safe-to-escape* of `e`.

An rvalue designating a reference to a field, `e.F`, has a *safe-to-escape* scope that is the same as the *safe-to-escape* of `e`.

## ?: and other multi-operand operators

For an operator with multiple operands that yields an rvalue, such as `e1 + e2` or `c ? e1 : e2`, the *safe-to-escape* of the result is the narrowest scope among the *safe-to-escape* of the operands of the operator.

For an operator with multiple operands that yields an lvalue, such as `c ? ref e1 : ref e2`, the *ref-safe-to-escape* of the operands must agree, and that is the *ref-safe-to-escape* of the resulting lvalue.

## Method invocation

An lvalue resulting from a ref-returning method invocation `e1.M(e2, ...)` is *ref-safe-to-escape* the smallest of the following scopes:
- The entire enclosing method
- the *ref-safe-to-escape* of all `ref` and `out` argument expressions (excluding the receiver)
- the *safe-to-escape* of all argument expressions (including the receiver)

> Note: the last bullet is necessary to handle code such as
> ``` c#
> var sp = new Span(...)
> return ref sp[0];
> ```
> or
> ``` c#
> return ref M(sp, 0);
> ```

An rvalue resulting from a method invocation `e1.M(e2, ...)` is *safe-to-escape* from the smallest of the following scopes:
- The entire enclosing method
- the *ref-safe-to-escape* of all `ref` and `out` argument expressions (excluding the receiver)
- the *safe-to-escape* of all argument expressions (including the receiver)

> Note that these rules are identical to the above rules for *ref-safe-to-escape*, but apply only when the return type is a `ref struct` type.

## Property invocations

A property invocation (either `get` or `set`) it treated as a method invocation of the underlying method by the above rules.

## `stackalloc`

A stackalloc expression is an rvalue that is *safe-to-escape* to the top-level scope of the method (but not from the entire method itself).

## Constructor invocations

A `new` expression that invokes a constructor obeys the same rules as a method invocation that is considered to return the type being constructed.

## `default` expressions

A `default` expression is *safe-to-escape* from the entire enclosing method.

## Other operators

***TODO: What others need to be handled? Should survey the language syntactic forms that are capable of producing a `ref struct` type.***

# Language Constraints

We wish to ensure that no `ref` local variable, and no variable of `ref struct` type, refers to stack memory or variables that are no longer alive. We therefore have the following language constraints:

- Neither a ref parameter, nor a ref local, nor a parameter or local of a `ref struct` type can be lifted into a lambda.

- Neither a ref parameter nor a parameter of a `ref struct` type may be an argument on an iterator method or an `async` method.

- A ref local may not be in scope at the point of a `yield return` statement or an `await` expression.

- A `ref struct` type may not be used as a type argument, or as an element type in a tuple type.

- A `ref struct` type may not be the declared type of a field, except that it may be the declared type of an instance field of another `ref struct`.

- A `ref struct` type may not be the element type of an array.

- A value of a `ref struct` type may not be boxed:
  - There is no conversion from a `ref struct` type to the type `object` or the type `System.ValueType`.
  - A `ref struct` type may not be declared to implement any interface
  - No instance method declared in `object` or in `System.ValueType` but not overridden in a `ref struct` type may be called with a receiver of that `ref struct` type.
  - No instance method of a `ref struct` type may be captured by method conversion to a delegate type.

- For a ref reassignment `ref e1 = ref e2`, the *ref-safe-to-escape* of `e2` must be at least as wide a scope as the *ref-safe-to-escape* of e1.

- For a ref return statement `return ref e1`, the *ref-safe-to-escape* of `e1` must be *ref-safe-to-escape* from the entire method. (TODO: Do we also need a rule that `e1` must be *safe-to-escape* from the entire method, or is that redundant?)

- For a return statement `return e1`, the *safe-to-escape* of `e1` must be *safe-to-escape* from the entire method.

- For an assignment `e1 = e2`, if the type of `e1` is a `ref struct` type, then the *safe-to-escape* of `e2` must be at least as wide a scope as the *safe-to-escape* of e1.

- In a method invocation, the following constraints apply:
  - If there is a `ref` or `out` argument to a `ref struct` type (including the receiver), with *safe-to-escape* E1, then
    - no `ref` or `out` argument (excluding the receiver) may have a narrower *ref-safe-to-escape* than E1; and
    - no argument (including the receiver) may have a narrower *safe-to-escape* than E1.

> ***Open Issue:*** We need some rule that permits us to produce an error when needing to spill a stack value of a `ref struct` type at an await expression, for example in the code
> ``` c#
> Foo(new Span<int>(...), await e2);
> ```

> ***Open Issue:*** This treatment does not yet consider local functions. The required rule might be of the form that "a local function may not refer to a local or parameter of `ref struct` type declared in an enclosing scope.

> ***Open Issue:*** This treatment does not yet consider `in` parameters. The required rule might be that a value argument to an `in` parameter has a *ref-safe-to-escape* that is the largest enclosing expression that contains no statement, ctor-initializer, or field initializer. This may break the invariant that the *ref-safe-to-escape* is some enclosing scope, as the result of the call can be captured in a ref local, which can then be used later. Perhaps we need to forbid capturing it in a ref local.

> ***Open Issue:*** This treatment does not yet consider an argument expression of the form `ref d.F` where d is of type dynamic. The required rule may be similar to the treatment of `in` parameters.




