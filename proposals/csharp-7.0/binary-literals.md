Binary Literals
===============

There’s a relatively common request to add binary literals to C# and VB. For bitmasks (e.g. flag enums) this seems genuinely useful, but it would also be great just for educational purposes.

Binary literals would look like this:

``` c#
int nineteen = 0b10011;
```

Syntactically and semantically they are identical to hexadecimal literals, except for using `b`/`B` instead of `x`/`X`, having only digits `0` and `1` and being interpreted in base 2 instead of 16.

There’s little cost to implementing these, and little conceptual overhead to users of the language.
# Syntax

The grammar would be as follows:

> _integer-literal:_
> &emsp;  ...
> &emsp;  _binary-integer-literal_
> 
> _binary-integer-literal:_
> &emsp;  `0b`  &emsp;  _binary-digits_  &emsp;  _integer-type-suffixopt_
> &emsp;  `0B`  &emsp;  _binary-digits_  &emsp;  _integer-type-suffixopt_
> 
> _binary-digits:_
> &emsp;  _binary-digit_
> &emsp;  _binary-digits_  &emsp;  _binary-digit_
> 
> _binary-digit:_  &emsp; one of
> &emsp;  `0`  `1`
