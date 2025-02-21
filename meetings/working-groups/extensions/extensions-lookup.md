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

# Lookup: more specific vs. better function member

We previously concluded that we should prefer more specific extensions members.  
But we could also leverage something like [better function member](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12643-better-function-member) to resolve more ambiguous non-method scenarios. 

We have competing goals:
1. we want to align extension methods with classic extension methods, and other extension members (properties) with extension methods
2. we want to align instance and static scenarios
3. we want to type-like behaviors as opposed to parameter-like behaviors

```
_ = "".P;

public static class E
{
    extension(object o)
    {
        public int P => throw null; 
    }
    extension(string s) // more specific parameter type
    {
        public int P { get { System.Console.Write("ran"); return 42; } }
    }
}
```

```
// Better conversion target
_ = IEnumerable<string>.P; // should prefer IEnumerable<string>

public static class E
{
    extension(IEnumerable<string>)
    {
      static int P => 0;
    }
    extension(IEnumerable<object>)
    {
      static int P => 0;
    }
}
```


```
_ = 42.P;

public static class E
{
    extension<T>(T t)
    {
        public int P => throw null; 
    }
    extension(int i) // non-generic, so better function member
    {
        public int P { get { System.Console.Write("ran"); return 42; } }
    }
}
```

```
_ = 42.P;

public static class E
{
    extension(in int i)
    {
        public int P => throw null; 
    }
    extension(int i) // better parameter-passing mode
    {
        public int P { get { System.Console.Write("ran"); return 42; } }
    }
}
```

Those betterness rules don't necessarily feel right when it comes to static extension methods:
```
1.M();

public static class E1
{
    extension(in int i)
    {
       void M() { }
    }
}

int.M2();
public static class E2
{
    extension(int)
    {
        public static void M() { }
    }
}
```

```
_ = "".M(""); // we want to prefer the `extension(string)`

public static class E
{
    extension(object o)
    {
        public int M(string s) { }
    }
    extension(string s)
    {
        public int M(object o) { }
    }
}
```

TODO2 write a proposal that removes less specific members, and also filters based on better conversion for receiver
