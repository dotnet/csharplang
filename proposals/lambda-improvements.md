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

[HttpPost("/")] Todo PostTodo([FromBody] Todo todo) => todo);
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
Attributes may be added to lambda expressions.
```csharp
f = [MyAttribute] x => x;          // [MyAttribute]lambda
f = [MyAttribute] (int x) => x;    // [MyAttribute]lambda
f = [MyAttribute] static x => x;   // [MyAttribute]lambda
f = [return: MyAttribute] () => 1; // [return: MyAttribute]lambda
```

_Should parentheses be required for the parameter list if attributes are added to the entire expression? (Should `[MyAttribute] x => x` be disallowed? If so, what about `[MyAttribute] static x => x`?)_

Attributes may be added to lambda parameters that are declared with explicit types.
```csharp
f = ([MyAttribute] x) => x;      // syntax error
f = ([MyAttribute] int x) => x;  // [MyAttribute]x
```

Attributes are not supported for anonymous methods declared with `delegate { }` syntax.
```csharp
f = [MyAttribute] delegate { return 1; };         // syntax error
f = delegate ([MyAttribute] int x) { return x; }; // syntax error
```

Attributes on the lambda expression or lambda parameters will be emitted to metadata on the method that maps to the lambda.

In general, customers should not depend on how lambda expressions and local functions map from source to metadata. How lambdas and local functions are emitted can, and has, changed between compiler versions.

The changes proposed here are targeted at the `Delegate` driven scenario.
It should be valid to inspect the `MethodInfo` associated with a `Delegate` instance to determine the signature of the lambda expression or local function including any explicit attributes and additional metadata emitted by the compiler such as default parameters.
This allows teams such as ASP.NET to make available the same behaviors for lambdas and local functions as ordinary methods.

## Explicit return type
An explicit return type may be specified after the parameter list.
```csharp
f = () : T => default;              // () : T
f = x : short => 1;                 // <unknown> : short
f = (ref int x) : ref int => ref x; // ref int : ref int
f = static _ : void => { };         // <unknown> : void
```

Explicit return types are not supported for anonymous methods declared with `delegate { }` syntax.
```csharp
f = delegate : int { return 1; };         // syntax error
f = delegate (int x) : int { return x; }; // syntax error
```

## Natural delegate type
A lambda expression has a natural type if the parameters types are explicit and either the return type is explicit or there is a common type from the natural types of all `return` expressions in the body.

The natural type is a delegate type where the parameter types are the explicit lambda parameter types and the return type `R` is:
- if the lambda return type is explicit, that type is used;
- if the lambda has no return expressions, the return type is `void` or `System.Threading.Tasks.Task` if `async`;
- if the common type from the natural type of all `return` expressions in the body is the type `R0`, the return type is `R0` or `System.Threading.Tasks.Task<R0>` if `async`.

A method group has a natural type if the method group contains a single method.

A method group might refer to extension methods. Normally method group resolution searches for extension methods lazily, only iterating through successive namespace scopes until extension methods are found that match the target type. But to determine the natural type will require searching all namespace scopes. _To minimize unnecessary binding, perhaps natural type should be calculated only in cases where there is no target type - that is, only calculate the natural type in cases where it is needed._

The delegate type for the lambda or method group and parameter types `P1, ..., Pn` and return type `R` is:
- if any parameter or return value is not by value, or there are more than 16 parameters, or any of the parameter types or return are not valid type arguments (say, `(int* p) => { }`), then the delegate is a synthesized `internal` anonymous delegate type with signature that matches the lambda or method group, and with parameter names `arg1, ..., argn` or `arg` if a single parameter;
- if `R` is `void`, then the delegate type is `System.Action<P1, ..., Pn>`;
- otherwise the delegate type is `System.Func<P1, ..., Pn, R>`.

`modopt()` or `modreq()` in the method group signature are ignored in the corresponding delegate type.

If two lambda expressions or method groups in the same compilation require synthesized delegate types with the same parameter types and modifiers and the same return type and modifiers, the compiler will use the same synthesized delegate type.

Lambdas or method groups with natural types can be used as initializers in `var` declarations.

```csharp
var f1 = () => default;        // error: no natural type
var f2 = x => { };             // error: no natural type
var f3 = x => x;               // error: no natural type
var f4 = () => 1;              // System.Func<int>
var f5 = () : string => null;  // System.Func<string>
```

```csharp
static void F1() { }
static void F1<T>(this T t) { }
static void F2(this string s) { }

var f6 = F1;    // error: multiple methods
var f7 = "".F1; // System.Action
var f8 = F2;    // System.Action<string> 
```

The synthesized delegate types are implicitly co- and contra-variant.
```csharp
var fA = (IEnumerable<string> e, ref int i) => { }; // void DA$(IEnumerable<string>, ref int);
fA = (IEnumerable<object> e, ref int i) => { };     // ok

var fB = (IEnumerable<object> e, ref int i) => { }; // void DB$(IEnumerable<object>, ref int);
fB = (IEnumerable<string> e, ref int i) => { };     // error: parameter type mismatch
```

### Implicit conversion to `System.Delegate`
A consequence of inferring a natural type is that lambda expressions and method groups with natural type are implicitly convertible to `System.Delegate`.
```csharp
static void Invoke(Func<string> f) { }
static void Invoke(Delegate d) { }

static string GetString() => "";
static int GetInt() => 0;

Invoke(() => "");  // Invoke(Func<string>)
Invoke(() => 0);   // Invoke(Delegate) [new]

Invoke(GetString); // Invoke(Func<string>)
Invoke(GetInt);    // Invoke(Delegate) [new]
```

If a natural type cannot be inferred, there is no implicit conversion to `System.Delegate`.
```csharp
static void Invoke(Delegate d) { }

Invoke(Console.WriteLine); // error: cannot to 'Delegate'; multiple candidate methods
Invoke(x => x);            // error: cannot to 'Delegate'; no natural type for 'x'
```

To avoid a breaking change, overload resolution will be updated to prefer strongly-typed delegates and expressions over `System.Delegate`.
_The example below demonstrates the tie-breaking rule for lambdas. Is there an equivalent example for method groups?_
```csharp
static void Execute(Expression<Func<string>> e) { }
static void Execute(Delegate d) { }

static string GetString() => "";
static int GetInt() => 0;

Execute(() => "");  // Execute(Expression<Func<string>>) [tie-breaker]
Execute(() => 0);   // Execute(Delegate) [new]

Execute(GetString); // Execute(Delegate) [new]
Execute(GetInt);    // Execute(Delegate) [new]
```

## Syntax

```antlr
lambda_expression
  : attribute_list* modifier* lambda_parameters (':' type)? '=>' (block | body)
  ;

lambda_parameters
  : lambda_parameter
  | '(' (lambda_parameter (',' lambda_parameter)*)? ')'
  ;

lambda_parameter
  : identifier
  | (attribute_list* modifier* type)? identifier equals_value_clause?
  ;
```

_Does the `: type` return type syntax introduce ambiguities with `?:` that cannot be resolved easily?_

_Should we allow attributes on parameters without explicit types, such as `([MyAttribute] x) => { }`? (We don't allow modifiers on parameters without explicit types, such as `(ref x) => { }`.)_

## Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2021/LDM-2021-03-03.md
