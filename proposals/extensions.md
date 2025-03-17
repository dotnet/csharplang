# Extension members

Champion issue: <https://github.com/dotnet/csharplang/issues/8697>

## Declaration

### Syntax

```antlr
class_body
    : '{' class_member_declaration* '}' ';'?
    | ';'
    ;

class_member_declaration
    : constant_declaration
    | field_declaration
    | method_declaration
    | property_declaration
    | event_declaration
    | indexer_declaration
    | operator_declaration
    | constructor_declaration
    | finalizer_declaration
    | static_constructor_declaration
    | type_declaration
    | extension_declaration // add
    ;

extension_declaration // add
    : 'extension' type_parameter_list? '(' receiver_parameter ')' type_parameter_constraints_clause* extension_body
    ;

extension_body // add
    : '{' extension_member_declaration* '}' ';'?
    ;

extension_member_declaration // add
    : method_declaration
    | property_declaration
    | indexer_declaration
    | operator_declaration
    ;

receiver_parameter // add
    : attributes? parameter_modifiers? type identifier?
    ;
```

Extension declarations shall only be declared in non-generic, non-nested static classes.  
It is an error for a type to be named `extension`.  

### Declaration spaces

Section [7.3 Declarations](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/basic-concepts.md#73-declarations) in the C# Standard is updated as follows (additions in **bold non-italic**):

> ...
> 
> There are several different types of declaration spaces, as described in the following.
> 
> ...
> 
> - Each method declaration, property declaration, property accessor declaration, indexer declaration, indexer accessor declaration, operator declaration, instance constructor declaration, anonymous function, and local function creates a new declaration space called a ***local variable declaration space***. Names are introduced into this declaration space through formal parameters (*fixed_parameter*s and *parameter_array*s) and *type_parameter*s. The set accessor for a property or an indexer introduces the name `value` as a formal parameter. 
**An *extension_declaration* introduces to each member declaration directly contained within it its *type_parameter_list* as type parameters and the *identifier*, if any, of its *receiver_parameter* as a formal parameter.**

There is an existing need to specify (maybe in Section[12.8.4 Simple Names](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1284-simple-names)) something along the lines of the following:

> If a local variable directly occurring in a given declaration space is accessed from within a static member or static lambda expression that is itself nested within that declaration space, a compile-time error is given.

Thus, for receiver parameters as well as any other local variable, a reference from within a static context still resolves to that variable, but leads to an error.

``` c#
public static class E
{
    extension(string s) // *1*
    {
        public int M(int i) // *2*
        {
            return s.Length + i;
        }
        public static string P => s; // Error: Cannot use s from static context
    }
}
```

In the example, `*1*` and `*2*` denote local variable declaration spaces, where `*2*` is nested within `*1*` and thus prohibited from introducing the same local variable names as `*1*`.

The receiver parameter `s` shall not be accessed from a static context; thus the body of the static property `P` yields a compile-time error.

### Static classes as extension containers

Extensions are declared inside top-level non-generic static classes, just like extension methods today, 
and can thus coexist with classic extension methods and non-extension static members:

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

An extension declaration is anonymous, and provides a _receiver specification_ with any associated type parameters and constraints, 
followed by a set of extension member declarations. The receiver specification may be in the form of a parameter, 
or - if only static extension members are declared - a type:

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
        public static IEnumerable<TElement> operator +(IEnumerable<TElement> first, IEnumerable<TElement> second) { ... }
    }
}
```

The type in the receiver specification is referred to as the _receiver type_ and the parameter name,
if present, is referred to as the _receiver parameter_.

If the _receiver parameter_ has an identifier, the _receiver type_ may not be static.  
The _receiver parameter_ is only allowed to have the refness modifiers listed below and `scoped`.  

### Extension members

Extension member declarations are syntactically identical to corresponding instance and static members
in class and struct declarations (with the exception of constructors). 
Instance members refer to the receiver with the receiver parameter name:

``` c#
public static class Enumerable
{
    extension(IEnumerable source)
    {
        // 'source' refers to receiver
        public bool IsEmpty => !source.GetEnumerator().MoveNext();
    }
}
```

It is an error to specify an instance extension member (method, property, indexer or event)
if the enclosing extension declaration does not specify a receiver parameter:

``` c#
public static class Enumerable
{
    extension(IEnumerable) // No parameter name
    {
        public bool IsEmpty => true; // Error: instance extension member not allowed
    }
}
```

It is an error to specify the `partial` modifier on a member of an extension declaration.

### Refness

By default the receiver is passed to instance extension members by value, just like other parameters. 
However, an extension declaration receiver in parameter form can specify `ref`, `ref readonly` and `in`, 
as long as the receiver type is known to be a value type. 

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

By default, extension members are lowered in such a way that the generated artifacts are not visible at the language level.
However, if the receiver specification is in the form of a parameter and specifies the `this` modifier, 
then any extension instance methods in that extension declaration will generate visible classic extension methods.

Specifically the generated static method has the attributes, modifiers and name of the declared extension method, 
as well as type parameter list, parameter list and constraints list concatenated from the extension declaration and the method declaration in that order:

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
    public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) { ... }
    public static IEnumerable<TSource> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)  { ... }
}
```

