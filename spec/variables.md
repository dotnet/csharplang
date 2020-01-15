# Variables

## General

Variables represent storage locations. Every variable has a type that determines what values can be stored in the variable. C\# is a type-safe language, and the C\# compiler guarantees that values stored in variables are always of the appropriate type. The value of a variable can be changed through assignment or through use of the ++ and -- operators.

A variable shall be ***definitely assigned*** (§10.4) before its value can be obtained.

As described in the following subclauses, variables are either ***initially assigned*** or ***initially unassigned***. An initially assigned variable has a well-defined initial value and is always considered definitely assigned. An initially unassigned variable has no initial value. For an initially unassigned variable to be considered definitely assigned at a certain location, an assignment to the variable shall occur in every possible execution path leading to that location.

## Variable categories

### General

C\# defines seven categories of variables: static variables, instance variables, array elements, value parameters, reference parameters, output parameters, and local variables. The subclauses that follow describe each of these categories.

\[*Example*: In the following code

class A\
{\
public static int x;\
int y;

void F(int\[\] v, int a, ref int b, out int c) {\
int i = 1;\
c = a + b++;\
}\
}

x is a static variable, y is an instance variable, v\[0\] is an array element, a is a value parameter, b is a reference parameter, c is an output parameter, and i is a local variable. *end example*\]

### Static variables

A field declared with the static modifier is called a ***static variable***. A static variable comes into existence before execution of the static constructor (§15.12) for its containing type, and ceases to exist when the associated application domain ceases to exist.

The initial value of a static variable is the default value (§10.3) of the variable’s type.

For the purposes of definite assignment checking, a static variable is considered initially assigned.

### Instance variables

#### General

A field declared without the static modifier is called an ***instance variable***.

#### Instance variables in classes

An instance variable of a class comes into existence when a new instance of that class is created, and ceases to exist when there are no references to that instance and the instance’s finalizer (if any) has executed.

The initial value of an instance variable of a class is the default value (§10.3) of the variable’s type.

For the purpose of definite assignment checking, an instance variable of a class is considered initially assigned.

#### Instance variables in structs

An instance variable of a struct has exactly the same lifetime as the struct variable to which it belongs. In other words, when a variable of a struct type comes into existence or ceases to exist, so too do the instance variables of the struct.

The initial assignment state of an instance variable of a struct is the same as that of the containing struct variable. In other words, when a struct variable is considered initially assigned, so too are its instance variables, and when a struct variable is considered initially unassigned, its instance variables are likewise unassigned.

### Array elements

The elements of an array come into existence when an array instance is created, and cease to exist when there are no references to that array instance.

The initial value of each of the elements of an array is the default value (§10.3) of the type of the array elements.

For the purpose of definite assignment checking, an array element is considered initially assigned.

### Value parameters

A parameter declared without a ref or out modifier is a ***value parameter***.

A value parameter comes into existence upon invocation of the function member (method, instance constructor, accessor, or operator) or anonymous function to which the parameter belongs, and is initialized with the value of the argument given in the invocation. A value parameter normally ceases to exist when execution of the function body completes. However, if the value parameter is captured by an anonymous function (§12.16.6.2), its lifetime extends at least until the delegate or expression tree created from that anonymous function is eligible for garbage collection.

For the purpose of definite assignment checking, a value parameter is considered initially assigned.

### Reference parameters

A parameter declared with a ref modifier is a ***reference parameter***.

A reference parameter does not create a new storage location. Instead, a reference parameter represents the same storage location as the variable given as the argument in the function member or anonymous function invocation. Thus, the value of a reference parameter is always the same as the underlying variable.

The following definite assignment rules apply to reference parameters. \[*Note*: The rules for output parameters are different, and are described in §10.2.7. *end note*\]

-   A variable shall be definitely assigned (§10.4) before it can be passed as a reference parameter in a function member or delegate invocation.

-   Within a function member or anonymous function, a reference parameter is considered initially assigned.

For a struct type, within an instance method or instance accessor (§12.2.1) or instance constructor with a constructor initializer, the this keyword behaves exactly as a reference parameter of the struct type (§12.7.8).

### Output parameters

A parameter declared with an out modifier is an ***output parameter***.

An output parameter does not create a new storage location. Instead, an output parameter represents the same storage location as the variable given as the argument in the function member or delegate invocation. Thus, the value of an output parameter is always the same as the underlying variable.

The following definite assignment rules apply to output parameters. \[*Note*: The rules for reference parameters are different, and are described in §10.2.6. *end note*\]

-   A variable need not be definitely assigned before it can be passed as an output parameter in a function member or delegate invocation.

