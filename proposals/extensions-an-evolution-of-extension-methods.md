# Extensions: An Evolution of Extension Methods

* [x] Proposed
* [ ] Prototype
* [ ] Implementation
* [ ] Specification

## Summary
[summary]: #summary

This proposal presents a design for "extension everything" as an evolution of classic extension methods and allows for
future expansion.

### Goals
- Describe a new syntax for extension members that scales to member kinds beyond just instance methods.
- Provide support for all functionality available to classic extension methods.
- Guarantee binary and source compatibility for classic extension method scenarios in the new syntax.
- Design support for the highest value extension member kinds that have the best chance of being implemented in the
  C# 14 timeframe.
- Lay the design groundwork for further extension member kinds, if or when the scenarios are compelling enough
  to pursue.

### Non-Goals
- Do not consider syntax for a possible future “roles” or "explicit extensions" feature. This proposal assumes that
  “extensions” and “roles” have no dependency on one another. However, it allows for future synergy through additional
  language features.
- Do not consider syntax to allow type parameters on member kinds that do not support them today. Although, this could
  be supported by future work.

## Motivation
[motivation]: #motivation

[Extension methods](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods)
are wildly popular! They first appeared in C# 3 as a smallish feature intended to support the syntactic rewrites of
LINQ’s query expression syntax[^1]. However, over the past 15+ years, extension methods have cemented themselves as a
crucial part of the C# developer toolkit.

A key reason for extension methods' popularity is the their inherent discoverability. Once a set of extension methods
are brought into scope, they can be discovered in IntelliSense simply by typing `.` after an expression. This
discoverability makes extension methods a powerful way to define important helpers within a code base or provide public
API surface for a library. Extension methods have been used to simplify interface implementation[^2], as a tool to
layer public API surface area[^3], and even to implement domain-specific languages (DSLs)[^4].

In the ten(!) C# releases since their introduction, extension methods have received some small improvements. For
example, C# 6 added the `static` modifier for using directives, allowing a type’s static members to be brought into
scope, including extension methods. C# 7.2 introduced support for `this ref` on extension methods targeting value
types. However, in all that time, there haven’t been any new extension members kinds added to C#, though there has
certainly been a consistent stream of requests for them.

Over the years, the C# language design team has received many requests for *extension properties*. Initially, extension
properties might seem straightforward but they present several challenges. The most glaring issue is that properties
don’t have a parameter list, so there isn’t an obvious place to declare a `this` parameter. However, even if that were
solved, C# properties cannot declare generic type parameters. Without a way to define a generic type parameter on an
extension property, it would be impossible to declare an `IsEmpty` property for `IEnumerable<T>`. And of course, once
those issues are addressed (along with generic property type inference and the inevitable overload resolution work),
why would we stop at extension properties? The next step would clearly be to add instance and static generic
properties. Given that, extension properties trigger a bit of an avalanche of design issues that lead to a much larger
C# feature that feels a bit niche and only saves the programmer a pair of empty parentheses. This has never seemed
worth the investment.

In addition, there a long-standing request from the .NET libraries team for *static extension methods*. Extension
methods that are accessible through member access on a type open up new API composition scenarios. Imagine how powerful
it would be to add a package reference to a .NET project and have new static methods accessible from `string` related
to that package's domain! Unfortunately, a natural syntax to declare a static extension method that derives from
classic extension method syntax has proven elusive. After all, extension methods are already declared as static
methods. Would a static extension method be `static static` or maybe require an extra attribute? Also, the `this`
parameter wouldn’t make sense, since a static extension method wouldn’t be passed an instance of the type. Then where
would the target type go? Clearly, a new syntax is needed to support static extension methods but what happens to
classic extension methods? Are there two radically different syntaxes?

This proposal aims to solve these issues and more through a new declaration syntax specially tailored for extension
members.

## Detailed design
[design]: #detailed-design

### The Extension Container

Extension members are declared in a new type declaration called an *extension container*.

