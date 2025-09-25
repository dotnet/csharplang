# Enums and unions

## Abstract

This proposal separates two high-level concepts into two separate syntaxes: `enum` and `union`. Unions are a type declaration that represent the union of existing types. Enums are a compositional type syntax. They allow declaring a sequence of nested types, each of which are unioned into the parent type. This is also known as an "algebraic data type". Union declarations take a list of existing types as input, while the enhanced enum syntax takes a list of type names, parameter lists, and bodies.

## Example

Union declaration:

```C#
union StringOrInt
{
  string,
  int
}
```

Enum declaration:

```C#
enum JsonValue
{
  Number(double value),
  Bool(bool Value),
  String(string Value),
  Object(ImmutableDictionary<string, JsonValue> Members),
  Array(ImmutableArray<JsonValue> Elements),
  Null
}
```

## Syntax

Expanded enum:
```bnf
<type-declaration> ::= "enum" <type-name> "{" <variant-list> "}"
<variant-list>     ::= <variant> { "," <variant> } [ "," ]
<variant>          ::= <variant-name> [ "(" <field-list> ")" ]
<field-list>       ::= <field> { "," <field> }
<field>            ::= <type> <field-name>
<type>             ::= <simple-type> | <generic-type>
<simple-type>      ::= "double" | "bool" | "string" | <type-name>
<generic-type>     ::= <type-name> "<" <type-arguments> ">"
<type-arguments>   ::= <type> { "," <type> }
<type-name>        ::= identifier
<variant-name>     ::= identifier
<field-name>       ::= identifier
```

Union:
```bnf
<union-declaration> ::= "union" <type-name> "{" <union-type-list> "}"
<union-type-list>   ::= <type> { "," <type> } [ "," ]
<type>              ::= <simple-type> | <generic-type>
<simple-type>       ::= "double" | "bool" | "string" | "int" | <type-name>
<generic-type>      ::= <type-name> "<" <type-arguments> ">"
<type-arguments>    ::= <type> { "," <type> }
<type-name>         ::= identifier
```

## Semantics

An enum has the same semantics as declaring all the cases as nested records, then making a union e.g.,

```C#
union JsonValue
{
  Number,
  Bool,
  String,
  Object,
  Array,
  Null;

  record Number(double value);
  record Bool(bool Value);
  record String(string Value);
  record Object(ImmutableDictionary<string, JsonValue> Members);
  record Array(ImmutableArray<JsonValue> Elements);
  record Null;
}
```

## Expansion options

The above grammar doesn't provide more than the minimal set of syntax. There are a few small additions that could be easily made.

### Allow extra members after a semicolon

In both enums and unions, after a semicolon additional members could be provided. Fields. auto-properties, and any other syntax that create additional fields would be prohibited.

## Allow extra members in a partial declaration

All enums and unions would have a so-called "primary" declaration that lists the union/enum members. Other partial declarations could be allowed, which would allow extra members. Again, no fields would be allowed.

## Allow record bodies in enums

Enums could be expanded to allow each declaration to have a body, e.g.

```C#
enum JsonValue
{
  Number(double Value)
  {
    public override string ToString() => Value.ToString();
  },
  Bool(bool Value)
  {
    public override string ToString() => Value.ToString();
  },
  String(string Value)
  {
    public override string ToString() => Value.ToString();
  },
  Object(ImmutableDictionary<string, JsonValue> Members)
  {
    public override string ToString() => Value.ToString();
  },
  Array(ImmutableArray<JsonValue> Elements)
  {
    public override string ToString() => Value.ToString();
  },
  Null
  {
    public override string ToString() => "null";
  }
}
```

If partial declarations of enums are allowed, the type members could also themselves be partial.
