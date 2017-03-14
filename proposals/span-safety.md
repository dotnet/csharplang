# Compile time enforcement of safety for ref-like types #

The main reason for the additional safety rules when dealing with types like `Span<T>` is that such types must be confined to the execution stack.
 
There are three reasons why `Span<T>` must be a stack-only type.

1. `Span<T>` is semantically a struct containing a reference and a range - `(ref T data, int length)`. Regardless of actual implementation, writes to such struct would not be atomic. Concurrent "tearing" of such struct would lead to the possibility of `length` not matching the `data`, causing out-of-range accesses and type-safety violations, which ultimately could result in GC heap corruption in seemingly "safe" code.
2. Some implementations of `Span<T>` literally contain a managed pointer in one of its fields. Managed pointers are not supported as fields of heap objects and code that manages to put a managed pointer on the GC heap typically crashes at JIT time.
3. It is permitted for a `Span<T>` to refer to data in the local stack frame - individual local variables or `stackalloc`-ed arrays. A scenario when an instance of a `Span<T>` outlives the referred data would lead to undefined behavior, including type-safety violations and heap corruptions.

All the above problems would be alleviated if instances of `Span<T>` would be allowed as stack data types only. In fact for the `#3` we need a stronger guarantee. Details on that to follow below.

An additional problem arises due to composition. It would be generally desirable to build more complex data types that would embed `Span<T>` instances. Such composite types would have to be structs and would share all the hazards and requirements of `Span<T>`. As a result the safety rules described here should be viewed as applicable to the whole range of **_ref-like types_**

At the minimum the restrictions described below would need to apply to `Span<T>`.

## `ref-like` types must be stack-only. ##

C# compiler already has a concept of a restricted types that covers special platform types such as `TypedReference`. In some cases those types are referred as ref-like types as well. We should probably use some different term to refer to `TypedReference` and similar types - like `restricted types` to avoid confusion.

In this document the "ref like" types include only `Span<T>` and related types. And the ref-like variables mean variables of "ref-like" types.  

In order to force ref-like variables to be stack only, we need the following restrictions: 

- ref-like type cannot be a type of an array element
- ref-like type cannot be used as a generic type argument
- ref-like variable cannot be boxed
- ref-like type cannot be a field of ordinary not ref-like type
- indirect restrictions, such as disallowed use of ref-like types in async methods, which are really a result of disallowing ref-like typed fields.

Note:

- it is ok to allow returning ref-like values from methods (additional rules apply when referring to local data)
- it is ok for a ref-like type be a field of another struct as long as the struct itself is ref-like.


## ref-like variables can be passed as `in` parameters ##

- it is ok to pass a ref-like variable by reference to a call as long as the reference is **_readonly_** . 
The reason is that exposing ref-like variables via writeable references make it nearly impossible to reason about their safety.

Example:

```cs

Span<T> Caller()
{
	Span<T> safeToReturn;

	if (TodaIsNotFriday())
	{
		// callee can "poison" any assumption that we can made about safeToReturn
		Callee(ref safeToReturn);
	}

	// is this safe?
	return safeToReturn;
}

void Callee(ref Span<int> arg1)
{
	// local refernces are escaping. 
	// what rule would disallow this?
	arg1 = new Span<T>(localloc(...));
}

```

Note: returning by a ordinary writeable reference appears to be ok by itself, but with values on heap being impossible, values in local frame being not returnable and variables passed from the caller being readonly, it may not matter.

Another way to look at this is that writeable references to a ref-like variable would never be exposed outside of the containing method, except, of course, if that method is a constructor which has to take `this` by a writeable ref.

## `ref-like` types must be readonly structs. ##

- ref-like types must be readonly. 

This is basically follows from the previous "only pass by read-only references" rule when applied to "this" inside members of ref-like types.

## not all `ref-like` variables are safe to return. ##

In addition to the regular rules that govern ref-returnability, there are additional rules that apply to by-value returns of ref-like typed variables.

* ref-like variables, free of local references are safe to return.
* ref-like parameters are safe to return.
* ref-like fields are safe to return as long as the receiver is safe to return
* a ref-like value, returned from an expression (unary, binary, conditional...) is safe to return if all ref and ref-like operands were safe to return.
* “this” is not considered safe to return inside the members of ref-like types
* a ref-like value, returned from a method is safe to return if all ref/out variables and ref-like values passed to that method as formal parameters were safe to return.
Specifically it is irrelevant if receiver is safe to return, regardless whether receiver is a struct, class or typed as a generic type parameter.

## Reassignment of `ref-like` variables. ##

Reassignment of ref-like variables to new values can easily circumvent scoping/life-time invariants. A particular concern is when a ref-like variable with a bigger span is assigned a ref-like variable that refers to a variable from an inner scope.
It would be even worse if the LHS is a returnable variable, but the RHS is not a returnable value.  

There are ways to prevent such problems:

The most permissive approach would be alias/value tracking via fixed point data-flow analysis. It is very expensive and complex approach to be practical, but will be called out here for completeness.

Simpler solutions are:
- make ref-like variables "single-assignment" similarly to the solution we have for ordinary ref variables.
- allow reassignment of ref-like variables, but only as long as the new value is ref-returnable.


