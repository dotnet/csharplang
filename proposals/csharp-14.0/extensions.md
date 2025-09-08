# Extension members

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Champion issue: https://github.com/dotnet/csharplang/issues/8697

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
    | operator_declaration
    ;

receiver_parameter // add
    : attributes? parameter_modifiers? type identifier?
    ;
```

Extension declarations shall only be declared in non-generic, non-nested static classes.  
It is an error for a type to be named `extension`.  

### Scoping rules

The type parameters and receiver parameter of an extension declaration are in scope within the body of the extension declaration. It is an error to refer to the receiver parameter from within a static member, except within a `nameof` expression. It is an error for members to declare type parameters or parameters (as well as local variables and local functions directly within the member body) with the same name as a type parameter or receiver parameter of the extension declaration.

``` c#
public static class E
{
    extension<T>(T[] ts)
    {
        public bool M1(T t) => ts.Contains(t);        // `T` and `ts` are in scope
        public static bool M2(T t) => ts.Contains(t); // Error: Cannot refer to `ts` from static context
        public void M3(int T, string ts) { }          // Error: Cannot reuse names `T` and `ts`
        public void M4<T, ts>(string s) { }           // Error: Cannot reuse names `T` and `ts`
    }
}
```

It is not an error for the members themselves to have the same name as the type parameters or receiver parameter of the enclosing extension declaration. Member names are not directly found in a simple name lookup from within the extension declaration; lookup will thus find the type parameter or receiver parameter of that name, rather than the member. 

Members do give rise to static methods being declared directly on the enclosing static class, and those can be found via simple name lookup; however, an extension declaration type parameter or receiver parameter of the same name will be found first.

``` c#
public static class E
{
    extension<T>(T[] ts)
    {
        public void T() { M(ts); } // Generated static method M<T>(T[]) is found
        public void M() { T(ts); } // Error: T is a type parameter
    }
}
```

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

If the _receiver parameter_ is named, the _receiver type_ may not be static.  
The _receiver parameter_ is not allowed to have modifiers if it is unnamed, and 
it is only allowed to have the refness modifiers listed below and `scoped` otherwise.  
The _receiver parameter_ bears the same restrictions as the first parameter of a classic extension method.  
The `[EnumeratorCancellation]` attribute is ignored if it is placed on the _receiver parameter_.  

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

It is an error to specify an instance extension member
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

It is an error to specify the following modifiers on a member of an extension declaration:
`abstract`, `virtual`, `override`, `new`, `sealed`, `partial`, and `protected` (and related accessibility modifiers).  
Properties in extension declarations may not have `init` accessors.  
The instance members are disallowed if the _receiver parameter_ is unnamed.  

All members shall have names that differ from the name of the static enclosing class and the name of the extended type if it has one.

It is an error to decorate an extension member with the `[ModuleInitializer]` attribute.

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

__Inferrability:__ For each non-method extension member, all the type parameters of its extension block must be used in the combined set of parameters
from the extension and the member.

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

### OverloadResolutionPriorityAttribute

Extension members within an enclosing static class are subject to prioritization according to ORPA values. The enclosing static
class is considered the "containing type" which ORPA rules consider.  
Any ORPA attribute present on an extension property is copied onto the implementation methods for the property's accessors,
so that the prioritization is respected when those accessors are used via disambiguation syntax.  

### Entry points

Methods of extension blocks do not qualify as entry point candidates (see "7.1 Application startup").
Note: the implementation method may still be a candidate.  

## Lowering

The lowering strategy for extension declarations is not a language level decision. 
However, beyond implementing the language semantics it must satisfy certain requirements:

- The format of generated types, members and metadata should be clearly specified in all cases so that other compilers can consume and generate it.
- The generated artifacts should be stable, in the sense that reasonable later modifications should not break consumers who compiled against earlier versions.

These requirements need more refinement as implementation progresses, and may need to be compromised in corner cases in order to allow for a reasonable implementation approach.

### Metadata for declarations

#### Goals

The design below allows:
- roundtripping of extension declaration symbols through metadata (full and reference assemblies),
- stable references to extension members (xml docs), 
- local determination of emitted names (helpful for EnC),
- public API tracking.  

For xml docs, the docID for an extension member is the docID for the extension member in metadata. For example, the docID used in `cref="Extension.extension(object).M(int)"`
is `M:Extension.<>E__ExtensionGroupingTypeNameForObject.M(System.Int32)` and that docID is stable across re-compilations and re-orderings of extension blocks. Ideally, it would also remain
stable when constraints on the extension block change, but we didn't find a design that would achieve that without detrimental effect on language design for member conflicts.

For EnC, it is useful to know locally (just by looking at a modified extension member) where the updated extension member is emitted in metadata.  

For public API tracking, more stable names reduce noise. But technically the extension grouping type names should not come into play in such scenarios. When looking at extension member `M`,
it doesn't matter what is the name of the extension grouping type, what matters is the signature of the extension block it belongs to. The public API signature should not be seen as
`Extension.<>E__ExtensionGroupingTypeNameForObject.M(System.Int32)` but rather as `Extension.extension(object).M(int)`. In other words, extension members should be seen as having two sets of
type parameters and two sets of parameters.

#### Overview

Extension blocks are grouped by their CLR-level signature. Each CLR equivalency group is emitted as an **extension grouping type** with a content-based name.
Extension blocks within a CLR equivalency group are then sub-grouped by C# equivalency. Each C# equivalency group is emitted as an **extension marker type** with a content-based name, nested in its corresponding extension grouping type.
An extension marker type contains a single **extension marker method** which encodes an extension parameter.
The extension marker method with its containing extension marker type encode the signature of an extension block with full fidelity.
Declaration of each extension member is emitted in the right extension grouping type, refers back to an extension marker type by its name via an attribute, and is accompanied by a top-level static **implementation method** with a modified signature.    

Here's a schematized overview of metadata encoding:
```
[Extension]
static class EnclosingStaticClass
{
    [Extension]
    public sealed class ExtensionGroupingType1 // has type parameters with minimal constraints sufficient to keep extension member declarations below valid
    {
        public static class ExtensionMarkerType1 // has re-declared type parameters with full fidelity of C# constraints
        {
            public static void <Extension>$(... extension parameter ...) // extension marker method
        }
        ... ExtensionMarkerType2, etc ...

