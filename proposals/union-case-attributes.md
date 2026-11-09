# Attributes on union case types

Champion issue: <https://github.com/dotnet/csharplang/issues/10415>

## Summary
[summary]: #summary

Allow attributes on individual case types in a union declaration, applying them to the corresponding synthesized constructor parameters.

## Motivation
[motivation]: #motivation

Annotating constructor parameters currently requires implementing a union type by hand rather than using a union declaration.
Supporting attributes on case types removes that need.

## Detailed design
[design]: #detailed-design

This proposal extends [Union declarations](csharp-15.0/unions.md#union-declarations).

### Syntax

Update the *case_types* production to allow attributes before each case type:

```diff
 case_types
-    : type (',' type)*
+    : case_type (',' case_type)*
+    ;
+case_type
+    : attributes? type
     ;
```

### Lowering

Attributes on a case type are applied to the parameter of its synthesized union constructor.
The default attribute target is `param`; an explicit `param:` target is also permitted.
Attribute binding and validation follow the same rules as for an explicitly declared constructor parameter.

For example:

```csharp
public union Pet([A] Cat, [B] Dog);
```

Lowers to:

```csharp
[Union]
public struct Pet : IUnion
{
    public Pet([A] Cat value) => Value = value;
    public Pet([B] Dog value) => Value = value;

    public object? Value { get; }
}
```
