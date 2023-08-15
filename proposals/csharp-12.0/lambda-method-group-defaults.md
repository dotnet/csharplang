# Optional and parameter array parameters for lambdas and method groups

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary

[summary]: #summary

To build on top of the lambda improvements introduced in C# 10 (see [relevant background](#relevant-background)), we propose adding support for default parameter values and `params` arrays in lambdas. This would enable users to implement the following lambdas:

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
addWithDefault(); // 3
addWithDefault(5); // 6

var counter = (params int[] xs) => xs.Length;
counter(); // 0
counter(1, 2, 3); // 3
```

Similarly, we will allow the same kind of behavior for method groups:
```csharp
var addWithDefault = AddWithDefaultMethod;
addWithDefault(); // 3
addWithDefault(5) // 6

var counter = CountMethod;
counter(); // 0
counter(1, 2); // 2

int AddWithDefaultMethod(int addTo = 2) {
  return addTo + 1;
}
int CountMethod(params int[] xs) {
  return xs.Length;
}
```

## Relevant background
[Lambda Improvements in C# 10](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/lambda-improvements.md)

[Method group conversion specification §10.8](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/conversions.md#108-method-group-conversions)

## Motivation

[motivation]: #motivation

App frameworks in the .NET ecosystem leverage lambdas heavily to allow users to quickly write business logic associated with an endpoint.

```csharp
var app = WebApplication.Create(args);

app.MapPost("/todos/{id}", (TodoService todoService, int id, string task) => {
  var todo = todoService.Create(id, task);
  return Results.Created(todo);
});
```

Lambdas don't currently support setting default values on parameters, so if a developer wanted to build an application that was resilient to scenarios where users didn't provide data, they're left to either use local functions or set the default values within the lambda body, as opposed to the more succinct proposed syntax.

```csharp
var app = WebApplication.Create(args);

app.MapPost("/todos/{id}", (TodoService todoService, int id, string task = "foo") => {
  var todo = todoService.Create(id, task);
  return Results.Created(todo);
});
```

The proposed syntax also has the benefit of reducing confusing differences between lambdas and local functions, making it easier to reason about constructs and "grow up" lambdas to functions without compromising features, particularly in other scenarios where lambdas are used in APIs where method groups can also be provided as references.
This is also the main motivation for supporting the `params` array which is not covered by the aforementioned use-case scenario.

For example: 
```csharp
var app = WebApplication.Create(args);

Result TodoHandler(TodoService todoService, int id, string task = "foo") {
  var todo = todoService.Create(id, task);
  return Results.Created(todo);
}

app.MapPost("/todos/{id}", TodoHandler);
```

## Current behavior
Currently, when a user implements a lambda with an optional or `params` parameter, the compiler raises an error.

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1; // error CS1065: Default values are not valid in this context.
var counter = (params int[] xs) => xs.Length; // error CS1670: params is not valid in this context
```

When a user attempts to use a method group where the underlying method has an optional or `params` parameter, this information isn't propagated, so the call to the method doesn't typecheck due to a mismatch in the number of expected arguments.
```cs
void M1(int i = 1) { }
var m1 = M1; // Infers Action<int>
m1(); // error CS7036: There is no argument given that corresponds to the required parameter 'obj' of 'Action<int>'

void M2(params int[] xs) { }
var m2 = M2; // Infers Action<int[]>
m2(); // error CS7036: There is no argument given that corresponds to the required parameter 'obj' of 'Action<int[]>'
```

## New behavior

Following this proposal, default values and `params` can be applied to lambda parameters with the following behavior:

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
addWithDefault(); // 3
addWithDefault(5); // 6

var counter = (params int[] xs) => xs.Length;
counter(); // 0
counter(1, 2, 3); // 3
```

Default values and `params` can be applied to method group parameters by specifically defining such method group:

```cs
int AddWithDefault(int addTo = 2) {
  return addTo + 1;
}

var add1 = AddWithDefault; 
add1(); // ok, default parameter value will be used

int Counter(params int[] xs) {
  return xs.Length;
}

var counter1 = Counter;
counter1(1, 2, 3); // ok, `params` will be used
```

## Breaking change

Currently, the inferred type of a method group is `Action` or `Func` so the following code compiles:
```csharp
void WriteInt(int i = 0) {
  Console.Write(i);
}

var writeInt = WriteInt; // Inferred as Action<int>
DoAction(writeInt, 3); // Ok, writeInt is an Action<int>

void DoAction(Action<int> a, int p) {
  a(p);
}

int Count(params int[] xs) {
  return xs.Length;
}
var counter = Count; // Inferred as Func<int[], int>
DoFunction(counter, 3); // Ok, counter is a Func<int[], int>

int DoFunction(Func<int[], int> f, int p) {
  return f(new[] { p });
}
```
Following this change, code of this nature would cease to compile in .NET SDK 7.0.200 or later.

```csharp
void WriteInt(int i = 0) {
  Console.Write(i);
}

