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

### Compatibility with classic extension methods

Instance extension methods generate artifacts that match those produced by classic extension methods.

Specifically the generated static method has the attributes, modifiers and name of the declared extension method, 
as well as type parameter list, parameter list and constraints list concatenated from the extension declaration and the method declaration in that order:

``` c#
public static class Enumerable
{
    extension<TSource>(IEnumerable<TSource> source) // Generate compatible extension methods
    {
        public IEnumerable<TSource> Where(Func<TSource, bool> predicate) { ... }
        public IEnumerable<TSource> Select<TResult>(Func<TSource, TResult> selector)  { ... }
    }
}
```

Generates:

``` c#
[Extension]
public static class Enumerable
{
    [Extension]
    public static IEnumerable<TSource> Where<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate) { ... }

    [Extension]
    public static IEnumerable<TSource> Select<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)  { ... }
}
```

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
A full generic type inference is attempted between the type of the arguments (including the actual receiver) and any type parameters (combining those in the extension declaration and in the extension member declaration).  
When explicit type arguments are provided, they are used to substitute the type parameters of the extension declaration and the extension member declaration.

``` c#
string[] strings = ...;

var query = strings.Select(s => s.Length); // extension invocation
var query2 = strings.Select<string, int>(s => s.Length); // ... with explicit full set of type arguments

var query3 = Enumerable.Select(strings, s => s.Length); // static method invocation
var query4 = Enumerable.Where<string, int>(strings, s => s.Length); // ... with explicit full set of type arguments
 
public static class Enumerable
{
    extension<TSource>(IEnumerable<TSource> source)
    {
        public IEnumerable<TResult> Select<TResult>(Func<T, TResult> predicate) { ... }
    }
}
```

Similarly to classic extension methods, the emitted implementation methods can be invoked statically.  
This allows the compiler to disambiguate between extension members with the same name and arity.  

```csharp
object.M(); // ambiguous
E1.M();

new object().M2(); // ambiguous
E1.M2(new object());

_ = _new object().P; // ambiguous
_ = E1.get_P(new object());

static class E1
{
    extension(object)
    {
        public static void M() { }
        public void M2() { }
        public int P => 42;
    }
}

static class E2
{
    extension(object)
    {
        public static void M() { }
        public void M2() { }
        public int P => 42;
    }
}
```

Static extension methods will be resolved like instance extension methods (we will consider an extra argument of the receiver type).  
Extension properties will be resolved like extension methods, with a single parameter (the receiver parameter) and a single argument (the actual receiver value).  

## Lowering

The lowering strategy for extension declarations is not a language level decision. 
However, beyond implementing the language semantics it must satisfy certain requirements:

- The format of generated types, members and metadata should be clearly specified in all cases so that other compilers can consume and generate it.
- The generated artifacts should be stable, in the sense that reasonable later modifications should not break consumers who compiled against earlier versions.

These requirements need more refinement as implementation progresses, and may need to be compromised in corner cases in order to allow for a reasonable implementation approach.

### Metadata for declarations

Each extension declaration is emitted as a nested private static class with a marker method and skeleton members.  
Each skeleton member is accompanied by a top-level static implementation method with a modified signature.    
The containing static class for an extension declaration is marked with an `[Extension]` attribute.  

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
- An implementation method has the same name as the original method.  
- It has type parameters derived from the extension declaration prepended to the type parameters of the original method (including attributes).  
- It has the same accessibility and attributes as the original method.  
- If it implements a static method, it has the same parameters and return type. 
- It if implements an instance method, it has a prepended parameter to the signature of the original method. 
  This parameter's attributes, refness, type, and name are derived from the receiver parameter declared in the relevant extension declaration.
- The parameters in implementation methods refer to type parameters owned by implementation method, instead of those of an extension declaration.  
- If the original member is an instance ordinary method, the implementation method is marked with an `[Extension]` attribute.

For example:
```
static class IEnumerableExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        public void Method() { ... }
        internal static int Property { get => ...; set => ...; }
        public int Property2 { get => ...; set => ...; }
    }

    extension(IAsyncEnumerable<int> values)
    {
        public async Task<int> SumAsync() { ... }
    }

    public static void Method2() { ... }
}
```
is emitted as
```
[Extension]
static class IEnumerableExtensions
{
    public class <>E__1<T>
    {
        public static <Extension>$(IEnumerable<T> source) => throw null;
        public void Method() => throw null;
        internal static int Property { get => throw null; set => throw null; }
        public int Property2 { get => throw null; set => throw null; }
    }

    public class <>E__2
    {
        public static <Extension>$(IAsyncEnumerable<int> values) => throw null;
        public Task<int> SumAsync() => throw null;
    }

    // Implementation for Method
    [Extension]
    public static void Method<T>(IEnumerable<T> source) { ... }

    // Implementation for Property
    internal static int get_Property<T>() { ... }
    internal static void set_Property<T>(int value) { ... }

    // Implementation for Property2
    public static int get_Property2<T>(IEnumerable<T> source) { ... }
    public static void set_Property2<T>(IEnumerable<T> source, int value) { ... }

    // Implementation for SumAsync
    [Extension]
    public static int SumAsync(IAsyncEnumerable<int> values) { ... }

    public static void Method2() { ... }
}
```

