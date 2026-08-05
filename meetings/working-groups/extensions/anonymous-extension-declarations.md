# Anonymous extension declarations

## Summary

This design draws on many recent proposals, and is derived based on a set of assumptions about the kind of feature that would be successful with developers. It places extension member declarations within anonymous extension declarations that specify underlying type and accompanying type parameters, and which are in turn nested within non-generic static classes.

``` c#
public static class Enumerable
{
    // Extension members for IEnumerable
    extension(IEnumerable)
    {
        // 'this' refers to underlying value
        public bool IsEmpty => !this.GetEnumerator().MoveNext();
    }
    // Extension members for IEnumerable<T>
    extension<T>(IEnumerable<T>)
    {
        public IEnumerable<T> Where(Func<T, bool> predicate) { ... }
        public IEnumerable<TResult> Select<TResult>(Func<T, TResult> selector) { ... }
        public static IEnumerable<T> operator +(IEnumerable<T> e1, IEnumerable<T> e2) { ... }
    }
    
    // Classic extension method
    public static IEnumerable<TResult> Cast<TResult>(this IEnumerable source) { ... }
    
    // Non-extension member
    public static IEnumerable<int> Range(int start, int count) { ... } 

}
```

## Motivation

This design starts from a set of assumptions about what matters more or less to users of the feature, deriving a design that meets them. Those assumptions, though gathered from the criticisms of both "type-based" and "member-based" proposals, are certainly debatable and won't all be agreed upon. They represent the beliefs and opinions of the author. Let's go through them below, along with a sneak peek at how they drive the design.

### Type names and grouping

* Developers want to be able to group a collection of related extension members under just one type name, even if they span multiple different underlying types. People would resent having to come up with multiple separate type names for different underlying types, and having to navigate those many names for disambiguation, `using static` inclusion etc.
* Separate type names for different receiver types won't be necessary for disambiguation - they aren't today.

This implies an overarching grouping entity that carries no inherent information beyond simply a type name. The proposed design reuses top-level non-generic static classes for this purpose.

### Relationship to classic extension methods

* People would like to be able to place their new extension members together with their existing extension methods.
* People do appreciate being able to declare non-extension static members next to extension declarations.
* People are better off leaving their classic extension methods as static method declarations so that the static invocation signature is obvious, and to avoid churn and risk.
* People will, however, expect lookup to work very closely similar between old and new extension methods.

Because the proposed design keeps using non-generic static classes as the overarching grouping entity, new extension declarations can sit side by side with old extension methods and non-extension static members. However, new extension methods do not work exactly the same as old ones. (Although do see the "Alternatives" section).

### Type parameters and underlying types

* Underlying types belong together with their type parameter declarations, and it would be confusing to separate them.
* People will resent the verbosity of having to repeat underlying types and accompanying type parameters for each member.
* Sometimes there's a need to specify different parameter details (ref-ness, nullability, ...) for the same underlying type.

The design uses anonymous extension declarations within static classes. These anonymous extension declarations specify type parameters, underlying type and any parameter details for the extension member declarations they contain.

### Member declarations

* Extension member declarations should look identical to corresponding member declarations in classes and structs.
* Parameter names for underlying values aren't important and people will resent the forced verbosity of having to specify them.

The design keeps extension members with the same syntax as their class and struct counterparts, including using `this` to refer to the underlying value.

### Regularity

* Rules and syntax should be consistent across all different member kinds, generic arities etc.
* Implementation limitations or compatibility constraints shouldn't be materially felt in the user level feature experience.
* There should be just one right way to declare a given extension member.

The design has exactly one syntactic location for each of the extension type name, underlying types with accompanying type parameters, additional parameter info and the extension members themselves.

### Efficiency

* Extension declarations shouldn't result in an unnecessarily large number of types being generated.
* Extension member invocations should not incur hidden penalties, allocations or copying.

The design represents anonymous extension declaration as nested static classes which are merged as much as generic arity and constraints allow. Extension members are represented as static members with an extra parameter for the underlying value, using any additional parameter info in that parameter's declaration. Extension member invocation is just invocation of those static members.

## Detailed design

### Declaration
Extensions are declared inside top-level non-generic static classes, just like extension methods today, and can thus coexist with classic extension methods and non-extension static members:

``` c#
public static class Enumerable
{
    // New extension declaration
    extension(IEnumerable) { ... }
    
    // Classic extension method
    public static IEnumerable<TResult> Cast<TResult>(this IEnumerable source) { ... }
    
    // Non-extension member
    public static IEnumerable<int> Range(int start, int count) { ... } 
}
```

An extension declaration is anonymous, and provides a specification of an underlying type with any associated type parameters and ref kinds, followed by a set of extension member declarations:

