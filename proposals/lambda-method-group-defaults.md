# Default Parameters for Lambdas and Method Groups

## Summary

[summary]: #summary

To build on top of the lambda improvements introduced in C#10 (see [relevant background](#relevant-background)), we propose adding support for default parameter values in lambdas. This would enable users to implement the following lambdas:

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
addWithDefault(); // 3
addWithDefault(5); // 6
```

Similarly, we will allow the same kind of behavior for method groups:
```csharp
var addWithDefault = addWithDefaultMethod;
addWithDefault(); // 3
addWithDefault(5) // 6

int addWithDefaultMethod(int addTo = 2) {
  return addTo + 1;
}
```

## Relevant Background
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

For example: 
```csharp
var app = WebApplication.Create(args);

Result todoHandler(TodoService todoService, int id, string task = "foo") {
  var todo = todoService.Create(id, task);
  return Results.Created(todo);
}

app.MapPost("/todos/{id}", todoHandler);
```

## Current Behavior
Currently, when a user implements a lambda with a default value, the compiler raises an error stating that default values are not allowed. 

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1; // Error: default values not allowed in this context
```

When a user attempts to use a method group where the underlying method has a default parameter, the
default param isn't propagated, so the call to the method doesn't typecheck due to a mismatch in the number of expected parameters.
```cs
void M(int i = 1) {}

var m = M; // Infers Action<int>
m(); // Error: no value provided for arg0
```

## New Behavior 

Following this proposal, default values can be applied to lambda parameters with the following behavior:

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
addWithDefault(); // 3
addWithDefault(5); // 6
```

Default values can be applied to method group parameters by specifically defining a method group that
has a default parameter:

```cs
void addWithDefault(int addTo = 2) {
  return addTo + 1;
}

var add1 = addWithDefault; 
add1(); // ok, default parameter will be used.
```

## Breaking Change

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
```
Following this change, code of this nature would cease to compile.

```csharp
void WriteInt(int i = 0) {
  Console.Write(i);
}

var writeInt = WriteInt; // Inferred as anonymous delegate type
DoAction(writeInt, 3); // Error, cannot convert from anonymous delegate type to Action

void DoAction(Action<int> a, int p) {
  a(p);
}
```
The impact of this breaking change needs to be considered. Fortunately, the use of `var` to infer the type of a method group has
only been supported since C# 10, so only code which has been written since then which explicity relies on this behavior would break.

## Detailed design

[design]: #detailed-design

### Grammar and Parser Changes
This enhancement requires the following changes to the grammar for lambda expressions.
```diff
explicit_anonymous_function_parameter
-    : anonymous_function_parameter_modifier? type identifier
+    : anonymous_function_parameter_modifier? type identifier default_argument?
    ;
```
No changes to the grammar are necessary for method groups since this proposal would only change their semantics.

### Binder Changes

#### Synthesizing New Delegate Types
As with the behavior for delegates with `ref` or `out` parameters, a new natural type is generated for each lambda or method group defined with any default parameter values.
Note that in the below examples, the notation `a'`, `b'`, etc. is used to represent these anonymous delegate types.

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
// internal delegate int b'(int arg0 = 2);
var printString = (string toPrint = "defaultString") => Console.WriteLine(toPrint);
// internal delegate void c'(string arg0 = "defaultString");
string pathJoin(string s1, string s2, string sep = "/") { return $"{s1}{sep}{s2}"; }
var joinFunc = pathJoin;
// internal delegate string d'(string arg0, string arg1, string arg3 = " ");
```

#### Conversion and Unification Behavior 
The delegates described previously will be unified when the same parameter (based on position) has the same default value, regardless of parameter name.

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
// internal delegate int b'(int arg0 = 13);
var b = (int i = 0) => 2;
// internal delegate int c'(int arg0 = 0);
var c = (int i = 13) => 3;
// internal delegate int b'(int arg0 = 13);
var d = (int c = 13) => 1;
// internal delegate int b'(int arg0 = 13);

var e = E;
// internal delegate int b'(int arg0 = 13);
var f = F;
// internal delegate int c'(int arg0 = 0);
var g = G;
// internal delegate int b'(int arg0 = 13);

a = b; // Not allowed
a = c; // Allowed
a = d; // Allowed
c = e; // Allowed
e = f; // Not Allowed
b = f; // Allowed
e = g; // Allowed

d = (int c = 10) => 2; // Error: default parameter is different between new lambda
                       // and synthesized delegate b'. We won't do implicit conversion
```

