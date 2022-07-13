# Default Parameters for Lambdas and Method Groups

## Summary

[summary]: #summary

To build on top of the lambda improvements introduced in C#10, we propose adding support for default parameter values in lambdas. This would enable users to implement the following lambdas.

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

## Motivation

[motivation]: #motivation

App frameworks in the .NET ecosystem leverage lambdas heavily to allow users to quickly write business logic associated with an endpoint.

```csharp
var app = WebApplication.Create(args);

app.MapPost("/todos/{id}", (int id, string task, TodoService todoService) => {
  var todo = todoService.Create(id, task);
  return Results.Created(todo);
});
```

Lambdas don't currently support setting default values on parameters, so if a developer wanted to build an application that was resilient to scenarios where users didn't provide data, they're left to either use local functions or set the default values within the lambda body, as opposed to the more succinct proposed syntax.

```csharp
var app = WebApplication.Create(args);

app.MapPost("/todos/{id}", (int id, string task = "foo", TodoService todoService) => {
  var todo = todoService.Create(id, task);
  return Results.Created(todo);
});

```

The proposed syntax also has the benefit of reducing confusing differences between lambdas and local functions, making it easier to reason about constructs and "grow up" lambdas to functions without compromising features, particularly in other scenarios where lambdas are used in APIs where method groups can also be provided as references.

For example: 
```csharp
var app = WebApplication.Create(args);

Result todoHandler(int id, string task = "foo", TodoService todoService) {
  var todo = todoService.Create(id, task);
  return Results.Created(todo);
}

app.MapPost("/todos/{id}", todoHandler);
```

Method groups also don't currently support default parameters in many instances which may cause confusion. For instance, consider the following example: 

```csharp
void M(int p = 1) {
  Console.WriteLine(p);
}

var m = M;
// type of m is inferred as Action<int>
m(); // error: Action<int> must take a parameter
```
The type of a method group in cases like this is inferred to be `Action` or `Func`, neither of which store information about default parameters. This leads to the 
error condition above, which seems fairly counterintuitve for many users. It seems that improving the ergonomics here would be ideal, especially
since this change affects lambdas and for consistency it is beneficial for method groups
to have the same behavior as lambdas.  


## Detailed design

[design]: #detailed-design

Currently, when a user implements a lambda with a default value, the compiler raises a `CS1065` error.

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
```

When a user attempts to use a method group where the underlying method has a default parameter, the
default param isn't propagated and the compiler raises a `CS7036` (no given parameter) error.
```cs
void M(int i = 1) {}

var m = M; // Infers Action<int>
m(); // Error: no value provided for arg0
```

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

The default value will be emitted to metadata. Users can introspect the `DefaultValue` in the `ParameterInfo` associated with the lambda or method group
by using the associated `MethodInfo`.

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
void addWithDefaultMethod(int addTo = 2) {
  return addTo + 1;
}

addWithDefault.Method.GetParameters()[0].DefaultValue; // 2

var add1 = addWithDefaultMethod;
add1.Method.GetParameters()[0].DefaultValue; // 2
```

As with the behavior for delegates with `ref` or `out` parameters, a new natural type is generated for each lambda or method group defined with any default parameter values.

Note that in the below examples, the notation `<>F{00000n}`, $n = {1, 2, ...}$ is used as a convention for generated anonymous delegate names. This is for explanation purposes only.
The notation should be interpreted as an unspeakable generated name, and not as a proposal for the name that the compiler would actually generate in these cases.

```csharp
var addWithDefault = (int addTo = 2) => addTo + 1;
// internal delegate int <>F{00000002}(int arg0 = 2);
var printString = (string toPrint = "defaultString") => Console.WriteLine(toPrint);
// internal delegate void <>F{00000003}(string arg0 = "defaultString");
string joinStrings(string s1, string s2, string sep = " ") { return $"{s1}{sep}{s2}"; }
var joinFun = joinStrings;
// internal delegate string <>F{00000004}(string arg0, string arg1, string arg3 = " ");
```

This enhancement requires the following changes to the grammar for lambda expressions.

