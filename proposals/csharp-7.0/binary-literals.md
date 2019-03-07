# Binary literals

There’s a relatively common request to add binary literals to C# and VB. For bitmasks (e.g. flag enums) this seems genuinely useful, but it would also be great just for educational purposes.

Binary literals would look like this:

```csharp
int nineteen = 0b10011;
```

Syntactically and semantically they are identical to hexadecimal literals, except for using `b`/`B` instead of `x`/`X`, having only digits `0` and `1` and being interpreted in base 2 instead of 16.

There’s little cost to implementing these, and little conceptual overhead to users of the language.

## Syntax

The grammar would be as follows:

```antlr
integer-literal:
    : ...
    | binary-integer-literal
    ;
binary-integer-literal:
    : `0b` binary-digits integer-type-suffix-opt
    | `0B` binary-digits integer-type-suffix-opt
    ;
binary-digits:
    : binary-digit
    | binary-digits binary-digit
    ;
binary-digit:
    : `0`
    | `1`
    ;
```