        ... extension members for ExtensionGroupingType1, each points to its corresponding extension marker type ...
    }

    ... ExtensionGroupingType2, etc ...

    ... implementation methods ...
}
```

The enclosing static class is emitted with an `[Extension]` attribute.  


### CLR-level signature vs. C#-level signature

The CLR-level signature of an extension block results from:
- normalizing type parameter names to `T0`, `T1`, etc ...
- removing attributes
- erasing the parameter name
- erasing parameter modifiers (like `ref`, `in`, `scoped`, ...)
- erasing tuple names
- erasing nullability annotations
- erasing `notnull` constraints

Note: other constraints are preserved, such as `new()`, `struct`, `class`, `allows ref struct`, `unmanaged`, and type constraints.

#### Extension grouping types

An extension grouping type is emitted to metadata for each set of extension blocks in source with a the same CLR-level signature.  
- Its name is unspeakable and determined based on the contents of the CLR-level signature. More details below.  
- Its type parameters have normalized names (`T0`, `T1`, ...) and have no attributes.  
- It is public and sealed.
- It is marked with the `specialname` flag and an `[Extension]` attribute.  

The content-based name of the extension grouping type is based on the CLR-level signature and includes the following:
- The fully qualified CLR name of the type of the extension parameter.
    - Referenced type parameter names will be normalized to `T0`, `T1`, etc ... based on the order they appear in the type declaration.
    - The fully qualified name will not include the containing assembly. It is common for types to be moved between assemblies and that should not break the xml doc references.
- Constraints of type parameters will be included and sorted such that reordering them in source code does not change the name. Specifically:
    - Type parameter constraints will be listed in declaration order. The constraints for the Nth type parameter will occur before the Nth+1 type parameter.
    - Type constraints will be sorted by comparing the full names ordinally.
    - Non-type constraints are ordered deterministically and are handled so as to avoid any ambiguity or collision with type constraints.
- Since this does not include attributes, it intentionally ignores C#-isms like tuple names, nullability, etc ...

Note: The name is guaranteed to remain stable across re-compilations, re-orderings and changes of C#-isms (ie. that don't affect the CLR-level signature).   

#### Extension marker types

The marker type re-declares the type parameters of its containing grouping type (an extension grouping type) to get full fidelity of the C# view of extension blocks.

An extension marker type is emitted to metadata for each set of extension blocks in source with a the same C#-level signature.  
- Its name is unspeakable and determined based on the contents of the C#-level signature of the extension block. More details below.  
- It redeclares the type parameters for its containing grouping type to be those declared in source (including name and attributes).
- It is public and static.
- It is marked with the `specialname` flag.  

The content-based name of the extension marker type is based on the following:
- The names of type parameters will be included in the order they appear in the extension declaration
- The attributes of type parameters will be included and sorted such that reordering them in source code does not change the name.
- Constraints of type parameters will be included and sorted such that reordering them in source code does not change the name.
- The fully qualified C# name of the extended type
    - This will include items like nullable annotations, tuple names, etc ... 
    - The fully qualified name will not include the containing assembly
- The name of the extension parameter
- The modifiers of the extension parameter (`ref`, `ref readonly`, `scoped`, ...) in a deterministic order
- The fully qualified name and attribute arguments for any attributes applied to the extension parameter in a deterministic order

Note: The name is guaranteed to remain stable across re-compilation and re-orderings.  
Note: extension marker types and extension marker methods are emitted as part of reference assemblies.  

#### Extension marker method

The purpose of the marker method is to encode the extension parameter of the extension block. 
Because it is a member of the extension marker type, it can refer to the re-declared type parameters of the extension marker type.

Each extension marker type contains a single method, the extension marker method.
- It is static, non-generic, void-returning, and is called `<Extension>$`.  
- Its single parameter has the attributes, refness, type and name from the extension parameter.  
  If the extension parameter doesn't specify a name, then the parameter name is empty.  
- It is marked with the `specialname` flag.

Accessibility of the marker method will be the least restrictive accessibility among corresponding declared
extension members, `private` is used if none are declared.

#### Extension members

Method/property declarations in an extension block in source are represented as members of the extension grouping type in metadata.  
- The signatures of the original methods are maintained (including attributes), but their bodies are replaced with `throw NotImplementedException()`.  
- Those should not be referenced in IL.  
- Methods, properties and their accessors are marked with `[ExtensionMarkerName("...")]` referring to the name of the extension marker type corresponding to the extension block for that member.  

#### Implementation methods

The method bodies for method/property declarations in an extension block in source are emitted 
as static implementation methods in the top-level static class.  
- An implementation method has the same name as the original method.  
- It has type parameters derived from the extension block prepended to the type parameters of the original method (including attributes).  
- It has the same accessibility and attributes as the original method.  
- If it implements a static method, it has the same parameters and return type. 
- It if implements an instance method, it has a prepended parameter to the signature of the original method. 
  This parameter's attributes, refness, type, and name are derived from the extension parameter declared in the relevant extension block.
- The parameters in implementation methods refer to type parameters owned by implementation method, instead of those of an extension block.  
- If the original member is an instance ordinary method, the implementation method is marked with an `[Extension]` attribute.

#### ExtensionMarkerName attribute

The `ExtensionMarkerNameAttribute` type is for compiler use only - it is not permitted in source. The type declaration is synthesized by the compiler if not already included in the compilation.

```csharp
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate, Inherited = false)]
public sealed class ExtensionMarkerNameAttribute : Attribute
{
    public ExtensionMarkerNameAttribute(string name)
        => Name = name;

