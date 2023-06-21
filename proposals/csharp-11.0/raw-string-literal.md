# Raw string literal

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
Allow a new form of string literal that starts with a minimum of three `"""` characters (but no maximum), optionally followed by a `new_line`, the content of the string, and then ends with the same number of quotes that the literal started with.  For example:

```
var xml = """
          <element attr="content"/>
          """;
```

Because the nested contents might itself want to use `"""` then the starting/ending delimiters can be longer like so:

```
var xml = """"
          Ok to use """ here
          """";
```

To make the text easy to read and allow for indentation that developers like in code, these string literals will naturally remove the indentation specified on the last line when producing the final literal value.  For example, a literal of the form:

```
var xml = """
          <element attr="content">
            <body>
            </body>
          </element>
          """;
```

Will have the contents:

```
<element attr="content">
  <body>
  </body>
</element>
```

This allows code to look natural, while still producing literals that are desired, and avoiding runtime costs if this required the use of specialized string manipulation routines.

If the indentation behavior is not desired, it is also trivial to disable like so:

```
var xml = """
          <element attr="content">
            <body>
            </body>
          </element>
""";
```

A single line form is also supported.  It starts with a minimum of three `"""` characters (but no maximum), the content of the string (which cannot contain any `new_line` characters), and then ends with the same number of quotes that the literal started with.  For example:

```
var xml = """<summary><element attr="content"/></summary>""";
```

Interpolated raw strings are also supported.  In this case, the string specifies the number of braces needed to start an interpolation (determined by the number of dollar signs present at the start of the literal).  Any brace sequence with fewer braces than that is just treated as content.  For example:

```
var json = $$"""
             {
                "summary": "text",
                "length" : {{value.Length}},
             };
             """
```

## Motivation

C# lacks a general way to create simple string literals that can contain effectively any arbitrary text.  All C# string literal forms today need some form of escaping in case the contents use some special character (always if a delimiter is used).  This prevents easily having literals containing other languages in them (for example, an XML, HTML or JSON literal).  

All current approaches to form these literals in C# today always force the user to manually escape the contents.  Editing at that point can be highly annoying as the escaping cannot be avoided and must be dealt with whenever it arises in the contents.  This is particularly painful for regexes, especially when they contain quotes or backslashes.  Even with a `@""` string, quotes themselves must be escaped leading to a mix of C# and regex interspersed. `{` and `}` are similarly frustrating in `$""` strings.

The crux of the problem is that all our strings have a fixed start/end delimiter.   As long as that is the case, we will always have to have an escaping mechanism as the string contents may need to specify that end delimiter in their contents.  This is particularly problematic as that delimiter `"` is exceedingly common in many languages.

To address this, this proposal allows for flexible start and end delimiters so that they can always be made in a way that will not conflict with the content of the string.

## Goals

1. Provide a mechanism that will allow *all* string values to be provided by the user without the need for *any* escape-sequences whatsoever.  Because all strings must be representable without escape-sequences, it must always be possible for the user to specify delimiters that will be guaranteed to not collide with any text contents.
2. Support interpolations in the same fashion.  As above, because *all* strings must be representable without escapes, it must always be possible for the user to specify an `interpolation` delimiter that will be guaranteed to not collide with any text contents.  Importantly, languages that use our `interpolation` delimiter characters (`{` and `}`) should feel first-class and not painful to use.
3. Multiline string literals should look pleasant in code and should not make indentation within the compilation unit look strange.  Importantly, literal values that themselves have no indentation should not be forced to occupy the first column of the file as that can break up the flow of code and will look unaligned with the rest of the code that surrounds it.
    * This behavior should be easy to override while keeping literals clear and easy to read.
4. For all strings that do not themselves contain a `new_line` or start or end with a quote (`"`) character, it should be possible to represent the string literal itself on a single line.
    - Optionally, with extra complexity, we could refine this to state that: For all strings that do not themselves contain a `new_line` (but can start or end with a quote `"` character), it should be possible to represent the string literal itself on a single line.  For more details see the expanded proposal in the `Drawbacks` section.

## Detailed design (non-interpolation case)

We will add a new `string_literal` production with the following form:

