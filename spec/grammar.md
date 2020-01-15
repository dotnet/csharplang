# Grammar {#grammar .Appendix1}

**This clause is informative.**

## General {#general-106 .Appendix2}

This annex contains summaries of the lexical and syntactic grammars found in the main document, and of the grammar extensions for unsafe code. Grammar productions appear here in the same order that they appear in the main document.

## Lexical grammar {#lexical-grammar-1 .Appendix2}

input::\
input-section~opt~

input-section::\
input-section-part\
input-section input-section-part

input-section-part::\
input-elements~opt~ new-line\
pp-directive

input-elements::\
input-element\
input-elements input-element

input-element::\
whitespace\
comment\
token

Line terminators

new-line::\
Carriage return character (*U+000D*)\
Line feed character (*U+000A*)\
Carriage return character (*U+000D*) followed by line feed character (*U+000A*)\
Next line character (*U+0085*)\
Line separator character (*U+2028*)\
Paragraph separator character (*U+2029*)

White space

whitespace::\
whitespace-character\
whitespace whitespace-character

whitespace-character::\
Any character with Unicode class Zs\
Horizontal tab character (*U+0009*)\
Vertical tab character (*U+000B*)\
Form feed character (*U+000C*)

### Comments {#comments-1 .Appendix3}

comment::\
single-line-comment\
delimited-comment

single-line-comment::\
*//* input-characters~opt~

input-characters::\
input-character\
input-characters input-character

input-character::\
Any Unicode character except a new-line-character

new-line-character::\
Carriage return character (*U+000D*)\
Line feed character (*U+000A*)\
Next line character (*U+0085*)\
Line separator character (*U+2028*)\
Paragraph separator character (*U+2029*)

delimited-comment::\
*/\** delimited-comment-text~opt~ asterisks */*

delimited-comment-text::\
delimited-comment-section\
delimited-comment-text delimited-comment-section

delimited-comment-section::\
/\
asterisks~opt~ not-slash-or-asterisk

asterisks::\
*\**\
asterisks *\**

not-slash-or-asterisk::\
Any Unicode character except */ or \**

### Tokens {#tokens-1 .Appendix3}

token::\
identifier\
keyword\
integer-literal\
real-literal\
character-literal\
string-literal\
operator-or-punctuator

Unicode character escape sequences

unicode-escape-sequence::\
*\\u* hex-digit hex-digit hex-digit hex-digit\
*\\U* hex-digit hex-digit hex-digit hex-digit hex-digit hex-digit hex-digit hex-digit

Identifiers

identifier::\
available-identifier\
*@* identifier-or-keyword

available-identifier::\
An identifier-or-keyword that is not a keyword

identifier-or-keyword::\
identifier-start-character identifier-part-characters~opt~

identifier-start-character::\
letter-character\
*underscore-character*

underscore-character::\
\_ (the underscore character U+005F)\
A unicode-escape-sequence representing the character U+005F

identifier-part-characters::\
identifier-part-character\
identifier-part-characters identifier-part-character

identifier-part-character::\
letter-character\
decimal-digit-character\
connecting-character\
combining-character\
formatting-character

letter-character::\
A Unicode character of classes Lu, Ll, Lt, Lm, Lo, or Nl\
A unicode-escape-sequence representing a character of classes Lu, Ll, Lt, Lm, Lo, or Nl

combining-character::\
A Unicode character of classes Mn or Mc\
A unicode-escape-sequence representing a character of classes Mn or Mc

decimal-digit-character::\
A Unicode character of the class Nd\
A unicode-escape-sequence representing a character of the class Nd

connecting-character::\
A Unicode character of the class Pc\
A unicode-escape-sequence representing a character of the class Pc

formatting-character::\
A Unicode character of the class Cf\
A unicode-escape-sequence representing a character of the class Cf

### Keywords {#keywords-1 .Appendix3}

keyword:: one of\
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

Literals

literal::\
boolean-literal\
integer-literal\
real-literal\
character-literal\
string-literal\
null-literal

boolean-literal::\
*true\
false*

integer-literal::\
decimal-integer-literal\
hexadecimal-integer-literal

decimal-integer-literal::\
decimal-digits integer-type-suffix~opt~

decimal-digits::\
decimal-digit\
decimal-digits decimal-digit

decimal-digit:: one of\
*0 1 2 3 4 5 6 7 8 9*

integer-type-suffix:: one of\
*U u L l UL Ul uL ul LU Lu lU lu*

hexadecimal-integer-literal::\
*0x* hex-digits integer-type-suffix~opt~\
*0X* hex-digits integer-type-suffix~opt~

hex-digits::\
hex-digit\
hex-digits hex-digit

hex-digit:: one of\
*0 1 2 3 4 5 6 7 8 9 A B C D E F a b c d e f*

real-literal::\
decimal-digits *.* decimal-digits exponent-part~opt~ real-type-suffix~opt~\
*.* decimal-digits exponent-part~opt~ real-type-suffix~opt~\
decimal-digits exponent-part real-type-suffix~opt~\
decimal-digits real-type-suffix

exponent-part::\
*e* sign~opt~ decimal-digits\
*E* sign~opt~ decimal-digits

sign:: one of\
*+ -*

real-type-suffix:: one of\
*F f D d M m*

character-literal::\
*'* character *'*