var writeInt = WriteInt; // Inferred as anonymous delegate type
DoAction(writeInt, 3); // Error, cannot convert from anonymous delegate type to Action

void DoAction(Action<int> a, int p) {
  a(p);
}

int Count(params int[] xs) {
  return xs.Length;
}
var counter = Count; // Inferred as anonymous delegate type
DoFunction(counter, 3); // Error, cannot convert from anonymous delegate type to Func

int DoFunction(Func<int[], int> f, int p) {
  return f(new[] { p });
}
```
The impact of this breaking change needs to be considered. Fortunately, the use of `var` to infer the type of a method group has
only been supported since C# 10, so only code which has been written since then which explicitly relies on this behavior would break.

## Detailed design

[design]: #detailed-design

### Grammar and parser changes
This enhancement requires the following changes to the grammar for lambda expressions.
```diff
 lambda_expression
   : modifier* identifier '=>' (block | expression)
-  | attribute_list* modifier* type? lambda_parameters '=>' (block | expression)
+  | attribute_list* modifier* type? lambda_parameter_list '=>' (block | expression)
   ;

+lambda_parameter_list
+  : lambda_parameters (',' parameter_array)?
+  | parameter_array
+  ;

 lambda_parameter
   : identifier
-  | attribute_list* modifier* type? identifier
+  | attribute_list* modifier* type? identifier default_argument?
   ;
```

Note that this allows default parameter values and `params` arrays only for lambdas, not for anonymous methods declared with `delegate { }` syntax.

Same rules as for method parameters ([§14.6.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#1462-method-parameters)) apply for lambda parameters:
- A parameter with a `ref`, `out` or `this` modifier cannot have a *default_argument*.
- A *parameter_array* may occur after an optional parameter, but cannot have a default value – the omission of arguments for a *parameter_array* would instead result in the creation of an empty array.

No changes to the grammar are necessary for method groups since this proposal would only change their semantics.

The following addition (in bold) is required to anonymous function conversions ([§10.7](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/conversions.md#107-anonymous-function-conversions)):

> Specifically, an anonymous function `F` is compatible with a delegate type `D` provided:
>
> - [...]
> - If `F` has an explicitly typed parameter list, each parameter in `D` has the same type and modifiers as the corresponding parameter in `F` **ignoring `params` modifiers and default values**.

### Updates of prior proposals

The following addition (in bold) is required to the [function types](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/lambda-improvements.md#natural-function-type) specification in a prior proposal:

> The natural type of an anonymous function expression or method group is a *function_type*. A *function_type* represents a method signature: the parameter types, **default values, ref kinds, `params` modifiers**, and return type and ref kind. Anonymous function expressions or method groups with the same signature have the same *function_type*.

The following addition (in bold) is required to the [delegate types](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/lambda-improvements.md#delegate-types) specification in a prior proposal:

> The delegate type for the anonymous function or method group with parameter types `P1, ..., Pn` and return type `R` is:
>
> - if any parameter or return value is not by value, **or any parameter is optional or `params`**, or there are more than 16 parameters, or any of the parameter types or return are not valid type arguments (say, `(int* p) => { }`), then the delegate is a synthesized `internal` anonymous delegate type with signature that matches the anonymous function or method group, and with parameter names `arg1, ..., argn` or `arg` if a single parameter;
> [...]

### Binder changes

#### Synthesizing new delegate types
As with the behavior for delegates with `ref` or `out` parameters, delegate types are synthesized for lambdas or method groups defined with optional or `params` parameters.
Note that in the below examples, the notation `a'`, `b'`, etc. is used to represent these anonymous delegate types.

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
// internal delegate int a'(int arg = 2);
var printString = (string toPrint = "defaultString") => Console.WriteLine(toPrint);
// internal delegate void b'(string arg = "defaultString");
var counter = (params int[] xs) => xs.Length;
// internal delegate int c'(params int[] arg);
string PathJoin(string s1, string s2, string sep = "/") { return $"{s1}{sep}{s2}"; }
var joinFunc = pathJoin;
// internal delegate string d'(string arg1, string arg2, string arg3 = " ");
```

#### Conversion and unification behavior 
Anonymous delegates with optional parameters will be unified when the same parameter (based on position) has the same default value, regardless of parameter name.

```csharp
int E(int j = 13) {
  return 11;
}

int F(int k = 0) {
  return 3;
}

int G(int x = 13) {
  return 4;
}

var a = (int i = 13) => 1;
// internal delegate int b'(int arg = 13);
var b = (int i = 0) => 2;
// internal delegate int c'(int arg = 0);
var c = (int i = 13) => 3;
// internal delegate int b'(int arg = 13);
var d = (int c = 13) => 1;
// internal delegate int b'(int arg = 13);

var e = E;
// internal delegate int b'(int arg = 13);
var f = F;
// internal delegate int c'(int arg = 0);
var g = G;
// internal delegate int b'(int arg = 13);

a = b; // Not allowed
a = c; // Allowed
a = d; // Allowed
c = e; // Allowed
e = f; // Not Allowed
b = f; // Allowed
e = g; // Allowed

d = (int c = 10) => 2; // Error: default parameter value is different between new lambda
                       // and synthesized delegate b'. We won't do implicit conversion
```

