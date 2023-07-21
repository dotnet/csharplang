# Declaration of `in`/`ref`/`out` lambda parameters without type name

## Summary  

Allow lambda parameter declarations with `in`/`ref`/`out` to be declared without requiring their type names.
```cs
// Given this delegate
delegate bool TryParse<T>(string text, out T result);

// Allow this simplified parameter declaration
TryParse<int> parse1 = (text, out result) => Int32.TryParse(text, out result);

// Currently only this is valid
TryParse<int> parse2 = (string text, out int result) => Int32.TryParse(text, out result);
```

## Detailed design

### Parameter declaration

Parameter declarations in lambda expressions now permit a single identifier after an `in`/`ref`/`out` modifier on the parameter.

The change in the spec will require that, in [the grammar for lambda expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12191-general), the `implicit_anonymous_function_parameter` rule must be adjusted as follows:

```diff
  implicit_anonymous_function_parameter
-    : identifier
+    : anonymous_function_parameter_modifier? identifier
     ;
```

The type of the parameters matches the type of the parameter in the target delegate type, including the by-reference modifiers.
