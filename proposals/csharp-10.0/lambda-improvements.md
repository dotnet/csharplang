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

## Attributes
Attributes may be added to lambda expressions.
```csharp
f = [MyAttribute] x => x;          // [MyAttribute]lambda
f = [MyAttribute] (int x) => x;    // [MyAttribute]lambda
f = [MyAttribute] static x => x;   // [MyAttribute]lambda
f = [return: MyAttribute] () => 1; // [return: MyAttribute]lambda
```
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

Attributes on the lambda or lambda parameters will be emitted to metadata on the method that maps to the lambda.

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
A lambda expression has a natural type if the parameters types are explicit and either the return type is explicit or there is a common type from the natural types of all `return` expressions in the body. Otherwise there is no natural type.

A method group has a natural type if the method group contains a single method and the method has no unbound type parameters.

Lambdas or method groups with natural types can be used as initializers in `var` declarations.

If the lambda or method group has no more than 16 parameters and no return value, and all parameters are passed by value, the natural type will be `delegate void System.Action<P1, ..., Pn>(P1, ..., Pn)` where `P1, ..., Pn` are the lambda parameter types.

If the lambda or method group has no more than 16 parameters and a by-value return type, and all parameters are passed by value, the natural type will be `delegate R System.Func<P1, ..., Pn, R>(P1, ..., Pn)` where `P1, ..., Pn` are the lambda parameter types, and `R` is the lambda return type.

Otherwise the natural type will be a synthesized `internal` anonymous `delegate` type with a signature that matches the lambda or method group.

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

### Anonymous delegate type
If synthesized delegate types are required, the compiler generates generic anonymous delegate types that are shared across all anonymous delegates in the module that have the same number of parameters and same parameter ref kinds.

The names of the synthesized delegate types and the names of the parameters are unspeakable.

The anonymous delegate types are not co- or contra-variant unlike the delegates constructed from `System.Action<>` and `System.Func<>`.

The natural type of a method group does not include any `modopt()` or `modreq()` on the method group.

`fA` and `fB` share a common generic type `internal delegate ref R <anonymous2><P1>(ref P1 <unnamed>)` below:
```csharp
var f9 = (ref int i) => { };    // delegate void <anonymous1><int>(ref int <unnamed>);
var fA = (ref char c) => ref c; // delegate ref char <anonymous2><char, char>(ref char <unnamed>);
var fB = (ref int i) => ref i;  // delegate ref int <anonymous2><int, int>(ref int <unnamed>);
```

_Issue: Should the compiler always generate anonymous delegate types rather than using `System.Action<>` and `System.Func<>`? There are observable differences: 1. the anonymous delegate types are not co- or contra-variant; and 2. using a synthesized delegate type would mean the following assignment to `y` would fail: `var x = () => { }; Action y = x;`._

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

To avoid a breaking change, overload resolution will be updated to prefer lambda and method group conversions that do not use the natural type.
_The example below demonstrates the tie-breaking rule for lambdas. Is there an equivalent example for method groups?_
```csharp
static void Execute(Expression<Func<string>> e) { }
static void Execute(Delegate d) { }

static string GetString() => "";
static int GetInt() => 0;

Execute(() => "");  // Execute(Expression<Func<string>>) [tie-breaker]
Execute(() => 0);   // Execute(Delegate)

Execute(GetString); // Execute(Delegate)
Execute(GetInt);    // Execute(Delegate)
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

## Design meetings

- _None_
