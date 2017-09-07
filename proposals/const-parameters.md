# CONST_PARAMETERS

* [x] Proposed
* [x] Prototype: [In Progress](https://github.com/dotnetrt/roslyn/tree/features/constparameters)
* [ ] LDM Discussion
* [ ] Implementation: [Not Started]()
* [ ] Specification: [Not Started]()

## Summary
[summary]: #summary

Const Parameters proposal aims to provide missing in C# language synatx (and other .NET languages too) allowing to enforce passing of compile time constant values as method (delegates, constructors) parameters. It translates into formal proposal Issue #744 [Expand const keyword usage to enable compile time enforcement of const semantics](https://github.com/dotnet/csharplang/issues/744).

## Motivation
[motivation]: #motivation

An inability to implement several SIMD intrinsics functions which require compile time constant values passed as arguments due to processor instruction implementation with language syntax enforcing this invocation pattern was main motivation behind the proposal. In particular some of the x86 SIMD instructions require immediate parameters which are encoded as part of instruction and do not have variants accepting register or memory parameters i.e. pshufd, vpshufd, pshufhw, vpshufhw, pshuflw, vpshuflw. 

Encoding of `pshufd xmm1, xmm2/m128, imm8` where `imm8` is an immediate 8-bit wide integral parameter requires passing compile time constants to every invocation of runtime intrinsic function which is later translated to this instruction by Jit compiler. Corresponding intrinsics function is as follows:
```C#
public static Vector256<float> Shuffle(Vector256<float> value, Vector256<float> right, const byte control);
```
Obviously in world of .NET managed languages source code is compiled twice: (i) once by Roslyn language compiler which produces CIL assemblies (ii) which are later compiled during runtime by Jit compiler to native code. Therefore support of the Const Parameters feature requires support on both levels, however, this proposal is concerned only with support at C# and to some extent at CIL level and affects only Roslyn compiler and runtime interface to CIL. AOT scenarios supported by CoreRT are outside of the scope of this proposal as well.

Proposing Const Parameters feature due to direct need for compiler support of one of coding patterns for SIMD intrinsic implementation does not mean that developers will not find some other smart and creative uses for Const Parameters. 

More details regarding SIMD intrinsics cen be found under the following links:

[.NET Platform Dependent Intrinsics](https://github.com/dotnet/designs/blob/master/accepted/platform-intrinsics.md)

[Design Review: Hardware Intrinsics for Intel](https://github.com/dotnet/apireviews/blob/master/2017/08-15-Intel%20Intrinsics/README.md)

In particular part [C# language feature: Requiring parameters to be literals](https://github.com/dotnet/apireviews/blob/master/2017/08-15-Intel%20Intrinsics/README.md#c-language-feature-requiring-parameters-to-be-literals) contains relevant notes.

[Video: Design Review: Hardware Intrinsics for Intel](https://www.youtube.com/watch?v=52Fjrhx7pKU)
Discussion relevant for Const Parameters starts after 38:30

[API Proposal: Add Intel hardware intrinsic functions and namespace](https://github.com/dotnet/corefx/issues/22940)

## Detailed design
[design]: #detailed-design

Design is based on existing semantic meaning of the 'const' keyword where scope of it's use is extended to modifying parameters semantics. Parameter semantics is modified by 'const' modifier in a way that invocation of 'const' parameter containing function requires passing in the place of 'const' parameter 'const' keyword followed by constant expression (this can be achieved by using named arguments as well). Constant expression type must match parameter type or be implicitly convertible to it during compilation. Use of constant expression as argument value allows to easily access constant folding optimizations implemented in Roslyn. Supported 'const' parameter types are identical to the types supported by constant expressions: 

```
a constant expression must be the null literal or a value with one of the following types: 
sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, decimal, bool, 
object, string, or any enumeration type.
```
As a result for developers intuitively 'const' meaning is not changing besides the scope where 'const' keyword is syntactically valid. Additionally if 'const' keyword located before parameter type declaration is treated similarly to 'ref', 'out' and 'this' modifiers it's usage should be easy to understand for developers as well. 

Applying parameter modifiers restrictions to 'const' modifier would mean that it can not be used neither with other parameters modifiers nor with params keyword. Furthermore 'const' modifier should have identical effects to 'out' and 'ref' modifiers during overload resolution and all parameter modifiers except 'this' should be used identically during function invocation.


Design proposal is detailed below in the form of BNF syntax adn main points are as follows:
- 'const' keyword is used to modify parameter declaration;
- constant parameter declaration may be proceeded by attributes;
- constant parameter cannot have default value;
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

## Drawbacks
[drawbacks]: #drawbacks

None.

## Alternatives
[alternatives]: #alternatives

There are no reasonably fully functional alternatives using compile time type safety. It is possible to implement similar functionality using attributes and compiler / analyzer support but with inferior user experiance. 

## Unresolved questions
[unresolved]: #unresolved-questions

- Should Constant Parameter be treated differently from other parameters like 'ref' and 'out' parameters and consequently participate in overload resolution? (In my opinion YES)

- How Constant Parameter should be represented in CLI? (Current proposal `modreq`'s)

- Should Constant Parameter accept default argument? (In my opinion YES - not yet implemented in prototype)

- In which C# language / Visual Studio version it could be shipped? - ETA (Feature conditional support in prototype is a WIP which depends on this decision)

## Design meetings

None.


