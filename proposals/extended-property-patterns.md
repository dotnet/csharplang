# Extended property patterns

## Summary
[summary]: #summary

Allow property subpatterns to reference nested members.

## Motivation
[motivation]: #motivation

When you want to match a child property, nesting another recursive pattern adds too much noise which will hurt readability with no real advantage.

## Detailed design
[design]: #detailed-design

The pattern syntax is modified as follow:

```diff
property_pattern
  : type? property_pattern_clause simple_designation?
  ;

property_pattern_clause
  : '{' (subpattern (',' subpattern)* ','?)? '}'
  ;

subpattern
- : identifier ':' pattern
+ : subpattern_name ':' pattern
  ;

+subpattern_name
+ : identifier 
+ | subpattern_name '.' identifier
+ ;
```

The receiver for each name lookup is the stripped type of the previous member starting from the *input type* of the *property_pattern*.

For example, a pattern of the form `{ Prop1.Prop2: pattern }` is exactly equivalent to `{ Prop1: { Prop1: pattern } }`.

Note that this will include the null check for `Nullable<T>` values as it is the case for the expanded form, so we only see the underlying type's members when we dot off of a name in a *property_ pattern*.

Repeated member paths are allowed. Under the hood, such member accesses are simplified to be evaluated once.
