# Lexical structure

## Programs

A C\# ***program*** consists of one or more ***source files***, known formally as ***compilation units*** (§14.2). A source file is an ordered sequence of Unicode characters. Source files typically have a one-to-one correspondence with files in a file system, but this correspondence is not required.

Conceptually speaking, a program is compiled using three steps:

1.  Transformation, which converts a file from a particular character repertoire and encoding scheme into a sequence of Unicode characters.

2.  Lexical analysis, which translates a stream of Unicode input characters into a stream of tokens.

3.  Syntactic analysis, which translates the stream of tokens into executable code.

Conforming implementations shall accept Unicode source files encoded with the UTF-8 encoding form (as defined by the Unicode standard), and transform them into a sequence of Unicode characters. Implementations can choose to accept and transform additional character encoding schemes (such as UTF-16, UTF-32, or non-Unicode character mappings).

\[*Note*: The handling of the Unicode NULL character (U+0000) is implementation-specific. It is strongly recommended that developers avoid using this character in their source code, for the sake of both portability and readability. When the character is required within a character or string literal, the escape sequences \\0 or \\u0000 may be used instead. *end note*\]

\[*Note*: It is beyond the scope of this standard to define how a file using a character representation other than Unicode might be transformed into a sequence of Unicode characters. During such transformation, however, it is recommended that the usual line-separating character (or sequence) in the other character set be translated to the two-character sequence consisting of the Unicode carriage-return character (U+000D) followed by Unicode line-feed character (U+000A). For the most part this transformation will have no visible effects; however, it will affect the interpretation of verbatim string literal tokens (§7.4.5.6). The purpose of this recommendation is to allow a verbatim string literal to produce the same character sequence when its source file is moved between systems that support differing non-Unicode character sets, in particular, those using differing character sequences for line-separation. *end note*\]

## Grammars

### General

This specification presents the syntax of the C\# programming language using two grammars. The ***lexical grammar*** (§7.2.2) defines how Unicode characters are combined to form line terminators, white space, comments, tokens, and pre-processing directives. The ***syntactic grammar*** (§7.2.4) defines how the tokens resulting from the lexical grammar are combined to form C\# programs.

### Grammar notation

The lexical and syntactic grammars are presented using ***grammar productions***. Each grammar production defines a non-terminal symbol and the possible expansions of that non-terminal symbol into sequences of non-terminal or terminal symbols. In grammar productions, *non-terminal* symbols are shown in italic type, and terminal symbols are shown in a fixed-width font.

The first line of a grammar production is the name of the non-terminal symbol being defined, followed by one or two colons. One colon is used for a production in the syntactic grammar, two colons for a production in the lexical grammar. Each successive indented line contains a possible expansion of the non-terminal given as a sequence of non-terminal or terminal symbols. \[*Example*: The production:

while-statement:\
*while* *(* boolean-expression *)* embedded-statement

defines a *while-statement* to consist of the token while, followed by the token “(”, followed by a *boolean-expression*, followed by the token “)”, followed by an *embedded-statement*. *end example*\]

When there is more than one possible expansion of a non-terminal symbol, the alternatives are listed on separate lines. \[*Example*: The production:

statement-list:\
statement\
statement-list statement

defines a *statement-list* to either consist of a *statement* or consist of a *statement-list* followed by a *statement*. In other words, the definition is recursive and specifies that a statement list consists of one or more statements. *end example*\]

A subscripted suffix “*opt*” is used to indicate an optional symbol. \[*Example*: The production:

block:\
*{* statement-list~opt~ *}*

is shorthand for:

block:\
*{* *}*\
*{* statement-list *}*

and defines a *block* to consist of an optional *statement-list* enclosed in “{” and “}” tokens. *end example*\]

Alternatives are normally listed on separate lines, though in cases where there are many alternatives, the phrase “one of” may precede a list of expansions given on a single line. This is simply shorthand for listing each of the alternatives on a separate line. \[*Example*: The production:

real-type-suffix:: *one of*\
*F f D d M m*

is shorthand for:

real-type-suffix::\
*F\
f\
D\
d\
M\
m*

*end example*\]

All terminal characters are to be understood as the appropriate Unicode character from the range U+0020 to U+007F, as opposed to any similar-looking characters from other Unicode character ranges.

### Lexical grammar

The lexical grammar of C\# is presented in §7.3, §7.4, and §7.5. The terminal symbols of the lexical grammar are the characters of the Unicode character set, and the lexical grammar specifies how characters are combined to form tokens (§7.4), white space (§7.3.4), comments (§7.3.3), and pre-processing directives (§7.5).

Every source file in a C\# program shall conform to the *input* production of the lexical grammar (§7.3.1).

### Syntactic grammar

The syntactic grammar of C\# is presented in the clauses, subclauses, and annexes that follow this subclause. The terminal symbols of the syntactic grammar are the tokens defined by the lexical grammar, and the syntactic grammar specifies how tokens are combined to form C\# programs.

Every source file in a C\# program shall conform to the *compilation-unit* production (§14.2) of the syntactic grammar.

### Grammar ambiguities

The productions for *simple-name* (§12.7.3) and *member-access* (§12.7.5) can give rise to ambiguities in the grammar for expressions. \[*Example*: The statement:

F(G&lt;A, B&gt;(7));

could be interpreted as a call to F with two arguments, G &lt; A and B &gt; (7). Alternatively, it could be interpreted as a call to F with one argument, which is a call to a generic method G with two type arguments and one regular argument. *end example*\]

If a sequence of tokens can be parsed (in context) as a *simple-name* (§12.7.3), *member-access* (§12.7.5), or *pointer-member-access* (§23.6.3) ending with a *type-argument-list* (§9.4.2), the token immediately following the closing &gt; token is examined. If it is one of

( ) \] : ; , . ? == !=

then the *type-argument-list* is retained as part of the *simple-name*, *member-access*, or *pointer-member-access* and any other possible parse of the sequence of tokens is discarded. Otherwise, the *type-argument-list* is not considered part of the *simple-name*, *member-access*, or *pointer-member-access*, even if there is no other possible parse of the sequence of tokens. \[*Note*: These rules are not applied when parsing a *type-argument-list* in a *namespace-or-type-name* (§8.8). *end note*\] \[*Example*: The statement:

F(G&lt;A, B&gt;(7));

will, according to this rule, be interpreted as a call to F with one argument, which is a call to a generic method G with two type arguments and one regular argument. The statements

F(G&lt;A, B&gt;7);\
F(G&lt;A, B&gt;&gt;7);

will each be interpreted as a call to F with two arguments. The statement

x = F&lt;A&gt; + y;

will be interpreted as a less-than operator, greater-than operator and unary-plus operator, as if the statement had been written x = (F &lt; A) &gt; (+y), instead of as a *simple-name* with a *type-argument-list* followed by a binary-plus operator. In the statement

x = y is C&lt;T&gt; && z;

the tokens C&lt;T&gt; are interpreted as a *namespace-or-type-name* with a *type-argument-list* due to being on the right-hand side of the is operator (§12.11.1). Because C&lt;T&gt; parses as a *namespace-or-type-name*, not a *simple-name*, *member-access*, or *pointer-member-access*, the above rule does not apply, and it is considered to have a *type-argument-list* regardless of the token that follows. *end example*\]

