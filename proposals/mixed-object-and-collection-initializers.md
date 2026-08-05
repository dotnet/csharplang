# Mixed object and collection initializers

Champion issue: <https://github.com/dotnet/csharplang/issues/10185>

Discussion: <https://github.com/dotnet/csharplang/discussions/10186>

## Summary

Today, a `{ ... }` initializer following `new T(...)` must be **either** an object initializer (only `Member = value` / `[args] = value`) **or** a collection initializer (only expressions that bind to `Add` calls) — never a mix of both. This proposal relaxes that restriction. A single initializer may contain any sequence of member initializers and element initializers, in any order:

```csharp
var div = new HtmlDivElement
{
    Width = "100%",
    Height = "100%",
    new HtmlSpanElement { Style = { FontColor = "red"  }, "Span 1" },
    new HtmlSpanElement { Style = { FontColor = "blue" }, "Span 2" },
};
```

Each element keeps its existing meaning: a *member_initializer* assigns to the named field/property/event (or invokes the indexer), and an *element_initializer* invokes an `Add` method on the object being initialized.

## Motivation

The most natural way to express "build an object that has some configured properties **and** a sequence of contained items" is to write the properties and the items together in a single initializer. C# already supports each half — object initializers cover the configured-properties story and collection initializers cover the contained-items story — but a single `{ ... }` can only carry one or the other.

The workaround today is to introduce an intermediate property (often named `Children` or `Items`) that is itself collection-initialized:

```csharp
var div = new HtmlDivElement
{
    Width = "100%",
    Height = "100%",
    Children =
    {
        new HtmlSpanElement { Style = { FontColor = "red"  }, Children = { "Span 1" } },
        new HtmlSpanElement { Style = { FontColor = "blue" }, Children = { "Span 2" } },
    },
};
```

The result is two extra levels of brace nesting per construction site for what is conceptually a single shape — an HTML/UI element that *is* its contents. The relaxation in this proposal lets the natural shape compile.

The feature is most visible in declarative UI / markup frameworks (WPF, Avalonia, MAUI, Windows Forms, HTML-builder libraries, etc.) where a parent control both has configurable properties and a list of children. It also helps DSL-style builders, builder objects that expose both options and items, and any type that already implements `IEnumerable` and exposes `Add`.

## Detailed design

