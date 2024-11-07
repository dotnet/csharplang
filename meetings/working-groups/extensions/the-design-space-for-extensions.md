# The design space for extensions

Let's put the current proposals for extensions in a broader context, and compare their pros and cons.

## Background: How we got here

The competing approaches and philosophies around how to express the declaration of extension members trace their roots all the way back to when extension methods were first designed. 

### C# 3: The start of extension methods

C# 3 shipped with the extension methods we know today. But their design was not a given: An alternative proposal was on the table which organized extension methods in type-like declarations, each for one specific extended (underlying) type. In this model, extension method declarations would look like instance method declarations, and future addition of other member kinds - even interfaces - would be syntactically straightforward. I cannot find direct references to this first type-based proposal,  but here is a slightly later version from 2009 that was inspired by it:

``` c#
public extension Complex : Point
{
    public Point Scale(double s) { return new Point(X * s, Y * s); }
    public double Size { get { return Math.Sqrt(X*X + Y*Y); } }
    public static Point operator +(Point p1, Point p2) { return new Point(p1.X + p2.X, p1.Y + p2.Y); }
}
```

Ultimately the current design was chosen for several reasons. Importantly it was much simpler: a syntactic hack on top of static methods. C# 3 was already brimming with heavy-duty features - lambdas, expression trees, query expressions, advanced type inference, etc. - so the appetite to go big on extension methods was limited. Moreover, the static method approach came with its own disambiguation mechanism - just call as a static method! - and allowed convenient grouping of extension methods within one static class declaration. The extension methods of `System.Linq.Enumerable` would have needed to be spread across about 15 extension type declarations if they had been split by underlying type.

But perhaps most significantly, we didn't know extension methods were going to be such a hit. There was a lot of skepticism in the community, especially around the risks of someone else being able to add members to your type. The full usefulness of the paradigm was not obvious even to us at the time; mostly we needed them for the query scenario to come together elegantly. So betting on them as a full-fledged new feature direction felt like a risky choice. Better to keep them a cheap hack to start with.

### C# 4: Foundered attempts at extension members

Of course extension methods _were_ a huge success in their own right, and the community was immediately asking for more; especially extension properties and extension interfaces. The LDM went to work on trying to generalize to all member kinds, but felt captive to the choices made in C# 3. We felt extension members would have to be a continuation, not just philosophically but _syntactically_, of the extension methods we'd shipped. For instance, extension properties would have to either use property syntax and take an extra `this` parameter somehow, or we'd need to operate at the lowered level of `set` and `get` methods representing the accessors of properties. Here is an example from 2008:

``` c#
public extension E
{
    public static string Name<T>(this Foo<T> myself){ get { … } set { … } }
    public static V this<K,V>(this Dict<K,V> dict)[K index] { get { … } }
    public static event Handler<MyArgs> OnExplode<T>(this Foo<T> it) { 
    	add { … }
    	remove { … }
    }
    public static operator + (BigInteger i, Complex c) { … }
    public static implicit operator Complex(BigInteger i) { … }
}
```

These explorations led to proposals of unbearable complexity, and after much design and implementation effort they were abandoned. At the time we were not ready to consider rebooting extensions with an alternative syntax, one that would leave the popular classic extension methods behind as a sort of legacy syntax.

### The return of type-based extensions

The Haskell programming language has _type classes_, which describe the relationships within groups of types and functions, and which, crucially, can be applied after the fact, without those types and functions participating. A proposal from Microsoft Research in Cambridge for adding type classes to C# triggered a string of proposals that eventually led back to extension interfaces: If extension members could somehow help a type implement an interface without the involvement of that type, this would facilitate similar adaptation capabilities to what type classes provide in Haskell, and would greatly aid software composition.

Extension interfaces fit well with the old alternative idea that extensions were a form of type declaration, so much so that we ended up with a grand plan where extensions were types, and where such types would even be a first class feature of their own - separate from the automatic extension of underlying types - in the form of _roles_.

This approach ran into several consecutive setbacks: We couldn't find a reasonable way to represent interface-implementing extensions in the runtime. Then the implementation of the "typeness" of extensions proved prohibitively expensive. In the end, the proposal had to be pared back to something much like the old alternative design from above: extensions as type declarations, but with no "typeness" and no roles. Here's a recent 2024 example:

``` c#
extension E for C
{
    // Instance members
    public string P { get => f; set => f = value.Trim(); }         // Property
    public T M<T>() where T : IParsable<T> => T.Parse(f, default); // Method
    public char this[int index] => f[index];                       // Indexer
    public C(string f) => this.f = f;                              // Constructor

    // Static members
    public static int ff = 0;                                // Static field
    public static int PP { get; set => field = Abs(value); } // Static property
    public static C MM(string s) => new C(s);                // Static method
    public static C operator +(C c1, C c2) => c1.f + c2.f;   // Operator
    public static implicit operator C(string s) => new C(s); // UD conversion

}
```

