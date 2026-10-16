# Members in Enum Declarations

Champion issue: <https://github.com/dotnet/csharplang/issues/8989>
Discussion: <https://github.com/dotnet/csharplang/discussions/8987>

## Summary

Allow users to declare methods, properties, and other members inside an enum.

```csharp
public closed enum Color
{
    Red,
    Green,
    Blue,
    Yellow,
    Cyan,
    Magenta,
    Black,
    White;

    public int ToRGB()
        => this switch
        {
            Red => 0xFF0000,
            Green => 0x00FF00,
            Blue => 0x0000FF,
            Yellow => 0xFFFF00,
            Cyan => 0x00FFFF,
            Magenta => 0xFF00FF,
            Black => 0x000000,
            White => 0xFFFFFF
        };
}
```

## Motivation

Behavior belonging to an enum can live alongside its values and be available wherever the type is used.

For [data-carrying enums](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md), these members can operate on the case represented by the enum:

```csharp
public enum struct Option<T>
{
    None,
    Some(T Value);

    public T GetValueOrDefault(T fallback)
        => this switch
        {
            Some(var value) => value,
            _ => fallback
        };
}
```

## Design

### Syntax

The body grammar below is subject to these rules: a values-only body may have a trailing comma, and a members-only body needs no leading separator. A semicolon is required between values and members and is permitted only when both are present. No comma is permitted immediately before it.

The following updates `enum_body` in [C# specification §19.2, Enum declarations](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/enums.md#192-enum-declarations). In grammar diffs, `-` marks removed lines and `+` marks added lines:

```diff
 enum_body
-    : '{' enum_member_declarations? '}'
-    | '{' enum_member_declarations ',' '}'
+    : '{' (enum_member_declarations ','?)? ';'? class_member_declaration* '}'
     ;
```

### Permitted Members

The initial design permits:

- Instance methods, computed properties and indexers, and events with explicit accessors, without additional instance storage.
- Static members, including constants, fields, and static constructors.
- Nested types, which follow their ordinary declaration rules.

User-declared instance fields, auto-properties requiring instance storage, field-like instance events, and instance constructors are not allowed. These restrictions preserve the enum's underlying value representation.

The usual accessibility, lookup, and overload rules apply. In an instance member, `this` is the enclosing enum value. User members share a declaration space with the enum's named values. Conflicts with compiler-generated members are errors.

## Implementation Considerations

The latest published CLI standard is ECMA-335, 6th edition (June 2012). [§I.8.5.2](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf#page=52) still prohibits enums from declaring methods, properties, events, or nonliteral static fields. We do not expect relaxing these restrictions to be a problem, but doing so requires runtime-team approval. The encoding and supported runtime targets remain to be determined.

## Alternative: Sibling Extension Blocks

An author can instead provide an [extension block](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md) in a sibling static class:

```csharp
namespace Example.Extensions;

public static class ColorExtensions
{
    extension(Color color)
    {
        public int ToRGB()
            => color switch
            {
                Color.Red => 0xFF0000,
                ...
            };
    }
}
```

This already supports methods and computed properties without new enum syntax. Consumers must still bring the extension namespace into scope, such as with `using Example.Extensions;`.

## Open Questions

### Separating Values from Members

Should the semicolon be required, or could the final comma after the values separate them from member declarations?

The semicolon gives a clear visual boundary. An enum value is `N` or `N = expression`. Methods, properties, fields, and nested types have distinguishable declaration syntax. Instance constructors are excluded, and static constructors start with `static`.

The alternative uses a comma instead of a semicolon to separate values from members, just as commas separate values from each other:

```csharp
enum Color
{
    Red, Green, Blue,

    public bool IsRed => this == Red;
}
```

The comma terminates a value initializer before any attributes on the next member. Without it, `[Foo]` following `Z = X.Y` could be parsed as element access.

```csharp
enum Example
{
    Z = X.Y // Missing comma: [Foo] could be parsed as part of X.Y[Foo].

    [Foo]
    public void M() { }
}
```

Member declarations would retain their ordinary syntax, with no extra separators between them. Mixed separators do have C# precedent: `for` headers use semicolons between clauses and commas within their initializer and iterator lists.
