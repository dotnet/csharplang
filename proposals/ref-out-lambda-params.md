# Declaration of lambda parameters with modifiers without type name

## Summary  

Allow lambda parameter declarations with modifiers (`in` / `ref` / `out` / etc.) to be declared without requiring their type names.

For example, given this delegate:
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
-    : implicit_anonymous_function_parameter (',' implicit_anonymous_function_parameter)*
+    : implicit_anonymous_function_parameter_ex (',' implicit_anonymous_function_parameter_ex)*
    ;

implicit_anonymous_function_parameter
    : identifier
    ;

implicit_anonymous_function_parameter_ex
    : anonymous_function_parameter_modifier? identifier
    ;
```

Notes

1. This does not apply lambda without a parameter list.  e.g. `ref x => x.ToString()` would not be legal.
2. A lambda parameter list cannot mix `implicit_anonymous_function_parameter_ex` and `explicit_anonymous_function_parameter` parameters.
3. An implicit lambda with a parameter list cannot have attributes (open question on if we want to allow that though).
4. An implicit lambda with a parameter list cannot have a default value (open question on if we want to allow that though).

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
with modifiers, then the set of compatible delegate types and expression tree types 
is restricted to those that have the same modifiers in the same order (§10.7).
```

### Open questions

1. Should attributes be allowed as well?
2. Should default parameter values be allowed?

Both seem viable, and may be worth it if we're doing the rest of this work.  With this formalization, we would likely instead say that:

```diff
explicit_anonymous_function_parameter
-    : attributes anonymous_function_parameter_modifier? type identifier default_argument?
+    : attributes anonymous_function_parameter_modifier? type? identifier default_argument?
    ;
```

With a rule that all parameters in the list would have to supply a type, or eschew a type.

We would also update the semantic specification to say:

```diff
- The parameters of an anonymous function in the form of a lambda_expression can 
be explicitly or implicitly typed. In an explicitly typed parameter list, the type
 of each parameter is explicitly stated. In an implicitly typed parameter list, 
the types of the parameters are inferred from the context in which the anonymous 
function occurs—specifically, when the anonymous function is converted to a compatible
 delegate type or expression tree type, that type provides the parameter types (§10.7).
+ The parameters of an anonymous function in the form of a lambda_expression can 
be explicitly or implicitly typed. In an `anonymous_function_signature` whose 
parameters have a provided `type`, the type of each parameter is explicitly stated. 
In all other signatures the types of the parameters are inferred from the context 
in which the anonymous function occurs—specifically, when the anonymous function 
is converted to a compatible delegate type or expression tree type, that type 
provides the parameter types (§10.7).
```
