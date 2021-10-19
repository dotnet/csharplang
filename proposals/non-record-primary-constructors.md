# Primary constructors in non-record classes and structs

* [x] Proposed
* [ ] Prototype: Not started
* [ ] Implementation: Not started
* [ ] Specification: Not started

## Summary
[summary]: #summary

Primary constructors, currently only available on [record types](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#primary-constructor), will be generalized to non-record classes and structs. They have the following differences in behavior from primary constructors in records:

- Instead of public members, a private field is generated for each parameter of the primary constructor.
- If the field is unreferenced (as a field) within the body of the class or struct declaration, it is not emitted. (The parameter can still be used in e.g. initializers).
- No corresponding deconstructor is generated.

## Motivation
[motivation]: #motivation

The ability of a class or struct in C# to have more than one constructor provides for generality, but at the expense of some tedium in the declaration syntax, because the constructor input and the class state need to be cleanly separated.

Primary constructors put the parameters of one constructor in scope for the whole class to be used for initialization or directly as object state. The trade-off is that any other constructors must call through the primary constructor.

``` c#
public class C(int i, string s)
{
    public int I { get; set; } = i; // i used for initialization
    public string S // s used directly in function members
    {
        get => s;
        set => s = value ?? throw new NullArgumentException(nameof(X));
    }
}
```

## Detailed design
[design]: #detailed-design

*Note*: Any similarity with the specification of [primary constructors in records](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#primary-constructor) is entirely intentional.

Class and struct declarations are augmented to allow a parameter list on the type name and and argument list on the base class:

``` antlr
class_declaration
  : attributes? class_modifier* 'partial'? 'class' identifier type_parameter_list?
  parameter_list? class_base? type_parameter_constraints_clause* class_body ';'?
  ;
  
class_base
  : ':' class_type argument_list?
  | ':' interface_type_list
  | ':' class_type  argument_list? ',' interface_type_list
  ;  
  
struct_declaration
  : attributes? struct_modifier* 'partial'? 'struct' identifier type_parameter_list?
    parameter_list? struct_interfaces? type_parameter_constraints_clause* struct_body ';'?
  ;
```

It is an error for a `class_base` to have an `argument_list` if the enclosing `class_declaration` does not contain a `parameter_list`. At most one partial type declaration of a partial class or struct may provide a `parameter_list`. The parameters in the `parameter_list` must all be value parameters.

A class or struct with a `parameter_list` has an implicit public constructor whose signature corresponds to the value parameters of the type declaration. This is called the ***primary constructor*** for the type, and causes the implicitly declared parameterless constructor, if present, to be suppressed. It is an error to have a primary constructor and a constructor with the same signature already present in the type declaration.

At runtime the primary constructor

1. executes the instance initializers appearing in the class or struct body

2. invokes the base class constructor with the arguments provided in the `class_base` clause, if present

If a class or struct has a primary constructor, any user-defined constructor, except "copy constructor" must have an explicit `this` constructor initializer. 

Parameters of the primary constructor as well as members of the record are in scope within the `argument_list`
of the `class_base` clause and within initializers of instance fields or properties. Instance members would
be an error in these locations (similar to how instance members are in scope in regular constructor initializers today, but an error to use), but the parameters of the primary constructor would be in scope and useable and would shadow members. Static members would also be usable, similar to how base calls and initializers work in ordinary constructors today.

A warning is produced if a parameter of the primary constructor is not read.

Expression variables declared in the `argument_list` are in scope within the `argument_list`. The same shadowing rules as within an argument list of a regular constructor initializer apply.

For each parameter in the `parameter_list`, if the type declaration does not directly contain a property or field declaration of the same name as the parameter, and if any expression within the body of the type declaration would reference such a member, then a private field is implicitly declared with the same name and type as the parameter.

The field is initialized to the value of the corresponding primary constructor parameter. Attributes can be applied to the synthesized field by using `field:` targets for attributes syntactically applied to the corresponding record parameter.

Unlike primary constructors in records, no deconstructor is generated.



