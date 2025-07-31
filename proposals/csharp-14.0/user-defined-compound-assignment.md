# User Defined Compound Assignment Operators

[!INCLUDE[Specletdisclaimer](./speclet-disclaimer.md)]

Champion issue: https://github.com/dotnet/csharplang/issues/9101

## Summary
[summary]: #summary

Allow user types to customize behavior of compound assignment operators in a way that the target of the assignment is
modified in-place.

## Motivation
[motivation]: #motivation

C# provides support for the developer overloading operator implementations for user-defined type.
Additionally, it provides support for "compound assignment operators" which allow the user to write
code similarly to `x += y` rather than `x = x + y`. However, the language does not currently allow
for the developer to overload these compound assignment operators and while the default behavior
does the right thing, especially as it pertains to immutable value types, it is not always
"optimal".

Given the following example
``` C#
class C1
{
    static void Main()
    {
        var c1 = new C1();
        c1 += 1;
        System.Console.Write(c1);
    }
    
    public static C1 operator+(C1 x, int y) => new C1();
}
```

with the current language rules, compound assignment operator `c1 += 1` invokes user defined `+` operator
and then assigns its return value to the local variable `c1`. Note that operator implementation must allocate
and return a new instance of `C1`, while, from the consumer's perspective, an in-place change to the original
instance of `C1` instead would work as good (it is not used after the assignment), with an additional benefit of
avoiding an extra allocation. 

When a program utilizes a compound assignment operation, the most common effect is that the original value is
"lost" and is no longer available to the program. With types which have large data (such as BigInteger, Tensors, etc.)
the cost of producing a net new destination, iterating, and copying the memory tends to be fairly expensive.
An in-place mutation would allow skipping this expense in many cases, which can provide significant improvements
to such scenarios.

Therefore, it may be beneficial for C# to allow user types to
customize behavior of compound assignment operators and optimize scenarios that would otherwise need to allocate
and copy.

## Detailed design
[design]: #detailed-design

### Syntax

Grammar at https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#15101-general is adjusted as follows.

Operators are declared using *operator_declaration*s:
```diff
operator_declaration
    : attributes? operator_modifier+ operator_declarator operator_body
    ;

operator_modifier
    : 'public'
    | 'static'
    | 'extern'
    | unsafe_modifier   // unsafe code support
    | 'abstract'
    | 'virtual'
    | 'sealed'
+   | 'override'
+   | 'new'
+   | 'readonly'
    ;

operator_declarator
    : unary_operator_declarator
    | binary_operator_declarator
    | conversion_operator_declarator
+   | increment_operator_declarator
+   | compound_assignment_operator_declarator
    ;

unary_operator_declarator
    : type 'operator' overloadable_unary_operator '(' fixed_parameter ')'
    ;

logical_negation_operator
    : '!'
    ;

overloadable_unary_operator
-   : '+' | 'checked'? '-' | logical_negation_operator | '~' | 'checked'? '++' | 'checked'? '--' | 'true' | 'false'
+   : '+' | 'checked'? '-' | logical_negation_operator | '~' | 'true' | 'false'
    ;

binary_operator_declarator
    : type 'operator' overloadable_binary_operator
        '(' fixed_parameter ',' fixed_parameter ')'
    ;

overloadable_binary_operator
    : 'checked'? '+'  | 'checked'? '-'  | 'checked'? '*'  | 'checked'? '/'  | '%'  | '&' | '|' | '^'  | '<<'
    | right_shift | '==' | '!=' | '>' | '<' | '>=' | '<='
    ;

conversion_operator_declarator
    : 'implicit' 'operator' type '(' fixed_parameter ')'
    | 'explicit' 'operator' type '(' fixed_parameter ')'
    ;

+increment_operator_declarator
+   : type 'operator' overloadable_increment_operator '(' fixed_parameter ')'
+   | 'void' 'operator' overloadable_increment_operator '(' ')'
+   ;

+overloadable_increment_operator
+   : 'checked'? '++' | 'checked'? '--'
+    ;

+compound_assignment_operator_declarator
+   : 'void' 'operator' overloadable_compound_assignment_operator
+       '(' fixed_parameter ')'
+   ;

+overloadable_compound_assignment_operator
+   : 'checked'? '+=' | 'checked'? '-=' | 'checked'? '*=' | 'checked'? '/=' | '%=' | '&=' | '|=' | '^=' | '<<='
+   | right_shift_assignment
+   | unsigned_right_shift_assignment
+   ;

operator_body
    : block
    | '=>' expression ';'
    | ';'
    ;
```

