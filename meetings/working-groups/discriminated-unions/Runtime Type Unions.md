# Runtime Type Unions

* [ ] Proposed
* [ ] Prototype: [Not Started](pr/1)
* [ ] Implementation: [Not Started](pr/1)
* [ ] Specification: [Not Started](pr/1)

## Summary

Runtime type unions are C# type unions built on top of support for type unions in the runtime. Each union has a closed "list" of case types.

They can be used without declaration via an anonymous type expression syntax:

```csharp
(int or string) x = 10;
```

Or declared with a name:

```csharp
union IntOrString(int, string);
```

Union specific case types can be succinctly declared if we adopt the "Case Classes" proposal:

```csharp
union Pet
{
    case Cat(...);
    case Dog(...);
}
```

Case types can be implicitly converted to the union type:

```csharp
(int or string) value = 10;
Pet pet = new Dog(...);
```

Or explicitly converted:

```csharp
Pet pet = (Pet)obj;  // convert from unknown to union
Dog dog = (Dog)pet;  // convert from union to case type
```

Patterns apply to the contents of a union. Switches on all cases types are exhaustive:

```csharp
var name = pet switch
{
    Cat cat => ...,
    Dog dog => ...
}
```

While there is no inheritance relationship between unions and case types, 
matching to a runtime union type succeeds when the value on the left
matches any of the case types.

```csharp
_ = obj is Pet;  // True if obj is Cat or Dog
```

## Motivation
To provide a canonical type union in C# that is also fully supported in the runtime and reflection,
so it works automatically in all contexts.

## Runtime Support
The runtime is aware of a special union type that is used only descriptively in metadata to describe the union, but is actually represented at runtime as a simple object reference. Certain IL operations are handled specially for values with this encoding to help guarantee that those values are compatible with one of the case types of the union.

### The Union Type
```csharp
System.Union<T1, T2>
```
The runtime is aware of a special abstract generic class `System.Union<T1, T2>`. 
This type can never be allocated, but it can be used in signatures, fields and local variables as a way to provide more information for a value that is ultimately just an object reference.

This type has two generic type parameters, both of which can either describe a case type or another union type when constructed.

For example, 
```csharp
Union<int, string>                  // int or string
Union<int, Union<string, double>>   // int or string or double
Union<Union<int, float>, Union<string, double>>  // int or float or string or double
```

In a sense, the runtime unravels the constructed union type to determine its set of case types. It uses this knowledge of the case types to implement specific IL instructions.

### Type Non-Equivalence
Union types constructed differently are different types.

```csharp
Union<int, string>  =/=   Union<string, int>
```
 However, there may be a conversion relationship between them.

### Type Conversion
When a union type is used as the target type of an `ISINST` or `CASTCLASS` IL instruction, these instructions succeed when the source runtime type would succeed on any one of the target union's case types, and fail when no case type would succeed.

When a union type is used as the static source type of an `ISINST` or `CASTCLASS` IL instruction, the union type is ignored and the runtime type is used always. (This is already how the runtime functions.)

The runtime is free to optimize away the actual type checks of the conversion when it knows that the source and target static types are compatible.

*Note: CASTCLASS succeeds when the source is a null value and target is a reference type or Nullable<T> type.*

### Named Sub Types
The union type `Union<T1, T2>` is allowed to have sub-types. 

```csharp
public sealed abstract class MyUnion : Union<int, string>;
```

These types must be abstract classes that declare no members, except for nested type declarations, and no type my derived from them (sealed abstract?).

These sub-types serve only as a means to give a union a name and to set it apart from other union types so they will be considered unique in signatures. However, the same assignment and conversion rules still apply.

#### Type Parameter Constraints
A type union type can be used in a type parameter constraint. However, it does not mean anything other than a constraint on normal sub typing. If you constrain a type parameter to a union type `Union<x,y>` then the type parameter must be that type or a sub-type of that type.

```csharp
    public abstract class IntOrString : Union<int, string>;

    public void M<T>() where T : Union<int, string> {...}

    M<Union<int, string>>();
    M<IntOrString>();
    M<int>(); // Error!
    M<string>(); // Error!
    M<(string, int)>(); // Error!
```

### Reflection
Reflection API assigning values to a field or parameter that is a union type will allow case types to be assigned (with runtime checks), as there is no way to actually have an instance of a union type.

```csharp
    void M(Union<int, string> value) {...};
    ...

    MethodInfo methodM = ...;
    method.Invoke(instance, new object []{ 10 });  // boxed int assignable to union
```

