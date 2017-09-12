# CONST_PARAMETERS

* [x] Proposed
* [x] Prototype: [In Progress](https://github.com/dotnetrt/roslyn/tree/features/constparameters)
* [ ] LDM Discussion
* [ ] Implementation: [Not Started]()
* [ ] Specification: [Not Started]()

## Summary
[summary]: #summary

Const Parameters proposal aims to provide missing in C# language synatx (and other .NET languages too) allowing to enforce passing of compile time constant values as a method, delegate, constructor, lambda expression, anonymous and local method parameters. It translates into formal proposal Issue #744 [Expand const keyword usage to enable compile time enforcement of const semantics](https://github.com/dotnet/csharplang/issues/744).

## Motivation
[motivation]: #motivation

An inability to implement several SIMD intrinsics functions which require compile time constant values passed as arguments due to processor instruction implementation with language syntax enforcing this invocation pattern was main motivation behind the proposal. In particular some of the x86 SIMD instructions require immediate parameters which are encoded as part of instruction and do not have variants accepting register or memory parameters i.e. `pshufd, vpshufd, pshufhw, vpshufhw, pshuflw, vpshuflw`. 

Encoding of `vpshufd ymm1, ymm2/m256, imm8` where `imm8` is an immediate 8-bit wide integral parameter requires passing compile time constants to every invocation of intrinsic function which is later translated to required by processor `vpshufd` instruction encoding by Jit compiler. Corresponding intrinsics function signature is as follows:
```C#
public static Vector256<float> Shuffle(Vector256<float> value, Vector256<float> right, const byte control);
```
Obviously in world of .NET managed languages source code is compiled twice (except for AOT scenarios): (i) once by Roslyn language compiler which produces CIL assemblies (ii) which are later compiled during runtime by Jit compiler to native code. Therefore, support of the `const parameters` feature requires support on both levels, however, this proposal is concerned only with support for C# language and to some extent with support at CIL level and affects only Roslyn compiler and runtime interface to CIL. Runtime support for `const parameters` and AOT scenarios supported by CoreRT are outside of the scope of this proposal.

Proposing `const parameters` feature due to direct need for compiler support of one of coding patterns needed for SIMD intrinsic implementation does not mean that developers will not find some other smart and creative uses for `const parameters`. 

More details regarding SIMD intrinsics cen be found under the following links:

[.NET Platform Dependent Intrinsics](https://github.com/dotnet/designs/blob/master/accepted/platform-intrinsics.md)

[Design Review: Hardware Intrinsics for Intel](https://github.com/dotnet/apireviews/blob/master/2017/08-15-Intel%20Intrinsics/README.md)

In particular part [C# language feature: Requiring parameters to be literals](https://github.com/dotnet/apireviews/blob/master/2017/08-15-Intel%20Intrinsics/README.md#c-language-feature-requiring-parameters-to-be-literals) contains relevant notes.

[Video: Design Review: Hardware Intrinsics for Intel](https://www.youtube.com/watch?v=52Fjrhx7pKU)
Discussion relevant for Const Parameters starts after 38:30

[API Proposal: Add Intel hardware intrinsic functions and namespace](https://github.com/dotnet/corefx/issues/22940)

## Detailed design
[design]: #detailed-design

Design is based on existing semantic meaning of the C# `const keyword` with usage scope extended to to context dependent modification of parameters declaration semantics. Allowing for modification of parameter declaration by proceeding it with `const keyword` requiring that invocation of `const parameters` containing function requires passing in the place of `const parameters` 'const' keyword followed by `constant expression`.

```C#
// Declaration
public void Method(const byte value);

// Usage
var x = Foo();
this.Method(const 1);           // OK  
this.Method(const 3 * 2 << 1);  // OK
this.Method(const x);           // Error - x is not a compile time constant or constant expression
```

Constant expression type must match parameter type or be implicitly convertible to it during compilation. Use of constant expression as argument value allows to easily access constant folding optimizations implemented in Roslyn. Types allowed in `const parameters` declarations are identical to the types supported by constant expressions: 

```
a constant expression must be the null literal or a value with one of the following types: 
sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, decimal, bool, 
object, string, or any enumeration type.
```
Developers should intuitively grasp `const parameters` meaning as it does not change `const keyword` meaning except for the scope where `const keyword` is syntactically valid. Additionally since 'const' keyword located before parameter type declaration is treated similarly to 'ref', 'out' and 'this' modifiers it's new usage should be easy to understand as well.

```C#
// Declarations with const parameters
public delegate void DelegateWithConst(const string message);

class A
{
    public long Error { get; private set; }
    public long Warning { get; private set; }
    public event DelegateWithConst SendMessageEvent = (const string message) => 
    { 
        Console.Write(message); 
    }

    public ulong Method(const byte value)
    {
        return LocalMethod(const 3 * 19);

        ulong LocalMethod(const uint localValue)
        {
            return value * localValue << 8;
        }
    }

    public A(const long errorId)
    {
        Error = errorId;
    }

    public A(long warningId) 
    {
        Warning = warningId;
    }
}

// Usage
int i = Foo();
var x = new A(i);               // OK 
var x = new A(2017);            // OK
x.Warning == 2017;              // True
x.Error == 2017;                // False

var y = new A(const i);         // Error - i is not const
var y = new A(const 2017);      // OK
y.Warning == 2017;              // False
y.Error = 2017;                 // True
``` 

Applying parameter modifiers restrictions to 'const' modifier would mean that it can not be used together with other parameters modifiers ('ref', 'out', 'this', 'params'). Furthermore 'const' modifier should have identical effects to 'out'/'ref' modifiers during overload resolution meaning that parameter with 'const' modifier will be resolved as a different from identical parameters declared without 'const' modifier or declared with 'ref'/'out' or 'this' modifiers. 'Const' parameter modifier should be used identically to 'ref'/'out' modifiers during function invocation by placing it before passed argument.

```C#

public class C
{
    public void TestConstant(ref int value) { return; }
    public void TestConstant(const int value) { return; }
    public void TestConstant(int value) { return; }
    public void M() { TestConstant(const 1); }              // Calls TestConst(const int value)
}
```

Design proposal is detailed below in the form of BNF syntax and main points are as follows:
- 'const' keyword is used to modify parameter declaration;
- constant parameter declaration may be proceeded by attributes;
- constant parameter can have default value;
- invocation of function with 'const' parameters requires passing 'const' keyowrd followed by constant expression
- 'const' parameter modifier similarly to 'ref' and 'out' modifiers participates in overload resolution
- 'const' argument value can not be modified inside function body  

```BNF
formal_parameter_list
    : fixed_parameters
    | fixed_parameters ',' parameter_array
    | parameter_array
    ;

fixed_parameters
    : fixed_parameter (',' fixed_parameter)*
    ;

fixed_parameter
    : attributes? parameter_modifier? type identifier default_argument?
    | const_parameter
    ;

default_argument
    : '=' expression
    ;
    
const_parameter
    : attributes? 'const' type identifier
    ;

parameter_modifier
    : 'const'       // Introduces const contextual keyword modifing parameter
    | 'ref'
    | 'out'
    | 'this'
    ;

parameter_array
    : attributes? 'params' array_type identifier
    ;

invocation_expression
    : primary_expression '(' argument_list? ')'
    ;

argument_list
    : argument (',' argument)*
    ;

argument
    : argument_name? argument_value
    ;

argument_name
    : identifier ':'
    ;

argument_value
    : expression
    | 'const' const_expression
    | 'ref' variable_reference
    | 'out' variable_reference
    ;        
```

### Examples

```C#
// Method declaration with constant parameter
public static Vector256<float> Shuffle(Vector256<float> value, Vector256<float> right, const byte control);

// Method invocation with constant parameter
var result = Avx2.Shuffle(value1, value2, const ((1 << 3) | (1 << 7));
 ```

## Drawbacks
[drawbacks]: #drawbacks

None.

## Alternatives
[alternatives]: #alternatives

There are no reasonably fully functional alternatives which would be based on compile time type safety. It is possible to implement similar functionality using attributes and compiler / analyzer support but with inferior user experiance - errors during method invocation will be raised due to the fact that parameter is marked with attribute. As a consequence there will be no overload resolution due to parameters difference being an attribute or overload resolution would be based on parameters attribute differences. 

## Unresolved questions
[unresolved]: #unresolved-questions

- Should Constant Parameter be treated similarly like 'ref' and 'out' parameters and consequently participate in overload resolution?

- If above is true should invocation require passing 'const' keyword before value of const parameter? 
- How Constant Parameters should be represented in CLI? (Current proposal `modreq`'s)

- Should Constant Parameters accept default argument?

- Which member declarations should accept const parameters?

- Should const parameters not be allowed in operator overloads?

- In which C# language / Visual Studio version const parameters feature could be shipped?

## Design meetings

None.


