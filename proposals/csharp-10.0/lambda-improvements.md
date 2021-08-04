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

Attributes are not supported for anonymous methods declared with `delegate { }` syntax.
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

_Open issue: Should default values be supported for lambda expression parameters for completeness?_

### Well-known attributes
_Should `System.Diagnostics.ConditionalAttribute` be disallowed on lambda expressions since there are few scenarios where a lambda expression could be used conditionally?_
```csharp
([Conditional("DEBUG")] static (x, y) => Assert(x == y))(a, b); // ok?
```

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

The parser should allow ref return types in assignment without parentheses.
```csharp
Delegate d1 = (ref int () => x); // ok
Delegate d2 = ref int () => x;   // ok
```

## Natural (function) type
A lambda expression has a natural type if the parameters types are explicit and the return type is either explicit or can be inferred (see [inferred return type](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#inferred-return-type)).

A method group has a natural type if all candidate methods in the method group have a common signature. (If the method group may include extension methods, the candidates include the containing type and all extension method scopes.)

The natural type of a lambda expression or method group is a _function_type_.
A _function_type_ represents a method signature: the parameter types and ref kinds, and return type and ref kind.
Lambda expressions or method groups with the same signature have the same _function_type_.

A _function_type_ exists at compile time only: _function_types_ do not appear in source or metadata.

_Open issue: Should the function_type be available from the compiler API?_

### Conversions
From a _function_type_ `F` there are implicit _function_type_ conversions:
- To a _function_type_ `G` if the parameters and return types of `F` are variance-convertible to the parameters and return type of `G`
- To `System.MulticastDelegate` or base classes or interfaces of `System.MulticastDelegate`
- To `System.Linq.Expressions.Expression` or `System.Linq.Expressions.LambdaExpression`

Lambda expressions and method groups already have _conversions from expression_ to delegate types and expression tree types (see [anonymous function conversions](https://github.com/dotnet/csharplang/blob/main/spec/conversions.md#anonymous-function-conversions) and [method group conversions](https://github.com/dotnet/csharplang/blob/main/spec/conversions.md#method-group-conversions)). Those conversions are sufficient for converting to strongly-typed delegate types and expression tree types. The _function_type_ conversions above add _conversions from type_ to the base types only: `System.MulticastDelegate`, `System.Linq.Expressions.Expression`, etc.

There are no conversions to a _function_type_ from a type other than a _function_type_.
There are no explicit conversions for _function_types_ since _function_types_ cannot be referenced in source.

A conversion to `System.MulticastDelegate` or base type or interface realizes the lambda or method group as an instance of an appropriate delegate type.
A conversion to `System.Linq.Expressions.Expression<TDelegate>` or base type realizes the lambda expression as an expression tree with an appropriate delegate type.

### Type inference
The existing rules for type inference are mostly unchanged (see [type inference](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#type-inference)). There are however a ***couple of changes*** below to specific phases of type inference.

#### First phase
The [first phase](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#the-first-phase) allows an anonymous function to bind to `Ti` even if `Ti` is not a delegate or expression tree type (perhaps a type parameter constrained to `System.Delegate` for instance).

> For each of the method arguments `Ei`:
> 
> *   If `Ei` is an anonymous function ***and `Ti` is a delegate type or expression tree type***, an *explicit parameter type inference* is made from `Ei` to `Ti`
> *   Otherwise, if `Ei` has a type `U` and `xi` is a value parameter then a *lower-bound inference* is made *from* `U` *to* `Ti`.
> *   Otherwise, if `Ei` has a type `U` and `xi` is a `ref` or `out` parameter then an *exact inference* is made *from* `U` *to* `Ti`.
> *   Otherwise, no inference is made for this argument.

#### Fixing
[Fixing](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#fixing) ensures other conversions are preferred over _function_type_ conversions. (Lambda expressions and method group expressions only contribute to lower bounds so handling of _function_types_ is needed for lower bounds only.)

> An *unfixed* type variable `Xi` with a set of bounds is *fixed* as follows:
> 
> *  The set of *candidate types* `Uj` starts out as the set of all types in the set of bounds for `Xi` ***where function types are ignored in lower bounds if there any types that are not function types***.
> *  We then examine each bound for `Xi` in turn: For each exact bound `U` of `Xi` all types `Uj` which are not identical to `U` are removed from the candidate set. For each lower bound `U` of `Xi` all types `Uj` to which there is *not* an implicit conversion from `U` are removed from the candidate set. For each upper bound `U` of `Xi` all types `Uj` from which there is *not* an implicit conversion to `U` are removed from the candidate set.
> *  If among the remaining candidate types `Uj` there is a unique type `V` from which there is an implicit conversion to all the other candidate types, then `Xi` is fixed to `V`.
> *  Otherwise, type inference fails.

### Best common type
Best common type is defined in terms of type inference (see [finding the best common type](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#finding-the-best-common-type-of-a-set-of-expressions)) so the changes above apply to best common type as well.
```csharp
var fs = new[] { (string s) => s.Length; (string s) => int.Parse(s) } // Func<string, int>[]
```

### `var`
Lambda expressions and method groups with natural types can be used as initializers in `var` declarations.
```csharp
var f1 = () => default;     // error: cannot infer type
var f2 = x => { };          // error: cannot infer type
var f3 = x => x;            // error: cannot infer type
var f4 = () => 1;           // System.Func<int>
var f5 = string () => null; // System.Func<string>

static void F1() { }
static void F1<T>(this T t) { }
static void F2(this string s) { }

var f6 = F1;    // error: multiple methods
var f7 = "".F1; // System.Action
var f8 = F2;    // System.Action<string> 
```

### Delegate types
The delegate type for the lambda or method group and parameter types `P1, ..., Pn` and return type `R` is:
- if any parameter or return value is not by value, or there are more than 16 parameters, or any of the parameter types or return are not valid type arguments (say, `(int* p) => { }`), then the delegate is a synthesized `internal` anonymous delegate type with signature that matches the lambda or method group, and with parameter names `arg1, ..., argn` or `arg` if a single parameter;
- if `R` is `void`, then the delegate type is `System.Action<P1, ..., Pn>`;
- otherwise the delegate type is `System.Func<P1, ..., Pn, R>`.

The compiler may allow more signatures to bind to `System.Action<>` and `System.Func<>` types in the future (if `ref struct` types are allowed type arguments for instance).

_Open issue: Should the compiler bind to a matching `System.Action<>` or `System.Func<>` type regardless of arity and synthesize a delegate type otherwise? If so, should the compiler warn if the expected delegate types are missing?_

`modopt()` or `modreq()` in the method group signature are ignored in the corresponding delegate type.

If two lambda expressions or method groups in the same compilation require synthesized delegate types with the same parameter types and modifiers and the same return type and modifiers, the compiler will use the same synthesized delegate type.

### Overload resolution
Overload resolution already prefers binding to a strongly-typed delegate over `System.Delegate`, and prefers binding a lambda expression to a strongly-typed `System.Linq.Expressions.Expression<TDelegate>` over the corresponding strongly-typed delegate `TDelegate`.

Overload resolution will be updated to prefer binding a lambda expression to `System.Linq.Expressions.Expression` over `System.Delegate`. A strongly-typed delegate will still be preferred over the weakly-typed `System.Linq.Expressions.Expression` however.

```csharp
static void Invoke(Func<string> f) { }
static void Invoke(Delegate d) { }
static void Invoke(Expression e) { }

static string GetString() => "";
static int GetInt() => 0;

Invoke(GetString); // Invoke(Func<string>) [unchanged]
Invoke(GetInt);    // Invoke(Delegate) [new]

Invoke(() => "");  // Invoke(Func<string>) [unchanged]
Invoke(() => 0);   // Invoke(Expression) [new]
```

_Inferring a delegate type for lambdas and method groups will result in some breaking changes in overload resolution: see [issues/4674](https://github.com/dotnet/csharplang/issues/4674)._

## Direct invocation
Lambda expressions may be invoked directly.
The compiler will generate a call to the underlying method without generating a delegate instance or synthesizing a delegate type.
Directly invoked lambda expressions do not require explicit parameter types.
```csharp
int zero = ((int x) => x)(0); // ok
int one = (x => x)(1);        // ok
```

_Direct invocation will be addressed separately since the feature does not depend on other changes in this proposal: see [issues/4748](https://github.com/dotnet/csharplang/issues/4748)._

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

## Design meetings

- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-03.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-12.md#lambda-improvements
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-21.md#inferred-types-for-lambdas-and-method-groups
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-05-10.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-06-02.md#lambda-return-type-parsing
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-06-21.md#open-questions-for-lambda-return-types
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-07-12.md