-   Following the normal completion of a function member or delegate invocation, each variable that was passed as an output parameter is considered assigned in that execution path.

-   Within a function member or anonymous function, an output parameter is considered initially unassigned.

-   Every output parameter of a function member or anonymous function shall be definitely assigned (§10.4) before the function member or anonymous function returns normally.

Within an instance constructor of a struct type, the this keyword behaves exactly as an output or reference parameter of the struct type, depending on whether the constructor declaration includes a constructor initializer (§12.7.8).

### Local variables

A ***local variable*** is declared by a *local-variable-declaration*, *foreach-statement*, or *specific-catch-clause* of a *try-statement*. For a *foreach-statement*, the local variable is an iteration variable (§13.9.5). For a *specific-catch-clause*, the local variable is an exception variable (§13.11). A local variable declared by a *foreach-statement* or *specific-catch-clause* is considered initially assigned.

A *local-variable-declaration* can occur in a *block*, a *for-statement*, a *switch-block*, or a *using-statement*.

The lifetime of a local variable is the portion of program execution during which storage is guaranteed to be reserved for it. This lifetime extends from entry into the scope with which it is associated, at least until execution of that scope ends in some way. (Entering an enclosed *block*, calling a method, or yielding a value from an iterator block suspends, but does not end, execution of the current scope.) If the local variable is captured by an anonymous function (§12.16.6.2), its lifetime extends at least until the delegate or expression tree created from the anonymous function, along with any other objects that come to reference the captured variable, are eligible for garbage collection. If the parent scope is entered recursively or iteratively, a new instance of the local variable is created each time, and its *local-variable-initializer*, if any, is evaluated each time. \[*Note*: A local variable is instantiated each time its scope is entered. This behavior is visible to user code containing anonymous methods. *end note*\] \[*Note*: The lifetime of an *iteration variable* (§13.9.5) declared by a *foreach-statement* is a single iteration of that statement. Each iteration creates a new variable. *end note*\] \[*Note*: The actual lifetime of a local variable is implementation-dependent. For example, a compiler might statically determine that a local variable in a block is only used for a small portion of that block. Using this analysis, the compiler could generate code that results in the variable’s storage having a shorter lifetime than its containing block.

The storage referred to by a local reference variable is reclaimed independently of the lifetime of that local reference variable (§8.9). *end note*\]

A local variable introduced by a *local-variable-declaration* is not automatically initialized and thus has no default value. Such a local variable is considered initially unassigned. \[*Note*: A *local-variable-declaration* that includes a *local-variable-initializer* is still initially unassigned. Execution of the declaration behaves exactly like an assignment to the variable (§10.4.4.5). It is possible to use a variable without executing its *local-variable-initializer*; e.g., within the initializer expression itself or by using a *goto-statement* to bypass the initialization:

goto L;

int x = 1; // never executed

L: x += 1; // error: x not definitely assigned

*end note*\]

Within the scope of a local variable, it is a compile-time error to refer to that local variable in a textual position that precedes its *local-variable-declarator*.

## Default values

The following categories of variables are automatically initialized to their default values:

-   Static variables.

-   Instance variables of class instances.

-   Array elements.

The default value of a variable depends on the type of the variable and is determined as follows:

-   For a variable of a *value-type*, the default value is the same as the value computed by the *value-type*’s default constructor (§9.3.3).

-   For a variable of a *reference-type*, the default value is null.

\[*Note*: Initialization to default values is typically done by having the memory manager or garbage collector initialize memory to all-bits-zero before it is allocated for use. For this reason, it is convenient to use all-bits-zero to represent the null reference. *end note*\]

## Definite assignment

### General

At a given location in the executable code of a function member or an anonymous function, a variable is said to be ***definitely assigned*** if the compiler can prove, by a particular static flow analysis (§10.4.4), that the variable has been automatically initialized or has been the target of at least one assignment. \[*Note*: Informally stated, the rules of definite assignment are:

-   An initially assigned variable (§10.4.2) is always considered definitely assigned.

-   An initially unassigned variable (§10.4.3) is considered definitely assigned at a given location if all possible execution paths leading to that location contain at least one of the following:

<!-- -->

-   A simple assignment (§12.18.2) in which the variable is the left operand.

-   An invocation expression (§12.7.6) or object creation expression (§12.7.11.2) that passes the variable as an output parameter.

-   For a local variable, a local variable declaration for the variable (§13.6.2) that includes a variable initializer.

The formal specification underlying the above informal rules is described in §10.4.2, §10.4.3, and §10.4.4. *end note*\]

