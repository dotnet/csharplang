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

Re-using the existing type inference algorithm solves the variance problem we'd discussed in LDM.  
It makes this scenario work as desired, because type inference is smarter than the implemented algorithm for extensions:
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

My proposal is that the implementation continue to diverge from the spec: instead of doing 2-phase lookup 
(as described in the section above, where we find compatible substituted extension containers, then find the candidate members in those)
we could do a 1-phase lookup. We would only do this in invocation scenarios.

For such invocation scenarios:
1. we collect all the candidate methods (both classic extension methods and new ones, without excluding any extension containers)
2. we combine all the type parameters and the parameters into a single signature
3. we apply overload resolution to the resulting set

The transformation at step2 would take a method like the following:
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

# Recap

If we accepted both parts of the proposal:
- `instance.Method(...)` would behave exactly the same whether `Method` is a classic or new extension method 
(from an implementation perspective)
- `Type.Method(...)` would behave exactly like the instance scenario
- other scenarios all use the new resolution method where we figure out the compatible substituted extension containers, then collect candidates
- `instance.Property` and `Type.Property` 
- `instance[...]`
(we first figure out the compatible substituted extension container, then do overload resolution with the candidate indexers)
- const, nested type, operators, ...