Anonymous delegates with an array as the last parameter will be unified when the last parameter has the same `params` modifier and array type, regardless of parameter name.

```csharp
int C(int[] xs) {
  return xs.Length;
}

int D(params int[] xs) {
  return xs.Length;
}

var a = (int[] xs) => xs.Length;
// internal delegate int a'(int[] xs);
var b = (params int[] xs) => xs.Length;
// internal delegate int b'(params int[] xs);

var c = C;
// internal delegate int a'(int[] xs);
var d = D;
// internal delegate int b'(params int[] xs);

a = b; // Not allowed
a = c; // Allowed
b = c; // Not allowed
b = d; // Allowed

c = (params int[] xs) => xs.Length; // Error: different delegate types; no implicit conversion
d = (int[] xs) => xs.Length; // Error: different delegate types; no implicit conversion
```

Similarly, there is of course compatibility with named delegates that already support optional and `params` parameters.
When default values or `params` modifiers differ in a conversion, the source one will be unused if it's in a lambda expression, since the lambda cannot be called in any other way.
That might seem counter-intuitive to users, hence a warning will be emitted when the source default value or `params` modifier is present and different from the target one.
If the source is a method group, it can be called on its own, hence no warning will be emitted.

```csharp
delegate int DelegateNoDefault(int x);
delegate int DelegateWithDefault(int x = 1);
int MethodNoDefault(int x) => x;
int MethodWithDefault(int x = 2) => x;
DelegateNoDefault d1 = MethodWithDefault; // no warning: source is a method group
DelegateWithDefault d2 = MethodWithDefault; // no warning: source is a method group
DelegateWithDefault d3 = MethodNoDefault; // no warning: source is a method group
DelegateNoDefault d4 = (int x = 1) => x; // warning: source present, target missing
DelegateWithDefault d5 = (int x = 2) => x; // warning: source present, target different
DelegateWithDefault d6 = (int x) => x; // no warning: source missing, target present

delegate int DelegateNoParams(int[] xs);
delegate int DelegateWithParams(params int[] xs);
int MethodNoParams(int[] xs) => xs.Length;
int MethodWithParams(params int[] xs) => xs.Length;
DelegateNoParams d7 = MethodWithParams; // no warning: source is a method group
DelegateWithParams d8 = MethodNoParams; // no warning: source is a method group
DelegateNoParams d9 = (params int[] xs) => xs.Length; // warning: source present, target missing
DelegateWithParams d10 = (int[] xs) => xs.Length; // no warning: source missing, target present
```

### IL/runtime behavior

The default parameter values will be emitted to metadata. The IL for this feature will be very similar in nature to the IL emitted for lambdas with `ref` and `out` parameters. A class which
inherits from `System.Delegate` or similar will be generated, and the `Invoke` method will include `.param` directives to set default parameter values or `System.ParamArrayAttribute` &ndash; 
just as would be the case for a standard named delegate with optional or `params` parameters.

These delegate types can be inspected at runtime, as normal.
In code, users can introspect the `DefaultValue` in the `ParameterInfo` associated with the lambda or method group by using the associated `MethodInfo`.

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
void AddWithDefaultMethod(int addTo = 2) {
  return addTo + 1;
}

addWithDefault.Method.GetParameters()[0].DefaultValue; // 2

var add1 = AddWithDefaultMethod;
add1.Method.GetParameters()[0].DefaultValue; // 2
```

## Open questions

**Open question:** how does this interact with the existing `DefaultParameterValue` attribute?

**Proposed answer:** For parity, permit the `DefaultParameterValue` attribute on lambdas and ensure that the delegate generation behavior matches for default parameter values supported via the syntax.

```csharp
var a = (int i = 13) => 1;
// same as
var b = ([DefaultParameterValue(13)] int i) => 1;
b = a; // Allowed
```

**Open question:** First, note that this is outside the scope of the current proposal but it might be worth discussing in the future.
Do we want to support defaults with implicitly typed lambda parameters? I.e., 

```csharp
delegate void M1(int i = 3);
M1 m = (x = 3) => x + x; // Ok

delegate void M2(long i = 2);
M2 m = (x = 3.0) => ...; //Error: cannot convert implicitly from long to double
```
This inference leads to some tricky conversion issues which would require more discussion.

There are also parsing performance considerations here. For instance, today the term
`(x = ` could never be the start of a lambda expression. If this syntax was allowed for lambda defaults, then the parser
would need a larger lookahead (scanning all the way until a `=>` token) in order to determine whether a term is a lambda or not.

## Design meetings

- [LDM 2022-10-10](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-10-10.md#params-support-for-lambda-default-parameters): decision to add support for `params` in the same way as default parameter values.