This does not change anything else about how the declared extension method works. 
However, adding the `this` modifier may lead to a binary break for consumers because the generated artifact may change.

### Operators

Although extension operators have explicit operand types, they still need to be declared within an extension declaration:

``` c#
public static class Enumerable
{
    extension<TElement>(IEnumerable<TElement>) where TElement : INumber<TElement>
    {
        public static IEnumerable<TElement> operator *(IEnumerable<TElement> vector, TElement scalar) { ... }
        public static IEnumerable<TElement> operator *(TElement scalar, IEnumerable<TElement> vector) { ... }
    }
}
```

This allows type parameters to be declared and inferred, and is analogous to how a regular user-defined operator must be declared within one of its operand types.

## Checking

__Inferrability:__ All the type parameters of an extension declaration must be used in the receiver type. 
This makes it always possible to infer the type arguments when applied to a receiver of the given receiver type.

__Uniqueness:__ Within a given enclosing static class, the set of extension member declarations with the same receiver type 
(modulo identity conversion and type parameter name substitution) are treated as a single declaration space 
similar to the members within a class or struct declaration, and are subject to the same 
[rules about uniqueness](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#153-class-members).

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

The application of this uniqueness rule includes classic extension methods within the same static class.
For the purposes of comparison with methods within extension declarations, the `this` parameter is treated
as a receiver specification along with any type parameters mentioned in that receiver type,
and the remaining type parameters and method parameters are used for the method signature:

``` c#
public static class Enumerable
{
    public static IEnumerable<TResult> Cast<TResult>(this IEnumerable source) { ... }
    
    extension(IEnumerable source) 
    {
        IEnumerable<TResult> Cast<TResult>() { ... } // Error! Duplicate declaration
    }
}
```

## Consumption

When an extension member lookup is attempted, all extension declarations within static classes that are `using`-imported contribute their members as candidates,
regardless of receiver type. Only as part of resolution are candidates with incompatible receiver types discarded.
A full generic type inference is attempted between the type of the actual receiver and any type parameters in the declared receiver type.

The inferrability and uniqueness rules mean that the name of the enclosing static type is sufficient to disambiguate 
between extension members on a given receiver type. 
As a strawman, consider `E @ T` as a disambiguation syntax meaning on a given expression `E` begin member lookup for an immediately enclosing expression in type `T`. For instance:

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

Means lookup `Where` in the type `Enumerable` with `strings` as its receiver.
A type argument `string` can now be inferred for `T` from the type of `strings` using standard generic type inference.

A similar approach also works for types: `T1 @ T2` means on a given type `T1`
begin static member lookup for an immediately enclosing expression in type `T2`.

This disambiguation approach should work not only for new extension members but also for classic extension methods.

Note that this is not a proposal for a specific disambiguation syntax;
it is only meant to illustrate how the inferrability and uniqueness rules enable disambiguation
without having to explicitly specify type arguments for an extension declaration's type parameters.

## Lowering

The lowering strategy for extension declarations is not a language level decision. 
However, beyond implementing the language semantics it must satisfy certain requirements:

- The format of generated types, members and metadata should be clearly specified in all cases so that other compilers can consume and generate it.
- The generated artifacts should be hidden from the language level not just of the new C# compiler but of any existing compiler that respects CLI rules (e.g. modreq's).
- The generated artifacts should be stable, in the sense that reasonable later modifications should not break consumers who compiled against earlier versions.

These requirements need more refinement as implementation progresses, and may need to be compromised in corner cases in order to allow for a reasonable implementation approach.

### Metadata for declarations

Each extension declaration is emitted as a nested private static class with a marker method and skeleton members.  
Each skeleton member is accompanied by a top-level static implementation method with a modified signature.  

#### Skeletons

Each extension declaration in source is emitted as an extension declaration in metadata.  
- Its name is unspeakable and determined based on the lexical order in the program.  
  The name is not guaranteed to remain stable across re-compilation. 
  Below we use `<>E__` followed by an index. For example: `<>E__2`.  
- Its type parameters are those declared in source (including attributes).  
- Its accessibility is public.  

Method/property/indexer declarations in an extension declaration in source are represented as skeleton members in metadata.  
The signatures of the original methods are maintained (including attributes), but their bodies are replaced with `throw null`.  
Those should not be referenced in IL.  

Note: This is similar to ref assemblies. The reason for using `throw null` bodies (as opposed to no bodies) 
is so that IL verification could run and pass (thus validating the completeness of the metadata).

The extension marker method encodes the receiver parameter.  
- It is public and static, and is called `<Extension>$`.  
- It has the attributes, refness, type and name from the receiver parameter on the extension declaration.  
- If the receiver parameter doesn't specify a name, then the parameter name is empty.  

Note: This allows roundtripping of extension declaration symbols through metadata (full and reference assemblies).  

Note: we may choose to only emit one extension skeleton type in metadata when duplicate extension declarations are found in source.  

#### Implementations

The method bodies for method/property/indexer declarations in an extension declaration in source are emitted 
as static implementation methods in the top-level static class.  
- An implementation method is named by prepending `<Extension>` for instance case or `<StaticExtension>` for static case to the name of the original method.  
  For example: `set_Property` => `<Extension>set_Property`.
- It has type parameters derived from the extension declaration prepended to the type parameters of the original method (including attributes).  
- It has the same accessibility and attributes as the original method.  
- If it implements a static method, it has the same parameters and return type. 
- It if implements an instance method, it has a prepended parameter to the signature of the original method. 
  This parameter's attributes, refness, type, and name are derived from the receiver parameter declared in the relevant extension declaration.
- The parameters in implementation methods refer to type parameters owned by implementation method, instead of those of an extension declaration.  

For example:
```
static class IEnumerableExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        public void Method() { ... }
        internal static int Property { get => ...; set => ...; }
    }

    extension(IAsyncEnumerable<int> values)
    {
        public async Task<int> SumAsync() { ... }
    }

    public void Method() { ... }
}
```
is emitted as
```
static class IEnumerableExtensions
{
    public class <>E__1<T>
    {
        public static <Extension>$(IEnumerable<T> source) => throw null;
        public void Method() => throw null;
        public static int Property { get => throw null; set => throw null; }
    }

    public class <>E__2
    {
        public static <Extension>$(IAsyncEnumerable<int> values) => throw null;
        public Task<int> SumAsync() => throw null;
    }

    // Implementation for Method
    public static void <Extension>Method<T>(IEnumerable<T> source) { ... }

    // Implementation for Property
    internal static int <StaticExtension>get_Property<T>() { ... }
    internal static void <StaticExtension>set_Property<T>(int value) { ... }

    // Implementation for SumAsync
    public static int <Extension>SumAsync(IAsyncEnumerable<int> values) { ... }
}
```

Whenever extension members are used in source, we will emit those as reference to implementation methods.  
For example: an invocation of `enumerableOfInt.Method()` would be emitted as a static call 
to `IEnumerableExtensions.<Extension>Method<int>(enumerableOfInt)`.  

Note: multiple extension declarations defining static members with the same signature cannot be represented in metadata.  
However, differences in return types can be represented, allowing for factory methods extending different types. For example:
```csharp
static class CollectionExtensions
{
    extension<T>(List<T>)
    {
        public static List<T> Create() { ... }
    }
    extension<T>(HashSet<T>)
    {
        public static HashSet<T> Create() { ... }
    }
}
```

## Open issues

### Metadata

- Is a gesture from users required to emit methods in 100% compatible way with classic extension methods? (ie. speakable name and `[Extension]` attribute)
- Should we emit implementation methods with speakable names instead, as a disambiguation strategy and also to allow
  usage from other languages? We could add an attribute to handle compile-time conflicts in factory scenario (`[ExtensionName("CreateList")]`).
- The metadata format currently doesn't include any modreqs to block other compilers. But the spec does mention we
  would block those scenarios. Let's either remove this requirement or update the metadata format.  
- We should follow-up on "factory scenario" where multiple extension declarations have static factory methods 
  with same parameter types but different return types.

### Lookup

- How do we resolve the small gap between classic and new extension methods in invocation?
- How do we mix classic and new extension methods in invocation?
- Should we just prefer more specific extension members or use a form of "better member" selection?
- Scoping and shadowing rules for extension parameter and type parameters?

### Add support for more member kinds

We do not need to implement all of this design at once, but can approach it one or a few member kinds at a time. 
Based on known scenarios in our core libraries, we should work in the following order:

1. Properties and methods (instance and static)
2. Operators
3. Indexers (instance and static, may be done opportunistically at an earlier point)
4. Anything else

How much do we want to front-load the design for other kinds of members?
```antlr
extension_member_declaration // add
    : constant_declaration
    | field_declaration
    | method_declaration
    | property_declaration
    | event_declaration
    | indexer_declaration
    | operator_declaration
    | constructor_declaration
    | finalizer_declaration
    | static_constructor_declaration
    | type_declaration
    ;
```

#### Nested types

If we do choose to move forward with extension nested types, here are some notes from previous discussions:
- There would be a conflict if two extension declarations declared nested extension types with same names and arity.
  We do not have a solution for representing this in metadata.  
- The rough approach we discussed for metadata:
  1. we would emit a skeleton nested type with original type parameters and no members
  2. we would emit an implementation nested type with prepended type parameters from the extension declaration and 
     all the member implementations as they appear in source (modulo references to type parameters)

#### Constructors

Constructors are generally described as an instance member in C#, since their body has access to the newly created value through the `this` keyword. 
This does not work well for the parameter-based approach to instance extension members, though, since there is no prior value to pass in as a parameter.

Instead, extension constructors work more like static factory methods. 
They are considered static members in the sense that they don't depend on a receiver parameter name. 
Their bodies need to explicitly create and return the construction result. 
The member itself is still declared with constructor syntax, but cannot have `this` or `base` initializers and does not rely on the receiver type having accessible constructors.

This also means that extension constructors can be declared for types that have no constructors of their own, such as interfaces and enum types:

``` c#
public static class Enumerable
{
    extension(IEnumerable<int>)
    {
        public static IEnumerable(int start, int count) => Range(start, count);
    }
    public static IEnumerable<int> Range(int start, int count) { ... } 
}
```

Allows:

```
var range = new IEnumerable<int>(1, 100);
```

### Disambiguation

We still need to settle on a disambiguation syntax. Per the above it needs to be able to take a receiver expression or type, as well as the name of a static class from which to begin member lookup. However, the feature does not have to be extension-specific. There are several cases in C# where it's awkward to get to the right member of a given receiver. Casting often works, but can lead to boxing that may be too expensive or lead to mutations being lost.

It would probably be unfortunate to ship extension members without a disambiguation syntax, so this has high priority.

### Shorter forms

The proposed design avoids per-member repetition of receiver specifications, but does end up with extension members being nested two-deep in a static class _and_ and extension declaration. It will likely be common for static classes to contain only one extension declaration or for extension declarations to contain only one member, and it seems plausible for us to allow syntactic abbreviation of those cases.

__Merge static class and extension declarations:__

``` c#
public static class EmptyExtensions : extension(IEnumerable source)
{
    public bool IsEmpty => !source.GetEnumerator().MoveNext();
}
```

This ends up looking more like what we've been calling a "type-based" approach, where the container for extension members is itself named.

__Merge extension declaration and extension member:__ 

``` c#
public static class Bits
{
    extension(ref ulong bits) public bool this[int index]
    {
        get => (bits & Mask(index)) != 0;
        set => bits = value ? bits | Mask(index) : bits & ~Mask(index);
    }
    static ulong Mask(int index) => 1ul << index;
}
 
public static class Enumerable
{
    extension<TSource>(IEnumerable<TSource> source) public IEnumerable<TSource> Where(Func<TSource, bool> predicate) { ... }
}
```

This ends up looking more like what we've been calling a "member-based" approach, where each extension member contains its own receiver specification. 

