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
      record_parameters? struct_interfaces? type_parameter_constraints_clauses? struct_body
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