character::\
single-character\
simple-escape-sequence\
hexadecimal-escape-sequence\
unicode-escape-sequence

single-character::\
Any character except *'* (*U+0027*), *\\* (*U+005C*), and new-line-character

simple-escape-sequence:: one of\
*\\' \\" \\\\ \\0 \\a \\b \\f \\n \\r \\t \\v*

hexadecimal-escape-sequence::\
*\\x* hex-digit hex-digit~opt~ hex-digit~opt~ hex-digit~opt~

string-literal::\
regular-string-literal\
verbatim-string-literal

regular-string-literal::\
*"* regular-string-literal-characters~opt~ *"*

regular-string-literal-characters::\
regular-string-literal-character\
regular-string-literal-characters regular-string-literal-character

regular-string-literal-character::\
single-regular-string-literal-character\
simple-escape-sequence\
hexadecimal-escape-sequence\
unicode-escape-sequence

single-regular-string-literal-character::\
Any character except *"* (*U+0022*), *\\* (*U+005C*), and new-line-character

verbatim-string-literal::\
*@"* verbatim-string-literal-characters~opt~ *"*

verbatim-string-literal-characters::\
verbatim-string-literal-character\
verbatim-string-literal-characters verbatim-string-literal-character

verbatim-string-literal-character::\
single-verbatim-string-literal-character\
quote-escape-sequence

single-verbatim-string-literal-character::\
Any character except *"*

quote-escape-sequence::\
*""*

null-literal::\
*null*

### Operators and punctuators {#operators-and-punctuators-1 .Appendix3}

operator-or-punctuator:: *one of*\
*{ } \[ \] ( ) . , : ;\
+ - \* / % & | \^ ! \~\
= &lt; &gt; ? ?? :: ++ -- && ||\
-&gt; == != &lt;= &gt;= += -= \*= /= %=\
&= |= \^= &lt;&lt; &lt;&lt;=*

right-shift::\
*&gt;* *&gt;*

right-shift-assignment::\
*&gt;* *&gt;=*

### Pre-processing directives {#pre-processing-directives-1 .Appendix3}

pp-directive::\
pp-declaration\
pp-conditional\
pp-line\
pp-diagnostic\
pp-region\
pp-pragma

conditional-symbol::\
Any identifier-or-keyword except *true* or *false*

pp-expression::\
whitespace~opt~ pp-or-expression whitespace~opt~

pp-or-expression::\
pp-and-expression\
pp-or-expression whitespace~opt~ *||* whitespace~opt~ pp-and-expression

pp-and-expression::\
pp-equality-expression\
pp-and-expression whitespace~opt~ *&&* whitespace~opt~ pp-equality-expression

pp-equality-expression::\
pp-unary-expression\
pp-equality-expression whitespace~opt~ *==* whitespace~opt~ pp-unary-expression\
pp-equality-expression whitespace~opt~ *!=* whitespace~opt~ pp-unary-expression

pp-unary-expression::\
pp-primary-expression\
*!* whitespace~opt~ pp-unary-expression

pp-primary-expression::\
*true*\
*false*\
conditional-symbol\
*(* whitespace~opt~ pp-expression whitespace~opt~ *)*

pp-declaration::\
whitespace~opt~ *\#* whitespace~opt~ *define* whitespace conditional-symbol pp-new-line\
whitespace~opt~ *\#* whitespace~opt~ *undef* whitespace conditional-symbol pp-new-line

pp-new-line::\
whitespace~opt~ single-line-comment~opt~ new-line

pp-conditional::\
pp-if-section pp-elif-sections~opt~ pp-else-section~opt~ pp-endif

pp-if-section::\
whitespace~opt~ *\#* whitespace~opt~ *if* whitespace pp-expression pp-new-line\
conditional-section~opt~

pp-elif-sections::\
pp-elif-section\
pp-elif-sections pp-elif-section

pp-elif-section::\
whitespace~opt~ *\#* whitespace~opt~ *elif* whitespace pp-expression pp-new-line\
conditional-section~opt~

pp-else-section::\
whitespace~opt~ *\#* whitespace~opt~ *else* pp-new-line conditional-section~opt~

pp-endif::\
whitespace~opt~ *\#* whitespace~opt~ *endif* pp-new-line

conditional-section::\
input-section\
skipped-section

skipped-section::\
skipped-section-part\
skipped-section skipped-section-part

skipped-section-part::\
skipped-characters~opt~ new-line\
pp-directive

skipped-characters::\
whitespace~opt~ not-number-sign input-characters~opt~

not-number-sign::\
Any input-character except *\#*

pp-line::\
whitespace~opt~ *\#* whitespace~opt~ *line* whitespace line-indicator pp-new-line

line-indicator::\
decimal-digits whitespace file-name~\
~decimal-digits~\
~*default\
hidden*

file-name::\
*"* file-name-characters *"*

file-name-characters::\
file-name-character\
file-name-characters file-name-character

file-name-character::\
Any input-character except *"* *(U+0022)*, and new-line-character

pp-diagnostic::\
whitespace~opt~ *\#* whitespace~opt~ *error* pp-message\
whitespace~opt~ *\#* whitespace~opt~ *warning* pp-message

pp-message::\
new-line\
whitespace input-characters~opt~ new-line

pp-region::\
pp-start-region conditional-section~opt~ pp-end-region

