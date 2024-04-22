# Partial properties
https://github.com/dotnet/csharplang/issues/6420

### Grammar

The *property_declaration* grammar [(§14.7.1)](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#1471-general) is updated as follows:

```diff
property_declaration
-    : attributes? property_modifier* type member_name property_body
+    : attributes? property_modifier* 'partial'? type member_name property_body
    ;  
```

**Remarks**: This is similar to how *method_header* [(§14.6.1)](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#1461-general) and *class_declaration* [(§14.2.1)](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#1421-general) are specified. (Note that [Issue #946](https://github.com/dotnet/csharplang/issues/946) proposes to relax the ordering requirement, and would probably apply to all declarations which allow the `partial` modifier.)

### Defining and implementing declarations
When a property declaration includes a *partial* modifier, that property is said to be a *partial property*. Partial properties may only be declared as members of partial types.

A *partial property* declaration is said to be a *defining declaration* when its accessors all have semicolon bodies, and it lacks the `extern` modifier. Otherwise, it is an *implementing declaration*.

```cs
partial class C
{
    // Defining declaration
    public partial string Prop { get; set; }

    // Implementing declaration
    public partial string Prop { get => field; set => field = value; }
}
```

Because we have reserved the syntactic form with semicolon accessor bodies for the *defining declaration*, a partial property cannot be *automatically implemented*. We therefore adjust [Automatically implemented properties (§15.7.4)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1574-automatically-implemented-properties) as follows:

> An automatically implemented property (or auto-property for short), is a non-abstract, non-extern, **non-partial,** non-ref-valued property with semicolon-only accessor bodies.

**Remarks**. It is useful for the compiler to be able to look at a single declaration in isolation and know whether it is a defining or an implementing declaration. Therefore we don't want to permit auto-properties by including two identical `partial` property declarations, for example. We don't think that the use cases for this feature involve implementing the partial property with an auto-property, but in cases where a trivial implementation is desired, we think the `field` keyword makes things simple enough.

A partial property must have one *defining declaration* and one *implementing declaration*.

**Remarks**. We also don't think it is useful to allow splitting the declaration across more than two parts, to allow different accessors to be implemented in different places, for example. Therefore we simply imitate the scheme established by partial methods.

Similar to partial methods, the attributes in the resulting property are the combined attributes of the parts are concatenated in an unspecified order, and duplicates are not removed.

Only the defining declaration of a partial property participates in lookup, similar to how only the defining declaration of a partial method participates in overload resolution.

**Remarks**. In the compiler, we would expect that only the symbol for the defining declaration appears in the member list, and the symbol for the implementing part can be accessed through the defining symbol. However, some features like nullable analysis might *see through* to the implementing declaration in order to provide more useful behavior.

```cs
partial class C
{
    public partial string Prop { get; set; }
    public partial string Prop { get => field; set => field = value; }

    public C() // warning CS8618: Non-nullable property 'Prop' must contain a non-null value when exiting constructor. Consider declaring the property as nullable.
    {
    }
}
```

### Matching signatures
The property declarations must have the same type and ref kind. The property declarations must have the same set of accessors.

The property declarations and their accessor declarations must have the same modifiers, though the modifiers may appear in a different order. This does not apply to the `extern` modifier, which may only appear on an *implementing declaration*.

A partial property is not permitted to have the `abstract` modifier.

```cs
partial class C1
{
    public partial string Prop { get; private set; }

    // Error: accessor modifier mismatch in 'set' accessor of 'Prop'
    public partial string Prop { get => field; set => field = value; }
}

partial class C2
{
    public partial string Prop { get; init; }

    // Error: implementation of 'Prop' must have an 'init' accessor to match definition
    public partial string Prop { get => field; set => field = value; }
}

partial class C3
{
    public partial string Prop { get; }

    // Error: implementation of 'Prop' cannot have a 'set' accessor because the definition does not have a 'set' accessor.
    public partial string Prop { get => field; set => field = value; }
}
```

### Indexers

Per [LDM meeting on 2nd November 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-11-02.md#partial-properties), indexers will be supported with this feature.

Indexer parameters must match in the same way that partial method parameters must match, and merging of parameter attributes across partial indexer declarations occurs in the same way that it does for partial methods.


```cs
partial class C
{
    public partial int this[int x] { get; set; }
    public partial int this[int x]
    {
        get => this._store[x];
        set => this._store[x] = value;
    }
}

// attribute merging
partial class C
{
    public partial int this[[Attr1] int x]
    {
        [Attr2] get;
        set;
    }

    public partial int this[[Attr3] int x]
    {
        get => this._store[x];
        [Attr4] set => this._store[x] = value;
    }

    // results in a merged member emitted to metadata:
    public int this[[Attr1, Attr3] int x]
    {
        [Attr2] get => this._store[x];
        [Attr4] set => this._store[x] = value;
    }
}
```

## Open Issues

### Other member kinds

A community member opened a discussion to request support for [partial events](https://github.com/dotnet/csharplang/discussions/8064). In the [LDM meeting on 2nd November 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-11-02.md#partial-properties), we decided to punt on support for events, in part because nobody at the time had requested it. We may want to revisit this question, since this request has now come in, and it has been over a year since we last discussed it.

We could also go even further in permitting partial declarations of constructors, operators, fields, and so on, but it's unclear if the design burden of these is justified, just because we are already doing partial properties.
