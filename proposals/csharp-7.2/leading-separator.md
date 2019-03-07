# Allow digit separator after 0b or 0x

In C# 7.2, we extend the set of places that digit separators (the underscore character) can appear in integral literals. [Beginning in C# 7.0, separators are permitted between the digits of a literal](../csharp-7.0/digit-separators.md). Now, in C# 7.2, we also permit digit separators before the first significant digit of a binary or hexadecimal literal, after the prefix.

```csharp
    123      // permitted in C# 1.0 and later
    1_2_3    // permitted in C# 7.0 and later
    0x1_2_3  // permitted in C# 7.0 and later
    0b101    // binary literals added in C# 7.0
    0b1_0_1  // permitted in C# 7.0 and later

    // in C# 7.2, _ is permitted after the `0x` or `0b`
    0x_1_2   // permitted in C# 7.2 and later
    0b_1_0_1 // permitted in C# 7.2 and later
```

We do not permit a decimal integer literal to have a leading underscore. A token such as `_123` is an identifier.