```antlr
extension_container_declaration
    : attributes? extension_container_modifier* 'partial'? 'extensions' identifier type_parameter_list? for_clause? type_parameter_constraints_clause* extension_container_body ';'?
    ;

extension_container_modifier
    : 'public'
    | 'internal'
    | unsafe_modifier   // unsafe code support
    ;
    
for_clause
    : 'for' type
    ;

extension_container_body
    : '{' extension_member_declaration* class_member_declaration* '}'
    ;

extension_member_declaration
    : extension_method_declaration
    | extension_property_declaration
    ;
```

Below are a few examples of extension containers:

```C#
extensions E
{
}

extensions E<T>
{
}

extensions E for string
{
}

extensions E<T> for T where T : IEquatable<T>
{
}
```

An extension container compiles to a type that is similar to a [static class](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/classes#15224-static-classes),
which is necessary for binary compatibility. It can't inherit from a base type, implement interfaces, and can only be
referenced in the same ways that a static class can. Using the plural `extensions` as a keyword is an important
distinction from other proposals that prefer `extension`. The `extensions` keyword makes it clear that this is a
container for members and not an entity that a programmer generally needs to be concerned with, except for situations
where disambiguation is required[^5].

Within an extension container body, there can be both extension members and regular static members (the same set that
are supported by static classes). There is no restriction on the type that an extension member can target (i.e. the
"receiver type") unless the programmer provides an optional _for-clause_. A _for-clause_ specifies a type that applies
to all extension members declared within the container. If a _for-clause_ type references a type parameter (e.g.
`IEnumerable<T>`), that type parameter must be declared on the extension container[^6].

### Instance Extension Methods

The syntax used to declare a classic extension method starts with a static method on a static class and adds a `this`
keyword to the first parameter that anoints it as the receiver type. From a conceptual point of view, the programmer
sees an extension method for what it really is: a static method that can be reduced syntactically when invoked to
appears as if it were an instance method.

Here's an example of a set of classic extension methods from Roslyn:

```C#
public static partial class Extensions
{
    public static SourceTextContainer AsTextContainer(this ITextBuffer buffer)
        => TextBufferContainer.From(buffer);

    internal static TextLine AsTextLine(this ITextSnapshotLine line)
        => line.Snapshot.AsText().Lines[line.LineNumber];
}
```

The syntax for instance extension methods within an extension container take the opposite approach. They are declared
as instance methods, but the receiver parameter is moved *before* the method name and no longer requires the `this`
modifier. In this way, the receiver parameter is given more importance and the syntax looks similar to how it is
expected to be invoked.

```C#
public partial extensions Extensions
{
    public SourceTextContainer (ITextBuffer buffer).AsTextContainer()
        => TextBufferContainer.From(buffer);

    internal TextLine (ITextSnapshotLine line).AsTextLine()
        => line.Snapshot.AsText().Lines[line.LineNumber];
}
```

When a _for-clause_ is included on the extension container, an instance extension method does not need to restate the
type from the _for-clause_. And, if there aren't any attributes or modifiers, the parentheses aren't needed, making a
syntactic connection to the single parameter form of a lambda expression.

```C#
internal partial extensions ProjectExtensions for Project
{
    public Document project.GetRequiredDocument(DocumentId documentId)
        => project.GetDocument(documentId) ?? throw new ...;

    public Document project.GetRequiredDocument(SyntaxTree tree)
        => project.GetDocument(tree) ?? throw new ...;

    public TextDocument project.GetRequiredAdditionalDocument(DocumentId documentId)
        => project.GetAdditionalDocument(documentId) ?? throw new ...;
}
```

For compatibility, all of the examples above compile to the same metadata as their equivalent classic extension method
syntax.

> [!NOTE]
> This proposal suggests a succinct syntax that allows just the receiver parameter name followed by `.`. There are
> other possibilities called out [below](#unresolved-questions).

The following grammar describes the syntax for an instance extension method declared in an extension container.

```antlr
extension_method_declaration
    : attributes? method_modifiers return_type extension_method_header method_body
    | attributes? ref_method_modifiers ref_kind ref_return_type extension_method_header ref_method_body
    ;

extension_method_header
    : receiver_parameter '.' member_name '(' parameter_list? ')'
    | receiver_parameter '.' member_name type_parameter_list '(' parameter_list? ')' type_parameter_constraints_clause*
    ;

receiver_parameter
    : '(' attributes? receiver_mode_modifier? type? identifier ')'
    | identifier
    | type
    ;

receiver_mode_modifier
    | 'ref'
    | 'ref readonly'
    | 'in'
```

### Static Extension Methods

The ability to declare a static extension method for a type is a long-standing ask from the .NET libraries team. This
provides new API layering possibilities and could provide new avenues for offering APIs down-level. Consider the 
[`string.Create(...)` method](https://learn.microsoft.com/en-us/dotnet/api/system.string.create?view=net-8.0#system-string-create-1(system-int32-0-system-buffers-spanaction((system-char-0))))
that was added in .NET Core 2.1.

```C#
public sealed partial class String
{
    public static string Create<TState>(int length, TState state, SpanAction<char, TState> action)
    {
    }
}
```

If static extension methods had been available, it would have been possible to define this method in a .NET package
that included down-level support[^7] like so.

```C#
public partial extensions Extensions
{
    public static string string.Create<TState>(int length, TState state, SpanAction<char, TState> action)
    {
    }
}
```

Like an instance extension method's receiver parameter, it is necessary to state the target type of the static
extension method before the method name[^8]. This allows the declaration syntax to align with the calling syntax. A
downside is that the type must be restated even if an optional _for-clause_ is defined, but this seems a small price to
allow regular static members alongside static extension methods.

### Extension Properties

The design for instance and static extension properties largely fall out of the design framework described used for
extension methods above.

```antlr
extension_property_declaration
    : attributes? property_modifier* type extension_property_header property_body
    | attributes? property_modifier* ref_kind type extension_property_header ref_property_body
    ;
    
extension_property_header
    : receiver_parameter '.' name
    ;
```

Here are a couple of examples selecting from existing Roslyn extension methods that could be declared as extension
properties.

```C#
internal extensions IComparerExtensions<T> for IComparer<T>
{
    public IComparer<T> comparer.Inverse => new InverseComparer<T>(comparer)
}

internal extensions ISymbolExtensions for ISymbol
{
    public bool ([NotNullWhen(true)] ISymbol? symbol).IsImplicitValueParameter
        => ...;
}

internal extensions CompilationExtensions for Compilation
{
    public INamedTypeSymbol? compilation.AttributeType
        => compilation.GetTypeByMetadataName(typeof(Attribute).FullName!);

    public INamedTypeSymbol? compilation.ExceptionType
        => compilation.GetTypeByMetadataName(typeof(Exception).FullName!);

    public INamedTypeSymbol? compilation.EqualityComparerOfTType
        => compilation.GetTypeByMetadataName(typeof(EqualityComparer<>).FullName!);

    public INamedTypeSymbol? compilation.ActionType
        => compilation.GetTypeByMetadataName(typeof(Action).FullName!);
}
```

Static extension properties may prove to be less common as static properties are less common in general. However, there
are still interesting cases! Consider the following extension method defined by the
[Fluent Assertions library](https://fluentassertions.com/typesandmethods/).

```C#
public static class AssertionExtensions
{
    public static TypeAssertions Should(this Type subject)
    {
        return new TypeAssertions(subject);
    }
}
```

That extension method is intended to be called like so:

``` C#
typeof(MyBaseClass).Should().BeAbstract();
```

If static extension properties were available when this library were defined, the Fluent Assertions DSL could have been
designed to require less ceremony.

```C#
public static class AssertionExtensions<T> for T
{
    public static TypeAssertions T.Should
    {
        return new TypeAssertions(typeof(T));
    }
}

// Usage:
MyBaseClass.Should.BeAbstract;
```

### Inference

For scenarios where the extension container doesn't declare a type parameter, existing type inference for extension
methods should be sufficient. If the extension container does declare a type parameter, an additional inference step
will be required to determine what extension containers apply for a given receiver. Consider the following code.

```C#
var numbers = new List<int>();

foreach (var text in numbers.ToFormattedStrings("x8"))
{
    Console.WriteLine(text);
}

extensions EnumerableExtensions<T> for IEnumerable<T> where T : IFormattable
{
    public IEnumerable<string> source.ToFormattedStrings(string format)
        => source.Select(x => x.ToString(format, formatProvider: null));
}
```

In this example, the compiler would need to first determine that `EnumerableExtensions<T>` is an appliable extension
type for `List<int>`. Then, applicable extension methods could be chosen and normal overload resolution would
continue.[^9]

### Disambiguation

For static extension methods and properties, disambiguation falls out. The programmer can simply call the member on the
extension container directly. For instance extension methods, it is possible to disambiguate by using the same static
invocation syntax as classic extension methods. However, it will be necessary to add a general unifying disambiguation
syntax at the call site to account for other scenarios. Below are a few strawman proposals:

#### Cast-style syntax

Because an extension container is really a type, it seems reasonable to allow it to be used with a _cast-expression_ to
disambiguate:

```C#
((Extensions)instance).Prop = 42;
Console.WriteLine(((Extensions)instance).Prop);
```

#### Invocation-style syntax

Since an extension container cannot have instance constructors, it seems reasonable to consider a syntax based on a
normal invocation.

```C#
Extensions(instance).Prop = 42;
Console.WriteLine(Extensions(instance).Prop);
```

This _might_ have some problems to sort out, but it seems possible.

#### Alias-qualified syntax

An underused C# why to qualify C# types is the `::` operator. Currently, this can be used whenever the left-hand side
is a namespace alias, extern alias, or the `global` alias. We could consider allowing the left-hand side to also be an
extension container.

```C#
Extensions::instance.Prop = 42;
Console.WriteLine(Extensions::instance.Prop);
```

Currently, the left-hand side can identifier. To support this, that operator would need to allow a fully-qualified
generic type name, which might also be alias-qualified. However, perhaps this idea might lead to others?

## Future Work

### More Member Kinds!

The goal of this proposal was to cover instance and static methods and properties, providing a design framework that
could be used to add other member kinds if the scenarios requiring them are important. Using a similar approach, it
should not be too difficult to provide syntax for other member kinds. For example:

``` C#
extensions E for int
{
    public bool number.this[int bit] => ...;

    public event Action number.NonsenseEvent
    {
        add => ...;
        remove => ...;
    }

    public static event Action int.NonsenseEvent
    {
        add => ...;
        remove => ...;
    }

    public static operator +(int x, string y) => ...;

    public static implicit operator int(string x) => ...;
}
```

Instance indexers are straightforward and would likely be useful. Events are possible, but similar to extension
properties, they would require `add` and `remove` accessors to avoid creating state that wouldn't flow with the
receiver.

Note that operators overloads and user-defined conversions are declared using the same syntax as always. It is not
legal to write an operator overload for an extension type, so the syntax is open to be used. However, one of operands
must match the type in the _for-clause_.

### Roles and Interfaces!

An alternate proposal for extensions explores a type-based approach in two related flavors: implicit and explicit
extensions. Recently, these have been re-renamed back to "extensions" (implicit extensions) and "roles" (explicit
extensions) to avoid confusion and might their conceptual differences clearer. This proposal provides a new design for
"extensions" as an evolution of classic extension methods that is fully disconnected from "roles". However, it's still
possible to bring some synergy back in the future if roles do become part of C#.

If roles manifest as light weight wrappers around an instance of an underlying type and eventually allow interface
implementation, it's possible that an extension container could leverage roles to provide a future "extension
interface". This is bit hand-wavey it's unclear what shape roles will ultimately take, but there is at least a
_possible_ universe where something like the following code could be made to work, possibly by generating an anonymous
role under-the hood.

``` C#
public extensions E for string : IDisposable
{
    public void s.Dispose()
    {
       
    }
}
```

The purpose here is not to promise anything or solve all of the potential issues (like ambiguity with explicit
interface implementation). The intention is to show that there can still be a path to synergy between extensions, as
presented by this proposal, and roles, if and when they become a part of the C# language.

## FAQ

### What about allowing `this` for member access?

Other proposals allow the programmer to use `this` within an instance extension member body to refer to the receiver
type. Additionally, unqualified member accesses implicitly binds to `this` to create the illusion that the user is
really typing in an instance method inside of a type. Unfortunately, that approach is incongruent with classic
extension methods and loses important semantic detail that might be provided by a name. For example, it's useful to
know that the `string` being operated on is actually `articleText` and not just any `string`.

In this proposal, it is assumed that extension methods are well-understood by C# programmers as fancy static methods.
So unqualified member access in an extension member should has static access within the extension container, allowing
access to all other static members and extension members. In addition, the programmer can always access the receiver
parameter in an instance extension method by name.

### Can non-method extension members declare generic type members?

No! As mentioned in the non-goals section, this proposal does not attempt to allow type parameters to be added to
extension members that can't already support them. A key reason for this is that doing the work to support type
parameters on, say, extension properties would be strange if weren't adding them for normal instance and static
properties as well. And, doing that is well-beyond the scope of extensions. If we ever allow type parameters to be
declared on regular properties, we should also allow them for extension properties.

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

- **Should the receiver parameter _always_ be parenthesized rather than allowing `<identifier>.`**?

  This is definitely something to consider. It seems reasonable to parenthesize the receiver parameter and remove
  the '.' for instance extension members.
  
  ```C#
  extensions StringExtensions for string
  {
      public bool ([NotNullWhen(false)] string? s) IsNullOrEmpty => ...;
      
      public int (text) CountWord(string word) => ...;
  }
  ```

- **Should the receiver parameter _always_ be required to state the type**?

  This is related to the question above. To some, it might seem too irregular in a method declaration to declare a
  parameter with just the name and no type. To be more regular with other top-level declarations,  the `.` for instance
  extension members as well. Merging that with the syntax above would look something like this:
  
  ```C#
  extensions StringExtensions for string
  {
      public bool ([NotNullWhen(false)] string? s) IsNullOrEmpty => ...;
      
      public int (string text) CountWord(string word) => ...;
  }
  ```

- **What syntax should be used for disambigation?**

  As shown [above](#Disambiguation), there are many syntactic possibilities for disambiguating an extension and this
  proposal only suggests a few. Conceptually, it feels like the invocation-style syntax aligns best for instance
  extensions by embracing the importance this design places on the receiver parameter. However, this is yet undecided.
  
- **Can an extension container declare generic type parameters without a _for-clause_**?
  
  This should be feasible but would need a restriction that all extension members use all of the type parameters
  somewhere in their receiver parameter or remaining parameter list. Otherwise, it will not be possible to infer the
  type arguments for the extension container from the call site, making an extension member uncallable in reduced form. 
  It seems reasonble to issue a warning if an instance extension member could would not be callable with instance
  syntax.
  
  ```C#
  extensions E<T>
  {
      public void (IComparable<T> obj) M1(); // Firne
      public int (string s) M2(IEnumerable<T> items); // Fine
      
      public T M3(); // Works with disambiguation, but issue a warning.
  }
  ```

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->

[^1]: For example, the query expression, `from x in Enumerable.Range(1, 10) select x * x` is rewritten syntactically at
compile-time as `Enumerable.Range(1, 10).Select(x => x * x)`. The `Enumerable.Select(...)` extension method ensure that
this rewrite compiles.
[^2]: Consider the [`ILogger`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger)
interface, which only has three interface members that need to be implemented. A much larger API surface is available
for an `ILogger` implementation by the 29 (as of this writing) extension methods defined by [`LoggerExtensions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loggerextensions)
in the same `Microsoft.Extensions.Logging` namespace.
[^3]: Roslyn's public API uses extension methods to provide separate API sets for C# and Visual Basic across a common
set of types.
[^4]: For an example of a domain-specific language implemented almost entirely with extensions, consider
[Fluent Assertions](https://fluentassertions.com/).
[^5]: One possible expansion of this proposal would be to allow for nameless extension containers, though such
extension containers would have no means of disambiguation.
[^6]: Taking a cue from classic extension methods, this proposal assumes that an extension container can't be nested
within another type. If that restriction is loosened, it would be possible for a _for-clause_ to reference a type
parameter from an enclosing type.
[^7]: Adding `string.Create(...)` as a static extension method might not have been advisable when it was introduced.
The point made by this proposal is that it would have been _possible_.
[^8]: This is syntactic similarity to explicitly-implemented interface members. However, since an extension container
cannot implement interfaces, this approach does not introduce an ambiguity.
[^9]: I'm pretty sure this is a *gross* oversimplification. `#notacsharpcompilerengineer`