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


