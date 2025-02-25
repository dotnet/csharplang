# Implicit compatibility for ported extension methods

The new extension syntax introduces a separation between the receiver specification and the member declaration itself. The natural semantics accompanying this leads to several differences from how classic extension methods behave; places where the fact that classic extension methods are really just static methods bleeds through to their behavior.

If we don't address this discrepancy, many classic extension methods will not be compatibly portable to the new syntax, and there will be an observable behavior misalignment between those that aren't ported and extension methods in the new syntax.

There are two strategies for addressing this: Explicit and implicit compat. 

With *explicit* compat, a syntactic marker signals that a given extension method in the new syntax should remain compatible with its corresponding classic declaration, and behavior is suitably adjusted to achieve that.

With *implicit* compat, all extension methods in the new syntax have behavior that makes all (or nearly all) existing consumption code continue to work the same way, even as new behavior is also embraced.

This document pursues **implicit compat**. It looks at each of the behavior discrepancies we know of, and suggests ways to address them.

## Static methods

Classic extension methods are static methods on the enclosing static class, and they may be invoked as such. To achieve implicit compat, a new extension instance method generates a static method that mimics the corresponding classic extension method. It uses:

- the attributes, accessibility, return type, name and body of the declared instance extension method,
- a type parameter list concatenated from the type parameters of the extension declaration and the extension method, in that order, and
- a parameter list concatenated from the receiver parameter of the extension declaration and the parameter list of the extension method, in that order.

``` c#
public static class Enumerable
{
    extension<TSource>(IEnumerable<TSource> source)
    {
        public IEnumerable<TSource> Where(Func<TSource, bool> predicate) { ... }
        public IEnumerable<TSource> Select<TResult>(Func<TSource, TResult> selector)  { ... }
    }
}
```

Generates

``` c#
public static class Enumerable
{
    public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) { ... }
    public static IEnumerable<TSource> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)  { ... }
}
```

## Type arguments

When type arguments are explicitly given to a classic extension method, they must correspond to all the method's type parameters, including those that are used in the receiver type. This may make sense when the classic extension method is invoked as a static method, but it is not a great experience when it is called as an extension method. By contrast, with new extensions any type parameters on the extension declaration are inferred from the receiver, and type arguments on invocation correspond only to those declared on the extension method itself.

It is not uncommon for classic extension methods to have type parameters both for use in the receiver parameter and in subsequent parameters or return types. An example is `System.Linq.Enumerable.Select` where a rewrite to new syntax would put `TSource` on the extension declaration, and `TResult` on the method declaration.

It is also not that uncommon for explicit type arguments to be given. A rough GitHub code search suggests that 1.3% of `Select` calls do pass type arguments explicitly. So this is a significant existing scenario.

With implicit compat, both "versions" of the type argument list are allowed. 

This could introduce ambiguities if there are overloads of the extension method with different number of type parameters. That situation is not uncommon in e.g. `System.Linq.Enumerable` (e.g. `SelectMany`) or `System.MemoryExtensions` (e.g. `Contains`). However, those overloads do seem to be distinguishable by parameter list. This is not surprising, since they would have been authored to not clash in the common case where type arguments are inferred. Thus, the scenario for true ambiguity seems very limited in practice.

We need to make a determination as to whether we believe significant code exists that currently relies on generic arity to disambiguate extension methods. Based on that we can choose to either take a breaking change or introduce some sort of preference system, where the "classic" arities win over the new "method-only" ones.

Given:
``` c#
public static class Enumerable
{
    extension<TSource>(IEnumerable<TSource> source)
    {
        public IEnumerable<TSource> Select<TResult>(Func<TSource, TResult> selector)  { ... }
    }

```

The call `myList.Select<int, string>(...)` would provide type arguments for `TSource` and `TResult`, foregoing the separate inference of `TSource` from the type of `myList`.