The definite assignment states of instance variables of a *struct-type* variable are tracked individually as well as collectively. In additional to the rules above, the following rules apply to *struct-type* variables and their instance variables:

-   An instance variable is considered definitely assigned if its containing *struct-type* variable is considered definitely assigned.

-   A *struct-type* variable is considered definitely assigned if each of its instance variables is considered definitely assigned.

Definite assignment is a requirement in the following contexts:

-   A variable shall be definitely assigned at each location where its value is obtained. \[*Note*: This ensures that undefined values never occur. *end note*\] The occurrence of a variable in an expression is considered to obtain the value of the variable, except when

<!-- -->

-   the variable is the left operand of a simple assignment,

-   the variable is passed as an output parameter, or

-   the variable is a *struct-type* variable and occurs as the left operand of a member access.

<!-- -->

-   A variable shall be definitely assigned at each location where it is passed as a reference parameter. \[*Note*: This ensures that the function member being invoked can consider the reference parameter initially assigned. *end note*\]

-   All output parameters of a function member shall be definitely assigned at each location where the function member returns (through a return statement or through execution reaching the end of the function member body). \[*Note*: This ensures that function members do not return undefined values in output parameters, thus enabling the compiler to consider a function member invocation that takes a variable as an output parameter equivalent to an assignment to the variable. *end note*\]

-   The this variable of a *struct-type* instance constructor shall be definitely assigned at each location where that instance constructor returns.

### Initially assigned variables

The following categories of variables are classified as initially assigned:

-   Static variables.

-   Instance variables of class instances.

-   Instance variables of initially assigned struct variables.

-   Array elements.

-   Value parameters.

-   Reference parameters.

-   Variables declared in a catch clause or a foreach statement.

### Initially unassigned variables

The following categories of variables are classified as initially unassigned:

-   Instance variables of initially unassigned struct variables.

-   Output parameters, including the this variable of struct instance constructors without a constructor initializer.

-   Local variables, except those declared in a catch clause or a foreach statement.

### Precise rules for determining definite assignment

#### General

In order to determine that each used variable is definitely assigned, the compiler shall use a process that is equivalent to the one described in this subclause.

The compiler processes the body of each function member that has one or more initially unassigned variables. For each initially unassigned variable *v*, the compiler determines a ***definite assignment state*** for *v* at each of the following points in the function member:

-   At the beginning of each statement

-   At the end point (§13.2) of each statement

-   On each arc which transfers control to another statement or to the end point of a statement

-   At the beginning of each expression

-   At the end of each expression

The definite assignment state of *v* can be either:

-   Definitely assigned. This indicates that on all possible control flows to this point, *v* has been assigned a value.

-   Not definitely assigned. For the state of a variable at the end of an expression of type bool, the state of a variable that isn’t definitely assigned might (but doesn’t necessarily) fall into one of the following sub-states:

<!-- -->

-   Definitely assigned after true expression. This state indicates that *v* is definitely assigned if the Boolean expression evaluated as true, but is not necessarily assigned if the Boolean expression evaluated as false.

-   Definitely assigned after false expression. This state indicates that *v* is definitely assigned if the Boolean expression evaluated as false, but is not necessarily assigned if the Boolean expression evaluated as true.

The following rules govern how the state of a variable *v* is determined at each location.

#### General rules for statements

-   *v* is not definitely assigned at the beginning of a function member body.

-   The definite assignment state of *v* at the beginning of any other statement is determined by checking the definite assignment state of *v* on all control flow transfers that target the beginning of that statement. If (and only if) *v* is definitely assigned on all such control flow transfers, then *v* is definitely assigned at the beginning of the statement. The set of possible control flow transfers is determined in the same way as for checking statement reachability (§13.2).

-   The definite assignment state of *v* at the end point of a block, checked, unchecked, if, while, do, for, foreach, lock, using, or switch statement is determined by checking the definite assignment state of *v* on all control flow transfers that target the end point of that statement. If *v* is definitely assigned on all such control flow transfers, then *v* is definitely assigned at the end point of the statement. Otherwise, *v* is not definitely assigned at the end point of the statement. The set of possible control flow transfers is determined in the same way as for checking statement reachability (§13.2).

    \[*Note*: Because there are no control paths to an unreachable statement, *v* is definitely assigned at the beginning of any unreachable statement. *end note*\]

#### Block statements, checked, and unchecked statements

The definite assignment state of *v* on the control transfer to the first statement of the statement list in the block (or to the end point of the block, if the statement list is empty) is the same as the definite assignment statement of *v* before the block, checked, or unchecked statement.

#### Expression statements

