# `allows` syntax for union declarations

## Summary

As compared to the current proposal, this proposal retains all aspects, but removes the parentheses in the declaration syntax:

```cs
union Pet(Cat, Dog);
```

And replaces them with the `allows` keyword (first introduced in C# 13):

```cs
union Pet allows Cat, Dog;
```

All features of the syntax in the current proposal are retained. The following example exercises more of the syntax:

```cs
[Description("Example")]
public partial union Pet<T> allows Cat<T>, Dog<T>
    where T : allows ref struct
{
    // members
}
```

## Motivation

The current syntax is a solid and workable syntax. While that is true, there are advantages that would be uniquely available with the `allows` syntax: syntactic feel and reception, and a path to coherence with closed hierarchies.

On syntactic feel, some LDM and community members have voiced concerns with commas-inside-parentheses for the syntax. Comma-separated items, especially within parens, convey a sense of carrying or passing _all_ of the items rather than just one of them. The syntax most closely resembles primary constructors based on its placement in the type declaration, but it gains nothing by this resemblence. `allows` communicates that this is a constraint, not a set of items which are all present together.

Closed hierarchies are also expected to need to list the allowed subtypes at the base declaration. This is so that exhaustiveness can be evaluated for a given expression involving a closed hierarchy without having to first bind all type declarations in the entire compilation. Java uses the keyword `permits` for the same purpose (<https://openjdk.org/jeps/409>). Thus, closed hierarchies could also benefit from this same syntax. This syntax even works well alongside primary constructors:

```cs
closed record Pet(int Feet) allows Cat, Dog;

record Cat(int Feet, ...) : Pet(Feet);
record Dog(int Feet, ...) : Pet(Feet);
```

It would feel coherent to have the exact same syntax meaning the exact same thing between unions and closed non-unions.

## Detailed design

All elements of the syntax retain the meaning they have under the current proposal, and can appear in all the same combinations. The only change is replacing the tokens `(`...`)` with the token `allows`.

### Base types

Thinking to the future, if unions ever supported base types such as implementing interfaces, it might look like this:

```cs
union Pet<T> allows Cat<T>, Dog<T>
    : ISomeInterface
    where T : allows ref struct
{
    // members
}
```

Putting everything on one line shows a possible downside where the `:` may appear strongly related to the last allowed type:

```cs
union Pet<T> allows Cat<T>, Dog<T> : ISomeInterface;
```

Where the original design's parentheses would clearly show that the `:` applies to `Pet<T>` and not to `Dog<T>`:

```cs
union Pet<T>(Cat<T>, Dog<T>) : ISomeInterface;
```

If we do not expect base types to be a large percentage of the use cases for the syntax, it may be wise not to optimize heavily around them. Both the `allows` syntax and the parenthesized syntax have aspects which may be initially misleading. Perhaps it could hit a sweet spot to optimize the syntax that will be seen more often.

## Specification

The grammar is updated to the following:

```antlr
union_declaration
    : attributes? struct_modifier* 'partial'? 'union' identifier type_parameter_list?
      ('allows' type (',' type)*)? type_parameter_constraints_clause*
      (`{` struct_member_declaration* `}` | ';')
    ;
```
