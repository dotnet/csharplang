# Modern Extensions

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

Many thanks to those who helped with this proposal.  Esp. @jnm2!

## Summary
[summary]: #summary

Modern Extensions introduce a new syntax to produce "extension members", greatly expanding on the set of supported members including properties, static methods, operators and constructors (and more), in a clean and cohesive fashion.

This new form subsumes C# 3's "classic extension methods", allowing migration to the new modern form in a semantically *identical* fashion (both at a source and ABI level).

Note: this proposal is broken into two parts.  A core kernel needed to initially ship with perfect forward and backwards compatibility with classic extension methods, and then potential enhancements on top of this kernel to make certain common designs more succinct and pleasant.  This does not preclude the feature shipping with any or all of these enhancements at launch.  It simply allows the design to be broken into separable concerns, with a clearer ordering of dependencies.

A rough strawman of the syntax is as follows.  In all cases, the extended type is shown to be generic, to indicate handling that complex case:

Note: the strawman is not intended to be final form.  That said, it is useful to see any proposed form with all members to ensure that it's generally comprehensible.  For example, a form that is only good for properties, but not for other members, it likely not appropriate.

```c#
extension E
{
    // Instance method form, replaces `public static int M<X>(this SomeType<X> val, ...) { } 
    public int M<X>(...) for SomeType<X> val { }

    // Property form:
    // Auto-properties, or usage of 'field' not allowed as extensions do not have instance state.
    public int Count<X> for SomeType<X> val { get { ... } }

    // Event form:
    // Note: would have to be the add/remove form.
    // field-backed events would not be possible as extensions do not have instance state.
    public event Action E<X> for SomeType<X> val { add { } remove { } }

    // Indexer form:
    public int this<T>[int index] for SomeType<X> val { get { ... } }

    // Operator form:
    // note: no SomeType<X> val, an operator is static, so it is not passed an instance value.
    public static SomeTypeX<X> operator+ <X>(SomeType<X> s1, SomeType<X> s2) for SomeType<X> { ... }
    
    // Conversion form:
    // note: no SomeType<X> val, an operator is static, so it is not passed an instance value.
    public static implicit operator SomeTypeX<X>(int i) for SomeType<X> { ... }

    // Constructor form:
    // note: no SomeType<X> val, an operator is static, so it is not passed an instance value.
    public SomeType<X>() for SomeType<X> { }

    // *Static* extension method (not possible today).  Called as `Assert.Boo("", "")`
    // note: no `Assert val`, a static method is not passed an instance value.
    public static bool Boo(string s1, string s2) for Assert { }

    // Static extensions properties, indexers and events are all conceptually supportable.
    // Though we can decide which are sensible to have or not.
    // Static extensions can having backing static fields. Static extension properties can use `field`.
    
    // Nested types must be supported to maintain compatibility with existing static classes with extension members in them.
}
```

Without specifying the full grammar changes, the intuition is that we are making the following changes:

```g4
for-clause
    | 'for' parameter
    ;

parameter (no change)
    | attributes? modifiers? type identifier ...
    ;

extension
    | attributes? modifiers? 'extension' identifier { member_declaration* }
    ;

// For method/property/indexer/operator/constructor/event declarations
// we are augmenting its syntax to allow type-parameters
// (if not already allowed) and a for-clause. For example:
property-declaration
    | attributes? modifiers identifier type-parameters for-clause property-body
    ;
    
compilation-unit-member
    | ...
    | extension
    ;
 
namespace-declaration-member
    | ...
    | extension
    ;
```

The use of `parameter` means all of the following are legal, with the same semantics that that classic extension methods have today:

```c#
for ref Span<T> span
for ref readonly Span<T> span
for in Span<T> span
for scoped ref Span<T> span
for scoped Span<T> span
```

Modern extensions continue to not allow adding fields or destructors to a type.

Not all of these *new* extension member forms need be supported.  For example, we may decide that a `static extension indexer` or `static extension event` is just too esoteric, and can be cut.  The proposal shows them all though to demonstrate completeness of the idea.  All *classic* forms must be supported of course.


## Migration and compatibility

Given an existing static class with extensions, a straightforward *semantically identical* (both at the source and binary level) translation to modern extensions is done in the following fashion.

```c#
// Existing style
static class E
{
    static TField field;
    static int Property => ...
    static void NonExtensionHelperMethod() { }

    static int ExtensionMethod(this string x, ...) { }
    static T GenericExtensionMethod<T, U>(this U u, ...) { }
}

