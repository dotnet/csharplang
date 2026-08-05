# Extending Extensions: A Guide to Relaxation

## Summary

This proposal aims to describe a series of possible relaxations to the design presented by
[anonymous extension declarations](https://github.com/dotnet/csharplang/blob/ac07118129334241b6a1dfa53a7e8c715b9ec0b1/meetings/working-groups/extensions/anonymous-extension-declarations.md).
Because that document lays out key design principles that this proposal builds upon, it should be considered required
reading for this one.

## Motivation

The anonymous extension declarations proposal provides an excellent solution to many of the challenges described in
[the design space for extensions](https://github.com/dotnet/csharplang/blob/ac07118129334241b6a1dfa53a7e8c715b9ec0b1/meetings/working-groups/extensions/the-design-space-for-extensions.md).
By introducing the concept of an "extension declaration" declared anonymously within a static class to serve as a
grouping for extension members, it elegantly avoids the "type explosion" problem that has plagued other designs. This
new design ensures that extension members can be declared with a simple, familiar syntax while removing any rough edges
that would complicate consumption.

The design is based on a set of assumptions that are not necessarily agreed upon, but impose restrictions that bring
clarity and simplicity to the design space. It is the belief of this author that some of these assumptions will not be
true for all and the feature will feel restrictive in its current form.

Generally, the restrictions will be felt by those declaring extension members, not those consuming extension members.
Classic extension methods offer a tremendous amount of flexibility with regard to the declaration and organization of
methods. In contrast, anonymous extension declarations design imposes limits that may serve as speed bumps to existing
extension method authors. It is the goal of this proposal to identify opportunities to relax some of those restrictions
with the goal of providing a similar level of flexibility for anonymous extension declarations that classic extension
methods already enjoy.

This is approach is not unlike C# records. Positional records provide a simple and straightforward syntax that offer
type authors a broad set of functionality. However, during the design of records, it was realized that type authors
needed ways to customize records in various ways. And so, many features were introduced for type authors to serve as
"knobs" when declaring records, such as init-only properties, Deconstruct methods, value-based equality customization,
required properties, mutable and immutable struct-based records, etc.

## Detailed design

### Optional parameter name on an extension declaration

The anonymous extension declarations proposal makes the following assumption:

> - Parameter names for underlying values aren't important and people will resent the forced verbosity of having to specify them.

While this leads to a clear design, that assertion will not be true for all. Parameter names provide important value
to a member’s signature and the code within.

- Parameter names are an important form of self-documentation when the type system isn’t expressive enough to clearly
  state the author's intent. For example, it is helpful when a parameter of type `Person` has the parameter name
  `teacher`. This is a clear indicator to the caller what sort of `Person` to pass and helps make the code within the
  member more readable.
- Parameter names can synergize semantically with other parameter names. For example, the parameter name `source` might
  be linked semantically to another parameter named `destination`.
- Parameter names can be used to disambiguate overloads by applying a named argument.
- Parameter names are important for XML documentation. Our own API documentation for the classic extension methods
  defined on [`System.Linq.Enumerable`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable) often have
  different documentation for the `source` parameter. In addition, parameters are referenced by name via the
  `<paramref name="name"/>` XML doc comment tag.

By disallowing parameter names for an extension member’s "receiver parameter", programmers must use `this` to refer to
the underlying value. However, in some cases, that may result in code that uses `this` in ways that are less natural to
most users’ mental model of `this`. Consider the example taken from the anonymous extension declarations proposal below.

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

The code above uses `this` in a way that will be unfamiliar to most users. Most users aren’t aware that that `this` can
be assigned within a mutable struct, and users who *are* aware largely view it as a bad practice.

To relax this, consider an optional parameter name on the anonymous extension declaration.

``` c#
public static class Bits
{
    extension(ref ulong number) // underlying value is passed by ref and called "number"
    {
        public bool this[int index]
        {
            get => (number & Mask(index)) != 0;
            set => number = value ? number | Mask(index) : number & ~Mask(index); // mutates underlying value
        }
    }
    static ulong Mask(int index) => 1ul << index;
}
```

The underlying type on the anonymous extension declaration corresponds strongly with a primary constructor, so it
should be clear to the programmer that the parameter name is accessible with the extension members.

Importantly, adding a parameter name would disable the ability to use `this` within an extension member body. Instead,
the programmer must use the parameter name with all member bodies in the declaration. In addition, because `this` is no
longer available, it won't be possible to reference `this` implicitly either.

``` c#
public static class Enumerable
{
    extension(IEnumerable source)
    {
        // Must use the parameter name to access the underlying value.
        public bool IsEmpty => !source.GetEnumerator().MoveNext();

        // Removing the parameter name would make it possible to use this, implicitly and explicitly.
        // public bool IsEmpty => !this.GetEnumerator().MoveNext();
        // public bool IsEmpty => !GetEnumerator().MoveNext();
    }
}
```

This proposed relaxation does not imply that attributes targeting the underlying value parameter would be moved to the
extension declaration. Instead, they would continue to be declared on the parameter using the
`param` attribute specifier just as they are in the base design.

Unfortunately, adding an optional parameter name at the top of the extension declaration means there’s another axis
that might force an extension author to need another declaration. If the author wants the `this` parameter name to
change between extension members within the same underlying type, they’ll need a declaration for each variation of the
parameter name.

This can be solved with another relaxation.

### Mixing underlying types within an extension declaration

While the anonymous extension declaration proposal protects the consumer from the "type explosion" problem, that
problem is still very much alive for the extension author. The base proposal makes the following assumptions:

> - Underlying types belong together with their type parameter declarations, and it would be confusing to separate them.
> - People will resent the verbosity of having to repeat underlying types and accompanying type parameters for each member.

These assumptions drive a design in which all members within an extension declaration must share the same underlying
type for their `this` parameter, including nullable reference types. While it is possible that both of these
assumptions may true, they will not be true for all.

It _may_ be confusing to separate underlying types from the their type parameter declarations. However, that depends
largely on the mental model a programmer develops for extensions. If they see extensions as a special form of type
inheritance, it would indeed by confusing to separate type parameters from the type itself! However, if they view
extensions as a new way to extension methods that allow for other members with a simpler syntax, they might not be so
confused.

It’s also true that people may resent having to repeat underlying types and accompanying type parameters for each
member. However, if the first assumption above is relaxed to allow the underlying type to be separated from their type
parameters, the second assumption becomes much more palatable. And, classic extension methods require that the
underlying type be repeated for each method, and it is a massively popular feature.

Consider the following classic extension methods from Roslyn’s public API:

```c#
public static class CSharpExtensions
{
    public static bool IsKind(this SyntaxToken token, SyntaxKind kind) => ...;
    public static bool IsKind(this SyntaxTrivia trivia, SyntaxKind kind) => ...;
    public static bool IsKind([NotNullWhen(true)] this SyntaxNode? node, SyntaxKind kind) => ...;
    public static bool IsKind(this SyntaxNodeOrToken nodeOrToken, SyntaxKind kind) => ...;
    public static bool ContainsDirective(this SyntaxNode node, SyntaxKind kind) => ...;
}
```

If these were written using the base design, they would need to be declared across four extension declarations:

```c#
public static class CSharpExtensions
{
    extension(SyntaxToken)
    {
        public bool IsKind(SyntaxKind kind) => ...;
    }
    
    extension(SyntaxTrivia)
    {
        public bool IsKind(SyntaxKind kind) => ...;
    }
    
    extension(SyntaxNode?)
    {
        [param: NotNullWhen(true)]
        public bool IsKind(SyntaxKind kind) => ...;
    }
    
    extension(SyntaxNodeOrToken)
    {
        public bool IsKind(SyntaxKind kind) => ...;
    }
    
    extension(SyntaxNode)
    {
        public bool ContainsDirective(SyntaxKind kind) => ...;
    }
}
```

It is this author’s belief that programmers will resent being forced to separate extension declarations by underlying
type. This resentment might even deepen for programmers who realize that all of the extension declarations above would
lower to the same nested static class. A common question might be, if the compiler generates extension declarations to
the same type, why must they be declared in separate declarations?

To relax this restriction, consider allowing the parenthesized underlying types to be moved to the member declarations.
Making a correspondence with the reduced form of an extension method, the underlying types would be declared before the
member names with a `.` token.

```c#
public static class CSharpExtensions
{
    extension
    {
        public bool (SyntaxToken).IsKind(SyntaxKind kind) => ...;
        public bool (SyntaxTrivia).IsKind(SyntaxKind kind) => ...;
        [param: NotNullWhen(true)]
        public bool (SyntaxNode?).IsKind(SyntaxKind kind) => ...;
        public bool (SyntaxNodeOrToken).IsKind(SyntaxKind kind) => ...;
        public bool (SyntaxNode).ContainsDirective(SyntaxKind kind) => ...;
    }
}
```

This expansion to the base design provides a similar level of grouping flexibility that programmers enjoy with
classic extension methods. In addition, the position chosen for a member-level underlying type works for all other
extension member kinds, as well.

```c#
public static class Extensions
{
    extension
    {
        // instance extension property
        public bool (Digit).IsPrime => ...;

        // instance extension indexer
        public bool (Digit).this[int bit] => ...;

        // instance extension event
        public event EventHandler (Digit).BitFlipped
        {
            add => ...;
            remove => ...;
        }

        // static extension method
        public static int (int).FromBits(ReadOnlySpan<bool> bits) => ...;

        // static extension property
        public static Utf8StringComparer (StringComparer).OrdinalUtf8 => ...;

        // static extension event
        public static event EventHandler SystemEvents.NetworkConnected
        {
            add => ...;
            remove => ...;
        }

        // operator overloads
        public static Digit operator +(Digit d) => ...;
        public static Digit operator +(Digit d1, Digit d2) => ...;
        
        // User-defined conversions
        public static implicit operator byte(Digit d) => ...;
        public static explicit operator Digit(byte b) => ...;
    }
}
```

Interestingly, there’s no need to provide declare the underlying type for an operator overload or user-defined
conversion. It should be implicit from the signature.

It would still be possible to declare non-method extension members that use type parameters and constraints. However,
such type parameters and constraints would go on the extension declaration. In addition, members within an extension
declaration must use all type parameters on the declaration in order to be callable as an extension.

```c#
public static class Extensions
{
    extension<T>
    {
        public bool (List<T>).IsEmpty => this.Count == 0;
    }
}
```

Finally, if an instance extension member is declared with its underlying type, the programmer can include a parameter
name.

```c#
public static class Extensions
{
    extension<T>
    {
        public bool (List<T> list).IsEmpty => list.Count == 0;
    }
}
```

## Unresolved Questions

- It seems that an extension member that declares its own own underlying type and doesn’t depend on an external type
parameter could be declared directly in the body of a static class. Is this useful, or would it be confusing?
- The base design uses a model that is arguably closer to extension members rather than extension types. After all, the
"types" are fully anonymous and transparent. Given that, would it be helpful to rename the "extension" keyword to
"extensions"? Does it matter?
- The base design lowers all extension declarations to a nested static class, even if a declaration doesn’t declare any
type parameters. If extension declarations are relaxed to allow the underlying type and parameter to be declared per
member, it seems that binary compatibility with classic extension methods could be achieved if non-generic extension
declarations lowered to generic members in the static class body.
- If a programmer declares an underlying type on one member, this proposal implies that means that _all_ members within
an extension declaration must declare their underlying type. Is it possible to allow both the extension declaration and
members declare an underlying type? In this case, the member would win and "override" the extension declaration’s
"default" underlying type. This might help avoid the "cliff" of switching the underlying type from the extension
declaration to it’s members. Or would this be too confusing for authors?
- Is there a way that an extension author could declare a parameter name per member without moving the underlying type?