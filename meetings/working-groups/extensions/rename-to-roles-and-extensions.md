# Re-rename to Roles and Extensions

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Rename `explicit extension` back to `role` and `implicit extension` back to simply `extension`.

## Motivation
[motivation]: #motivation

The proposed [Extensions](https://github.com/dotnet/csharplang/blob/main/proposals/extensions.md) feature design eliminates the previously proposed distinction between "roles" and "extensions", in recognition that they are in fact two flavors of the same feature. Instead it introduces modifiers `explicit` and `implicit` onto the shared declaration keyword `extension`.

This makes sense from a language designer economy-of-concepts point of view. However, the intended mainline *use* of the two kinds of extension differs considerably: One is explicitly used _as_ a type, whereas the other implicitly adds members to an _existing_ type. The overlap in intended usage is small, boiling down to using an implicit extension explicitly as a type for disambiguation purposes. 

We've now had some time to get experience with the design. In practice, anyone but a language designer or implementer rarely has to discuss the overall feature as a whole. Any given declaration is either an "explicit extension" or an "implicit extension", and the intended mode of use follows from that. The terms are not only long but deceptively similar, causing confusion as well as verbosity. 

We should go back to clear, separate nouns for the two features. The previously used nouns "role" and "extension" are great candidates, though the choice of terms is less important than the choice of having two of them. This will help people understand them separately, in a manner specific to their main usage patterns. It allows developers to adopt them one at a time, and not worry much about the connections between them.

Going back to a narrower use of the term "extension" also keeps it better aligned with the meaning developers are already used to from extension methods. There have been demands for "extension members" and "extension everything" ever since extension methods were added, and this use of the term matches the intuition reflected in these asks.

This is not a proposal to turn roles and extensions into completely separate features. With the rename an extension will still *also* be a role. The two features benefit hugely from having a shared core. The syntax of declarations, the type of `this` inside them, the conversions to and from the underlying type, the code generation strategies, etc., etc., all entirely overlap. Additionally, envisioned future feature evolution such as inheritance and interface implementation apply to and have compelling scenarios for both features.

It is conceivable that this step opens up opportunities where more differences between roles and extensions would be possible and beneficial. That is totally fine but is not implied here.

## Detailed design
[design]: #detailed-design

In the [proposal grammar](https://github.com/dotnet/csharplang/blob/main/proposals/extensions.md#design) change the `extension_declaration` production as follows:

``` antlr
role_declaration
    : role_modifier* ('role' | 'extension') identifier type_parameter_list? ('for' type)? type_parameter_constraints_clause* role_body
    ;
```

Rename all productions named `extension_NNN` to `role_NNN`. Throughout the proposal, update code snippets accordingly.

In prose, change "implicit extension" to "extension" and unqualified "extension" to "role". There are only two occurrences of "explicit extension" in prose; they can easily be rewritten (to e.g. "non-extension role"), and "role" can safely be used as the overarching term. It turns out that it is rare for the specification to have to talk about explicit extensions *only*, since everything that applies to them applies to implicit extensions also. 

In the [Implementation Details](https://github.com/dotnet/csharplang/blob/main/proposals/extensions.md#implementation-details) section we will want to adjust the naming of emitted attributes and members, and there are probably a few other places that would benefit from a rewording after this change.

### Examples

``` c#
// Roles intended to be used directly as types
public role Order for JsonElement
{
    public string Description => GetProperty("description").GetString()!;
}

public role Customer for JsonElement
{
    public string Name => GetProperty("name").GetString()!;
    public IEnumerable<Order> Orders => GetProperty("orders").EnumerateArray();
}

// Extensions intended to provide new function members to existing types
public extension JsonString for string
{
    private static readonly JsonSerializerOptions s_indentedOptions = new() { WriteIndented = true };

    public JsonElement ParseAsJson() => JsonDocument.Parse(this).RootElement;

    public static string CreateIndented(JsonElement element)
        => element.ValueKind != JsonValueKind.Undefined
            ? JsonSerializer.Serialize(element, s_indentedOptions)
            : string.Empty;
}
```

## Drawbacks
[drawbacks]: #drawbacks

It becomes less directly clear from syntax that extensions are just roles with more behavior.

## Alternatives
[alternatives]: #alternatives

### Alternative nouns
While "extension" is very likely the right term for "implicit extensions" because of the connection to the existing extension methods, there is more room for debate about "role". While that term does occur in literature, it is not particularly established among developers. We can essentially pick whichever term we like, and many others have been proposed.

### Allowing `extension role`

If we want the language to more directly reflect that every extension is a role, we could make the declaration syntax for extensions allow an optional `role` keyword to occur in the declaration:

``` c#
public extension role JsonString for string { ... }

// equivalent to

public extension JsonString for string { ... }
```

This would be somewhat similar to `record class` which is allowed to be abbreviated to `record`. However, for records the use of `record class` might be motivated by wanting to highlight that it is not a `record struct`. For `extension role` on the other hand there is no `extension something-else` to distinguish it from.  

We don't have great scenarios at the moment (outside disambiguation) where an extension is *also* intended to be used as a role (i.e. an explicit type), but such scenarios may come up. In those cases, it might be useful to stress this intent by explicitly putting the word `role` in the declaration, even if it doesn't have semantic impact.

This is something that could be added anytime. We could ship the feature without it, and add the ability to say `extension role` as a long form of `extension` add any future point if it seems warranted. By analogy we also shipped `record` first and then retcon'ed it to be an abbreviation of `record class` in a subsequent release when `record struct` was added.

### Implementing extensions before roles

Using separate nouns as proposed here, it may become an option to ship extensions first and roles later. While `this`-access and disambiguation for extensions use them as a named type, that is only going to be needed locally in a function body. By contrast, roles are likely to be used in signatures, and so require quite an elaborate metadata scheme for implementation.

Perhaps there's a stage where we leave out role declarations, as well as disallow extension types from occurring in signatures. The latter restriction would be similar to the one that applies to anonymous types in today's C#.

## Unresolved questions
[unresolved]: #unresolved-questions

## Design meetings

https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-09-18.md#extensions-naming
