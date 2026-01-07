# Extra accessor in property override

Champion issue: TBD

Many thanks to [@MrJul](https://github.com/MrJul) for putting together the original proposal and investigating the implementation.

## Summary

An overriding property may now introduce an accessor that is not present in the base property:

```cs
public class RenderBlock
{
    public abstract int Width { get; }
}

// While some implementations will not add an accessor...
public class AutoSizeText : RenderBlock
{
    public override int Width => CalculateWidthFromText();
}

// Others can!
public class Rectangle : RenderBlock
{
    public override int Width { get; set; }
}
```

This brings parity with interface implementation, and it produces metadata that is already valid.

## Motivation

This is one of the higher upvoted requests on csharplang, with 149 positive reactions and no negative reactions. The feature allows the C# language to express a valid IL construct by simply removing compile-time errors preventing the addition of an accessor when overriding.

It is possible and sometimes useful to add an accessor when implementing an interface property. However, this is not possible when overriding a class property, despite its usefulness. This difference between implementing an interface property and overriding a class property is a wart with no benefits. APIs can work around this wart declaring a `SetXyz` method next to an overridden get-only `Xyz` property. The odd API pattern is highly visible and it is a reason to avoid class inheritance in favor of interface implementation:

```cs
// Today's workaround is a disconnected accessor-as-standalone-method.
// The API is affected for all users of the 'Rectangle' class and its own derived classes.
// Compare to the code sample above.
public class Rectangle : RenderBlock
{
    private int _width;

    public override int Width => _width;

    public virtual int SetWidth(int value) => _width = value;
}
```

There is no essential reason why class inheritance should come with the limitation of disallowing an extra property accessor. Removing the compile-time errors thus removes a wart in the language at low cost. The compiler's code generation is already working as desired when the errors are removed, and consumption of such properties already works.

This feature is similar in spirit to the [covariant returns](csharp-9.0/covariant-returns.md) feature. Covariant returns allow a get-only property to change its return type when being overridden. This allows a derived class to tailor the overridden property to the behavior of the derived class without losing the relationship to the base property. This feature expands the tailoring options, not only to allow the property type to be more relevant to the derived class, but also to allow the property's readability or writeability to be more relevant to the derived class.

## Detailed design

The following compiler errors are removed:

- CS0545: cannot override because 'property' does not have an overridable get accessor
- CS0546: cannot override because 'property' does not have an overridable set accessor

Once these errors are removed, no further changes are expected to be needed in code generation or in consumption. Peverify accepts the resulting compiler output. This valid metadata is also consumed as expected by the C# compiler. This was demonstrated around ten years ago.

When an overriding property adds an accessor that is not present on the base property, the additional accessor behaves just like a non-additional accessor. For example:

- Both accessors may be auto-implemented or use the `field` keyword.
- The property may be partial. Both partial parts must still agree on the accessors that are being declared for this property.
- Both accessors must be overridden in a second-level derived class if the property is `abstract override`.
- Neither accessor may be overridden in a second-level derived class if the property is `sealed override`.
- A second-level derived class may call the added accessor directly via `base`.
- Accessibility may differ between the accessors. (Accessibility of the accessor still cannot be wider than the accessibility of the property itself.)
- An extra accessor may be added even when the property has a [covariant return type](csharp-9.0/covariant-returns.md).

These behaviors are all natural effects of the presence of the extra accessor in the property definition.

## Specification

Insertions are in **bold**, deletions are in ~~strikethrough~~.

[§15.7.6 Virtual, sealed, override, and abstract accessors](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#1576-virtual-sealed-override-and-abstract-accessors) will be updated as follows:

> If the inherited property has only a single accessor (i.e., if the inherited property is read-only or write-only), the overriding property shall include ~~only~~ that accessor, **and can optionally define an extra accessor.** If the inherited property includes both accessors (i.e., if the inherited property is read-write), the overriding property can include either a single accessor or both accessors.
>
> **If an extra accessor is defined, there are no restrictions on its definition with respect to the property being an override. If the property itself is virtual, sealed, or abstract, the extra accessor is respectively virtual, sealed, or abstract. The extra accessor may be declared with a narrower accessibility than the property itself. A overriding property that declares an extra accessor may be an auto-property ([§15.7.4 Automatically implemented properties](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#1574-automatically-implemented-properties)). The extra accessor may be declared even when the property uses a covariant return type ([§TBD covariant returns](csharp-9.0/covariant-returns.md)).**

## Alternatives

Doing nothing is an alternative. Today's workaround is to declare a new `SetXyz` method next to the getter-only `Xyz` property. This odd API pattern is a highly visible artifact caused by the choice of class inheritance to polymorphically access the property rather than interface implementation. There is no inherent necessity for class inheritance to come with the potential of forcing this odd API pattern.
