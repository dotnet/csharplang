# Treat multiple `using var _` as discards

Alternate proposal to [anonymous-using-declarations.md](anonymous-using-declarations.md).

Champion issue (anonymous using declarations): <https://github.com/dotnet/csharplang/issues/8606>

## Summary

Allow multiple `using var _` declarations by treating them as discards when conflicts would otherwise occur.

This follows the same conflict-detection philosophy as C# 9.0's lambda discard parameters, but applies it to using 
declarations - the one remaining context where developers are forced to create identifiers they don't want.

## Motivation

C# 8.0 introduced using declarations, allowing `using` to be applied directly to local variable declarations:

```csharp
using var someDisposable = GetSomeDisposable();
// someDisposable is disposed at the end of the enclosing scope
```

However, when acquiring multiple disposables solely for cleanup (without needing to reference them), developers hit a 
pain point:

```csharp
using var _ = GetDisposable();
using var _ = GetOtherDisposable();  // Error CS0128: A local variable named '_' is already defined
```

Today, the workaround is ugly numbered discards:

```csharp
using var _ = GetDisposable();
using var __ = GetOtherDisposable();
using var ___ = GetYetAnotherDisposable();

// or

using var _1 = GetDisposable();
using var _2 = GetOtherDisposable();
using var _3 = GetYetAnotherDisposable();
```

We see these patterns extensively in real-world codebases - Roslyn itself has nearly 500 cases alone, and analysis of 
internal and open-source projects shows thousands of instances. This is a genuine pain point that developers are 
actively working around with hacks. Rather than continuing to ignore this issue, we should provide a clean, first-class 
solution.

This proposal makes the natural syntax just work:

```csharp
using var _ = GetDisposable();
using var _ = GetOtherDisposable();
using var _ = GetYetAnotherDisposable();  // All three are discards
```

### Precedent: Lambda Discard Parameters

C# 9.0 introduced a similar conflict-detection approach for lambda parameters:

```csharp
// C# 8.0 and earlier - ERROR
Action<int, int> a = (_, _) => { };  // Error CS0100: duplicate parameter '_'

// C# 9.0 and later - LEGAL (both parameters are discards)
Action<int, int> a = (_, _) => { };
```

This proposal applies that same philosophy to `using var _` declarations.

## Detailed Design

### No Syntax Changes

This proposal requires no changes to C#'s syntax or grammar. This is purely a change to semantics and binding behavior.

### Core Rule and Scope

When the compiler encounters conflicting `_` declarations that would produce CS0128 in **using var declarations** 
(`using var _`), it treats them as discards instead.

This transformation applies ONLY to `using var _` declarations. Regular local variable declarations (`var _` without 
`using`) are NOT affected - developers should use normal discard syntax (`_ = ...;`) for those cases. Lambda parameters 
already have their own discard behavior from C# 9.0 and remain independent.

The detection is purely syntactic during binding:
1. Detect conflicting `_` declarations within a local variable declaration space
2. Check if all conflicts are `using var` declarations
3. If so, treat all as discards instead of producing CS0128

### Using Declarations

Multiple `using var _` declarations in the same scope become discards:

```csharp
void Method()
{
    using var _ = GetDisposable();
    using var _ = GetOtherDisposable();
    using var _ = GetYetAnotherDisposable();  // All three are discards
}
```

A single `using var _` with no conflicts remains a named variable (backward compatibility):

```csharp
using var _ = GetDisposable();  // Named variable (current behavior preserved)
```

### Lambda and Local Function Boundaries

Lambda parameters already have their own discard behavior from C# 9.0, which operates independently. Lambdas and local 
functions create separate declaration spaces:

```csharp
void Method()
{
    using var _ = GetDisposable();
    using var _ = GetOther();           // Both are discards (same scope)
    
    Action a = () =>
    {
        using var _ = GetThird();       // Named variable (separate scope)
    };
}
```

### Method and Local Function Parameters

Method and local function parameters are externally visible signatures and never participate in this transformation. A 
method or local function parameter named `_` conflicting with a `using var _` remains an error:

```csharp
void Method(int _)  // Parameter must stay named (externally visible)
{
    // ERROR CS0128: single using var _ doesn't get special treatment, conflicts with parameter
    using var _ = GetDisposable();
}
```

## Specification Changes

### §7.3 Declarations - Declaration Spaces

The existing rule states that local variable declaration spaces may be nested, but it is an error for conflicting 
declarations to have the same name (producing CS0128). This error is detected syntactically during binding.

Add the following exception to this rule:

> However, when multiple `using var` declarations would conflict solely because they all use the identifier `_`, then all 
> such conflicting declarations shall be treated as discards (§9.2.9.2) instead. The identifier `_` introduces no name 
> into any declaration space, and the value cannot be referenced.
>
> *Note*: This rule applies only when conflicts would occur. A single `using var _` with no conflicts retains its 
> meaning as a named variable for backward compatibility.

### §7.3 Declarations - Method and Local Function Parameters (Optional)

This clarification may not be necessary if the above section is sufficiently clear, but can be added if desired:

> Method and local function parameters are externally visible and must remain named identifiers. When a method or local 
> function parameter named `_` would conflict with a using declaration named `_`, the conflict remains a compile error.