    public string Name { get; }
}
```

Note: Although some attribute targets are included for future-proofing (extension nested types, extension fields, extension events), AttributeTargets.Constructor is not included as extension constructors would not be constructors.

#### Example

Note: we use simplified content-based names for the example, for readability.
Note: since C# cannot represent type parameter re-declaration, the code representing metadata is not valid C# code.

Here's an example illustrating how grouping works, without members:

```
class E
{
    extension<T>(IEnumerable<T> source)
    {
        ... member in extension<T>(IEnumerable<T> source)
    }

    extension<U>(ref IEnumerable<U?> p)
    {
        ... member in extension<U>(ref IEnumerable<U?> p)
    }

    extension<T>(IEnumerable<U> source)
        where T : IEquatable<U>
    {
        ... member in extension<T>(IEnumerable<U> source) where T : IEquatable<U>
    }
}
```
is emitted as
```
[Extension]
class E
{
    [Extension, SpecialName]
    public sealed class <>E__ContentName_For_IEnumerable_T<T0>
    {
        [SpecialName]
        public static class <>E__ContentName1 // note: re-declares type parameter T0 as T
        {
            [SpecialName]
            public static void <Extension>$(IEnumerable<T> source) { }
        }

        [SpecialName]
        public static class <>E__ContentName2 // note: re-declares type parameter T0 as U
        {
            [SpecialName]
            public static void <Extension>$(ref IEnumerable<U?> p) { }
        }

        [ExtensionMarkerName("<>E__ContentName1")]
        ... member in extension<T>(IEnumerable<T> source)

        [ExtensionMarkerName("<>E__ContentName2")]
        ... member in extension<U>(ref IEnumerable<U?> p)
    }

