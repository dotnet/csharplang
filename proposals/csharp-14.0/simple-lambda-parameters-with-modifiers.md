# Simple lambda parameters with modifiers

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Champion issue: <https://github.com/dotnet/csharplang/issues/8637>

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

No changes.  The [latest lambda grammar](../csharp-12.0/lambda-method-group-defaults.md#detailed-design) is:

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
   - [Low-level struct improvements](../csharp-11.0/low-level-struct-improvements.md#syntax) in C# 11
   - [`ref readonly` parameters](../csharp-12.0/ref-readonly-parameters.md#parameter-declarations) in C# 12
1. The presence/absence of a type has no impact on whether a modifier is required or optional.  In other words, if a modifier was required
   with a type present, it is still required with the type absent.  Similarly, if a modifier was optional with a type present, it is 
   optional with the type absent.

### Semantics

https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/expressions#12192-anonymous-function-signatures is updated as follows:

In a `lambda_parameter_list` all `lambda_parameter` elements must either have a `type`
present or not have a `type` present.  The former is an "explicitly
typed parameter list", while the latter is an "implicitly typed
parameter list".

Parameters in an implicitly typed parameter list cannot have a `default_argument`.  They
can have an `attribute_list`.

The following change is required to [anonymous function conversions](../csharp-12.0/lambda-method-group-defaults.md#detailed-design):

[...]
> If F has an explicitly **or implicitly typed parameter list**, each parameter in D has the same type and
> modifiers as the corresponding parameter in F ignoring params modifiers and default values.

### Notes/Clarifications

`scoped` and `params` are allowed as explicit modifiers in a lambda without an explicit type present. Semantics
remain the same for both.  Specifically, neither is part of the determination made
[in](https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/expressions#12192-anonymous-function-signatures):

> If an anonymous function has an explicit_anonymous_function_signature, then the set of compatible delegate
> types and expression tree types is restricted to those that have the same parameter types and modifiers in
> the same order.

The only modifiers that restrict compatible delegate types are `ref`, `out`, `in` and `ref readonly`. 
For example, in an explicitly typed lambda, the following is currently ambiguous:

```c#
delegate void D<T>(scoped T t) where T : allows ref struct;
delegate void E<T>(T t) where T : allows ref struct;

class C
{
    void M<T>() where T : allows ref struct
    {
        // error CS0121: The call is ambiguous between the following methods or properties: 'C.M1<T>(D<T>)' and 'C.M1<T>(E<T>)'
        // despite the presence of the `scoped` keyword.
        M1<T>((scoped T t) => { });
    }

    void M1<T>(D<T> d) where T : allows ref struct
    {
    }

    void M1<T>(E<T> d) where T : allows ref struct
    {
    }
}
```

This remains the case when using implicitly typed lambdas:

```c#
delegate void D<T>(scoped T t) where T : allows ref struct;
delegate void E<T>(T t) where T : allows ref struct;

class C
{
    void M<T>() where T : allows ref struct
    {
        // This will remain ambiguous.  'scoped' will not be used to restrict the set of delegates.
        M1<T>((scoped t) => { });
    }

    void M1<T>(D<T> d) where T : allows ref struct
    {
    }

    void M1<T>(E<T> d) where T : allows ref struct
    {
    }
}
```

### Open Questions

1. Should `scoped` *always* be a modifier in a lambda in C# 14?  This matters for a case like:

   ```C#
   M((scoped s = default) => { });
   ```

   In this case, this is does *not* fall under the 'simple lambda parameter' spec, as a 'simple lambda'
   cannot contain a initializer (`= default`).  As such, `scoped` here is treated as a `type` (like it was
   in C# 13).  Do we want to maintain this?  Or would it just be simpler to have a more blanket rule that
   `scoped` is always modifier, and thus would still be a modifier here on an invalid simple parameter?

   Recomendation: Make this a modifier.  We already dissuade people from types that are all lowercase,
   *AND* we've made it illegal to make a type called `scoped` in C# as well.  So this could only be some
   sort of case of referencing a type from another library.  The workaround is trivial if you did somehow
   hit this.  Just use `@scoped` to make this a type name instead of a modifier.

2. Allow `params` in a simple lambda parameter? Prior lambda work already added support for `params T[] values`
   in a lambda.  This modifier is optional, and the lambda and the original delegate are allowed to have a
   mismatch on this modifier (though we warn if the delegate does not have the modifier and the lambda does).
   Should we continue allowing this with a simple lambda parameter.  e.g. `M((params values) => { ... })`

   Recomendation: Yes.  Allow this.  The purpose of this spec is to allow just dropping the 'type' from a
   lambda parameter, while keeping the modifiers.  This is just another case of that.  This also just falls
   out from the impl (as did supporting attributes on these parameters), so it's more work to try to block
   this.

   Conclusion 1/15/2025: No. This will always be an error. There do not appear to be any useful cases for this
   and no one is asking for this.  It is easier and safer to restrict this non-sensical case from the start.
   If relevant use cases are presented, we can reconsider.

3. Does 'scoped' influence overload resolution?  For example, if there were multiple overloads of a delegate
   and one had a 'scoped' parameter, while the other did not, would the presense of 'scoped' influencce
   overload resolution.

   Recomendation: No.  Do not have 'scoped' influence overload resolution.  That is *already* how things
   work with normal *explicitly typed* lambdas.  For example:

   ```c#
   delegate void D<T>(scoped T t) where T : allows ref struct;
   delegate void E<T>(T t) where T : allows ref struct;

   class C
   {
       void M<T>() where T : allows ref struct
       {
           M1<T>((scoped T t) => { });
       }

       void M1<T>(D<T> d) where T : allows ref struct
       {
       }

       void M1<T>(E<T> d) where T : allows ref struct
       {
       }
   }
   ```

   This is ambiguous today.  Despite having 'scoped' on `D<T>` and 'scoped' in the lambda parameter, we
   do not resolve this.  We do not believe this should change with implicitly typed lambdas.
   
   Conclusion 1/15/2025: The above will hold true for 'simple lambas' as well.  'scoped' will not influence
   overload resolution (while `ref` and `out` will continue to do so).

5. Allow '(scoped x) => ...' lambdas?

   Recommendation: Yes.  If we do not allow this then we can end up in scenarios where a user can write
   the full explicitly typed lambda, but not the implicitly typed version.  For example:

   ```c#
   delegate ReadOnlySpan<int> D(scoped ReadOnlySpan<int> x);

   class C
   {
       static void Main(string[] args)
       {
           D d = (scoped ReadOnlySpan<int> x) => throw null!;
           D d = (ReadOnlySpan<int> x) => throw null!; // error! 'scoped' is required
       }
   }
   ```

   Removing 'scoped' here would cause an error (the language requires the correspondance in this case between
   the lambda and delegate.  As we want the user to be able to write lambdas like this, without specifying
   the type explicitly, that then means that `(scoped x) => ...` needs to be allowed. 

   Conclusion 1/15/2025: We will allow `(scoped x) => ...` lambdas.
