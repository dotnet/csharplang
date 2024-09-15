
# String/Character escape sequence `\e`

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
An addition of the string/character escape sequence `\e` as a shortcut/short-hand replacement
for the character code point `0x1b`, commonly known as the `ESCAPE` (or `ESC`) character.  
This character is currently accessible using one of the following escape sequences:
- `\u001b`
- `\U0000001b`
- `\x1b` (not recommended, see the picture attached at the bottom.)

With the implementation of this proposal, the following assertions should be true:
```csharp
char escape_char = '\e';

Assert.IsTrue(escape_char == (char)0x1b, "...");
Assert.IsTrue(escape_char == '\u001b', "...");
Assert.IsTrue(escape_char == '\U0000001b', "...");
Assert.IsTrue(escape_char == '\x1b', "...");
```

## Detailed design
The language syntax specification is changed as follows in section 
[6.4.5.5](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/lexical-structure.md#6455-character-literals):

```diff
fragment Simple_Escape_Sequence
-    : '\\\'' | '\\"' | '\\\\' | '\\0' | '\\a' | '\\b' | '\\f' | '\\n' | '\\r' | '\\t' | '\\v'
+    : '\\\'' | '\\"' | '\\\\' | '\\0' | '\\a' | '\\b' | '\\f' | '\\n' | '\\r' | '\\t' | '\\v' | '\\e'
    ;
```
As well as the addition of the **last line** to the following table in the specifications:

> A simple escape sequence represents a Unicode character, as described in the table below.
> 
> | **Escape sequence** | **Character name** | **Unicode code point** |
> |---------------------|--------------------|--------------------|
> | `\'`                | Single quote       | U+0027             |
> | ...                 | ...                | ...                |
> | `\e`                | Escape character   | U+001B             |
> 
> The type of a *Character_Literal* is `char`.
