# Nominal Type Unions

## Summary

Named unions have a closed list of "case types":

``` c#
public union IntOrString(int, string);
```

Fresh case types can be succinctly declared if we adopt the "Case Declarations" proposal:

``` c#
public union Pet
{
    case Cat(...);
    case Dog(...);
}
```

Case types convert implicitly to the union type:

``` c#
Pet pet = dog;
```

Patterns apply to the *contents* of a union. Switches that handle all case types are exhaustive:

``` c#
var name = pet switch
{
    Cat cat => ...,
    Dog dog => ...,
    // No warning about missing cases
}
```

There is no "is-a" relationship between a union type and its case types:

``` c#
_ = obj is Pet; // True only if 'obj' is an actual boxed 'Pet'
```

## Motivation

- **Cost:** Forego runtime support and get 90% of the value for 10% of the cost.
- **Performance:** Eliminate more allocations now and in the future.
- **Simplicity:** Avoid introducing a new kind of runtime type relationship in e.g. generic code.
- **Flexibility:** Embrace custom unions and low allocation layout options in the future.

## Detailed design

### Syntax

A union declaration has a name and a list of case types.

``` antlr
type_union_declaration
    : attributes? struct_modifier* 'partial'? 'union' identifier type_parameter_list?
      '(' type (',' type)* ')' type_parameter_constraints_clause* 
      (`{` type_declarations `}` | ';')
    ;
```

Case types can be any type that converts to `object`, e.g., interfaces, type parameters, nullable types and other unions. It is fine for cases to overlap, and for unions to nest or be null.

### Lowering

``` c#
public union Pet(Cat, Dog){ ... }
```

Is lowered to:

``` c#
[Union]
public record struct Pet
{
    public object? Value { get; }
    
    public Pet(Cat value) => Value = value;
    public Pet(Dog value) => Value = value;
    
    ...
}
```

### Implicit conversion

A "union conversion" implicitly converts from a case type to the union. It is sugar for calling the union's constructor:

``` c#
Pet pet = dog;
// becomes
Pet pet = new Pet(dog);
```

If more than one constructor overload applies an ambiguity error occurs.

### Pattern matching

Except for the unconditional `_` and `var` patterns, all patterns on a union value get implicitly applied to its `Value` property:

``` c#
var name = pet switch
{
    Dog dog => dog.Name,   // applies to 'pet.Value'
    var p => p.ToString(), // applies to 'pet'
};
```

A pattern cannot _explicitly_ access a union's `Value` property. This is similar to `Nullable<T>`.

### Exhaustiveness

A `switch` expression is exhaustive if it handles all of a union's cases:

``` c#
var name = pet switch
{
    Dog dog => ...,
    Cat cat => ...,
    // No warning about non-exhaustive switch
};
```

### Nullability

The null state of a union's `Value` property is tracked normally, with these additions: 
- When none of the case types are nullable, the default state for `Value` is "not null" rather than "maybe null". 
- When a union constructor is called (explicitly or through a union conversion), the new union's `Value` gets the null state of the incoming value.

Switch exhaustiveness obeys normal nullability rules:

``` c#
Pet pet = GetNullableDog(); // 'pet.Value' is "maybe null"
var value = pet switch
{
    Dog dog => ...,
    Cat cat => ...,
    // Warning: 'null' not handled
}
```

### Nested unions

Unions can recursively have unions as case types:

``` c#
union Pet (Cat, Dog);
union Animal (Pet, Cow);
```

There is no "merging" of nested unions - in patterns or elsewhere. An `Animal` is never directly a `Cat`, but it might be a `Pet` that is a `Cat`:

``` c#
if (animal is Pet p && p is Cat c) ...
```

Or simply

```c#
if (animal is Pet and Cat c) ...
```

## Drawbacks

- *Merging:* Unlike runtime-supported unions, nested unions do not "merge". If the declaration is nested then so is the consuming code.
- *Runtime type relationships:* Some functionality, e.g. testing whether a value belongs to a union, requires helper methods.
- *Serialization:* Serialization frameworks may need to special case unions to roundtrip them correctly.
- *Generic specialization:* Unions, like other structs, put pressure on generic specialization in the current runtime.

## Optional features

- *Custom union types:* Generalize to a compiler pattern so custom union types can be manually authored.
- *Helper methods:* Generate helper methods to facilitate common union functionality such as testing union membership.
- *Alternative layouts:* Let the user request a non-boxing union layout, e.g. with a `struct` keyword. 
