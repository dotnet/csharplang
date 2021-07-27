# Enhanced #line directives

## Summary
[summary]: #summary 
The compiler applies the mapping defined by `#line` directives to diagnostic locations and sequence points emitted to the PDB.

Currently only the line number and file path can be mapped while the starting character is inferred from the source code. The proposal is to allow specifying full span mapping.

## Motivation
[motivation]: #motivation

DSLs that generate C# source code (such as ASP.NET Razor) can't currently produce precise source mapping using `#line` directives. This results in degraded debugging experience in some cases as the sequence points emitted to the PDB can't map to the precise location in the original source code.

For example, the following Razor code
```
@page "/"
Time: @DateTime.Now
```

generates code like so (simplified):

```C#
#line hidden
void Render()
{
   _builder.Add("Time:");
#line 2 "page.razor"
   _builder.Add(DateTime.Now);
#line hidden
}
```

The above directive would map the sequence point emitted by the compiler for the `_builder.Add(DateTime.Now);` statement to the line 2, but the column would be off (16 instead of 7).

The Razor source generator actually incorrectly generates the following code:
```C#
#line hidden
void Render()
{
   _builder.Add("Time:");
   _builder.Add(
#line 2 "page.razor"
      DateTime.Now
#line hidden
);
}
```

The intent was to preserve the starting character and it works for diagnostic location mapping. However, this does not work for sequence points since `#line` directive only applies to the sequence points that follow it. There is no sequence point in the middle of the  `_builder.Add(DateTime.Now);` statement (sequence points can only be emitted at IL instructions with empty evaluation stack). The `#line 2` directive in above code thus has no effect on the generated PDB and the debugger won't place a breakpoint or stop on the `@DateTime.Now` snippet in the Razor page. 

Issues addressed by this proposal: 
https://github.com/dotnet/roslyn/issues/43432
https://github.com/dotnet/roslyn/issues/46526

## Detailed design
[design]: #detailed-design

We amend the syntax of `line_indicator` used in `pp_line` directive like so:

Current:
```
line_indicator
    : decimal_digit+ whitespace file_name
    | decimal_digit+
    | 'default'
    | 'hidden'
    ;
```

Proposed:
```
line_indicator
    : '(' decimal_digit+ ',' decimal_digit+ ')' '-' '(' decimal_digit+ ',' decimal_digit+ ')' decimal_digit+ whitespace file_name
    | '(' decimal_digit+ ',' decimal_digit+ ')' '-' '(' decimal_digit+ ',' decimal_digit+ ')' file_name
    | decimal_digit+ whitespace file_name
    | decimal_digit+
    | 'default'
    | 'hidden'
    ;
```

That is, the `#line` directive would accept either 5 decimal numbers (_start line_, _start character_, _end line_, _end character_, _character offset_), 
4 decimal numbers (_start line_, _start character_, _end line_, _end character_), or a single one (_line_).

If _character offset_ is not specified its default value is 0, otherwise it specifies the number of UTF-16 characters. The number must be non-negative and less then length of the line following the #line directive in the unmapped file.

(_start line_, _start character_)-(_end line_, _end character_) specifies a span in the mapped file. _start line_ and _end line_ are positive integers that specify line numbers. _start character_, _end character_ are positive integers that specify UTF-16 character numbers. _start line_, _start character_, _end line_, _end character_ are 1-based, meaning that the first line of the file and the first UTF-16 character on each line is assigned number 1.

