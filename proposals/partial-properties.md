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

A *partial property* declaration is said to be a *defining declaration* when its accessors all have semicolon bodies, and it lacks the `extern` modifier. Otherwise, it is an *implementing declaration*. A *partial property* cannot be an auto-property.

```cs
partial class C
{
    // Defining declaration
    public partial string Prop { get; set; }

    // Implementing declaration
    public partial string Prop { get => field; set => field = value; }
}
```

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

## Drawbacks
[drawbacks]: #drawbacks

As always, this feature adds to the language concept count and must be weighed accordingly.

The fact that adding the `partial` modifier can change an auto-property declaration to a defining partial declaration may be confusing.

## Alternatives
[alternatives]: #alternatives

We could consider more flexible designs which permit different accessor definitions or implementations to be spread across different declarations.

We could consider introducing some special way to denote that a partial property implementation is an auto-property, separate from the `field` keyword.

We could also consider doing nothing, which means that source generators and perhaps our tooling will need to establish conventions for working around the limitations of the field-based approach.

## Unresolved questions
[unresolved]: #unresolved-questions

Should we permit partial indexers as part of this feature? It would increase orthogonality to allow this, but the word "indexer" is mentioned zero times since 2020 in the community discussion for this feature.

Similarly, should we permit other kinds of partial members like fields, events, constructors, operators, etc? The same is mentioned in [extending-partial-methods.md](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/extending-partial-methods.md#partial-on-all-members).