The following updates are presented as a diff against [§12.8.16.2 – §12.8.16.4 of the C# 7 standard](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12816-the-new-operator), as updated by [compound-assignment-in-initializer-and-with](compound-assignment-in-initializer-and-with.md).

Throughout this section, ~~strikethrough~~ indicates text being removed from the existing specification, and **bold** indicates text being added. Unchanged prose is quoted verbatim for context.

### Unified initializer grammar

The grammar in [§12.8.16.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128162-object-creation-expressions) currently picks one of two mutually-exclusive forms (`object_initializer` or `collection_initializer`) at the wrapping level. Merge them into a single form whose elements may be either kind:

```diff
 object_or_collection_initializer
-    : object_initializer
-    | collection_initializer
+    : '{' initializer_element_list? '}'
+    | '{' initializer_element_list ',' '}'
     ;

- object_initializer
-     : '{' member_initializer_list? '}'
-     | '{' member_initializer_list ',' '}'
-     ;
-
- collection_initializer
-     : '{' element_initializer_list '}'
-     | '{' element_initializer_list ',' '}'
-     ;
-
- member_initializer_list
-     : member_initializer (',' member_initializer)*
-     ;
-
- element_initializer_list
-     : element_initializer (',' element_initializer)*
-     ;

+ initializer_element_list
+     : initializer_element (',' initializer_element)*
+     ;

+ initializer_element
+     : member_initializer
+     | element_initializer
+     ;
```

The productions for *member_initializer* (as updated by [compound-assignment-in-initializer-and-with](compound-assignment-in-initializer-and-with.md)) and *element_initializer* (from [§12.8.16.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128164-collection-initializers)) are unchanged by this proposal. Each element keeps its existing per-kind meaning; the only relaxation is that the wrapping list now admits any sequence of the two kinds.

### Section prose

In [§12.8.16.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128162-object-creation-expressions):

> An object creation expression can omit the constructor argument list and enclosing parentheses provided it includes ~~an object initializer or collection initializer~~ **an *object_or_collection_initializer***. Omitting the constructor argument list and enclosing parentheses is equivalent to specifying an empty argument list.

> Processing of an object creation expression that includes ~~an object initializer or collection initializer~~ **an *object_or_collection_initializer*** consists of first processing the instance constructor and then ~~processing the member or element initializations specified by the object initializer ([§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers)) or collection initializer ([§12.8.16.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128164-collection-initializers))~~ **, in lexical order, processing each *initializer_element*: a *member_initializer* per [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers), or an *element_initializer* per [§12.8.16.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128164-collection-initializers)**.

In [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers):

> ~~An object initializer specifies values for zero or more fields, properties, or indexer elements of an object.~~ **A *member_initializer* specifies a value for a field, property, event, or indexer element of the object being initialized. Any number of *member_initializer*s may appear within the enclosing *object_or_collection_initializer*, interleaved freely with *element_initializer*s ([§12.8.16.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128164-collection-initializers)).**

> ~~An object initializer consists of a sequence of member initializers, enclosed by `{` and `}` tokens and separated by commas.~~ Each *member_initializer* shall designate a target for the initialization. [… remainder of paragraph unchanged …]

Where the prose (here and in [compound-assignment-in-initializer-and-with](compound-assignment-in-initializer-and-with.md)) refers to the *enclosing member_initializer_list* — for example, in the first-form exclusivity rule — that reference becomes the *enclosing initializer_element_list*; rules quantified over *member_initializer*s in that list continue to consider only *member_initializer* elements, ignoring any interleaved *element_initializer*s.

In [§12.8.16.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128164-collection-initializers):

> ~~A collection initializer specifies the elements of a collection.~~ **An *element_initializer* contributes one element to the object being initialized by invoking an `Add` method. Any number of *element_initializer*s may appear within the enclosing *object_or_collection_initializer*, interleaved freely with *member_initializer*s ([§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers)).**

> ~~A collection initializer consists of a sequence of element initializers, enclosed by `{` and `}` tokens and separated by commas.~~ Each element initializer specifies an element to be added to the collection object being initialized [… remainder of paragraph unchanged …].

> ~~The collection object to which a collection initializer is applied~~ **When the enclosing *initializer_element_list* contains at least one *element_initializer*, the object being initialized** shall be of a type that implements `System.Collections.IEnumerable` or a compile-time error occurs. [… remainder of paragraph unchanged …]

An *element_initializer* does not discharge any `required` field or property obligation; the rule from [compound-assignment-in-initializer-and-with](compound-assignment-in-initializer-and-with.md) — that `required` is satisfied only by an `=` *member_initializer* — is unaffected by the presence of *element_initializer*s.

### Example

```csharp
public class Form : IEnumerable
{
    public string Title { get; set; }
    public event EventHandler Closing;

    public void Add(Control child) { /* ... */ }
    public IEnumerator GetEnumerator() { /* ... */ }
}

Form f = new Form
{
    Title = "Hello",
    new Label("Username:"),
    Closing += OnClose,
    new TextBox(),
};
```

has the same effect as

```csharp
Form __f = new Form();
__f.Title = "Hello";
__f.Add(new Label("Username:"));
__f.Closing += OnClose;
__f.Add(new TextBox());
Form f = __f;
```

where `__f` is an otherwise invisible and inaccessible temporary variable.

## Back-compat analysis

This is a pure extension. The new *initializer_element_list* production is a strict superset of both the previous *member_initializer_list* and *element_initializer_list*: every program that compiled before this change parses and binds identically. The relaxation only admits sequences that previously produced a parse error (a member initializer in a list otherwise composed of element initializers, or vice-versa). No expression that compiles today changes meaning.

## Open LDM questions

### Should members and elements be required to appear in groups, or may they be interleaved?

The proposal as drafted allows arbitrary interleaving:

```csharp
new Form
{
    Title = "A",
    new Label { Text = "1" },
    Closing += OnClose,           // member initializer between element initializers
    new Label { Text = "2" },
};
```

The compiler can always disambiguate, and lexical order is meaningful (Add calls and member assignments may have observable interactions). Forbidding interleaving — e.g., requiring that every *member_initializer* appear lexically before every *element_initializer* in the same list — would be a stylistic constraint with no underlying semantic justification, and would prevent legitimate ordering-sensitive patterns.

The author's recommendation is to **allow arbitrary interleaving**, and let analyzers encourage grouping where teams want it.

## Related discussions

- [Discussion #4879: Allow both collection and property initializer](https://github.com/dotnet/csharplang/discussions/4879)

## Design meetings

TBD