```
string_literal
    : regular_string_literal
    | verbatim_string_literal
    | raw_string_literal
    ;

raw_string_literal
    : single_line_raw_string_literal
    | multi_line_raw_string_literal
    ;

raw_string_literal_delimiter
    : """
    | """"
    | """""
    | etc.
    ;

raw_content
    : not_new_line+
    ;

single_line_raw_string_literal
    : raw_string_literal_delimiter raw_content raw_string_literal_delimiter
    ;

multi_line_raw_string_literal
    : raw_string_literal_delimiter whitespace* new_line (raw_content | new_line)* new_line whitespace* raw_string_literal_delimiter
    ;

not_new_line
    : <any unicode character that is not new_line>
    ;
```

The ending delimiter to a `raw_string_literal` must match the starting delimiter.  So if the starting delimiter is `"""""` the ending delimiter must be that as well.  

The above grammar for a `raw_string_literal` should be interpreted as:

1. It starts with at least three quotes (but no upper bound on quotes).
2. It then continues with contents on the same line as the starting quotes.  These contents on the same line can be blank, or non-blank. 'blank' is synonymous with 'entirely whitespace'.
3. If the contents on that same line is non-blank no further content can follow. In other words the literal is required to end with the same number of quotes on that same line.
4. If the contents on the same line is blank, then the literal can continue with a `new_line` and some number of subsequent content lines and `new_line`s.
    - A content line is any text except a `new_line`.
    - It then ends with a `new_line` some number (possibly zero) of `whitespace` and the same number of quotes that the literal started with.

## Raw string literal value

The portions between the starting and ending `raw_string_literal_delimiter` are used to form the value of the `raw_string_literal` in the following fashion:

* In the case of `single_line_raw_string_literal` the value of the literal will exactly be the contents between the starting and ending `raw_string_literal_delimiter`.
* In the case of `multi_line_raw_string_literal` the initial `whitespace* new_line` and the final `new_line whitespace*` is not part of the value of the string.  However, the final `whitespace*` portion preceding the `raw_string_literal_delimiter` terminal is considered the 'indentation whitespace' and will affect how the other lines are interpreted.
* To get the final value the sequence of `(raw_content | new_line)*` is walked and the following is performed:
    * If it a `new_line` the content of the `new_line` is added to the final string value.
    * If it is not a 'blank' `raw_content` (i.e. `not_new_line+` contains a non-`whitespace` character):
        * the 'indentation whitespace' must be a prefix of the `raw_content`.  It is an error otherwise.
        * the 'indentation whitespace' is stripped from the start of `raw_content` and the remainder is added to the final string value.
    * If it is a 'blank' `raw_content` (i.e. `not_new_line+` is entirely `whitespace`):
        * the 'indentation whitespace' must be a prefix of the `raw_content` or the `raw_content` must be a prefix of of the 'indentation whitespace'.  It is an error otherwise.
        * as much of the 'indentation whitespace' is stripped from the start of `raw_content` and any remainder is added to the final string value.

## Clarifications:

1. A `single_line_raw_string_literal` is not capable of representing a string with a `new_line` value in it.  A `single_line_raw_string_literal` does not participate in the 'indentation whitespace' trimming.  Its value is always the exact characters between the starting and ending delimiters.  

2. Because a `multi_line_raw_string_literal` ignores the final `new_line` of the last content line, the following represents a string with no starting `new_line` and no terminating `new_line`

```
var v1 = """
         This is the entire content of the string.
         """
```

This maintains symmetry with how the starting `new_line` is ignored, and it also provides a uniform way to ensure the 'indentation whitespace' can always be adjusted. To represent a string with a terminal `new_line` an extra line must be provided like so:

```
var v1 = """
         This string ends with a new line.

         """
```

3. A `single_line_raw_string_literal` cannot represent a string value that starts or ends with a quote (`"`) though an augmentation to this proposal is provided in the `Drawbacks` section that shows how that could be supported.

4. A `multi_line_raw_string_literal` starts with `whitespace* new_line` following the initial `raw_string_literal_delimiter`.  This content after the delimiter is entirely ignored and is not used in any way when determining the value of the string.  This allows for a mechanism to specify a `raw_string_literal` whose content starts with a `"` character itself.  For example:

```
var v1 = """
         "The content of this string starts with a quote
         """
```

5. A `raw_string_literal` can also represent content that end with a quote (`"`).  This is supported as the terminating delimiter must be on its own line. For example:

```
var v1 = """
         "The content of this string starts and ends with a quote"
         """
```

```
var v1 = """
         ""The content of this string starts and ends with two quotes""
         """
```

5. The requirement that a 'blank' `raw_content` be either a prefix of the 'indentation whitespace' or the 'indentation whitespace' must be a prefix of it helps ensure confusing scenarios with mixed whitespace do not occur, especially as it would be unclear what should happen with that line.  For example, the following case is illegal:

