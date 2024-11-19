# Extension members

## What's changed

This proposal is an update on [Anonymous extension declarations](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/extensions/anonymous-extension-declarations.md) in the following ways:

- It leans into a parameter-style syntax for the underlying value.
- It clarifies the goals of lowering, while leaving more details to implementation.
- It includes syntax for generating compatible extension methods.

### Underlying value: `this` vs parameter

 The strongest sentiment I've heard is that whichever approach we take should be done *consistently*. Of the two there's a lean towards the parameter approach. This proposal leans into the parameter-based approach as strongly as it can.

### Lowering

The proposal takes the stance that the declarations generated from lowering - static methods and/or types - should be hidden from the language level, making extension members a "real" abstraction. There has been curiosity about exposing these directly to users somehow, but there is no concrete proposal on the table.

This frees up the implementation strategy considerably, but it still needs to establish metadata and rules that can be followed and consumed by other compilers, and that ensure stability and compatibility for consuming code as APIs evolve.

### Compatibility with classic extension methods

There is a clear desire to be able to move classic extension methods forward into new syntax without breaking existing callers. This proposal lets extension methods be marked for compatibility, which will cause them to generate visible static methods in the pattern of classic extension methods.

## Declaration

### Static classes as extension containers

Extensions are declared inside top-level non-generic static classes, just like extension methods today, and can thus coexist with classic extension methods and non-extension static members:

``` c#
public static class Enumerable
{
    // New extension declaration
    extension(IEnumerable source) { ... }
    
    // Classic extension method
    public static IEnumerable<TResult> Cast<TResult>(this IEnumerable source) { ... }
    
    // Non-extension member
    public static IEnumerable<int> Range(int start, int count) { ... } 
}
```

### Extension declarations

An extension declaration is anonymous, and provides a receiver specification with any associated type parameters and constraints, followed by a set of extension member declarations. The receiver specification may be in the form of a parameter, or - if only static extension members are declared - a type:

``` c#
public static class Enumerable
{
    extension(IEnumerable source) // extension members for IEnumerable
    {
        public bool IsEmpty { get { ... } }
    }
    extension<TSource>(IEnumerable<TSource> source) // extension members for IEnumerable<TSource>
    {
        public IEnumerable<T> Where(Func<TSource, bool> predicate) { ... }
        public IEnumerable<TResult> Select<TResult>(Func<TSource, TResult> selector) { ... }
    }
    extension<TElement>(IEnumerable<TElement>) // static extension members for IEnumerable<TElement>
        where TElement : INumber<TElement>
    {
        public static IEnumerable<TElement> operator +(IEnumerable<TElement> e1, IEnumerable<TElement> e2) { ... }
    }
}
```

### Extension members

Extension member declarations are syntactically identical to corresponding instance and static members in class and struct declarations (with one exception: constructors). Instance members refer to the receiver with the parameter name given in the extension declaration's receiver specification:

``` c#
public static class Enumerable
{
    extension(IEnumerable source)
    {
        // 'source' refers to underlying value
        public bool IsEmpty => !source.GetEnumerator().MoveNext();
    }
}
```

It is an error to specify an instance extension member (method, property, indexer or event) if the receiver specification in the enclosing extension declaration is not in the form of a parameter:

``` c#
public static class Enumerable
{
    extension(IEnumerable) // No parameter name
    {
        public bool IsEmpty => true; // Error: instance extension member not allowed
    }
}
```

### Refness

By default the receiver is passed to instance extension members by value, just like other parameters. However, an extension declaration receiver in parameter form can specify `ref`, `ref readonly` and `in`, as long as the receiver type is known to be a value type. 

If `ref` is specified, an instance member or one of its accessors can be declared `readonly`, which prevents it from mutating the receiver:

``` c#
public static class Bits
{
    extension(ref ulong bits) // receiver is passed by ref
    {
        public bool this[int index]
        {
            set => bits = value ? bits | Mask(index) : bits & ~Mask(index); // mutates receiver
            readonly get => (bits & Mask(index)) != 0;                // cannot mutate receiver
        }
    }
    static ulong Mask(int index) => 1ul << index;
}
```

### Nullability and attributes

Receiver types can be or contain nullable reference types, and receiver specifications that are in the form of parameters can specify attributes:

``` c#
public static class NullableExtensions
{
    extension(string? text)
    {
        public string AsNotNull => text is null ? "" : text;
    }
    extension([NotNullWhen(false)] string? text)
    {
        public bool IsNullOrEmpty => text is null or [];
    }
    extension<T> ([NotNull] T t) where T : class?
    {
        public void ThrowIfNull() => ArgumentNullException.ThrowIfNull(t);
    }
}
```

### Compatible extension methods

By default, extension members are lowered in such a way that the generated artifacts are not visible at the language level. However, if the receiver specification is in the form of a parameter and specifies the `this` modifier, then any extension instance methods in that extension declaration are lowered to visible classic extension methods.

Specifically the generated static method has the attributes, modifiers and name of the declared extension method, as well as type parameter list, parameter list and constraints list concatenated from the extension declaration and the method declaration in that order:

``` c#
public static class Enumerable
{
    extension<TSource>(this IEnumerable<TSource> source) // Generate compatible extension methods
    {
        public IEnumerable<TSource> Where(Func<TSource, bool> predicate) { ... }
        public IEnumerable<TSource> Select<TResult>(Func<TSource, TResult> selector)  { ... }
    }
}
```

Generates:

``` c#
public static class Enumerable
{
    public IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) { ... }
    public IEnumerable<TSource> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)  { ... }
}
```

### Constructors

*To be completed*

- Provide new constructor overloads for existing types
- But does not mirror constructor syntax
- Body needs to explicitly create object and return like a static factory

### Operators

*To be completed*

- Operators need to be in an extension declaration, even though they specify both their own operators
- This is in analogy with other operator declarations
- Helps with lookup rules
- Helps with uniqueness rules

## Checking

*To be completed*

- Similar to previous doc

## Consumption

*To be completed*

- Similar to previous doc
- Maybe avoid introducing strawman disambiguation
- Note that ref extensions must be invoked on *variables*

## Lowering

The lowering strategy for extension declarations is not a language level decision. However, beyond implementing the language semantics it must satisfy certain requirements:

- The form of generated types, members and metadata should be clearly specified so that other compilers can consume and generate it.
- The generated artifacts should be hidden from the language level not just of the new C# compiler but of any existing compiler that respects CLI rules (e.g. around modreq's).
- The generated artifacts should be stable, in the sense that later edits that aren't usually considered breaking should not break consumers who compiled against earlier versions.

## Order of implementation

We should do member kinds in the following order:

- Properties and methods (instance and static)
- Operators
- Indexers (instance and static, can be done opportunistically at an earlier point)
- Anything else

## Future directions

*To be completed*

- Abbreviations as in previous doc

