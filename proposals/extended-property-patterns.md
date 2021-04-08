# Extended property patterns

## Summary
[summary]: #summary

Allow property subpatterns to reference nested members, for instance:
```cs
if (e is MethodCallExpression { Method.Name: "MethodName" })
``` 
Instead of:
```cs
if (e is MethodCallExpression { Method: { Name: "MethodName" } })
```

## Motivation
[motivation]: #motivation

When you want to match a child property, nesting another recursive pattern adds too much noise which will hurt readability with no real advantage.

## Detailed design
[design]: #detailed-design

The [*property_pattern*](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/patterns.md#property-pattern) syntax is modified as follow:

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

The receiver for each name lookup is the type of the previous member *T0*, starting from the *input type* of the *property_pattern*. if *T* is a nullable type, *T0* is its underlying type, otherwise *T0* is equal to *T*.

For example, a pattern of the form `{ Prop1.Prop2: pattern }` is exactly equivalent to `{ Prop1: { Prop2: pattern } }`.

Note that this will include the null check when *T* is a nullable value type or a reference type. This null check means that the nested properties available will be the properties of *T0*, not of *T*.

Repeated member paths are allowed. Under the hood, such member accesses are simplified to be evaluated once.
