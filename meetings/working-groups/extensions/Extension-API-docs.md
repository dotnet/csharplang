# Extensions and API reference

Extensions introduce new requirements for our API reference pipeline. The addition of new extension member types, and the representation of extension containers require changes to the pipeline for a good customer experience:

- The docs term is "Extension Methods". That term currently means "extension methods with an instance receiver". Now, extensions can be properties, indexers, or operators. These extension members can be accessed as either an instance member on the extended type, or as a static member on the extended type.
- Readers need to know if the receiver is an instance of a type, or the type itself.
- Readers occasionally need to know the class name of holding the extension, typically for disambiguation.
- The extension container becomes an unspeakable name, and it should be elided in documentation.

The new extensions experience should be built on the framework used for the existing extension methods. The document describes the new experience as a set of enhancements to the existing extension method documentation.

<< Add navigation notes and anchors>>

## Existing Extension methods

The prototype for an extension method communicates many of the key concepts that consumers need to use these methods in their application. Consider this prototype:

```csharp
public static class SomeExtensionsAndStuff
{
    public static bool IsEmpty<T>(this IEnumerable<T> source) => source.Any() == false;
}
```

The prototype and the class declaration communicate important information to readers.

- The first parameter, noted with the `this` modifier indicates two important keys:
  - The method is an extension method.
  - The receiver is an instance of an `IEnumerable<T>`.
- The class name `SomeExtensionsAndStuff` indicates how it can be called as a static method, if multiple extension methods have signatures that create an ambiguity.

For example, users can call extension methods in two ways:

- As though it were an instance of a receiver:
  ```csharp
  bool empty = sequence.IsEmpty();
  ```
- As a static method call using the declaring type as the receiver:
  ```csharp
  bool empty = SomeExtensionsAndStuff.IsEmpty(sequence);
  ```

The presentation and navigation elements used for the docs site help users find these methods and recognize that these methods are *extension methods*. For the following notes the links are to the docs for `System.Linq.Enumerable` and the extensions on `System.Collections.Generic.IEnumerable<T>` as they exist today:

