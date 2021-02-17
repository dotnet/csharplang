# Lambda improvements

## Summary

Support lambdas with attributes, explicit return type, and natural type.

## Attributes
### Motivation
Support for attributes on lambdas would provide parity with methods and local functions.

### Design
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

## Explicit return type
### Motivation
Support for explicit return types would provide symmetry with lambda parameters where explicit types can be specified.

Allowing explicit return types would also provide control over compiler performance in nested lambdas where overload resolution must bind the lambda body currently to determine the signature.

### Design
An explicit return type may be specified after the parameter list.
```csharp
f = () : T => default;              // () : T
f = x : int => 1;                   // <unknown> : int
f = (ref int x) : ref int => ref x; // ref int : ref int
f = static _ : void => { };         // <unknown> : void
```

Explicit return types are not supported for anonymous methods declared with `delegate { }` syntax.
```csharp
f = delegate : int { return 1; };         // syntax error
f = delegate (int x) : int { return x; }; // syntax error
```

## Natural type
### Motivation
A natural type for lambda expressions and method groups will allow more scenarios where lambdas and method groups may be used without an explicit delegate type, including in `var` declarations.

### Design
A lambda expression has a natural type if the parameters types are explicit and either the return type is explicit or there is a common type from the natural types of all `return` expressions in the body. Otherwise there is no natural type.

A method group has a natural type if the method group contains a single method and the method or reduced extension method has no unbound type parameters.

If the lambda or method group has no more than 16 parameters and no return value, and all parameters are passed by value, the natural type will be `delegate void System.Action<P1, ..., Pn>(P1, ..., Pn)` where `P1, ..., Pn` are the lambda parameter types.

If the lambda or method group has no more than 16 parameters and a by-value return type, and all parameters are passed by value, the natural type will be `delegate R System.Func<P1, ..., Pn, R>(P1, ..., Pn)` where `P1, ..., Pn` are the lambda parameter types, and `R` is the lambda return type.

Otherwise the natural type will be an `internal` anonymous `delegate` type with a signature matching the lambda signature. The names of the anonymous delegate types and the names of the parameters are unspeakable.

Lambdas or method groups with natural types can be used as initializers in `var` declarations.

```csharp
var f1 = () => default;        // error: no natural type
var f2 = x => { };             // error: no natural type
var f3 = x => x;               // error: no natural type
var f4 = () => 1;              // System.Func<int>
var f5 = () : string => null;  // System.Func<string>
var f6 = (ref int x) => ref x; // delegate ref int <anonymous>(ref int <unnamed>);
```

```csharp
static void F1() { }
static void F1(this string s) { }
static void F2(this string s) { }

var f7 = F1;    // error: multiple methods
var f8 = "".F1; // System.Action
var f9 = F2;    // System.Action<string> 
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
