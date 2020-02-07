# Statements

## General

C# provides a variety of statements. 

> [!NOTE] 
> Most of these statements will be familiar to developers who have programmed in C and C++.

[#Grammar_statement .anchor](#Grammar_statement .anchor)

```ANTLR
statement:
    labeled-statement
    declaration-statement
    embedded-statement
```

[#Grammar_embedded_statement .anchor](#Grammar_embedded_statement .anchor)

```ANTLR
embedded-statement:
    block
    empty-statement
    expression-statement
    selection-statement
    iteration-statement
    jump-statement
    try-statement
    checked-statement
    unchecked-statement
    lock-statement
    using-statement
    yield-statement
```

The *embedded-statement* nonterminal is used for statements that appear within other statements. The use of *embedded-statement* rather than *statement* excludes the use of declaration statements and labeled statements in these contexts. \[*Example*: The code

```csharp
void F(bool b) {
	if (b)
	int i = 44;
}
```

results in a compile-time error because an if statement requires an *embedded-statement* rather than a *statement* for its if branch. If this code were permitted, then the variable i would be declared, but it could never be used. Note, however, that by placing i’s declaration in a block, the example is valid. *end example*\]

## End points and reachability

Every statement has an ***end point***. In intuitive terms, the end point of a statement is the location that immediately follows the statement. The execution rules for composite statements (statements that contain embedded statements) specify the action that is taken when control reaches the end point of an embedded statement. \[*Example*: When control reaches the end point of a statement in a block, control is transferred to the next statement in the block. *end example*\]

If a statement can possibly be reached by execution, the statement is said to be ***reachable***. Conversely, if there is no possibility that a statement will be executed, the statement is said to be ***unreachable***.

\[*Example*: In the following code

```csharp
void F() {
	Console.WriteLine("reachable");
	goto Label;
	Console.WriteLine("unreachable");
	Label:
	Console.WriteLine("reachable");
}
```

the second invocation of Console.WriteLine is unreachable because there is no possibility that the statement will be executed. *end example*\]

A warning is reported if the compiler determines that a statement is unreachable. It is specifically not an error for a statement to be unreachable.

> [!NOTE]
> To determine whether a particular statement or end point is reachable, the compiler performs flow analysis according to the reachability rules defined for each statement. The flow analysis takes into account the values of constant expressions (§12.20) that control the behavior of statements, but the possible values of non-constant expressions are not considered. In other words, for purposes of control flow analysis, a non-constant expression of a given type is considered to have any possible value of that type.

In the example

```csharp
void F() {
    const int i = 1;
    if (i == 2) Console.WriteLine("unreachable");
}
```

the Boolean expression of the if statement is a constant expression because both operands of the == operator are constants. As the constant expression is evaluated at compile-time, producing the value false, the Console.WriteLine invocation is considered unreachable. However, if i is changed to be a local variable

```csharp
void F() {
    int i = 1;
    if (i == 2) Console.WriteLine("reachable");
}
```

the Console.WriteLine invocation is considered reachable, even though, in reality, it will never be executed. *end note*\]

The *block* of a function member or an anonymous function is always considered reachable. By successively evaluating the reachability rules of each statement in a block, the reachability of any given statement can be determined.

\[*Example*: In the following code

```csharp
void F(int x) {
    Console.WriteLine("start");
    if (x < 0) Console.WriteLine("negative");
}
```

the reachability of the second Console.WriteLine is determined as follows:

- The first Console.WriteLine expression statement is reachable because the block of the F method is reachable (§13.3).

- The end point of the first Console.WriteLine expression statement is reachable because that statement is reachable (§13.7 and §13.3).

- The if statement is reachable because the end point of the first Console.WriteLine expression statement is reachable (§13.7 and §13.3).

- The second Console.WriteLine expression statement is reachable because the Boolean expression of the if statement does not have the constant value false.

*end example*\]

There are two situations in which it is a compile-time error for the end point of a statement to be reachable:

- Because the switch statement does not permit a switch section to “fall through” to the next switch section, it is a compile-time error for the end point of the statement list of a switch section to be reachable. If this error occurs, it is typically an indication that a break statement is missing.

- It is a compile-time error for the end point of the block of a function member or an anonymous function that computes a value to be reachable. If this error occurs, it typically is an indication that a return statement is missing (§13.10.5).

## Blocks

### General

A *block* permits multiple statements to be written in contexts where a single statement is allowed.

[#Grammar_block .anchor](#Grammar_block .anchor)

```ANTLR
block:
{ statement-list~opt~ }
```

A *block* consists of an optional *statement-list* (§13.3.2), enclosed in braces. If the statement list is omitted, the block is said to be empty.

A block may contain declaration statements (§13.6). The scope of a local variable or constant declared in a block is the block.

Within a block, the meaning of a name used in an expression context shall always be the same (§12.7.3.2).

A block is executed as follows:

- If the block is empty, control is transferred to the end point of the block.

- If the block is not empty, control is transferred to the statement list. When and if control reaches the end point of the statement list, control is transferred to the end point of the block.

The statement list of a block is reachable if the block itself is reachable.

The end point of a block is reachable if the block is empty or if the end point of the statement list is reachable.

A *block* that contains one or more yield statements (§13.15) is called an iterator block. Iterator blocks are used to implement function members as iterators (§15.14). Some additional restrictions apply to iterator blocks:

- It is a compile-time error for a return statement to appear in an iterator block (but yield return statements are permitted).

- It is a compile-time error for an iterator block to contain an unsafe context (§23.2). An iterator block always defines a safe context, even when its declaration is nested in an unsafe context.

### Statement lists

A ***statement list*** consists of one or more statements written in sequence. Statement lists occur in *block*s (§13.3) and in *switch-block*s (§13.8.3).

[#Grammar_statement_list .anchor](#Grammar_statement_list .anchor)

```ANTLR
statement-list:
    statement
    statement-list statement
```

A statement list is executed by transferring control to the first statement. When and if control reaches the end point of a statement, control is transferred to the next statement. When and if control reaches the end point of the last statement, control is transferred to the end point of the statement list.

A statement in a statement list is reachable if at least one of the following is true:

- The statement is the first statement and the statement list itself is reachable.

- The end point of the preceding statement is reachable.

- The statement is a labeled statement and the label is referenced by a reachable goto statement.

The end point of a statement list is reachable if the end point of the last statement in the list is reachable.

## The empty statement

An *empty-statement* does nothing.

[#Grammar_empty_statement .anchor](#Grammar_empty_statement .anchor)

```ANTLR
empty-statement:
    ;
```

An empty statement is used when there are no operations to perform in a context where a statement is required.

Execution of an empty statement simply transfers control to the end point of the statement. Thus, the end point of an empty statement is reachable if the empty statement is reachable.

\[*Example*: An empty statement can be used when writing a while statement with a null body:

```csharp
bool ProcessMessage() {…}

void ProcessMessages() {
    while (ProcessMessage())
    ;
}
```

Also, an empty statement can be used to declare a label just before the closing “}” of a block:

```csharp
void F() {
    …
    
    if (done) goto exit;
    …
    
    exit: ;
}
```

[#_Toc497631513 .anchor](#_Toc497631513 .anchor)[#_Ref471972610 .anchor](#_Ref471972610 .anchor)*end example*\]

## Labeled statements

A *labeled-statement* permits a statement to be prefixed by a label. Labeled statements are permitted in blocks, but are not permitted as embedded statements.

[#Grammar_labeled_statement .anchor](#Grammar_labeled_statement .anchor)

```ANTLR
labeled-statement:
    identifier : statement
```

A labeled statement declares a label with the name given by the *identifier*. The scope of a label is the whole block in which the label is declared, including any nested blocks. It is a compile-time error for two labels with the same name to have overlapping scopes.

A label can be referenced from goto statements (§13.10.4) within the scope of the label. 

> [!NOTE] 
> This means that goto statements can transfer control within blocks and out of blocks, but never into blocks.

Labels have their own declaration space and do not interfere with other identifiers. \[*Example*: The example

```csharp
int F(int x) {
    if (x >= 0) goto x;
    x = -x;
    x: return x;
}
```

is valid and uses the name x as both a parameter and a label. [#_Toc445783018 .anchor](#_Toc445783018 .anchor) *end example*\]

Execution of a labeled statement corresponds exactly to execution of the statement following the label.

In addition to the reachability provided by normal flow of control, a labeled statement is reachable if the label is referenced by a reachable goto statement, unless the goto statement is inside the try block or a catch block of a *try-statement* that includes a finally block whose end point is unreachable, and the labeled statement is outside the *try-statement*.

## Declaration statements

### General

A *declaration-statement* declares a local variable or constant. Declaration statements are permitted in blocks, but are not permitted as embedded statements.

[#Grammar_declaration_statement .anchor](#Grammar_declaration_statement .anchor)

```ANTLR
declaration-statement:
    local-variable-declaration ;
    local-constant-declaration ;
```

### Local variable declarations

A *local-variable-declaration* declares one or more local variables.

[#Grammar_local_variable_declaration .anchor](#Grammar_local_variable_declaration .anchor)

```ANTLR
local-variable-declaration:
    local-variable-type local-variable-declarators
```

[#Grammar_local_variable_type .anchor](#Grammar_local_variable_type .anchor)

```ANTLR
local-variable-type:
    type
    var
```

[#Grammar_local_variable_declarators .anchor](#Grammar_local_variable_declarators .anchor)

```ANTLR
local-variable-declarators:
    local-variable-declarator
    local-variable-declarators , local-variable-declarator
```

[#Grammar_local_variable_declarator .anchor](#Grammar_local_variable_declarator .anchor)

```ANTLR
local-variable-declarator:
    identifier
    identifier = local-variable-initializer
```

[#Grammar_local_variable_initializer .anchor](#Grammar_local_variable_initializer .anchor)local-variable-initialize[#_Toc445783019 .anchor](#_Toc445783019 .anchor)

```ANTLR
r:
    expression
    array-initializer
```

The *local-variable-type* of a *local-variable-declaration* either directly specifies the type of the variables introduced by the declaration, or indicates with the identifier var that the type should be inferred based on an initializer. The type is followed by a list of *local-variable-declarator*s, each of which introduces a new variable. A *local-variable-declarator* consists of an *identifier* that names the variable, optionally followed by an “=” token and a *local-variable-initializer* that gives the initial value of the variable.

In the context of a local variable declaration, the identifier var acts as a contextual keyword (§7.4.4).When the *local-variable-type* is specified as var and no type named var is in scope, the declaration is an ***implicitly typed local variable declaration***, whose type is inferred from the type of the associated initializer expression. Implicitly typed local variable declarations are subject to the following restrictions:

- The *local-variable-declaration* cannot include multiple *local-variable-declarator*s.

- The *local-variable-declarator* shall include a *local-variable-initializer*.

- The *local-variable-initializer* shall be an *expression*.

- The initializer *expression* shall have a compile-time type.

- The initializer *expression* cannot refer to the declared variable itself

\[*Example*: The following are incorrect implicitly typed local variable declarations:

```csharp
var x; // Error, no initializer to infer type from
var y = {1, 2, 3}; // Error, array initializer not permitted
var z = null; // Error, null does not have a type
var u = x => x + 1; // Error, anonymous functions do not have a type
var v = v++; // Error, initializer cannot refer to v itself
```

*end example*\]

The value of a local variable is obtained in an expression using a *simple-name* (§12.7.3), and the value of a local variable is modified using an *assignment* (§12.18). A local variable shall be definitely assigned (§10.4) at each location where its value is obtained.

The scope of a local variable declared in a *local-variable-declaration* is the block in which the declaration occurs. It is an error to refer to a local variable in a textual position that precedes the *local-variable-declarator* of the local variable. Within the scope of a local variable, it is a compile-time error to declare another local variable or constant with the same name.

A local variable declaration that declares multiple variables is equivalent to multiple declarations of single variables with the same type. Furthermore, a variable initializer in a local variable declaration corresponds exactly to an assignment statement that is inserted immediately after the declaration.

\[*Example*: The example

```csharp
void F() {
    int x = 1, y, z = x * 2;
}
```
corresponds exactly to

```csharp
void F() {
    int x; x = 1;
    int y;
    int z; z = x * 2;
}
```

*end example*\]

In an implicitly typed local variable declaration, the type of the local variable being declared is taken to be the same as the type of the expression used to initialize the variable. \[*Example*:

```csharp
var i = 5;
var s = "Hello";
var d = 1.0;
var numbers = new int[] {1, 2, 3};
var orders = new Dictionary<int,Order>();
```

The implicitly typed local variable declarations above are precisely equivalent to the following explicitly typed declarations:

```csharp
int i = 5;
string s = "Hello";
double d = 1.0;
int\[\] numbers = new int\[\] {1, 2, 3};
Dictionary<int,Order> orders = new Dictionary<int,Order>();
```

*end example*\]

### Local constant declarations

A *local-constant-declaration* declares one or more local constants.

[#Grammar_local_constant_declaration .anchor](#Grammar_local_constant_declaration .anchor)

```ANTLR
local-constant-declaration:

    const type constant-declarators

constant-declarators:
    constant-declarator
    constant-declarators , constant-declarator

constant-declarator:
    identifier = constant-expression
```

[#_Ref450638231 .anchor](#_Ref450638231 .anchor)[#_Toc445783021 .anchor](#_Toc445783021 .anchor)The *type* of a *local-constant-declaration* specifies the type of the constants introduced by the declaration. The type is followed by a list of *constant-declarator*s, each of which introduces a new constant. A *constant-declarator* consists of an *identifier* that names the constant, followed by an “=” token, followed by a *constant-expression* (§12.20) that gives the value of the constant.

The *type* and *constant-expression* of a local constant declaration shall follow the same rules as those of a constant member declaration (§15.4).

The value of a local constant is obtained in an expression using a *simple-name* (§12.7.3).

The scope of a local constant is the block in which the declaration occurs. It is an error to refer to a local constant in a textual position that precedes the end of its *constant-declarator*. Within the scope of a local constant, it is a compile-time error to declare another local variable or constant with the same name.

A local constant declaration that declares multiple constants is equivalent to multiple declarations of single constants with the same type.

## Expression statements

An *expression-statement* evaluates a given expression. The value computed by the expression, if any, is discarded.


[#Grammar_expression_statement .anchor](#Grammar_expression_statement .anchor)

```ANTLR
expression-statement:
    statement-expression ;
```
[#Grammar_statement_expression .anchor](#Grammar_statement_expression .anchor)

```ANTLR
statement-expression:
    invocation-expression
    object-creation-expression
    assignment
    post-increment-expression
    post-decrement-expression
    pre-increment-expression
    pre-decrement-expression
    await-expression
```

Not all expressions are permitted as statements. 

> [!NOTE] 
> In particular, expressions such as x + y and x == 1, that merely compute a value (which will be discarded), are not permitted as statements.

Execution of an expression statement evaluates the contained expression and then transfers control to the end point of the expression statement. The end point of an *expression-statement* is reachable if that *expression-statement* is reachable.

## Selection statements

### General

Selection statements select one of a number of possible statements for execution based on the value of some expression.

[#Grammar_selection_statement .anchor](#Grammar_selection_statement .anchor)

```ANTLR
selection-statement:
    if-statement
    switch-statement
```
### The if statement

The if statement selects a statement for execution based on the value of a Boolean expression.

[#Grammar_if_statement .anchor](#Grammar_if_statement .anchor)

```ANTLR
if-statement:
    if ( boolean-expression ) embedded-statement\
    if ( boolean-expression ) embedded-statement else 
    embedded-statement
```
[#_Toc445783024 .anchor](#_Toc445783024 .anchor)An else part is associated with the lexically nearest preceding if that is allowed by the syntax. \[*Example*: Thus, an if statement of the form

```csharp
if (x) if (y) F(); else G();

is equivalent to

if (x) {
    if (y) {
        F();
    }
    else {
        G();
    }
}
```

*end example*\]

An if statement is executed as follows:

- The *boolean-expression* (§12.21) is evaluated.

- If the Boolean expression yields true, control is transferred to the first embedded statement. When and if control reaches the end point of that statement, control is transferred to the end point of the if statement.

- If the Boolean expression yields false and if an else part is present, control is transferred to the second embedded statement. When and if control reaches the end point of that statement, control is transferred to the end point of the if statement.

- If the Boolean expression yields false and if an else part is not present, control is transferred to the end point of the if statement.

The first embedded statement of an if statement is reachable if the if statement is reachable and the Boolean expression does not have the constant value false.

The second embedded statement of an if statement, if present, is reachable if the if statement is reachable and the Boolean expression does not have the constant value true.

The end point of an if statement is reachable if the end point of at least one of its embedded statements is reachable. In addition, the end point of an if statement with no else part is reachable if the if statement is reachable and the Boolean expression does not have the constant value true.

### The switch statement

The switch statement selects for execution a statement list having an associated switch label that corresponds to the value of the switch expression.

[#Grammar_switch_statement .anchor](#Grammar_switch_statement .anchor)

```ANTLR
switch-statement:
    switch ( expression ) switch-block
```

[#Grammar_switch_block .anchor](#Grammar_switch_block .anchor)

```ANTLR
switch-block:
    { switch-sections~opt~ }
```
[#Grammar_switch_sections .anchor](#Grammar_switch_sections .anchor)

```ANTLR
switch-sections:
    switch-section
    switch-sections switch-section
```

[#Grammar_switch_section .anchor](#Grammar_switch_section .anchor)

```ANTLR
switch-section:
    switch-labels statement-list
```

[#Grammar_switch_labels .anchor](#Grammar_switch_labels .anchor)

```ANTLR
switch-labels:
    switch-label
    switch-labels switch-label
```

[#Grammar_switch_label .anchor](#Grammar_switch_label .anchor)

```ANTLR
switch-label:
    case constant-expression :
    default :
```

[#_Toc445783025 .anchor](#_Toc445783025 .anchor)A *switch-statement* consists of the keyword switch, followed by a parenthesized expression (called the ***switch expression***), followed by a *switch-block*. The *switch-block* consists of zero or more *switch-section*s, enclosed in braces. Each *switch-section* consists of one or more *switch-labels* followed by a *statement-list* (§13.3.2).

The ***governing type*** of a switch statement is established by the switch expression.

- If the type of the switch expression is `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `bool`, `string`, or an `enum-type`, or if it is the nullable value type corresponding to one of these types, then that is the governing type of the switch statement.

- Otherwise, exactly one user-defined implicit conversion shall exist from the type of the switch expression to one of the following possible governing types: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `string`, or, a nullable value type corresponding to one of those types.

- Otherwise, a compile-time error occurs.

The constant expression of each case label shall denote a value of a type that is implicitly convertible (§11.2) to the governing type of the switch statement. A compile-time error occurs if two or more case labels in the same switch statement specify the same constant value.

There can be at most one default label in a switch statement.

A switch statement is executed as follows:

- The switch expression is evaluated and converted to the governing type.

- If one of the constants specified in a case label in the same switch statement is equal to the value of the switch expression, control is transferred to the statement list following the matched case label.

- If none of the constants specified in case labels in the same switch statement is equal to the value of the switch expression, and if a default label is present, control is transferred to the statement list following the default label.

- If none of the constants specified in case labels in the same switch statement is equal to the value of the switch expression, and if no default label is present, control is transferred to the end point of the switch statement.

If the end point of the statement list of a switch section is reachable, a compile-time error occurs. This is known as the “no fall through” rule. \[*Example*: The example

```csharp
switch (i) {
    case 0:
    CaseZero();
    break;
    case 1:
    CaseOne();
    break;
    default:
    CaseOthers();
    break;
}
```

is valid because no switch section has a reachable end point. Unlike C and C++, execution of a switch section is not permitted to “fall through” to the next switch section, and the example

```csharp
switch (i) {
    case 0:
    CaseZero();
    case 1:
    CaseZeroOrOne();
    default:
    CaseAny();
}
```

results in a compile-time error. When execution of a switch section is to be followed by execution of another switch section, an explicit goto case or goto default statement shall be used:

```csharp
switch (i) {
    case 0:
    CaseZero();
    goto case 1;
    case 1:
    CaseZeroOrOne();
    goto default;
    default:
    CaseAny();
    break;
}
```

*end example*\]

Multiple labels are permitted in a *switch-section*. \[*Example*: The example

```csharp
switch (i) {
    case 0:
    CaseZero();
    break;
    case 1:
    CaseOne();
    break;
    case 2:
    default:
    CaseTwo();
    break;
}
```

is valid. The example does not violate the “no fall through” rule because the labels case 2: and default: are part of the same *switch-section*. *end example*\]

> [!NOTE] 
> The “no fall through” rule prevents a common class of bugs that occur in C and C++ when break statements are accidentally omitted. For example, the sections of the switch statement above can be reversed without affecting the behavior of the statement:

```csharp
switch (i) {
    default:
    CaseAny();
    break;
    case 1:
    CaseZeroOrOne();
    goto default;
    case 0:
    CaseZero();
    goto case 1;
}
```

*end note*\]

> [!NOTE] 
> The statement list of a switch section typically ends in a break, goto case, or goto default statement, but any construct that renders the end point of the statement list unreachable is permitted. For example, a while statement controlled by the Boolean expression true is known to never reach its end point. Likewise, a throw or return statement always transfers control elsewhere and never reaches its end point. Thus, the following example is valid:

```csharp
switch (i) {
    case 0:
        while (true) F();
    case 1:
        throw new ArgumentException();
    case 2:
        return;
}
```

*end note*\]

\[*Example*: The governing type of a switch statement can be the type string. For example:

```csharp
void DoCommand(string command) {
    switch (command.ToLower()) {
        case "run":
            DoRun();
            break;
        case "save":
            DoSave();
            break;
        case "quit":
            DoQuit();
            break;
        default:
            InvalidCommand(command);
            break;
    }
}
```

*end example*\]

> [!NOTE] 
> Like the string equality operators (§12.11.8), the switch statement is case sensitive and will execute a given switch section only if the switch expression string exactly matches a case label constant. 
When the governing type of a switch statement is string or a nullable value type, the value null is permitted as a case label constant.

The *statement-list*s of a *switch-block* may contain declaration statements (§13.6). The scope of a local variable or constant declared in a switch block is the switch block.

Within a switch block, the meaning of a name used in an expression context shall always be the same (§12.7.3.2).

The statement list of a given switch section is reachable if the switch statement is reachable and at least one of the following is true:

- The switch expression is a non-constant value.

- The switch expression is a constant value that matches a case label in the switch section.

- The switch expression is a constant value that doesn’t match any case label, and the switch section contains the default label.

- A switch label of the switch section is referenced by a reachable goto case or goto default statement.

The end point of a switch statement is reachable if at least one of the following is true:

- The switch statement contains a reachable break statement that exits the switch statement.

- The switch statement is reachable, the switch expression is a non-constant value, and no default label is present.

- The switch statement is reachable, the switch expression is a constant value that doesn’t match any case label, and no default label is present.

## Iteration statements

### General

Iteration statements repeatedly execute an embedded statement.

[#Grammar_iteration_statement .anchor](#Grammar_iteration_statement .anchor)

```ANTLR
iteration-statement:
    while-statement
    do-statement
    for-statement
    foreach-statement
```

### The while statement

The while statement conditionally executes an embedded statement zero or more times.

[#Grammar_while_statement .anchor](#Grammar_while_statement .anchor)

```ANTLR
while-statement:
    while ( boolean-expression ) embedded-statement
```

A while statement is executed as follows:

- The *boolean-expression* (§12.21) is evaluated.

- If the Boolean expression yields true, control is transferred to the embedded statement. When and if control reaches the end point of the embedded statement (possibly from execution of a continue statement), control is transferred to the beginning of the while statement.

- If the Boolean expression yields false, control is transferred to the end point of the while statement.

Within the embedded statement of a while statement, a break statement (§13.10.2) may be used to transfer control to the end point of the while statement (thus ending iteration of the embedded statement), and a continue statement (§13.10.3) may be used to transfer control to the end point of the embedded statement (thus performing another iteration of the while statement).

The embedded statement of a while statement is reachable if the while statement is reachable and the Boolean expression does not have the constant value false.

The end point of a while statement is reachable if at least one of the following is true:

- The while statement contains a reachable break statement that exits the while statement.

- The while statement is reachable and the Boolean expression does not have the constant value true.

### The do statement

The do statement conditionally executes an embedded statement one or more times.

[#Grammar_do_statement .anchor](#Grammar_do_statement .anchor)

```ANTLR
do-statement:
    do embedded-statement while ( boolean-expression ) ;
```

A do statement is executed as follows:

- Control is transferred to the embedded statement.

- When and if control reaches the end point of the embedded statement (possibly from execution of a continue statement), the *boolean-expression* (§12.21) is evaluated. If the Boolean expression yields true, control is transferred to the beginning of the do statement. Otherwise, control is transferred to the end point of the do statement.

Within the embedded statement of a do statement, a break statement (§13.10.2) may be used to transfer control to the end point of the do statement (thus ending iteration of the embedded statement), and a continue statement (§13.10.3) may be used to transfer control to the end point of the embedded statement (thus performing another iteration of the do statement).

The embedded statement of a do statement is reachable if the do statement is reachable.

[#_Ref470173280 .anchor](#_Ref470173280 .anchor)[#_Toc445783028 .anchor](#_Toc445783028 .anchor)The end point of a do statement is reachable if at least one of the following is true:

- The do statement contains a reachable break statement that exits the do statement.

- The end point of the embedded statement is reachable and the Boolean expression does not have the constant value true.

### The for statement

The for statement evaluates a sequence of initialization expressions and then, while a condition is true, repeatedly executes an embedded statement and evaluates a sequence of iteration expressions.

[#Grammar_for_statement .anchor](#Grammar_for_statement .anchor)

```ANTLR
for-statement:
    for ( for-initializer~opt~ ; for-condition~opt~ ; for-iterator~opt~ ) embedded-statement
```

[#Grammar_for_initializer .anchor](#Grammar_for_initializer .anchor)

```ANTLR
for-initializer:
    local-variable-declaration
    statement-expression-list
```

[#Grammar_for_condition .anchor](#Grammar_for_condition .anchor)

```ANTLR
for-condition:
    boolean-expression
```

[#Grammar_for_iterator .anchor](#Grammar_for_iterator .anchor)

```ANTLR
for-iterator:
    statement-expression-list
```

[#Grammar_statement_expression_list .anchor](#Grammar_statement_expression_list .anchor)

```ANTLR
statement-expression-list:
    statement-expression
    statement-expression-list , statement-expression
```

The *for-initializer*, if present, consists of either a *local-variable-declaration* (§13.6.2) or a list of *statement-expression*s (§13.7) separated by commas. The scope of a local variable declared by a *for-initializer* starts at the *local-variable-declarator* for the variable and extends to the end of the embedded statement. The scope includes the *for-condition* and the *for-iterator*.

The *for-condition*, if present, shall be a *boolean-expression* (§12.21).

The *for-iterator*, if present, consists of a list of *statement-expression*s (§13.7) separated by commas.

A for statement is executed as follows:

- If a *for-initializer* is present, the variable initializers or statement expressions are executed in the order they are written. This step is only performed once.

- If a *for-condition* is present, it is evaluated.

- If the *for-condition* is not present or if the evaluation yields true, control is transferred to the embedded statement. When and if control reaches the end point of the embedded statement (possibly from execution of a continue statement), the expressions of the *for-iterator*, if any, are evaluated in sequence, and then another iteration is performed, starting with evaluation of the *for-condition* in the step above.

- If the *for-condition* is present and the evaluation yields false, control is transferred to the end point of the for statement.

Within the embedded statement of a for statement, a break statement (§13.10.2) may be used to transfer control to the end point of the for statement (thus ending iteration of the embedded statement), and a continue statement (§13.10.3) may be used to transfer control to the end point of the embedded statement (thus executing the *for-iterator* and performing another iteration of the for statement, starting with the *for-condition*).

The embedded statement of a for statement is reachable if one of the following is true:

- The for statement is reachable and no *for-condition* is present.

- The for statement is reachable and a *for-condition* is present and does not have the constant value false.

The end point of a for statement is reachable if at least one of the following is true:

- The for statement contains a reachable break statement that exits the for statement.

- The for statement is reachable and a *for-condition* is present and does not have the constant value true.

### The foreach statement

The foreach statement enumerates the elements of a collection, executing an embedded statement for each element of the collection.

[#Grammar_foreach_statement .anchor](#Grammar_foreach_statement .anchor)

```ANTLR
foreach-statement:
    foreach ( local-variable-type identifier in expression ) embedded-statement
```

The *local-variable-type* and *identifier* of a foreach statement declare the ***iteration variable*** of the statement. If the var identifier is given as the *local-variable-type*, and no type named var is in scope, the iteration variable is said to be an ***implicitly typed iteration variable***, and its type is taken to be the element type of the foreach statement, as specified below. The iteration variable corresponds to a read-only local variable with a scope that extends over the embedded statement. During execution of a foreach statement, the iteration variable represents the collection element for which an iteration is currently being performed. A compile-time error occurs if the embedded statement attempts to modify the iteration variable (via assignment or the ++ and -- operators) or pass the iteration variable as a ref or out parameter.

In the following, for brevity, `IEnumerable`, `IEnumerator`, `IEnumerable<T>` and `IEnumerator<T>` refer to the corresponding types in the namespaces `System.Collections` and `System.Collections.Generic`.

The compile-time processing of a foreach statement first determines the ***collection type***, ***enumerator type*** and ***element type*** of the expression. This determination proceeds as follows:

- If the type `X` of *expression* is an array type then there is an implicit reference conversion from X to the `IEnumerable` interface (since `System.Array` implements this interface). The ***collection type*** is the `IEnumerable` interface, the ***enumerator type*** is the IEnumerator interface and the ***element type*** is the element type of the array type `X`.

- If the type `X` of *expression* is dynamic then there is an implicit conversion from *expression* to the `IEnumerable` interface (§11.2.9). The ***collection type*** is the `IEnumerable` interface and the ***enumerator type*** is the `IEnumerator` interface. If the var identifier is given as the *local-variable-type* then the ***element type*** is dynamic, otherwise it is object.

- Otherwise, determine whether the type `X` has an appropriate GetEnumerator method:

<!-- -->

- Perform member lookup on the type `X` with identifier `GetEnumerator` and no type arguments. If the member lookup does not produce a match, or it produces an ambiguity, or produces a match that is not a method group, check for an enumerable interface as described below. It is recommended that a warning be issued if member lookup produces anything except a method group or no match.

- Perform overload resolution using the resulting method group and an empty argument list. If overload resolution results in no applicable methods, results in an ambiguity, or results in a single best method but that method is either static or not public, check for an enumerable interface as described below. It is recommended that a warning be issued if overload resolution produces anything except an unambiguous public instance method or no applicable methods.

- If the return type `E` of the `GetEnumerator` method is not a class, struct or interface type, an error is produced and no further steps are taken.

- Member lookup is performed on `E` with the identifier `Current` and no type arguments. If the member lookup produces no match, the result is an error, or the result is anything except a public instance property that permits reading, an error is produced and no further steps are taken.

- Member lookup is performed on `E` with the identifier `MoveNext` and no type arguments. If the member lookup produces no match, the result is an error, or the result is anything except a method group, an error is produced and no further steps are taken.

- Overload resolution is performed on the method group with an empty argument list. If overload resolution results in no applicable methods, results in an ambiguity, or results in a single best method but that method is either static or not public, or its return type is not bool, an error is produced and no further steps are taken.

- The ***collection type*** is `X`, the ***enumerator type*** is `E`, and the ***element type*** is the type of the Current property.

<!-- -->

- Otherwise, check for an enumerable interface:

<!-- -->

- If among all the types `Ti` for which there is an implicit conversion from `X` to `IEnumerable<Ti>`, there is a unique type `T` such that `T` is not dynamic and for all the other `Ti` there is an implicit conversion from `IEnumerable<T>` to `IEnumerable<Ti>`, then the ***collection type*** is the interface `IEnumerable<T>`, the ***enumerator type*** is the interface `IEnumerator<T>`, and the ***element type*** is `T`.

- Otherwise, if there is more than one such type `T`, then an error is produced and no further steps are taken.

- Otherwise, if there is an implicit conversion from `X` to the `System.Collections.IEnumerable` interface, then the ***collection type*** is this interface, the ***enumerator type*** is the interface `System.Collections.IEnumerator`, and the ***element type*** is object.

- Otherwise, an error is produced and no further steps are taken.

The above steps, if successful, unambiguously produce a collection type `C`, enumerator type `E` and element type `T`. `A` foreach statement of the form

```csharp
foreach (V v in x) embedded-statement
```

is then expanded to:

```csharp
{
    E e = ((C)(x)).GetEnumerator();
    try {
        while (e.MoveNext()) {
            V v = (V)(T)e.Current;
            embedded-statement
        }
    }
    finally {
        … // Dispose e
    }
}
```

The variable e is not visible to or accessible to the expression `x` or the embedded statement or any other source code of the program. The variable `v` is read-only in the embedded statement. If there is not an explicit conversion (§11.2.13) from `T` (the element type) to `V` (the *local-variable-type* in the foreach statement), an error is produced and no further steps are taken. 

> [!NOTE] 
> If x has the value null, a `System.NullReferenceException` is thrown at run-time. *end note*\]

An implementation is permitted to implement a given *foreach-statement* differently; e.g., for performance reasons, as long as the behavior is consistent with the above expansion.

The placement of `v` inside the while loop is important for how it is captured (§12.16.6.2) by any anonymous function occurring in the *embedded-statement*.

```csharp
[Example:
int[] values = { 7, 9, 13 };
Action f = null;
foreach (var value in values)
{
    if (f == null) f = () => Console.WriteLine("First value: " + value);
}
f();
```


If `v` in the expanded form were declared outside of the while loop, it would be shared among all iterations, and its value after the for loop would be the final value, 13, which is what the invocation of `f` would print. Instead, because each iteration has its own variable `v`, the one captured by `f` in the first iteration will continue to hold the value 7, which is what will be printed. (Note that earlier versions of C# declared `v` outside of the while loop.) *end example*\]

The body of the finally block is constructed according to the following steps:

- If there is an implicit conversion from E to the `System.IDisposable` interface, then

<!-- -->

- If E is a non-nullable value type then the finally clause is expanded to the semantic equivalent of:

```csharp
finally {
    ((System.IDisposable)e).Dispose();
}
```

- Otherwise the finally clause is expanded to the semantic equivalent of:

```csharp
finally {
    System.IDisposable d = e as System.IDisposable;
    if (d != null) d.Dispose();
}
```

except that if E is a value type, or a type parameter instantiated to a value type, then the conversion of e to `System.IDisposable` shall not cause boxing to occur.

- Otherwise, if E is a sealed type, the finally clause is expanded to an empty block:

```csharp
finally {
}
```

- Otherwise, the finally clause is expanded to:

```csharp
finally {
    System.IDisposable d = e as System.IDisposable;
    if (d != null) d.Dispose();
}
```

The local variable d is not visible to or accessible to any user code. In particular, it does not conflict with any other variable whose scope includes the finally block.

The order in which foreach traverses the elements of an array, is as follows: For single-dimensional arrays elements are traversed in increasing index order, starting with index 0 and ending with index Length – 1. For multi-dimensional arrays, elements are traversed such that the indices of the rightmost dimension are increased first, then the next left dimension, and so on to the left.

\[*Example*: The following example prints out each value in a two-dimensional array, in element order:

```csharp
using System;

class Test
{
    static void Main() {
        double\[,\] values = {
            {1.2, 2.3, 3.4, 4.5},
            {5.6, 6.7, 7.8, 8.9}
        };


        foreach (double elementValue in values)
            Console.Write("{0} ", elementValue);
        Console.WriteLine();
    }
}
```

The output produced is as follows:

1.2 2.3 3.4 4.5 5.6 6.7 7.8 8.9

*end example*\]

\[*Example*: In the following example

```csharp
int[] numbers = { 1, 3, 5, 7, 9 };
foreach (var n in numbers) Console.WriteLine(n);

the type of n is inferred to be int, the element type of numbers.
```

*end example*\]

## Jump statements

### General

Jump statements unconditionally transfer control.

[#Grammar_jump_statement .anchor](#Grammar_jump_statement .anchor)

```ANTLR
jump-statement:
    break-statement
    continue-statement
    goto-statement
    return-statement
    throw-statement
```

[#_Ref470868227 .anchor](#_Ref470868227 .anchor)[#_Toc445783031 .anchor](#_Toc445783031 .anchor)The location to which a jump statement transfers control is called the ***target*** of the jump statement.

When a jump statement occurs within a block, and the target of that jump statement is outside that block, the jump statement is said to ***exit*** the block. While a jump statement can transfer control out of a block, it can never transfer control into a block.

Execution of jump statements is complicated by the presence of intervening try statements. In the absence of such try statements, a jump statement unconditionally transfers control from the jump statement to its target. In the presence of such intervening try statements, execution is more complex. If the jump statement exits one or more try blocks with associated finally blocks, control is initially transferred to the finally block of the innermost try statement. When and if control reaches the end point of a finally block, control is transferred to the finally block of the next enclosing try statement. This process is repeated until the finally blocks of all intervening try statements have been executed.

\[*Example*: In the following code

```csharp
using System;

class Test
{
    static void Main() {
        while (true) {
            try {
                try {
                    Console.WriteLine("Before break");
                    break;
                }
                finally {
                    Console.WriteLine("Innermost finally block");
                }
            }
            finally {
                Console.WriteLine("Outermost finally block");
            }
        }
        Console.WriteLine("After break");
    }
}
```
the finally blocks associated with two try statements are executed before control is transferred to the target of the jump statement.

The output produced is as follows:

Before break\
Innermost finally block\
Outermost finally block\
After break

*end example*\]

### The break statement

The break statement exits the nearest enclosing switch, while, do, for, or foreach statement.

[#Grammar_break_statement .anchor](#Grammar_break_statement .anchor)

```ANTLR
break-statement:
    break ;
```

The target of a break statement is the end point of the nearest enclosing switch, while, do, for, or foreach statement. If a break statement is not enclosed by a switch, while, do, for, or foreach statement, a compile-time error occurs.

When multiple switch, while, do, for, or foreach statements are nested within each other, a break statement applies only to the innermost statement. To transfer control across multiple nesting levels, a goto statement (§13.10.4) shall be used.

A break statement cannot exit a finally block (§13.11). When a break statement occurs within a finally block, the target of the break statement shall be within the same finally block; otherwise a compile-time error occurs.

A break statement is executed as follows:

- [#_Ref470868245 .anchor](#_Ref470868245 .anchor)[#_Toc445783032 .anchor](#_Toc445783032 .anchor)If the break statement exits one or more try blocks with associated finally blocks, control is initially transferred to the finally block of the innermost try statement. When and if control reaches the end point of a finally block, control is transferred to the finally block of the next enclosing try statement. This process is repeated until the finally blocks of all intervening try statements have been executed.

- Control is transferred to the target of the break statement.

Because a break statement unconditionally transfers control elsewhere, the end point of a break statement is never reachable.

### The continue statement

The continue statement starts a new iteration of the nearest enclosing while, do, for, or foreach statement.

[#Grammar_continue_statement .anchor](#Grammar_continue_statement .anchor)

```ANTLR
continue-statement:
    continue ;
```

The target of a continue statement is the end point of the embedded statement of the nearest enclosing while, do, for, or foreach statement. If a continue statement is not enclosed by a while, do, for, or foreach statement, a compile-time error occurs.

When multiple while, do, for, or foreach statements are nested within each other, a continue statement applies only to the innermost statement. To transfer control across multiple nesting levels, a goto statement (§13.10.4) shall be used.

A continue statement cannot exit a finally block (§13.11). When a continue statement occurs within a finally block, the target of the continue statement shall be within the same finally block; otherwise a compile-time error occurs.

A continue statement is executed as follows:

- If the continue statement exits one or more try blocks with associated finally blocks, control is initially transferred to the finally block of the innermost try statement. When and if control reaches the end point of a finally block, control is transferred to the finally block of the next enclosing try statement. This process is repeated until the finally blocks of all intervening try statements have been executed.

- Control is transferred to the target of the continue statement.

Because a continue statement unconditionally transfers control elsewhere, the end point of a continue statement is never reachable.

### The goto statement

The goto statement transfers control to a statement that is marked by a label.

[#Grammar_goto_statement .anchor](#Grammar_goto_statement .anchor)

```ANTLR
goto-statement:
    goto identifier ;
    goto case constant-expression ;
    goto default ;
```

The target of a goto *identifier* statement is the labeled statement with the given label. If a label with the given name does not exist in the current function member, or if the goto statement is not within the scope of the label, a compile-time error occurs. 

> [!NOTE] 
> This rule permits the use of a goto statement to transfer control *out of* a nested scope, but not *into* a nested scope. In the example


```csharp
using System;

class Test
{
    static void Main(string\[\] args) {
        string\[,\] table = {
            {"Red", "Blue", "Green"},
            {"Monday", "Wednesday", "Friday"}
        };

        foreach (string str in args) {
            int row, colm;
            for (row = 0; row <= 1; ++row)
                for (colm = 0; colm <= 2; ++colm)
                    if (str == table\[row,colm\])
                        goto done;

            Console.WriteLine("{0} not found", str);
            continue;
        done:
            Console.WriteLine("Found {0} at \[{1}\]\[{2}\]", str, row, colm);
        }
    }
}
```

a goto statement is used to transfer control out of a nested scope. *end note*\]

The target of a goto case statement is the statement list in the immediately enclosing switch statement (§13.8.3) which contains a case label with the given constant value. If the goto case statement is not enclosed by a switch statement, if the *constant-expression* is not implicitly convertible (§11.2) to the governing type of the nearest enclosing switch statement, or if the nearest enclosing switch statement does not contain a case label with the given constant value, a compile-time error occurs.

The target of a goto default statement is the statement list in the immediately enclosing switch statement (§13.8.3), which contains a default label. If the goto default statement is not enclosed by a switch statement, or if the nearest enclosing switch statement does not contain a default label, a compile-time error occurs.

A goto statement cannot exit a finally block (§13.11). When a goto statement occurs within a finally block, the target of the goto statement shall be within the same finally block, or otherwise a compile-time error occurs.

A goto statement is executed as follows:

- If the goto statement exits one or more try blocks with associated finally blocks, control is initially transferred to the finally block of the innermost try statement. When and if control reaches the end point of a finally block, control is transferred to the finally block of the next enclosing try statement. This process is repeated until the finally blocks of all intervening try statements have been executed.

- Control is transferred to the target of the goto statement.

Because a goto statement unconditionally transfers control elsewhere, the end point of a goto statement is never reachable.

### The return statement

The return statement returns control to the current caller of the function member in which the return statement appears.

[#Grammar_return_statement .anchor](#Grammar_return_statement .anchor)

```ANTLR
return-statement:
    return expression~opt~ ;
```

A function member is said to ***compute a value*** if it is a method with a non-void result type (§15.6.11), the get accessor of a property or indexer, or a user-defined operator. Function members that do not compute a value are methods with the effective return type void, set accessors of properties and indexers, add and remove accessors of event, instance constructors, static constructors and finalizers.

Within a function member, a return statement with no expression can only be used if the function member does not compute a value. Within a function member, a return statement with an expression can only be used if the function member computes a value. Where the return statement includes an expression, an implicit conversion (§11.2) shall exist from the type of the expression to the effective return type of the containing function member.

Return statements can also be used in the body of anonymous function expressions (§12.16), and participate in determining which conversions exist for those functions (§11.7.1).

It is a compile-time error for a return statement to appear in a finally block (§13.11).

A return statement is executed as follows:

- If the return statement specifies an expression, the expression is evaluated and its value is converted to the effective return type of the containing function by an implicit conversion. The result of the conversion becomes the result value produced by the function.

- If the return statement is enclosed by one or more try or catch blocks with associated finally blocks, control is initially transferred to the finally block of the innermost try statement. When and if control reaches the end point of a finally block, control is transferred to the finally block of the next enclosing try statement. This process is repeated until the finally blocks of all enclosing try statements have been executed.

- If the containing function is not an async function, control is returned to the caller of the containing function along with the result value, if any.

- If the containing function is an async function, control is returned to the current caller, and the result value, if any, is recorded in the return task as described in (§15.15.2).

Because a return statement unconditionally transfers control elsewhere, the end point of a return statement is never reachable.

### The throw statement

The throw statement throws an exception.

[#Grammar_throw_statement .anchor](#Grammar_throw_statement .anchor)

```ANTLR
throw-statement:
    throw expression~opt~ ;
```

A throw statement with an expression throws an exception produced by evaluating the expression. The expression shall be implicitly convertible to `System.Exception`, and the result of evaluating the expression is converted to `System.Exception` before being thrown. If the result of the conversion is null, a `System.NullReferenceException` is thrown instead.

A throw statement with no expression can be used only in a catch block, in which case, that statement re-throws the exception that is currently being handled by that catch block.

Because a throw statement unconditionally transfers control elsewhere, the end point of a throw statement is never reachable.

When an exception is thrown, control is transferred to the first catch clause in an enclosing try statement that can handle the exception. The process that takes place from the point of the exception being thrown to the point of transferring control to a suitable exception handler is known as ***exception propagation***. Propagation of an exception consists of repeatedly evaluating the following steps until a catch clause that matches the exception is found. In this description, the ***throw point*** is initially the location at which the exception is thrown.

- In the current function member, each try statement that encloses the throw point is examined. For each statement `S`, starting with the innermost try statement and ending with the outermost try statement, the following steps are evaluated:

<!-- -->

- If the try block of S encloses the throw point and if S has one or more catch clauses, the catch clauses are examined in order of appearance to locate a suitable handler for the exception. The first catch clause that specifies an exception type T (or a type parameter that at run-time denotes an exception type T) such that the run-time type of E derives from T is considered a match. A general catch (§13.11) clause is considered a match for any exception type. If a matching catch clause is located, the exception propagation is completed by transferring control to the block of that catch clause.

- Otherwise, if the try block or a catch block of S encloses the throw point and if S has a finally block, control is transferred to the finally block. If the finally block throws another exception, processing of the current exception is terminated. Otherwise, when control reaches the end point of the finally block, processing of the current exception is continued.

<!-- -->

- If an exception handler was not located in the current function invocation, the function invocation is terminated, and one of the following occurs:

<!-- -->

- If the current function is non-async, the steps above are repeated for the caller of the function with a throw point corresponding to the statement from which the function member was invoked.

- If the current function is async and task-returning, the exception is recorded in the return task, which is put into a faulted or cancelled state as described in §15.15.2.

- If the current function is async and void-returning, the synchronization context of the current thread is notified as described in §15.15.3.

<!-- -->

- If the exception processing terminates all function member invocations in the current thread, indicating that the thread has no handler for the exception, then the thread is itself terminated. The impact of such termination is implementation-defined.

## The try statement

The try statement provides a mechanism for catching exceptions that occur during execution of a block. Furthermore, the try statement provides the ability to specify a block of code that is always executed when control leaves the try statement.

[#Grammar_try_statement .anchor](#Grammar_try_statement .anchor)

```ANTLR
try-statement:
    try block catch-clauses
    try block catch-clauses~opt~ finally-clause
```

[#Grammar_catch_clauses .anchor](#Grammar_catch_clauses .anchor)

```ANTLR
catch-clauses:
    specific-catch-clauses
    specific-catch-clauses~opt~ general-catch-clause
```

[#Grammar_specific_catch_clauses .anchor](#Grammar_specific_catch_clauses .anchor)

```ANTLR
specific-catch-clauses:
    specific-catch-clause
    specific-catch-clauses specific-catch-clause
```

[#Grammar_specific_catch_clause .anchor](#Grammar_specific_catch_clause .anchor)

```ANTLR
specific-catch-clause:
    catch ( type identifier~opt~ ) block
```
[#Grammar_general_catch_clause .anchor](#Grammar_general_catch_clause .anchor)

```ANTLR
general-catch-clause:
    catch block
```

[#Grammar_finally_clause .anchor](#Grammar_finally_clause .anchor)

```ANTLR
finally-clause:
    finally block
```

There are three possible forms of try statements:

- A try block followed by one or more catch blocks.

- A try block followed by a finally block.

- A try block followed by one or more catch blocks followed by a finally block.

When a catch clause specifies a *type*, the type shall be `System.Exception` or a type that derives from `System.Exception`. When a catch clause specifies a *type-parameter* it shall be a type parameter type whose effective base class is or derives from `System.Exception`.

When a catch clause specifies both a *class-type* and an *identifier*, an ***exception variable*** of the given name and type is declared. The exception variable corresponds to a local variable with a scope that extends over the catch block. During execution of the catch block, the exception variable represents the exception currently being handled. For purposes of definite assignment checking, the exception variable is considered definitely assigned in its entire scope.

Unless a catch clause includes an exception variable name, it is impossible to access the exception object in the catch block.

A catch clause that specifies neither an exception type nor an exception variable name is called a general catch clause. A try statement can only have one general catch clause, and, if one is present, it shall be the last catch clause.

> [!NOTE] 
> Some programming languages might support exceptions that are not representable as an object derived from `System.Exception`, although such exceptions could never be generated by C# code. A general catch clause might be used to catch such exceptions. Thus, a general catch clause is semantically different from one that specifies the type `System.Exception`, in that the former might also catch exceptions from other languages.

In order to locate a handler for an exception, catch clauses are examined in lexical order. A compile-time error occurs if a catch clause specifies a type that is the same as, or is derived from, a type that was specified in an earlier catch clause for the same try. 

> [!NOTE]
> Without this restriction, it would be possible to write unreachable catch clauses.

Within a catch block, a throw statement (§13.10.6) with no expression can be used to re-throw the exception that was caught by the catch block. Assignments to an exception variable do not alter the exception that is re-thrown.

\[*Example*: In the following code


using System;

```csharp
class Test
{
    static void F() {
        try {
            G();
        }
        catch (Exception e) {
            Console.WriteLine("Exception in F: " + e.Message);
            e = new Exception("F");
            throw; // re-throw
        }
    }

    static void G() {
        throw new Exception("G");
    }

    static void Main() {
        try {
            F();
        }
        catch (Exception e) {
            Console.WriteLine("Exception in Main: " + e.Message);
        }
    }
}
```

the method F catches an exception, writes some diagnostic information to the console, alters the exception variable, and re-throws the exception. The exception that is re-thrown is the original exception, so the output produced is:

```csharp
Exception in F: G
Exception in Main: G
```

If the first catch block had thrown e instead of rethrowing the current exception, the output produced would be as follows:

```csharp
Exception in F: G
Exception in Main: F
```

*end example*\]

It is a compile-time error for a break, continue, or goto statement to transfer control out of a finally block. When a break, continue, or goto statement occurs in a finally block, the target of the statement shall be within the same finally block, or otherwise a compile-time error occurs.

It is a compile-time error for a return statement to occur in a finally block.

A try statement is executed as follows:

- Control is transferred to the try block.

- When and if control reaches the end point of the try block:

<!-- -->

- If the try statement has a finally block, the finally block is executed.

- Control is transferred to the end point of the try statement.

<!-- -->

- If an exception is propagated to the try statement during execution of the try block:

<!-- -->

- The catch clauses, if any, are examined in order of appearance to locate a suitable handler for the exception. The first catch clause that specifies the exception type or a base type of the exception type is considered a match. A general catch clause is considered a match for any exception type. If a matching catch clause is located:

<!-- -->

- If the matching catch clause declares an exception variable, the exception object is assigned to the exception variable.

- Control is transferred to the matching catch block.

- When and if control reaches the end point of the catch block:

<!-- -->

- If the try statement has a finally block, the finally block is executed.

- Control is transferred to the end point of the try statement.

<!-- -->

- If an exception is propagated to the try statement during execution of the catch block:

<!-- -->

- If the try statement has a finally block, the finally block is executed.

- The exception is propagated to the next enclosing try statement.

<!-- -->

- If the try statement has no catch clauses or if no catch clause matches the exception:

<!-- -->

- If the try statement has a finally block, the finally block is executed.

- The exception is propagated to the next enclosing try statement.

The statements of a finally block are always executed when control leaves a try statement. This is true whether the control transfer occurs as a result of normal execution, as a result of executing a break, continue, goto, or return statement, or as a result of propagating an exception out of the try statement.

If an exception is thrown during execution of a finally block, and is not caught within the same finally block,the exception is propagated to the next enclosing try statement. If another exception was in the process of being propagated, that exception is lost. The process of propagating an exception is discussed further in the description of the throw statement (§13.10.6).

The try block of a try statement is reachable if the try statement is reachable.

A catch block of a try statement is reachable if the try statement is reachable.

The finally block of a try statement is reachable if the try statement is reachable.

The end point of a try statement is reachable if both of the following are true:

- The end point of the try block is reachable or the end point of at least one catch block is reachable.

- If a finally block is present, the end point of the finally block is reachable.

## The checked and unchecked statements

The checked and unchecked statements are used to control the ***overflow-checking context*** for integral-type arithmetic operations and conversions.

[#Grammar_checked_statement .anchor](#Grammar_checked_statement .anchor)

```ANTLR
checked-statement:
    checked block
```

[#Grammar_unchecked_statement .anchor](#Grammar_unchecked_statement .anchor)

```ANTLR
unchecked-statement:
    unchecked block
```

The checked statement causes all expressions in the *block* to be evaluated in a checked context, and the unchecked statement causes all expressions in the *block* to be evaluated in an unchecked context.

The checked and unchecked statements are precisely equivalent to the checked and unchecked operators (§12.7.14), except that they operate on blocks instead of expressions.

## The lock statement

The lock statement obtains the mutual-exclusion lock for a given object, executes a statement, and then releases the lock.

[#Grammar_lock_statement .anchor](#Grammar_lock_statement .anchor)

```ANTLR
lock-statement:
    lock ( expression ) embedded-statement
```

The expression of a lock statement shall denote a value of a type known to be a *reference*. No implicit boxing conversion (§11.2.8) is ever performed for the expression of a lock statement, and thus it is a compile-time error for the expression to denote a value of a *value-type*.

A lock statement of the form

```csharp
lock (x) …
```

where x is an expression of a *reference-type*, is precisely equivalent to:

```csharp
bool __lockWasTaken = false;
try {
    System.Threading.Monitor.Enter(x, ref __lockWasTaken); …
}
finally {
    if (__lockWasTaken) System.Threading.Monitor.Exit(x);
}
```

except that x is only evaluated once.

While a mutual-exclusion lock is held, code executing in the same execution thread can also obtain and release the lock. However, code executing in other threads is blocked from obtaining the lock until the lock is released.

## The using statement

The using statement obtains one or more resources, executes a statement, and then disposes of the resource.

[#Grammar_using_statement .anchor](#Grammar_using_statement .anchor)

```ANTLR
using-statement:
    using ( resource-acquisition ) embedded-statement
```

[#Grammar_resource_acquisition .anchor](#Grammar_resource_acquisition .anchor)

```ANTLR
resource-acquisition:
    local-variable-declaration
    expression
```

A ***resource*** is a class or struct that implements the `System.IDisposable` interface, which includes a single parameterless method named Dispose. Code that is using a resource can call Dispose to indicate that the resource is no longer needed.

If the form of *resource-acquisition* is *local-variable-declaration* then the type of the *local-variable-declaration* shall be either dynamic or a type that can be implicitly converted to `System.IDisposable`. If the form of *resource-acquisition* is *expression* then this expression shall be implicitly convertible to `System.IDisposable`.

Local variables declared in a *resource-acquisition* are read-only, and shall include an initializer. A compile-time error occurs if the embedded statement attempts to modify these local variables (via assignment or the ++ and -- operators), take the address of them, or pass them as ref or out parameters.

A using statement is translated into three parts: acquisition, usage, and disposal. Usage of the resource is implicitly enclosed in a try statement that includes a finally clause. This finally clause disposes of the resource. If a null resource is acquired, then no call to Dispose is made, and no exception is thrown. If the resource is of type dynamic it is dynamically converted through an implicit dynamic conversion (§11.2.9) to IDisposable during acquisition in order to ensure that the conversion is successful before the usage and disposal.

A using statement of the form

using (ResourceType resource = expression) statement

corresponds to one of three possible expansions. When ResourceType is a non-nullable value type or a type parameter with the value type constraint (§15.2.5), the expansion is semantically equivalent to

```csharp
{
    ResourceType resource = expression;

    try {

        statement;

    }

    finally {

        ((IDisposable)resource).Dispose();

    }
}
```

except that the cast of resource to `System.IDisposable` shall not cause boxing to occur.

Otherwise, when ResourceType is dynamic, the expansion is

```csharp
{
    ResourceType resource = expression;
    
    IDisposable d = resource;
    
    try {

        statement;

    }

    finally {

        if (d != null) d.Dispose();

    }
}
```

Otherwise, the expansion is

```csharp
{
    ResourceType resource = expression;
    
    try {

        statement;

    }

    finally {

        IDisposable d = (IDisposable)resource;

        if (d != null) d.Dispose();

    }
}
```

In any expansion, the resource variable is read-only in the embedded statement, and the d variable is inaccessible in, and invisible to, the embedded statement.

An implementation is permitted to implement a given using-statement differently, e.g., for performance reasons, as long as the behavior is consistent with the above expansion.

A using statement of the form:

using (expression) statement

has the same three possible expansions. In this case ResourceType is implicitly the compile-time type of the expression, if it has one. Otherwise the interface IDisposable itself is used as the ResourceType. The resource variable is inaccessible in, and invisible to, the embedded statement.

When a *resource-acquisition* takes the form of a *local-variable-declaration*, it is possible to acquire multiple resources of a given type. A using statement of the form

using (ResourceType r1 = e1, r2 = e2, …, rN = eN) statement

is precisely equivalent to a sequence of nested using statements:

```csharp
using (ResourceType r1 = e1)
using (ResourceType r2 = e2)
…
using (ResourceType rN = eN)
statement
```

\[*Example*: The example below creates a file named log.txt and writes two lines of text to the file. The example then opens that same file for reading and copies the contained lines of text to the console.

```csharp
using System;
using System.IO;

class Test
{
    static void Main() {
        using (TextWriter w = File.CreateText("log.txt")) {
            w.WriteLine("This is line one");
            w.WriteLine("This is line two");
        }

        using (TextReader r = File.OpenText("log.txt")) {
            string s;\
            while ((s = r.ReadLine()) != null) {
                Console.WriteLine(s);
            }

        }
    }
}
```

Since the TextWriter and TextReader classes implement the IDisposable interface, the example can use using statements to ensure that the underlying file is properly closed following the write or read operations. *end example*\]

## The yield statement

The yield statement is used in an iterator block (§13.3) to yield a value to the enumerator object (§15.14.5) or enumerable object (§15.14.6) of an iterator or to signal the end of the iteration.

[#Grammar_yield_statement .anchor](#Grammar_yield_statement .anchor)

```ANTLR
yield-statement:
    yield return expression ;
    yield break ;
```

yield is a contextual keyword (§7.4.4) and has special meaning only when used immediately before a return or break keyword.

There are several restrictions on where a yield statement can appear, as described in the following.

- It is a compile-time error for a yield statement (of either form) to appear outside a *method-body*, *operator-body*, or *accessor-body*.

- It is a compile-time error for a yield statement (of either form) to appear inside an anonymous function.

- It is a compile-time error for a yield statement (of either form) to appear in the finally clause of a try statement.

- It is a compile-time error for a yield return statement to appear anywhere in a try statement that contains any *catch-clauses*.

\[*Example*: The following example shows some valid and invalid uses of yield statements.

```csharp
delegate IEnumerable<int> D();

IEnumerator<int> GetEnumerator() {
    try {
        yield return 1; // Ok
        yield break; // Ok
    }
    finally {
        yield return 2; // Error, yield in finally
        yield break; // Error, yield in finally
    }

    try {
        yield return 3; // Error, yield return in try/catch
        yield break; // Ok
    }
    catch {
        yield return 4; // Error, yield return in try/catch
        yield break; // Ok
    }

    D d = delegate {
        yield return 5; // Error, yield in an anonymous function
    };
}

int MyMethod() {
    yield return 1; // Error, wrong return type for an
    // iterator block
}
```

*end example*\]

An implicit conversion (§11.2) shall exist from the type of the expression in the yield return statement to the yield type (§15.14.4) of the iterator.

A yield return statement is executed as follows:

- The expression given in the statement is evaluated, implicitly converted to the yield type, and assigned to the Current property of the enumerator object.

- Execution of the iterator block is suspended. If the yield return statement is within one or more try blocks, the associated finally blocks are *not* executed at this time.

- The MoveNext method of the enumerator object returns true to its caller, indicating that the enumerator object successfully advanced to the next item.

The next call to the enumerator object’s MoveNext method resumes execution of the iterator block from where it was last suspended.

A yield break statement is executed as follows:

- If the yield break statement is enclosed by one or more try blocks with associated finally blocks, control is initially transferred to the finally block of the innermost try statement. When and if control reaches the end point of a finally block, control is transferred to the finally block of the next enclosing try statement. This process is repeated until the finally blocks of all enclosing try statements have been executed.

- Control is returned to the caller of the iterator block. This is either the MoveNext method or Dispose method of the enumerator object.

Because a yield break statement unconditionally transfers control elsewhere, the end point of a yield break statement is never reachable.