# Nullable Parameter Default Value Analysis

## Analysis of declarations

In a method declaration it's desirable for the compiler to give warnings for parameter default values which are incompatible with the parameter's type.

```cs
void M(string s = null) // warning CS8600: Converting null literal or possible null value to non-nullable type.
{
}
```

However, unconstrained generics present a problem where a bad value can go in but we don't warn about it for compat reasons. Therefore we adopted a strategy of simulating a assignment of the default value to the parameter in the method body, then joining in the resulting state, giving us the desired warnings in the method signature as well as the desired initial nullable state for the parameter.

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
    public static void Main()
    {
        Del<string> del = str => str.ToString(); // expected warning, but didn't get one
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
    public virtual void M(T t = default) { } // let's start warning here
}

public class Override : Base<string>
{
    public override void M(string s)
    {
        s.ToString(); // let's not warn here
    }
}
```
As a user you'd probably find a warning on `s.ToString()` confusing and useless--the thing that's broken here is the incompatibility of the type and default value in `T t = default`, and that's where user's fix needs to go.

**Instead, we should enforce that the default value is compatible with the parameter in all scenarios, including unconstrained generics.** I am certain that this is how we should do it in `/langversion:9` in VS 16.9. I also believe that we should do this in `/langversion:8` under the "bug fix" umbrella. `[AllowNull]` can be applied to unconstrained generic parameters to allow `default` as a default value, so C# 8 users are not blocked. I could be convinced otherwise about doing it in `/langversion:8` depending on the impact.

As far as implementation strategy: we should just do this in SourceComplexParameterSymbol at the same time we bind the parameter's default value. We can ensure sufficient amount of consistency, as well as reasonable handling of suppression, perhaps by creating a NullableWalker and doing a "mini-analysis" of the assignment of the default value whose final state is discarded.

