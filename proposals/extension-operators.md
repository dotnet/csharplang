# Extension operators

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

A static extension operator for the extended type `T` must take at least one parameter of type `S` or `S?`, where `S` and `T` are identity convertible.

For operators that require pair-wise declaration, the two declarations are allowed to occur in separate extension blocks for extended types `S` and `T` respectively, as long as `S` and `T` are identity convertible and the extension blocks occur in the same static class.

Like other extension members, extension operators cannot use the `abstract`, `virtual`, `override` or `sealed` modifiers.

An extension operator may not have the same signature as a predefined operator.

``` c#
public static class Operators
{
    extension(int[])
    {
        public static int[] operator +(int[] vector, int scalar) { ... }    // OK; parameter and extended type agree
        public static int operator +(int scalar1, int scalar2) { ... }      // ERROR: extended type not used as parameter type
                                                                            // and ERROR: same signature as predefined +(int, int)
        public static bool operator ==(int[] vector1, int[] vector2){ ... } // ERROR: '!=' declaration missing
        public static bool operator <(int[] vector1, int[] vector2){ ... }  // OK: `>` is declared below
    }
    extension(int[])
    {
        public static bool operator >(int[] vector1, int[] vector2){ ... }  // OK: `<` is declared above
    }
}
```

**Open question:** Should we disallow user-defined extension operators where the types of all parameters and receivers are type parameters? Otherwise specific instantiations could have the same signature as a predefined operator.

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

## Open design questions

### Should extension operators on Nullable of extended type be disallowed?

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