Whenever extension members are used in source, we will emit those as reference to implementation methods.  
For example: an invocation of `enumerableOfInt.Method()` would be emitted as a static call 
to `IEnumerableExtensions.Method<int>(enumerableOfInt)`.  

Note: the metadata representation supports static extension methods that differ in return type.
For example:
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
But if the return types match too, the signatures will conflict.
```csharp
static class CollectionExtensions
{
    extension<T>(List<T>)
    {
        public static T[] Create() { ... }
    }
    extension<T>(HashSet<T>)
    {
        public static T[] Create() { ... }
    }
}
```

## Open issues

- Confirm `extension` vs. `extensions` as the keyword

### Metadata

- Should skeleton methods throw `NotSupportedException` or some other standard exception (right now we do `throw null;`)?
- Should we accept more than one parameter in marker method in metadata (in case new versions add more info)?
- Should the extension marker or speakable implementation methods be marked with special name?
- Should we add `[Extension]` attribute on the static class even when there is no instance extension method inside? (answer: yes, LDM 2025-03-10)
- Confirm we should add `[Extension]` attribute to implementation getters and setters too. (answer: no, LDM 2025-03-10)

#### static factory scenario

We talked about emitting a modopt on return type for implementation methods corresponding to static extension members.
But that has some limitations, as roslyn only allows named type symbols (so no type parameters or array types).

### Lookup

- How to resolve instance method invocations now that we have speakable implementation names? We need to avoid ambiguity by understanding that those are the same method.
- How to resolve static extension methods? (answer: just like instance extension methods, LDM 2025-03-03)
- Should betterness be adjusted for resolution of static extension methods?
- How to resolve properties? (answered in broad strokes LDM 2025-03-03, but needs follow-up for betterness)
- Scoping and shadowing rules for extension parameter and type parameters (answer: in scope of extension block, shadowing disallowed, LDM 2025-03-10)
- How should ORPA apply to new extension methods?
- How to retcon the classic extension resolution rules? Do we 
  1. update the standard for classic extension methods, and use that to also describe new extension methods,
  2. keep the existing language for classic extension methods, use that to also describe new extension methods, but have a known spec deviation for both,
  3. keep the existing language for classic extension methods, but use different language for new extension methods, and only have a known spec deviation for classic extension methods?
- Confirm that we want to disallow explicit type arguments on a property access:
```csharp
string s = "ran";
_ = s.P<object>; // error

static class E
{
    extension<T>(T t)
    {
        public int P => 0;
    }
}
```
- Confirm that we want betterness rules to apply even when the receiver is a type
```csharp
int.M();

static class E1
{
    extension(int)
    {
        public static void M() { }
    }
}
static class E2
{
    extension(in int i)
    {
        public static void M() => throw null;
    }
}
```
- Confirm that we don't want some betterness across all members before we determine the winning member kind:
```
string s = null;
s.M(); // error

static class E
{
    extension(string s)
    {
        public System.Action M => throw null;
    }
    extension(object o)
    {
        public string M() => throw null;
    }
}
```
- Do we want to synthesize a receiver?
```csharp
static class E
{
    extension(object o)
    {
        public void M() 
        {
            M2();
        }
        public void M2() { }
    }
}
```

### Accessibility

- What is the meaning of `private` within an extension declaration? ([thread](https://github.com/dotnet/roslyn/pull/77358#discussion_r1974061527))

### Extension declaration validation

- Should we relax the type parameter validation (inferrability: all the type parameters must appear in the type of the extension parameter) where there are only methods? This would allow porting 100% of classic extension methods.  
If you have `TResult M<TResult, TSource>(this TSource source)`, you could port it as `extension<TResult, TSource>(TSource source) { TResult M() ... }`.
- Confirm whether init-only accessors should be allowed in extensions
- Should the only difference in receiver ref-ness be allowed `extension(int receiver) { public void M2() {} }`    `extension(ref int receiver) { public void M2() {} }`?
- Should we complain about a conflict like this `extension(object receiver) { public int P1 => 1; }`   `extension(object receiver) { public int P1 {set{}} }`?

### XML docs

- Is `paramref` to receiver parameter supported on extension members? Even on static? How is it encoded in the output? Probably standard way `<paramref name="..."/>` would work for a human,  but there is a risk that some existing tools won't be happy to not find it among the parameters on the API.
- Are we supposed to copy doc comments to the implementation methods with speakable names?
- Should `<param>` element corresponding to receiver parameter be copied from extension container for instance methods? Anything else should be copied from container to implementation methods (`<typeparam>` etc.) ?

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

