# Extensions WG 2024-08-09

## Conversions

We reviewed proposed approach for conversions:
- extend existing conversions rather than introduce a new "extension conversion
- extend categories of "reference types", "value types", "nullable value types", "enum types"
- follow-ups: non-transitive identity conversion (`EInt1 => EInt2`), user-defined conversions

## Reference nullability

Allowing top-level nullability on underlying types is tempting for implicit scenarios, but it's going to cause problems on explicit scenarios.
If we define `explicit extension E for object? { }`, then do you get to say `E?`? Also would `E` be considered annotated or not?
This is already tracked as an open issue to investigate further.

## Differences between extensions and structs you could write today?

```
implicit extension JsonDoc for string { public void ExtensionMember() { } }
```
vs.
```
struct JsonDoc
{
   public static implicit operator string(JsonDoc)
   public static implicit operator JsonDoc(string)
}
```

With extensions you get the following benefits:
- identity/standard conversion
- List<[JsonDoc] string> vs. List<string>
- extension conversions
- `"".ExtensionMember()` (by virtue of `implicit`)

## Relationship between implicit and explicit?

Every extensions can be used explicitly, but only implicit ones can come into play implicitly.

## Brainstorming on naming extensions

The name "JsonDoc" brings a mental model of hierarchy or "is-a" relationship. This feels more natural for explicit usages.
But the name "StringExtension" does not bring such a mental model. This feels more natural for implicit usages.

`JsonDoc s = "";`
`StringExtension s = "";` // weird

## Disambiguation for properties

Allowing implicit extensions to be named explicitly helps for disambiguation.

`StringExtension.ExtensionMember(receiver, args)` // fallback for extension methods 

```
implicit extension StringExtension1 for string { public int Prop { get; set; } }
implicit extension StringExtension2 for string { public int Prop { get; set; } }

"".Prop // how to disambiguate?
((StringExtension1)"").Prop
```

## Concern over type explosions

```
static class MemoryExtension
{
  public static void M1(this Type1)
  public static void M2(this Type2)
}
```
You would need to split this into two extension types. One for Type1 and the other for Type2.