```
var v1 = """
         Start
<tab>
         End
         """
```

6. Here the 'indentation whitespace' is nine space characters, but the 'blank' `raw_content` does not start with a prefix of that.  There is no clear answer as to how that `<tab>` line should be treated at all.  Should it be ignored?  Should it be the same as `.........<tab>`?  As such, making it illegal seems the clearest for avoiding confusion.

7. The following cases are legal though and represent the same string:

```
var v1 = """
         Start
<four spaces>
         End
         """
```

```
var v1 = """
         Start
<nine spaces>
         End
         """
```

In both these cases, the 'indentation whitespace' will be nine spaces.  And in both cases, we will remove as much of that prefix as possible, leading the 'blank' `raw_content` in each case to be empty (not counting every `new_line`).  This allows users to not have to see and potentially fret about whitespace on these lines when they copy/paste or edit these lines.

8. In the case though of:

```
var v1 = """
         Start
<ten spaces>
         End
         """
```

The 'indentation whitespace' will still be nine spaces.  Here though, we will remove as much of the 'indentation whitespace' as possible, and the 'blank' `raw_content` will contribute a single space to the final content.  This allows for cases where the content does need whitespace on these lines that should be preserved.

9. The following is technically not legal:

```
var v1 = """
         """
```

This is because the start of the raw string must have a `new_line` (which it does) but the end must have a `new_line` as well (which it does not).  The minimal legal `raw_string_literal` is:

```
var v1 = """

         """
```

However, this string is decidedly uninteresting as it is equivalent to `""`.

## Indentation examples

The 'indentation whitespace' algorithm can be visualized on several inputs like so:

### Example 1 - Standard case
```
var xml = """
          <element attr="content">
            <body>
            </body>
          </element>
          """;
```

is interpreted as

```
var xml = """
          |<element attr="content">
          |  <body>
          |  </body>
          |</element>
           """;
```

### Example 2 - End delimiter on same line as content.

```
var xml = """
          <element attr="content">
            <body>
            </body>
          </element>""";
```

This is illegal.  The last content line must end with a `new_line`.


### Example 3 - End delimiter before start delimiter

```
var xml = """
          <element attr="content">
            <body>
            </body>
          </element>
""";
```

is interpreted as

```
var xml = """
|          <element attr="content">
|            <body>
|            </body>
|          </element>
""";
```

### Example 4 - End delimiter after start delimiter

```
var xml = """
          <element attr="content">
            <body>
            </body>
          </element>
              """;
```

This is illegal.  The lines of content must start with the 'indentation whitespace'

### Example 5 - Empty blank line
```
var xml = """
          <element attr="content">
            <body>
            </body>

          </element>
          """;
```

is interpreted as

```
var xml = """
          |<element attr="content">
          |  <body>
          |  </body>
          |
          |</element>
           """;
```


### Example 5 - Blank line with less whitespace than prefix (dots represent spaces)
```
var xml = """
          <element attr="content">
            <body>
            </body>
....
          </element>
          """;
```

is interpreted as

```
var xml = """
          |<element attr="content">
          |  <body>
          |  </body>
          |
          |</element>
           """;
```



### Example 5 - Blank line with more whitespace than prefix (dots represent spaces)
```
var xml = """
          <element attr="content">
            <body>
            </body>
..............
          </element>
          """;
```

is interpreted as

```
var xml = """
          |<element attr="content">
          |  <body>
          |  </body>
          |....
          |</element>
           """;
```

## Detailed design (interpolation case)