The methods `Type.IsAssignableFrom` and `Type.IsAssignableTo` will return true if a value of the source type is assignable to any of a target union's case types. If both source and target types are union types, the methods will return true if all the source union's case types are assignable to one of the target union's case types.

The runtime `Type` class will add the following members to help tools and libraries using reflection understand when a type is actually a union.

```csharp
    public class Type 
    {
        public bool IsUnion { get; }
        public Type[] GetUnionCaseTypes();
    }
```

## Language Support

### Anonymous Type Unions
A type expression exists to refer to a runtime union type without specifying a name. These anonymous unions unify across assemblies since they are all translated to the same generic system type.

#### Grammar
```csharp
anonymousUnionType := '(' <type> ('or' <type>)* ')';
```

#### Lowering
An anonymous type union is a synonym for an equivalent System.Union<T1,T2>.

|Union Syntax|Lowered Type|
|------|-------|
|`(A or B)` |`Union<A, B>` |
|`(A or B or C)` |`Union<A, Union<B, C>>` |
|`(A or B or C or D)` |`Union<A, Union<B, Union<C, D>>>`|

### Nominal Type Unions
A nominal type union exists to give a type union a name, since repeatedly writing anonymous union expressions for unions with multiple cases gets tedious after about three.

You declare one minimally using the `union` keyword, a name and a list of type references.

```csharp
union Pet (Cat, Dog, Bird, Fish, Turtle, Rabbit);
```

The nominal type union has no members other than those inherited from object, yet it may declared nested types.
Because of this, a nominal type union can declared its case types as part of the overall declaration. 

```csharp
union Pet(Cat, Dog)
{
    public record Cat(...);
    public record Dog(...);
    ...
}
```

