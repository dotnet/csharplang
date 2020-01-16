# Records Work-in-Progress

Unlike the other records proposals, this is not a proposal in itself, but a work-in-progress designed to record consensus design
decisions for the records feature. Specification detail will be added as necessary to resolve questions.

The syntax for a record is proposed to be added as follows:

```antlr
class_declaration
    : attributes? class_modifiers? 'partial'? 'class' identifier type_parameter_list?
      parameter_list? type_parameter_constraints_clauses? class_body
    ;

struct_declaration
    : attributes? struct_modifiers? 'partial'? 'struct' identifier type_parameter_list?
      parameter_list? struct_interfaces? type_parameter_constraints_clauses? struct_body
    ;

class_body
    : '{' class_member_declarations? '}'
    | ';'
    ;

struct_body
    : '{' struct_members_declarations? '}'
    | ';'
    ;
```

The `attributes` non-terminal will also permit a new contextual attribute, `data`.

A class (struct) declared with a parameter list or `data` modifier is called a record class (record struct), either of which is a record type.

It is an error to declare a record type without both a parameter list and the `data` modifier.

## Members of a record type

In addition to the members declared in the class-body, a record type has the following additional members:

### Primary Constructor

A record type has a public constructor whose signature corresponds to the value parameters of the
type declaration. This is called the primary constructor for the type, and causes the implicitly
declared default constructor to be suppressed. It is an error to have a primary constructor and
a constructor with the same signature already present in the class.
At runtime the primary constructor 

1. executes the instance field initializers appearing in the class-body; and then
    invokes the base class constructor with no arguments.

1. initializes compiler-generated backing fields for the properties corresponding to the value parameters (if these properties are compiler-provided; see [Synthesized properties](#Synthesized Properties)); then


[ ] TODO: add base call syntax and specification about choosing base constructor through overload resolution

### Properties

For each record parameter of a record type declaration there is a corresponding public property member whose name and type are taken from the value parameter declaration. If no concrete (i.e. non-abstract) property with a get accessor and with this name and type is explicitly declared or inherited, it is produced by the compiler as follows:

For a record struct or a record class:

* A public get-only auto-property is created. Its value is initialized during construction with the value of the corresponding primary constructor parameter. Each "matching" inherited virtual property's get accessor is overridden.