- The Table of Contents (TOC) nodes for the extended type, such as [`IEnumerable<T>`](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1), list only the members defined on the interface. In other words, none of the extension methods are listed in the TOC (left navigation pane) under the extended type.
- The API docs build system generates a section on the type page for the extended type, such as `IEnumerable<T>`, that lists all [extension methods](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1#extension-methods). This section uses the following format:
  - The prototypes show the `this` modifier, indicating that they are extension methods.
  - Each overload has a separate entry in the section.
  - The signatures indicate if the method is a specialization (`Sum(this IEnumerable<int>)`) or generic (`Where<T>(this IEnumerable<T> source, Func<T, bool> predicate)`).
  - The section lists all extensions, from all extending types. They are grouped by type, then sorted alphabetically. **We would like this to change, and have the extension methods sorted alphabetically, without regard to the containing class.**
  - The signature does not show any indication of the extending type.
  - The docs pipeline generates this section on the extended type from the descriptions of the extension methods. The `///` comments on the extended type (for example, `System.Collections.Generic.IEnumerable<T>`) doesn't need to include all extension methods in the entire library.

In source, the existing `///` elements on the extending type and the extension method declarations enable this presentation.

## Docs presentation for C# 14 extension members

The presentation for C# 14 extensions needs to account for several new types of extension members:

- Extension properties.
- Extension indexers.
- Extension operators.
- Extension members whose receiver is a *type* rather than an *instance of a type*. In other words, extension members that are meant to accessed as though they are static members of the extended type. These can include methods, properties, and indexers.

The current format for extension methods should be extended as follows:

### Extension member prototypes

When an extension member prototype is shown, the format should show the extension container:

```csharp
extension<T>(IEnumerable<T> source)
{
    public bool IsEmpty { get; }
}
```

The `source` parameter is referred to as the *receiver parameter*, or *receiver*. The *receiver parameter* may be an instance, as shown above, or it may be a type, as in the following:

```csharp
extension<T>(IEnumerable<T>)
{
    public static IEnumerable<T> Create(int size, Func<int, T> factory);
}
```

The *receiver parameter* includes a parameter name when the receiver is an instance. The *receiver parameter* doesn't include a parameter name when the receiver is a type.

This presentation enables the following:

- Readers see the new extension syntax when consuming new extensions, driving awareness and adoption.
- Readers can quickly distinguish a new-style extension (noted by the receiver parameter), and existing extension methods (noted by the `this` modifier on the first parameter).
- The `extension` container indicates that the member is an extension member.
- The parameter on the `extension` node indicates the type extended, and provides a key to know if the member is intended to extend an instance or extend the type.
- The prototype only includes the `static` modifier if it would be present in source, as in an operator declaration:
  ```csharp
  extension<T>(IEnumerable<T>)
  {
      public static IEnumerable<T> operator + (IEnumerable<T> left, IEnumerable<T> right);
  }
  ```

### Extension members in the extended type's page

The API docs build system generates the section on the type page for the extended type that lists all extension members. This section should have sub-sections for *extension methods*, *extension properties*, and *extension operators*. Extension indexers should follow the format for indexers, and be listed as an `Item[]` property. There isn't a `this` modifier on the first parameter. In fact, the receiver is declared on the extension, not the member. The prototypes in this section should expand to show the `extension` container, as follows:

- The prototypes are displayed as described in the previous section.
- Otherwise, the format is consistent with the current format:
  - Each overload has a separate entry in the section.
  - The signatures indicate if the method is a specialization (`extension(IEnumerable<int> source) { ... }`) or generic (`extension<T>(IEnumerable<T> source) { ... }`).
  - The section lists all extensions, from all extending types. They are sorted alphabetically, as proposed for current extension methods.
  - The signature does not show any indication of the extending type.

### Extension class page

The page for the class containing extensions will need only minimal updates in how extension members are displayed. The `static` classes that contain extension methods are classes, and could already define static properties, indexers, and operators. The additional work involves understanding the [unspeakable extension type](#unspeakable-extension-type) that contains new extension members.

- The TOC node for the class will typically have additional nodes for **Properties** (including indexers), and **operators**. Classes already support this, so it should already work. Note that the node for methods displays *method groups*, not *individual overloads*. That should remain.
- The page should also have sections for **Properties**, and **operators**.
- The prototypes for extensions should be displayed as shown [above](#extension-member-prototypes).

### Extension member page

There should be a new style for extension members. This should be modeled after the existing member template, with the following changes:

- The receiver type should be shown in the title and the header block.
- The receiver parameter should have its own block. It should precede the other parameter block.
- The prototype for the member should follow the format shown [above](#extension-member-prototypes). The receiver parameter is named for extensions whose receiver is an instance. The name is not included for extensions where the receiver is a type.

The emphasis on the receiver parameter reinforces the new syntax, and is necessary for readers to see the extended type on the new extension member.

### Unspeakable extension type

The compiler generates a nested type with an unspeakable name, and an unspeakable member, for each extension container. See the example under [implementation](https://github.com/dotnet/csharplang/blob/main/proposals/extensions.md#implementations) in the feature spec.

The unspeakable nested type requires the following behavior:

- The unspeakable type, for example `<>E__1<T>`, should not be displayed in the API docs.
- The unspeakable member, for example `public static <Extension>$(IEnumerable<T> source)` should not be displayed in the API docs. However, the single parameter for this method defines the receiver type and name for all extensions in this container.
- The public members of the unspeakable nested class provides the prototypes for all extensions. Those are important in the docs.

### XML comments on the receiver parameter

Developers use the `<param>` and (optionally) `<typeparam>` tags on the `extension` container to provide information about the receiver type.

The compiler needs to emit these nodes somewhere in the XML output. Suggestion: On the unspeakable member that notes the receiver. The pipeline needs to read that XML and emit the proper HTML.

## Disambiguation and API docs

A disambiguation syntax is required when more than one `class` declares extension members with the same signature. Consumers must use a static syntax to specify which method should be called. We believe this is the less common case. However, it is common enough that our docs presentation should clearly display the class where an extension member is declared.

The C# LDM hasn't finalized the [disambiguation syntax](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/extensions/disambiguation-syntax-examples.md). The final disambiguation syntax shouldn't impact the API generation pipeline. We will demonstrate it in docs.