If using the [Case Classes](#) proposal, there is no need to also specify the nested type in the type reference list, allowing the declaration to become simpler:

```csharp
union Pet
{
    case Cat(...);
    case Dog(...);
    ...
}
```

#### Grammar
```csharp
nominalUnionType :=   
    <attribute>* <accessibility>? 'union' <name> 
    (<type-parameter-list>)? 
    (<type-reference-list>)? 
    (<type-constraints>)* 
    ('{' <nested-type-declarations> '}' | ';')
```

#### Lowering
Nominal type unions are lowered to a declaration of a sub-type of the `System.Union<T1, T2>` type.

```csharp
public union Result<TValue, TError>(Success<TValue>, Failure<TError>);
```
is lowered to:
```csharp
public sealed abstract class Result<TValue, TError> : System.Union<Success<TValue>, Failure<TError>>;
```

#### Assignment
When the target of an assignment is a type union, and the source is assignable to one of the case types, the assignment can be made without an explicit conversion (cast).

When the source of an assignment is also a type union, and each of the source's case types can be assigned to any one of the target's case types, the assignment can be made without an explicit conversion.

```csharp
(int or string) x = 10;
(int or string or double) y = x;
int z = x; // error!
(int or double) = x; // error!
```

Regardless, the assignment is encoded as a conversion generating a `CASTCLASS` instruction, unless the [Runtime Assignable Conversion](#runtime-assignable-conversions) feature is implemented.

#### Conversion
C# is aware of type unions. Casts and pattern matching allow union types in both the source and target position, and all are handled by the same runtime IL as used today.

However, normally the C# compiler will report an error when the source of a cast or pattern match will never match the target type. This rule is modified for type unions.

When a union is the source of a cast or pattern match, no error is given if the target type is compatible with at least one of the source's case types.

```csharp
(int or string) x = ...;
var r = x switch 
{
    int value => ...,
    string value => ...,
    double value => ... // error! this will never match
}
```

When a union is the target of a cast or pattern match, no error is given if the source type is compatible with at least one of the target's case types.

```csharp
int x = ...;
var r = x switch 
{
    int value => ...,
    (int or string) => ...,
    (string or double) => ...  // error! this will never match
}
```

When both the source and target types are union types, no error is given if the target and source case types have at least on compatible combination.

```csharp
(int or string) x = ...;
var r = x switch 
{
    (string or int) => ...,
    (int or string or double) => ...,
    (string or double) => ...,
    (double or float) => ...  // error! will never match
}
```

#### Exhaustiveness
A `switch` with a source value that statically has a type union type is exhausted if all case types are handled in the switch. If the switch is exhausted, no default case need be specified.

```csharp
(int or string or double) x = ...
var r = x switch
{
    int value => ...,
    string value => ...,
    double value => ...
}
```

#### Nullability
When one of a type union's case types is a type that can be null, a nullable reference type or nullable value, the type union is considered nullable or may be null by the null type checker and may be assigned a null value.

```csharp
(int or string?) x = null;  // legal
```

In addition, a type union without a nullable case type may still be considered nullable if the type is referred to using the question-mark syntax or is inferred to be possibly null using the existing null type checker rules.

```csharp
(int or string)? x = null;  // legal
```

Specifying both is unnecessary, but also allowed.

```csharp
(int or string?)? x = null; // legal
```

However, even when a case type is declared as nullable, you cannot use a nullable type in a type pattern match. Yet, as with any reference type, when a type union is nullable it may be matched with a null pattern match. 

```csharp
(int or string)? x = ...;
if (x is null) {...}
```

A switch with a nullable type union must include a null pattern match in order to be considered exhausted.

```csharp
(int or string?) x = ...;
var r = switch
{
    int value => ...,
    string value => ...,
    null => ...
}
```

**Question:** Can you assign a value of a nullable variation of a case type to a nullable type union when the case type itself is not nullable?

```csharp
    int? v = ...;
    (int or string?) x = v; // Error?
```
How about now?
```csharp
    int? v = ...;
    (int or string)? x = v; // Error?
```

There is no actual violation of the type system. A value with this type may be null or not. However, the intuition may be that this is not possible for one or both.

Could maybe the first case be a warning, since you went out of your way to describe the case types and either being nullable or not? But the second sort of implies that any value could be nullable.

```csharp
    (int or string)?  <===>  (int? or string?)
```

**Question:** If type parameters are used as case types, without constraints that would imply nullability of the case type, should the type union be considered nullable?

```csharp
    void Method<T1, T2>()
    {
        (T1, T2) x = null; // allowed?

        var r = x switch 
        {
            T1 value => ...,
            T2 value => ...,
            null => ...   // necessary?
        }
    }
```

## Drawbacks

- No back-compat: Only works in newer runtimes and cannot be reasonably used via older language versions.
- Delay: Getting these features into a new runtime may take a long time.
- Boxing: No non-boxing solution.

## Optional Features
Optional features are stretch goals.

### Runtime Assignable Conversions

The runtime IL type checker is enhanced to understand the assignment rules without requiring a `CASTCLASS` instruction to convert between different (though compatible) union types.

1. A source value can be to a target with a union type if the source value's static type is assignable to at least one of the target union's case types.
2. A source value with a union type is assignable to a target with a different union type if each of the source's case types are assignable to at least one of the target's cast types.

### Type Parameter Union Constraints
To have a more meaningful constraint for type unions, a new kind of constraint must exist that constrains against the union case membership. This needs to be represented specially in metadata and would require different syntax in C#.

A new constraint operator exists C# that means union subtype. Instead of `:`, we use the `in` keyword.
The type parameter `T` matches if it has a `:` relationship with any one of the constraint's case types, or if it is a union itself then each case type must have a `:` relationship with one of the constraint's case types. If the constraint's type is not a union, then the `in` operator has the same meaning as the `:` operator.

```csharp
    record Animal;
    record Dog : Animal;
    class AnimalOrInt : Union<Animal, int>;

    void Method<T>() where T in (int or Animal) {...}
    
    Method<int>(); // okay
    Method<Animal>(); // okay
    Method<Dog>(); // okay
    Method<double>(); // Error!
    Method<Union<int, Animal>>(); // okay
    Method<Union<Animal, int>>(); // okay
    Method<Union<int, Dog>>(); // okay
    Method<AnimalOrInt>(); // okay
    Method<Union<int, double>>(); // Error!
```

This means T is constrained to be either an int or Animal.

If a generic type parameter is used as part of generic constraint of another type parameter, and that type parameter is constructed with a union type, the constraint checker sees that it is a union and applies the same rule (at whatever point this normally happens, loading, jit, e tc).

```csharp
    void Method<T, U>() where T in U {...}
    
    Method<int, (int or Animal)>(); // okay
    Method<Animal, (int or Animal)>(); // okay
    Method<Dog, (int or Animal)>(); // okay
    Method<double, (int or Animal)>(); // Error!
    Method<(int or Animal), (int or Animal)>(); // okay
    Method<(Animal or int), (int or Animal)>(); // okay
    Method<(int or Dog), (int or Animal)>(); // okay
    Method<AnimalOrInt, (int or Animal)>(); // okay
    Method<(int or double), (int or Animal)>(); // Error!

    Method<int, AnimalOrInt>(); // okay
    Method<(int or Animal), AnimalOrInt>(); // okay
```
