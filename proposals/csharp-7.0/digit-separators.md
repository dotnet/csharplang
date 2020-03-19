# Digit separators

Being able to group digits in large numeric literals would have great readability impact and no significant downside. 

Adding binary literals (#215) would increase the likelihood of numeric literals being long, so the two features enhance each other. 

We would follow Java and others, and use an underscore `_` as a digit separator. It would be able to occur everywhere in a numeric literal (except as the first and last character), since different groupings may make sense in different scenarios and especially for different numeric bases:

```csharp
int bin = 0b1001_1010_0001_0100;
int hex = 0x1b_a0_44_fe;
int dec = 33_554_432;
int weird = 1_2__3___4____5_____6______7_______8________9;
double real = 1_000.111_1e-1_000;
```

Any sequence of digits may be separated by underscores, possibly more than one underscore between two consecutive digits. They are allowed in decimals as well as exponents, but following the previous rule, they may not appear next to the decimal (`10_.0`), next to the exponent character (`1.1e_1`), or next to the type specifier (`10_f`). When used in binary and hexadecimal literals, they may not appear immediately following the `0x` or `0b`.

The syntax is straightforward, and the separators have no semantic impact - they are simply ignored.

This has broad value and is easy to implement.
