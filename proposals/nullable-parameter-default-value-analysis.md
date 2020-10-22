# Nullable Parameter Default Value Analysis

## Analysis of declarations

In a method declaration it's desirable for the compiler to give warnings for parameter default values which are incompatible with the parameter's type.

```cs
void M(string s = null) // warning CS8600: Converting null literal or possible null value to non-nullable type.
{
}
```

However, unconstrained generics present a problem where a bad value can go in but we don't warn about it for compat reasons. Therefore we adopted a strategy of simulating an assignment of the default value to the parameter, giving us the desired warnings in the method signature as well as the desired initial nullable state for the parameter.

```cs
class C<T>
{
    void M0(T t) { }

    void M1(T t = default) // no warning here
    {
        M0(t); // warning CS8604: Possible null reference argument for parameter 't' in 'void C<T>.M0(T t)'.
    }
}
```

It's difficult to update the parameter initial state appropriately in all scenarios. Here are some scenarios where the approach falls over:

### [Overriding methods with optional parameters](https://github.com/dotnet/roslyn/issues/48848)
```cs
Base<string> obj = new Override();
obj.M(); // throws NRE at runtime

public class Base<T>
{
    public virtual void M(T t = default) { } // no warning
}

public class Override : Base<string>
{
    public override void M(string s)
    {
        s.ToString(); // no warning today, but something in this sample ought to warn. :)
    }
}
```
In the above sample we may call the method `Base<string>.M()` and dispatch to `Override.M()`. We need to account for the possibility that the caller implicitly provided `null` as an argument for `s` via the base, but currently we do not do so.

---

### [Lambda conversion to delegates which have optional parameters](https://github.com/dotnet/roslyn/issues/48844)
```cs
public delegate void Del<T>(T t = default);

public class C
{
    public static T M0<T>(T t) => t;

    public static void Main()
    {
        Del<string> del = str => M0(str).ToString(); // expected warning, but didn't get one
        del(); // throws NRE at runtime
    }
}
```
In the above sample we expect that a lambda converted to the type `Del<string>` will have a `MaybeNull` initial state for its parameter because of the default value. Currently we don't handle this case properly.

---

### [Abstract methods and delegate declarations which have optional parameters](https://github.com/dotnet/roslyn/issues/48847)
```cs
public abstract class C
{
    public abstract void M1(string s = null); // expected warning, but didn't get one
}

interface I
{
    void M1(string s = null); // expected warning, but didn't get one
}

public delegate void Del1(string s = null); // expected warning, but didn't get one
```
In the above sample, we want warnings on these parameters which aren't directly associated with any method implementation. However, since these parameter lists don't have any methods with bodies that we want to flow analyze, we never hit the EnterParameters method in NullableWalker which simulates the assignments and produces the warnings.

---

### Indexers with get and set accessors
```cs
public class C
{
    public int this[int i, string s = null] // no warning here
    {
        get // entire accessor syntax has warning CS8600: Converting null literal or possible null value to non-nullable type.
        {
            return i;
        }

        set // entire accessor syntax has warning CS8600: Converting null literal or possible null value to non-nullable type.
        {
        }
    }
}
```
This last sample is just an annoyance. Here we synthesize a distinct parameter symbol for each accessor, whose location is the entire accessor syntax. We simulate the default value assignment in each accessor and give a warning on the parameter, which ends up giving duplicate warnings that don't really show where the problem is.

## Suggested change to declaration analysis

**We shouldn't update the parameter's initial state in flow analysis based on the default value.** It introduces strange complexity and missing warnings around overriding, delegate conversions, etc. that is not worth accounting for, and would cause user confusion if we did account for them. Revisiting the overriding sample from above:

```cs
public class Base<T>
{
    public virtual void M(T t = default) { } // no warning
}

public class Override : Base<string>
{
    public override void M(string s)
    {
        s.ToString(); // no warning today, but something in this sample ought to warn. :)
    }
}
```
As a user you'd probably find a warning on `s.ToString()` confusing and useless--the thing that's broken here is the incompatibility of the type and default value in `T t = default`, and that's where user's fix needs to go.

**Instead, we should enforce that the default value is compatible with the parameter in all scenarios, including unconstrained generics.** I am certain that this is how we should do it in `/langversion:9` in VS 16.9. I also believe that we should do this in `/langversion:8` under the "bug fix" umbrella. `[AllowNull]` can be applied to unconstrained generic parameters to allow `default` as a default value, so C# 8 users are not blocked. I could be convinced otherwise about doing it in `/langversion:8` depending on the impact.

