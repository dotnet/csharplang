# Variance Safety for static interface members

## Summary

Allow static, non-virtual members in interfaces to treat type parameters in their declarations as invariant, regardless of their declared variance.

## Motivation


- https://github.com/dotnet/csharplang/issues/3275
- https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-06-24.md#interface-static-member-variance

We considered variance in `static` interface members. Today, for co/contravariant type parameters
used in these members, they must follow the full standard rules of variance, leading to some
inconsistency with the way that `static` fields are treated vs `static` properties or methods:

```cs
public interface I<out T>
{
    static Task<T> F = Task.FromResult(default(T)); // No problem
    static Task<T> P => Task.FromResult(default(T));   //CS1961
    static Task<T> M() => Task.FromResult(default(T));    //CS1961
    static event EventHandler<T> E; // CS1961
}
```

Because these members are `static` and non-virtual, there aren't any safety issues here: you can't
derive a looser/more restricted member in some fashion by subtyping the interface and overriding
the member.

## Detailed Design

Here is the proposed content for Vaiance Safety section of the language specification
(https://github.com/dotnet/csharplang/blob/master/spec/interfaces.md#variance-safety).
The change is the addition of "*These restrictions do not apply to ocurrances of types
within declarations of static members.*" sentence at the beginning of the section. 

### Variance safety

The occurrence of variance annotations in the type parameter list of a type restricts the places where types can occur within the type declaration.
*These restrictions do not apply to ocurrances of types within declarations of static members.*

A type `T` is ***output-unsafe*** if one of the following holds:

*  `T` is a contravariant type parameter
*  `T` is an array type with an output-unsafe element type
*  `T` is an interface or delegate type `S<A1,...,Ak>` constructed from a generic type `S<X1,...,Xk>` where for at least one `Ai` one of the following holds:
   * `Xi` is covariant or invariant and `Ai` is output-unsafe.
   * `Xi` is contravariant or invariant and `Ai` is input-safe.
   
A type `T` is ***input-unsafe*** if one of the following holds:

*  `T` is a covariant type parameter
*  `T` is an array type with an input-unsafe element type
*  `T` is an interface or delegate type `S<A1,...,Ak>` constructed from a generic type `S<X1,...,Xk>` where for at least one `Ai` one of the following holds:
   * `Xi` is covariant or invariant and `Ai` is input-unsafe.
   * `Xi` is contravariant or invariant and `Ai` is output-unsafe.

Intuitively, an output-unsafe type is prohibited in an output position, and an input-unsafe type is prohibited in an input position.

A type is ***output-safe*** if it is not output-unsafe, and ***input-safe*** if it is not input-unsafe.


## Other Considerations

We also considered whether this could potentially interfere with some of the other
enhancements we hope to make regarding roles, type classes, and extensions. These should all be
fine: we won't be able to retcon the existing static members to be virtual-by-default for interfaces,
as that would end up being a breaking change on multiple levels, even without changing the variance
behavior here.