For an expression statement *stmt* that consists of the expression *expr*:

-   *v* has the same definite assignment state at the beginning of *expr* as at the beginning of *stmt*.

-   If *v* if definitely assigned at the end of *expr*, it is definitely assigned at the end point of *stmt*; otherwise, it is not definitely assigned at the end point of *stmt*.

#### Declaration statements

-   If *stmt* is a declaration statement without initializers, then *v* has the same definite assignment state at the end point of *stmt* as at the beginning of *stmt*.

-   If *stmt* is a declaration statement with initializers, then the definite assignment state for *v* is determined as if *stmt* were a statement list, with one assignment statement for each declaration with an initializer (in the order of declaration).

#### If statements

For an if statement *stmt* of the form:

if ( *expr* ) *then-stmt* else *else-stmt*

-   *v* has the same definite assignment state at the beginning of *expr* as at the beginning of *stmt*.

-   If *v* is definitely assigned at the end of *expr*, then it is definitely assigned on the control flow transfer to *then-stmt* and to either *else-stmt* or to the end-point of *stmt* if there is no else clause.

-   If *v* has the state “definitely assigned after true expression” at the end of *expr*, then it is definitely assigned on the control flow transfer to *then-stmt*, and not definitely assigned on the control flow transfer to either *else-stmt* or to the end-point of *stmt* if there is no else clause.

-   If *v* has the state “definitely assigned after false expression” at the end of *expr*, then it is definitely assigned on the control flow transfer to *else-stmt*, and not definitely assigned on the control flow transfer to *then-stmt*. It is definitely assigned at the end-point of *stmt* if and only if it is definitely assigned at the end-point of *then-stmt*.

-   Otherwise, *v* is considered not definitely assigned on the control flow transfer to either the *then-stmt* or *else-stmt*, or to the end-point of *stmt* if there is no else clause.

#### Switch statements

In a switch statement *stmt* with a controlling expression *expr*:

-   The definite assignment state of *v* at the beginning of *expr* is the same as the state of *v* at the beginning of *stmt*.

-   The definite assignment state of *v* on the control flow transfer to a reachable switch block statement list is the same as the definite assignment state of *v* at the end of *expr*.

#### While statements

For a while statement *stmt* of the form:

while ( *expr* ) *while-body*

-   *v* has the same definite assignment state at the beginning of *expr* as at the beginning of *stmt*.

-   If *v* is definitely assigned at the end of *expr*, then it is definitely assigned on the control flow transfer to *while-body* and to the end point of *stmt*.

-   If *v* has the state “definitely assigned after true expression” at the end of *expr*, then it is definitely assigned on the control flow transfer to *while-body*, but not definitely assigned at the end-point of *stmt*.

-   If *v* has the state “definitely assigned after false expression” at the end of *expr*, then it is definitely assigned on the control flow transfer to the end point of *stmt*, but not definitely assigned on the control flow transfer to *while-body*.

#### Do statements

For a do statement *stmt* of the form:

do *do-body* while ( *expr* ) ;

-   *v* has the same definite assignment state on the control flow transfer from the beginning of *stmt* to *do-body* as at the beginning of *stmt*.

-   *v* has the same definite assignment state at the beginning of *expr* as at the end point of *do-body*.

-   If *v* is definitely assigned at the end of *expr*, then it is definitely assigned on the control flow transfer to the end point of *stmt*.

-   If *v* has the state “definitely assigned after false expression” at the end of *expr*, then it is definitely assigned on the control flow transfer to the end point of *stmt*, but not definitely assigned on the control flow transfer to *do-body*.

#### For statements

Definite assignment checking for a for statement of the form:

for ( *for-initializer* ; *for-condition* ; *for-iterator* ) *embedded-statement*

is done as if the statement were written:

{\
*for-initializer* ;\
while ( *for-condition* ) {\
*embedded-statement* ;\
LLoop: *for-iterator* ;\
}\
}

with continue statements that target the for statement being translated to goto statements targeting the label LLoop. If the *for-condition* is omitted from the for statement, then evaluation of definite assignment proceeds as if *for-condition* were replaced with true in the above expansion.

#### Break, continue, and goto statements

The definite assignment state of *v* on the control flow transfer caused by a break, continue, or goto statement is the same as the definite assignment state of *v* at the beginning of the statement.

#### Throw statements

For a statement *stmt* of the form

throw *expr* ;

the definite assignment state of *v* at the beginning of *expr* is the same as the definite assignment state of *v* at the beginning of *stmt*.

#### Return statements

For a statement *stmt* of the form

return *expr* ;