The call `myList.Select<string>(...)` would provide a type argument for `TResult` in the above `Select` method, with `TSource` being inferred from the type of `myList`.

Given that it is non-breaking (enough), we could "backport" this behavior to existing extension methods as well.

## Overload resolution

Classic extension methods get excluded from consideration if there isn't an identity, reference or boxing conversion from the receiver to the this-parameter. However, after that point, the receiver gets treated as just yet another argument in determining which method overload wins. This can lead to ambiguities such as this:

``` c#
"Hello".M("World!"); // Ambiguous!

public static class MyExtensions
{
    public static void M(this object o, string s) { ... }
    public static void M(this string s, object o) { ... }
}
```

For new extension methods, it seems much more in line with expectations that applicable methods "on" more specific types shadow (and thus eliminate) applicable methods "on" base types. After all, that's how lookup works in type hierarchies: As soon as we find an applicable method, we look no further up the chain.

It seems such elimination would lead to _fewer_ ambiguities, without causing different results when overload resolution does succeed. Thus it wouldn't be breaking behavior for ported classic extension methods. This claim needs to be investigated for counterexamples of course.

``` c#
"Hello".M("World!"); // Picks string.M(object) because receiver is more specific

public static class MyExtensions
{
    extension(object o)
    {
        publicvoid M(string s) { ... }
    }
    extension(string s)
    {
        public void M(object o) { ... }
    }
}
```

Given that it is non-breaking, we could "backport" this behavior to existing extension methods as well.

## Type inference

When type arguments are inferred for a given classic extension method, any argument may impact the inference of any type parameter. By contrast, with new extensions, type arguments for the extension declaration type parameters are inferred from the receiver, whereas arguments to the extension method may only impact type arguments for the extension method's own type parameters.

While we can construct examples where this makes a difference, we have not yet encountered such examples in the wild. If we find this to be very rare, we may choose not to do anything to mitigate it.

If we _do_ choose to address it, we would continue to use classic inference for extension methods, lumping in the type parameters and method parameters from both the extension declaration and extension method. For modern usage, this would be unlikely to produce observably different results; only slightly fewer errors. However, it would allow any occurrences of this pattern to continue to compile as well.

``` c#
void M(I<string> i, out object o)
{
    i.M1(out o); // infers E.M1<object>
    i.M2(out o); // error CS1503: Argument 1: cannot convert from 'out object' to 'out string'
    i.M3(out o); // infers E.M3<object>
}

public static class E
{
   public static void M1<T>(this I<T> i, out T t) { ... }
   extension<T>(I<T> i)
   {
      public void M2(out T t) { ... }
   }
   extension<T>(I<T> i)
   {
      public void M3(out T t) { ... }
   }
}
public interface I<out T> { }
```

## Other differences

There are some more special discrepancies between instance methods and classic extension methods, which warrant explicit design decisions for the new extension syntax.

### Out-of-order type parameters

Classic extension methods can have type parameters that do not occur in the receiver type precede ones that do in the type parameter list. There is no direct way to port such an extension method compatibly to the new syntax. We do not know of examples of this in the wild, and the best way forward is probably to accept that such methods, should they exist, will have to stay in classic syntax in order to remain fully compatible.

### InterpolatedStringHandlerArgumentAttribute

In instance methods this attribute can use the empty string to denote the name of the receiver. This doesn't currently work for classic extension methods. Should it work for new extension methods? Should it also be made to work for old ones? In both cases the receiver already has a name, as it is expressed as a parameter.

### CallerArgumentExpressionAttribute

This attribute cannot be used to refer to the receiver in instance methods. However, in classic extension methods it can, because the receiver is expressed as a parameter. For implicit compat it should remain able to do so in the new extension method syntax.

## Next steps

There are several open questions, assumptions about existing code and lacking details in this proposal. If LDM approves of the direction, we need to drill down on the details through spec and implementation, and validate our assumptions about potential for breaks.