> The implementation would constraint these numbers so that they specify a valid [sequence point source span](https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md#sequence-points-blob):
> - _start line_ - 1 is within range [0, 0x20000000) and not equal to 0xfeefee.
> - _end line_ - 1 is within range [0, 0x20000000) and not equal to 0xfeefee.
> - _start character_ - 1 is within range [0, 0x10000)
> - _end character_ - 1 is within range [0, 0x10000)
> - _end line_ is greater or equal to _start line_.
> - _start line_ is equal to _end line_ then _end character_ is greater than _start character_.

> Note that the numbers specified in the directive syntax are 1-based numbers but the actual spans in the PDB are zero-based. Hence the -1 adjustments above.

The mapped spans of sequence points and the locations of diagnostics that `#line` directive applies to are calculated as follows.

Let _d_ be the zero-based number of the unmapped line containing the `#line` directive.
Let span L = (start: (_start line_ - 1, _start character_ - 1), end: (_end line_ - 1, _end character_ - 1)) be zero-based span specified by the directive.

Function M that maps a position (line, character) within the scope of the `#line` directive in the source file containing the #line directive to a mapped position (mapped line, mapped character) is defined as follows:

_M_(_l_, _c_) =

  _l_ = _d_ + 1		=> 	(_L.start.line_ + _l_ – _d_ – 1, _L.start.character_ + max(_c_ – _character offset_, 0))
  _l_ > _d_ + 1		=> 	(_L.start.line_ + _l_ – _d_ – 1, _c_)

The syntax constructs that sequence points are associated with are determined by the compiler implementation and not covered by this specification.
The compiler also decides for each sequence point its unmapped span. This span may partially or fully cover the associated syntax construct.

Once the unmapped spans are determined by the compiler the function _M_ defined above is applied to their starting and ending positions, with the exception of the ending position of all sequence points within the scope of the #line directive whose unmapped location is at line _d_ + 1 and character less than character offset. The end position of all these sequence points is _L.end_.

> Example [5.i] demonstrates why it is necessary to provide the ability to specify the end position of the first sequence point span.

> The above definition allows the generator of the unmapped source code to avoid intimate knowledge of which exact source constructs of the C# language produce sequence points. The mapped spans of the sequence points in the scope of the `#line` directive are derived from the relative position of the corresponding unmapped spans to the first unmapped span.

> Specifying the _character offset_ allows the generator to insert any single-line prefix on the first line. This prefix is generated code that is not present in the mapped file. Such inserted prefix affects the value of the first unmapped sequence point span. Therefore the starting character of subsequent sequence point spans need to be offset by the length of the prefix (_character offset_). See example [2].

![image](https://user-images.githubusercontent.com/41759/120511514-563c7e00-c37f-11eb-9436-20c2def68932.png)

### Examples

For clarity the examples use `spanof('...')` and `lineof('...')` pseudo-syntax to express the mapped span start position and line number, respectively, of the specified code snippet.

#### 1. First and subsequent spans

Consider the following code with unmapped zero-based line numbers listed on the right:

```
#line 1 10 1 15 "a"   // 3
  A();B(              // 4
);C();                // 5
    D();              // 6
 ```

d = 3
L = (0, 9)..(0, 14)

There are 4 sequence point spans the directive applies to with following unmapped and mapped spans:
(4, 2)..(4, 5) => (0, 9)..(0, 14)
(4, 6)..(5, 1) => (0, 15)..(1, 1)
(5, 2)..(5, 5) => (1, 2)..(1, 5)
(6, 4)..(6, 7) => (2, 4)..(2, 7)

#### 2. Character offset

Razor generates `  _builder.Add(` prefix of length 15 (including two leading spaces).

Razor:
```
@page "/"                                  
@F(() => 1+1, 
   () => 2+2
) 
```

Generated C#:
```C#
#line hidden
void Render()            
{ 
#line spanof('F(...)') 15 "page.razor"  // 4
  _builder.Add(F(() => 1+1,            // 5
  () => 2+2                            // 6
));                                    // 7
#line hidden
}
);
}
```

d = 4
L = (1, 1)..(3,0)
_character offset_ = 15

Spans: 
- `_builder.Add(F(…));` => `F(…)`: (5, 2)..(7, 2) => (1, 1)..(3, 0)
- `1+1` => `1+1`: (5, 23)..(5, 25) => (1, 9)..(1, 11)                                     
- `2+2` => `2+2`: (6, 7)..(6, 9) => (2, 7)..(2, 9)

#### 3. Razor: Single-line span

Razor:
```
@page "/"
Time: @DateTime.Now
```

Generated C#:
```C#
#line hidden
void Render()
{
  _builder.Add("Time:");
#line spanof('DateTime.Now') 15 "page.razor"
  _builder.Add(DateTime.Now);
#line hidden
);
}
```

#### 4. Razor: Multi-line span

Razor:
```
@page "/"                                  
@JsonToHtml(@"
{
  ""key1"": "value1",
  ""key2"": "value2"
}") 
```

Generated C#:
```C#
#line hidden
void Render()
{
  _builder.Add("Time:");
#line spanof('JsonToHtml(@"...")') 15 "page.razor"
  _builder.Add(JsonToHtml(@"
{
  ""key1"": "value1",
  ""key2"": "value2"
}"));
#line hidden
}
);
}
```

#### 5. Razor: block constructs

##### i. block containing expressions

In this example, the mapped span of the first sequence point that is associated with the IL instruction that is emitted for the `_builder.Add(Html.Helper(() => ` statement
needs to cover the whole expression of `Html.Helper(...)` in the generated file `a.razor`. This is achieved by application of rule [1] to the end position of the sequence point.

```
@Html.Helper(() => 
{
    <p>Hello World</p>
    @DateTime.Now
})
```

```C#
#line spanof('Html.Helper(() => { ... })') 13 "a.razor"
_builder.Add(Html.Helper(() => 
#line lineof('{') "a.razor"
{
#line spanof('DateTime.Now') 13 "a.razor"
_builder.Add(DateTime.Now);
#line lineof('}') "a.razor"
}
#line hidden
)
```

##### ii. block containing statements

Uses existing `#line line file` form since

a) Razor does not add any prefix,
b) `{` is not present in the generated file and there can't be a sequence point placed on it, therefore the span of the first unmapped sequence point is unknown to Razor.

The starting character of `Console` in the generated file must be aligned with the Razor file.

```
@{Console.WriteLine(1);Console.WriteLine(2);}
```

```C#
#line lineof('@{') "a.razor"
  Console.WriteLine(1);Console.WriteLine(2);
#line hidden
```

##### iii. block containing top-level code (@code, @functions)

Uses existing `#line line file` form since

a) Razor does not add any prefix,
b) `{` is not present in the generated file and there can't be a sequence point placed on it, therefore the span of the first unmapped sequence point is unknown to Razor.

The starting character of `[Parameter]` in the generated file must be aligned with the Razor file.

```
@code {
    [Parameter]
    public int IncrementAmount { get; set; }
}
```

```C#
#line lineof('[') "a.razor"
    [Parameter]
    public int IncrementAmount { get; set; }
#line hidden
```

#### 6. Razor: `@for`, `@foreach`, `@while`, `@do`, `@if`, `@switch`, `@using`, `@try`, `@lock`

Uses existing `#line line file` form since
a) Razor does not add any prefix. 
b) the span of the first unmapped sequence point may not be known to Razor (or shouldn't need to know).

The starting character of the keyword in the generated file must be aligned with the Razor file.

```
@for (var i = 0; i < 10; i++)
{
}
@if (condition)
{
}
else
{
}
```

```C#
#line lineof('for') "a.razor"
 for (var i = 0; i < 10; i++)
{
}
#line lineof('if') "a.razor"
 if (condition)
{
}
else
{
}
#line hidden
```