-   The definite assignment state of *v* at the beginning of *expr* is the same as the definite assignment state of *v* at the beginning of *stmt*.

-   If *v* is an output parameter, then it shall be definitely assigned either:

<!-- -->

-   after *expr*

-   or at the end of the finally block of a try-finally or try-catch-finally that encloses the return statement.

For a statement *stmt* of the form:

return ;

-   If *v* is an output parameter, then it shall be definitely assigned either:

<!-- -->

-   before *stmt*

-   or at the end of the finally block of a try-finally or try-catch-finally that encloses the return statement.

#### Try-catch statements

For a statement *stmt* of the form:

try *try-block*\
catch ( … ) *catch-block-1*\
…\
catch ( … ) *catch-block-n*

-   The definite assignment state of *v* at the beginning of *try-block* is the same as the definite assignment state of *v* at the beginning of *stmt*.

-   The definite assignment state of *v* at the beginning of *catch-block-i* (for any *i*) is the same as the definite assignment state of *v* at the beginning of *stmt*.

-   The definite assignment state of *v* at the end-point of *stmt* is definitely assigned if (and only if) *v* is definitely assigned at the end-point of *try-block* and every *catch-block-i* (for every *i* from 1 to *n*).

#### Try-finally statements

For a try statement *stmt* of the form:

try *try-block* finally *finally-block*

-   The definite assignment state of *v* at the beginning of *try-block* is the same as the definite assignment state of *v* at the beginning of *stmt*.

-   The definite assignment state of *v* at the beginning of *finally-block* is the same as the definite assignment state of *v* at the beginning of *stmt*.

-   The definite assignment state of *v* at the end-point of *stmt* is definitely assigned if (and only if) at least one of the following is true:

<!-- -->

-   *v* is definitely assigned at the end-point of *try-block*

-   *v* is definitely assigned at the end-point of *finally-block*

If a control flow transfer (such as a goto statement) is made that begins within *try-block*, and ends outside of *try-block*, then *v* is also considered definitely assigned on that control flow transfer if *v* is definitely assigned at the end-point of *finally-block*. (This is not an only if—if *v* is definitely assigned for another reason on this control flow transfer, then it is still considered definitely assigned.)

#### Try-catch-finally statements

Definite assignment analysis for a try-catch-finally statement of the form:

try *try-block*\
catch ( … ) *catch-block-1*\
…\
catch ( … ) *catch-block-n*\
finally *finally-block*

is done as if the statement were a try-finally statement enclosing a try-catch statement:

try {\
try *try-block*\
catch ( … ) *catch-block-1*\
…\
catch ( … ) *catch-block-n*\
}\
finally *finally-block*

\[*Example*: The following example demonstrates how the different blocks of a try statement (§13.11) affect definite assignment.

class A\
{\
static void F() {\
int i, j;\
try {\
goto LABEL;\
// neither i nor j definitely assigned\
i = 1;\
// i definitely assigned\
}

catch {\
// neither i nor j definitely assigned\
i = 3;\
// i definitely assigned\
}

finally {\
// neither i nor j definitely assigned\
j = 5;\
// j definitely assigned\
}\
// i and j definitely assigned\
LABEL:;\
// j definitely assigned\
\
}\
}

*end example*\]

#### Foreach statements

For a foreach statement *stmt* of the form:

foreach ( *type* *identifier* in *expr* ) *embedded-statement*

-   The definite assignment state of *v* at the beginning of *expr* is the same as the state of *v* at the beginning of *stmt*.

-   The definite assignment state of *v* on the control flow transfer to *embedded-statement* or to the end point of *stmt* is the same as the state of *v* at the end of *expr*.

#### Using statements

For a using statement *stmt* of the form:

using ( *resource-acquisition* ) *embedded-statement*

-   The definite assignment state of *v* at the beginning of *resource-acquisition* is the same as the state of *v* at the beginning of *stmt*.

-   The definite assignment state of *v* on the control flow transfer to *embedded-statement* is the same as the state of *v* at the end of *resource-acquisition*.

#### Lock statements

For a lock statement *stmt* of the form:

lock ( *expr* ) *embedded-statement*

-   The definite assignment state of *v* at the beginning of *expr* is the same as the state of *v* at the beginning of *stmt*.

-   The definite assignment state of *v* on the control flow transfer to *embedded-statement* is the same as the state of *v* at the end of *expr*.

#### Yield statements

For a yield return statement *stmt* of the form:

yield return *expr* ;

-   The definite assignment state of *v* at the beginning of *expr* is the same as the state of *v* at the beginning of *stmt*.

-   The definite assignment state of *v* at the end of *stmt* is the same as the state of *v* at the end of *expr*.