```diff
explicit_anonymous_function_parameter
-    : anonymous_function_parameter_modifier? type identifier
+    : anonymous_function_parameter_modifier? type identifier default_argument?
    ;
```
No changes to the grammar are necessary for method groups since this proposal would only change their semantics.

## Delegate Unification Behavior

The delegates described previously will be unified when the same parameter (based on position) has the same default value, regardless of parameter name.
The following examples demonstrate this behavior:

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
// internal delegate int <>F{00000002}(int arg0 = 13);
var b = (int i = 0) => 2;
// internal delegate int <>F{00000003}(int arg0 = 0);
var c = (int i = 13) => 3;
// internal delegate int <>F{00000002}(int arg0 = 13);
var d = (int c = 13) => 1;
// internal delegate int <>F{00000002}(int arg0 = 13);

var e = E;
// internal delegate int <>F{00000002}(int arg0 = 13);
var f = F;
// internal delegate int <>F{00000003}(int arg0 = 0);
var g = G;
// internal delegate int <>F{00000003}(int arg0 = 0);

a = b; // Not allowed
a = c; // Allowed
a = d; // Allowed
c = e // Allowed
e = f // Not Allowed
b = f // Allowed
e = g // Allowed
```

Similarly, there is of course compatibility with named delegates that have default parameters as well: 
```csharp
int D(int a = 1) {
  return a;
}

delegate int Del(int a = 1);
// Open question; default parameter value in Delegate type does not match, 
// but could do implicit conversion
Del del = (int x = 100) => x;

// Allowed, because default parameter value in lambda matches default parameter value in delegate
Del del1 = (int x = 1) => x;

Del del2 = D;
// This behavior does not change and compiles as before as per the method group conversion rules 

var d = D;
// d is inferred as internal delegate int <>F{00000001}(int arg0 = 1);

Del del3 = d; 
// Not allowed. Cannot convert internal delegate type to Del.
// Note that there is no change here from previous behavior, when d would be inferred
// to be Action<int> since Action<int> also cannot be converted to a named delegate type. 
```

There is still an open question around how we want to handle certain cases of delegate re-assignment; this is addressed in the [Open Questions](#open-questions) section.

## Compatibility With Existing Method Group Conversions

Since lambdas and method groups with default parameter values are typed as anonymous delegates, the
method group conversion rules as described in [§10.8](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#108-method-group-conversions)
apply. We can first consider the following example: 

```csharp
int M(int x = 20) {
  return x;
}

var d = (int x = 10) => x;
// internal delegate int <>F{00000002}(int arg0 = 10);
d = M; // Ok. The existing method group rules apply here, and the signature of M
       // is allowed to be converted to internal delegate int <>F{00000002}(int arg0 = 10);
d();   // will use default value from original lambda. Confusing
```
The above code has an implicit conversion from a method group to a delegate. However, the anonymous
delegate type that the method group is converted to has a default parameter which differs from the underlying method. 

Though this case semantically makes sense and the existing conversion rules can be used here, this may be a misleading case and thus it seems wise for a warning to be emitted in cases like this.

## Open Questions

**Open question:** how does this interact with the existing `DefaultParameterValue` attribute?

**Proposed answer:** For parity, permit the `DefaultParameterValue` attribute on lambdas and ensure that the delegate generation behavior matches for default parameter values supported via the syntax.

```csharp
var a = (int i = 13) => 1;
// same as
var b = ([DefaultParameterValue(13)] int i) => 1;
b = a; // Allowed
```

**Open question** How do we handle reassignment of lambdas with default parameters?
This open question pertains to the following case:

```csharp
var d = (int x = 10) => x;
// internal delegate int <>F{00000002}(int arg0 = 10);
d = (int y = 20) => y; // Error or implicit conversion?
```
In the above case, `d`, which has an anonymous delegate type, is reassigned to a different lambda expression. We can either raise an error in this case
or allow for an implicit "target-type" conversion since the new lambda expression is identical in signature except for the differing
default parameter value. If we perform the implicit conversion, though, then the second lambda expression would be backed by an anonymous delegate time with a different default parameter.
This would have the effect of "changing" the default parameter fo the lambda. Thus, if we allow this case we should emit a warning since this might be unexpected behavior.