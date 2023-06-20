# Allow new-lines in all interpolations

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

* [x] Proposed
* [x] Implementation: https://github.com/dotnet/roslyn/pull/56853
* [x] Specification: this file.

## Summary
[summary]: #summary

The language today non-verbatim and verbatim interpolated strings (`$""` and `$@""` respectively).  The primary *sensible* difference for these is that a non-verbatim interpolated string works like a normal string and cannot contain newlines in its text segments, and must instead use escapes (like `\r\n`).  Conversely, a verbatim interpolated string can contain newlines in its text segments (like a verbatim string), and doesn't escape newlines or other character (except for `""` to escape a quote itself).

This is all reasonable and will not change with this proposal.

What is unreasonable today is that we extend the restriction on 'no newlines' in a non-verbatim interpolated string *beyond* its text segments into the *interpolations* themselves.  This means, for example, that you cannot write the following:

```c#
var v = $"Count is\t: { this.Is.A.Really(long(expr))
                            .That.I.Should(
                                be + able)[
                                    to.Wrap()] }.";
```

Ultimately, the 'interpolation must be on a single line itself' rule is just a restriction of the current implementation.  That restriction really isn't necessary, and can be annoying, and would be fairly trivial to remove (see work https://github.com/dotnet/roslyn/pull/54875 to show how).   In the end, all it does is force the dev to place things on a single line, or force them into a verbatim interpolated string (both of which may be unpalatable).

The interpolation expressions themselves are not text, and shouldn't be beholden to any escaping/newline rules therin.  

## Specification change

```diff
single_regular_balanced_text_character
-    : '<Any character except / (U+002F), @ (U+0040), \" (U+0022), $ (U+0024), ( (U+0028), ) (U+0029), [ (U+005B), ] (U+005D), { (U+007B), } (U+007D) and new_line_character>'
-    | '</ (U+002F), if not directly followed by / (U+002F) or * (U+002A)>'
+    : <Any character except @ (U+0040), \" (U+0022), $ (U+0024), ( (U+0028), ) (U+0029), [ (U+005B), ] (U+005D), { (U+007B), } (U+007D)>
+    | comment
    ;
```

## LDM Discussions

https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-09-20.md