A yield break statement has no effect on the definite assignment state.

#### General rules for constant expressions

The following applies to any constant expression, and takes priority over any rules from the following sections that might apply:

For a constant expression with value true:

-   If *v* is definitely assigned before the expression, then *v* is definitely assigned after the expression.

-   Otherwise *v* is “definitely assigned after false expression” after the expression.

\[*Example*:

int x;\
if (true) {}\
else\
{\
Console.WriteLine(x);\
}

*end example*\]

For a constant expression with value false:

-   If *v* is definitely assigned before the expression, then *v* is definitely assigned after the expression.

-   Otherwise *v* is “definitely assigned after true expression” after the expression.

\[*Example*:

int x;\
if (false)\
{\
Console.WriteLine(x);\
}

*end example*\]

For all other constant expressions, the definite assignment state of *v* after the expression is the same as the definite assignment state of *v* before the expression.

#### General rules for simple expressions

The following rule applies to these kinds of expressions: literals (§12.7.2), simple names (§12.7.3), member access expressions (§12.7.5), non-indexed base access expressions (§12.7.9), typeof expressions (§12.7.12), and default value expressions (§12.7.15).

-   The definite assignment state of *v* at the end of such an expression is the same as the definite assignment state of *v* at the beginning of the expression.

#### General rules for expressions with embedded expressions

The following rules apply to these kinds of expressions: parenthesized expressions (§12.7.4), element access expressions (§12.7.7), base access expressions with indexing (§12.7.9), increment and decrement expressions (§12.7.10, §12.8.6), cast expressions (§12.8.7), unary +, -, \~, \* expressions, binary +, -, \*, /, %, &lt;&lt;, &gt;&gt;, &lt;, &lt;=, &gt;, &gt;=, ==, !=, is, as, &, |, \^ expressions (§12.9, §12.10, §12.11, §12.12), compound assignment expressions (§12.18.3), checked and unchecked expressions (§12.7.14), array and delegate creation expressions (§12.7.11) , and await expressions (§12.8.8).

Each of these expressions has one or more subexpressions that are unconditionally evaluated in a fixed order. \[*Example*: The binary % operator evaluates the left hand side of the operator, then the right hand side. An indexing operation evaluates the indexed expression, and then evaluates each of the index expressions, in order from left to right. *end example*\] For an expression *expr*, which has subexpressions *expr~1~*, *expr~2~*, …, *expr~n~*, evaluated in that order:

-   The definite assignment state of *v* at the beginning of *expr~1~* is the same as the definite assignment state at the beginning of *expr*.

-   The definite assignment state of *v* at the beginning of *expr~i~* (*i* greater than one) is the same as the definite assignment state at the end of *expr~i-1~*.

-   The definite assignment state of *v* at the end of *expr* is the same as the definite assignment state at the end of *expr~n~*.

#### Invocation expressions and object creation expressions

If the method to be invoked is a partial method that has no implementing partial method declaration, or is a conditional method for which the call is omitted (§22.5.3.2), then the definite assignment state of *v* after the invocation is the same as the definite assignment state of *v* before the invocation. Otherwise the following rules apply:

For an invocation expression *expr* of the form:

*primary-expression* ( *arg~1~*, *arg~2~*, … , *arg~n~* )

or an object creation expression *expr* of the form:

new *type* ( *arg~1~*, *arg~2~*, … , *arg~n~* )

-   For an invocation expression, the definite assignment state of *v* before *primary-expression* is the same as the state of *v* before *expr*.

-   For an invocation expression, the definite assignment state of *v* before *arg~1~* is the same as the state of *v* after *primary-expression*.

-   For an object creation expression, the definite assignment state of *v* before *arg~1~* is the same as the state of *v* before *expr*.

-   For each argument *arg~i~*, the definite assignment state of *v* after *arg~i~* is determined by the normal expression rules, ignoring any ref or out modifiers.

-   For each argument *arg~i~* for any *i* greater than one, the definite assignment state of *v* before *arg~i~* is the same as the state of *v* after *arg~i-1~*.

-   If the variable *v* is passed as an out argument (i.e., an argument of the form “out *v*”) in any of the arguments, then the state of *v* after *expr* is definitely assigned. Otherwise, the state of *v* after *expr* is the same as the state of *v* after *arg~n~*.

-   For array initializers (§12.7.11.5), object initializers (12.7.11.3), collection initializers (§12.7.11.4) and anonymous object initializers (§12.7.11.7), the definite assignment state is determined by the expansion that these constructs are defined in terms of.

#### Simple assignment expressions