    [Extension, SpecialName]
    public sealed class <>ContentName_For_IEnumerable_T_With_Constraint<T0>
       where T0 : IEquatable<T0>
    {
        [SpecialName]
        public static class <>E__ContentName3 // note: re-declares type parameter T0 as U
        {
            [SpecialName]
            public static void <Extension>$(IEnumerable<U> source) { }
        }

        [ExtensionMarkerName("ContentName3")]
        public static bool IsPresent(U value) => throw null!;
    }

    ... implementation methods
}
```

Here's an example illustrating how members are emitted:
```
static class IEnumerableExtensions
{
    extension<T>(IEnumerable<T> source) where T : notnull
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
    [Extension, SpecialName]
    public sealed class <>E__ContentName_For_IEnumerable_T<T0>
    {
        // Extension marker type is emitted as a nested type and re-declares its type parameters to include C#-isms
        // In this example, the type parameter `T0` is re-declared as `T` with a `notnull` constraint:
        // .class <>E__IEnumerableOfT<T>.<>E__ContentName_For_IEnumerable_T_Source
        // .typeparam T
        //     .custom instance void NullableAttribute::.ctor(uint8) = (...)
        [SpecialName]
        public static class <>E__ContentName_For_IEnumerable_T_Source
        {
            [SpecialName]
            public static <Extension>$(IEnumerable<T> source) => throw null;
        }

        [ExtensionMarkerName("<>E__ContentName_For_IEnumerable_T_Source")]
        public void Method() => throw null;

        [ExtensionMarkerName("<>E__ContentName_For_IEnumerable_T_Source")]
        internal static int Property
        {
            [ExtensionMarkerName("<>E__ContentName_For_IEnumerable_T_Source")]
            get => throw null;
            [ExtensionMarkerName("<>E__ContentName_For_IEnumerable_T_Source")]
            set => throw null;
        }

        [ExtensionMarkerName("<>E__ContentName_For_IEnumerable_T_Source")]
        public int Property2
        {
            [ExtensionMarkerName("<>E__ContentName_For_IEnumerable_T_Source")]
            get => throw null;
            [ExtensionMarkerName("<>E__ContentName_For_IEnumerable_T_Source")]
            set => throw null;
        }
    }

    [Extension, SpecialName]
    public sealed class <>E__ContentName_For_IAsyncEnumerable_Int
    {
        [SpecialName]
        public static class <>E__ContentName_For_IAsyncEnumerable_Int_Values
        {
            [SpecialName]
            public static <Extension>$(IAsyncEnumerable<int> values) => throw null;
        }