### §9.2.9.2 Discards

Update the discard specification:

> A discard is indicated by the identifier `_` in the following contexts:
> - [existing contexts...]
> - A using var declaration (`using var _`) when multiple such declarations with the same identifier would conflict in 
>   the same local variable declaration space (§7.3)

### §13.14 The using statement

Add clarification for using declarations:

> When multiple `using var _` declarations would conflict within the same local variable declaration space, the conflict 
> resolution rules in §7.3 apply.

## Open Design Questions

### Should this support explicit types (`using IDisposable _`)?

This proposal currently specifies `using var _` only. An alternative would be to also support explicitly-typed using 
declarations like `using IDisposable _`.

**Arguments for `var _` only:**
- Discards don't normally present their type (though they do acquire one from initialization)
- `var _` is the closest analog to discard syntax in the variable declaration space
- Simpler, more focused feature
- Avoids questions about type compatibility when mixing `var _` and `Type _`

**Arguments for supporting both `var _` and `Type _`:**
- Some developers prefer explicit types and would be frustrated by being forced to use `var`
- The implementation complexity is similar either way
- More general solution
- Example of mixing:
  ```csharp
  using var _ = GetDisposable();
  using IDisposable _ = GetOtherDisposable();  // Would both become discards
  ```

**Recommendation**: Start with `var _` only. If explicit types prove to be a significant pain point, they can be added 
later without breaking changes.

### Should lambda parameter `_` and `using var _` unify?

This proposal keeps `using var _` discard detection separate from C# 9.0's lambda parameter discard detection. This 
means:

```csharp
Action a = _ =>
{
    using var _ = GetDisposable();  // ERROR CS0128 (parameter and using var conflict)
};
```

**Arguments for keeping them separate:**
- Simpler mental model - each feature operates independently within its own scope
- Avoids complexity in the compiler's conflict detection spanning different declaration contexts
- C# 9.0 lambda parameter discards already work well on their own
- Clearer what's happening - conflicts are detected purely within one declaration space

**Arguments for unifying:**
- More consistent - both use the same philosophy of "detect conflicts, make them all discards"
- Avoids surprising CS0128 errors when mixing lambda parameters and using declarations
- More general solution that handles all `_` conflicts together
- Example of unified behavior:
  ```csharp
  Action a = _ =>
  {
      using var _ = GetDisposable();  // Both parameter and using var become discards
  };
  
  Action<int, int> b = (_, _) =>
  {
      using var _ = GetDisposable();  // All three become discards
  };
  ```

**Recommendation**: Start with separate detection for simplicity. The LDM can decide if unification is desirable.

## Drawbacks

### Pragmatic Tradeoffs

Ideally, `_` would be treated universally as a discard in all contexts. However, achieving that ideal would require 
breaking changes to existing code where `_` is used as a regular identifier, and would introduce conflicts with using 
alias directives at the top level (`using _ = ...;`). The latter conflict is not hypothetical - we've observed `using _` 
in using aliases in several real codebases. Given C#'s strong commitment to compatibility, we're unlikely to get there 
soon without severe breaks.

This proposal takes the proven pattern from C# 9.0 lambda discards and extends it minimally to solve the specific pain 
point with using declarations. It's non-breaking, lightweight, and focused on the one scenario where developers are 
forced to create unwanted identifiers.

Importantly, `using var _` is already in widespread use in existing codebases - developers have been working around the 
CS0128 limitation with numbered discards (`_1`, `_2`, etc.) for years. This proposal simply makes that existing pattern 
less painful and more natural.

**Future Compatibility**: This proposal does not conflict with a hypothetical future where C# might treat `_` 
universally as a discard. If that day comes, this feature would simply become a subset of the broader change.

### Specific Concerns

1. **Context-dependent meaning**: The meaning of `using var _` changes based on whether conflicts exist. However:
   - This only affects currently-illegal code (CS0128 error), so there is no breaking change
   - This follows the precedent established by C# 9.0 lambda discard parameters

2. **Asymmetry with regular locals**: `using var _` gets special treatment while `var _` does not. However:
   - This asymmetry is justified: `using` requires a declaration; regular code doesn't (use `_ = ...;` instead)
   - The pain point is specifically about being forced to create identifiers you don't want
   - This keeps the feature focused and minimal

## Alternatives

### Alternative: Apply to all `var _` declarations

Extend this to all local variable declarations, not just using declarations:

```csharp
var _ = ComputeSomething();
var _ = ComputeOther();  // Would be legal
```

However, this is unnecessary. Developers can and should write:

```csharp
_ = ComputeSomething();
_ = ComputeOther();
```

The pain point only exists where syntax *requires* a declaration (using declarations and lambda parameters). Making 
this change broadly would add complexity without addressing a real problem.

## Additional Notes

### IDE Support

IDEs could provide analyzers that suggest converting numbered discards (`_1`, `_2`) in using declarations to the 
`using var _` pattern when multiple are present. However, no compiler diagnostics are proposed - this is purely 
enabling currently-illegal code to work naturally.

## Related Issues

This proposal addresses [#2235](https://github.com/dotnet/csharplang/issues/2235) and champion issue 
[#8606](https://github.com/dotnet/csharplang/discussions/8605).
