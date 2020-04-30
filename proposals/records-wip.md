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

In addition to the members declared in the class or struct body, a record type has the following additional members:

### Primary Constructor

A record type has a public constructor whose signature corresponds to the value parameters of the
type declaration. This is called the primary constructor for the type, and causes the implicitly
declared default class constructor, if present, to be suppressed. It is an error to have a primary
constructor and a constructor with the same signature already present in the class.

At runtime the primary constructor

1. executes the instance field initializers appearing in the class-body; and then
    invokes the base class constructor with no arguments.

1. initializes compiler-generated backing fields for the properties corresponding to the value parameters (if these properties are compiler-provided

### Properties

For each record parameter of a record type declaration there is a corresponding public property member whose name and type are taken from the value parameter declaration. If no concrete (i.e. non-abstract) property with a get accessor and with this name and type is explicitly declared or inherited, it is produced by the compiler as follows:

For a record struct or a record class:

* A public get-only auto-property is created. Its value is initialized during construction with the value of the corresponding primary constructor parameter. Each "matching" inherited abstract property's get accessor is overridden.

### Equality members

Record types produce synthesized implementations for the following methods:

* `object.GetHashCode()` override, unless it is sealed or user provided
* `object.Equals(object)` override, unless it is sealed or user provided
* `T Equals(T)` method, where `T` is the current type

`T Equals(T)` is specified to perform value equality such that `Equals` is
true if and only if all the instance fields declared in the receiver type
are equal to the fields of the other type.

`object.Equals` performs the equivalent of

```C#
override Equals(object o) => Equals(o as T);
```

## `with` expression

A `with` expression is a new expression using the following syntax.

```antlr
with_expression
    : switch_expression
    | switch_expression 'with' anonymous_object_initializer
```

A `with` expression allows for "non-destructive mutation", designed to
produce a copy of the receiver expression with modifications to properties
listed in the `anonymous_object_initializer`.

A valid `with` expression has a receiver with a non-void type. The receiver type must contain an accessible parameterless instance method called `Clone` whose return type must be the type of the receiver express type, or a base type thereof.

On the right hand side of the `with` expression is an `anonymous_object_initializer` with a
sequence of assignments with a compiler-generated record property of the receiver on the left-hand side of the
assignment, and an arbitrary expression on the right-hand side which is implicitly convertible to the type
of the left-hand side.

The evaluation of a `with` expression is equivalent to calling the `Clone` method exactly once,
and then setting the backing field of each record property in the argument list to its corresponding
expression, in lexical order, using the result of the `Clone` method as the receiver.