pp-start-region::\
whitespace~opt~ *\#* whitespace~opt~ *region* pp-message

pp-end-region::\
whitespace~opt~ *\#* whitespace~opt~ *endregion* pp-message

pp-pragma::\
whitespaceopt *\#* whitespace~opt~ *pragma* pp-pragma-text

pp-pragma-text::\
new-line\
whitespace input-characters~opt~ new-line

## Syntactic grammar {#syntactic-grammar-1 .Appendix2}

### Basic concepts {#basic-concepts-1 .Appendix3}

namespace-name:\
namespace-or-type-name

type-name:\
namespace-or-type-name

namespace-or-type-name:\
identifier type-argument-list~opt\
~namespace-or-type-name *.* identifier type-argument-list~opt~\
qualified-alias-member

### Types {#types-1 .Appendix3}

type:\
reference-type\
value-type\
type-parameter

value-type:\
struct-type\
enum-type

struct-type:\
type-name\
simple-type\
nullable-value-type

simple-type:\
numeric-type\
*bool*

numeric-type:\
integral-type\
floating-point-type\
*decimal*

integral-type:\
*sbyte\
byte\
short\
ushort\
int\
uint\
long\
ulong\
char*

nullable-type:\
non-nullable-value-type *?*

non-nullable-value-type:\
type

floating-point-type:\
*float\
double*

enum-type:\
type-name

type-argument-list:\
*&lt;* type-arguments *&gt;*

type-arguments:\
type-argument\
type-arguments *,* type-argument

type-argument:\
type

type-parameter:\
identifier

### Variables {#variables-1 .Appendix3}

variable-reference:\
expression

### Expressions {#expressions-6 .Appendix3}

argument-list:\
argument\
argument-list *,* argument

argument:\
argument-name~opt~ argument-value

argument-name:\
identifier *:*

argument-value:\
expression\
*ref* variable-reference\
*out* variable-reference

primary-expression:\
primary-no-array-creation-expression\
array-creation-expression

primary-no-array-creation-expression:\
literal\
simple-name\
parenthesized-expression\
member-access\
invocation-expression\
element-access\
this-access\
base-access\
post-increment-expression\
post-decrement-expression\
object-creation-expression\
delegate-creation-expression\
anonymous-object-creation-expression\
typeof-expression\
sizeof-expression\
checked-expression\
unchecked-expression\
default-value-expression\
anonymous-method-expression

simple-name:\
identifier type-argument-list~opt~

parenthesized-expression:\
*(* expression *)*

member-access:\
primary-expression *.* identifier type-argument-list~opt~\
predefined-type *.* identifier type-argument-list~opt\
~qualified-alias-member *.* identifier type-argument-list~opt~

predefined-type: one of\
*bool byte char decimal double float int long\
object sbyte short string uint ulong ushort*

invocation-expression:\
primary-expression *(* argument-list~opt~ *)*

element-access:\
primary-no-array-creation-expression *\[* argument-list *\]*

expression-list:\
expression\
expression-list *,* expression

this-access:\
*this*

base-access:\
*base* *.* identifier type-argument-list~opt~\
*base* *\[* argument-list *\]*

post-increment-expression:\
primary-expression *++*

post-decrement-expression:\
primary-expression *--*

object-creation-expression:\
*new* type *(* argument-list~opt~ *)* object-or-collection-initializer~opt~\
*new* type object-or-collection-initializer

object-or-collection-initializer:\
object-initializer\
collection-initializer

object-initializer:\
*{* member-initializer-list~opt~ *}*\
*{* member-initializer-list *,* *}*

member-initializer-list:\
member-initializer\
member-initializer-list *,* member-initializer

member-initializer:\
identifier *=* initializer-value

initializer-value:\
expression\
object-or-collection-initializer

collection-initializer:\
*{* element-initializer-list *}*\
*{* element-initializer-list *,* *}*

element-initializer-list:\
element-initializer\
element-initializer-list *,* element-initializer

element-initializer:\
non-assignment-expression\
*{* expression-list *}*

array-creation-expression:\
*new* non-array-type *\[* expression-list *\]* rank-specifiers~opt~ array-initializer~opt~\
*new* array-type array-initializer\
*new* rank-specifier array-initializer

delegate-creation-expression:\
*new* delegate-type *(* expression *)*

anonymous-object-creation-expression:\
*new* anonymous-object-initializer

anonymous-object-initializer:\
*{* member-declarator-list~opt~ *}*\
*{* member-declarator-list *,* *}*

member-declarator-list:\
member-declarator\
member-declarator-list *,* member-declarator

member-declarator:\
simple-name\
member-access\
base-access\
identifier = expression

typeof-expression:\
*typeof* *(* type *)\
typeof* *(* unbound-type-name *)\
typeof ( void )*

unbound-type-name:\
identifier generic-dimension-specifier~opt~\
identifier *::* identifier generic-dimension-specifier~opt~\
unbound-type-name ***.*** identifier generic-dimension-specifier~opt~

generic-dimension-specifier:\
*&lt;* commas~opt~ *&gt;*

commas:\
*,*\
commas *,*

checked-expression:\
*checked* *(* expression *)*

unchecked-expression:\
*unchecked* *(* expression *)*

default-value-expression:\
*default* *(* type *)*

