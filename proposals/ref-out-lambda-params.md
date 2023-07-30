# Declaration of lambda parameters with modifiers without type name

## Summary  

Allow lambda parameter declarations with modifiers (`in` / `ref` / `out` / `ref readonly` / `scoped` / `scoped ref` / `params`) to be declared without requiring their type names.

Given this delegate:
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

### Parameter declaration

Parameter declarations in lambda expressions with parenthesized parameters now permit a single identifier after a modifier on the parameter. This does not apply to lambda expressions with a single parameter with omitted parentheses.

For example,
```csharp
SelfReturnerIn<string> fin = in x => x;
SelfReturnerRef<string> fref = ref x => x;
SelfReturnerOut<string> fout = out x => x;
SelfReturnerRefReadonly<string> frr = ref readonly x => x;
SelfReturnerScoped<string> frr = scoped x => x;
SelfReturnerScopedRef<string> frr = scoped ref x => x;
SelfReturnerParams<string> frr = params x => x;

delegate T SelfReturnerIn<T>(in T t);
delegate T SelfReturnerRef<T>(ref T t);
delegate T SelfReturnerOut<T>(out T t);
delegate T SelfReturnerRefReadonly<T>(ref readonly T t);
delegate T SelfReturnerScoped<T>(scoped T t);
delegate T SelfReturnerScopedRef<T>(scoped ref T t);
delegate T SelfReturnerParams<T>(params T[] t);
```

are all illegal, due to ambiguity with taking the reference of the returned expression in the `ref` case. For consistency, all other modifiers are also left unsupported and illegal.

The change in the spec will require that [the grammar for lambda expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12191-general) be adjusted as follows:

```diff
  implicit_anonymous_function_parameter_list
-     : implicit_anonymous_function_parameter
-       (',' implicit_anonymous_function_parameter)*
+     : implicit_parenthesized_anonymous_function_parameter
+       (',' implicit_parenthesized_anonymous_function_parameter)*
      ;

+ implicit_parenthesized_anonymous_function_parameter
+     : anonymous_function_parameter_modifier? implicit_anonymous_function_parameter
      ;
```

The type of the parameters matches the type of the parameter in the target delegate type, including the modifiers.

Attributes on the parameters will not be affected in any way.

It will still be illegal for `async` lambdas to contain by-ref parameters, since it is illegal to have by-ref parameters in async methods.

If the lambda expression is not assigned to an expression with a type, the type cannot be inferred from usage. For example, the following is illegal:
```csharp
var d = (in a, ref b, out c) =>
{
    Method(in a, ref b, out c);
}

void Method(in int a, ref int b, out int c)
{
    c = a;
    b = c;
}
```

This remains illegal as the current behavior for implicit-typed parameters without modifiers does not infer the type of the parameters through usage inside the body of the lambda expression. For example, the following is illegal:
```csharp
// Error: The delegate type could not be inferred
var dd = (a, b) => Method2(a, b);

int Method2(int a, int b) => a + b;
```
