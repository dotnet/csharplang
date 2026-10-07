# Type inference using method group natural type

Champion issue: https://github.com/dotnet/csharplang/issues/9007

## Summary
[summary]: #summary

It allows the natural type of a method group to contribute to method type inference

The current [type inference rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1263-type-inference) allow 
- method groups to contribute to output type inference
- infering bounds for the `Ui` type parameters in `C<U1 ... Uk>` when given a `C<V1...Vk>` and `C` is a class, struct, interface or delegate type.  
But they don't allow method groups to contribute to bounds.

```
Test(IsEven); // Error CS0411	The type arguments for method 'Program.Test<T>(Func<T, bool>)' cannot be inferred from the usage. Try specifying the type arguments explicitly.

partial class Program
{
    public static bool IsEven(int x) => x % 2 == 0;
    public static void Test<T>(Func<T, bool> predicate) { }
}
```

## Design

We modify the [explicit parameter type inference rules](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12638-explicit-parameter-type-inferences) to not just apply to explicitly-typed lambdas, but also to method groups:

An *explicit parameter type inference* is made *from* an expression `E` *to* a type `T` in the following way:

- If `E` is an explicitly typed anonymous function \***or method group with a unique signature** with parameter types `U₁...Uᵥ` and `T` is a delegate type or expression tree type with parameter types `V₁...Vᵥ` then for each `Uᵢ` an *exact inference* is made *from* `Uᵢ` *to* the corresponding `Vᵢ`.

