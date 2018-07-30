# inheritdoc

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

<!-- One paragraph explanation of the feature. -->

## Motivation
[motivation]: #motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement,  and include examples of how the feature is used. This section can start out light before the prototyping phase but should get into specifics and corner-cases as the feature is iteratively designed and implemented. -->

I believe a good way to start here would be examining the features currently provided by SHFB for this documentation element, and then identifying ones which would be *impractical* from an implementation perspective or "not particularly needed" from an overall usage perspective.

:link: [inheritdoc](http://ewsoftware.github.io/XMLCommentsGuide/html/86453FFB-B978-4A2A-9EB5-70E118CA8073.htm) (Sandcastle XML Comments Guide)

### Syntax

I believe it's reasonable to use the same form as defined by SHFB:

```xml
<inheritdoc [cref="member"] [select="xpath-filter-expr"] />
```

This element may be placed either as a top-level element or as an inline element in a documentation comment.

### Compilation

The compiler is updated in the following ways to account for this element:

* The compiler MUST allow the use of an `inheritdoc` element as a top-level element in XML documentation comments.
* The compiler MUST allow the use of an `inheritdoc` element as an inline element in XML documentation comments.
* The compiler SHOULD report a warning if the `inheritdoc` element appears without a `cref` attribute, and no candidate for inheriting documentation exists.
* The compiler MUST evaluate the `cref` attribute value in the same manner as it does for the `see` element, and include the resolved documentation ID as the value of the attribute in the output documentation file.
* The compiler MAY report a warning if the `select` attribute is specified, but the value is not a syntactically valid XPath expression.
* The compiler SHOULD preserve the placement and form of the `inheritdoc` element in the XML documentation file. It MUST NOT replace the `inheritdoc` element with inherited documentation when writing the documentation file.

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

* The `overloads` element is never inherited when the `select` attribute is omitted. In other words, the default value for the `select` attribute is `*[not(self::overloads)]`.
* If an element includes a `cref` attribute, it is only omitted if the matching existing element has a `cref` attribute that resolves to the same documentation ID is already included
* If an element includes a `href` attribute, it is only omitted if the matching existing element has an `href` attribute with an equivalent URI
* If an element includes a `name`, `vref`, and/or `xref` attribute, it is only omitted if the matching existing element has the corresponding attribute(s) with the same value(s)
* After observing the above, an element is omitted if an existing element (already inherited or a sibling of the `inheritdoc` element) has the same element name

#### Inline `inheritdoc` elements

When an `inheritdoc` element appears inline (as opposed to the top level), the base node from which the `select` query is evaluated changes to the parent element of the `inheritdoc` element. The path to the matching node is identified by all of the following:

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
/// <item><inheritdoc select="/summary[0]/list[@type='bullet'][0]/item[1]/*[not(self::overloads)]"/></item>
/// </list>
class WithSelectAttribute { }
```

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->