``` c#
public static class Enumerable
{
    extension(IEnumerable) // extension members for IEnumerable
    {
        public bool IsEmpty { get { ... } }
    }
    extension<T>(IEnumerable<T>) // extension members for IEnumerable<T>
    {
        public IEnumerable<T> Where(Func<T, bool> predicate) { ... }
        public IEnumerable<TResult> Select<TResult>(Func<T, TResult> selector) { ... }
        public static IEnumerable<T> operator +(IEnumerable<T> e1, IEnumerable<T> e2) { ... }
    }
}
```

The extension member declarations are syntactically identical to corresponding instance and static members in class and struct declarations. Instance members refer to the underlying value with the keyword `this`:

``` c#
public static class Enumerable
{
    extension(IEnumerable)
    {
        // 'this' refers to underlying value
        public bool IsEmpty => !this.GetEnumerator().MoveNext();
    }
}
```

As in instance members, the reference to `this` can be implicit:

``` c#
public static class Enumerable
{
    extension(IEnumerable)
    {
        // implicit 'this.GetEnumerator()'
        public bool IsEmpty => !GetEnumerator().MoveNext();
    }
}
```

By default, the underlying value is passed to instance extension members by value, but an extension declaration can explicitly specify a different ref kind, as long as the underlying type is known to be a value type:

``` c#
public static class Bits
{
    extension(ref ulong) // underlying value is passed by ref
    {
        public bool this[int index]
        {
            get => (this & Mask(index)) != 0;
            set => this = value ? this | Mask(index) : this & ~Mask(index); // mutates underlying value
        }
    }
    static ulong Mask(int index) => 1ul << index;
}
```

Underlying types in extension declarations can be or contain nullable reference types. If underlying types vary by nullability, separate extension declarations are needed.

Individual extension members can place attributes on the implicit `this` parameter by using the `param` target in an attribute placed on the member itself. `param` is not currently allowed in this position, but is already used elsewhere in C# to refer to implicit parameters:

``` c#
public static class NullableExtensions
{
    extension(string?)
    {
        public string AsNotNull => this is null ? "" : this;
        [param:NotNullWhen(false)] public bool IsNullOrEmpty => this is null or [];
    }
    extension<T> (T) where T : class?
    {
        [param:NotNull] public void ThrowIfNull() => ArgumentNullException.ThrowIfNull(this);
    }
}
```

### Lowering

Extension declarations give rise to static classes nested within the enclosing static class. These inner static classes are generated with unspeakable names and do not pollute the namespace of the enclosing static class. 

Within a given enclosing static class, extension declarations with the same number of type parameters and equivalent constraints are merged into a single static class even if they differ on underlying type, ref-kind or type parameter names. Thus the minimum number of nested classes are generated to represent the combinations of type parameters and constraints present across all the extension declarations within the enclosing static class:

``` c#
public static class MyExtensions
{
    extension<TSource>(Span<TSource>) where TSource : class?
    {
        ...
    }
    extension<TElement>(Span<TElement?>) where TElement : class
    {
        ...
    }
}
```

Because the signatures are sufficiently equivalent, this is lowered to just one class:

``` c#
public static class MyExtensions
{
    public static class __E1<__T1> where __T1 : class
    {
        ...
    }
}
```

The requirement is not unlike that between the two parts of a partial method in today's C#.

The members themselves are generated as static members. For instance members, insofar as it is possible to represent in IL, the same kind of member is generated, but with an extra first parameter for the underlying value. Where not possible, the body is represented as one or two (for accessors) static methods, and attributes are emitted to record their original member kind.

The generated first parameter includes any ref kind specified in the extension declaration, and any attributes on the member which have the `param` target specifier:

``` c#
public static class NullableExtensions
{
    extension<T> (T) where T : class?
    {
        [param:NotNull] public void ThrowIfNull() => ArgumentNullException.ThrowIfNull(this);
    }
}
```

Generates:

``` c#
public static class NullableExtensions
{
    public static class __E1<__T1> where T1__ : class
    {
        public static void ThrowIfNull([NotNull] __this) => ArgumentNullException.ThrowIfNull(__this);
    }
}
```

### Checking

__Inferrability:__ The underlying type of an extension declaration must make use of all the type parameters of that extension declaration, so that it is always possible to infer the type arguments when applied to the underlying type.

