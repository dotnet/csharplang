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

## Natural delegate type
A lambda expression has a natural type if the parameters types are explicit and either the return type is explicit or there is a common type from the natural types of all `return` expressions in the body.

The natural type is a delegate type where the parameter types are the explicit lambda parameter types and the return type `R` is:
- if the lambda return type is explicit, that type is used;
- if the lambda has no return expressions, the return type is `void` or `System.Threading.Tasks.Task` if `async`;
- if the common type from the natural type of all `return` expressions in the body is the type `R0`, the return type is `R0` or `System.Threading.Tasks.Task<R0>` if `async`.

The natural type of an _individual method_ in a method group is a delegate type with the parameter types, ref kinds, and return type and ref kind, of that method. Parameter names and custom modifiers are ignored.

A method group may contain multiple methods across the containing type and all extension methods.
The natural type of the _method group_ is the common natural type for all methods in the method group.
If there is no common type, the method group has no natural type.

Normally method group resolution searches for extension methods lazily, only iterating through successive namespace scopes until extension methods are found that match the target type. But to determine the common natural type will require searching all namespace scopes.

The requirement of a common type across all methods in the method group means that adding an overload (including an extension method overload) for a method may be a breaking change if the original method was used as a method group with inferred type.

The delegate type for the lambda or method group and parameter types `P1, ..., Pn` and return type `R` is:
- if any parameter or return value is not by value, or there are more than 16 parameters, or any of the parameter types or return are not valid type arguments (say, `(int* p) => { }`), then the delegate is a synthesized `internal` anonymous delegate type with signature that matches the lambda or method group, and with parameter names `arg1, ..., argn` or `arg` if a single parameter;
- if `R` is `void`, then the delegate type is `System.Action<P1, ..., Pn>`;
- otherwise the delegate type is `System.Func<P1, ..., Pn, R>`.

The compiler may allow more signatures to bind to `System.Action<>` and `System.Func<>` types in the future (if `ref struct` types are allowed type arguments for instance).

_Should the compiler bind to a matching `System.Action<>` or `System.Func<>` type regardless of arity and synthesize a delegate type otherwise? If so, should the compiler warn if the expected delegate types are missing?_

`modopt()` or `modreq()` in the method group signature are ignored in the corresponding delegate type.

If two lambda expressions or method groups in the same compilation require synthesized delegate types with the same parameter types and modifiers and the same return type and modifiers, the compiler will use the same synthesized delegate type.

### `var`
Lambda expressions and method groups with natural types can be used as initializers in `var` declarations.
```csharp
var f1 = () => default;        // error: no natural type
var f2 = x => { };             // error: no natural type
var f3 = x => x;               // error: no natural type
var f4 = () => 1;              // System.Func<int>
var f5 = () : string => null;  // System.Func<string>

static void F1() { }
static void F1<T>(this T t) { }
static void F2(this string s) { }

var f6 = F1;    // error: multiple methods
var f7 = "".F1; // System.Action
var f8 = F2;    // System.Action<string> 
```

### Invoking lambdas
Lambda expressions with natural types can be invoked directly.
```csharp
int zero = ((int x) => x)(0); // ok
```

### Implicit conversions
A consequence of inferring a natural type is that lambda expressions and method groups with natural type are implicitly convertible to `System.MulticastDelegate` and to base classes and interfaces implemented by `System.MulticastDelegate` (including `System.Delegate`, `System.Object`, and `System.ICloneable`).

If a natural type cannot be inferred, there is no implicit conversion to `System.MulticastDelegate` or base classes or interfaces.
```csharp
Delegate d1 = 1.GetHashCode; // ok
Delegate d2 = 2.ToString;    // error: cannot convert to 'System.Delegate'; multiple 'ToString' methods
object o1 = (int x) => x;    // ok
object o2 = x => x;          // error: cannot convert to 'System.Object'; no natural type for 'x => x'
```

The compiler will also treat lambda expressions with natural type as implicitly convertible to `System.Linq.Expressions.Expression` as an expression tree. Base classes and interfaces implemented by `System.Linq.Expressions.Expression` are ignored when calculating conversions to expression trees.

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
- Inferring a delegate type for lambdas and method groups might result in breaking changes in overload resolution: see [issues/4674](https://github.com/dotnet/csharplang/issues/4674) for examples.

## Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2021/LDM-2021-03-03.md
- https://github.com/dotnet/csharplang/blob/master/meetings/2021/LDM-2021-04-12.md