Similarly, there is of course compatibility with named delegates that have default parameters as well: 
```csharp
int D(int a = 1) {
  return a;
}

delegate int Del(int a = 1);

Del del = (int x = 100) => x; // Error: Default parameter does not match, so no conversion can be performed
                              
Del del1 = (int x = 1) => x; // Allowed, because default parameter value in lambda matches default parameter value in delegate

Del del2 = D; // This behavior does not change and compiles as before as per the method group conversion rules 

var d = D;
// d is inferred as internal delegate int a'(int arg0 = 1);

Del del3 = d; // Not allowed. Cannot convert internal delegate type to Del.
              // Note that there is no change here from previous behavior, when d would be inferred
              // to be Action<int> since Action<int> also cannot be converted to a named delegate type.
```

Since lambdas and method groups with default parameter values are typed as anonymous delegates, the
method group conversion rules as described in [§10.8](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#108-method-group-conversions)
apply. We can first consider the following example: 

```csharp
int M(int x = 20) {
  return x;
}

var d = (int x = 10) => x;
// internal delegate int b'(int arg0 = 10);

d = M; // Allowed with warning. The existing method group rules apply here, and the signature of M
       // is allowed to be converted to internal delegate int b'(int arg0 = 10); However, because this behavior could be confusing, it is worth alerting the user.
d();   // This call will use default value x = 10 from original lambda
```
The above code has an implicit conversion from a method group to a delegate. 
However, the anonymous delegate type that the method group is converted to has a default parameter which differs from the underlying method. 
Note that the default value for the underlying delegate type will be used here, which may seem counter-intuitive to users. Because of this, we will emit a warning.

#### Other Conversion Cases

**Lambda Without Default, Target With Default**:

This case is already handled by the compiler, and we do not want to have the behavior here change.
```csharp
delegate void D1(int i = 42);
D1 d = i => { }; // Allowed. This is an implicit conversion which is already allowed in the compiler.
```

**Lambda/Method Group With Default, Target Without Default**:

In this case, we will allow an implicit conversion from a lambda with a default to a delegate without. 
However, because the default parameter(s) in the lambda will go unused (since the lambda cannot be called any other way) 
we will emit a warning as there is a good chance the user has missed something. 

In the case of method groups, we will continue to allow the implicit conversion to delegate with no warning, even if 
there are no defaults in the target delegate type in order to keep our existing behavior. For more information on method group conversions,
see [§10.8](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/conversions.md#108-method-group-conversions).

Note that the core distinction here is that with a method group, the underlying method can still
be called by name in the standard way whereas if a lambda is assigned to a delegate, it can only
be called through the delegate. 

```csharp
void Fun(int i = 4) { ... }

delegate void D1(int i);
D1 d = (int i = 1) => 42; // Allowed WITH warning, since the lambda is now only callable through the delegate
                          // and the default parameter has effectively been thrown away.

D1 d = Fun; // Allowed WITHOUT warning, since Fun and its default parameter are still accessible
            // without using the delegate.
```

### IL/Runtime Behavior

The default parameter values will be emitted to metadata. The IL for this feature will be very similar in nature to the IL emitted for lambdas with `ref` and `out` parameters. A class which
inherits from `System.Delegate` or similar will be generated, and the `Invoke` method will include `.param` directives to set default parameters -- 
just as would be the case for a standard named delegate with default parameters.

These delegate types are able to inspected at runtime, as normal.
In code, users can introspect the `DefaultValue` in the `ParameterInfo` associated with the lambda or method group by using the associated `MethodInfo`.

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
void addWithDefaultMethod(int addTo = 2) {
  return addTo + 1;
}

addWithDefault.Method.GetParameters()[0].DefaultValue; // 2

var add1 = addWithDefaultMethod;
add1.Method.GetParameters()[0].DefaultValue; // 2
```

## Open Questions

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