Interpolations in normal interpolated strings (e.g. `$"..."`) are supported today through the use of the `{` character to start an `interpolation` and the use of an `{{` escape-sequence to insert an actual open brace character.  Using this same mechanism would violate goals '1' and '2' of this proposal.  Languages that have `{` as a core character (examples being JavaScript, JSON, Regex, and even embedded C#) would now need escaping, undoing the purpose of raw string literals.

To support interpolations we introduce them in a different fashion than normal `$"` interpolated strings.  Specifically, an `interpolated_raw_string_literal` will start with some number of `$` characters.  The count of these indicates how many `{` (and `}`) characters are needed in the content of the literal to delimit the `interpolation`.  Importantly, there continues to be no escaping mechanism for curly braces.  Rather, just as with quotes (`"`) the literal itself can always ensure it specifies delimiters for the interpolations that are certain to not collide with any of the rest of the content of the string.  For example a JSON literal containing interpolation holes can be written like so:

```c#
var v1 = $$"""
         {
            "orders": 
            [
                { "number": {{order_number}} }
            ]
         }
         """
```

Here, the `{{...}}` matches the requisite count of two braces specified by the `$$` delimiter prefix.  In the case of a single `$` that means the interpolation is specified just as `{...}` as in normal interpolated string literals.  Importantly, this means that an interpolated literal with `N` `$` characters can have a sequence of `2*N-1` braces (of the same type in a row).  The last `N` braces will start (or end) an interpolation, and the remaining `N-1` braces will just be content.  For example:

```c#
var v1 = $$"""X{{{1+1}}}Z""";
```

In this case the inner two `{{` and `}}` braces belong to the interpolation, and the outer singular braces are just content.  So the above string is equivalent to the content `X{2}Z`. Having `2*N` (or more) braces is always an error.  To have longer sequences of braces as content, the number of `$` characters must be increased accordingly.

Interpolated raw string literals are defined as:

```
interpolated_raw_string_literal
    : single_line_interpolated_raw_string_literal
    | multi_line_interpolated_raw_string_literal
    ;

interpolated_raw_string_start
    : $
    | $$
    | $$$
    | etc.
    ;

interpolated_raw_string_literal_delimiter
    : interpolated_raw_string_start raw_string_literal_delimiter
    ;

single_line_interpolated_raw_string_literal
    : interpolated_raw_string_literal_delimiter interpolated_raw_content raw_string_literal_delimiter
    ;

multi_line_interpolated_raw_string_literal
    : interpolated_raw_string_literal_delimiter whitespace* new_line (interpolated_raw_content | new_line)* new_line whitespace* raw_string_literal_delimiter
    ;

interpolated_raw_content
    : (not_new_line | raw_interpolation)+
    ;

raw_interpolation
    : raw_interpolation_start interpolation raw_interpolation_end
    ;

raw_interpolation_start
    : {
    | {{
    | {{{
    | etc.
    ;

raw_interpolation_end
    : }
    | }}
    | }}}
    | etc.
    ;

```

The above is similar to the definition of `raw_string_literal` but with some important differences.  A `interpolated_raw_string_literal` should be interpreted as:

1. It starts with at least one dollar sign (but no upper bound) and then three quotes (also with no upper bound).
2. It then continues with content on the same line as the starting quotes.  This content on the same line can be blank, or non-blank. 'blank' is synonymous with 'entirely whitespace'.
3. If the content on that same line is non-blank no further content can follow.  In other words the literal is required to end with the same number of quotes on that same line.
4. If the contents on the same line is blank, then the literal can continue with a `new_line` and some number of subsequent content lines and `new_line`s.
    - A content line is any text except a `new_line`.
    - A content line can contain multiple `raw_interpolation` occurrences at any position.  The `raw_interpolation` must start with an equal number of open braces (`{`) as the number of dollar signs at the start of the literal.
    - If 'indentation whitespace' is not-empty, a `raw_interpolation` cannot immediately follow a `new_line`.
    - The `raw_interpolation` will following the normal rules specified at [§11.7.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1173-interpolated-string-expressions).  Any `raw_interpolation` must end with the same number of close braces (`}`) as dollar signs and open braces.
    - Any `interpolation` can itself contain new-lines within in the same manner as an `interpolation` in a normal `verbatim_string_literal` (`@""`).
    - It then ends with a `new_line` some number (possibly zero) of `whitespace` and the same number of quotes that the literal started with.

Computation of the interpolated string value follows the same rules as a normal `raw_string_literal` except updated to handle lines containing `raw_interpolation`s.  Building the string value happens in the same fashion, just with the interpolation holes replaced with whatever values those expressions produce at runtime.  If the `interpolated_raw_string_literal` is converted to a `FormattableString` then the values of the interpolations are passed in their respective order to the `arguments` array to `FormattableString.Create`.  The rest of the content of the `interpolated_raw_string_literal` *after* the 'indentation whitespace' has been stripped from all lines will be used to generate `format` string passed to `FormattableString.Create`, except with appropriately numbered `{N}` contents in each location where a `raw_interpolation` occurred (or `{N,constant}` in the case if its `interpolation` is of the form `expression ',' constant_expression`).

There is an ambiguity in the above specification.  Specifically when a section of `{` in text and `{` of an interpolation abut. For example:

```
var v1 = $$"""
         {{{order_number}}}
         """
```

This could be interpreted as: `{{ {order_number } }}` or `{ {{order_number}} }`.  However, as the former is illegal (no C# expression could start with `{`) it would be pointless to interpret that way.  So we interpret in the latter fashion, where the innermost `{` and `}` braces form the interpolation, and any outermost ones form the text.  In the future this might be an issue if the language ever supports any expressions that are surrounded by braces.  However, in that case, the recommendation would be to write such a case like so: `{{({some_new_expression_form})}}`.  Here, parentheses would help designate the expression portion from the rest of the literal/interpolation.  This has precedence already with how ternary conditional expressions need to be wrapped to not conflict with the formatting/alignment specifier of an interpolation (e.g. `{(x ? y : z)}`).

Examples: (upcoming)

## Drawbacks

Raw string literals add more complexity to the language.  We already have many string literal forms already for numerous purposes.  `""` strings, `@""` strings, and `$""` strings already have a lot of power and flexibility.  But they all lack a way to provide raw contents that never need escaping.

The above rules do not support the case of 4.a:

4. ...
    - Optionally, with extra complexity, we could refine this to state that: For all strings that do not themselves contain a `new_line` (but can start or end with a quote `"` character), it should be possible to represent the string literal itself on a single line.

That's because we have no means to know that a starting or ending quote (`"`) should belong to the contents and not the delimiter itself.  If this is an important scenario we want to support though, we can add a parallel `'''` construct to go along with the `"""` form.  With that parallel construct, a single line string that start and ends with `"` can be written easily as `'''"This string starts and ends with quotes"'''` along with the parallel construct `"""'This string starts and ends with apostrophes'"""`.  This may also be desirable to support to help visually separate out quote characters, which may help when embedding languages that primarily use one quote character much more than then other.

## Alternatives

https://github.com/dotnet/csharplang/discussions/89 covers many options here.  Alternatives are numerous, but i feel stray too far into complexity and poor ergonomics.  This approach opts for simplicity where you just keep increasing the start/end quote length until there is no concern about a conflict with the string contents.  It also allows the code you write to look well indented, while still producing a dedented literal that is what most code wants.

One of the most interesting potential variations though is the use of `` ` `` (or ` ``` `) fences for these raw string literals.  This would have several benefits:
1. It would avoid all the issues with strings starting er ending with quotes.
2. It would look familiar to markdown.  Though that in itself is potentially not a good thing as users might expect markdown interpretation.
3. A raw string literal would only have to start and end with a single character in most cases, and would only need multiple in the much rarer case of contents that contain back-ticks themselves.
4. It would feel natural to extend this in the future with ` ```xml `, again akin to markdown.  Though, of course, that is also true of the `"""` form.

Overall though, the net benefit here seems small.  In keeping with C# history, i think `"` should continue to be the `string literal` delimiter, just as it is for `@""` and `$""`.

## Design meetings

### ~~Open issues to discuss~~  Resolved issues:

- [x] should we have a single line form?  We technically could do without it.  But it would mean simple strings not containing a newline would always take at least three lines.  I think we should  It's very heavyweight to force single line constructs to be three lines just to avoid escaping.

Design decision: Yes, we will have a single line form.

- [x] should we require that multiline *must* start with a newline?  I think we should.  It also gives us the ability to support things like `"""xml` in the future.

Design decision: Yes, we will require that multiline must start with a newline

- [x] should the automatic dedenting be done at all?  I think we should.  It makes code look so much more pleasant.

Design decision: Yes, automatic dedenting will be done.

- [x] should we restrict common-whitespace from mixing whitespace types?  I don't think we should.  Indeed, there is a common indentation strategy called "tab for indentation, space for alignment".  It would be very natural to use this to align the end delimiter with the start delimiter in a case where the start delimiter doesn't start on a tab stop.

Design decision: We will not have any restrictions on mixing whitespace.

- [x] should we use something else for the fences?  `` ` `` would match markdown syntax, and would mean we didn't need to always start these strings with three quotes.  Just one would suffice for the common case.

Design decision: We will use `"""`

- [x] should we have a requirement that the delimiter have more quotes than the longest sequence of quotes in the string value?  Technically it's not required.  for example:

```
var v = """
        contents"""""
        """
```

This is a string with `"""` as the delimiter.  Several community members have stated this is confusing and we should require in a case like this that the delimiter always have more characters.  That would then be:

```
var v = """"""
        contents"""""
        """"""
```

Design decision: Yes, the delimiter must be longer than any sequence of quotes in the string itself.