We will refer to the resulting flavor of design as "_type-based extensions_", because the underlying type of the extension is specified on the extension type itself, and the members are just "normal" instance and static member declarations, including providing access to the underlying value with the `this` keyword rather than a parameter.

### The return of member-based extensions

Now that the bigger story of extensions as types with interfaces has been put on hold with its future prospects in question, it is worth asking: Are we still on the right syntactic and philosophical path? Perhaps we should instead do something that is more of a continuation of classic extension methods, and is capable of bringing those along in a compatible way.

This has led to several proposals that we will collectively refer to as "_member-based extensions_". Unlike most of the abandoned C# 4 designs of yore, these designs do break with classic extension methods _syntactically_. Like the type-based approach they embrace an extension member declaration syntax that is based on the corresponding instance member declaration syntax from classes and structs. However, unlike type-based extensions, the underlying type is expressed at the member level, using new syntax that retains more characteristics of a parameter.

Here are a few examples from [this recent proposal](https://github.com/dotnet/csharplang/pull/8525):

``` c#
public partial extensions Extensions
{
    public SourceTextContainer (ITextBuffer buffer).AsTextContainer()
        => TextBufferContainer.From(buffer);

    internal TextLine (ITextSnapshotLine line).AsTextLine()
        => line.Snapshot.AsText().Lines[line.LineNumber];
}
internal extensions IComparerExtensions<T> for IComparer<T>
{
    public IComparer<T> comparer.Inverse => new InverseComparer<T>(comparer)
}
```

The motivation is not just a closer philosophical relationship with classic extension methods: It is an explicit goal that existing classic extension methods can be ported to the new syntax in such a way that they remain source and binary compatible. This includes allowing them to be called as static methods, when their declarations follow a certain pattern.

We've had much less time to explore this approach. There are many possible syntactic directions, and we are just now beginning to tease out which properties are inherent to the approach, and which are the result of specific syntax choices. Which leads us to the following section, trying to compare and contrast the two approaches.

## Comparing type-based and member-based proposals

Both approaches agree on a number of important points, even as the underlying philosophy differs in what currently feels like fundamental ways:

- __Member syntax:__ In both approaches the member declaration syntax is based on the corresponding instance member declaration syntax. They may be adorned or modified in different ways, but neither attempts to use the naked static method syntax of classic extension methods, or otherwise embrace the lowered form in declaration syntax.
- __Type syntax:__ Both also introduce a new form of type declaration (with the keyword `extension` or `extensions`) to hold extension member declarations. Neither approach keeps extension members in static classes.
- __Abstraction:__ Both generally hide the low-level representation of the declaration from language-level use (with one exception in the member-based approach for compatibility purposes). This means that both have the same need for a disambiguation mechanism to replace classic extension methods' ability to be called directly as static methods.
- __Lookup:__ Both are amenable to pretty much the same range of design options regarding extension member lookup, inference of type arguments, and overload resolution.

And of course both approaches share the same overarching goal: to be able to facilitate extension members of nearly every member kind, not just instance methods. Either now or in the future this may include instance and static methods, properties, indexers, events, operators, constructors, user-defined conversions, and even static fields. The only exception is members that add instance state, such as instance fields, auto-properties and field-like events.

The similarities make it tempting to search for a middle ground, but we haven't found satisfactory compromise proposals (though [not for lack of trying](https://github.com/dotnet/csharplang/issues/8519)). Most likely this is because the differences are pretty fundamental. So let's look at what divides the two approaches.

### Relationship to classic extension methods

The core differentiating factor between the two approaches is how they relate to classic extension methods.

In the member-based approach, it is a key goal that existing classic extension methods be able to migrate to the new syntax with 100% source and binary compatibility. This includes being able to continue to call them directly as static methods, even though they are no longer directly declared as such. A lot of design choices for the feature flow from there: The underlying type is specified in the style of a parameter, including parameter name and potential ref-kinds. The body refers to the underlying value through the parameter name.

Only instance extension methods declared within a non-generic `extensions` declaration are compatible and can be called as static methods, and the signature of that static method is no longer self-evident in the declaration syntax.

The type-based approach also aims for comparable expressiveness to classic extension methods, but without the goal of bringing them forward compatibly. Instead it has a different key objective, which is to declare extension members with the same syntax as the instance and static members they "pretend" to be, leaving the specification of the underlying type to the enclosing type declaration. This "thicker" abstraction cannot compatibly represent existing classic extension methods. People who want their existing extension methods to stay fully compatible can instead leave them as they are, and they will play well with new extension members.

While the type-based approach looks like any other class or struct declaration, this may be deceptive and lead to surprises when things don't work the same way.

The member-based approach is arguably more contiguous with classic extension methods, whereas the type-based approach is arguably simpler. Which has more weight?

### Handling type parameters

An area where the member-based approach runs into complexity is when the underlying type is an open generic type. We know from existing extension methods that this is quite frequent, not least in the core .NET libraries where about 30% of extension methods have an open generic underlying type. This includes nearly all extension methods in `System.Linq.Enumerable` and `System.MemoryExtensions`.

Classic extension methods facilitate this through one or (occasionally) more type parameters on the static method that occur in the `this` parameter's type:

``` c#
public static class MemoryExtensions
{
    public static Span<T> AsSpan<T>(this T[]? array);
}
```

The same approach can be used to - compatibly - declare such a method with the member-based approach:

``` c#
public extensions MemoryExtensions
{
    public Span<T> (T[]? array).AsSpan<T>();
}
```

We should assume that open generic underlying types would be similarly frequent for other extension member kinds, such as properties and operators. However, those kinds of member declarations don't come with the ability to declare type parameters. If we were to declare `AsSpan` as a property, where to declare the `T`?

This is a non-issue for the type-based approach, which always has type parameters and underlying type on the enclosing `extension` type declaration.

For the member-based approach there seem to be two options:

1. Allow non-method extension members to also have type parameters.
2. Allow the type parameter and the specification of the underlying type on the enclosing type.

Both lead to significant complication:

#### Type parameters on non-method extension members

Syntactically we can probably find a place to put type parameters on each kind of member. But other questions abound: Should these be allowed on non-extension members too? If so, how does that work, and if not, why not? How are type arguments explicitly passed to each member kind when they can't be inferred - or are they always inferred?

``` c#
public extensions MemoryExtensions 
{
    public Span<T> (T[]? array).AsSpan<T> { get; }
}
```
This seems like a big language extension to bite off, especially since type parameters on other members isn't really a goal, and current proposals don't go there.

#### Allow type parameters and underlying type to be specified on the enclosing type declaration

If the enclosing `extensions` type declaration can specify type parameters and underlying type, that would give members such as properties a place to put an open generic underlying type without themselves having type parameters:

``` c#
public extensions MemoryExtensions<T> for T[]?
{
    public Span<T> (array).AsSpan { get; }
}
```

This is indeed how current member-based proposals address the situation. However, this raises its own set of complexities:

1. Classic extension methods are purely method-based, and the enclosing static class is just a plain container contributing nothing but its name. Here, though, the enclosing `extensions` declaration starts carrying crucial information for at least some scenarios.
2. There are now two distinct ways of providing the underlying type for an extension member, and figuring out which to use becomes a bit of a decoder-ring situation:
    - For a compatible port of a classic extension method, the underlying type and any type parameters must be on the _member_.
    - For non-method extension members with an open generic underlying type, the underlying type and the type parameters must be on the enclosing  _type_.
    - For extension methods that do not need to be compatible and have an open generic underlying type, the underlying type and the type parameters can be specified _either_ on the method or the type, but the generated code will be incompatible between the two.
    - For extension members with a closed underlying type, the underlying type can be defined _either_ on the method or the type, and the two are interchangeable.
3. Once an `extensions` declaration specifies an underlying type, it can no longer be shared between extension members with different underlying types. The grouping of extension members with different underlying types that is one of the benefits of the member-based approach doesn't actually work when non-method extension members with open generic underlying types are involved: You need separate `extensions` declarations with separate type-level underlying types just as in the type-based approach!

``` c#
public extensions ArrayMemoryExtensions<T> for T[]?
{
    public Span<T> (array).AsSpan { get; }
}
public extensions ArraySegmentMemoryExtensions<T> for ArraySegment<T>?
{
    public Span<T> (segment).AsSpan { get; }
}
```

In summary, classic extension methods rely critically on static methods being able to specify both parameters and type parameters. A member-based approach must either extend that capability fully to other member kinds, or it must partially embrace a type-based approach.

### Tweaking parameter semantics

An area where the type-based approach runs into complexity is when the default behavior for how the underlying value is referenced does not suffice, and the syntax suffers from not having the expressiveness of "parameter syntax" for the underlying value.

This is a non-issue for the member-based approach, which allows all this detail to be specified on each member.

There are several kinds of information one might want to specify on the underlying value:

#### By-ref or by_value for underlying value types

In classic extension methods, the fact that the `this` parameter _is_ a parameter can be used to specify details about it that real instance methods don't get to specify about how `this` works in their body. By default, `this` parameters, like all parameters, are passed by value. However, if the underlying type is a value type they can also be specified as `ref`, `ref readonly` and `in`. The benefit is to avoid copying of large structs and - in the case of `ref` - to enable mutation of the receiver itself rather than a copy.

The use of this varies wildly, but is sometimes very high. Measuring across a few different libraries, the percentage of existing extension methods on value types that take the underlying value by reference ranges from 2% to 78%!

The type-based approach abstracts away the parameter passing semantics of the underlying value - extension instance members just reference it using `this`, in analogy with instance members in classes and structs. But classes and structs have different "parameter passing" semantics for `this`! In classes `this` is by-value, and in structs `this` is by `ref` - or `ref readonly` when the member or struct is declared `readonly`.

There are two reasonable designs for what the default should be for extension members:

1. Follow classes and structs: When the underlying type is a reference type, pass `this` by value, and when it is a value type pass `this` by `ref` (or perhaps `ref readonly` when the member is `readonly`). In the rare case (<2%) that the underlying type is an unconstrained type parameter, decide at runtime.
2. Follow classic extension methods: Always pass `this` by value.

Either way, the default will be wrong for some significant number of extension members on value types! Passing by value prevents mutation. Passing by reference is unnecessarily expensive for small value types.

In order to get to reasonable expressiveness on this, the type-based approach would need to break the abstraction and get a little more "parameter-like" with the underlying type. For instance, the `for` clause might optionally specify `ref` or `in`:

``` c#
public extension TupleExtensions for ref (int x, int y)
{
    public void Swap() => (x, y) = (y, x); // `this` is by ref and can be mutated
    public readonly int Sum => x + y; // `this` is ref readonly and cannot me mutated
}
```

#### Attributes

This-parameters can have attributes. It is quite rare (< 1%), and the vast majority are nullability-related. Of course, extension members can have attributes, but they would need a way to specify that an attribute goes on the implicit `this` parameter! 

One way is to introduce an additional attribute target, say `this`, which can be put on instance extension members:

``` c#
[this:NotNullWhen(false)] public bool IsNullOrEmpty => this is null or [];
```

#### Nullable reference types

A classic extension method can specify the underlying type as a nullable reference type. It is fairly rare (< 2%) but allows for useful scenarios, since, unlike instance members, extension members can actually have useful behavior when invoked on null. Anotating the receiver as nullable allows the extension method to be called without warning on a value that may be null, in exchange for its body dealing with the possibility that the parameter may be null.

A type-based approach could certainly allow the `for` clause to specify a nullable reference type as the underlying type. However, not all extension members on that type might want it to be nullable, and forcing them to be split across two `extension` declarations seems to break with the ideal that nullability shouldn't have semantic impact:

``` c#
public extension NullableStringExtension for string?
{
    [this:NotNullWhen(false)] public bool IsNullOrEmpty => this is null or [];
}
public extension StringExtension for string
{
    public string Reverse() => ...;
}
```

It would be better if nullability could be specified at the member level. But how? Adding new syntax to members seems to be exactly what the type-based approach is trying to avoid! The best bet may be using an attribute with the `this` target as introduced above:

``` c#
public extension StringExtension for string
{
    [this:AllowNull][this:NotNullWhen(false)] public bool IsNullOrEmpty => this is null or [];
    public string Reverse() => ...;
}
```

This would allow extension members on nullable and nonnullable versions of the same underlying reference type to be grouped together.

### Grouping

Classic extension methods can be grouped together in static classes without regard to their underlying types. This is not the case with the type-based approach, which requires an `extension` declaration for each underlying type. Unfortunately it is also only partially the case for the member-based approach, as we saw above. Adding e.g. extension properties to `MemoryExtensions`, which has a lot of open generic underlying types, would lead to it having to be broken up into several `extensions` declarations.

This is an important quality of classic extension methods that unfortunately neither approach is able to fully bring forward.

### Non-extension members

Current static classes can of course have non-extension static members, and it is somewhat common for those to co-exist with extension methods.

In the member-based approach a similar thing should be easy to allow. Since the extension members have special syntactic elements, ordinary static members wouldn't conflict.

In the type-based approach, ordinary member syntax introduces extension members! So if we want _non_-extension members that would have to be accommodated specially somehow.

### Interface implementation

The type-based syntax lends itself to a future where extensions implement interfaces on behalf of underlying types.

For the member-based syntax that would require more design.

## Summary

All in all, both approaches have some challenges. The member-based approach struggles with open generic underlying types, which are fairly common. They can be addressed in two different ways, both of which add significant syntax and complexity. 

The type-based approach abstracts away parameter details that are occasionally useful or even critical, and would need to be augmented with ways to "open the lid" to bring that expressiveness forward from classic extension methods when needed.