For an expression *expr* of the form *w* = *expr-rhs*:

-   The definite assignment state of *v* before *w* is the same as the definite assignment state of *v* before *expr*.

-   The definite assignment state of *v* before *expr-rhs* is the same as the definite assignment state of *v* after *w*.

-   If *w* is the same variable as *v*, then the definite assignment state of *v* after *expr* is definitely assigned. Otherwise, the definite assignment state of *v* after *expr* is the same as the definite assignment state of *v* after *expr-rhs*.

\[*Example*: In the following code

class A\
{\
static void F(int\[\] arr) {\
int x;

arr\[x = 1\] = x; // ok\
}\
}

the variable x is considered definitely assigned after arr\[x = 1\] is evaluated as the left hand side of the second simple assignment. *end example*\]

#### && expressions

For an expression *expr* of the form *expr-first* && *expr-second*:

-   The definite assignment state of *v* before *expr-first* is the same as the definite assignment state of *v* before *expr*.

-   The definite assignment state of *v* before *expr-second* is definitely assigned if and only if the state of *v* after *expr-first* is either definitely assigned or “definitely assigned after true expression”. Otherwise, it is not definitely assigned.

-   The definite assignment state of *v* after *expr* is determined by:

<!-- -->

-   If the state of *v* after *expr-first* is definitely assigned, then the state of *v* after *expr* is definitely assigned.

-   Otherwise, if the state of *v* after *expr-second* is definitely assigned, and the state of *v* after *expr-first* is “definitely assigned after false expression”, then the state of *v* after *expr* is definitely assigned.

-   Otherwise, if the state of *v* after *expr-second* is definitely assigned or “definitely assigned after true expression”, then the state of *v* after *expr* is “definitely assigned after true expression”.

-   Otherwise, if the state of *v* after *expr-first* is “definitely assigned after false expression”, and the state of *v* after *expr-second* is “definitely assigned after false expression”, then the state of *v* after *expr* is “definitely assigned after false expression”.

-   Otherwise, the state of *v* after *expr* is not definitely assigned.

\[*Example*: In the following code

class A\
{\
static void F(int x, int y) {\
int i;\
if (x &gt;= 0 && (i = y) &gt;= 0) {\
// i definitely assigned\
}\
else {\
// i not definitely assigned\
}\
// i not definitely assigned\
}\
}

the variable i is considered definitely assigned in one of the embedded statements of an if statement but not in the other. In the if statement in method F, the variable i is definitely assigned in the first embedded statement because execution of the expression (i = y) always precedes execution of this embedded statement. In contrast, the variable i is not definitely assigned in the second embedded statement, since x &gt;= 0 might have tested false, resulting in the variable i’s being unassigned. *end example*\]

#### || expressions

For an expression *expr* of the form *expr-first* || *expr-second*:

-   The definite assignment state of *v* before *expr-first* is the same as the definite assignment state of *v* before *expr*.

-   The definite assignment state of *v* before *expr-second* is definitely assigned if and only if the state of *v* after *expr-first* is either definitely assigned or “definitely assigned after true expression”. Otherwise, it is not definitely assigned.

-   The definite assignment statement of *v* after *expr* is determined by:

<!-- -->

-   If the state of *v* after *expr-first* is definitely assigned, then the state of *v* after *expr* is definitely assigned.

-   Otherwise, if the state of *v* after *expr-second* is definitely assigned, and the state of *v* after *expr-first* is “definitely assigned after true expression”, then the state of *v* after *expr* is definitely assigned.

-   Otherwise, if the state of *v* after *expr-second* is definitely assigned or “definitely assigned after false expression”, then the state of *v* after *expr* is “definitely assigned after false expression”.

-   Otherwise, if the state of *v* after *expr-first* is “definitely assigned after true expression”, and the state of *v* after *expr-second* is “definitely assigned after true expression”, then the state of *v* after *expr* is “definitely assigned after true expression”.

-   Otherwise, the state of *v* after *expr* is not definitely assigned.