// New style
extension E
{
    // Non extensions stay exactly the same.
    static TField field;
    static int Property => ...

    // Note the lack of a 'for-clause'.  This is a normal static method.
    // An *modern static extension method* will have a 'for-clause' on it
    static void NonExtensionHelperMethod() { }

    // Migrated *instance* extension members
    int ExtensionMethod(...) for string x { }
    T GenericExtensionMethod<T, U>(...) for U u { }
}
```

In other words, all existing extension methods drop `static` from their signature, and move their first parameter to a `for-clause` placed within the method header (currently strawmanned as after the parameter list).  The strawman chooses this location as it already cleanly supports clauses, being where the type parameter constraint clauses already go.

Note: the syntax of a `for-clause` is `'for' parameter`, allowing things like a parameter name to be specified.  `parameter` is critical in this design to ensure the classic extension method `this` parameter can always cleanly move. 

The extension itself (E) will get emitted exactly as a static class would be that contains extension methods (allowing usage from older compilers and other languages without any updates post this mechanical translation).

New extension members (beyond instance members) will need to have their metadata form decided on.  Consumption from older compilers and different languages of these new members will be specified at a later point in time.

A full example of this translation with a real world complex signature would be:

```c#
static class Enumerable
{
    public static TResult Sum<TSource, TResult, TAccumulator>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
        where TResult : struct, INumber<TResult>
        where TAccumulator : struct, INumber<TAccumulator>
    {
        // Original body
    }
}

extension Enumerable
{
    public TResult Sum<TSource, TResult, TAccumulator>(Func<TSource, TResult> selector)
        for IEnumerable<TSource> source
        where TResult : struct, INumber<TResult>
        where TAccumulator : struct, INumber<TAccumulator>
    {
        // Exactly the same code as original body.
    }
}
```

This form supports *non* extension static methods.  For example `Enumerable.Range` would migrate like so:

```c#
static class Enumerable
{
    public static IEnumerable<int> Range(int start, int count) { ... }
}


extension Enumerable
{
    // Exact same signature.  No 'for-clause'.
    public static IEnumerable<int> Range(int start, int count) { ... }
}
```

Perfect source and binary compatibility are goals here.  That means any other features that work with static classes and extensions are expected to migrate over to extensions without change.  This includes, but it not limited to other features like 'attributes'.  For example, all attributes that might be present on a `static class` should migrate unchanged to an `extension`.  Other features not mentioned here should not be inferred to not be part of this design.  By default all features should continue being compatible, and only explicitly specified deviations should be allowed.

## Disambiguation

Classic extension methods today can be disambiguated by falling back to static-invocation syntax.  For example, if `x.Count()` is ambiguous, it is possible to switch to some form of `StaticClass.Count(x)` to call the desired method.  A similar facility is needed for modern extension members.  While the existing method-invocation-translation approach works fine for methods (where the receiver can be remapped to the first argument of the static extension method call), it is ungainly for these other extension forms.

As an initial strawman this proposal suggests reusing `cast expression` syntax for disambiguation purposes.  For example:

```c#
var v1 = ((Extension)receiver).ExtensionMethod(); // instead of Extension.ExtensionMethod(receiver)
var v2 = ((Extension)receiver).ExtensionProperty;
var v3 = ((Extension)receiver)[indexerArg];
var v4 = (Extension)receiver1 + receiver2;
```

Constructors and static methods would not need any special syntax as the extension can cleanly be referenced as a type where needed.

```c#
var v3 = new Extension(...); // Makes instance of the actual extended type.
var v4 = Extension.StaticExtensionMethod(...);
```

Note 1: while the cast syntax traditionally casts or converts a value, that would not be the case for its use here.  It would only be used as a lookup mechanism to indicate which extension gets priority.  Importantly, even with this syntax, extensions themselves are not types.  For example:

```c#
Extension e1;                   // Not legal.  Extension is not a type.
Extension[] e2;                 // Not legal.  Extension is not a type.
List<Extension> e3;             // Not legal.  Extension is not a type.
var v1 = (Extension)receiver;   // Not legal.  Can't can't have a value of extension type.
```

This is exactly the same as the restrictions on static-types *except* with the carve out that you can use the extension in a cast-syntax or new-expression *only* for lookup purposes, or where a static-class could be used, and nothing else.  Usage in places like `nameof(Extension)` or `typeof(Extension)` would still be fine, as those are places where a static type is allowed.

Note 2. If cast syntax is not desirable here (especially if confuses the idea if extensions are types), we can come up with a new syntactic form.  We are not beholden to the above syntax.

# Future expansion

The above initial strawman solves several major goals for we want for the extensions space:

1. Supporting a much broader set of extension member types.
2. Having a clean syntax for extension members that matches the regular syntax form (in other words, an extension proeprty still looks like a property).
3. Ensuring teams can move safely to modern extensions *especially* in environments where source *and* binary compatibility is non-negotiable.

However, there are parts of its core design that are not ideal in the long term which we would like to ensure we can expand on.  These expansions could be released with extensions if time and other resources permit.  Or they could come later and cleanly sit on top of the feature to improve the experience.

These areas are:

## Expansion 1: Syntactic clumsiness and repetition

The initial extension form considers source and binary compatibility as core requirements that must be present to ensure easy migration, allowing codebases to avoid both:
1. bifurcation; where some codebases adopt modern extensions and some do not.
2. internal inconsistency; where some codebases must keep around old extensions and new extensions, with confusion about the semantics of how each interacts with the other.

Because classic extension methods have very few restrictions, modern extension methods need to be flexible enough to support all the scenarios which they support.

However, many codebases do not need all the flexibility that classic extension methods afforded.  For example, classic extension methods allow disparate extension methods in a single static class to target multiple different types.  For use cases where that isn't required, we forsee a natural extension (pun intended) where one can translate a modern extension like so:

```c#
extension E
{
    // All extension members extend the same thing:

