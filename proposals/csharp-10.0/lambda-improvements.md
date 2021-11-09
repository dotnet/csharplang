# Lambda improvements

## Summary
Proposed changes:
1. Allow lambdas with attributes
2. Allow lambdas with explicit return type
3. Infer a natural delegate type for lambdas and method groups

## Motivation
Support for attributes on lambdas would provide parity with methods and local functions.

Support for explicit return types would provide symmetry with lambda parameters where explicit types can be specified.
Allowing explicit return types would also provide control over compiler performance in nested lambdas where overload resolution must bind the lambda body currently to determine the signature.

A natural type for lambda expressions and method groups will allow more scenarios where lambdas and method groups may be used without an explicit delegate type, including as initializers in `var` declarations.

Requiring explicit delegate types for lambdas and method groups has been a friction point for customers, and has become an impediment to progress in ASP.NET with recent work on [MapAction](https://github.com/dotnet/aspnetcore/pull/29878).

[ASP.NET MapAction](https://github.com/dotnet/aspnetcore/pull/29878) without proposed changes (`MapAction()` takes a `System.Delegate` argument):
```csharp
[HttpGet("/")] Todo GetTodo() => new(Id: 0, Name: "Name");
app.MapAction((Func<Todo>)GetTodo);

[HttpPost("/")] Todo PostTodo([FromBody] Todo todo) => todo;
app.MapAction((Func<Todo, Todo>)PostTodo);
```

[ASP.NET MapAction](https://github.com/dotnet/aspnetcore/pull/29878) with natural types for method groups:
```csharp
[HttpGet("/")] Todo GetTodo() => new(Id: 0, Name: "Name");
app.MapAction(GetTodo);

[HttpPost("/")] Todo PostTodo([FromBody] Todo todo) => todo);
app.MapAction(PostTodo);
```

[ASP.NET MapAction](https://github.com/dotnet/aspnetcore/pull/29878) with attributes and natural types for lambda expressions:
```csharp
app.MapAction([HttpGet("/")] () => new Todo(Id: 0, Name: "Name"));
app.MapAction([HttpPost("/")] ([FromBody] Todo todo) => todo);
```

## Attributes
Attributes may be added to lambda expressions and lambda parameters.
To avoid ambiguity between method attributes and parameter attributes, a lambda expression with attributes must use a parenthesized parameter list.
Parameter types are not required.
```csharp
f = [A] () => { };        // [A] lambda
f = [return:A] x => x;    // syntax error at '=>'
f = [return:A] (x) => x;  // [A] lambda
f = [A] static x => x;    // syntax error at '=>'

f = ([A] x) => x;         // [A] x
f = ([A] ref int x) => x; // [A] x
```

Multiple attributes may be specified, either comma-separated within the same attribute list or as separate attribute lists.
```csharp
var f = [A1, A2][A3] () => { };    // ok
var g = ([A1][A2, A3] int x) => x; // ok
``` 

Attributes are not supported for _anonymous methods_ declared with `delegate { }` syntax.
```csharp
f = [A] delegate { return 1; };         // syntax error at 'delegate'
f = delegate ([A] int x) { return x; }; // syntax error at '['
```

The parser will look ahead to differentiate a collection initializer with an element assignment from a collection initializer with a lambda expression.
```csharp
var y = new C { [A] = x };    // ok: y[A] = x
var z = new C { [A] x => x }; // ok: z[0] = [A] x => x
```

The parser will treat `?[` as the start of a conditional element access.
```csharp
x = b ? [A];               // ok
y = b ? [A] () => { } : z; // syntax error at '('
```

Attributes on the lambda expression or lambda parameters will be emitted to metadata on the method that maps to the lambda.

In general, customers should not depend on how lambda expressions and local functions map from source to metadata. How lambdas and local functions are emitted can, and has, changed between compiler versions.

The changes proposed here are targeted at the `Delegate` driven scenario.
It should be valid to inspect the `MethodInfo` associated with a `Delegate` instance to determine the signature of the lambda expression or local function including any explicit attributes and additional metadata emitted by the compiler such as default parameters.
This allows teams such as ASP.NET to make available the same behaviors for lambdas and local functions as ordinary methods.

## Explicit return type
An explicit return type may be specified before the parenthesized parameter list.
```csharp
f = T () => default;                    // ok
f = short x => 1;                       // syntax error at '=>'
f = ref int (ref int x) => ref x;       // ok
f = static void (_) => { };             // ok
f = async async (async async) => async; // ok?
```

The parser will look ahead to differentiate a method call `T()` from a lambda expression `T () => e`.

Explicit return types are not supported for anonymous methods declared with `delegate { }` syntax.
```csharp
f = delegate int { return 1; };         // syntax error
f = delegate int (int x) { return x; }; // syntax error
```

Method type inference should make an exact inference from an explicit lambda return type.
```csharp
static void F<T>(Func<T, T> f) { ... }
F(int (i) => i); // Func<int, int>
```

Variance conversions are not allowed from lambda return type to delegate return type (matching similar behavior for parameter types).
```csharp
Func<object> f1 = string () => null; // error
Func<object?> f2 = object () => x;   // warning
```

The parser allows lambda expressions with `ref` return types within expressions without additional parentheses.
```csharp
d = ref int () => x; // d = (ref int () => x)
F(ref int () => x);  // F((ref int () => x))
```

`var` cannot be used as an explicit return type for lambda expressions.
```csharp
class var { }

d = var (var v) => v;              // error: contextual keyword 'var' cannot be used as explicit lambda return type
d = @var (var v) => v;             // ok
d = ref var (ref var v) => ref v;  // error: contextual keyword 'var' cannot be used as explicit lambda return type
d = ref @var (ref var v) => ref v; // ok
```

## Natural (function) type
An [_anonymous function_ expression](../../spec/expressions.md#anonymous-function-expressions) (a _lambda expression_ or an _anonymous method_) has a natural type if the parameters types are explicit and the return type is either explicit or can be inferred (see [inferred return type](../../spec/expressions.md#inferred-return-type)).

A _method group_ has a natural type if all candidate methods in the method group have a common signature. (If the method group may include extension methods, the candidates include the containing type and all extension method scopes.)

The natural type of an anonymous function expression or method group is a _function_type_.
A _function_type_ represents a method signature: the parameter types and ref kinds, and return type and ref kind.
Anonymous function expressions or method groups with the same signature have the same _function_type_.

_Function_types_ are used in a few specific contexts only:
- implicit and explicit conversions
- [method type inference](../../spec/expressions.md#type-inference) and [best common type](../../spec/expressions.md#finding-the-best-common-type-of-a-set-of-expressions)
- `var` initializers

A _function_type_ exists at compile time only: _function_types_ do not appear in source or metadata.

### Conversions
From a _function_type_ `F` there are implicit _function_type_ conversions:
- To a _function_type_ `G` if the parameters and return types of `F` are variance-convertible to the parameters and return type of `G`
- To `System.MulticastDelegate` or base classes or interfaces of `System.MulticastDelegate`
- To `System.Linq.Expressions.Expression` or `System.Linq.Expressions.LambdaExpression`

Anonymous function expressions and method groups already have _conversions from expression_ to delegate types and expression tree types (see [anonymous function conversions](../../spec/conversions.md#anonymous-function-conversions) and [method group conversions](../../spec/conversions.md#method-group-conversions)). Those conversions are sufficient for converting to strongly-typed delegate types and expression tree types. The _function_type_ conversions above add _conversions from type_ to the base types only: `System.MulticastDelegate`, `System.Linq.Expressions.Expression`, etc.

There are no conversions to a _function_type_ from a type other than a _function_type_.
There are no explicit conversions for _function_types_ since _function_types_ cannot be referenced in source.

A conversion to `System.MulticastDelegate` or base type or interface realizes the anonymous function or method group as an instance of an appropriate delegate type.
A conversion to `System.Linq.Expressions.Expression<TDelegate>` or base type realizes the lambda expression as an expression tree with an appropriate delegate type.

```csharp
Delegate d = delegate (object obj) { }; // Action<object>
Expression e = () => "";                // Expression<Func<string>>
object o = "".Clone;                    // Func<object>
```

_Function_type_ conversions are not implicit or explicit [standard conversions](../../spec/conversions.md#standard-conversions) and are not considered when determining whether a user-defined conversion operator is applicable to an anonymous function or method group.
From [evaluation of user defined conversions](../../spec/conversions.md#evaluation-of-user-defined-conversions):

> For a conversion operator to be applicable, it must be possible to perform a standard conversion ([Standard conversions](../../spec/conversions.md#standard-conversions)) from the source type to the operand type of the operator, and it must be possible to perform a standard conversion from the result type of the operator to the target type.

```csharp
class C
{
    public static implicit operator C(Delegate d) { ... }
}

C c;
c = () => 1;      // error: cannot convert lambda expression to type 'C'
c = (C)(() => 2); // error: cannot convert lambda expression to type 'C'
```

A warning is reported for an implicit conversion of a method group to `object`, since the conversion is valid but perhaps unintentional.
```csharp
Random r = new Random();
object obj;
obj = r.NextDouble;         // warning: Converting method group to 'object'. Did you intend to invoke the method?
obj = (object)r.NextDouble; // ok
```

### Type inference
The existing rules for type inference are mostly unchanged (see [type inference](../../spec/expressions.md#type-inference)). There are however a **couple of changes** below to specific phases of type inference.

#### First phase
The [first phase](../../spec/expressions.md#the-first-phase) allows an anonymous function to bind to `Ti` even if `Ti` is not a delegate or expression tree type (perhaps a type parameter constrained to `System.Delegate` for instance).

> For each of the method arguments `Ei`:
> 
> *   If `Ei` is an anonymous function **and `Ti` is a delegate type or expression tree type**, an *explicit parameter type inference* is made from `Ei` to `Ti` **and an *explicit return type inference* is made from `Ei` to `Ti`.**
> *   Otherwise, if `Ei` has a type `U` and `xi` is a value parameter then a *lower-bound inference* is made *from* `U` *to* `Ti`.
> *   Otherwise, if `Ei` has a type `U` and `xi` is a `ref` or `out` parameter then an *exact inference* is made *from* `U` *to* `Ti`.
> *   Otherwise, no inference is made for this argument.

> #### **Explicit return type inference**
> 
> **An *explicit return type inference* is made *from* an expression `E` *to* a type `T` in the following way:**
> 
> *  **If `E` is an anonymous function with explicit return type `Ur` and `T` is a delegate type or expression tree type with return type `Vr` then an *exact inference* ([Exact inferences](../../spec/expressions.md#exact-inferences)) is made *from* `Ur` *to* `Vr`.**

#### Fixing
[Fixing](../../spec/expressions.md#fixing) ensures other conversions are preferred over _function_type_ conversions. (Lambda expressions and method group expressions only contribute to lower bounds so handling of _function_types_ is needed for lower bounds only.)

> An *unfixed* type variable `Xi` with a set of bounds is *fixed* as follows:
> 
> *  The set of *candidate types* `Uj` starts out as the set of all types in the set of bounds for `Xi` **where function types are ignored in lower bounds if there any types that are not function types**.
> *  We then examine each bound for `Xi` in turn: For each exact bound `U` of `Xi` all types `Uj` which are not identical to `U` are removed from the candidate set. For each lower bound `U` of `Xi` all types `Uj` to which there is *not* an implicit conversion from `U` are removed from the candidate set. For each upper bound `U` of `Xi` all types `Uj` from which there is *not* an implicit conversion to `U` are removed from the candidate set.
> *  If among the remaining candidate types `Uj` there is a unique type `V` from which there is an implicit conversion to all the other candidate types, then `Xi` is fixed to `V`.
> *  Otherwise, type inference fails.

### Best common type
[Best common type](../../spec/expressions.md#finding-the-best-common-type-of-a-set-of-expressions) is defined in terms of type inference so the type inference changes above apply to best common type as well.
```csharp
var fs = new[] { (string s) => s.Length; (string s) => int.Parse(s) } // Func<string, int>[]
```

### `var`
Anonymous functions and method groups with function types can be used as initializers in `var` declarations.
```csharp
var f1 = () => default;           // error: cannot infer type
var f2 = x => x;                  // error: cannot infer type
var f3 = () => 1;                 // System.Func<int>
var f4 = string () => null;       // System.Func<string>
var f5 = delegate (object o) { }; // System.Action<object>

static void F1() { }
static void F1<T>(this T t) { }
static void F2(this string s) { }

var f6 = F1;    // error: multiple methods
var f7 = "".F1; // System.Action
var f8 = F2;    // System.Action<string> 
```

Function types are not used in assignments to discards.
```csharp
d = () => 0; // ok
_ = () => 1; // error
```

### Delegate types
The delegate type for the anonymous function or method group with parameter types `P1, ..., Pn` and return type `R` is:
- if any parameter or return value is not by value, or there are more than 16 parameters, or any of the parameter types or return are not valid type arguments (say, `(int* p) => { }`), then the delegate is a synthesized `internal` anonymous delegate type with signature that matches the anonymous function or method group, and with parameter names `arg1, ..., argn` or `arg` if a single parameter;
- if `R` is `void`, then the delegate type is `System.Action<P1, ..., Pn>`;
- otherwise the delegate type is `System.Func<P1, ..., Pn, R>`.

The compiler may allow more signatures to bind to `System.Action<>` and `System.Func<>` types in the future (if `ref struct` types are allowed type arguments for instance).

`modopt()` or `modreq()` in the method group signature are ignored in the corresponding delegate type.

If two anonymous functions or method groups in the same compilation require synthesized delegate types with the same parameter types and modifiers and the same return type and modifiers, the compiler will use the same synthesized delegate type.

### Overload resolution

[Better function member](../../spec/expressions.md#better-function-member) is updated to prefer members where none of the conversions and none of the type arguments involved inferred types from lambda expressions or method groups.

> #### Better function member
> ...
> Given an argument list `A` with a set of argument expressions `{E1, E2, ..., En}` and two applicable function members `Mp` and `Mq` with parameter types `{P1, P2, ..., Pn}` and `{Q1, Q2, ..., Qn}`, `Mp` is defined to be a ***better function member*** than `Mq` if
>
> 1. **for each argument, the implicit conversion from `Ex` to `Px` is not a _function_type_conversion_, and**
>    *  **`Mp` is a non-generic method or `Mp` is a generic method with type parameters `{X1, X2, ..., Xp}` and for each type parameter `Xi` the type argument is inferred from an expression or from a type other than a _function_type_, and**
>    *  **for at least one argument, the implicit conversion from `Ex` to `Qx` is a _function_type_conversion_, or `Mq` is a generic method with type parameters `{Y1, Y2, ..., Yq}` and for at least one type parameter `Yi` the type argument is inferred from a _function_type_, or**
> 2. for each argument, the implicit conversion from `Ex` to `Qx` is not better than the implicit conversion from `Ex` to `Px`, and for at least one argument, the conversion from `Ex` to `Px` is better than the conversion from `Ex` to `Qx`.

[Better conversion from expression](../../spec/expressions.md#better-conversion-from-expression) is updated to prefer conversions that did not involve inferred types from lambda expressions or method groups.

> #### Better conversion from expression
> 
> Given an implicit conversion `C1` that converts from an expression `E` to a type `T1`, and an implicit conversion `C2` that converts from an expression `E` to a type `T2`, `C1` is a ***better conversion*** than `C2` if:
> 1. **`C1` is not a _function_type_conversion_ and `C2` is a _function_type_conversion_, or**
> 2. `E` is a non-constant _interpolated\_string\_expression_, `C1` is an _implicit\_string\_handler\_conversion_, `T1` is an _applicable\_interpolated\_string\_handler\_type_, and `C2` is not an _implicit\_string\_handler\_conversion_, or
> 3. `E` does not exactly match `T2` and at least one of the following holds:
>     * `E` exactly matches `T1` ([Exactly matching Expression](../../spec/expressions.md#exactly-matching-expression))
>     * `T1` is a better conversion target than `T2` ([Better conversion target](../../spec/expressions.md#better-conversion-target))

## Syntax

```antlr
lambda_expression
  : modifier* identifier '=>' (block | expression)
  | attribute_list* modifier* type? lambda_parameters '=>' (block | expression)
  ;

lambda_parameters
  : lambda_parameter
  | '(' (lambda_parameter (',' lambda_parameter)*)? ')'
  ;

lambda_parameter
  : identifier
  | attribute_list* modifier* type? identifier equals_value_clause?
  ;
```

## Open issues

Should default values be supported for lambda expression parameters for completeness?

Should `System.Diagnostics.ConditionalAttribute` be disallowed on lambda expressions since there are few scenarios where a lambda expression could be used conditionally?
```csharp
([Conditional("DEBUG")] static (x, y) => Assert(x == y))(a, b); // ok?
```

Should the _function_type_ be available from the compiler API, in addition to the resulting delegate type?

Currently, the inferred delegate type uses `System.Action<>` or `System.Func<>` when parameter and return types are valid type arguments _and_ there are no more than 16 parameters, and if the expected `Action<>` or `Func<>` type is missing, an error is reported. Instead, should the compiler use `System.Action<>` or `System.Func<>` regardless of arity? And if the expected type is missing, synthesize a delegate type otherwise?
