# Collection expressions

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
[summary]: #summary

Collection expressions introduce a new terse syntax, `[e1, e2, e3, etc]`, to create common collection values.  Inlining other collections into these values is possible using a spread operator `..` like so: `[e1, ..c2, e2, ..c2]`.

Several collection-like types can be created without requiring external BCL support.  These types are:

* [Array types](https://github.com/dotnet/csharplang/blob/main/spec/types.md#array-types), such as `int[]`.
* [`Span<T>`](https://learn.microsoft.com/dotnet/api/system.span-1) and [`ReadOnlySpan<T>`](https://learn.microsoft.com/dotnet/api/system.readonlyspan-1).
* Types that support [collection initializers](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#collection-initializers), such as [`List<T>`](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1).

Further support is present for collection-like types not covered under the above through a new attribute and API pattern that can be adopted directly on the type itself.

## Motivation
[motivation]: #motivation

* Collection-like values are hugely present in programming, algorithms, and especially in the C#/.NET ecosystem.  Nearly all programs will utilize these values to store data and send or receive data from other components. Currently, almost all C# programs must use many different and unfortunately verbose approaches to create instances of such values. Some approaches also have performance drawbacks. Here are some common examples:

  * Arrays, which require either `new Type[]` or `new[]` before the `{ ... }` values.
  * Spans, which may use `stackalloc` and other cumbersome constructs.
  * Collection initializers, which require syntax like `new List<T>` (lacking inference of a possibly verbose `T`) prior to their values, and which can cause multiple reallocations of memory because they use N `.Add` invocations without supplying an initial capacity.
  * Immutable collections, which require syntax like `ImmutableArray.Create(...)` to initialize the values, and which can cause intermediary allocations and data copying. More efficient construction forms (like `ImmutableArray.CreateBuilder`) are unwieldy and still produce unavoidable garbage.

* Looking at the surrounding ecosystem, we also find examples everywhere of list creation being more convenient and pleasant to use.  TypeScript, Dart, Swift, Elm, Python, and more opt for a succinct syntax for this purpose, with widespread usage, and to great effect. Cursory investigations have revealed no substantive problems arising in those ecosystems with having these literals built in.

* C# has also added [list patterns](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/list-patterns.md) in C# 11.  This pattern allows matching and deconstruction of list-like values using a clean and intuitive syntax.  However, unlike almost all other pattern constructs, this matching/deconstruction syntax lacks the corresponding construction syntax.

* Getting the best performance for constructing each collection type can be tricky. Simple solutions often waste both CPU and memory.  Having a literal form allows for maximum flexibility from the compiler implementation to optimize the literal to produce at least as good a result as a user could provide, but with simple code.  Very often the compiler will be able to do better, and the specification aims to allow the implementation large amounts of leeway in terms of implementation strategy to ensure this.

An inclusive solution is needed for C#. It should meet the vast majority of casse for customers in terms of the collection-like types and values they already have. It should also feel natural in the language and mirror the work done in pattern matching.

This leads to a natural conclusion that the syntax should be like `[e1, e2, e3, e-etc]` or `[e1, ..c2, e2]`, which correspond to the pattern equivalents of `[p1, p2, p3, p-etc]` and `[p1, ..p2, p3]`.

## Detailed design
[design]: #detailed-design

The following [grammar](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#primary-expressions) productions are added:

```diff
primary_no_array_creation_expression
  ...
+ | collection_expression
  ;

+ collection_expression
  : '[' ']'
  | '[' collection_element ( ',' collection_element )* ']'
  ;

+ collection_element
  : expression_element
  | spread_element
  ;

+ expression_element
  : expression
  ;

+ spread_element
  : '..' expression
  ;
```

Collection literals are [target-typed](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.1/target-typed-default.md#motivation).

### Spec clarifications
[spec-clarifications]: #spec-clarifications

* For brevity, `collection_expression` will be referred to as "literal" in the following sections.
* `expression_element` instances will commonly be referred to as `e1`, `e_n`, etc.
* `spread_element` instances will commonly be referred to as `..s1`, `..s_n`, etc.
* *span type* means either `Span<T>` or `ReadOnlySpan<T>`.
* Literals will commonly be shown as `[e1, ..s1, e2, ..s2, etc]` to convey any number of elements in any order.  Importantly, this form will be used to represent all cases such as:

  * Empty literals `[]`
  * Literals with no `expression_element` in them.
  * Literals with no `spread_element` in them.
  * Literals with arbitrary ordering of any element type.

* The *iteration type* of `..s_n` is the type of the *iteration variable* determined as if `s_n` were used as the expression being iterated over in a [`foreach_statement`](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement).
* Variables starting with `__name` are used to represent the results of the evaluation of `name`, stored in a location so that it is only evaluated once.  For example `__e1` is the evaluation of `e1`.
* `List<T>`, `IEnumerable<T>`, etc. refer to the respective types in the `System.Collections.Generic` namespace.
* The specification defines a [translation](#collection-literal-translation) of the literal to existing C# constructs.  Similar to the [*query expression translation*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11173-query-expression-translation), the literal is itself only legal if the translation would result in legal code.  The purpose of this rule is to avoid having to repeat other rules of the language that are implied (for example, about convertibility of expressions when assigned to storage locations).
* An implementation is not required to translate literals exactly as specified below.  Any translation is legal if the same result is produced and there are no observable differences in the production of the result.
  * For example, an implementation could translate literals like `[1, 2, 3]` directly to a `new int[] { 1, 2, 3 }` expression that itself bakes the raw data into the assembly, eliding the need for `__index` or a sequence of instructions to assign each value. Importantly, this does mean if any step of the translation might cause an exception at runtime that the program state is still left in the state indicated by the translation.

* References to 'stack allocation' refer to any strategy to allocate on the stack and not the heap.  Importantly, it does not imply or require that that strategy be through the actual `stackalloc` mechanism.  For example, the use of [inline arrays](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/inline-arrays.md) is also an allowed and desirable approach to accomplish stack allocation where available. 

* Collections are assumed to be well-behaved.  For example:

  * It is assumed that the value of `Count` on a collection will produce that same value as the number of elements when enumerated.
  * The types used in this spec defined in the `System.Collections.Generic` namespace are presumed to be side-effect free.  As such, the compiler can optimize scenarios where such types might be used as intermediary values, but otherwise not be exposed.
  * It is assumed that a call to some applicable `.AddRange(x)` member on a collection will result in the same final value as iterating over `x` and adding all of its enumerated values individually to the collection with `.Add`.
  * The behavior of collection literals with collections that are not well-behaved is undefined.

## Conversions
[conversions]: #conversions

A *collection expression conversion* allows a collection expression to be converted to a type.

The following implicit *collection expression conversions* exist from a collection expression:

* To a single dimensional *array type* `T[]` where:

  * For each *element* `Ei` there is an *implicit conversion* to `T`.

* To a *span type* `System.Span<T>` or `System.ReadOnlySpan<T>` where:

  * For each *element* `Ei` there is an *implicit conversion* to `T`.

* To a *type* with a *[create method](#create-methods)* with *parameter type* `System.ReadOnlySpan<T>` where:

  * For each *element* `Ei` there is an *implicit conversion* to `T`.

* To a *struct* or *class type* that implements `System.Collections.Generic.IEnumerable<T>` where:

  * For each *element* `Ei` there is an *implicit conversion* to `T`.

* To a *struct* or *class type* that implements `System.Collections.IEnumerable` and *does not implement* `System.Collections.Generic.IEnumerable<T>`.

* To an *interface type* `System.Collections.Generic.IEnumerable<T>`, `System.Collections.Generic.IReadOnlyCollection<T>`, `System.Collections.Generic.IReadOnlyList<T>`, `System.Collections.Generic.ICollection<T>`, or `System.Collections.Generic.IList<T>` where:

  * For each *element* `Ei` there is an *implicit conversion* to `T`.

In the cases above, a collection expression *element* `Ei` is considered to have an *implicit conversion* to *type* `T` if:

* `Ei` is an *expression element* and there is an implicit conversion from `Ei` to `T`.
* `Ei` is a *spread element* `Si` and there is an implicit conversion from the *iteration type* of `Si` to `T`.

Types for which there is an implicit collection expression conversion from a collection expression are the valid *target types* for that collection expression.

The following additional implicit conversions exist from a *collection expression*:

* To a *nullable value type* `T?` where there is a *collection expression conversion* from the collection expression to a value type `T`. The conversion is a *collection expression conversion* to `T` followed by an *implicit nullable conversion* from `T` to `T?`.

* To a reference type `T` where there is a *[create method](#create-methods)* associated with `T` that returns a type `U` and an *implicit reference conversion* from `U` to `T`. The conversion is a *collection expression conversion* to `U` followed by an *implicit reference conversion* from `U` to `T`.

* To an interface type `I` where there is a *[create method](#create-methods)* associated with `I` that returns a type `V` and an *implicit boxing conversion* from `V` to `I`. The conversion is a *collection expression conversion* to `V` followed by an *implicit boxing conversion* from `V` to `I`.

## Create methods
[create-methods]: #create-methods

A *create method* is indicated with a `[CollectionBuilder(...)]` attribute on the *collection type*.
The attribute specifies the *builder type* and *method name* of a method to be invoked to construct an instance of the collection type.

```c#
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        Inherited = false,
        AllowMultiple = false)]
    public sealed class CollectionBuilderAttribute : System.Attribute
    {
        public CollectionBuilderAttribute(Type builderType, string methodName);
        public Type BuilderType { get; }
        public string MethodName { get; }
    }
}
```
The attribute can be applied to a `class`, `struct`, `ref struct`, or `interface`.
The attribute is not inherited although the attribute can be applied to a base `class` or an `abstract class`.

The collection type must have an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement).

For the *create method*:

* The *builder type* must be a non-generic `class` or `struct`.
* The method must be defined on the *builder type* directly.
* The method must be `static`.
* The method must be accessible where the collection expression is used.
* The *arity* of the method must match the *arity* of the collection type.
* The method must have a single parameter of type `System.ReadOnlySpan<E>`, passed by value, and there is an [*identity conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1022-identity-conversion) from `E` to the [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) of the *collection type*.
* There is an [*identity conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1022-identity-conversion), [*implicit reference conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1028-implicit-reference-conversions), or [*boxing conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1029-boxing-conversions) from the method return type to the *collection type*.

An error is reported if the `[CollectionBuilder]` attribute does not refer to an invocable method with the expected signature.

Method overloads on the *builder type* with distinct signatures are ignored. Methods declared on base types or interfaces are ignored.

For a *collection expression* with a target type <code>C&lt;S<sub>0</sub>, S<sub>1</sub>, &mldr;&gt;</code> where the *type declaration* <code>C&lt;T<sub>0</sub>, T<sub>1</sub>, &mldr;&gt;</code> has an associated *builder method* <code>B.M&lt;U<sub>0</sub>, U<sub>1</sub>, &mldr;&gt;()</code>, the *generic type arguments* from the target type are applied in order &mdash; and from outermost containing type to innermost &mdash; to the *builder method*.

The span parameter for the *create method* can be explicitly marked `scoped` or `[UnscopedRef]`. If the parameter is implicitly or explicitly `scoped`, the compiler *may* allocate the storage for the span on the stack rather than the heap.

For example, a possible *create method* for `ImmutableArray<T>`:

```csharp
[CollectionBuilder(typeof(ImmutableArray), "Create")]
public struct ImmutableArray<T> { ... }

public static class ImmutableArray
{
    public static ImmutableArray<T> Create<T>(ReadOnlySpan<T> items) { ... }
}
```

With the *create method* above, `ImmutableArray<int> ia = [1, 2, 3];` could be emitted as:

```csharp
[InlineArray(3)] struct __InlineArray3<T> { private T _element0; }

Span<int> __tmp = new __InlineArray3<int>();
__tmp[0] = 1;
__tmp[1] = 2;
__tmp[2] = 3;
ImmutableArray<int> ia =
    ImmutableArray.Create((ReadOnlySpan<int>)__tmp);
```

## Construction
[construction]: #construction

The elements of a collection expression are *evaluated* in order, left to right.
Each element is evaluated exactly once, and any further references to the elements refer to the results of this initial evaluation.

A spread element may be *iterated* before or after the subsequent elements in the collection expression are *evaluated*.

An unhandled exception thrown from any of the methods used during construction will be uncaught and will prevent further steps in the construction.

`Length`, `Count`, and `GetEnumerator` are assumed to have no side effects.

---

If the target type is a *struct* or *class type* that implements `System.Collections.IEnumerable`, and the target type does not have a *[create method](#create-methods)*, the construction of the collection instance is as follows:

* The elements are evaluated in order. Some or all elements may be evaluated *during* the steps below rather than before.

* The compiler *may* determine the *known length* of the collection expression by invoking [*countable*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md#adding-index-and-range-support-to-existing-library-types) properties &mdash; or equivalent properties from well-known interfaces or types &mdash; on each *spread element expression*.

* The constructor that is applicable with no arguments is invoked.

* For each element in order:
  * If the element is an *expression element*, the applicable `Add` instance or extension method is invoked with the element *expression* as the argument. (Unlike classic [*collection initializer behavior*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117154-collection-initializers), element evaluation and `Add` calls are not necessarily interleaved.)
  * If the element is a *spread element* then one of the following is used:
    * An applicable `GetEnumerator` instance or extension method is invoked on the *spread element expression* and for each item from the enumerator the applicable `Add` instance or extension method is invoked on the *collection instance* with the item as the argument. If the enumerator implements `IDisposable`, then `Dispose` will be called after enumeration, regardless of exceptions.
    * An applicable `AddRange` instance or extension method is invoked on the *collection instance* with the spread element *expression* as the argument.
    * An applicable `CopyTo` instance or extension method is invoked on the *spread element expression* with the collection instance and `int` index as arguments.

* During the construction steps above, an applicable `EnsureCapacity` instance or extension method *may* be invoked one or more times on the *collection instance* with an `int` capacity argument.

---

If the target type is an *array*, a *span*, a type with a *[create method](#create-methods)*, or an *interface*, the construction of the collection instance is as follows:

* The elements are evaluated in order. Some or all elements may be evaluated *during* the steps below rather than before.

* The compiler *may* determine the *known length* of the collection expression by invoking [*countable*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md#adding-index-and-range-support-to-existing-library-types) properties &mdash; or equivalent properties from well-known interfaces or types &mdash; on each *spread element expression*.

* An *initialization instance* is created as follows:
  * If the target type is an *array* and the collection expression has a *known length*, an array is allocated with the expected length.
  * If the target type is a *span* or a type with a *create method*, and the collection has a *known length*, a span with the expected length is created referring to contiguous storage.
  * Otherwise intermediate storage is allocated.

* For each element in order:
  * If the element is an *expression element*, the initialization instance *indexer* is invoked to add the evaluated expression at the current index.
  * If the element is a *spread element* then one of the following is used:
    * A member of a well-known interface or type is invoked to copy items from the spread element expression to the initialization instance.
    * An applicable `GetEnumerator` instance or extension method is invoked on the *spread element expression* and for each item from the enumerator, the initialization instance *indexer* is invoked to add the item at the current index. If the enumerator implements `IDisposable`, then `Dispose` will be called after enumeration, regardless of exceptions.
    * An applicable `CopyTo` instance or extension method is invoked on the *spread element expression* with the initialization instance and `int` index as arguments.

* If intermediate storage was allocated for the collection, a collection instance is allocated with the actual collection length and the values from the initialization instance are copied to the collection instance, or if a span is required the compiler *may* use a span of the actual collection length from the intermediate storage. Otherwise the initialization instance is the collection instance.

* If the target type has a *create method*, the create method is invoked with the span instance.

---

> *Note:*
> The compiler may *delay* adding elements to the collection &mdash; or *delay* iterating through spread elements &mdash; until after evaluating subsequent elements. (When subsequent spread elements have *countable* properties that would allow calculating the expected length of the collection before allocating the collection.) Conversely, the compiler may *eagerly* add elements to the collection &mdash; and *eagerly* iterate through spread elements &mdash; when there is no advantage to delaying.
>
> Consider the following collection expression:
> ```c#
> int[] x = [a, ..b, ..c, d];
> ```
> 
> If spread elements `b` and `c` are *countable*, the compiler could delay adding items from `a` and `b` until after `c` is evaluated, to allow allocating the resulting array at the expected length. After that, the compiler could eagerly add items from `c`, before evaluating `d`.
> ```c#
> var __tmp1 = a;
> var __tmp2 = b;
> var __tmp3 = c;
> var __result = new int[2 + __tmp2.Length + __tmp3.Length];
> int __index = 0;
> __result[__index++] = __tmp1;
> foreach (var __i in __tmp2) __result[__index++] = __i;
> foreach (var __i in __tmp3) __result[__index++] = __i;
> __result[__index++] = d;
> x = __result;
> ```

## Empty collection literal

* The empty literal `[]` has no type.  However, similar to the [*null-literal*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/lexical-structure.md#6457-the-null-literal), this literal can be implicitly converted to any [*constructible*](#conversions) collection type.

    For example, the following is not legal as there is no *target type* and there are no other conversions involved:

    ```c#
    var v = []; // illegal
    ```

* Spreading an empty literal is permitted to be elided.  For example:

    ```c#
    bool b = ...
    List<int> l = [x, y, .. b ? [1, 2, 3] : []];
    ```

    Here, if `b` is false, it is not required that any value actually be constructed for the empty collection expression since it would immediately be spread into zero values in the final literal.

* The empty collection expression is permitted to be a singleton if used to construct a final collection value that is known to not be mutable.  For example:

    ```c#
    // Can be a singleton, like Array.Empty<int>()
    int[] x = []; 

    // Can be a singleton. Allowed to use Array.Empty<int>(), Enumerable.Empty<int>(),
    // or any other implementation that can not be mutated.
    IEnumerable<int> y = [];

    // Must not be a singleton.  Value must be allowed to mutate, and should not mutate
    // other references elsewhere.
    List<int> z = [];
    ```

## Ref safety
[ref-safety]: #ref-safety

See [*safe context constraint*](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/structs.md#164121-general) for definitions of the *safe-context* values: *declaration-block*, *function-member*, and *caller-context*.

The *safe-context* of a collection expression is:

* The safe-context of an empty collection expression `[]` is the *caller-context*.

* If the target type is a *span type* `System.ReadOnlySpan<T>`, and `T` is one of the *primitive types* `bool`, `sbyte`, `byte`, `short`, `ushort`, `char`, `int`, `uint`, `long`, `ulong`, `float`, or `double`, and the collection expression contains *constant values only*, the safe-context of the collection expression is the *caller-context*.

* If the target type is a *span type* `System.Span<T>` or `System.ReadOnlySpan<T>`, the safe-context of the collection expression is the *declaration-block*.

* If the target type is a *ref struct type* with a [*create method*](#create-methods), the safe-context of the collection expression is the [*safe-context of an invocation*](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/structs.md#164126-method-and-property-invocation) of the create method where the collection expression is the span argument to the method.

* Otherwise the safe-context of the collection expression is the *caller-context*.

A collection expression with a safe-context of *declaration-block* cannot escape the enclosing scope, and the compiler *may* store the collection on the stack rather than the heap.

To allow a collection expression for a ref struct type to escape the *declaration-block*, it may be necessary to cast the expression to another type.

```csharp
static ReadOnlySpan<int> AsSpanConstants()
{
    return [1, 2, 3]; // ok: span refers to assembly data section
}

static ReadOnlySpan<T> AsSpan2<T>(T x, T y)
{
    return [x, y];    // error: span may refer to stack data
}

static ReadOnlySpan<T> AsSpan3<T>(T x, T y, T z)
{
    return (T[])[x, y, z]; // ok: span refers to T[] on heap
}
```

## Type inference
[type-inference]: #type-inference

```c#
var a = AsArray([1, 2, 3]);          // AsArray<int>(int[])
var b = AsListOfArray([[4, 5], []]); // AsListOfArray<int>(List<int[]>)

static T[] AsArray<T>(T[] arg) => arg;
static List<T[]> AsListOfArray<T>(List<T[]> arg) => arg;
```

The [*type inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1163-type-inference) rules are updated as follows.

The existing rules for the [*first phase*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11632-the-first-phase) are extracted to a new *input type inference* section, and a  rule is added to *input type inference* and *output type inference* for collection expression expressions.

> 11.6.3.2 The first phase
>
> For each of the method arguments `Eᵢ`:
>
> * An *input type inference* is made *from* `Eᵢ` *to* the corresponding *parameter type* `Tᵢ`.
>
> An *input type inference* is made *from* an expression `E` *to* a type `T` in the following way:
>
> * If `E` is a *collection expression* with elements `Eᵢ`, and `T` is a type with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ` or `T` is a *nullable value type* `T0?` and `T0` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ`, then for each `Eᵢ`:
>   * If `Eᵢ` is an *expression element*, then an *input type inference* is made *from* `Eᵢ` *to* `Tₑ`.
>   * If `Eᵢ` is an *spread element* with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Sᵢ`, then a [*lower-bound inference*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116310-lower-bound-inferences) is made *from* `Sᵢ` *to* `Tₑ`.
> * *[existing rules from first phase]* ...

> 11.6.3.7 Output type inferences
>
> An *output type inference* is made *from* an expression `E` *to* a type `T` in the following way:
>
> * If `E` is a *collection expression* with elements `Eᵢ`, and `T` is a type with an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ` or `T` is a *nullable value type* `T0?` and `T0` has an [*iteration type*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/statements.md#1295-the-foreach-statement) `Tₑ`, then for each `Eᵢ`:
>   * If `Eᵢ` is an *expression element*, then an *output type inference* is made *from* `Eᵢ` *to* `Tₑ`.
>   * If `Eᵢ` is an *spread element*, no inference is made from `Eᵢ`.
> * *[existing rules from output type inferences]* ...

## Extension methods

No changes to [*extension method invocation*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11783-extension-method-invocations) rules. 

> 11.7.8.3 Extension method invocations
>
> An extension method `Cᵢ.Mₑ` is *eligible* if:
>
> * ...
> * An implicit identity, reference, or boxing conversion exists from *expr* to the type of the first parameter of `Mₑ`.

A collection expression does not have a natural type so the existing conversions from *type* are not applicable. As a result, a collection expression cannot be used directly as the first parameter for an extension method invocation.

```c#
static class Extensions
{
    public static ImmutableArray<T> AsImmutableArray<T>(this ImmutableArray<T> arg) => arg;
}

var x = [1].AsImmutableArray();           // error: collection expression has no target type
var y = [2].AsImmutableArray<int>();      // error: ...
var z = Extensions.AsImmutableArray([3]); // ok
```

## Overload resolution
[overload-resolution]: #overload-resolution

[*Better conversion from expression*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11644-better-conversion-from-expression) is updated to prefer certain target types in collection expression conversions.

In the updated rules:
* A *span_type* is one of:
  * `System.Span<T>`
  * `System.ReadOnlySpan<T>`.
* An *array_or_array_interface_or_string_type* is one of:
  * an *array type*
  * `System.String`
  * one of the following *interface types* implemented by an *array type*:
    * `System.Collections.Generic.IEnumerable<T>`
    * `System.Collections.Generic.IReadOnlyCollection<T>`
    * `System.Collections.Generic.IReadOnlyList<T>`
    * `System.Collections.Generic.ICollection<T>`
    * `System.Collections.Generic.IList<T>`

> Given an implicit conversion `C₁` that converts from an expression `E` to a type `T₁`, and an implicit conversion `C₂` that converts from an expression `E` to a type `T₂`, `C₁` is a ***better conversion*** than `C₂` if one of the following holds:
>
> * **`E` is a *collection expression* and one of the following holds:**
>   * **`T₁` is `System.ReadOnlySpan<E₁>`, and `T₂` is `System.Span<E₂>`, and an implicit conversion exists from `E₁` to `E₂`**
>   * **`T₁` is `System.ReadOnlySpan<E₁>` or `System.Span<E₁>`, and `T₂` is an *array_or_array_interface_or_string_type* with *iteration type* `E₂`, and an implicit conversion exists from `E₁` to `E₂`**
>   * **`T₁` is not a *span_type*, and `T₂` is not a *span_type*, and an implicit conversion exists from `T₁` to `T₂`**
> * **`E` is not a *collection expression* and one of the following holds:**
>   * `E` exactly matches `T₁` and `E` does not exactly match `T₂`
>   * `E` exactly matches both or neither of `T₁` and `T₂`, and `T₁` is a [*better conversion target*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11646-better-conversion-target) than `T₂`
> * `E` is a method group, ...

Examples of differences with overload resolution between array initializers and collection expressions:
```c#
static void Generic<T>(Span<T> value) { }
static void Generic<T>(T[] value) { }

static void SpanDerived(Span<string> value) { }
static void SpanDerived(object[] value) { }

static void ArrayDerived(Span<object> value) { }
static void ArrayDerived(string[] value) { }

// Array initializers
Generic(new[] { "" });      // string[]
SpanDerived(new[] { "" });  // ambiguous
ArrayDerived(new[] { "" }); // string[]

// Collection expressions
Generic([""]);              // Span<string>
SpanDerived([""]);          // Span<string>
ArrayDerived([""]);         // ambiguous
```

## Span types
[span-types]: #span-types

The span types `ReadOnlySpan<T>` and `Span<T>` are both [*constructible collection types*](#conversions).  Support for them follows the design for [`params Span<T>`](https://github.com/dotnet/csharplang/blob/main/proposals/params-span.md). Specifically, constructing either of those spans will result in an array T[] created on the [stack](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/unsafe-code.md#229-stack-allocation) if the params array is within limits (if any) set by the compiler. Otherwise the array will be allocated on the heap.

If the compiler chooses to allocate on the stack, it is not required to translate a literal directly to a `stackalloc` at that specific point.  For example, given:

```c#
foreach (var x in y)
{
    Span<int> span = [a, b, c];
    // do things with span
}
```

The compiler is allowed to translate that using `stackalloc` as long as the `Span` meaning stays the same and [*span-safety*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md) is maintained.  For example, it can translate the above to:

```c#
Span<int> __buffer = stackalloc int[3];
foreach (var x in y)
{
    __buffer[0] = a
    __buffer[1] = b
    __buffer[2] = c;
    Span<int> span = __buffer;
    // do things with span
}
```

The compiler can also [inline arrays](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/inline-arrays.md), if available, when choosing to allocate on the stack.

If the compiler decides to allocate on the heap, the translation for `Span<T>` is simply:

```c#
T[] __array = [...]; // using existing rules
Span<T> __result = __array;
```

## Collection literal translation
[collection-literal-translation]: #collection-literal-translation

A collection expression has a *known length* if the compile-time type of each *spread element* in the collection expression is [*countable*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md#adding-index-and-range-support-to-existing-library-types).

### Interface translation
[interface-translation]: #interface-translation

Given a target type `IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `ICollection<T>`, or `IList<T>`a compliant implementation is only required to produce a value that implements that interface.  A compliant implementation is free to: 

1. Use an existing type that implements that interface.
1. Synthesize a type that implements the interface.

In either case, the type used is allowed to implement a larger set of interfaces than those strictly required.

Synthesized types are free to employ any strategy they want to implement the required interfaces properly.  For example, a synthesized type might inline the elements directly within itself, avoiding the need for additional internal collection allocations.  A synthesized type could also not use any storage whatsoever, opting to compute the values directly.  For example, using `Enumerable.Range(1, 10)` for `[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]`.

#### Non-mutable interface translation
[non-mutable-interface-translation]: #non-mutable-interface-translation

Given a target type or `IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, the value generated is allowed to implement more interfaces than required.  For example, implementing the mutable interfaces as well (specifically, implementing `ICollection<T>` or `IList<T>`).  However, in that case:

1. The value must return `true` when queried for `ICollection<T>.IsReadOnly`.   This ensures consumers can appropriately tell that the collection is non-mutable, despite implementing the mutable views.
1. The value must throw on any call to a mutation method (like `IList<T>.Add`).  This ensures safety, preventing a non-mutable collection from being accidentally mutated.

It is recommended that any type that is synthesized implement all these interfaces. This ensures that maximal compatibility with existing libraries, including those that introspect the interfaces implemented by a value in order to light up performance optimizations.

#### Mutable interface translation
[non-mutable-interface-translation]: #non-mutable-interface-translation

Given a target type or `ICollection<T>` or `IList<T>`:

1. The value must return `false` when queried for `ICollection<T>.IsReadOnly`. 

The value generated is allowed to implement more interfaces than required.  Specifically, implementing `IList<T>` even when only targeting `ICollection<T>`.  However, in that case:

1. The value must support all mutation methods (like `IList<T>.RemoveAt`).

### Known length translation
[known-length-translation]: #known-length-translation

Having a *known length* allows for efficient construction of a result with the potential for no copying of data and no unnecessary slack space in a result.

Not having a *known length* does not prevent any result from being created. However, it may result in extra CPU and memory costs producing the data, then moving to the final destination.

* For a *known length* literal `[e1, ..s1, etc]`, the translation first starts with the following:

  ```c#
  int __len = count_of_expression_elements +
              __s1.Count;
              ...
              __s_n.Count;
    ```

* Given a target type `T` for that literal:

  * If `T` is some `T1[]`, then the literal is translated as:

    ```c#
    T1[] __result = new T1[__len];
    int __index = 0;

    __result[__index++] = __e1;
    foreach (T1 __t in __s1)
        __result[__index++] = __t;

    // further assignments of the remaining elements
    ```

    The implementation is allowed to utilize other means to populate the array.  For example, utilizing efficient bulk-copy methods like `.CopyTo()`.

  * If `T` is some `Span<T1>`, then the literal is translated as the same as above, except that the `__result` initialization is translated as:

    ```c#
    Span<T1> __result = new T1[__len];

    // same assignments as the array translation
    ```

    The translation may use `stackalloc T1[]` or an [*inline array*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/inline-arrays.md) rather than `new T1[]` if [*span-safety*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.2/span-safety.md) is maintained.

  * If `T` is some `ReadOnlySpan<T1>`, then the literal is translated the same as for the `Span<T1>` case except that the final result will be that `Span<T1>` [implicitly converted](https://learn.microsoft.com/dotnet/api/system.span-1.op_implicit#system-span-1-op-implicit(system-span((-0)))-system-readonlyspan((-0))) to a `ReadOnlySpan<T1>`.

    A `ReadOnlySpan<T1>` where `T1` is some primitive type, and all collection elements are constant does not need its data to be on the heap, or on the stack.  For example, an implementation could construct this span directly as a reference to portion of the data segment of the program.

    The above forms (for arrays and spans) are the base representations of the collection expression and are used for the following translation rules:

    * If `T` is some `C<S0, S1, …>` which has a corresponding [create-method](#create-methods) `B.M<U0, U1, …>()`, then the literal is translated as:

      ```c#
      // Collection literal is passed as is as the single B.M<...>(...) argument
      C<S0, S1, …> __result = B.M<S0, S1, …>([...])
      ```

      As the *create method* must have an argument type of some instantiated `ReadOnlySpan<T>`, the translation rule for spans applies when passing the collection expression to the create method.

    * If `T` supports [collection initializers](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#collection-initializers), then:

      * if the type `T` contains an accessible constructor with a single parameter `int capacity`, then the literal is translated as:

        ```c#
        T __result = new T(capacity: __len);
        __result.Add(__e1);
        foreach (var __t in __s1)
            __result.Add(__t);

        // further additions of the remaining elements
        ```

        Note: the name of the parameter is required to be `capacity`.

        This form allows for a literal to inform the newly constructed type of the count of elements to allow for efficient allocation of internal storage.  This avoids wasteful reallocations as the elements are added.

      * otherwise, the literal is translated as:

        ```c#
        T __result = new T();

        __result.Add(__e1);
        foreach (var __t in __s1)
            __result.Add(__t);

        // further additions of the remaining elements
        ```

        This allows creating the target type, albeit with no capacity optimization to prevent internal reallocation of storage.

### Unknown length translation
[unknown-length-translation]: #unknown-length-translation

* Given a target type `T` for an *unknown length* literal:

  * If `T` supports [collection initializers](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#collection-initializers), then the literal is translated as:

    ```c#
    T __result = new T();

    __result.Add(__e1);
    foreach (var __t in __s1)
        __result.Add(__t);

    // further additions of the remaining elements
    ```

    This allows spreading of any iterable type, albeit with the least amount of optimization possible.

  * If `T` is some `T1[]`, then the literal has the same semantics as:

    ```c#
    List<T1> __list = [...]; /* initialized using predefined rules */
    T1[] __result = __list.ToArray();
    ```

    The above is inefficient though; it creates the intermediary list, and then creates a copy of the final array from it.  Implementations are free to optimize this away, for example producing code like so:

    ```c#
    T1[] __result = <private_details>.CreateArray<T1>(
        count_of_expression_elements);
    int __index = 0;

    <private_details>.Add(ref __result, __index++, __e1);
    foreach (var __t in __s1)
        <private_details>.Add(ref __result, __index++, __t);

    // further additions of the remaining elements

    <private_details>.Resize(ref __result, __index);
    ```

    This allows for minimal waste and copying, without additional overhead that library collections might incur.

    The counts passed to `CreateArray` are used to provide a starting size hint to prevent wasteful resizes.

  * If `T` is some *span type*, an implementation may follow the above `T[]` strategy, or any other strategy with the same semantics, but better performance.  For example, instead of allocating the array as a copy of the list elements, `CollectionsMarshal.AsSpan(__list)` could be used to obtain a span value directly.

## Unsupported scenarios
[unsupported-scenarios]: #unsupported-scenarios

While collection literals can be used for many scenarios, there are a few that they are not capable of replacing.  These include:

* Multi-dimensional arrays (e.g. `new int[5, 10] { ... }`). There is no facility to include the dimensions, and all collection literals are either linear or map structures only.
* Collections which pass special values to their constructors. There is no facility to access the constructor being used.
* Nested collection initializers, e.g. `new Widget { Children = { w1, w2, w3 } }`.  This form needs to stay since it has very different semantics from `Children = [w1, w2, w3]`.  The former calls `.Add` repeatedly on `.Children` while the latter would assign a new collection over `.Children`.  We could consider having the latter form fall back to adding to an existing collection if `.Children` can't be assigned, but that seems like it could be extremely confusing.

## Syntax ambiguities
[syntax-ambiguities]: #syntax-ambiguities

* There are two "true" syntactic ambiguities where there are multiple legal syntactic interpretations of code that uses a `collection_literal_expression`.

  * The `spread_element` is ambiguous with a [`range_expression`](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md#systemrange).  One could technically have:

    ```c#
    Range[] ranges = [range1, ..e, range2];
    ```

    To resolve this, we can either:

    * Require users to parenthesize `(..e)` or include a start index `0..e` if they want a range.
    * Choose a different syntax (like `...`) for spread.  This would be unfortunate for the lack of consistency with slice patterns.

* There are two cases where there isn't a true ambiguity but where the syntax greatly increases parsing complexity.  While not a problem given engineering time, this does still increase cognitive overhead for users when looking at code.

  * Ambiguity between `collection_literal_expression` and `attributes` on statements or local functions.  Consider:

    ```c#
    [X(), Y, Z()]
    ```

    This could be one of:

    ```c#
    // A list literal inside some expression statement
    [X(), Y, Z()].ForEach(() => ...);

    // The attributes for a statement or local function
    [X(), Y, Z()] void LocalFunc() { }
    ```

    Without complex lookahead, it would be impossible to tell without consuming the entirety of the literal.

    Options to address this include:

    * Allow this, doing the parsing work to determine which of these cases this is.
    * Disallow this, and require the user wrap the literal in parentheses like `([X(), Y, Z()]).ForEach(...)`.
    * Ambiguity between a `collection_literal_expression` in a `conditional_expression` and a `null_conditional_operations`.  Consider:

    ```c#
    M(x ? [a, b, c]
    ```

    This could be one of:

    ```c#
    // A ternary conditional picking between two collections
    M(x ? [a, b, c] : [d, e, f]);

    // A null conditional safely indexing into 'x':
    M(x ? [a, b, c]);
    ```

    Without complex lookahead, it would be impossible to tell without consuming the entirety of the literal.

    Note: this is a problem even without a *natural type* because target typing applies through `conditional_expressions`.

    As with the others, we could require parentheses to disambiguate.  In other words, presume the `null_conditional_operation` interpretation unless written like so: `x ? ([1, 2, 3]) :`.  However, that seems rather unfortunate. This sort of code does not seem unreasonable to write and will likely trip people up.

## Drawbacks
[drawbacks]: #drawbacks

* This introduces [yet another form](https://xkcd.com/927/) for collection expressions on top of the myriad ways we already have. This is extra complexity for the language.  That said, this also makes it possible to unify on one ~~ring~~ syntax to rule them all, which means existing codebases can be simplified and moved to a uniform look everywhere.
* Using `[`...`]` instead of `{`...`}` moves away from the syntax we've generally used for arrays and collection initializers already.  Specifically that it uses `[`...`]` instead of `{`...`}`.  However, this was already settled on by the language team when we did list patterns.  We attempted to make `{`...`}` work with list patterns and ran into insurmountable issues.  Because of this, we moved to `[`...`]` which, while new for C#, feels natural in many programming languages and allowed us to start fresh with no ambiguity.  Using `[`...`]` as the corresponding literal form is complementary with our latest decisions, and gives us a clean place to work without problem.

This does introduce warts into the language.  For example, the following are both legal and (fortunately) mean the exact same thing:

```c#
int[] x = { 1, 2, 3 };
int[] x = [ 1, 2, 3 ];
```

However, given the breadth and consistency brought by the new literal syntax, we should consider recommending that people move to the new form.  IDE suggestions and fixes could help in that regard.

## Alternatives
[alternatives]: #alternatives

* What other designs have been considered? What is the impact of not doing this?

## Resolved questions
[resolved]: #resolved-questions

* Should the compiler use `stackalloc` for stack allocation when *inline arrays* are not available and the *iteration type* is a primitive type?

  Resolution: No. Managing a `stackalloc` buffer requires additional effort over an *inline array* to ensure the buffer is not allocated repeatedly when the collection expression is within a loop. The additional complexity in the compiler and in the generated code outweighs the benefit of stack allocation on older platforms.

* In what order should we evaluate literal elements compared with Length/Count property evaluation?  Should we evaluate all elements first, then all lengths?  Or should we evaluate an element, then its length, then the next element, and so on?

  Resolution: We evaluate all elements first, then everything else follows that.

* Can an *unknown length* literal create a collection type that needs a *known length*, like an array, span, or Construct(array/span) collection?  This would be harder to do efficiently, but it might be possible through clever use of pooled arrays and/or builders.

  Resolution: Yes, we allow creating a fixes-length collection from an *unknown length* literal.  The compiler is permitted to implement this in as efficient a manner as possible.

  The following text exists to record the original discussion of this topic.

  <details>

  Users could always make an *unknown length* literal into a *known length* one with code like this:

  ```c#
  ImmutableArray<int> x = [a, ..unknownLength.ToArray(), b];
  ```

  However, this is unfortunate due to the need to force allocations of temporary storage.  We could potentially be more efficient if we controlled how this was emitted.

  </details>

* Can a `collection_expression` be target-typed to an `IEnumerable<T>` or other collection interfaces?

  For example:

  ```c#
  void DoWork(IEnumerable<long> values) { ... }
  // Needs to produce `longs` not `ints` for this to work.
  DoWork([1, 2, 3]);
  ```

  Resolution: Yes, a literal can be target-typed to any interface type `I<T>` that `List<T>` implements.  For example, `IEnumerable<long>`. This is the same as target-typing to `List<long>` and then assigning that result to the specified interface type. The following text exists to record the original discussion of this topic.

  <details>

  The open question here is determining what underlying type to actually create.  One option is to look at the proposal for [`params IEnumerable<T>`](https://github.com/dotnet/csharplang/issues/179).  There, we would generate an array to pass the values along, similar to what happens with `params T[]`.

  </details>

* Can/should the compiler emit `Array.Empty<T>()` for `[]`?  Should we mandate that it does this, to avoid allocations whenever possible?

    Yes. The compiler should emit `Array.Empty<T>()` for any case where this is legal and the final result is non-mutable.  For example, targeting `T[]`, `IEnumerable<T>`, `IReadOnlyCollection<T>` or `IReadOnlyList<T>`.  It should not use `Array.Empty<T>` when the target is mutable (`ICollection<T>` or `IList<T>`).

* Should we expand on collection initializers to look for the very common `AddRange` method? It could be used by the underlying constructed type to perform adding of spread elements potentially more efficiently.  We might also want to look for things like `.CopyTo` as well.  There may be drawbacks here as those methods might end up causing excess allocations/dispatches versus directly enumerating in the translated code.

    Yes.  An implementation is allowed to utilize other methods to initialize a collection value, under the presumption that these methods have well-defined semantics, and that collection types should be "well behaved".  In practice though, an implementation should be cautious as benefits in one way (bulk copying) may come with negative consequences as well (for example, boxing a struct collection).

    An implementation should take advantage in the cases where there are no downsides.  For example, with an `.AddRange(ReadOnlySpan<T>)` method.

## Unresolved questions
[unresolved]: #unresolved-questions

* Should it be legal to create and immediately index into a collection literal?  Note: this requires an answer to the unresolved question below of whether collection literals have a *natural type*.
* Stack allocations for huge collections might blow the stack.  Should the compiler have a heuristic for placing this data on the heap?  Should the language be unspecified to allow for this flexibility?  We should follow the spec for [`params Span<T>`](https://github.com/dotnet/csharplang/issues/1757).
* Do we need to target-type `spread_element`?  Consider, for example:

  ```c#
  Span<int> span = [a, ..b ? [c] : [d, e], f];
  ```

  Note: this may commonly come up in the following form to allow conditional inclusion of some set of elements, or nothing if the condition is false:

  ```c#
  Span<int> span = [a, ..b ? [c, d, e] : [], f];
  ```

  In order to evaluate this full literal, we need to evaluate the element expressions within.  That means being able to evaluate `b ? [c] : [d, e]`.  However, absent a target type to evaluate this expression in the context of, and absent any sort of *natural type*, this would we would be unable to determine what to do with either `[c]` or `[d, e]` here.

  To resolve this, we could say that when evaluating a literal's `spread_element` expression, there was an implicit target type equivalent to the target type of the literal itself.  So, in the above, that would be rewritten as:

  ```c#
  int __e1 = a;
  Span<int> __s1 = b ? [c] : [d, e];
  int __e2 = f;

  Span<int> __result = stackalloc int[2 + __s1.Length];
  int __index = 0;

  __result[__index++] = a;
  foreach (int __t in __s1)
    __result[index++] = __t;
  __result[__index++] = f;

  Span<int> span = __result;
  ```

## Design meetings
[design-meetings]: #design-meetings

https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-11-01.md#collection-literals
https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-09.md#ambiguity-of--in-collection-expressions
https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-09-28.md#collection-literals

## Working group meetings
[working-group-meetings]: #working-group-meetings

https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2022-10-06.md
https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2022-10-14.md
https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2022-10-21.md
https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2023-04-05.md
https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2023-04-28.md
https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2023-05-26.md
https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2023-06-12.md
https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2023-06-26.md
https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2023-08-03.md
https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/CL-2023-08-10.md

## Upcoming agenda items

* Stack allocations for huge collections might blow the stack.  Should the compiler have a heuristic for placing this data on the heap?  Should the language be unspecified to allow for this flexibility?  We should follow what the spec/impl does for [`params Span<T>`](https://github.com/dotnet/csharplang/issues/1757). Options are:

  * Always stackalloc.  Teach people to be careful with Span.  This allows things like `Span<T> span = [1, 2, ..s]` to work, and be fine as long as `s` is small.  If this could blow the stack, users could always create an array instead, and then get a span around this.  This seems like the most in line with what people might want, but with extreme danger.
  * Only stackalloc when the literal has a *fixed* number of elements (i.e. no spread elements).  This then likely makes things always safe, with fixed stack usage, and the compiler (hopefully) able to reuse that fixed buffer.  However, it means things like `[1, 2, ..s]` would never be possible, even if the user knows it is completely safe at runtime.

* How does overload resolution work?  If an API has:

  ```C#
  public void M(T[] values);
  public void M(List<T> values);
  ```

  What happens with `M([1, 2, 3])`?  We likely need to define 'betterness' for these conversions.

* Should we expand on collection initializers to look for the very common `AddRange` method? It could be used by the underlying constructed type to perform adding of spread elements potentially more efficiently.  We might also want to look for things like `.CopyTo` as well.  There may be drawbacks here as those methods might end up causing excess allocations/dispatches versus directly enumerating in the translated code.
* Generic type inference should be updated to flow type information to/from collection literals.  For example:

  ```C#
  void M<T>(T[] values);
  M([1, 2, 3]);
  ```

  It seems natural that this should be something the inference algorithm can be made aware of.  Once this is supported for the 'base' constructible collection type cases (`T[]`, `I<T>`, `Span<T>` `new T()`), then it should also fall out of the `Collect(constructible_type)` case.  For example:

  ```C#
  void M<T>(ImmutableArray<T> values);
  M([1, 2, 3]);
  ```

  Here, `Immutable<T>` is constructible through an `init void Construct(T[] values)` method.  So the `T[] values` type would be used with inference against `[1, 2, 3]` leading to an inference of `int` for `T`.

* Cast/Index ambiguity.

  Today the following is an expression that is indexed into

  ```c#
  var v = (Expr)[1, 2, 3];
  ```

  But it would be nice to be able to do things like:

  ```c#
  var v = (ImmutableArray<int>)[1, 2, 3];
  ```

  Can/should we take a break here?

* Syntactic ambiguities with `?[`.  

  It might be worthwhile to change the rules for `nullable index access` to state that no space can occur between `?` and `[`.  That would be a breaking change (but likely minor as VS already forces those together if you type them with a space).  If we do this, then we can have `x?[y]` be parsed differently than `x ? [y]`.

  A similar thing occurs if we want to go with https://github.com/dotnet/csharplang/issues/2926.  In that world `x?.y` is ambiguous with `x ? .y`.  If we require the `?.` to abut, we can syntactically distinguish the two cases trivially.  
