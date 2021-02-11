# Lambda improvements

## Summary

Support lambdas with explicit return type, attributes, and natural type.

## Syntax

```antlr
lambda_expression
  : attribute_list* modifier* lambda_parameters (':' type)? '=>' ( block | body)
  ;

lambda_parameters
  : parameter
  | '(' (parameter (',' parameter)*)? ')'
  ;

parameter
  : attribute_list* modifier* type? (identifier_token | '__arglist') equals_value_clause?
  ;
```
Attributes before a single parameter without parentheses bind to the parameter rather than the lambda.
```csharp
F([MyAttribute] x => x + 1);   // [MyAttribute]x
F([MyAttribute] (y) => y + 1); // [MyAttribute]lambda
```

The return type may be a `ref` type.
```csharp
F(() : ref int => ref x); //  ok
```

Explicit return types and attributes are not supported for anonymous methods declared with `delegate ()` syntax.
```csharp
F(delegate (int x) : int { return x + 1; });         // syntax error ': int'
F([MyAttribute] delegate (int y) { return y + 1; }); // syntax error 'delegate'
```

## Natural type

Lambda expressions will have a natural type, allowing more scenarios where a lambda expression may be used without an explicit delegate type.

The natural type is an `internal` anonymous `delegate` type. Anonymous delegate types will be shared within the compilation for lambdas with common signatures. The names of the delegate types, and the names of the parameters, are unspeakable.

The natural type parameter types are the explicit parameter types, or `object` if implicit.
The natural type return type is the explicit return type, or if implicit, the best common type from the natural types of all `return` expressions, or `object`.

The natural type allows `var` declarations with lambda expressions as initializers.
```csharp
var f1 = () => 1;              // delegate int <anonymous>();
var f2 = () => default;        // delegate object <anonymous>();
var f3 = () : string => null;  // delegate string <anonymous>();
var f4 = x => { };             // delegate void <anonymous>(object <unnamed>);
var f5 = x => x;               // delegate object <anonymous>(object <unnamed>);
var f6 = (ref int x) => ref x; // delegate ref int <anonymous>(ref int <unnamed>);
```

Since the natural type uses `object` for implicit types, this may result in boxing of parameters or return value.
```csharp
var f1 = x => x;       // delegate object <anonymous>(object <unnamed>);
int y = f1(1);         // error: cannot convert 'object' to 'int'
var f2 = x : int => x; // error: cannot convert 'object' to 'int'
```

## Design meetings

- _None_
