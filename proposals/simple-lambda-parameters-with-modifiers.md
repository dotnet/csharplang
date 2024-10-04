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

```diff
implicit_anonymous_function_signature
    : '(' implicit_anonymous_function_parameter_list? ')'
    | implicit_anonymous_function_parameter
    ;

implicit_anonymous_function_parameter_list
    : implicit_anonymous_function_parameter (',' implicit_anonymous_function_parameter)*
    ;

implicit_anonymous_function_parameter
-   : identifier
+   : attributes? 'scoped'? anonymous_function_parameter_modifier? identifier
    ;

explicit_anonymous_function_parameter
    : 'scoped'? anonymous_function_parameter_modifier? type identifier
    ;

anonymous_function_parameter_modifier
    : 'in'
    | 'ref' 'readonly'?
    | 'out'
    ;
```

### Notes

1. This does not apply to a lambda without a parameter list. `ref x => x.ToString()` would not be legal.
1. A lambda parameter list still cannot mix `implicit_anonymous_function_parameter` and `explicit_anonymous_function_parameter` parameters.
1. `(ref readonly p) =>`, `(scoped ref p) =>`, and `(scoped ref readonly p) =>` will be allowed, just as they are with explicit parameters, due to:
   - [Low-level struct improvements](csharp-11.0/low-level-struct-improvements.md#Syntax) in C# 11
   - [`ref readonly` parameters](csharp-12.0/ref-readonly-parameters.md#parameter-declarations) in C# 12

### Semantics

https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12192-anonymous-function-signatures is updated as follows:

```diff
If an anonymous function has an explicit_anonymous_function_signature, then the set
of compatible delegate types and expression tree types is restricted to those that
have the same parameter types and modifiers in the same order (§10.7). In contrast 
to method group conversions (§10.8), contra-variance of anonymous function parameter 
types is not supported. If an anonymous function does not have an anonymous_function_signature, 
then the set of compatible delegate types and expression tree types is restricted 
to those that have no out parameters.

+ If an anonymous function contains an `implicit_anonymous_function_parameter_ex` 
+ with modifiers, then the set of compatible delegate types and expression tree types 
+ is restricted to those that have the same modifiers in the same order (§10.7).
```

## Open LDM questions

1. Currently (in C# 13 `(scoped x) => ...` means "A lambda with a parameter named 'x' with type 'scoped'".  We would like to change that to be treated as the "scoped modifier" instead.  We can gate this behind a language version change so it only breaks on upgrade.  There is also a suitable user workaround if they truly had this pathological code.  Namely, writing `(@scoped x) => ...`.

2. A prior LDM meeting established that neither attributes nor default parameter values would be supported on simple lambda parameters with modifiers.  I would like to revist the decision about attributes.  Specifically, we already allow attributes on simple lambdas like `([Attr] a) => ...` in the compiler today.  So it would be weird to have that support, but have it break if you changed it to `([Attr] ref a) => ...`.  Note: default parameter values are not supported on simple lambdas.  So we can keep the rule that they continue to not be supported when modifiers are added.

## Answered LDM questions

### Allowing attributes or default parameter values

Both seem viable, and may be worth it if we're doing the rest of this work.  With this formalization, we would likely instead say that:

```diff
explicit_anonymous_function_parameter
-    : attributes anonymous_function_parameter_modifier? type identifier default_argument?
+    : attributes anonymous_function_parameter_modifier? type? identifier default_argument?
    ;
```

Note: This grammar is technically ambiguous with implicit_anonymous_function_parameter.  We'd need
to clarify that at least one of `attributes, modifier, type or default-argument` would have to be provided.
We'd also need a rule that all parameters in the list would have to supply a type, or eschew a type.

We would also update the semantic specification to say:

```diff
- The parameters of an anonymous function in the form of a lambda_expression can 
- be explicitly or implicitly typed. In an explicitly typed parameter list, the type
- of each parameter is explicitly stated. In an implicitly typed parameter list, 
- the types of the parameters are inferred from the context in which the anonymous 
-function occurs—specifically, when the anonymous function is converted to a compatible
- delegate type or expression tree type, that type provides the parameter types (§10.7).
+ The parameters of an anonymous function in the form of a lambda_expression can 
+ be explicitly or implicitly typed. In an `anonymous_function_signature` whose 
+ parameters have a provided `type`, the type of each parameter is explicitly stated. 
+ In all other signatures the types of the parameters are inferred from the context 
+ in which the anonymous function occurs—specifically, when the anonymous function 
+ is converted to a compatible delegate type or expression tree type, that type 
+ provides the parameter types (§10.7).
```

#### Answer

Neither attributes nor default parameter values will be supported without a fully-typed lambda.