        [ExtensionMarkerName("<>E__ContentName_For_IAsyncEnumerable_Int_Values")]
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

## XML docs

The doc comments on the extension block are emitted for the marker type (the DocID for the extension block is `E.<>E__MarkerContentName_For_ExtensionOfT'1` in the example below).  
They are allowed to reference the extension parameter and type parameters using `<paramref>` and `<typeparamref>` respectively).  
Note: you may not document the extension parameter or type parameters (with `<param>` and `<typeparam>`) on an extension member.  

If two extension blocks are emitted as one marker type, their doc comments are merged as well.

Tools consuming the xml docs are responsible for copying the `<param>` and `<typeparam>` from the extension block onto the extension members as appropriate (ie. the parameter information should only be copied for instance members).  

An `<inheritdoc>` is emitted on implementation methods and it refers to the relevant extension member with a `cref`. For example, the implementation method for a getter refers to the documentation of the extension property. 
If the extension member has not doc comments, then the `<inheritdoc>` is omitted. 

For extension blocks and extension members, we don't presently warn if:
- the extension parameter is documented, but the parameters on the extension member aren't
- or vice-versa
- or in the equivalent scenarios with undocumented type parameters

For instance, the following doc comments:
```
/// <summary>Summary for E</summary>
static class E
{
    /// <summary>Summary for extension block</summary>
    /// <typeparam name="T">Description for T</typeparam>
    /// <param name="t">Description for t</param>
    extension<T>(T t)
    {
        /// <summary>Summary for M, which may refer to <paramref name="t"/> and <typeparamref name="T"/></summary>
        /// <typeparam name="U">Description for U</typeparam>
        /// <param name="u">Description for u</param>
        public void M<U>(U u) => throw null!;

        /// <summary>Summary for P</summary>
        public int P => 0;
    }
}
```
yield the following xml:
```
<?xml version="1.0"?>
<doc>
    <assembly>
        <name>Test</name>
    </assembly>
    <members>
        <member name="T:E">
            <summary>Summary for E</summary>
        </member>
        <member name="T:E.&lt;&gt;E__MarkerContentName_For_ExtensionOfT`1">
            <summary>Summary for extension block</summary>
            <typeparam name="T">Description for T</typeparam>
            <param name="t">Description for t</param>
        </member>
        <member name="M:E.&lt;&gt;E__MarkerContentName_For_ExtensionOfT`1.M``1(``0)">
            <summary>Summary for M, which may refer to <paramref name="t"/> and <typeparamref name="T"/></summary>
            <typeparam name="U">Description for U</typeparam>
            <param name="u">Description for u</param>
        </member>
        <member name="P:E.&lt;&gt;E__MarkerContentName_For_ExtensionOfT`1.P">
            <summary>Summary for P</summary>
        </member>
        <member name="M:E.M``2(``0,``1)">
            <inheritdoc cref="M:E.&lt;&gt;E__MarkerContentName_For_ExtensionOfT`1.M``1(``0)"/>
        </member>
        <member name="M:E.get_P``1(``0)">
            <inheritdoc cref="P:E.&lt;&gt;E__MarkerContentName_For_ExtensionOfT`1.P"/>
        </member>
    </members>
</doc>
```

### CREF references

We can treat extension blocks like nested types, that can be addressed by their signature (as if it were a method with a single extension parameter).
Example: `E.extension(ref int).M()`.

But a cref cannot address an extension block itself. `E.extension(int)` could refer to a method named "extension" in type `E`.  

```csharp
static class E
{
  extension(ref int i)
  {
    void M() { } // can be addressed by cref="E.extension(ref int).M()" or cref="extension(ref int).M()" within E, but not cref="M()"
  }
  extension(ref  int i)
  {
    void M(int i2) { } // can be addressed by cref="E.extension(ref int).M(int)" or cref="extension(ref int).M(int)" within E
  }
}
```

The lookup knowns to look in all matching extension blocks.  
As we disallow unqualified references to extension members, cref would also disallow them.

The syntax would be:
```antlr
member_cref
  : conversion_operator_member_cref
  | extension_member_cref // added
  | indexer_member_cref
  | name_member_cref
  | operator_member_cref
  ;

extension_member_cref // added
 : 'extension' type_argument_list? cref_parameter_list '.' member_cref
 ;

qualified_cref
  : type '.' member_cref
  ;

cref
  : member_cref
  | qualified_cref
  | type_cref
  ;
```

It's an error to use `extension_member_cref` at top-level (`extension(int).M`) or nested in another extension (`E.extension(int).extension(string).M`).  

## Breaking changes

Types and aliases may not be named "extension".


## Open issues

<details>
<summary>Temporary section of the document related to open issues, including discussion of unfinalized syntax and alternative designs</summary>

- Should we adjust receiver requirements when accessing an extension member? ([comment](https://github.com/dotnet/roslyn/pull/78685#discussion_r2126534632))
- ~~Confirm `extension` vs. `extensions` as the keyword~~ (answer: `extension`, LDM 2025-03-24)
- ~~Confirm that we want to disallow `[ModuleInitializer]`~~ (answer: yes, disallow, LDM 2025-06-11)
- ~~Confirm that we're okay to discard extension blocks as entry point candidates~~ (answer: yes, discard, LDM 2025-06-11)
- ~~Confirm LangVer logic (skip new extensions, vs. consider and report them when picked)~~ (answer: bind unconditionally and report LangVer error except for instance extension methods, LDM 2025-06-11)
- ~~Should `partial` be required for extension blocks that merge and have their doc comments merged?~~ (answer: doc comments are merged silently when blocks get merged, no `partial` needed, confirmed by email 2025-09-03)
- ~~Confirm that members should not be named after the containing or the extended types.~~ (answer: yes, confirmed by email 2025-09-03)

### ~~Revisit grouping/conflict rules in light of portability issue: https://github.com/dotnet/roslyn/issues/79043~~

(answer: this scenario was resolved as part of new metadata design with content-based type names, it is allowed)

The current logic is to group extension blocks that have the same receiver type. This doesn't account for constraints.
This causes a portability issue with this scenario:
```
static class E
{
   extension<T>(ref T) where T : struct
      void M()
   extension<T>(T) where T : class
      void M()
}
```

The proposal is to use the same grouping logic that we're planning for the extension grouping type design, namely to account for CLR-level constraints (ie. ignoring notnull, tuple names, nullability annotations).

### Should refness be encoded in the grouping type name?

- ~~Review proposal that `ref` not be included in extension grouping type name (needs further discussion after WG revisits grouping/conflict rules, LDM 2025-06-23)~~ (answer: confirmed by email 2025-09-03)
```
public static class E
{
  extension(ref int)
  {
    public static void M()
  }
}
```
It is emitted as:
```
public static class E
{
  public static class <>ExtensionTypeXYZ
  {
    .. marker method ...
    void M()
  }
}
```

And third-party CREF reference for `E.extension(ref int).M` are emitted as `M:E.<>ExtensionGroupingTypeXYZ.M()`
If `ref` is removed or added to an extension parameter, we probably don't want the CREF to break.

We don't much care for this scenario, as any usage as extension would be an ambiguity:
```
public static class E
{
  extension(ref int)
    static void M()
  extension(int)
    static void M()
}
```

But we care about this scenario (for portability and usefulness), and this should work with proposed metadata design after we adjust conflict rules:
```
static class E
{
   extension<T>(ref T) where T : struct
      void M()
   extension<T>(T) where T : class
      void M()
}
```

Not accounting for refness has a downside, as we loose portability in this scenario:
```
static class E
{
   extension<T>(ref T)
      void M()
   extension<T>(T)
      void M()
}
// portability issue: since we're grouping without accounting for refness, the emitted extension members conflict (not implementation members). Mitigation: keep as classic extensions or split to another static class
```

### nameof

- ~~Should we disallow extension properties in nameof like we do classic and new extension methods?~~ (answer: we'd like to use `nameof(EnclosingStaticClass.ExtensionMember). Needs design, likely punt from .NET 10. LDM 2025-06-11)