\[*Example*: In the following code

class A\
{\
static void G(int x, int y) {\
int i;\
if (x &gt;= 0 || (i = y) &gt;= 0) {\
// i not definitely assigned\
}\
else {\
// i definitely assigned\
}\
// i not definitely assigned\
}\
}

the variable i is considered definitely assigned in one of the embedded statements of an if statement but not in the other. In the if statement in method G, the variable i is definitely assigned in the second embedded statement because execution of the expression (i = y) always precedes execution of this embedded statement. In contrast, the variable i is not definitely assigned in the first embedded statement, since x &gt;= 0 might have tested true, resulting in the variable i's being unassigned. *end example*\]

#### ! expressions

For an expression *expr* of the form ! *expr-operand*:

-   The definite assignment state of *v* before *expr-operand* is the same as the definite assignment state of *v* before *expr*.

-   The definite assignment state of *v* after *expr* is determined by:

<!-- -->

-   If the state of v after *expr-operand* is definitely assigned, then the state of v after *expr* is definitely assigned.

-   Otherwise, if the state of v after *expr-operand* is “definitely assigned after false expression”, then the state of v after *expr* is “definitely assigned after true expression”.

-   Otherwise, if the state of v after *expr-operand* is “definitely assigned after true expression”, then the state of v after *expr* is “definitely assigned after false expression”.

-   Otherwise, the state of v after *expr* is not definitely assigned.

#### ?? expressions

For an expression *expr* of the form *expr-first* ?? *expr-second*:

-   The definite assignment state of *v* before *expr-first* is the same as the definite assignment state of *v* before *expr*.

-   The definite assignment state of *v* before *expr-second* is the same as the definite assignment state of *v* after *expr-first*.

-   The definite assignment statement of *v* after *expr* is determined by:

<!-- -->

-   If *expr-first* is a constant expression (§12.20) with value null, then the state of *v* after *expr* is the same as the state of *v* after *expr-second*.

-   Otherwise, the state of *v* after *expr* is the same as the definite assignment state of *v* after *expr-first*.

#### ?: expressions

For an expression *expr* of the form *expr-cond* ? *expr-true* : *expr-false*:

-   The definite assignment state of *v* before *expr-cond* is the same as the state of *v* before *expr*.

-   The definite assignment state of *v* before *expr-true* is definitely assigned if the state of *v* after *expr-cond* is definitely assigned or “definitely assigned after true expression”.

-   The definite assignment state of *v* before *expr-false* is definitely assigned if the state of *v* after *expr-cond* is definitely assigned or “definitely assigned after false expression”.

-   The definite assignment state of *v* after *expr* is determined by:

<!-- -->

-   If *expr-cond* is a constant expression (§12.20) with value true then the state of *v* after *expr* is the same as the state of *v* after *expr-true*.

-   Otherwise, if *expr-cond* is a constant expression (§12.20) with value false then the state of *v* after *expr* is the same as the state of *v* after *expr-false*.

-   Otherwise, if the state of *v* after *expr-true* is definitely assigned and the state of *v* after *expr-false* is definitely assigned, then the state of *v* after *expr* is definitely assigned.

-   Otherwise, the state of *v* after *expr* is not definitely assigned.

#### Anonymous functions

For a *lambda-expression* or *anonymous-method-expression* *expr* with a body (either *block* or *expression*) *body*:

-   The definite assignment state of a parameter is the same as for a parameter of a named method (§10.2.6, §10.2.7).

-   The definite assignment state of an outer variable *v* before *body* is the same as the state of *v* before *expr*. That is, definite assignment state of outer variables is inherited from the context of the anonymous function.

-   The definite assignment state of an outer variable *v* after *expr* is the same as the state of *v* before *expr*.

\[*Example*: The example

delegate bool Filter(int i);

void F() {\
int max;

// Error, max is not definitely assigned\
Filter f = (int n) =&gt; n &lt; max;

max = 5;\
DoWork(f);\
}

generates a compile-time error since max is not definitely assigned where the anonymous function is declared. *end example*\] \[*Example*: The example

delegate void D();

void F() {\
int n;\
D d = () =&gt; { n = 1; };

d();

// Error, n is not definitely assigned\
Console.WriteLine(n);\
}

also generates a compile-time error since the assignment to n in the anonymous function has no affect on the definite assignment state of n outside the anonymous function. *end example*\]

## Variable references

A *variable-reference* is an *expression* that is classified as a variable. A *variable-reference* denotes a storage location that can be accessed both to fetch the current value and to store a new value.

[]{#Grammar_variable_reference .anchor}variable-reference:\
expression

[[]{#_Toc445783004 .anchor}]{#_Toc446302805 .anchor}\[*Note*: In C and C++, a *variable-reference* is known as an *lvalue*. *end note*\]

## Atomicity of variable references

Reads and writes of the following data types shall be atomic: bool, char, byte, sbyte, short, ushort, uint, int, float, and reference types. In addition, reads and writes of enum types with an underlying type in the previous list shall also be atomic. Reads and writes of other types, including long, ulong, double, and decimal, as well as user-defined types, need not be atomic. Aside from the library functions designed for that purpose, there is no guarantee of atomic read-modify-write, such as in the case of increment or decrement.

