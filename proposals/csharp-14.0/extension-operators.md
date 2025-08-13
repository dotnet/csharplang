# Extension operators

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

***Note:*** *Does not cover user-defined implicit and explicit conversion operators, which are not yet designed or planned.*

## Declaration
Like all extension members, extension operators are declared within an extension block:

``` c#
public static class Operators
{
    extension<TElement>(TElement[] source) where TElement : INumber<TElement>
    {
        public static TElement[] operator *(TElement[] vector, TElement scalar) { ... }
        public static TElement[] operator *(TElement scalar, TElement[] vector) { ... }
        public void operator *=(TElement scalar) { ... }
    }
}
```

Extension operator declarations generally follow the rules for non-extension [user-defined operators in the Standard](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1510-operators) as well as the soon-to-be-added [user-defined compound assignment operators](https://github.com/dotnet/csharplang/blob/main/proposals/user-defined-compound-assignment.md). 

Generally speaking, the rules pertaining to the *containing type* of the declaration instead apply to the *extended type*. 

A static extension operator for the extended type `T` must take at least one parameter of type `S`, where `S` and `T` are identity convertible.
Note, that, unlike for regular user-defined operators, ```Nullable<T>``` is not a valid type for `S`.

For operators that require pair-wise declaration, the two declarations are allowed to occur in separate extension blocks for extended types `S` and `T` respectively, as long as `S` and `T` are identity convertible and the extension blocks occur in the same static class.

Instance compound assignment and increment operators are supposed to mutate the instance.
Therefore, the following restrictions are applied for such extension operators:
- Receiver type must be known to be either a reference type or a value type. I.e. cannot be an unconstrained type parameter.
- If receiver type is a value type, the receiver parameter must be a 'ref' parameter.
- If receiver type is a reference type, the receiver parameter must be a value parameter.

Like other extension members, extension operators cannot use the `abstract`, `virtual`, `override` or `sealed` modifiers.

``` c#
public static class Operators
{
    extension(int[])
    {
        public static int[] operator +(int[] vector, int scalar) { ... }    // OK; parameter and extended type agree
        public static int operator +(int scalar1, int scalar2) { ... }      // ERROR: extended type not used as parameter type
        public static bool operator ==(int[] vector1, int[] vector2){ ... } // ERROR: '!=' declaration missing
        public static bool operator <(int[] vector1, int[] vector2){ ... }  // OK: `>` is declared below
    }
    extension(int[])
    {
        public static bool operator >(int[] vector1, int[] vector2){ ... }  // OK: `<` is declared above
    }
}
```

## Overload resolution

Like other extension members, extension operators are only considered if no applicable predefined or non-extension user-defined operators were found. The search then proceeds outwards scope by scope, starting with the innermost namespace within which the operator application occurs, until at least one applicable extension operator declaration is found. For compound assignment operators, this involves looking for first user-defined compound assignment operators and then, if none found, non-assignment operators, before moving on to the next scope.

At each scope, overload resolution works the same as other extension members:
- The set of operator declarations for the given name (i.e. operator) is determined
- Type inference is attempted for each, based on operand expressions
- Declarations that fail type inference are removed
- Declarations that are not applicable to the operands are removed
- If there are no remaining candidates, proceed to the enclosing scope (or, if looking for compound-assignment operators, the corresponding simple operator)
- Applying overload resolution between the candidate, select the unique best candidate, if it exists
- If no unique best candidate can be found, overload resolution fails with an ambiguity error.

Using `*` and `*=` declarations above:

``` c#
int[] numbers = { 1, 2, 3 };

var i = 2 * 3;       // predefined operator *(int, int)
var v = numbers * 4; // extension operator *(int[], int)
v *= 5;              // extension operator *=(int)
```

## Extension user-defined conditional logical operators

The following [restrictions](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12143-user-defined-conditional-)
are specified for regular user-defined operators:

>When the operands of `&&` or `||` are of types that declare an applicable user-defined `operator &` or `operator |`, both of the following shall be true, where `T` is the type in which the selected operator is declared:
>
>- The return type and the type of each parameter of the selected operator shall be `T`. In other words, the operator shall compute the logical AND or the logical OR of two operands of type `T`, and shall return a result of type `T`.
>- `T` shall contain declarations of `operator true` and `operator false`.

By analogy, the following restrictions are used for extension operators:
When the operands of `&&` or `||` are of types that declare an applicable user-defined extension `operator &` or
extension `operator |`, both of the following shall be true:

- The operator shall compute the logical AND or the logical OR of two operands of type `T`, and shall return a result of type `T`.
- The enclosing static class shall contain declarations of extension `operator true` and extension `operator false` applicable to
  an instance of type `T`.

 For example, these declarations satisfy the restrictions, given that lifted form of `operator &` is used:
 ``` c#
S1? s11 = new S1();
S1? s12 = new S1();
_ = s11 && s12;
  
public static class Extensions
{
    extension(S1)
    {
        public static S1 operator &(S1 x, S1 y) => x;
    }
    extension(S1?)
    {
        public static bool operator false(S1? x) => false;
        public static bool operator true(S1? x) => true;
    }
}

public struct S1
{}
```

## Use of extension operators in Linq Expression Trees

When an expression utilizing a user-defined operator is used in a lambda that is converted to a Linq Expression tree,
an expression node that compiler creates includes a `MethodInfo` pointing to the operator method.

For example:
``` c#
public class C1
{
    public static C1 operator +(C1 x, int y) => x;
}

public class Program
{
    static void Main()
    {
        Expression<System.Func<C1, C1>> x = (c1) => c1 + 1;
    } 
}
```

uses [Expression.Add(Expression left, Expression right, MethodInfo? method)](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression.add#system-linq-expressions-expression-add(system-linq-expressions-expression-system-linq-expressions-expression-system-reflection-methodinfo)) factory.

When the operator is an extension operator, a MethodInfo referring to the corresponding implementation method in the enclosing class will be used.

Note, `&&`/`||` operators utilizing extension operators will be blocked in Linq Expression trees due to limitations of
factory methods. The [issue](https://github.com/dotnet/runtime/issues/115674) illustrates the failure mode for the factory
methods.  

## Open design questions

### [Resolved] Should extension operators on Nullable of extended type be disallowed?

It is allowed to declare a regular user-defined operator on Nullable of containing type.
Such operator can be consumed on an instance of containing type, as well as on an 
instance of Nullable of containing type.
``` c#
S1? s1 = new S1();
s1 = +s1; // Ok
s1 = +s1.Value; // Ok

struct S1
{
    public static S1? operator +(S1? x) => x;
}
```

Similarly, it is allowed to declare an extension user-defined operator on Nullable of extended type. 
However, such operator can be consumed only on an instance of extended type. Consumption on an 
instance of Nullable of extended type is not allowed because a conversion from ```Nullable<T>``` to 
```T``` is not a valid extension receiver type conversion.
``` c#
S1? s1 = new S1();
s1 = +s1; // Error: no matching operator
s1 = +s1.Value; // Ok

struct S1;

public static class Extensions
{
    extension(S1)
    {
        public static S1? operator +(S1? x) => x;
    }
}
```

This makes such operator declarations somewhat useless, they won't ever be consumed on `null` instances,
and, therefore, no real reason to have nullable parameter types. Should declarations like this be disallowed?

[Resolution:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-06-04.md#should-extension-operators-on-nullable-of-extended-type-be-disallowed)
>Restriction accepted: extension operators can only be declared for the type being extended in an extension block, not for the nullable underlying type.

### [Resolved] Applicability of bitwise operators during evaluation of user-defined conditional logical operators

Language adds extra [restrictions](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12143-user-defined-conditional-)
to bitwise operators suitable for evaluation of user-defined conditional logical operators:

>When the operands of `&&` or `||` are of types that declare an applicable user-defined `operator &` or `operator |`, both of the following shall be true, where `T` is the type in which the selected operator is declared:
>
>- The return type and the type of each parameter of the selected operator shall be `T`. In other words, the operator shall compute the logical AND or the logical OR of two operands of type `T`, and shall return a result of type `T`.
>- `T` shall contain declarations of `operator true` and `operator false`.

However, the specification isn't clear whether the restriction should eliminate a candidate bitwise operator
as inapplicable when the requirements are not satisfied, or the requirements should be applied to the best
bitwise operator after overload resolution among the candidates is complete. Right now, compiler 
implements the latter, which leads to the following behavior:
``` c#
 S1 s1 = new S1();
 S2 s2 = new S2();
        
 // error CS0217: In order to be applicable as a short circuit operator
 //               a user-defined logical operator ('S1.operator &(S1, S2)')
 //               must have the same return type and parameter types
 _ = s1 && s2;
 
struct S1
{
    // If this operator is removed, candidate from S2 is successfully used
    public static S2 operator &(S1 x, S2 y) => y;
}

struct S2
{
    public static S2 operator &(S2 x, S2 y) => y;
    public static bool operator true(S2 x) => false;
    public static bool operator false(S2 x) => true;
    
    public static implicit operator S2(S1 x) => default;
}
```

Here is another example that could benefit from the former approach:
``` C#
S1 s1 = new S1();
S2 s2 = new S2();

// error CS0034: Operator '&&' is ambiguous on operands of type 'S1' and 'S2'
_ = s1 && s2;

struct S1
{
    // If this operator is removed, candidate from S2 is successfully used
    public static S1 operator &(S1 x, S1 y) => y;
    public static implicit operator S1(S2 x) => default;
}

struct S2
{
    public static S2 operator &(S2 x, S2 y) => y;
    public static bool operator true(S2 x) => false;
    public static bool operator false(S2 x) => true;
    
    public static implicit operator S2(S1 x) => default;
}
```

The precise applicability rules are even more important for extension operators. Extension operators are considered
only when there are no applicable regular user-defined operators. And, only when there are no applicable
extension operators in the given extension scope, candidates from next extension scope are considered. 

Hence the question, should the restrictions be part of a candidate applicability check during overload 
resolution rather than a post validation after the overload resolution is complete?

[Resolution:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-06-04.md#applicability-of-bitwise-operators-during-evaluation-of-user-defined-conditional-logical-operators)
> We're not doing anything here for now. Rejected until we see use cases.

### [Resolved] Extension user-defined conditional logical operators

The following [restrictions](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#12143-user-defined-conditional-)
are specified for regular user-defined operators:

>When the operands of `&&` or `||` are of types that declare an applicable user-defined `operator &` or `operator |`, both of the following shall be true, where `T` is the type in which the selected operator is declared:
>
>- The return type and the type of each parameter of the selected operator shall be `T`. In other words, the operator shall compute the logical AND or the logical OR of two operands of type `T`, and shall return a result of type `T`.
>- `T` shall contain declarations of `operator true` and `operator false`.

By analogy, the following restrictions are proposed for extension operators:
When the operands of `&&` or `||` are of types that declare an applicable user-defined extension `operator &` or
extension `operator |`, both of the following shall be true:

- The operator shall compute the logical AND or the logical OR of two operands of type `T`, and shall return a result of type `T`.
- The enclosing static class shall contain declarations of extension `operator true` and extension `operator false` applicable to
  an instance of type `T`.

 For example, these declarations satisfy the restrictions, given that lifted form of `operator &` is used:
 ``` c#
S1? s11 = new S1();
S1? s12 = new S1();
_ = s11 && s12;
  
public static class Extensions
{
    extension(S1)
    {
        public static S1 operator &(S1 x, S1 y) => x;
    }
    extension(S1?)
    {
        public static bool operator false(S1? x) => false;
        public static bool operator true(S1? x) => true;
    }
}

public struct S1
{}
``` 

[Resolution:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-06-04.md#extension-user-defined-conditional-logical-operators)
> Proposal accepted.

### [Resolved] Extension compound assignment operators

Compound assignment operators are instance operators and are supposed to mutate the instance.
Therefore, the following restrictions are proposed for extension compound assignment operators:
- Receiver type must be known to be either a reference type or a value type. I.e. cannot be an unconstrained type parameter.
- If receiver type is a value type, the receiver parameter must be a 'ref' parameter.
- If receiver type is a reference type, the receiver parameter must be a value parameter.

[Resolution:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-06-04.md#extension-compound-assignment-operators)
> Restriction adopted, we can look at it again in the future when there is more bandwidth.

### [Resolved] Dynamic evaluation

Extension operators are not used by dynamic evaluation. This might lead to compile time errors.
For example:
``` c#
dynamic s1 = new object();
var s2 = new object();

// error CS7083: Expression must be implicitly convertible to Boolean or its type 'object' must define operator 'false'.
_ = s2 && s1;

public static class Extensions
{
    extension(object)
    {
        public static object operator &(object x, object y) => x;
        public static bool operator false(object x) => false;
        public static bool operator true(object x) => throw null;
    }
}
```

An attempt to do a compile time optimization using non-dynamic static type of 's2' ignores true/false extensions.
One might say this is desirable because runtime binder wouldn't be able to use them as well.

[Resolution:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-06-11.md#dynamic-resolution-of-operator-truefalse)
Restriction accepted. Extension operator `true`/`false` are not used for `dynamic` `&&` or `||`.

### [Resolved] Use of extension operators in Linq Expression Trees

When an expression utilizing a user-defined operator is used in a lambda that is converted to a Linq Expression tree,
an expression node that compiler creates includes a `MethodInfo` pointing to the operator method.

For example:
``` c#
public class C1
{
    public static C1 operator +(C1 x, int y) => x;
}

public class Program
{
    static void Main()
    {
        Expression<System.Func<C1, C1>> x = (c1) => c1 + 1;
    } 
}
```

uses [Expression.Add(Expression left, Expression right, MethodInfo? method)](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression.add#system-linq-expressions-expression-add(system-linq-expressions-expression-system-linq-expressions-expression-system-reflection-methodinfo)) factory.

The question is what should we do when the operator is an extension operator.
We cannot use a MethodInfo referencing the extension operator itself because IL is not allowed
to refer to any declaration from an extension block.

Proposal: Use MethodInfo referring to a corresponding implementation method in the enclosing class.
          A quick smoke test confirmed that an expression tree like that can be compiled, executed,
          and execution calls the implementation method.

[Resolution:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-06-11.md#extension-operators-in-linq-expression-trees)
Accepted.

### [Resolved] Is the rule "an extension operator may not have the same signature as a predefined operator." worth having as specified?

If our goal is to prevent users from declaring an operator that would be shadowed by a predefined operator,
then it doesn't look like the rule as specified achieves it.
For example, the rule is going to prevent a user from defining ```operator –(int x)``` and ```operator –(long x)```,
but it is not going to prevent declaration of ```operator –(byte x)```. However, in the following code it would be
shadowed by predefined ```operator –(int x)```.
``` c#
byte x = 0;
var y = -x;
```

Perhaps the rule should be changed to something like the following instead?
> If operator overload resolution with an argument list consisting of expressions with types
> matching declared parameters in the same order against the set of predefined operators of
> the same kind succeeds, then the signature of declared extension operator is considered illegal. 

For ```operator –(byte x)```, we would perform an operator overload resolution with an argument list
[byte] against predefined unary `-` operators. It would succeed with predefined ```operator –(int x)``` as 
the result. Therefore, an error would be reported for extension ```operator –(byte x)```.

For ```operator +(byte x, byte y)```, we would perform an operator overload resolution with an argument list
[byte, byte] against predefined binary `+` operators. It would succeed with predefined ```operator +(int x, int y)``` as 
the result. Therefore, an error would be reported for extension ```operator +(byte x, byte y)```.

When considering signatures of instance compound assignment operators, the receiver parameter is going to
contribute to the argument list. For ```extension(byte).operator+=(short)```, we would perform an operator overload
resolution with an argument list [byte, short] against predefined binary `+` operators. It would succeed with
predefined ```operator +(int x, int y)``` as the result. Therefore, an error would be reported for
```extension(byte).operator+=(short)```.

[Resolution:](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-06-11.md#built-in-operator-protection-rules)
Rule is abandoned. We will not have built-in errors or warnings here.

## Design meetings

- https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-06-04.md
- https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-06-11.md