### pattern-based constructs

#### Methods
- ~~Where should new extension methods come into play?~~ (answer: same places where classic extension methods come into play, LDM 2025-05-05)

This includes:  
- `GetEnumerator`/`GetAsyncEnumerator` in `foreach`
- `Deconstruct` in deconstruction, in positional pattern and foreach
- `Add` in collection initializers
- `GetPinnableReference` in `fixed`
- `GetAwaiter` in `await`

This excludes:  
- `Dispose`/`DisposeAsync` in `using` and `foreach`
- `MoveNext`/`MoveNextAsync` in `foreach`
- `Slice` and `int` indexers in implicit indexers (and possibly list-patterns?)
- `GetResult` in `await`

#### Properties and indexers
- ~~Where should extension properties and indexers come into play?~~  (answer: let's start with the four, LDM 2025-05-05)  

We'd include:  
- object initializer: `new C() { ExtensionProperty = ... }`
- dictionary intializer: `new C() { [0] = ... }`
- `with`: `x with { ExtensionProperty = ... }`
- property patterns: `x is { ExtensionProperty: ... }`  
  
We'd exclude:  
- `Current` in `foreach`
- `IsCompleted` in `await`
- `Count`/`Length` properties and indexers in list-pattern
- `Count`/`Length` properties and indexers in implicit indexers

##### Delegate-returning properties
- ~~Confirm that extension properties of this shape should only come into play in LINQ queries, to match what instance properties do.~~ (answer: makes sense, LDM 2025-04-06)

##### List and spread pattern
- Confirm that extension `Index`/`Range` indexers should play in list-patterns (answer: not relevant for C# 14)

##### Revisit where `Count`/`Length` extension properties come into play  

#### [Collection expressions](../csharp-12.0/collection-expressions.md)

- Extension `Add` works
- Extension `GetEnumerator` works for spread
- Extension `GetEnumerator` does not affect the determination of the element type (must be instance)
- Static `Create` extension methods should not count as a blessed **create** method
- Should extension countable properties affect collection expressions?

#### [`params` collections](../csharp-13.0/params-collections.md)

- Extensions `Add` does not affect what types are allowed with `params`

#### [dictionary expressions](https://github.com/dotnet/csharplang/blob/main/proposals/dictionary-expressions.md)

- Confirm that extension indexers don't play in dictionary expressions, as the presence of the indexer is an integral part of what defines a dictionary type. (answer: not relevant for C# 14)

### `extern`

- ~~we're planning to allow `extern` for portability: https://github.com/dotnet/roslyn/issues/78572~~ (answer: approved, LDM 2025-06-23)

### Naming/numbering scheme for extension type

[Issue](https://github.com/dotnet/roslyn/issues/78416)  
The current numbering system causes problems with the [validation of public APIs](https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview#validator-types)
which ensures that public APIs match between reference-only assemblies and implementation assemblies.

~~Should we make one of the following changes?~~ (answer: we're adopting a content-based naming scheme to increase public API stability, and the tools will still need to be updated to account for marker methods)
1. adjust the tool
2. use some content-based naming scheme (TBD)
3. let the name be controlled via some syntax

### New generic extension Cast method still can't work in LINQ

[Issue](https://github.com/dotnet/roslyn/issues/78415)  
In earlier designs of roles/extensions, it was possible to only specify the type arguments of the method explicitly.  
But now that we're focusing on seemless transition from classic extension methods, all the type arguments must be given explicitly.  
This fails to address a problem with extension Cast method usage in LINQ.

~~Should we make a change to extensions feature to accomodate this scenario?~~ (answer: no, this does not cause us to revisit the extension resolution design, LDM 2025-05-05)

### Constraining the extension parameter on an extension member

~~Should we allow the following?~~ (answer: no, this could be added later)

```csharp
static class E
{
    extension<T>(T t)
    {
        public void M<U>(U u) where T : C<U>  { } // error: 'E.extension<T>(T).M<U>(U)' does not define type parameter 'T'
    }
}

public class C<T> { }
```

### Nullability

- ~~Confirm the current design, ie. maximal portability/compatibility~~ (answer: yes, LDM 2025-04-17)

```csharp
    extension([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool b)
    {
        public void AssertTrue() => throw null!;
    }
```
```csharp
    extension([System.Diagnostics.CodeAnalysis.NotNullIfNotNull("o")] ref int? i)
    {
        public void M(object? o)  => throw null!;
    }
```

### Metadata

- ~~Should skeleton methods throw `NotSupportedException` or some other standard exception (right now we do `throw null;`)?~~ (answer: yes, LDM 2025-04-17)
- ~~Should we accept more than one parameter in marker method in metadata (in case new versions add more info)?~~ (answer: we can remain strict, LDM 2025-04-17)
- ~~Should the extension marker or speakable implementation methods be marked with special name?~~ (answer: the marker method should be marked with special name and we should check it, but not implementation methods, LDM 2025-04-17)
- ~~Should we add `[Extension]` attribute on the static class even when there is no instance extension method inside?~~ (answer: yes, LDM 2025-03-10)
- ~~Confirm we should add `[Extension]` attribute to implementation getters and setters too.~~ (answer: no, LDM 2025-03-10)
- ~~Confirm that the extension types should be marked with special name and the compiler will require this flag in metadata (this is a breaking change from preview)~~ (answer: approved, LDM 2025-06-23)

#### static factory scenario

- ~~What are the conflict rules for static methods?~~ (answer: use existing C# rules for the enclosing static type, no relaxation, LDM 2025-03-17)

### Lookup

- ~~How to resolve instance method invocations now that we have speakable implementation names?~~ We prefer the skeleton method to its corresponding implementation method. 
- ~~How to resolve static extension methods?~~ (answer: just like instance extension methods, LDM 2025-03-03)
- ~~How to resolve properties?~~ (answered in broad strokes LDM 2025-03-03, but needs follow-up for betterness)
- ~~Scoping and shadowing rules for extension parameter and type parameters~~ (answer: in scope of extension block, shadowing disallowed, LDM 2025-03-10)
- ~~How should ORPA apply to new extension methods?~~  (answer: treat extension blocks as transparent, the "containing type" for ORPA is the enclosing static class, LDM 2025-04-17)

```
public static class Extensions
{
    extension(Type1)
    {
        [OverloadResolutionPriority(1)]
        public void Overload(...)
    }
    extension(Type2)
    {
        public void Overload(...)
    }
}
```
- ~~Should ORPA apply to new extension properties?~~ (answer: yes and ORPA should be copied onto implementation methods, LDM 2025-04-23)
```
public static class Extensions
{
    extension(int[] i)
    {
        public P { get => }
    }
    extension(ReadOnlySpan<int> r)
    {
       [OverloadResolutionPriority(1)]
       public P { get => }
    }
}
```
- How to retcon the classic extension resolution rules? Do we 
  1. update the standard for classic extension methods, and use that to also describe new extension methods,
  2. keep the existing language for classic extension methods, use that to also describe new extension methods, but have a known spec deviation for both,
  3. keep the existing language for classic extension methods, but use different language for new extension methods, and only have a known spec deviation for classic extension methods?
- ~~Confirm that we want to disallow explicit type arguments on a property access~~ (answer: no property access with explicit type arguments, discussed in WG)
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
- ~~Confirm that we want betterness rules to apply even when the receiver is a type~~ (answer: a type-only extension parameter should be considered when resolving static extension members, LDM 2025-06-23)
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
- ~~Confirm that we're okay with having an ambiguity when both methods and properties are applicable~~ (answer: we should design a proposal to do better than the status quo, punting out of .NET 10, LDM 2025-06-23)
- ~~Confirm that we don't want some betterness across all members before we determine the winning member kind~~ (answer: punting out of .NET 10, WG 2025-07-02)
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
- ~~Do we have an implicit receiver within extension declarations?~~ (answer: no, was previously discussed in LDM)
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
- ~~Should we allow lookup on type parameter?~~ ([discussion](https://github.com/dotnet/csharplang/discussions/8696#discussioncomment-12817547)) (answer: no, we're going to wait on feedback, LDM 2025-04-16)

### Accessibility

- ~~What is the meaning of accessibility within an extension declaration?~~ (answer: extension declarations do not count as an accessibility scope, LDM 2025-03-17)
- ~~Should we apply the "inconsistent accessibility" check on the receiver parameter even for static members?~~  (answer: yes, LDM 2025-04-17)
```csharp
public static class Extensions
{
    extension(PrivateType p)
    {
        // We report inconsistent accessibility error, 
        //   because we generate a `public static void M(PrivateType p)` implementation in enclosing type
        public void M() { } 

        public static void M2() { } // should we also report here, even though not technically necessary?
    }

    private class PrivateType { }
}
```

### Extension declaration validation

- ~~Should we relax the type parameter validation (inferrability: all the type parameters must appear in the type of the extension parameter) where there are only methods?~~ (answer: yes, LDM 2025-04-06) 
This would allow porting 100% of classic extension methods.  
If you have `TResult M<TResult, TSource>(this TSource source)`, you could port it as `extension<TResult, TSource>(TSource source) { TResult M() ... }`. 

- ~~Confirm whether init-only accessors should be allowed in extensions~~  (answer: okay to disallow for now, LDM 2025-04-17)
- ~~Should the only difference in receiver ref-ness be allowed `extension(int receiver) { public void M2() {} }`    `extension(ref int receiver) { public void M2() {} }`?~~ (answer: no, keep spec'ed rule, LDM 2025-03-24)
- ~~Should we complain about a conflict like this `extension(object receiver) { public int P1 => 1; }`   `extension(object receiver) { public int P1 {set{}} }`?~~ (answer: yes, keep spec'ed rule, LDM 2025-03-24)
- ~~Should we complain about conflicts between skeleton methods that aren't conflicts between implementation methods?~~ (answer: yes, keep spec'ed rule, LDM 2025-03-24)
```csharp
static class E
{
    extension(object)
    {
        public void Method() {  }
        public static void Method() { }
    }
}
```
The current conflict rules are: 1. check no conflict within similar extensions using class/struct rules, 2. check no conflict between implementation methods across various extensions declarations.  
- ~~Do we stil need the first part of the rules?~~ (answer: yes, we're keeping this structure as it helps with consumption of the APIs, LDM 2025-03-24)

### XML docs

- ~~Is `paramref` to receiver parameter supported on extension members? Even on static? How is it encoded in the output? Probably standard way `<paramref name="..."/>` would work for a human,  but there is a risk that some existing tools won't be happy to not find it among the parameters on the API.~~ (answer: yes paramref to extension parameter is allowed on extension members, LDM 2025-05-05)
- ~~Are we supposed to copy doc comments to the implementation methods with speakable names?~~ (answer: no copying, LDM 2025-05-05)
- ~~Should `<param>` element corresponding to receiver parameter be copied from extension container for instance methods? Anything else should be copied from container to implementation methods (`<typeparam>` etc.) ?~~ (answer: no copying, LDM 2025-05-05)
- ~~Should `<param>` for extension parameter be allowed on extension members as an override?~~ (answer: no, for now, LDM 2025-05-05)
- Will the summary on extension blocks would appear anywhere?

#### CREF

- ~~Confirm syntax~~  (answer: proposal is good, LDM 2025-06-09)
- ~~Should it be possible to refer to an extension block (`E.extension(int)`)?~~ (answer: no, LDM 2025-06-09)
- ~~Should it be possible to refer to a member using an unqualified syntax: `extension(int).Member`?~~ (answer: yes, LDM 2025-06-09)
- Should we use different characters for unspeakable name, to avoid XML escaping? (answer: defer to WG, LDM 2025-06-09)
- ~~Confirm it's okay that both references to skeleton and implementation methods are possible: `E.M` vs. `E.extension(int).M`. Both seem necessary (extension properties and portability of classic extension methods).~~ (answer: yes, LDM 2025-06-09)
- ~~Are extension metadata names problematic for versioning docs?~~ (answer: yes, we're going to move away from ordinals and use a content-based stable naming scheme)

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

</details>