There are five categories of overloadable operators: [unary operators](#unary-operators), [binary operators](#binary-operators),
[conversion operators](#conversion-operators), [increment operators](#increment-operators), [compound assignment operators](#compound-assignment-operators).

>The following rules apply to all operator declarations:
>
>- An operator declaration shall include ~~both~~ a `public` ~~and a `static`~~ modifier.

Compound assignment and instance increment operators can hide operators declared in a base class. Therefore,
the following paragraph is no longer accurate and should either be adjusted accordingly, or it can be removed: 
>Because operator declarations always require the class or struct in which the operator is declared to participate in the signature of the operator,
it is not possible for an operator declared in a derived class to hide an operator declared in a base class. Thus, the `new` modifier is never
required, and therefore never permitted, in an operator declaration.

### Unary operators
[unary-operators]: #unary-operators

See https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#15102-unary-operators.

An operator declaration shall include a `static` modifier and shall not include an `override` modifier.

The following bullet point is removed:
>- A unary `++` or `--` operator shall take a single parameter of type `T` or `T?` and shall return that same type or a type derived from it.

The following paragraph is adjusted to no longer mention `++` and `--` operator tokens:
> The signature of a unary operator consists of the operator token (`+`, `-`, `!`, `~`, `++`, `--`, `true`, or `false`) and the type of the single parameter. The return type is not part of a unary operator’s signature, nor is the name of the parameter.

An example in the section should be adjusted to not use a user defined increment operator. 

### Binary operators
[binary-operators]: #binary-operators

See https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#15103-binary-operators.

An operator declaration shall include a `static` modifier and shall not include an `override` modifier.

### Conversion operators
[conversion-operators]: #conversion-operators

See https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#15104-conversion-operators.

An operator declaration shall include a `static` modifier and shall not include an `override` modifier.

### Increment operators
[increment-operators]: #increment-operators

The following rules apply to static increment operator declarations, where `T` denotes the instance type of the class or struct that contains the operator declaration:

- An operator declaration shall include a `static` modifier and shall not include an `override` modifier.
- An operator shall take a single parameter of type `T` or `T?` and shall return that same type or a type derived from it.

The signature of a static increment operator consists of the operator tokens ('checked'? `++`, 'checked'? `--`) and the type of the single parameter.
The return type is not part of a static increment operator’s signature, nor is the name of the parameter.

Static increment operators are very similar to [unary operators](#unary-operators).

The following rules apply to instance increment operator declarations:
- An operator declaration shall not include a `static` modifier.
- An operator shall take no parameters.
- An operator shall have `void` return type.

Effectively, an instance increment operator is a void returning instance method that has no parameters and
has a special name in metadata.

The signature of an instance increment operator consists of the operator tokens ('checked'? '++' | 'checked'? '--').

A `checked operator` declaration requires a pair-wise declaration of a `regular operator`. A compile-time error occurs otherwise. 
See also https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/checked-user-defined-operators.md#semantics.

The purpose of the method is to adjust the value of the instance to result of the requested increment operation,
whatever that means in context of the declaring type.

Example:
``` C#
class C1
{
    public int Value;

    public void operator ++()
    {
        Value++;
    }
}
```

An instance increment operator can override an operator with the same signature declared in a base class,
an `override` modifier can be used for this purpose.

The following "reserved" special names should be added to ECMA-335 to support instance versions of increment/decrement operators:
| Name | Operator |
| -----| -------- |
|op_DecrementAssignment| `--` |
|op_IncrementAssignment| `++` |
|op_CheckedDecrementAssignment| checked `--` |
|op_CheckedIncrementAssignment| checked `++` |

### Compound assignment operators
[compound-assignment-operators]: #compound-assignment-operators

The following rules apply to compound assignment operator declarations:
- An operator declaration shall not include a `static` modifier.
- An operator shall take one parameter.
- An operator shall have `void` return type.

Effectively, a compound assignment operator is a void returning instance method that takes one parameter and
has a special name in metadata.

The signature of a compound assignment operator consists of the operator tokens
('checked'? '+=', 'checked'? '-=', 'checked'? '*=', 'checked'? '/=', '%=', '&=', '|=', '^=', '<<=', right_shift_assignment, unsigned_right_shift_assignment) and
the type of the single parameter. The name of the parameter is not part of a compound assignment operator’s signature.

A `checked operator` declaration requires a pair-wise declaration of a `regular operator`. A compile-time error occurs otherwise.
See also https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/checked-user-defined-operators.md#semantics.

The purpose of the method is to adjust the value of the instance to result of ```<instance> <binary operator token> parameter```.

Example:
``` C#
class C1
{
    public int Value;

    public void operator +=(int x)
    {
        Value+=x;
    }
}
```

A compound assignment operator can override an operator with the same signature declared in a base class,
an `override` modifier can be used for this purpose.

ECMA-335 already "reserved" the following special names for user defined increment operators:
| Name | Operator |
| -----| -------- |
|op_AdditionAssignment|'+=' |
|op_SubtractionAssignment|'-=' |
|op_MultiplicationAssignment|'*=' |
|op_DivisionAssignment|'/=' |
|op_ModulusAssignment|'%=' |
|op_BitwiseAndAssignment|'&=' |
|op_BitwiseOrAssignment|'&#124;=' |
|op_ExclusiveOrAssignment|'^=' |
|op_LeftShiftAssignment|'<<='|
|op_RightShiftAssignment| right_shift_assignment|
|op_UnsignedRightShiftAssignment|unsigned_right_shift_assignment|

However, it states that CLS compliance requires the operator methods to be non-void static methods with two parameters,
i.e. matches what C# binary operators are. We should consider relaxing the CLS compliance requirements
to allow the operators to be void returning instance methods with a single parameter.

The following names should be added to support checked versions of the operators:
| Name | Operator |
| -----| -------- |
|op_CheckedAdditionAssignment| checked '+=' |
|op_CheckedSubtractionAssignment| checked '-=' |
|op_CheckedMultiplicationAssignment| checked '*=' |
|op_CheckedDivisionAssignment| checked '/=' |

### Prefix increment and decrement operators

See https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1296-prefix-increment-and-decrement-operators

If `x` in `«op» x` is classified as a variable and a new language version is targeted, then the priority is given to
[instance increment operators](#increment-operators) as follows.

First, an attempt is made to process the operation by applying
[instance increment operator overload resolution](#instance-increment-operator-overload-resolution).
If the process produces no result and no error, then the operation is processed
by applying unary operator overload resolution as
https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1296-prefix-increment-and-decrement-operators
currently specifies.

Otherwise, an operation `«op»x` is evaluated as follows.

If type of `x` is known to be a reference type, the `x` is evaluated to get an instance `x₀`, the operator method is
invoked on that instance, and `x₀` is returned as result of the operation.
If `x₀` is `null`, the operator method invocation will throw a NullReferenceException.


For example:
``` C#
var a = ++(new C()); // error: not a variable
var b = ++a; // var temp = a; temp.op_Increment(); b = temp; 
++b; // b.op_Increment();
var d = ++C.P1; // error: setter is missing
++C.P1; // error: setter is missing
var e = ++C.P2; // var temp = C.op_Increment(C.get_P2()); C.set_P2(temp); e = temp;
++C.P2; // var temp = C.op_Increment(C.get_P2()); C.set_P2(temp);

class C
{
    public static C P1 { get; } = new C();
    public static C P2 { get; set; } = new C();

    public static C operator ++(C x) => ...;
    public void operator ++() => ...;
}
```

If type of `x` is not known to be a reference type:
- If result of increment is used, the `x` is evaluated to get an instance `x₀`, the operator method is
  invoked on that instance, `x₀` is assigned to `x` and `x₀` is returned as result of
  the compound assignment.
- Otherwise, the operator method is invoked on `x`.
  
Note that side effects in `x` are evaluated only once in the process.

For example:
``` C#
var a = ++(new S()); // error: not a variable
var b = ++S.P2; // var temp = S.op_Increment(S.get_P2()); S.set_P2(temp); b = temp;
++S.P2; // var temp = S.op_Increment(S.get_P2()); S.set_P2(temp);
++b; // b.op_Increment(); 
var d = ++S.P1; // error: set is missing
++S.P1; // error: set is missing
var e = ++b; // var temp = b; temp.op_Increment(); e = (b = temp); 

struct S
{
    public static S P1 { get; } = new S();
    public static S P2 { get; set; } = new S();

    public static S operator ++(S x) => ...;
    public void operator ++() => ...;
}
```


### Postfix increment and decrement operators

See https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12816-postfix-increment-and-decrement-operators

If result of the operation is used or `x` in `x «op»` is not classified as a variable
or an old language version is targeted, the operation is processed by applying unary operator overload resolution as
https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12816-postfix-increment-and-decrement-operators
currently specifies. 
The reason why we are not even trying instance increment operators when result is used, is the fact that,
if we are dealing with a reference type, it is not possible to produce value of `x` before the operation if it is mutated in-place.
If we are dealing with a value type, we will have to make copies anyway, etc.

Otherwise, the priority is given to [instance increment operators](#increment-operators) as follows.

First, an attempt is made to process the operation by applying
[instance increment operator overload resolution](#instance-increment-operator-overload-resolution).
If the process produces no result and no error, then the operation is processed
by applying unary operator overload resolution as
https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12816-postfix-increment-and-decrement-operators
currently specifies.

Otherwise, an operation `x«op»` is evaluated as follows.

If type of `x` is known to be a reference type, the operator method is invoked on `x`.
If `x` is `null`, the operator method invocation will throw a NullReferenceException.


For example:
``` C#
var a = (new C())++; // error: not a variable
var b = new C(); 
var c = b++; // var temp = b; b = C.op_Increment(temp); c = temp; 
b++; // b.op_Increment();
var d = C.P1++; // error: missing setter
C.P1++; // error: missing setter
var e = C.P2++; // var temp = C.get_P2(); C.set_P2(C.op_Increment(temp)); e = temp;
C.P2++; // var temp = C.get_P2(); C.set_P2(C.op_Increment(temp));

class C
{
    public static C P1 { get; } = new C();
    public static C P2 { get; set; } = new C();

    public static C operator ++(C x) => ...; 
    public void operator ++() => ...;
}
```

If type of `x` is not known to be a reference type, the operator method is invoked on `x`.

For example:
``` C#
var a = (new S())++; // error: not a variable
var b = S.P2++; // var temp = S.get_P2(); S.set_P2(S.op_Increment(temp)); b = temp;
S.P2++; // var temp = S.get_P2(); S.set_P2(S.op_Increment(temp));
b++; // b.op_Increment(); 
var d = S.P1++; // error: set is missing
S.P1++; // error: missing setter
var e = b++; // var temp = b; b = S.op_Increment(temp); e = temp; 

struct S
{
    public static S P1 { get; } = new S();
    public static S P2 { get; set; } = new S();

    public static S operator ++(S x) => ...; 
    public void operator ++() => ...;
}
```

### Instance increment operator overload resolution
[instance-increment-operator-overload-resolution]: #instance-increment-operator-overload-resolution

An operation of the form `«op» x` or `x «op»`, where «op» is an overloadable instance increment operator,
and `x` is an expression of type `X`, is processed as follows:

- The set of candidate user-defined operators provided by `X` for the operation `operator «op»(x)` is determined
  using the rules of [candidate instance increment operators](#candidate-instance-increment-operators).
- If the set of candidate user-defined operators is not empty, then this becomes the set of candidate operators for the operation.
  Otherwise, the overload resolution yields no result.
- The [overload resolution rules](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1264-overload-resolution)
  are applied to the set of candidate operators to select the best operator,
  and this operator becomes the result of the overload resolution process. If overload resolution fails to select a single best operator,
  a binding-time error occurs.

### Candidate instance increment operators
[candidate-instance-increment-operators]: #candidate-instance-increment-operators

Given a type `T` and an operation `«op»`, where `«op»` is an overloadable instance increment operator,
the set of candidate user-defined operators provided by `T` is determined as follows:
- In `unchecked` evaluation context, it is a group of operators that would be produced
  by [Member lookup](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1251-general)
  process when only instance `operator «op»()` operators were considered matching the target name `N`.
- In `checked` evaluation context, it is a group of operators that would be produced
  by [Member lookup](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1251-general)
  process when only instance `operator «op»()` and instance `operator checked «op»()` operators were considered
  matching the target name `N`. The `operator «op»()` operators that have pair-wise matching `operator checked «op»()`
  declarations are excluded from the group.

### Compound assignment

See https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12214-compound-assignment

The paragraph at the beginning that deals with `dynamic` is still applicable as is.

Otherwise, if `x` in `x «op»= y` is classified as a variable and a new language version is targeted,
then the priority is given to [compound assignment operators](#compound-assignment-operators) as follows.

First, an attempt is made to process an operation of the form `x «op»= y` by applying
[compound assignment operator overload resolution](#compound-assignment-operator-overload-resolution).
If the process produces no result and no error, then the operation is processed
by applying binary operator overload resolution as
https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12214-compound-assignment
currently specifies.

Otherwise, the operation is evaluated as follows.

If type of `x` is known to be a reference type, the `x` is evaluated to get an instance `x₀`, the operator method is
invoked on that instance with `y` as the argument, and `x₀` is returned as result of the compound assignment.
If `x₀` is `null`, the operator method invocation will throw a NullReferenceException.

For example:
``` C#
var a = (new C())+=10; // error: not a variable
var b = a += 100; // var temp = a; temp.op_AdditionAssignment(100); b = temp; 
var c = b + 1000; // c = C.op_Addition(b, 1000)
c += 5; // c.op_AdditionAssignment(5);
var d = C.P1 += 11; // error: setter is missing
var e = C.P2 += 12; // var temp = C.op_Addition(C.get_P2(), 12); C.set_P2(temp); e = temp;
C.P2 += 13; // var temp = C.op_Addition(C.get_P2(), 13); C.set_P2(temp);

class C
{
    public static C P1 { get; } = new C();
    public static C P2 { get; set; } = new C();

    // op_Addition
    public static C operator +(C x, int y) => ...;

    // op_AdditionAssignment
    public void operator +=(int y) => ...;
}
```

If type of `x` is not known to be a reference type:
- If result of compound assignment is used, the `x` is evaluated to get an instance `x₀`, the operator method is
  invoked on that instance with `y` as the argument, `x₀` is assigned to `x` and `x₀` is returned as result of
  the compound assignment.
- Otherwise, the operator method is invoked on `x` with `y` as the argument.
  
Note that side effects in `x` are evaluated only once in the process.

For example:
``` C#
var a = (new S())+=10; // error: not a variable
var b = S.P2 += 100; // var temp = S.op_Addition(S.get_P2(), 100); S.set_P2(temp); b = temp;
S.P2 += 100; // var temp = S.op_Addition(S.get_P2(), 100); S.set_P2(temp);
var c = b + 1000; // c = S.op_Addition(b, 1000)
c += 5; // c.op_AdditionAssignment(5); 
var d = S.P1 += 11; // error: setter is missing
var e = c += 12; // var temp = c; temp.op_AdditionAssignment(12); e = (c = temp); 

struct S
{
    public static S P1 { get; } = new S();
    public static S P2 { get; set; } = new S();

    // op_Addition
    public static S operator +(S x, int y) => ...;

    // op_AdditionAssignment
    public void operator +=(int y) => ...;
}
```


### Compound assignment operator overload resolution
[compound-assignment-operator-overload-resolution]: #compound-assignment-operator-overload-resolution

An operation of the form `x «op»= y`, where `«op»=` is an overloadable compound assignment operator, `x` is an expression of type `X` is processed as follows:

- The set of candidate user-defined operators provided by `X` for the operation `operator «op»=(y)` is determined
  using the rules of [candidate compound assignment operators](#candidate-compound-assignment-operators).
- If at least one candidate user-defined operator in the set is applicable to the argument list `(y)`,
  then this becomes the set of candidate operators for the operation. Otherwise, the overload resolution yields no result.
- The [overload resolution rules](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1264-overload-resolution)
  are applied to the set of candidate operators to select the best operator with respect to the argument list `(y)`,
  and this operator becomes the result of the overload resolution process. If overload resolution fails to select a single best operator,
  a binding-time error occurs.

### Candidate compound assignment operators
[candidate-compound-assignment-operators]: #candidate-compound-assignment-operators

Given a type `T` and an operation `«op»=`, where `«op»=` is an overloadable compound assignment operator,
the set of candidate user-defined operators provided by `T` is determined as follows:
- In `unchecked` evaluation context, it is a group of operators that would be produced
  by [Member lookup](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1251-general)
  process when only instance `operator «op»=(Y)` operators were considered matching the target name `N`.
- In `checked` evaluation context, it is a group of operators that would be produced
  by [Member lookup](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1251-general)
  process when only instance `operator «op»=(Y)` and instance `operator checked «op»=(Y)` operators were considered
  matching the target name `N`. The `operator «op»=(Y)` operators that have pair-wise matching `operator checked «op»=(Y)`
  declarations are excluded from the group.

## Open questions
[open]: #open-questions

### [Resolved] Should `readonly` modifier be allowed in structures?

It feels like there would be no benefit in allowing to mark a method with `readonly` when the whole
purpose of the method is to modify the instance.

[Conclusion:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-02.md#conclusion)
We will allow `readonly` modifiers, but we will not relax the target requirements at this time.

### [Resolved] Should shadowing be allowed?

If a derived class declares a 'compound assignment'/'instance increment' operator with the same signature as one in base,
should we require an `override` modifier?

[Conclusion:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-02.md#conclusion-1)
Shadowing will be allowed with the same rules as methods.

### [Resolved] Should we have any consistency enforcement between declared `+=` and `+` operators?

During [LDM-2025-02-12](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-02-12.md#user-defined-instance-based-operators)
a concern was raised about authors accidentally pushing their users into odd scenarios where a `+=` may work, but
`+` won't (or vice versa) because one form declares extra operators than the other.

[Conclusion:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-02.md#conclusion-2)
Checks will not be done on consistency between different forms of operators.

## Alternatives
[alternatives]: #alternatives

### Keep using static methods

We could consider using static operator methods where the instance to be mutated
is passed as the first parameter. In case of a value type, that parameter must be a `ref` parameter.
Otherwise, the method won't be able to mutate the target variable. At the same time, in case of a class
type, that parameter should not be a `ref` parameter. Because in case of a class, the passed in instance
must be mutated, not the location where the instance is stored. However, when an operator is declared
in an interface, it is often not known whether the interface will be implemented only by classes,
or only by structures. Therefore, it is not clear whether the first parameter should be a `ref` parameter.

## Design meetings

- [LDM-2025-02-12](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-02-12.md#user-defined-instance-based-operators)
- [LDM-2025-04-02](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-02.md#user-defined-compound-assignment-operators)