    public void M() for SomeType str { ... }
    public int P for SomeType str { get { ... } }
    public static operator+(...) for SomeType str { ... }
    // etc
}

// Can be translated to:

extension E for SomeType str
{
    public void M() { ... }
    public int P { get { ... } }
    public static operator+(...) { ... }
}

TODO: Do an ecosystem check on what percentage of existing extensions could use this simpler form.

TODO: It's possible someone might have an extension where almost all extensions extend a single type, and a small handful do something slightly different (perhaps extending by `this ref`).  Would it be beneficial here to *still* allow the extension members to provide a `for-clause` to override that default for that specific member.  For example:

```c#
extension StringExtensions for string str
{
    // Lots of normal extension methods on string ...

    // Override here to extend `string?`
    public bool MyIsNullOrEmpty() for [NotNullWhen(false)] string? str
    {
    }
}
```

It seems like this would be nice to support with little drawback.

## Expansion 2: Optional syntactic components

As above, we want modern extensions to completely subsume classic extension methods.  As such, a modern extension  method must be able to support everything a classic extension method supported.  For example:

```c#
static class Extensions
{
    // Yes, this is legal
    public static void MakeNonNull([Attr] this ref int? value)
    {
        if (value is null)
            value = 0;
    }
}
```

For this reason, the strawman syntax is:

```g4
for-clause
    | 'for' parameter`
    ;

parameter (unchanged)
    | attributes? modifiers? type identifier
    | attributes? modifiers? type identifier '=' expression
    ;
```

Fortunately, extension methods today don't support a default value for the `this` parameter, so we don't have to support migrating the second `= value` form forward, and we would consider writing a default value in a `for-clause` to be an error.

However, for many extensions no name is really required.  All non-static extension members (instance methods, properties, indexers and events) are conceptually a way to extend `this` with new functionality.  This is so much so the case that we even designed classic extension methods to use the `this` keyword as their designator.  As such, we forsee potentially making the name optional, allowing one to write an extension like so:

```c#
extension Enumerable
{
    public TResult Sum<TSource, TResult, TAccumulator>(Func<TSource, TResult> selector)
        for IEnumerable<TSource> // no name
        where TResult : struct, INumber<TResult>
        where TAccumulator : struct, INumber<TAccumulator>
    {
        // Use 'this' in here to represent the value being extended
    }
}
```

This would have to come with some default name chosen by the language for the parameter in metadata.  But that never be needed by anyone calling it from a modern compiler.

## Expansion 3: Generic extensions.

The initial design allows for extending generic types through the use of generic extension members.  For example:

```c#
extension IListExtensions
{
    public void ForEach<T>(Action<T> act) for IList<T> list
    {
        foreach (var value in list)
            act(list);
    }

