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

For example, these would not be legal:
```csharp
SelfReturnerIn<string> fin = in x => x;
SelfReturnerRef<string> fref = ref x => x;
SelfReturnerOut<string> fout = out x => x;

delegate T SelfReturnerIn<T>(in T t);
delegate T SelfReturnerRef<T>(ref T t);
delegate T SelfReturnerOut<T>(out T t);
```

All the above examples are not legal due to ambiguity with taking the reference of the returned expression in the `ref` case. For consistency, all other modifiers are also left unsupported and illegal.

Using the `scoped` modifier alone is supported, since it was explicitly ruled out as a type name without the presence of `@` before the identifier in C# 11. This means that the following code will resolve `x` as an implicitly-typed lambda parameter with the `scoped` modifier:

```csharp
SelfReturnerScoped<string> fs = (scoped x) => x;

delegate T SelfReturnerScoped<T>(scoped T t);
```

The type of the parameters matches the type of the parameter in the target delegate type, including the modifiers.

Implicitly-typed parameters with modifiers can be assigned attributes. Implicitly-typed parameters with modifiers will be eligible for being assigned a default value. Currently, only `scoped` ref-struct parameters can have default values.

Discard identifiers will be supported, as long as the modifiers are properly and correctly provided for the respective parameters, matching the target delegate type. This means that a parameter simply declared as `_` will not match a parameter declared `ref int x`, since the discard parameter needs to be accompanied by the `ref` modifier to match.

More specifically, a proper lambda declaration involving discarded parameter names would be:
```csharp
delegate void Test(ref int x, scoped ref int y, params int[] p);

Test t = (ref _, scoped ref _, params _) => { };
```

It will still be illegal for `async` lambdas to contain by-ref parameters, since it is illegal to have by-ref parameters in async methods.

If the lambda expression is not assigned to an expression with an explicit type, the type cannot be inferred from usage. For example, the following is illegal:
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

### Grammar

The change in the spec will require that [the grammar for lambda expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12191-general) be adjusted as follows:

```diff
  implicit_anonymous_function_signature
      : '(' implicit_anonymous_function_parameter_list? ')'
      | implicit_anonymous_function_parameter
      ;

  implicit_anonymous_function_parameter_list
-     : implicit_anonymous_function_parameter
-       (',' implicit_anonymous_function_parameter)*
+     : implicit_parenthesized_anonymous_function_parameter
+       (',' implicit_parenthesized_anonymous_function_parameter)*
      ;

+ implicit_parenthesized_anonymous_function_parameter
+     : attributes? anonymous_function_parameter_modifier* identifier default_argument?
      ;
```

We take into account the fact that the `anonymous_function_parameter_modifier` rule is declared as follows:

```antlr
anonymous_function_parameter_modifier
  : 'ref'
  | 'out'
  | 'in'
  | 'scoped'
  | 'readonly'
  | 'params'
  ;
```

Should the above production rule be updated to only reflect the valid combinations of modifiers, the `implicit_parenthesized_anonymous_function_parameter` rule is updated accordingly to reflect the applicable modifiers.

# Open Questions

- [x] Question: Do we consider making a breaking change supporting `scoped` as the parameter modifier in implicitly-typed parameters?
  - Answer: [Yes](https://github.com/dotnet/csharplang/pull/7369#issuecomment-1670155767)

- [ ] Question:

By always considering `scoped` as a parameter modifier in implicitly-typed lambda parameters, do we also extend this breaking change onto explicitly-typed lambda parameters? This would change the existing snippet's meaning:
```csharp
public class C
{
    public void M(scoped sc, scoped scoped scsc)
    {
        MSc mSc = M;
        MSc mSc1 = (scoped sc, scoped scoped scsc) => { };
    }
    
    public delegate void MSc(scoped sc, scoped scoped scsc);
}

public ref struct @scoped { }
```
`sc` in `mSc1` would now mean an implicitly-typed parameter with the `scoped` modifier, causing an error about not specifying its type explicitly, given the presence of `scsc` in that lambda. As of C# 11, `scoped` in this case refers to the type of the lambda parameter. The delegate and method declarations are unaffected from this behavior adjustment.

Without this breaking change, we have to special-case implicitly-typed lambda parameters over explicitly-typed ones, and adjust the parsing behavior accordingly, which sounds like too much work for supporting a pathological case.

An even further step is to introduce this breaking change in parameters in all signatures, methods, delegates and anonymous functions. However, there is no motivation other than consistency and alignment with the breaking changes around `scoped`.
