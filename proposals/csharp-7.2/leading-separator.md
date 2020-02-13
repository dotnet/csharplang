# Allow digit separator after 0b or 0x

This proposal specifies the changes required to the [C# 7.1 (draft) Language specification](XXX) to support leading underscores in binary and hex literals. It builds on top of the changes added in 7.0 when binary literals were introduced.

## Changes to [Lexical structure](../../spec/lexical-structure.md)

### Literals

#### Integer literals

> The grammar for [integer literals](../../spec/lexical-structure.md#Integer-literals) is modified to allow one or more `_` separators before the first digit of a hex or binary literal.

```antlr
hex_digits
    : '_'? hex_digit
    | '_'? hex_digit hex_digits_and_underscores? hex_digit
    ;

binary_digits
    : '_'? binary_digit
    | '_'? binary_digit binary_digits_and_underscores? binary_digit
    ;
```

> Make the following changes to the examples:

\[Example:
```csharp
0x_abc               // hex, int
0B__111              // binary, int
```
end example\]