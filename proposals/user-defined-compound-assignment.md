# User Defined Compound Assignment Operators

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
and then assigns its return value to the local variable `c1`. Note that operator implementation has to allocate
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
-   : '+' | '-' | logical_negation_operator | '~' | '++' | '--' | 'true' | 'false'
+   : '+' | '-' | logical_negation_operator | '~' | 'true' | 'false'
    ;

binary_operator_declarator
    : type 'operator' overloadable_binary_operator
        '(' fixed_parameter ',' fixed_parameter ')'
    ;

overloadable_binary_operator
    : '+'  | '-'  | '*'  | '/'  | '%'  | '&' | '|' | '^'  | '<<' 
    | right_shift | '==' | '!=' | '>' | '<' | '>=' | '<='
    ;

conversion_operator_declarator
    : 'implicit' 'operator' type '(' fixed_parameter ')'
    | 'explicit' 'operator' type '(' fixed_parameter ')'
    ;

+increment_operator_declarator
+   : type 'operator' overloadable_increment_operator '(' fixed_parameter? ')'
+   ;

+overloadable_increment_operator
+   : '++' | '--'
+    ;

+compound_assignment_operator_declarator
+   : type 'operator' overloadable_compound_assignment_operator
+       '(' fixed_parameter ')'
+   ;

+overloadable_compound_assignment_operator
+   : '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<='
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

### Unary operators
[unary-operators]: #unary-operators

### Binary operators
[binary-operators]: #binary-operators

### Conversion operators
[conversion-operators]: #conversion-operators

### Increment operators
[increment-operators]: #increment-operators

### Compound assignment operators
[compound-assignment-operators]: #compound-assignment-operators


## Drawbacks
[drawbacks]: #drawbacks

Why should we *not* do this?

## Alternatives
[alternatives]: #alternatives

What other designs have been considered? What is the impact of not doing this?

## Open questions
[open]: #open-questions

What parts of the design are still undecided?

