There are two parts to this proposal:
1. adjust how we find compatible substituted extension containers
2. align with current implementation of extension methods

# Finding a compatible substituted extension container

The proposal here is to look at `extension<extensionTypeParameters>(receiverParameter)` like a method signature, 
and apply current [type inference](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1263-type-inference) 
and [receiver applicability](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#128103-extension-method-invocations) rules to it, given the type of a receiver.

The type inference step infers the extension type parameters (if possible).  
The applicability step tells us whether the extension works with the given receiver, 
using the applicability rules of `this` parameters.

This can be applied both when the receiver is an instance or when it is a type.

Re-using the existing type inference and conversion algorithm solves the variance problem we'd discussed in LDM.  
It makes this scenario work as desired:
```cs
IEnumerable<string>.M();

static class E
{
  extension(IEnumerable<object>)
  {
    public static void M() { }
  }
}
```

Note: this change was made in the new extensions feature branch.

# Aligning with implementation of classic extension methods

Decision: we're resolving new instance extension members exactly like classic extension methods.

The above should bring the behavior of new extensions very close to classic extensions.  
But there is still a small gap with the current implementation of classic extension methods, 
when arguments beyond the receiver are required for type inference of the type parameters
on the extension container.  

The spec for classic extension methods specifies 2 phases (find candidates compatible with the receiver, then complete the overload resolution), 
but the implementation only has 1 phase (find all candidates and do overload resolution with all the arguments including one for the receiver value).

Example we had [discussed](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-10-02.md#extensions):
```cs
public class C
{
    public void M(I<string> i, out object o)
    {
        i.M(out o); // infers E.M<object>
        i.M2(out o); // error CS1503: Argument 1: cannot convert from 'out object' to 'out string'
    }
}
public static class E
{
   public static void M<T>(this I<T> i, out T t) { t = default; }
   extension<T>(I<T> i)
   {
      public void M2(out T t) { t = default; }
   }
}
public interface I<out T> { }
```

We have a few options:
1. Bend the implementation of new extension member lookup to work the same way as classic extensions (1-phase for invocation scenarios, 2-phases for others)
   We can either update the spec for classic extension methods, or document a spec deviation for both classic and new extension methods. 
2. Introduce a subtle difference in behavior between new and old extension methods (and figure out how we then resolve when both kinds are candidates)
   This means that migrating from a classic extension method to a new extension method is not quite 100% compatible.
3. Make new and old extension methods both work the new way, thus breaking existing uses of existing extension methods

Note: depending on this choice, still need to solve how to mix classic and new extension methods in invocation and function type scenarios.

## Details on option 1 (1 phase design to match implementation of classic extension methods)

If we choose to do 1-phase lookup for invocation scenarios, we would:
1. we collect all the candidate methods (both classic extension methods and new ones, without excluding any extension containers)
2. we combine all the type parameters and the parameters into a single signature
3. we apply overload resolution to the resulting set

The transformation at step 2 would take a method like the following:
```cs
static class E
{
  extension<extensionTypeParameters>(receiverParameter)
  {
    void M<methodTypeParameters>(methodParameters);  
  }
}
```
and produce a signature like this:
```
static void M<extensionTypeParameters, methodTypeParameters>(this receiverParameter, methodParameters);
```

Note: for static scenarios, we would play the same trick as in the above section, where we take a type/static receiver and use it as an argument.

Note: This approach solve the mixing question.

# Overload resolution for extension methods

Decision: we're going for maximum compatibility between new instance extension methods and classic extension methods.

We previously concluded that we should prefer more specific extensions members.  
But classic extension methods don't follow that.
Instead they use betterness rules ([better function member](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12643-better-function-member)).

We remove less specific applicable candidates of instance methods (type-like behavior)
```
new Derived().M(new Derived()); // Derived.M

public class Base 
{
    public void M(Derived d) { }
}

public class Derived : Base 
{
    public void M(Base b) { }
}
```
[sharplab](https://sharplab.io/#v2:C4LglgNgPgdgpgdwAQBE4CcwDc4BMAUAlAHQCy+8yamOBhhA3EgPTOobZ5kCwAUHwAEAzEgEAmJACEAhgGc4SPgG8+SNaJECALEnLVOuJLkJIlSAL59L/XsNET9tJCClyFy1ervbd+GfKQAIxMza3MgA)

But we rely on betterness for classic extension methods (parameter-like behavior)
```
"".M(); // E.M(string)

public static class E 
{
    public static void M(this object o) { }
    public static void M(this string s) { }
}
```
[sharplab](https://sharplab.io/#v2:C4LglgNgPgRDB0BZAFASgNwAID03MFElkABARgAZUBYAKFuIGZMyA2ZgJgM1oG9bMBzJq2YAWTCmAALMAGdMAewBGAKwCmAY2CLUmHpgC+/QY2ak2xcZJnyy5TLN36jNA0A=)

```
"".M(""); // ambiguous

public static class E 
{
    public static void M(this object o, string s) { }
    public static void M(this string s, object o) { }
}
```

[sharplab](https://sharplab.io/#v2:C4LglgNgPgRDB0BZAFHAlAbgAQHodYEMBbAIzAHMBXAe0oGcBYAKGYAEBmLVgRgDYuATFgCiWZgG9mWaV049+rACxYUwABZg6WaiQBWAUwDGwbQBou3AAxY6aLOKwBfKTI4WFy1Rq09rdczoGxtp2Ds5M4UA)

Which should we do for new extension methods?

We have competing goals:
1. we want to align extension methods with classic extension methods (portability/compat, parameter-like behaviors) and with instance methods (type-like behaviors)
2. we want to align other extension members (properties) with extension methods
3. we want to align instance and static scenarios

Options for methods:
1. maximum compatibility with classic extension methods
2. maximum alignment with instance methods

## Extension methods proposal

Gather candidates (no applicability involved)
Pruning more candidates:
1. by type inference (including the type parameters on the extension declaration)  
2. by applicability to arguments (including the extension parameter)  
3. Remove based on inaccessible type arguments (apply, see RemoveInaccessibleTypeArguments)
4. Remove less specific or hidden applicable candidates (doesn't apply, see RemoveLessDerivedMembers and RemoveHiddenMembers)
5. Remove static-instance mismatches (apply)  
6. Remove candidates with constraints violations (apply)  
Figure out best candidate: 
1. Remove lower priority/ORPA members (apply)  
2. Remove worse members (better function member) (including the receiver parameter)  

Note: the other pruning steps in overload resolution not sdon't apply to extension receiver parameter scenarios (RemoveDelegateConversionsWithWrongReturnType, RemoveCallingConventionMismatches, RemoveMethodsNotDeclaredStatic)

# Resolution for static methods

Do we want the same semantics for static extension methods (ie. we pretend like we have a receiver/value of the given type) or do we want some new semantics?  
I assume that we want the old semantics.  
This also makes it clear what to expect in a "Color Color" scenario, where we don't know whether the receiver is an instance or static.

# Resolution for properties

We have similar questions for extension properties.  
We're going to cover three questions:
- what kind of pruning should be applied?
- what kind of betterness should be applied?
- how should properties be resolved together with methods?

## Pruning candidates

### Prefer more specific

Yes, we'd previously agree that we want to prefer more specific members.

```csharp
_ = "".P; // should pick E(string).P

public static class E
{
    extension(object o)
    {
        public int P => throw null; 
    }
    extension(string s) // more specific parameter type
    {
        public int P => 0;
    }
}
```

### Static/instance mismatch

If we try to follow the behavior of regular instance or static methods, then the resolution of extension properties should prune based on static/instance mismatch:
```csharp
_ = 42.P;

static class E1
{
    extension(int i)
    {
        public int P => 0;
    }
}
static class E2
{
    extension(int)
    {
        public static int P => throw null;
    }
}
```
```csharp
_ = int.P;

static class E1
{
    extension(int i)
    {
        public int P => throw null;
    }
}
static class E2
{
    extension(int)
    {
        public static int P => 0;
    }
}
```

## Betterness

### Better conversion from expression

```csharp
IEnumerable<C2> iEnumerableOfC2 = null;
_ = iEnumerableOfC2.P; // should we prefer IEnumerable<C1> because it is a better conversion? (parameter-like behavior)

public static class E
{
    extension(IEnumerable<C1> i)
    {
       int P => 0;
    }
    extension(IEnumerable<object> i)
    {
       int P => throw null;
    }
}
public class C1 { }
public class C2 : C1 { }
```
```csharp
_ = IEnumerable<C2>.P; // should we prefer IEnumerable<C1> because it is a better conversion? (parameter-like behavior)

public static class E
{
    extension(IEnumerable<C1>)
    {
      static int P => 0;
    }
    extension(IEnumerable<object>)
    {
      static int P => throw null;
    }
}
```
[classic extension analog](https://sharplab.io/#v2:C4LglgNgPgAgDAAhgRgCwG4CwAoFBmAHgGEAmAPgTAQF4EA7AVwgi2xzADoBZACgEp0CAPRCEAUW498xZGT44cMPEmQA2JCXE4A3jgT6kylOpioEvYAAswAZxWEishAA8+CbQgC+eg0pUmzC2s7aQB7ACMAKwBTAGNgCld3LxxvXCNNR2S0vxhMzRAELI9PIA===)

Should both extensions be applicable both when the receiver is an instance or a type?  
If yes, should we have some preference between those two?  

### Prefer non-generic over generic

If we follow the parameter-like behavior of classic extension methods, then we'd probably want more better member rules:
```csharp
_ = 42.P;

public static class E
{
    extension<T>(T t)
    {
        public int P => throw null; 
    }
    extension(int i) // non-generic, so better function member
    {
        public int P => 0;
    }
}
```

### Prefer by-value parameter

```csharp
_ = 42.P;

public static class E
{
    extension(in int i)
    {
        public int P => throw null; 
    }
    extension(int i) // better parameter-passing mode
    {
        public int P => 0;
    }
}
```

### Issue with betterness in static scenarios

But those betterness rules don't necessarily feel right when it comes to static extension methods:
```csharp
int.M2();
public static class E1
{
    extension(in int i)
    {
       public static void M() { }
    }
}

public static class E2
{
    extension(int)
    {
        public static void M() { }
    }
}
```

## Resolving properties and methods together

Following last LDM's decision to use old semantics for new extension methods, we have to find a new way to resolve properties and methods together.  

Previously, we would only gather candidates from compatible extensions, then we would decide the winning member kind and proceed with resolving (either overload resolution for methods, or picking the single property).  
Now that we're gathering all candidates (without regards to compatibility of extensions), we're thinking to delay the determination of the member kind.

The process that we've brainstormed:
1. gather candidates
2. prune candidates by type inference and applicability to arguments
3. prune candidates by other rules (static/instance mismatch, prefer more specific, ...)
4. determine member kind

If all the remaining candidates are methods, the member kind is method and we resolve to the best method.  
If the only remaining candidate is a property, the member kind is property and we resolve to that property.  
Otherwise we have an ambiguity.  

Note: I don't think there's a scenario for removing lower priority members based on ORPA here. 

### Remove static/instance mismatches

The following example illustrates the relevance of step 3 above, when we have a method and a property, but one has a static/instance mismatch.

```csharp
object.M();

static class E1
{
    extension(object)
    {
        public static string M() => throw null;
    }
}

static class E2
{
    extension(object o)
    {
        public string M() => throw null;
    }
}
```

### Prefer more specific

The following example illustrates the relevance of step 3 above, when we have a method and a property, but one is more specific.  
The problem is that we've decided not to apply the "more specific" pruning step to extension methods, for compatibility with classic extension methods.  

```csharp
string.M();

static class E1
{
    extension(string)
    {
        public static string M() => throw null;
    }
}

static class E2
{
    extension(object)
    {
        public static System.Action M => throw null;
    }
}
```


## Function types

Note: the determination of function types starts with the applicable candidates, with candidates pruned, but some of the pruning rules weren't applicable (static/instance mismatch).  
I assume we'll want all possible applicable pruning steps to apply:

```csharp
var x = C.M; // binds to static method

public class C { }
public static class E1
{
    extension(object)
    {
        public static void M() { }
    }
    extension(object o)
    {
        public void M(int i) { }
    }
}
```
