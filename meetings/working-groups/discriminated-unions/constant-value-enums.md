# Constant Value Enums

## Summary

Extend ordinary enum declarations to support primitive constant types beyond the existing integral underlying types.

## Motivation

String-valued enums can represent names used by protocols and services, while floating-point enums can group named numeric constants. Related discussions include [#8804](https://github.com/dotnet/csharplang/discussions/8804), [#1860](https://github.com/dotnet/csharplang/issues/1860), and [#6927](https://github.com/dotnet/csharplang/discussions/6927).

## Detailed Design

### Underlying Types and Members

Permit any simple type (integral types, floating-point types, `decimal`, `bool`, and `char`) or `string` as an enum's underlying type. A type name resolving to one of these types is also permitted.

```csharp
enum Traditional : int { A = 1, B = 2 }
enum Priority : string { Low = "low", Medium = "medium", High = "high" }
enum TranscendentalConstants : double { Pi = 3.14159, E = 2.71828 }
```

Members remain named constants and cannot have parameter lists. Non-integral constant enums require explicit values for each member.

### Translation

These compile to `System.Enum` subclasses with the appropriate `value__` backing field.

### Exhaustiveness

Constant enums can compose with the [closed enums proposal](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md). An open enum permits explicit conversions from underlying values outside its declared set, so a switch over its named members still needs a fallback. For example:

```csharp
var value = priority switch
{
    Priority.Low => -1,
    Priority.Medium => 0,
    Priority.High => 1,
    _ => -1 // Unknown underlying value or a member added in a later version.
};
```

### Grammar Changes

The following updates the underlying-type grammar in [C# specification §19.2, Enum declarations](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/enums.md#192-enum-declarations). Throughout this grammar diff, `-` indicates text removed from the existing specification and `+` indicates text added.

```diff
 enum_base
-    : ':' integral_type
-    | ':' integral_type_name
+    : ':' enum_underlying_type
     ;

-integral_type_name
-    : type_name // Shall resolve to an integral type other than char
+enum_underlying_type
+    : simple_type
+    | 'string'
+    | type_name // Must resolve to one of the above
     ;
```