## Lexical analysis

### General

The *input* production defines the lexical structure of a C\# source file. Each source file in a C\# program shall conform to this lexical grammar production.

[]{#Grammar_input_section .anchor}input::\
input-section~opt~

input-section::\
input-section-part\
input-section input-section-part

[]{#Grammar_input_section_part .anchor}input-section-part::\
input-elements~opt~ new-line\
pp-directive

[]{#Grammar_input_elements .anchor}input-elements::\
input-element\
input-elements input-element

[]{#Grammar_input_element .anchor}input-element::\
whitespace\
comment\
token

Five basic elements make up the lexical structure of a C\# source file: Line terminators (§7.3.2), white space (§7.3.4), comments (§7.3.3), tokens (§7.4), and pre-processing directives (§7.5). Of these basic elements, only tokens are significant in the syntactic grammar of a C\# program (§7.2.4), except in the case of a &gt; token being combined with another token to form a single operator (§7.4.6).

The lexical processing of a C\# source file consists of reducing the file into a sequence of tokens that becomes the input to the syntactic analysis. Line terminators, white space, and comments can serve to separate tokens, and pre-processing directives can cause sections of the source file to be skipped, but otherwise these lexical elements have no impact on the syntactic structure of a C\# program.

When several lexical grammar productions match a sequence of characters in a source file, the lexical processing always forms the longest possible lexical element. \[*Example*: The character sequence // is processed as the beginning of a single-line comment because that lexical element is longer than a single / token. [[[]{#_Toc442097701 .anchor}]{#_Toc441919489 .anchor}]{#_Toc441918885 .anchor}*end example*\]

### Line terminators

Line terminators divide the characters of a C\# source file into lines.

[]{#Grammar_new_line .anchor}new-line::\
Carriage return character (*U+000D*)\
Line feed character (*U+000A*)\
Carriage return character (*U+000D*) followed by line feed character (*U+000A*)\
Next line character (*U+0085*)\
Line separator character (*U+2028*)\
Paragraph separator character (*U+2029*)

For compatibility with source code editing tools that add end-of-file markers, and to enable a source file to be viewed as a sequence of properly terminated lines, the following transformations are applied, in order, to every source file in a C\# program:

-   If the last character of the source file is a Control-Z character (U+001A), this character is deleted.

-   [[]{#OLE_LINK8 .anchor}]{#OLE_LINK7 .anchor}A carriage-return character (U+000D) is added to the end of the source file if that source file is non-empty and if the last character of the source file is not a carriage return (U+000D), a line feed (U+000A), a next line character (U+0085), a line separator (U+2028), or a paragraph separator (U+2029). \[*Note*: The additional carriage-return allows a program to end in a *pp-directive* (§7.5) that does not have a terminating *new-line*. *end note*\]

### Comments

Two forms of comments are supported: delimited comments and single-line comments.

A ***delimited comment*** begins with the characters /\* and ends with the characters \*/. Delimited comments can occupy a portion of a line, a single line, or multiple lines. \[*Example*: The example

/\* Hello, world program\
This program writes “hello, world” to the console\
\*/\
class Hello\
{\
static void Main() {\
System.Console.WriteLine("hello, world");\
}\
}

includes a delimited comment. *end example*\]

A ***single-line comment*** begins with the characters // and extends to the end of the line. \[*Example*: The example

// Hello, world program\
// This program writes “hello, world” to the console\
//\
class Hello // any name will do for this class\
{\
static void Main() { // this method must be named "Main"\
System.Console.WriteLine("hello, world");\
}\
}

shows several single-line comments. *end example*\]

[]{#Grammar_comment .anchor}comment::\
single-line-comment\
delimited-comment

[]{#Grammar_single_line_comment .anchor}single-line-comment::\
*//* input-characters~opt~

[]{#Grammar_input_characters .anchor}input-characters::\
input-character\
input-characters input-character

[]{#Grammar_input_character .anchor}input-character::\
Any Unicode character except a new-line-character

[]{#Grammar_new_line_character .anchor}new-line-character::\
Carriage return character (*U+000D*)\
Line feed character (*U+000A*)\
Next line character (*U+0085*)\
Line separator character (*U+2028*)\
Paragraph separator character (*U+2029*)

[]{#Grammar_delimited_comment .anchor}delimited-comment::\
*/\** delimited-comment-text~opt~ asterisks */*

[]{#Grammar_delimited_comment_text .anchor}delimited-comment-text::\
delimited-comment-section\
delimited-comment-text delimited-comment-section

[]{#Grammar_delimited_comment_section .anchor}delimited-comment-section::\
/\
asterisks~opt~ not-slash-or-asterisk

[]{#Grammar_asterisks .anchor}asterisks::\
*\**\
asterisks *\**

[]{#Grammar_not_asterisk .anchor}not-slash-or-asterisk::\
Any Unicode character except */ or \**

Comments do not nest. The character sequences /\* and \*/ have no special meaning within a single-line comment, and the character sequences // and /\* have no special meaning within a delimited comment.

Comments are not processed within character and string literals.

\[*Note*: These rules must be interpreted carefully. For instance, in the example below, the delimited comment that begins before A ends between B and C(). The reason is that

// B \*/ C();

is not actually a single-line comment, since // has no special meaning within a delimited comment, and so \*/ does have its usual special meaning in that line.

Likewise, the delimited comment starting before D ends before E. The reason is that "D \*/ " is not actually a string literal, since it appears inside a delimited comment.

A useful consequence of /\* and \*/ having no special meaning within a single-line comment is that a block of source code lines can be commented out by putting // at the beginning of each line. In general it does not work to put /\* before those lines and \*/ after them, as this does not properly encapsulate delimited comments in the block, and in general may completely change the structure of such delimited comments.

Example code:

static void Main() {

/\* A

// B \*/ C();

Console.WriteLine(/\* "D \*/ "E");

}

*end note*\]

### White space

White space is defined as any character with Unicode class Zs (which includes the space character) as well as the horizontal tab character, the vertical tab character, and the form feed character.

[[[]{#OLE_LINK18 .anchor}]{#OLE_LINK17 .anchor}]{#Grammar_whitespace .anchor}whitespace::\
whitespace-character\
whitespace whitespace-character

[]{#Grammar_whitespace_character .anchor}whitespace-character::\
Any character with Unicode class Zs\
Horizontal tab character (*U+0009*)\
Vertical tab character (*U+000B*)\
Form feed character (*U+000C*)

## Tokens

### General

There are several kinds of ***token***s: identifiers, keywords, literals, operators, and punctuators. White space and comments are not tokens, though they act as separators for tokens.

[]{#Grammar_token .anchor}token::\
identifier\
keyword\
integer-literal\
real-literal\
character-literal\
string-literal\
operator-or-punctuator

### Unicode character escape sequences

A Unicode escape sequence represents a Unicode code point. Unicode escape sequences are processed in identifiers (§7.4.3), character literals (§7.4.5.5), and regular string literals (§7.4.5.6). A Unicode escape sequence is not processed in any other location (for example, to form an operator, punctuator, or keyword).

[]{#Grammar_unicode_escape_sequence .anchor}unicode-escape-sequence::\
*\\u* hex-digit hex-digit hex-digit hex-digit\
*\\U* hex-digit hex-digit hex-digit hex-digit hex-digit hex-digit hex-digit hex-digit

A Unicode character escape sequence represents the single Unicode code point formed by the hexadecimal number following the “\\u” or “\\U” characters. Since C\# uses a 16-bit encoding of Unicode code points in character and string values, a Unicode code point in the range U+10000 to U+10FFFF is represented using two Unicode surrogate code units. Unicode code points above U+FFFF are not permitted in character literals. Unicode code points above U+10FFFF are invalid and are not supported.

Multiple translations are not performed. For instance, the string literal “\\u005Cu005C” is equivalent to “\\u005C” rather than “\\”. \[*Note*: The Unicode value \\u005C is the character “\\”. *end note*\]

\[*Example*: The example

class Class1\
{\
static void Test(bool \\u0066) {\
char c = '\\u0066';\
if (\\u0066)\
System.Console.WriteLine(c.ToString());\
}\
}

shows several uses of \\u0066, which is the escape sequence for the letter “f”. The program is equivalent to

class Class1\
{\
static void Test(bool f) {\
char c = 'f';\
if (f)\
System.Console.WriteLine(c.ToString());\
}\
}

*end example*\]

### Identifiers

The rules for identifiers given in this subclause correspond exactly to those recommended by the Unicode Standard Annex 15 except that underscore is allowed as an initial character (as is traditional in the C programming language), Unicode escape sequences are permitted in identifiers, and the “@” character is allowed as a prefix to enable keywords to be used as identifiers.

[]{#Grammar_identifier .anchor}identifier::\
available-identifier\
*@* identifier-or-keyword

[]{#Grammar_available_identifier .anchor}available-identifier::\
An identifier-or-keyword that is not a keyword

[]{#Grammar_identifier_or_keyword .anchor}identifier-or-keyword::\
identifier-start-character identifier-part-characters~opt~

[]{#Grammar_identifier_part_characters .anchor}identifier-start-character::\
letter-character\
*underscore-character*[]{#Grammar_identifier_start_character .anchor}

underscore-character::\
\_ (the underscore character U+005F)\
A unicode-escape-sequence representing the character U+005F[]{#Grammar_underscore_character .anchor}

identifier-part-characters::\
identifier-part-character\
identifier-part-characters identifier-part-character

[]{#Grammar_identifier_part_character .anchor}identifier-part-character::\
letter-character\
decimal-digit-character\
connecting-character\
combining-character\
formatting-character

[]{#Grammar_letter_character .anchor}letter-character::\
A Unicode character of classes Lu, Ll, Lt, Lm, Lo, or Nl\
A unicode-escape-sequence representing a character of classes Lu, Ll, Lt, Lm, Lo, or Nl

[]{#Grammar_combining_character .anchor}combining-character::\
A Unicode character of classes Mn or Mc\
A unicode-escape-sequence representing a character of classes Mn or Mc

[]{#Grammar_decimal_digit_character .anchor}decimal-digit-character::\
A Unicode character of the class Nd\
A unicode-escape-sequence representing a character of the class Nd

[]{#Grammar_connecting_character .anchor}connecting-character::\
A Unicode character of the class Pc\
A unicode-escape-sequence representing a character of the class Pc

[]{#Grammar_formatting_character .anchor}formatting-character::\
A Unicode character of the class Cf\
A unicode-escape-sequence representing a character of the class Cf

\[*Note*: For information on the Unicode character classes mentioned above, see *The Unicode Standard*. *end note*\]

\[*Example*: Examples of valid identifiers include “identifier1”, “\_identifier2”, and “@if”. *end example*\]

An identifier in a conforming program shall be in the canonical format defined by Unicode Normalization Form C, as defined by Unicode Standard Annex 15. The behavior when encountering an identifier not in Normalization Form C is implementation-defined; however, a diagnostic is not required.

The prefix “@” enables the use of keywords as identifiers, which is useful when interfacing with other programming languages. The character @ is not actually part of the identifier, so the identifier might be seen in other languages as a normal identifier, without the prefix. An identifier with an @ prefix is called a ***verbatim identifier*.** \[*Note*: Use of the @ prefix for identifiers that are not keywords is permitted, but strongly discouraged as a matter of style. *end note*\]

\[*Example*: The example:

class @class\
{\
public static void @static(bool @bool) {\
if (@bool)\
System.Console.WriteLine("true");\
else\
System.Console.WriteLine("false");\
}\
}

class Class1\
{\
static void M() {\
cl\\u0061ss.st\\u0061tic(true);\
}\
}

defines a class named “class” with a static method named “static” that takes a parameter named “bool”. Note that since Unicode escapes are not permitted in keywords, the token “cl\\u0061ss” is an identifier, and is the same identifier as “@class”. *end example*\]

Two identifiers are considered the same if they are identical after the following transformations are applied, in order:

-   The prefix “@”, if used, is removed.

-   Each *unicode-escape-sequence* is transformed into its corresponding Unicode character.

-   Any *formatting-character*s are removed.

Identifiers containing two consecutive underscore characters (U+005F) are reserved for use by the implementation; however, no diagnostic is required if such an identifier is defined. \[*Note*: For example, an implementation might provide extended keywords that begin with two underscores. *end note*\]

### Keywords

A ***keyword*** is an identifier-like sequence of characters that is reserved, and cannot be used as an identifier except when prefaced by the @ character.

[]{#Grammar_keyword .anchor}keyword:: one of\
*abstract as base bool break\
byte case catch char checked\
class const continue decimal default\
delegate do double else enum\
event explicit extern false finally\
fixed float for foreach goto\
if implicit in int interface\
internal is lock long namespace\
new null object operator out\
override params private protected public\
readonly ref return sbyte sealed\
short sizeof stackalloc static string\
struct switch this throw true\
try typeof uint ulong unchecked\
unsafe ushort using virtual void\
volatile while*

[[[[[]{#_Ref462576210 .anchor}]{#_Ref450668500 .anchor}]{#_Ref449414818 .anchor}]{#_Ref449414802 .anchor}]{#_Toc445782958 .anchor}A ***contextual keyword*** is an identifier-like sequence of characters that has special meaning in certain contexts, but is not reserved, and can be used as an identifier outside of those contexts as well as when prefaced by the @ character.

contextual-keyword: *one of the following identifiers*\
*add alias ascending async await\
by descending dynamic equals from\
get global group into join\
let orderby partial remove select\
set value var where yield*

In most cases, the syntactic location of contextual keywords is such that they can never be confused with ordinary identifier usage. For example, within a property declaration, the “get” and “set” identifiers have special meaning (§15.7.3). An identifier other than get or set is never permitted in these locations, so this use does not conflict with a use of these words as identifiers.

In certain cases the grammar is not enough to distinguish contextual keyword usage from identifiers. In all such cases it will be specified how to disambiguate between the two. For example, the contextual keyword var in implicitly typed local variable declarations (§13.6.2) might conflict with a declared type called var, in which case the declared name takes precedence over the use of the identifier as a contextual keyword.

Another example such disambiguation is the contextual keyword await (§12.8.8.1), which is considered a keyword only when inside a method declared async, but can be used as an identifier elsewhere.

Just as with keywords, contextual keywords can be used as ordinary identifiers by prefixing them with the @ character.

\[*Note*: When used as contextual keywords, these identifiers cannot contain unicode-escape-sequences. *end note*\].

### Literals

#### General

A ***literal*** (§12.7.2) is a source code representation of a value.

[]{#Grammar_literal .anchor}literal::\
boolean-literal\
integer-literal\
real-literal\
character-literal\
string-literal\
null-literal

#### Boolean literals

There are two Boolean literal values: true and false.

[]{#Grammar_boolean_literal .anchor}boolean-literal::\
*true\
false*[[[[]{#_Ref462414137 .anchor}]{#_Ref462413171 .anchor}]{#_Ref462394190 .anchor}]{#_Toc445782960 .anchor}

The type of a *boolean-literal* is bool.

#### Integer literals

Integer literals are used to write values of types int, uint, long, and ulong. Integer literals have two possible forms: decimal and hexadecimal.

[]{#Grammar_integer_literal .anchor}integer-literal::\
decimal-integer-literal\
hexadecimal-integer-literal

[]{#Grammar_decimal_integer_literal .anchor}decimal-integer-literal::\
decimal-digits integer-type-suffix~opt~

[]{#Grammar_decimal_digits .anchor}decimal-digits::\
decimal-digit\
decimal-digits decimal-digit

[]{#Grammar_decimal_digit .anchor}decimal-digit:: one of\
*0 1 2 3 4 5 6 7 8 9*

[]{#Grammar_integer_type_suffix .anchor}integer-type-suffix:: one of\
*U u L l UL Ul uL ul LU Lu lU lu*

[]{#Grammar_hexadecimal_integer_literal .anchor}hexadecimal-integer-literal::\
*0x* hex-digits integer-type-suffix~opt~\
*0X* hex-digits integer-type-suffix~opt~

[]{#Grammar_hex_digits .anchor}hex-digits::\
hex-digit\
hex-digits hex-digit

[]{#Grammar_hex_digit .anchor}hex-digit:: one of\
*0 1 2 3 4 5 6 7 8 9 A B C D E F a b c d e f*

[]{#_Toc445782961 .anchor}The type of an integer literal is determined as follows:

-   If the literal has no suffix, it has the first of these types in which its value can be represented: int, uint, long, ulong.

-   If the literal is suffixed by U or u, it has the first of these types in which its value can be represented: uint, ulong.

-   If the literal is suffixed by L or l, it has the first of these types in which its value can be represented: long, ulong.

-   If the literal is suffixed by UL, Ul, uL, ul, LU, Lu, lU, or lu, it is of type ulong.

If the value represented by an integer literal is outside the range of the ulong type, a compile-time error occurs.

\[*Note*: As a matter of style, it is suggested that “L” be used instead of “l” when writing literals of type long, since it is easy to confuse the letter “l” with the digit “1”. *end note*\]

To permit the smallest possible int and long values to be written as integer literals, the following two rules exist:

-   When an *integer-literal* representing the value 2147483648 (2^31^) and no *integer-type-suffix* appears as the token immediately following a unary minus operator token (§12.8.3), the result (of both tokens) is a constant of type int with the value −2147483648 (−2^31^). In all other situations, such an *integer-literal* is of type uint.

-   When an *integer-literal* representing the value 9223372036854775808 (2^63^) and no *integer-type-suffix* or the *integer-type-suffix* L or l appears as the token immediately following a unary minus operator token (§12.8.3), the result (of both tokens) is a constant of type long with the value −9223372036854775808 (−2^63^). In all other situations, such an *integer-literal* is of type ulong.

#### Real literals

Real literals are used to write values of types float, double, and decimal.

[]{#Grammar_real_literal .anchor}real-literal::\
decimal-digits *.* decimal-digits exponent-part~opt~ real-type-suffix~opt~\
*.* decimal-digits exponent-part~opt~ real-type-suffix~opt~\
decimal-digits exponent-part real-type-suffix~opt~\
decimal-digits real-type-suffix

[]{#Grammar_exponent_part .anchor}exponent-part::\
*e* sign~opt~ decimal-digits\
*E* sign~opt~ decimal-digits

[]{#Grammar_sign .anchor}sign:: one of\
*+ -*

[]{#Grammar_real_type_suffix .anchor}real-type-suffix:: one of\
*F f D d M m*

[]{#_Toc445782962 .anchor}If no *real-type-suffix* is specified, the type of the *real-literal* is double. Otherwise, the *real-type-suffix* determines the type of the real literal, as follows:

-   A real literal suffixed by F or f is of type float. \[*Example*: The literals 1f, 1.5f, 1e10f, and 123.456F are all of type float. *end example*\]

-   A real literal suffixed by D or d is of type double. \[*Example*: The literals 1d, 1.5d, 1e10d, and 123.456D are all of type double. *end example*\]

-   A real literal suffixed by M or m is of type decimal. \[*Example*: The literals 1m, 1.5m, 1e10m, and 123.456M are all of type decimal. *end example*\] This literal is converted to a decimal value by taking the exact value, and, if necessary, rounding to the nearest representable value using banker's rounding (§9.3.8). Any scale apparent in the literal is preserved unless the value is rounded. \[*Note*: Hence, the literal 2.900m will be parsed to form the decimal with sign 0, coefficient 2900, and scale 3. *end note*\]

If the magnitude of the specified literal is too large to be represented in the indicated type, a compile-time error occurs. \[*Note*: In particular, a *real-literal* will never produce a floating-point infinity. A non-zero *real-literal* may, however, be rounded to zero. *end note*\]

The value of a real literal of type float or double is determined by using the IEC 60559 “round to nearest” mode with ties broken to “even” (a value with the least-significant-bit zero), and all digits considered significant.

\[*Note*: In a real literal, decimal digits are always required after the decimal point. For example, 1.3F is a real literal but 1.F is not. *end note*\]

#### Character literals

A character literal represents a single character, and consists of a character in quotes, as in 'a'.

[]{#Grammar_character_literal .anchor}character-literal::\
*'* character *'*

[]{#Grammar_character .anchor}character::\
single-character\
simple-escape-sequence\
hexadecimal-escape-sequence\
unicode-escape-sequence

[]{#Grammar_single_character .anchor}single-character::\
Any character except *'* (*U+0027*), *\\* (*U+005C*), and new-line-character

[]{#Grammar_simple_escape_sequence .anchor}simple-escape-sequence:: one of\
*\\' \\" \\\\ \\0 \\a \\b \\f \\n \\r \\t \\v*

[]{#Grammar_hexadecimal_escape_sequence .anchor}hexadecimal-escape-sequence::\
*\\x* hex-digit hex-digit~opt~ hex-digit~opt~ hex-digit~opt~

[]{#_Toc445782963 .anchor}\[*Note*: A character that follows a backslash character (\\) in a *character* shall be one of the following characters: ', ", \\, 0, a, b, f, n, r, t, u, U, x, v. Otherwise, a compile-time error occurs. *end note*\]

\[*Note*: The use of the \\x *hexadecimal-escape-sequence* production can be error-prone and hard to read due to the variable number of hexadecimal digits following the \\x. For example, in the code:

string good = "\\x9Good text";

string bad = "\\x9Bad text";

it might appear at first that the leading character is the same (U+0009, a tab character) in both strings. In fact the second string starts with U+9BAD as all three letters in the word "Bad" are valid hexadecimal digits. As a matter of style, it is recommended that \\x is avoided in favour of either specific escape sequences (\\t in this example) or the fixed-length \\u escape sequence. *end note*\]

A hexadecimal escape sequence represents a single Unicode UTF-16 code unit, with the value formed by the hexadecimal number following “\\x”.

If the value represented by a character literal is greater than U+FFFF, a compile-time error occurs.

A Unicode escape sequence (§7.4.2) in a character literal shall be in the range U+0000 to U+FFFF.

A simple escape sequence represents a Unicode character, as described in the table below.

  --------------------- -------------------- ------------------------
  **Escape sequence**   **Character name**   **Unicode code point**
  \\'                   Single quote         U+0027
  \\"                   Double quote         U+0022
  \\\\                  Backslash            U+005C
  \\0                   Null                 U+0000
  \\a                   Alert                U+0007
  \\b                   Backspace            U+0008
  \\f                   Form feed            U+000C
  \\n                   New line             U+000A
  \\r                   Carriage return      U+000D
  \\t                   Horizontal tab       U+0009
  \\v                   Vertical tab         U+000B
  --------------------- -------------------- ------------------------

[]{#_Ref467581683 .anchor}

The type of a *character-literal* is char.

#### String literals

[]{#_Toc445782964 .anchor}C\# supports two forms of string literals: ***regular string literals*** and ***verbatim string literals***. A regular string literal consists of zero or more characters enclosed in double quotes, as in "hello", and can include both simple escape sequences (such as \\t for the tab character), and hexadecimal and Unicode escape sequences.

A verbatim string literal consists of an @ character followed by a double-quote character, zero or more characters, and a closing double-quote character. \[*Example*: A simple example is @"hello". *end example*\] In a verbatim string literal, the characters between the delimiters are interpreted verbatim, with the only exception being a *quote-escape-sequence*, which represents one double-quote character. In particular, simple escape sequences, and hexadecimal and Unicode escape sequences are not processed in verbatim string literals. A verbatim string literal may span multiple lines.

[]{#Grammar_string_literal .anchor}string-literal::\
regular-string-literal\
verbatim-string-literal

[]{#Grammar_regular_string_literal .anchor}regular-string-literal::\
*"* regular-string-literal-characters~opt~ *"*

[]{#Grammar_regular_string_literal_chars .anchor}regular-string-literal-characters::\
regular-string-literal-character\
regular-string-literal-characters regular-string-literal-character

[]{#Grammar_regular_string_literal_char .anchor}regular-string-literal-character::\
single-regular-string-literal-character\
simple-escape-sequence\
hexadecimal-escape-sequence\
unicode-escape-sequence

[]{#Grammar_single_regular_string_literal_ch .anchor}single-regular-string-literal-character::\
Any character except *"* (*U+0022*), *\\* (*U+005C*), and new-line-character

[]{#Grammar_verbatim_string_literal .anchor}verbatim-string-literal::\
*@"* verbatim-string-literal-characters~opt~ *"*

[]{#Grammar_verbatim_string_literal_chars .anchor}verbatim-string-literal-characters::\
verbatim-string-literal-character\
verbatim-string-literal-characters verbatim-string-literal-character

[]{#Grammar_verbatim_string_literal_char .anchor}verbatim-string-literal-character::\
single-verbatim-string-literal-character\
quote-escape-sequence

[]{#Grammar_single_verbatim_string_literal_c .anchor}single-verbatim-string-literal-character::\
Any character except *"*

[]{#Grammar_quote_escape_sequence .anchor}quote-escape-sequence::\
*""*

\[*Example*: The example

string a = "Happy birthday, Joel"; // Happy birthday, Joel\
string b = @"Happy birthday, Joel"; // Happy birthday, Joel

string c = "hello \\t world"; // hello world\
string d = @"hello \\t world"; // hello \\t world

string e = "Joe said \\"Hello\\" to me"; // Joe said "Hello" to me\
string f = @"Joe said ""Hello"" to me"; // Joe said "Hello" to me

string g = "\\\\\\\\server\\\\share\\\\file.txt"; // \\\\server\\share\\file.txt\
string h = @"\\\\server\\share\\file.txt"; // \\\\server\\share\\file.txt

string i = "one\\r\\ntwo\\r\\nthree";\
string j = @"one\
two\
three";

shows a variety of string literals. The last string literal, j, is a verbatim string literal that spans multiple lines. The characters between the quotation marks, including white space such as new line characters, are preserved verbatim, and each pair of double-quote characters is replaced by one such character. *end example*\]

\[*Note*: Any line breaks within verbatim string literals are part of the resulting string. If the exact characters used to form line breaks are semantically relevant to an application, any tools that translate line breaks in source code to different formats (between "\\n" and "\\r\\n", for example) will change application behavior. Developers should be careful in such situations. *end note*\]

\[*Note*: Since a hexadecimal escape sequence can have a variable number of hex digits, the string literal "\\x123" contains a single character with hex value 123. To create a string containing the character with hex value 12 followed by the character 3, one could write "\\x00123" or "\\x12" + "3" instead. *end note*\]

The type of a *string-literal* is string.

Each string literal does not necessarily result in a new string instance. When two or more string literals that are equivalent according to the string equality operator (§12.11.8), appear in the same assembly, these string literals refer to the same string instance. \[*Example*: For instance, the output produced by

class Test\
{\
static void Main() {\
object a = "hello";\
object b = "hello";\
System.Console.WriteLine(a == b);\
}\
}

is True because the two literals refer to the same string instance. *end example*\]

#### The null literal

[]{#Grammar_null_literal .anchor}null-literal::\
*null*

[]{#_Hlt501205090 .anchor}A *null-literal* represents a null value. It does not have a type, but can be converted to any reference type or nullable value type through a null literal conversion (§11.2.6)."

### Operators and punctuators

There are several kinds of operators and punctuators. Operators are used in expressions to describe operations involving one or more operands. \[*Example*: The expression a + b uses the + operator to add the two operands a and b. *end example*\] Punctuators are for grouping and separating.

[]{#Grammar_operator_or_punctuator .anchor}operator-or-punctuator:: *one of*\
*{ } \[ \] ( ) . , : ;\
+ - \* / % & | \^ ! \~\
= &lt; &gt; ? ?? :: ++ -- && ||\
-&gt; == != &lt;= &gt;= += -= \*= /= %=\
&= |= \^= &lt;&lt; &lt;&lt;=*

[]{#Grammar_right_shift .anchor}right-shift::\
*&gt;* *&gt;*

[]{#Grammar_right_shift_assignment .anchor}right-shift-assignment::\
*&gt; &gt;=*

*right-shift* is made up of the two tokens &gt; and &gt;. Similarly, *right-shift-assignment* is made up of the two tokens &gt; and &gt;=. Unlike other productions in the syntactic grammar, no characters of any kind (not even whitespace) are allowed between the two tokens in each of these productions. These productions are treated specially in order to enable the correct handling of *type-parameter-lists* (§15.2.3). \[*Note*: Prior to the addition of generics to C\#, &gt;&gt; and &gt;&gt;= were both single tokens. However, the syntax for generics uses the &lt; and &gt; characters to delimit type parameters and type arguments. It is often desirable to use nested constructed types, such as List&lt;Dictionary&lt;string, int&gt;&gt;. Rather than requiring the programmer to separate the &gt; and &gt; by a space, the definition of the two *operator-or-punctuator*s was changed. *end note*\]

## Pre-processing directives

### General

The pre-processing directives provide the ability to skip conditionally sections of source files, to report error and warning conditions, and to delineate distinct regions of source code. \[*Note*: The term “pre-processing directives” is used only for consistency with the C and C++ programming languages. In C\#, there is no separate pre-processing step; pre-processing directives are processed as part of the lexical analysis phase. *end note*\]

[]{#Grammar_pp_directive .anchor}pp-directive::\
pp-declaration\
pp-conditional\
pp-line\
pp-diagnostic\
pp-region\
pp-pragma

The following pre-processing directives are available:

-   \#define and \#undef, which are used to define and undefine, respectively, conditional compilation symbols (§7.5.4).

-   \#if, \#elif, \#else, and \#endif, which are used to skip conditionally sections of source code (§7.5.5).

-   \#line, which is used to control line numbers emitted for errors and warnings (§7.5.8).

-   \#error, which is used to issue errors (§7.5.6).

-   \#region and \#endregion, which are used to explicitly mark sections of source code (§7.5.7).

-   \#pragma, which is used to specify optional contextual information to a compiler (§7.5.9).

A pre-processing directive always occupies a separate line of source code and always begins with a \# character and a pre-processing directive name. White space may occur before the \# character and between the \# character and the directive name.

A source line containing a \#define, \#undef, \#if, \#elif, \#else, \#endif, \#line, or \#endregion directive can end with a single-line comment. Delimited comments (the /\* \*/ style of comments) are not permitted on source lines containing pre-processing directives.

Pre-processing directives are not tokens and are not part of the syntactic grammar of C\#. However, pre-processing directives can be used to include or exclude sequences of tokens and can in that way affect the meaning of a C\# program. \[*Example*: When compiled, the program

\#define A\
\#undef B

class C\
{\
\#if A\
void F() {}\
\#else\
void G() {}\
\#endif

\#if B\
void H() {}\
\#else\
void I() {}\
\#endif\
}

results in the exact same sequence of tokens as the program

class C\
{\
void F() {}\
void I() {}\
}

Thus, whereas lexically, the two programs are quite different, syntactically, they are identical. *end example*\]

### Conditional compilation symbols

The conditional compilation functionality provided by the \#if, \#elif, \#else, and \#endif directives is controlled through pre-processing expressions (§7.5.3) and conditional compilation symbols.

[]{#Grammar_conditional_symbol .anchor}conditional-symbol::\
Any identifier-or-keyword except *true* or *false*

Two conditional compilation symbols are considered the same if they are identical after the following transformations are applied, in order:

-   Each *unicode-escape-sequence* is transformed into its corresponding Unicode character.

-   Any *formatting-characters* are removed.

A conditional compilation symbol has two possible states: ***defined*** or ***undefined***. At the beginning of the lexical processing of a source file, a conditional compilation symbol is undefined unless it has been explicitly defined by an external mechanism (such as a command-line compiler option). When a \#define directive is processed, the conditional compilation symbol named in that directive becomes defined in that source file. The symbol remains defined until a \#undef directive for that same symbol is processed, or until the end of the source file is reached. An implication of this is that \#define and \#undef directives in one source file have no effect on other source files in the same program.

When referenced in a pre-processing expression (§7.5.3), a defined conditional compilation symbol has the Boolean value true, and an undefined conditional compilation symbol has the Boolean value false. There is no requirement that conditional compilation symbols be explicitly declared before they are referenced in pre-processing expressions. Instead, undeclared symbols are simply undefined and thus have the value false.

The namespace for conditional compilation symbols is distinct and separate from all other named entities in a C\# program. Conditional compilation symbols can only be referenced in \#define and \#undef directives and in pre-processing expressions.

### Pre-processing expressions

Pre-processing expressions can occur in \#if and \#elif directives. The operators !, ==, !=, &&, and || are permitted in pre-processing expressions, and parentheses may be used for grouping.

[]{#Grammar_pp_expression .anchor}pp-expression::\
whitespace~opt~ pp-or-expression whitespace~opt~

[]{#Grammar_pp_or_expression .anchor}pp-or-expression::\
pp-and-expression\
pp-or-expression whitespace~opt~ *||* whitespace~opt~ pp-and-expression

[]{#Grammar_pp_and_expression .anchor}pp-and-expression::\
pp-equality-expression\
pp-and-expression whitespace~opt~ *&&* whitespace~opt~ pp-equality-expression

[]{#Grammar_pp_equality_expression .anchor}pp-equality-expression::\
pp-unary-expression\
pp-equality-expression whitespace~opt~ *==* whitespace~opt~ pp-unary-expression\
pp-equality-expression whitespace~opt~ *!=* whitespace~opt~ pp-unary-expression

[]{#Grammar_pp_unary_expression .anchor}pp-unary-expression::\
pp-primary-expression\
*!* whitespace~opt~ pp-unary-expression

[]{#Grammar_pp_primary_expression .anchor}pp-primary-expression::\
*true*\
*false*\
conditional-symbol\
*(* whitespace~opt~ pp-expression whitespace~opt~ *)*

[[[[[[]{#_Toc508360745 .anchor}]{#_Toc505663892 .anchor}]{#_Toc505589727 .anchor}]{#_Toc503163992 .anchor}]{#_Toc501035298 .anchor}]{#_Ref496286045 .anchor}When referenced in a pre-processing expression, a defined conditional compilation symbol has the Boolean value true, and an undefined conditional compilation symbol has the Boolean value false.

Evaluation of a pre-processing expression always yields a Boolean value. The rules of evaluation for a pre-processing expression are the same as those for a constant expression (§12.20), except that the only user-defined entities that can be referenced are conditional compilation symbols.

### Definition directives 

The definition directives are used to define or undefine conditional compilation symbols.

[]{#Grammar_pp_declaration .anchor}pp-declaration::\
whitespace~opt~ *\#* whitespace~opt~ *define* whitespace conditional-symbol pp-new-line\
whitespace~opt~ *\#* whitespace~opt~ *undef* whitespace conditional-symbol pp-new-line

[]{#Grammar_pp_new_line .anchor}pp-new-line::\
whitespace~opt~ single-line-comment~opt~ new-line

The processing of a \#define directive causes the given conditional compilation symbol to become defined, starting with the source line that follows the directive. Likewise, the processing of a \#undef directive causes the given conditional compilation symbol to become undefined, starting with the source line that follows the directive.

Any \#define and \#undef directives in a source file shall occur before the first *token* (§7.4) in the source file; otherwise a compile-time error occurs. In intuitive terms, \#define and \#undef directives shall precede any “real code” in the source file.

\[*Example*: The example:

\#define Enterprise

\#if Professional || Enterprise\
\#define Advanced\
\#endif

namespace Megacorp.Data\
{\
\#if Advanced\
class PivotTable {…}\
\#endif\
}

is valid because the \#define directives precede the first token (the namespace keyword) in the source file.

*end example*\]

\[*Example*: The following example results in a compile-time error because a \#define follows real code:

\#define A\
namespace N\
{\
\#define B\
\#if B\
class Class1 {}\
\#endif\
}

*end example*\]

A \#define may define a conditional compilation symbol that is already defined, without there being any intervening \#undef for that symbol. \[*Example*: The example below defines a conditional compilation symbol A and then defines it again.

\#define A\
\#define A

For compilers that allow conditional compilation symbols to be defined as compilation options, an alternative way for such redefinition to occur is to define the symbol as a compiler option as well as in the source. *end example*\]

A \#undef may “undefine” a conditional compilation symbol that is not defined. \[*Example*: The example below defines a conditional compilation symbol A and then undefines it twice; although the second \#undef has no effect, it is still valid.

\#define A\
\#undef A\
\#undef A

*end example*\]

### Conditional compilation directives

The conditional compilation directives are used to conditionally include or exclude portions of a source file.

[]{#Grammar_pp_conditional .anchor}pp-conditional::\
pp-if-section pp-elif-sections~opt~ pp-else-section~opt~ pp-endif

[]{#Grammar_pp_if_section .anchor}pp-if-section::\
whitespace~opt~ *\#* whitespace~opt~ *if* whitespace pp-expression pp-new-line\
conditional-section~opt~

[]{#Grammar_pp_elif_sections .anchor}pp-elif-sections::\
pp-elif-section\
pp-elif-sections pp-elif-section

[]{#Grammar_pp_elif_section .anchor}pp-elif-section::\
whitespace~opt~ *\#* whitespace~opt~ *elif* whitespace pp-expression pp-new-line\
conditional-section~opt~

[]{#Grammar_pp_else_section .anchor}pp-else-section::\
whitespace~opt~ *\#* whitespace~opt~ *else* pp-new-line conditional-section~opt~

[]{#Grammar_pp_endif .anchor}pp-endif::\
whitespace~opt~ *\#* whitespace~opt~ *endif* pp-new-line

[]{#Grammar_conditional_section .anchor}conditional-section::\
input-section\
skipped-section

[]{#Grammar_skipped_section .anchor}skipped-section::\
skipped-section-part\
skipped-section skipped-section-part

[]{#Grammar_skipped_section_part .anchor}skipped-section-part::\
skipped-characters~opt~ new-line\
pp-directive

[]{#Grammar_skipped_characters .anchor}skipped-characters::\
whitespace~opt~ not-number-sign input-characters~opt~

[]{#Grammar_not_number_sign .anchor}not-number-sign::\
Any input-character except *\#*

\[*Note*: As indicated by the syntax, conditional compilation directives shall be written as sets consisting of, in order, a \#if directive, zero or more \#elif directives, zero or one \#else directive, and a \#endif directive. Between the directives are conditional sections of source code. Each section is controlled by the immediately preceding directive. A conditional section may itself contain nested conditional compilation directives provided these directives form complete sets. *end note*\]

A *pp-conditional* selects at most one of the contained *conditional-section*s for normal lexical processing:

-   The *pp-expression*s of the \#if and \#elif directives are evaluated in order until one yields true. If an expression yields true, the *conditional-section* of the corresponding directive is selected.

-   If all *pp-expression*s yield false, and if a \#else directive is present, the *conditional-section* of the \#else directive is selected.

-   Otherwise, no *conditional-section* is selected.

The selected *conditional-section*, if any, is processed as a normal *input-section*: the source code contained in the section shall adhere to the lexical grammar; tokens are generated from the source code in the section; and pre-processing directives in the section have the prescribed effects.

The remaining *conditional-section*s, if any, are processed as *skipped-section*s: except for pre-processing directives, the source code in the section need not adhere to the lexical grammar; no tokens are generated from the source code in the section; and pre-processing directives in the section shall be lexically correct but are not otherwise processed. Within a *conditional-section* that is being processed as a *skipped-section*, any nested *conditional-section*s (contained in nested \#if…\#endif and \#region…\#endregion constructs) are also processed as *skipped-section*s.

\[*Example*: The following example illustrates how conditional compilation directives can nest:

\#define Debug // Debugging on\
\#undef Trace // Tracing off

class PurchaseTransaction\
{\
void Commit() {\
\#if Debug\
CheckConsistency();\
\#if Trace\
WriteToLog(this.ToString());\
\#endif\
\#endif\
CommitHelper();\
}\
…\
}

Except for pre-processing directives, skipped source code is not subject to lexical analysis. For example, the following is valid despite the unterminated comment in the \#else section:

\#define Debug // Debugging on

class PurchaseTransaction\
{\
void Commit() {\
\#if Debug\
CheckConsistency();\
\#else\
/\* Do something else\
\#endif\
}\
…\
}

Note, however, that pre-processing directives are required to be lexically correct even in skipped sections of source code.

Pre-processing directives are not processed when they appear inside multi-line input elements. For example, the program:

class Hello\
{\
static void Main() {\
System.Console.WriteLine(@"hello,\
\#if Debug\
world\
\#else\
Nebraska\
\#endif\
");\
}\
}

results in the output:

hello,\
\#if Debug\
world\
\#else\
Nebraska\
\#endif

In peculiar cases, the set of pre-processing directives that is processed might depend on the evaluation of the *pp-expression*. The example:

\#if X\
/\*\
\#else\
/\* \*/ class Q { }\
\#endif

always produces the same token stream (class Q { }), regardless of whether or not X is defined. If X is defined, the only processed directives are \#if and \#endif, due to the multi-line comment. If X is undefined, then three directives (\#if, \#else, \#endif) are part of the directive set. *end example*\]

### Diagnostic directives

The diagnostic directives are used to generate explicitly error and warning messages that are reported in the same way as other compile-time errors and warnings.

[]{#Grammar_pp_diagnostic .anchor}pp-diagnostic::\
whitespace~opt~ *\#* whitespace~opt~ *error* pp-message\
whitespace~opt~ *\#* whitespace~opt~ *warning* pp-message

[]{#Grammar_pp_message .anchor}pp-message::\
new-line\
whitespace input-characters~opt~ new-line

\[*Example*: The example

\#if Debug && Retail\
\#error A build can't be both debug and retail\
\#endif

class Test {…}

produces a compile-time error (“A build can’t be both debug and retail”) if the conditional compilation symbols Debug and Retail are both defined. Note that a *pp-message* can contain arbitrary text; specifically, it need not contain well-formed tokens, as shown by the single quote in the word can't. *end example*\]

### Region directives

The region directives are used to mark explicitly regions of source code.

[]{#Grammar_pp_region .anchor}pp-region::\
pp-start-region conditional-section~opt~ pp-end-region

[]{#Grammar_pp_start_region .anchor}pp-start-region::\
whitespace~opt~ *\#* whitespace~opt~ *region* pp-message

[]{#Grammar_pp_end_region .anchor}pp-end-region::\
whitespace~opt~ *\#* whitespace~opt~ *endregion* pp-message

No semantic meaning is attached to a region; regions are intended for use by the programmer or by automated tools to mark a section of source code. The message specified in a \#region or \#endregion directive likewise has no semantic meaning; it merely serves to identify the region. Matching \#region and \#endregion directives may have different *pp-message*s.

The lexical processing of a region:

\#region\
…\
\#endregion

corresponds exactly to the lexical processing of a conditional compilation directive of the form:

\#if true\
…\
\#endif

### Line directives

Line directives may be used to alter the line numbers and source file names that are reported by the compiler in output such as warnings and errors. These values are also used by caller-info attributes (§22.5.5).

\[*Note*: Line directives are most commonly used in meta-programming tools that generate C\# source code from some other text input. *end note*\]

[]{#Grammar_pp_line .anchor}pp-line::\
whitespace~opt~ *\#* whitespace~opt~ *line* whitespace line-indicator pp-new-line

[]{#Grammar_line_indicator .anchor}line-indicator::\
decimal-digits whitespace file-name~\
~decimal-digits~\
~*default\
hidden*

[]{#Grammar_file_name .anchor}file-name::\
*"* file-name-characters *"*

[]{#Grammar_file_name_characters .anchor}file-name-characters::\
file-name-character\
file-name-characters file-name-character

[]{#Grammar_file_name_character .anchor}file-name-character::\
Any input-character except *"* *(U+0022)*, and new-line-character

When no \#line directives are present, the compiler reports true line numbers and source file names in its output. When processing a \#line directive that includes a *line-indicator* that is not default, the compiler treats the line *after* the directive as having the given line number (and file name, if specified).

A \#line default directive undoes the effect of all preceding \#line directives. The compiler reports true line information for subsequent lines, precisely as if no \#line directives had been processed.

A \#line hidden directive has no effect on the file and line numbers reported in error messages, or produced by use of CallerLineNumberAttribute (§22.5.5.2). It is intended to affect source level debugging tools so that, when debugging, all lines between a \#line hidden directive and the subsequent \#line directive (that is not \#line hidden) have no line number information, and are skipped entirely when stepping through code.

\[*Note*: Note that a *file-name* differs from a regular string literal in that escape characters are not processed; the ‘\\’ character simply designates an ordinary backslash character within a *file-name*. *end note*\]

### Pragma directives

The \#pragma preprocessing directive is used to specify contextual information to a compiler. \[*Note*: For example, a compiler might provide \#pragma directives that

-   Enable or disable particular warning messages when compiling subsequent code.

-   Specify which optimizations to apply to subsequent code.

-   Specify information to be used by a debugger.

*end note*\]

[]{#Grammar_pp_pragma_body .anchor}pp-pragma::\
whitespaceopt *\#* whitespace~opt~ *pragma* pp-pragma-text

[]{#Grammar_pp_pragma_text .anchor}pp-pragma-text::\
new-line\
whitespace input-characters~opt~ new-line

The *input-characters* in the *pp-pragma-text* are interpreted by the compiler in an implementation-defined manner. The information supplied in a \#pragma directive shall not change program semantics. A \#pragma directive shall only change compiler behavior that is outside the scope of this language specification. If the compiler cannot interpret the *input-characters*, the compiler can produce a warning; however, it shall not produce a compile-time error.

\[*Note*: *pp-pragma-text* can contain arbitrary text; specifically, it need not contain well-formed tokens. *end note*\]