__Uniqueness:__ Within a given enclosing static class, the set of extension member declarations with the same underlying type (modulo identity conversion and type parameter name substitution) are treated as a single declaration space similar to the members within a class or struct declaration, and are subject to the same [rules about uniqueness](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#153-class-members).

The uniqueness rule also applies across classic extension methods within the same static class, where any type parameter list is used unchanged, the `this` parameter is used to determine the underlying type, and the remaining parameters are used for the method signature.

``` c#
public static class MyExtensions
{
    extension<T1>(IEnumerable<int>) // Error! T1 not inferrable
    {
        ...
    }
    extension<T2>(IEnumerable<T2>)
    {
        public bool IsEmpty { get ... }
    }
    extension<T3>(IEnumerable<T3>?)
    {
        public bool IsEmpty { get ... } // Error! Duplicate declaration
    }
}
```

### Lookup and disambiguation

When an extension member lookup is attempted, all extension declarations within static classes that are `using`-imported contribute their members as candidates, regardless of underlying type. Only as part of resolution are candidates with incompatible underlying types discarded. A full generic type inference is attempted between the type of a receiver and any type parameters in the underlying type.

The inferrability and uniqueness rules mean that the name of the enclosing static type is sufficient to disambiguate between extension members on a given underlying type. As a strawman, consider `E @ T` as a disambiguation syntax meaning on a given expression `E` begin member lookup for an immediately enclosing expression in type `T`. For instance:

``` c#
string[] strings = ...;
var query  = (strings @ Enumerable).Where(s => s.Length > 10);
 
public static class Enumerable
{
    extension<T>(IEnumerable<T>)
    {
        public IEnumerable<T> Where(Func<T, bool> predicate) { ... }
    }
}
```

Means lookup `Where` in the type `Enumerable` with `strings` as its underlying value. A type argument for `T` can now be inferred from the type of `strings` using standard generic type inference.

A similar approach also works for types: `T1 @ T2` means on a given type `T1` begin static member lookup for an immediately enclosing expression in type `T2`.

This disambiguation approach should work not only for new extension members but also for classic extension methods.

Note that this is not a proposal for a specific disambiguation syntax; it is only meant to illustrate how the inferrability and uniqueness rules enable disambiguation without having to explicitly specify type arguments for an extension declaration's type parameters.

## Drawbacks

## Alternatives

### Avoiding nesting for simple cases

The proposed design avoids a lot of repetition, but does end up with extension members being nested two-deep in a static class _and_ and extension declaration.

Two kinds of short-hand syntax are possible:

__Merge static class and extension declarations:__ When a static class contains only a singe extension declaration and nothing else, allow it to be abbreviated to a top-level extension declaration _with_ a name:

``` c#
public extension(IEnumerable) Enumerable
{
    public bool IsEmpty => !GetEnumerator().MoveNext();
}
```

This ends up looking more like what we've been calling a "type-based" approach, where the container for extension members is itself named. However, the nesting would still be applied in the generated output.

__Merge extension declaration and extension member:__ When an extension declaration contains only one member, allow the `{ ... }` curlies to be omitted around that member:

``` c#
public static class Bits
{
    extension(ref ulong) public bool this[int index]
    {
        get => (this & Mask(index)) != 0;
        set => this = value ? this | Mask(index) : this & ~Mask(index); // mutates underlying value
    }
    static ulong Mask(int index) => 1ul << index;
}
```
``` c#
public static class Enumerable
{
    extension<T>(IEnumerable<T>) public IEnumerable<T> Where(Func<T, bool> predicate) { ... }
}
```

This ends up looking more like what we've been calling a "member-based" approach, where each extension member contains its own details about the underlying type. However, in the generated output the type parameter and underlying type would still be applied to a generated nested class, not the member itself.

### Embracing compatibility

Allowing full compatibility for existing extension methods to be ported to new syntax was an explicit non-goal for this proposal, but in the end it does come quite close to being able to embrace such compatibility. an instance extension method in this syntax has everything it needs to generate an equivalent classic extension method - except a parameter name for the `this` parameter. Why is this necessary? Because existing callers of the extension method as a static method may pass the `this` argument by name! 

``` c#
public static class Enumerable
{
    extension<T>(IEnumerable<T>)
    {
        [GenerateStaticMethod("source")]
        public IEnumerable<T> Where(Func<T, bool> predicate) 
        { 
            foreach (var e in this){ ... }
        }
        [GenerateStaticMethod("source")]
        public IEnumerable<T> Select<TResult>(Func<T, TResult> selector) 
        { 
            foreach (var e in this){ ... }
        }
    }
}
```

Type parameters from the extension declaration and the method declaration would be concatenated. In principle there are some classic extension methods that couldn't be expressed like this: ones where the type parameters used in the underlying type aren't first in the type parameter list:

``` c#
public static IEnumerable<TResult> WeirdSelect<TResult, TSource>(
    this IEnumerable<TSource> source,
    Func<TSource, TResult> selector) 
{ ... }
```

I don't think I've ever seen an extension method like that! But any such method would have to stay in the classic syntax.

### Member-based lowering strategy

The proposed lowering strategy merges extension declarations into anonymous static classes under the hood. This helps reduce the amount of transformation that needs to happen on individual members - properties can stay properties, and so on.

However, it does lead to challenges:
- How do we generate stable names for these anonymous classes?
- This would be the first time we generate "unspeakable" public types. How does the ecosystem manage that - other languages, documentation, etc.?

As an alternative, we could generate extension members as static methods directly on the enclosing static class. Any member from a generic extension declaration would need to be turned into one or two generic methods with mangled names, and metadata would need to be generated to track how it all belongs together in extension declarations. All in all, members would be much more heavily transformed, but we would avoid the extra layer of anonymous static classes.

## Unresolved questions

- How stable can we make the generated names of merged static classes, and how much does it matter?