unary-expression:\
primary-expression\
*+* unary-expression\
*-* unary-expression\
*!* unary-expression\
*\~* unary-expression\
pre-increment-expression\
pre-decrement-expression\
cast-expression\
await-expression

pre-increment-expression:\
*++* unary-expression

pre-decrement-expression:\
*--* unary-expression

cast-expression:\
*(* type *)* unary-expression

await-expression:\
*await* unary-expression

multiplicative-expression:\
unary-expression\
multiplicative-expression *\** unary-expression\
multiplicative-expression */* unary-expression\
multiplicative-expression *%* unary-expression

additive-expression:\
multiplicative-expression\
additive-expression *+* multiplicative-expression\
additive-expression *–* multiplicative-expression

shift-expression:\
additive-expression\
shift-expression *&lt;&lt;* additive-expression\
shift-expression right-shift additive-expression

relational-expression:\
shift-expression\
relational-expression *&lt;* shift-expression\
relational-expression *&gt;* shift-expression\
relational-expression *&lt;=* shift-expression\
relational-expression *&gt;=* shift-expression\
relational-expression *is* type\
relational-expression *as* type

equality-expression:\
relational-expression\
equality-expression *==* relational-expression\
equality-expression *!=* relational-expression

and-expression:\
equality-expression\
and-expression *&* equality-expression

exclusive-or-expression:\
and-expression\
exclusive-or-expression *\^* and-expression

inclusive-or-expression:\
exclusive-or-expression\
inclusive-or-expression *|* exclusive-or-expression

conditional-and-expression:\
inclusive-or-expression\
conditional-and-expression *&&* inclusive-or-expression

conditional-or-expression:\
conditional-and-expression\
conditional-or-expression *||* conditional-and-expression

null-coalescing-expression:\
conditional-or-expression\
conditional-or-expression *??* null-coalescing-expression

conditional-expression:\
null-coalescing-expression\
null-coalescing-expression *?* expression *:* expression

lambda-expression:\
*async~opt~* anonymous-function-signature *=&gt;* anonymous-function-body

anonymous-method-expression:\
*async~opt~* *delegate* explicit-anonymous-function-signature~opt~ block

anonymous-function-signature:\
explicit-anonymous-function-signature\
implicit-anonymous-function-signature

explicit-anonymous-function-signature:\
*(* explicit-anonymous-function-parameter-list*opt* )

explicit-anonymous-function-parameter-list:\
explicit-anonymous-function-parameter\
explicit-anonymous-function-parameter-list *,* explicit-anonymous-function-parameter

explicit-anonymous-function-parameter:\
anonymous-function-parameter-modifier~opt~ type identifier

anonymous-function-parameter-modifier:\
*ref\
out*

implicit-anonymous-function-signature:\
( implicit-anonymous-function-parameter-list*~opt~* )\
implicit-anonymous-function-parameter

implicit-anonymous-function-parameter-list:\
implicit-anonymous-function-parameter\
implicit-anonymous-function-parameter-list *,* implicit-anonymous-function-parameter

implicit-anonymous-function-parameter:\
identifier

anonymous-function-body:\
expression\
block

query-expression:\
from-clause query-body

from-clause:\
*from* type~opt~ identifier *in* expression

query-body:\
query-body-clauses~opt~ select-or-group-clause query-continuation~opt~

query-body-clauses:\
query-body-clause\
query-body-clauses query-body-clause

query-body-clause:\
from-clause\
let-clause\
where-clause\
join-clause\
join-into-clause\
orderby-clause

let-clause:\
*let* identifier *=* expression

where-clause:\
*where* boolean-expression

join-clause:\
*join* type~opt~ identifier *in* expression *on* expression *equals* expression

join-into-clause:\
*join* type~opt~ identifier *in* expression *on* expression *equals* expression *into* identifier

orderby-clause:\
*orderby* orderings

orderings:\
ordering\
orderings *,* ordering

ordering:\
expression ordering-direction~opt~

ordering-direction:\
*ascending*\
*descending*

select-or-group-clause:\
select-clause\
group-clause

select-clause:\
*select* expression

group-clause:\
*group* expression *by* expression

query-continuation:\
*into* identifier query-body

assignment:\
unary-expression assignment-operator expression

assignment-operator:\
*=\
+=\
-=\
\*=\
/=\
%=\
&=\
|=\
\^=\
&lt;&lt;=\
*right-shift-assignment

expression:\
non-assignment-expression\
assignment

non-assignment-expression:\
conditional-expression\
lambda-expression\
query-expression

constant-expression:\
expression

boolean-expression:\
expression

### Statements {#statements-1 .Appendix3}

statement:\
labeled-statement\
declaration-statement\
embedded-statement

embedded-statement:\
block\
empty-statement\
expression-statement\
selection-statement\
iteration-statement\
jump-statement\
try-statement\
checked-statement\
unchecked-statement\
lock-statement\
using-statement\
yield-statement

block:\
*{* statement-list~opt~ *}*

statement-list:\
statement\
statement-list statement

empty-statement:\
*;*

labeled-statement:\
identifier *:* statement

declaration-statement:\
local-variable-declaration *;*\
local-constant-declaration *;*

local-variable-declaration:\
local-variable-type local-variable-declarators

local-variable-type:\
type\
var

local-variable-declarators:\
local-variable-declarator\
local-variable-declarators *,* local-variable-declarator

local-variable-declarator:\
identifier\
identifier = local-variable-initializer

local-variable-initializer:\
expression\
array-initializer

local-constant-declaration:\
*const* type constant-declarators

constant-declarators:\
constant-declarator\
constant-declarators *,* constant-declarator

constant-declarator:\
identifier = constant-expression

expression-statement:\
statement-expression *;*

statement-expression:\
invocation-expression\
object-creation-expression\
assignment\
post-increment-expression\
post-decrement-expression\
pre-increment-expression\
pre-decrement-expression\
await-expression

selection-statement:\
if-statement\
switch-statement

if-statement:\
*if* *(* boolean-expression *)* embedded-statement\
*if* *(* boolean-expression *)* embedded-statement *else* embedded-statement

switch-statement:\
*switch* *(* expression *)* switch-block

switch-block:\
*{* switch-sections~opt~ *}*

switch-sections:\
switch-section\
switch-sections switch-section

switch-section:\
switch-labels statement-list

switch-labels:\
switch-label\
switch-labels switch-label

switch-label:\
*case* constant-expression *:*\
*default* *:*

iteration-statement:\
while-statement\
do-statement\
for-statement\
foreach-statement

while-statement:\
*while* *(* boolean-expression *)* embedded-statement

do-statement:\
*do* embedded-statement *while* *(* boolean-expression *)* *;*

for-statement:\
*for* *(* for-initializer~opt~ *;* for-condition~opt~ *;* for-iterator~opt~ *)* embedded-statement

for-initializer:\
local-variable-declaration\
statement-expression-list

for-condition:\
boolean-expression

for-iterator:\
statement-expression-list

statement-expression-list:\
statement-expression\
statement-expression-list *,* statement-expression

foreach-statement:\
*foreach* *(* local-variable-type identifier *in* expression *)* embedded-statement

jump-statement:\
break-statement\
continue-statement\
goto-statement\
return-statement\
throw-statement

break-statement:\
*break* *;*

continue-statement:\
*continue* *;*

goto-statement:\
*goto* identifier *;\
goto* *case* constant-expression *;*\
*goto* *default* *;*

return-statement:\
*return* expression~opt~ *;*

throw-statement:\
*throw* expression~opt~ *;*

try-statement:\
*try* block catch-clauses\
*try* block catch-clauses~opt~ finally-clause

catch-clauses:\
specific-catch-clauses\
specific-catch-clauses~opt~ general-catch-clause

specific-catch-clauses:\
specific-catch-clause\
specific-catch-clauses specific-catch-clause

specific-catch-clause:\
*catch* *(* type identifier~opt~ *)* block

general-catch-clause:\
*catch* block

finally-clause:\
*finally* block

checked-statement:\
*checked* block

unchecked-statement:\
*unchecked* block

lock-statement:\
*lock* *(* expression *)* embedded-statement

using-statement:\
*using* *(* resource-acquisition *)* embedded-statement

resource-acquisition:\
local-variable-declaration\
expression

yield-statement:\
*yield* *return* expression *;*\
*yield* *break* *;*

###  Namespaces {#namespaces-1 .Appendix3}

compilation-unit:\
extern-alias-directives~opt~ using-directives~opt~ global-attributes~opt~\
namespace-member-declarations~opt~

namespace-declaration:\
*namespace* qualified-identifier namespace-body *;*~opt~

qualified-identifier:\
identifier\
qualified-identifier *.* identifier

namespace-body:\
*{* extern-alias-directives~opt~ using-directives~opt~ namespace-member-declarations~opt~ *}*

extern-alias-directives:\
extern-alias-directive\
extern-alias-directives extern-alias-directive

extern-alias-directive:\
*extern* *alias* identifier *;*

using-directives:\
using-directive\
using-directives using-directive

using-directive:\
using-alias-directive\
using-namespace-directive

using-alias-directive:\
*using* identifier *=* namespace-or-type-name *;*

using-namespace-directive:\
*using* namespace-name *;*

namespace-member-declarations:\
namespace-member-declaration\
namespace-member-declarations namespace-member-declaration

namespace-member-declaration:\
namespace-declaration\
type-declaration

type-declaration:\
class-declaration\
struct-declaration\
interface-declaration\
enum-declaration\
delegate-declaration

qualified-alias-member:\
identifier *::* identifier type-argument-list~opt~

### Classes {#classes-1 .Appendix3}

class-declaration:\
attributes~opt~ class-modifiers~opt~ *partial*~opt~ *class* identifier type-parameter-list~opt~\
class-base~opt~ type-parameter-constraints-clauses~opt~ class-body *;*~opt~

class-modifiers:\
class-modifier\
class-modifiers class-modifier

class-modifier:\
*new*\
*public\
protected\
internal*\
*private*\
*abstract*\
*sealed\
static*

type-parameter-list:\
*&lt;* type-parameters *&gt;*

type-parameters:\
attributes~opt~ type-parameter\
type-parameters *,* attributes~opt~ type-parameter

class-base:\
*:* class-type\
*:* interface-type-list\
*:* class-type *,* interface-type-list

interface-type-list:\
interface-type\
interface-type-list *,* interface-type

type-parameter-constraints-clauses:\
type-parameter-constraints-clause\
type-parameter-constraints-clauses type-parameter-constraints-clause

type-parameter-constraints-clause:\
*where* type-parameter *:* type-parameter-constraints

type-parameter-constraints:\
primary-constraint\
secondary-constraints\
constructor-constraint\
primary-constraint *,* secondary-constraints\
primary-constraint *,* constructor-constraint\
secondary-constraints *,* constructor-constraint\
primary-constraint *,* secondary-constraints *,* constructor-constraint

primary-constraint:\
class-type\
*class*\
*struct*

secondary-constraints:\
interface-type\
type-parameter\
secondary-constraints *,* interface-type\
secondary-constraints *,* type-parameter

constructor-constraint:\
*new* *(* *)*

class-body:\
*{* class-member-declarations~opt~ *}*

class-member-declarations:\
class-member-declaration\
class-member-declarations class-member-declaration

class-member-declaration:\
constant-declaration\
field-declaration\
method-declaration\
property-declaration\
event-declaration\
indexer-declaration\
operator-declaration\
constructor-declaration\
finalizer-declaration\
static-constructor-declaration\
type-declaration

constant-declaration:\
attributes~opt~ constant-modifiers~opt~ *const* type constant-declarators *;*

constant-modifiers:\
constant-modifier\
constant-modifiers constant-modifier

constant-modifier:\
*new*\
*public*\
*protected\
internal*\
*private*

constant-declarators:\
constant-declarator\
constant-declarators *,* constant-declarator

constant-declarator:\
identifier = constant-expression

field-declaration:\
attributes~opt~ field-modifiers~opt~ type variable-declarators *;*

field-modifiers:\
field-modifier\
field-modifiers field-modifier

field-modifier:\
*new*\
*public*\
*protected\
internal*\
*private\
static\
readonly\
volatile*

variable-declarators:\
variable-declarator\
variable-declarators *,* variable-declarator

variable-declarator:\
identifier\
identifier = variable-initializer

variable-initializer:\
expression\
array-initializermethod-declaration:\
method-header method-body

method-header:\
attributes~opt~ method-modifiers~opt~ partial~opt~ return-type member-name\
type-parameter-list~opt~*\
(* formal-parameter-list~opt~ *)* type-parameter-constraints-clauses~opt~

method-modifiers:\
method-modifier\
method-modifiers method-modifier

method-modifier:\
*new\
public\
protected\
internal\
private\
static\
virtual\
sealed\
override\
abstract\
extern\
async*

return-type:\
type\
*void*

method-body:\
block\
*;*

formal-parameter-list:\
fixed-parameters\
fixed-parameters *,* parameter-array\
parameter-array

fixed-parameters:\
fixed-parameter\
fixed-parameters *,* fixed-parameter

fixed-parameter:\
attributes~opt~ parameter-modifier~opt~ type identifier default-argument~opt~

default-argument:\
= expression

parameter-modifier:\
*parameter-mode-modifier\
this*

parameter-mode-modifier:\
*ref\
out*

parameter-array:\
attributes~opt~ *params* array-type identifier

property-declaration:\
attributes~opt~ property-modifiers~opt~ type member-name *{* accessor-declarations *}*

property-modifiers:\
property-modifier\
property-modifiers property-modifier

property-modifier:\
*new\
public\
protected\
internal\
private\
static\
virtual\
sealed\
override\
abstract\
extern*

accessor-declarations:\
get-accessor-declaration set-accessor-declaration~opt~\
set-accessor-declaration get-accessor-declaration~opt~

get-accessor-declaration:\
attributes~opt~ accessor-modifier~opt~ *get* accessor-body

set-accessor-declaration:\
attributes~opt~ accessor-modifier~opt~ *set* accessor-body

accessor-modifier:\
*protected\
internal\
private\
protected* *internal\
internal* *protected*

accessor-body:\
block\
*;*

event-declaration:\
attributes~opt~ event-modifiers~opt~ *event* type variable-declarators *;\
*attributes~opt~ event-modifiers~opt~ *event* type member-name\
*{* event-accessor-declarations *}*

event-modifiers:\
event-modifier\
event-modifiers event-modifier

event-modifier:\
*new\
public\
protected\
internal\
private\
static\
virtual\
sealed\
override\
abstract\
extern*

event-accessor-declarations:\
add-accessor-declaration remove-accessor-declaration\
remove-accessor-declaration add-accessor-declaration

add-accessor-declaration:\
attributes~opt~ *add* block

remove-accessor-declaration:\
attributes~opt~ *remove* block

indexer-declaration:\
attributes~opt~ indexer-modifiers~opt~ indexer-declarator *{* accessor-declarations *}*

indexer-modifiers:\
indexer-modifier\
indexer-modifiers indexer-modifier

indexer-modifier:\
*new\
public\
protected\
internal\
private\
virtual\
sealed\
override\
abstract\
extern*

indexer-declarator:\
type *this* *\[* formal-parameter-list *\]*\
type interface-type *.* *this* *\[* formal-parameter-list *\]*

operator-declaration:\
attributes~opt~ operator-modifiers operator-declarator operator-body

operator-modifiers:\
operator-modifier\
operator-modifiers operator-modifier

operator-modifier:\
*public\
static\
extern*

operator-declarator:\
unary-operator-declarator\
binary-operator-declarator\
conversion-operator-declarator

unary-operator-declarator:\
type *operator* overloadable-unary-operator *(* fixed-parameter *)*

overloadable-unary-operator: *one of*\
*+ - ! \~ ++ -- true false*

binary-operator-declarator:\
type *operator* overloadable-binary-operator *(* fixed-parameter *,* fixed-parameter *)*

overloadable-binary-operator: *one of*\
*+ - \* / % & | \^ &lt;&lt;* right-shift*\
== != &gt; &lt; &gt;= &lt;=*

conversion-operator-declarator:*\
implicit* *operator* type *(* fixed-parameter *)*\
*explicit* *operator* type *(* fixed-parameter *)*

operator-body:\
block\
*;*

constructor-declaration:\
attributes~opt~ constructor-modifiers~opt~ constructor-declarator constructor-body

constructor-modifiers:\
constructor-modifier\
constructor-modifiers constructor-modifier

constructor-modifier:\
*public*\
*protected\
internal*\
*private\
extern*

constructor-declarator:\
identifier *(* formal-parameter-list~opt~ *)* constructor-initializer~opt~

constructor-initializer:\
*:* *base* *(* argument-list~opt~ *)\
:* *this* *(* argument-list~opt~ *)*

constructor-body:\
block\
*;*

static-constructor-declaration:\
attributes~opt~ static-constructor-modifiers identifier *(* *)* static-constructor-body

static-constructor-modifiers:\
*extern*~opt~ *static*\
*static extern*~opt~

static-constructor-body:\
block*\
*;

finalizer-declaration:\
attributes~opt~ *extern*~opt~ *\~* identifier *(* *)* finalizer-body

finalizer-body:\
block\
*;*

### Structs {#structs-1 .Appendix3}

struct-declaration:\
attributes~opt~ struct-modifiers~opt~ *partial*~opt~ *struct* identifier type-parameter-list~opt\
~struct-interfaces~opt~ type-parameter-constraints-clauses~opt~ struct-body *;*~opt~

struct-modifiers:\
struct-modifier\
struct-modifiers struct-modifier

struct-modifier:\
*new*\
*public\
protected\
internal*\
*private*

struct-interfaces:\
*:* interface-type-list

struct-body:\
*{* struct-member-declarations~opt~ *}*

struct-member-declarations:\
struct-member-declaration\
struct-member-declarations struct-member-declaration

struct-member-declaration:\
…\
fixed-size-buffer-declaration

### Arrays {#arrays-1 .Appendix3}

array-initializer:\
*{* variable-initializer-list~opt~ *}*\
*{* variable-initializer-list *,* *}*

variable-initializer-list:\
variable-initializer\
variable-initializer-list *,* variable-initializer

variable-initializer:\
expression\
array-initializer

### Interfaces {#interfaces-1 .Appendix3}

interface-declaration:\
attributes~opt~ interface-modifiers~opt~ *partial*~opt~ *interface\
*identifier variant-type-parameter-list~opt\
~ interface-base~opt~ type-parameter-constraints-clauses~opt~ interface-body *;*~opt~

interface-modifiers:\
interface-modifier\
interface-modifiers interface-modifier

interface-modifier:\
*new*\
*public\
protected\
internal*\
*private*

variant-type-parameter-list:\
*&lt;* variant-type-parameters *&gt;*

variant-type-parameters:\
attributes~opt~ variance-annotation~opt~ type-parameter\
variant-type-parameters *,* attributes~opt~ variance-annotation~opt~ type-parameter

variance-annotation:\
*in*\
*out*

interface-base:\
*:* interface-type-list

interface-body:\
*{* interface-member-declarations~opt~ *}*

interface-member-declarations:\
interface-member-declaration\
interface-member-declarations interface-member-declaration

interface-member-declaration:\
interface-method-declaration\
interface-property-declaration\
interface-event-declaration\
interface-indexer-declaration

interface-method-declaration:\
attributes~opt~ *new*~opt~ return-type identifier type-parameter-list~opt~\
*(* formal-parameter-list~opt~ *)* type-parameter-constraints-clauses~opt~ *;*

interface-property-declaration:\
attributes~opt~ *new*~opt~ type identifier *{* interface-accessors *}*

interface-accessors:\
attributes~opt~ *get* *;\
*attributes~opt~ *set* *;\
*attributes~opt~ *get* *;* attributes~opt~ *set* *;*\
attributes~opt~ *set* *;* attributes~opt~ *get* *;*

interface-event-declaration:\
attributes~opt~ *new*~opt~ *event* type identifier *;*

interface-indexer-declaration:\
attributes~opt~ *new*~opt~ type *this* *\[* formal-parameter-list *\]* *{* interface-accessors *}*

### Enums {#enums-1 .Appendix3}

enum-declaration:\
attributes~opt~ enum-modifiers~opt~ *enum* identifier enum-base~opt~ enum-body *;*~opt~

enum-base:\
*:* integral-type

enum-body:\
*{* enum-member-declarations~opt~ *}*\
*{* enum-member-declarations *,* *}*

enum-modifiers:\
enum-modifier\
enum-modifiers enum-modifier

enum-modifier:\
*new*\
*public\
protected\
internal*\
*private*

enum-member-declarations:\
enum-member-declaration\
enum-member-declarations *,* enum-member-declaration

enum-member-declaration:\
attributes~opt~ identifier\
attributes~opt~ identifier *=* constant-expression

### Delegates {#delegates-1 .Appendix3}

delegate-declaration:\
attributes~opt~ delegate-modifiers~opt~ *delegate* return-type\
identifier variant-type-parameter-list~opt\
~ *(* formal-parameter-list~opt~ *)* type-parameter-constraints-clauses~opt~ *;*

delegate-modifiers:\
delegate-modifier\
delegate-modifiers delegate-modifier

delegate-modifier:\
*new*\
*public\
protected\
internal*\
*private*

### Attributes {#attributes-1 .Appendix3}

global-attributes:\
global-attribute-sections

global-attribute-sections:\
global-attribute-section\
global-attribute-sections global-attribute-section

global-attribute-section:\
*\[* global-attribute-target-specifier attribute-list *\]\
\[* global-attribute-target-specifier attribute-list , *\]*

global-attribute-target-specifier:\
global-attribute-target *:*

global-attribute-target:\
identifier *equal to assembly or module*

attributes:\
attribute-sections

attribute-sections:\
attribute-section\
attribute-sections attribute-section

attribute-section:\
*\[* attribute-target-specifier~opt~ attribute-list *\]\
\[* attribute-target-specifier~opt~ attribute-list , *\]*

attribute-target-specifier:\
attribute-target *:*

attribute-target:\
identifier *not equal to assembly or module\
keyword*

attribute-list:\
attribute\
attribute-list *,* attribute

attribute:\
attribute-name attribute-arguments~opt~

attribute-name:\
type-name

attribute-arguments:\
*(* positional-argument-list~opt~ *)\
(* positional-argument-list *,* named-argument-list *)\
(* named-argument-list *)*

positional-argument-list:\
positional-argument\
positional-argument-list *,* positional-argument

positional-argument:\
argument-name~opt~ attribute-argument-expression

named-argument-list:\
named-argument\
named-argument-list *,* named-argument

named-argument:\
identifier *=* attribute-argument-expression

attribute-argument-expression:\
expression

## Grammar extensions for unsafe code {#grammar-extensions-for-unsafe-code .Appendix2}

class-modifier:\
…\
*unsafe*

struct-modifier:\
…\
*unsafe*

interface-modifier:\
…\
*unsafe*

delegate-modifier:\
…\
*unsafe*

field-modifier:\
…\
*unsafe*

method-modifier:\
…\
*unsafe*

property-modifier:\
…\
*unsafe*

event-modifier:\
…\
*unsafe*

indexer-modifier:\
…\
*unsafe*

operator-modifier:\
…\
*unsafe*

constructor-modifier:\
…\
*unsafe*

finalizer-declaration:\
attributes~opt~ *extern*~opt~ *unsafe*~opt~ *\~* identifier *(* *)* finalizer-body\
attributes~opt~ *unsafe*~opt~ *extern*~opt~ *\~* identifier *(* *)* finalizer-body

static-constructor-modifiers:\
*extern*~opt~ *unsafe*~opt~ *static*\
*unsafe*~opt~ *extern*~opt~ *static*\
*extern*~opt~ *static* *unsafe*~opt~\
*unsafe*~opt~ *static* *extern*~opt~\
*static* *extern*~opt~ *unsafe*~opt~\
*static* *unsafe*~opt~ *extern*~opt~

embedded-statement:\
…\
unsafe-statement

unsafe-statement:\
*unsafe* block

type:\
…\
pointer-type

non-array-type:\
…\
pointer-type

pointer-type:\
unmanaged-type *\**\
*void* *\**

unmanaged-type:\
type

primary-no-array-creation-expression:\
…\
pointer-member-access\
pointer-element-access

unary-expression:\
…\
pointer-indirection-expression\
addressof-expression

pointer-indirection-expression:\
*\** unary-expression

pointer-member-access:\
primary-expression *-&gt;* identifier type-argument-list~opt~

pointer-element-access:\
primary-no-array-creation-expression *\[* expression *\]*

addressof-expression:\
*&* unary-expression

embedded-statement:\
…\
fixed-statement

fixed-statement:\
*fixed* *(* pointer-type fixed-pointer-declarators *)* embedded-statement

fixed-pointer-declarators:\
fixed-pointer-declarator\
fixed-pointer-declarators *,* fixed-pointer-declarator

fixed-pointer-declarator:\
identifier *=* fixed-pointer-initializer

fixed-pointer-initializer:\
*&* variable-reference\
expression

struct-member-declaration:\
…\
fixed-size-buffer-declaration

fixed-size-buffer-declaration:\
attributes~opt~ fixed-size-buffer-modifiers~opt~ *fixed* buffer-element-type\
fixed-size-buffer-declarators *;*

fixed-size-buffer-modifiers:\
fixed-size-buffer-modifier\
fixed-size-buffer-modifier fixed-size-buffer-modifiers

fixed-size-buffer-modifier:\
*new*\
*public*\
*protected\
internal*\
*private\
unsafe*

buffer-element-type:\
type

fixed-size-buffer-declarators:\
fixed-size-buffer-declarator\
fixed-size-buffer-declarator *,* fixed-size-buffer-declarators

fixed-size-buffer-declarator:\
identifier *\[* constant-expression *\]*

local-variable-initializer:\
…\
stackalloc-initializer

stackalloc-initializer:\
*stackalloc* unmanaged-type *\[* expression *\]*

**End of informative text.**
