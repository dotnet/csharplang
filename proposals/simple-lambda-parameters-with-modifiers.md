# Simple lambda parameters with modifiers

## Summary

Allow lambda parameters to be declared with modifiers without requiring their type names. For example, `(ref entry) =>` rather than `(ref FileSystemEntry entry) =>`.

As another example, given this delegate:
```cs
delegate bool TryParse<T>(string text, out T result);
```

Allow this simplified parameter declaration:
```cs
TryParse<int> parse1 = (text, out result) => Int32.TryParse(text, out result);
```

Currently only this is valid:
```cs
TryParse<int> parse2 = (string text, out int result) => Int32.TryParse(text, out result);
```

## Detailed design

### Grammar

No changes.  The [latest lambda grammar](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/lambda-method-group-defaults.md#detailed-design) is:

```g4
 lambda_expression
   : modifier* identifier '=>' (block | expression)
   | attribute_list* modifier* type? lambda_parameter_list '=>' (block | expression)
   ;

lambda_parameter_list
  : lambda_parameters (',' parameter_array)?
  | parameter_array
  ;

 lambda_parameter
   : identifier
  | attribute_list* modifier* type? identifier default_argument?
   ;
```

This grammar already considers `modifiers* identifier` to be syntactically legal.

### Notes

1. This does not apply to a lambda without a parameter list. `ref x => x.ToString()` would not be legal.
1. A lambda parameter list still cannot mix `implicit_anonymous_function_parameter` and `explicit_anonymous_function_parameter` parameters.
1. `(ref readonly p) =>`, `(scoped ref p) =>`, and `(scoped ref readonly p) =>` will be allowed, just as they are with explicit parameters, due to:
   - [Low-level struct improvements](csharp-11.0/low-level-struct-improvements.md#Syntax) in C# 11
   - [`ref readonly` parameters](csharp-12.0/ref-readonly-parameters.md#parameter-declarations) in C# 12

### Semantics

https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12192-anonymous-function-signatures is updated as follows:

```diff
In a `lambda_parameter_list` all `lambda_parameter` elements must either have a `type`
present or not have a `type` present.  The former is an "explicitly
typed parameter list", while the latter is an "implicitly typed
parameter list".

Parameters in an implicitly typed parameter list cannot have a `default_argument`.  They
can have an `attribute_list`.

The following change is required to [anonymous function conversions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/lambda-method-group-defaults.md#detailed-design):

```diff
[...]
- If F has an explicitly typed parameter list, each parameter in D has the same type and
- modifiers as the corresponding parameter in F ignoring params modifiers and default values.
> If F has an explicitly **or implicitly typed parameter list**, each parameter in D has the same type and
> modifiers as the corresponding parameter in F ignoring params modifiers and default values.
```