As far as implementation strategy: we should just do this in SourceComplexParameterSymbol at the same time we bind the parameter's default value. We can ensure sufficient amount of consistency, as well as reasonable handling of suppression, perhaps by creating a NullableWalker and doing a "mini-analysis" of the assignment of the default value whose final state is discarded.

---

## Analysis of calls
It turns out there is another whole set of concerns here, which only overlap a bit with the declaration scenarios. Consider the following samples:

```cs
M1().ToString(); // no warning
M1(null).ToString(); // warning
M1("a").ToString(); // no warning

[return: NotNullIfNotNull("s")]
string? M1(string? s = "a") => s;
```

```cs
M2().ToString(); // no warning
M2(null).ToString(); // warning
M2("a").ToString(); // no warning

[return: NotNullIfNotNull("s")]
string? M2([CallerMemberName] string? s = null) => s;
```

Here the constant value that we pass implicitly affects the result of the analysis. For quite some time the code that comes up with the "real" default argument, the one we will actually emit based on presence of `[CallerMemberName]`, `[CallerLineNumber]` etc. attributes, has lived in LocalRewriter. It is somewhat hacked up to make different decisions based on whether it is being called from lowering or from IOperation. The places that need to know this information are:
1. The compiler lowering layer
2. IOperation
3. NullableWalker

NullableWalker must call into the code in LocalRewriter to generate a BoundExpression that wraps the ConstantValue associated with the optional parameter. This tends to make behavior uniform between source and metadata methods, so I believe we should keep doing that. The following example illustrates what I mean:

```cs
M1().ToString(); // we warn here since the synthesized default argument just looks at ConstantValue.Null without considering the suppression

[return: NotNullIfNotNull("s")]
string? M1(string s = null!) => s; // default value warning is suppressed
```

Because we have no way of encoding suppression in constant values in metadata (and probably no desire to come up with such an encoding), this approach gets us the same behavior at the call site whether `M1` is from source or from metadata. It's also worth noting that nullable warnings from the synthesized implicit arguments are **always suppressed**, because they are only a symptom of a bad default value that needs to be fixed in the signature.

Synthesizing the BoundExpression on the fly in NullableWalker has [negative impact on the implementation](https://github.com/dotnet/roslyn/blob/21ca45690196dfd7bd159636a9acf3fd2a86949b/src/Compilers/CSharp/Portable/FlowAnalysis/NullableWalker.cs#L5174-L5200), particularly because we must maintain a dictionary of `_defaultValuesOpt` and we have to use the `_disableNullabilityAnalysis` flag when visiting the synthesized arguments to keep from triggering debug assertions.

## Suggested change to calls

Instead of keeping the logic for "what's the actual implicit argument being passed" in LocalRewriter, let's determine what these arguments are *at the time the call is bound*. I propose modifying BoundCall roughly as follows:

```xml
<Node Name="BoundCall" Base="BoundExpression">
    <!-- ...leave all existing properties as-is -->

    <!-- Implicitly passed default arguments to the method. -->
    <Field Name="DefaultArgumentsOpt" Type="ImmutableArray&lt;BoundDefaultArgument&gt;" Null="allow" />
</Node>

<Node Name="BoundDefaultArgument" Base="BoundNode">
    <Field Name="Parameter" Type="ParameterSymbol" />
    <Field Name="Argument" Type="BoundExpression" />
</Node>
```

This can accomplish a few things:

1. Fix the layering violation in IOperation and NullableWalker by making it so those components no longer need to reach into lowering.
2. Simplify usages and allow removal of some ugly workarounds by making the set of "arguments that will actually be emitted" available in the bound tree.
3. Reduce multiple binding of the default arguments, since we know the arguments will be needed to emit in batch scenarios, and the arguments will be needed if nullable is enabled or if the project uses IOperation-based analyzers.

Lowering would handle the `BoundCall.DefaultArgumentsOpt` by simply folding them into the `BoundCall.Arguments` at the appropriate position when rewriting the call.

# Summary
- Don't use parameter default values to modify the parameter's initial state. Just warn on any bad default value.
- Include the actual default arguments that will be passed in a BoundCall, accounting for any call-site dependent behavior from CallerMemberName and similar attributes.
