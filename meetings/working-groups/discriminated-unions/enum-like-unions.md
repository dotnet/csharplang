# Enum-like unions

## Summary

**Enum-like unions** are "enum-like" in the same sense that field-like events are "field-like": They are introduced by the same keyword, and to the consumer they are the same kind of thing (unions/events), but their body uses an alternative, terser, syntax that is similar to another kind of declaration (enums/fields):

```csharp
public union Gate { Locked, Closed, Open(float percent) }
```

## Motivation

For straightforward "discriminated union" scenarios, it is desirable to have a very terse syntax for declaring fresh case types, and it is helpful to lean into enum syntax (curly braces, simple names, commas) to manifest the analogy. Both these claims are borne out by other programming languages.

However, to a consumer such unions aren't observably different from ones declared with type references, just like field-like events aren't observably different from other events. Sharing the `union` keyword makes the shared semantics clear. 

Also, this proposal leaves open a path to unify "struct-like" and "enum-like" declarations in the future.

## Detailed design

```antlr
union_declaration
    : attributes? struct_modifier* 'partial'? 'union' identifier type_parameter_list? union_declarator
    ;

union_declarator
    : struct_like_union_declarator
    | enum_like_union_declarator
    ;

struct_like_union_declarator
    : '(' type (',' type)* ')' type_parameter_constraints_clause* 
      (`{` struct_member_declaration* `}` | ';')
    ;
    
enum_like_union_declarator
    : type_parameter_constraints_clause* `{` enum_like_union_member_list `}`
    ;

enum_like_union_member_list
    : enum_like_union_member (',' enum_like_union_member)* (`,`)?
    ;

enum_like_union_member
    : identifier (`(` parameter_list? `)`)?
    ;
```

Note that the `struct_like_union_declarator` shown here just reflects the current plan of record, but could change as part of other decisions. It's exact shape is not part of this proposal.

### Semantics

Enum-like unions are translated into struct-like unions, where enum-like union members are translated into nested record declarations (with primary constructor parameter lists if they contain parameter lists `(...)`) and added to the resulting unions case type list.

### Examples

```csharp
public union Gate { Locked, Closed, Open(float percent) }

union Pet
{
    Cat(string Name, string Personality),
    Dog(string Name, bool Friendly),
    Bird(string Name, string Species),
    None
}

public union Option<T>
{
    None,
    Some(T value),
}
```


## Drawbacks

- Is it too subtle that the presence or absence of a list of case type references determines whether the `{...}` body is enum-like or struct-like?

- Does the `enum` keyword need to be present to stress the analogy to enums?

- Like enums, enum-like unions cannot declare struct members such as function members or nested types. Their body is reserved for case members.

- Types declared as enum-like union members cannot declare their own bodies. This represents quite a cliff, as doing so for even one member requires the whole union to be rewritten as a struct-like union.

## Alternatives

- Use other keywords or additional keywords to further differentiate an enum-like union declaration from a struct-like one.

- Allow the `enum_like_union_member_list` to be followed by a `;` and a list of `struct_member_declaration`s so that enum-like unions also can have e.g. function members.
    - This could also be allowed in actual enum declarations, maintaining the analogy.
    
- Fully unify struct-like and enum-like declarations by allowing both a list of case type references and a list of enum-like union members in the same union declaration. This would be a superset of the proposal, but would go against the current decision to keep the two kinds of union declarations separate.

## Open questions

None.

