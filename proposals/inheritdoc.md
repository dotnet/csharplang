# inheritdoc

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/sharwell/roslyn/inheritdoc)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

<!-- One paragraph explanation of the feature. -->

This feature allows members to inherit documentation from other members, either in whole or in part.

## Motivation
[motivation]: #motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

This feature reduces the need to copy/paste documentation comments over time. The primary advantages of the feature relate to overall maintainability of software. With fewer duplicates of documentation, the process for updating documentation in response to a change in the source code is simplified.

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement,  and include examples of how the feature is used. This section can start out light before the prototyping phase but should get into specifics and corner-cases as the feature is iteratively designed and implemented. -->

I believe a good way to start here would be examining the features currently provided by SHFB for this documentation element, and then identifying ones which would be *impractical* from an implementation perspective or "not particularly needed" from an overall usage perspective.

:link: [inheritdoc](http://ewsoftware.github.io/XMLCommentsGuide/html/86453FFB-B978-4A2A-9EB5-70E118CA8073.htm) (Sandcastle XML Comments Guide)

### Syntax

I believe it's reasonable to use the same form as defined by SHFB:

```xml
<inheritdoc [cref="member"] [path="xpath-filter-expr"] />
```

This element may be placed either as a top-level element or as an inline element in a documentation comment.

### Compilation

The compiler is updated in the following ways to account for this element:

* The compiler MUST allow the use of an `inheritdoc` element as a top-level element in XML documentation comments.
* The compiler MUST allow the use of an `inheritdoc` element as an inline element in XML documentation comments.
* The compiler SHOULD report a warning if the `inheritdoc` element appears without a `cref` attribute, and no candidate for inheriting documentation exists.
* The compiler SHOULD emit the documentation file with `inheritdoc` elements replaced by their inherited content. The compiler MAY support emitting the documentation file with `inheritdoc` elements not replaced; in this case the compiler SHOULD preserve the placement and form of the `inheritdoc` element in the XML documentation file, except with the `cref` attribute expanded to the documentation ID of the referenced member.
* The compiler MAY report a warning if the `path` attribute is specified, but the value is not a syntactically valid XPath expression.

#### Candidate for inheritance

⚠️ Some items in this list are written as existence (Boolean), while others identify the actual candidate(s). This section should be cleaned up with the understanding that the compiler only needs to care about existence for the purpose of reporting a warning as described above.

* For types and interfaces
  * The type is derived from a class which is not `System.ValueType`, `System.Enum`, `System.Delegate`, or `System.MulticastDelegate`, OR
  * The type implements a named interface which is not itself
* For constructors
  * The containing type of the constructor is derived (directly or indirectly) from a type which contains a constructor with the same signature
* For explicit interface implementations
  * The candidate is the implemented interface member
* For overriding methods (methods marked with `override`
  * The candidate is the overridden method
* For implicit interface implementations
  * The candidates are the implemented interface members

#### Impacted warnings

The following warnings, which are specific to the Roslyn implementation of a compiler for C#, should be updated to account for this feature:

##### CS1573

> Parameter 'parameter' has no matching param tag in the XML comment for 'parameter' (but other parameters do)

Care should be taken to not report this warning in the following scenario. It would be sufficient to disable this warning any time `inheritdoc` appears as a top-level documentation element.

```csharp
class Base {
  /// <param name="x">Doc for x</param>
  protected void Method1(int x) { }
}

class Derived : Base {
  /// <inheritdoc cref="Base.Method1"/>
  /// <param name="y">Doc for y</param>
  protected void Method2(int x, int y) { }
}
```

##### CS1712

> Type parameter has no matching typeparam tag in the XML comment (but other type parameters do)

This is similar to the previous case, but applies for type parameters. The example shows type parameters for a generic type, but it can also apply to generic methods.

```csharp
/// <typeparam name="T">Doc for T</param>
class Base<T> {
}

/// <inheritdoc/>
/// <typeparam name="T2">Doc for T2</param>
class Derived<T, T2> : Base<T> {
}
```

### Tools

:memo: This section defines the inheritance semantics of the `inheritdoc` element, as they would apply to the tools responsible for interpreting the meaning of `inheritdoc`. If the compiler expands `inheritdoc` during the production of a documentation file (e.g. https://github.com/dotnet/csharplang/issues/313#issuecomment-322247608), then the compiler would be considered a tool for the purposes of this section.

#### General

The expansion of an `inheritdoc` element produces a XML node set which replaces the `inheritdoc` element.

#### Inheritance rules

The inheritance rules determine the element(s) from which documentation is inherited. The behavior is unspecified if a cycle exists in these elements.

The search order is defined under the **Top-Level Inheritance Rules** section of [inheritdoc](http://ewsoftware.github.io/XMLCommentsGuide/html/86453FFB-B978-4A2A-9EB5-70E118CA8073.htm). However, the rules for determining which elements to ignore are generalized to the following:

* The `overloads` element is never inherited when the `path` attribute is omitted. In other words, the default value for the `path` attribute is `*[not(self::overloads)]`.
* If an element includes a `cref` attribute, it is only omitted if the matching existing element has a `cref` attribute that resolves to the same documentation ID is already included
* If an element includes a `href` attribute, it is only omitted if the matching existing element has an `href` attribute with an equivalent URI
* If an element includes a `name`, `vref`, and/or `xref` attribute, it is only omitted if the matching existing element has the corresponding attribute(s) with the same value(s)
* After observing the above, an element is omitted if an existing element (already inherited or a sibling of the `inheritdoc` element) has the same element name

#### Inline `inheritdoc` elements

When an `inheritdoc` element appears inline (as opposed to the top level), the base node from which the `path` query is evaluated changes to the parent element of the `inheritdoc` element. The path to the matching node is identified by all of the following:

* The element name
* The values of attributes of the element
* The index of the element

For example, the following `inheritdoc` elements are equivalent:

```csharp
/// <summary>
/// <list type="number"><item></item></list>
/// <list type="bullet">
/// <item></item>
/// <item><inheritdoc/></item>
/// </list>
class WithoutSelectAttribute { }

/// <summary>
/// <list type="number"><item></item></list>
/// <list type="bullet">
/// <item></item>
/// <item><inheritdoc path="/summary[0]/list[@type='bullet'][0]/item[1]/*[not(self::overloads)]"/></item>
/// </list>
class WithSelectAttribute { }
```

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

This feature may result in a change in compilation behavior for users already working with external tools that process `inheritdoc` elements. While the language implementation is simplified by not providing a switch to enable/disable the new feature, it may produce a barrier to adoption for users observing semantic changes between the external tools and the new compiler support.

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

### Alternatives to `inheritdoc` in the compiler

The primary current alternative is the `include` documentation element. Documentation included from external files is already fully-supported by the language. In cases where documentation needs to be shared across multiple code elements, the content can be placed in a separate XML file which all locations reference in the same manner.

A second alternative comes in the form of external tooling, such as [Sandcastle Help File Builder](https://github.com/EWSoftware/SHFB/). External tools operating on documentation files produced by the C# compiler already recognize `inheritdoc` elements, and can resolve the referenced content at the time documentation is rendered (e.g. to a web site).

### Alternatives to specific design items

The first design for this feature required the compiler operate in "pass-through" mode, preserving the `inheritdoc` elements in the documentation output. This behavior reduced the ability of the compiler to report warnings related to the content of documentation comments, and placed additional demands on tools that process documentation comments. The updated design encourages the compile-time evaluation and replacement of `inheritdoc` elements.

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

* Syntax
    * Should the `path` attribute be supported?
    * Should the `path` attribute be named `select` instead (to match SHFB), and/or account for the fact that existing documentation may already use `select` attributes for this?
* Inheritance candidates
    * Should the inheritance candidate for constructors only look at the immediate base type, or at all inherited types?
    * How are inheritance candidates disambiguated when a member implicitly implements an interface member and overrides a member from a base class?
    * How are inheritance candidates disambiguated when a member implicitly implements multiple interface member?
    * How are inheritance candidates disambiguated when a type has both a base class and implements one or more interfaces?
* Inherited documentation
    * How should inherited documentation behave when it includes an `inheritdoc` element? This situation is especially possible since earlier versions of the C# compiler produced documentation files that preserved `inheritdoc` elements.
    * How should the compiler handle inherited documentation with a verbatim `cref`?
    * How will localization be handled?
* Conditional behavior
    * Should the C# compiler support "pass-through" mode for `inheritdoc` elements (i.e. provide a compiler switch to disable evaluation)?
    * Should the `inheritdoc` feature be tied to a specific language version?

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->

