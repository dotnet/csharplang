# Members in Enum Declarations

## Summary

Allow users to declare methods, properties, and other members inside an enum, after a semicolon separating them from the enum's values.

```csharp
public enum Color
{
    Red,
    Green,
    Blue,
    Yellow,
    Cyan,
    Magenta,
    Black,
    White;

    public int ToRGB() => this switch
    {
        Red => 0xFF0000,
        Green => 0x00FF00,
        Blue => 0x0000FF,
        Yellow => 0xFFFF00,
        Cyan => 0x00FFFF,
        Magenta => 0xFF00FF,
        Black => 0x000000,
        White => 0xFFFFFF,
        _ => throw new InvalidOperationException("Unknown color.")
    };
}
```

## Motivation

Behavior belonging to an enum can live alongside its values and be available wherever the type is used. This follows the earlier discussion of [methods in enum declarations](https://github.com/dotnet/csharplang/discussions/8987).

For [data-carrying enums](https://github.com/dotnet/csharplang/blob/main/proposals/data-carrying-enums.md), these members can operate on the active case:

```csharp
public enum struct Option<T>
{
    None,
    Some(T value);

    public T GetValueOrDefault(T fallback) => this switch
    {
        Some(var value) => value,
        _ => fallback
    };
}
```

## Design

### Syntax

The existing values-only form remains valid. A member section can be empty and can follow an empty value list or a list with a trailing comma.

The following extends `enum_body` in [C# specification §19.2, Enum declarations](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/enums.md#192-enum-declarations). In grammar diffs, `+` marks added alternatives:

```diff
 enum_body
     : '{' enum_member_declarations? '}'
     | '{' enum_member_declarations ',' '}'
+    | '{' enum_member_declarations? ';' class_member_declaration* '}'
+    | '{' enum_member_declarations ',' ';' class_member_declaration* '}'
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

The [CLI restrictions on enums](https://github.com/dotnet/csharplang/discussions/8987#discussioncomment-11754496) prohibit methods, properties, events, and nonliteral static fields. Direct emission would require runtime support; a translation through helper types is another possibility. The encoding and supported runtime targets remain to be determined.

## Alternative: Sibling Extension Blocks

An author can instead provide an [extension block](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md) in a sibling static class:

```csharp
namespace Example.Extensions;

public static class ColorExtensions
{
    extension(Color color)
    {
        public int ToRGB() => color switch
        {
            Color.Red => 0xFF0000,
            ...
        };
    }
}
```

This already supports methods and computed properties without new enum syntax. Consumers must still bring the extension namespace into scope, such as with `using Example.Extensions;`.