    public long LongCount<T> for IList<T> list
    {
        get
        {
            long count = 0;
            foreach (var value in list)
                count++;

            return count;
        }
    }
}
```

Ideally with the optional first expansion we could 'lift' `List<T>` up to `extension IListExtensions`.  However, this doesn't work as we need to define the type parameter it references.  This naturally leads to the following idea:

```c#
extension IListExtensions<T> for IList<T> list
{
    public void ForEach(Action<T> act) { ... }
    public long LongCount { ... }
}
```

This has a few new, but solvable, design challenges.  For example, say one has the code:

```c#
List<int> ints = ...;
var v = ints.ForEach(i => Console.WriteLine(i));
```

This naturally raises the question of how does this extension get picked for this particular receiver, and how does its type parameter get instantiated to the `int` type.

Conceptually (and trying to keep somewhat in line with classic extension methods), we really want to think of the 'receiver' as an 'argument' to some method where normal type inference occurs.  Morally, we could think of there being a `IListExtension<T> Infer<T>(IList<T> list)` function whose shape is determined by the extension and its type-parameters and the extended receiver parameter.

Then, when trying to determine if an extension applies to a receiver, it would be akin to calling that function with the receiver and seeing if inference works.  In the above example that would mean performing type inference on `Infer(ints)` seeing that `T` then bound to `int`, which then gives you back `IListExtensions<int>`.  At that point, lookup would then find and perform overload resolution on `ForEach(Action<int>)` with the lambda parameter.

This approach does fundamentally expand on the initial extension-members approach, as now, calling extensions is done in two phases.  An initial phase to determine and infer extension type parameters based on the receiver, and a second phase to determine and perform overload resolution on the member.

We believe this is very powerful and beneficial.  But there are deep design questions here which may cause this to be scheduled after the core extension members work happens.

## Expansion 4: Extensions as actual types

We are very undecided on if we actually want this.  Currently, our view is that it feels like 'roles' fits this goal much better, especially if roles have the ability to be 'implicit' or 'explicit'.  Extensions exist very much in the space where they are erased and really are just delicious delicious sugar over calling effectively static helpers to augment a type or value.

Roles, on the other hand, seem more fitted to the type space where they are truly part of the type system, intended to appear in signatures, generics, and the like, potentially with strong enforcement about values moving into or out of the role.

This warrants deep discussion about the path taken here and the future we are envisioning, to ensure we're happy with any paths this current approach may close off or make more difficult.

# Alternate strawmen syntaxes

The above strawman chooses a big syntactic jump forward for all extension member cases.  That's not at all a requirement, and many options are possible.  A non-exhaustive list of variants are:

## Variant 1: Reuse existing syntax when possible

Instead of introducing `extension` or syntax for an instance-extension-method, we can reuse syntax.  For example:

```c#
// Continue to use static-class
static class MyExtensions
{
    // Continue using existing syntax for instance extension methods
    public static void Boo<T>(this T value) { }

    // New instance extensions would be simpler:
    // Property form:
    // No more 'val' to name the instance.  You use 'this'.  Same for the other instance members.
    public int Count<X> for SomeType<X> { get { ... } }

    // Static extension members are the same as above.  They already do not name an 'instance'.
}
```

This has the benefit of not needing two syntaxes for instance method extensions.  And reducing the amount of tweaks an extension member can make.

Pros: It's clear what this is reducing to.  Merging ('partial') with existing static classes is clear.  All the restrictions on static-classes stay the same and don't need to reapply to 'extensions'.
Cons: Later augmentations like `static class MyExtensions for string` may or may not feel good. 

## Variant 2: No generic non-method members.

The original proposal puts generics on all members, beyond just methods.  However, this is not explicitly required, as this only adds capabilities to new extension members (extension methods already support generics).  As such, we could require that any non-method extensions on generic-types themselves stay non-generic, with only the extension being generic.  In other words:

```c#

extension E<T> for SomeType<X>
{
    // Property form:
    // Auto-properties, or usage of 'field' not allowed as extensions do not have instance state.
    public int Count { get { ... } }

    // Event form:
    // Note: would have to be the add/remove form.
    // field-backed events would not be possible as extensions do not have instance state.
    public event Action E { add { } remove { } }

    // Indexer form:
    public int this[int index] { get { ... } }

    // Operator form:
    // note: no SomeType<X> val, an operator is static, so it is not passed an instance value.
    public static SomeTypeX<X> operator+(SomeType<X> s1, SomeType<X> s2) for SomeType<X> { ... }
    
    // Conversion form:
    // note: no SomeType<X> val, an operator is static, so it is not passed an instance value.
    public static implicit operator SomeTypeX<T>(int i) { ... }

    // Constructor form:
    // note: no SomeType<X> val, an operator is static, so it is not passed an instance value.
    public SomeType() { }
}
```

## Detailed design
[design]: #detailed-design
