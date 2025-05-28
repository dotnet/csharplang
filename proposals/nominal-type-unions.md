# Nominal Type Unions for C#

* [x] Proposed
* [ ] Prototype: [Not Started](pr/1)
* [ ] Implementation: [Not Started](pr/1)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

A type union is a type that can represent a single value from a closed and disjoint set of types declared separately. These types, known as *case types* in this proposal, are not required to be declared in a hierarchy or share a common base (other than object).

```csharp
    record Cat(...);   // the case types
    record Dog(...);
    record Bird(...);

    union Pet(Cat, Dog, Bird);  // the type union
```

Values of a case type can be assigned directly to a variable of a type-union type. However, since type unions can represent values of unrelated types, having a variable of one is similar to having a variable typed as object. Interacting with a type union instance typically first involves testing and accessing its value in the form of one of its case types.

```csharp
    Pet pet = new Dog(...);
    ...
    if (pet is Dog d) {...}
```

This proposal introduces a first class nominal type union to C# that is fundamentally a struct wrapper around a value. It is declared and used like any other type, yet, it is also treated specially in some situations to help create an illusion that it is the value and not the wrapper. 

```csharp
    union Pet(Cat, Dog, Bird);
```
becomes
```csharp
    readonly struct Pet 
    {
        public Pet(Cat value) {...}
        public Pet(Dog value) {...}
        public Pet(Bird value) {...}
        public object Value => ...;
    }
```

To treat it like its value, certain operations like casting and pattern matching are translated by the compiler to API calls on the type union instance. 
 
```csharp
    if (pet is Dog dog) {...}   
```
becomes
```csharp
    if (pet is { Value: Dog dog }) {...}
```

The proposal is broken down into a core set of features needed to make nominal type union's viable and an additional assortment of optional features that may improve performance or capability and can be conditionally implemented over time.

## Motivation
C# has been adopting functional programming concepts for many years as the language continues to evolve to support popular paradigms as they become mainstream, like the monadic behaviors of nullable types and sequence operators in LINQ and more recently tuples and records that focus on data over behavior and help shift development to a more immutable style.

Writing software using functional paradigms often utilizes coding practices that are opposite or inverted from object-oriented ones. Frequently data is not hidden in the way one might do with objects, where ideally only the contract of behavior is exposed via methods and polymorphism is handled by virtual dispatch. Instead, a fixed set of types are used to represent a well known and finite data model with elements that are themselves often polymorphic leading to the need to identify and handle cases explicitly in the code.

You see this happening in the real world in places where the veil of abstraction is lifted, such as when protocols and data models are defined for communicating and exchanging information between boundaries (like machines, processes and libraries) and beneath the veil where the stuff of algorithmic minutia takes place. In these situations behavior is not the contract, the data is.

When you are operating on a data model in this way it becomes necessary to be confident that you are handling all cases when a property or collection can optionally contain more than one kind of data. Having the language help you enforce and know when this is true is extremely valuable. Both are usually solved in programming languages using the concepts of *type unions* and *discriminated unions*.

Type unions allow you to specify a variable as being able to hold a value from a set of otherwise unrelated types instead of just one type. Values of other types are not allowed and using the variable typically requires testing and converting it to one of its known case types. 

Likewise, discriminated unions allow you to have a variable that holds different kinds of values depending on its given named state and then depending on which state the variable is in you can get access to just those associated values. They are kind of like type unions without the types. Yet, in a language like C#, these named states with their values are better expressed as records allowing discriminated unions to be built out of type unions.

The only way currently to have a variable hold values of more than one type in C# is to either give the variable the object type or to use a base type that all the case types share. Yet, using object does not allow the language to restrict what the variable contains and a base type, while better, still does not constrain it because the type itself does not describe the set of possible sub types and additional sub types can easily be declared elsewhere outside the scope of the original design.

C# needs type unions to enable a better style of programming when polymorphic data models are being operated on. It solves the problem of guaranteeing that a variable can hold a value from more than one type, but also from only a specific set of types, and helps inform you when all cases have been handled.

### Everyday Unions
Type unions can also be used for less grand purposes. While you might not have any published protocols or data models to operate on, simply being able to return a value that can be one of many types and have that documented strongly in the method's signature is a huge benefit. Both consumers of a method like this and the compiler will know right away what types need to be handled in the result. 

```csharp
    public union TestResult(double, string);

    // Possible values are obvious
    public TestResult RunTest(...) {...};
```

Likewise, a parameter using a type union can constrain the values passed in, that might otherwise require abundant overloading, or be used as a settable property that cannot even have an overload.

```csharp
    // otherwise would need N overloads to constrain input
    public void RegisterVisit(Pet patient, DateTime time, string reason, string vet);

    // cannot have overloads for properties
    public Pet Patient { get; set; }
```

Having a way to express this makes code easier to read and prove correct because there are less ways to provide invalid input and more ways to ensure that all cases are understood.

### Beyond Class Hierarchies
A shallow class hierarchy can sometimes be used as a substitute for a type union if there is a way for both the compiler and you to know the full set of sub-types being used as cases and you have the freedom to declare all these cases for just this purpose. A separate proposal known as *Closed Class Hierarchies* attempts to solve this. Yet, even when this is possible, nominal type unions are often a better solution.

When to use *nominal type unions* over *class hierarchies*.

- **To avoid allocations of the case types.**  

   *This is important when the cases may be constructed and consumed frequently, like with the Option and Result types.*

- **Some or all of the case types are declared elsewhere.**  

   *For example, you want to constrain a field to only double and string values. You don't get to redeclare these types. You could declare a hierarchy with special sub-types that wrap these values but that would no longer be a type union of the desired types and those wrapper types would require allocations too.*

- **The case types have other uses outside the union.**  

  *While it might make sense to declare Animal and Vehicle in the same hierarchy of ThingsInAutoAccident, it may be awkward to have the Animal type as part of that hierarchy when also using it in your MeatsOnTheMenu model.*
   
- **The need to handle only a subset of cases.**  

   *You have a hierarchy of animals but need to limit a variable to just pets. You might be able to introduce Pet as an additional abstract class wedged between Animal and the pet specific case types but sometimes a perfect taxonomy cannot be defined when multiple subsets are needed.*

- **The need for multiple unions of similar cases.**  

  *There is no multiple inheritance in C# and therefore no sharing of the same case types in different hierarchies. You would need to create multiple identical classes with different base classes instead with no easy way to convert between them.*


## Principles

Some foundational principles that guided this proposal.

- **A nominal type union represents a single value.**  

  *It holds a single value that can be one of its case types. Its not meant to have other instance fields or properties.*
  
- **A nominal type union is a distinct type from other type unions even when they share the same cases.**  

  *They are not interchangeable or structural. However, conversion between similar type-union types should be possible.*

- **A nominal type union instance behaves as if it were its value in some situations.**  

  *Some operations applied to the union instance are instead applied to the value of the union, as far as its meaning is well understood and it is practical to do so.*

- **A nominal type union is its own type.**  

  *The type is not erased. An instance of one might behave as if it were the value it wraps in some situations, but it is also its own instance distinct from the value everywhere else.*

- **A nominal type union is always meant to have a value of one of its case types.**  

  *The reality, however, is that a default or uninitialized state does exist that we pretend does not, leading to the need to define what happens when this occurs.*

- **A nominal type union's value is never meant to be null.**  

  *The reality, however, is that null values can still sneak in which leads us to need to define what happens when this occurs.*

- **A nominal type union's storage model is opaque.**  

  *The field(s) storing the value are hidden behind constructors and properties/methods allowing the storage model to vary depending on the cases and potentially improve over time.*

- **A nominal type union is exactly what you declare it to be.**  

  *A type union that includes cases that are other type unions (not recommended but could happen due to type argument cases) is not flattened into a union of the leaf cases. The values held for these cases are type union instances not the values they indirectly contain.*


## Declaration

A nominal type union is declared using the `union` keyword and minimally a name and a list of case types that are each declared elsewhere. 
```csharp
    union Pet(Cat, Dog, Bird);
```
Though rare, some nominal type unions may declare type parameters:
```csharp
    union Option<TValue>(Some<TValue>, None);
```

Or add additional members:
```csharp
    union Option<TValue>(Some<TValue>, None)
    {
        public bool HasValue => this is Some<TVale>;
    }
```

They may even declare their case types as nested declarations:
```csharp
    public union Option<TValue>(Some<TValue>, None)
    {
        public record struct Some(TValue Value);
        public record struct None();
    }
```

Others may choose to declare and implement interfaces for special circumstances:

 ```csharp
    public partial union Pet(Cat, Dog, Bird) : ICustomSerializable
    {
        void ICustomSerializable.SerializeTo(Stream stream) => ...;
    }
```

*These interfaces are elements of the union type and not related to the case types or the value of the union. The might be needed for interop with libraries or frameworks that require them.*

### Grammar
```
type_union_declaration
    : attributes? struct_modifier* 'partial'? 'union' identifier type_parameter_list?
      case_type_list? struct_interfaces? type_parameter_constraints_clause* struct_body
    ;
case_type_list
    : '(' case_type (',' case_type)* ')'
case_type
    : attributes? case_modifiers? type
case_modifiers
    : ???
```

### Modifiers
A nominal type union may have zero or more modifiers. However, only certain modifiers are allowed.
- Any accessibility modifier: public, private, internal, etc
- partial
- file

### Type Parameters
The nominal type union can have zero or more type parameters that can be used in member declarations, interface declarations and case type specifications.

```csharp
    union OneOrList<A>(A, IReadOnlyList<A>);
```

- Note: declaring case types that are just type parameters hides knowledge of the true types used at runtime from the compiler at compile time making it possible to end up with type unions at runtime that would typically be avoided like unions containing other type unions or unions with duplicate case types. It also reduces the ability of the compiler to optimize the storage layout of the data.

### Case Types
A nominal type union specifies a list of case types. These types determine the closed set of types that a value contained by the type union can have. They can be any type except for the ones that are excluded by producing errors.

The following conditions produce an error by the compiler:

- A case type is a pointer or ref type.  
  *These types cannot be boxed to object and accessed via Value property.*

- A case type is the containing union's type.  
  *Infinite storage recursion?*  

The following conditions produce a warning from the compiler. If found the author has likely made a mistake. The warning can be overridden with pragmas.

- A case type is a type-union type.  
  *The author likely intended to have the individual cases of this type listed instead.*

- A case type is specified more than once.  
  *This is a redundant case that will never be used in practice.*

- A case type subsumes another case type.  
  *The author probably does not realize the subsumption and redundancy of additional cases.*

- A case type is the object type.  
  *The object type not only subsumes all other cases, it does not constrain the values the union may have in any way, defeating the purpose of the union.*

- A case type is a nullable struct or reference type.  
  *Type Unions only have non-null values.*

### Base Type
A nominal type union cannot declare a base type, only interfaces.

If the intention of having a base type was to extend an existing type union with more cases, the way of doing this is to list the cases of the other union in this union's case type list. An optional [Combined Unions](#combined-unions) feature would make this easier.

### Interfaces
A nominal type union can declare any interfaces that it implements. These are interfaces of the union type and may have no relation to the interfaces implemented by the case types.

### Body
A nominal type union may declare additional members beyond the members generated by the compiler. These are member of the union type and may have no relation to the members of the case types.

- Any member that introduces an instance field is disallowed and will produce an error at compile time. A type union only represents a single value.

- Any constructor declared must defer to one of the compiler's generated constructors. These each behave like a record's generated primary constructor. *There may not be a reasonable use case for defining additional constructors.*

### Partial Type Unions
A nominal type union may have multiple partial declarations.

```csharp
public partial union Pet(Cat, Dog, Bird);
```
```csharp
public partial union Pet
{
    public bool HasFourLegs => ...;
}
```

- Each partial declaration follows the same rules as other types with respect to modifiers, type parameters and constraints.
- One and only one partial declaration includes the list of case types. The order of the case types may be significant and a change in order may be a breaking change.

## Basic Representation
The nominal type union is emitted as a read-only struct, with constructors corresponding to the case types and a property for accessing the contained value. All interfaces, members and attributes declared on the type union are included as part of the emitted struct type.

```csharp
    public union Pet(Cat, Dog, Bird);
```
Becomes
```csharp
    public readonly struct Pet
    {
        // constructors
        public Pet(Cat value) {...}
        public Pet(Dog value) {...}
        public Pet(Bird value) {...}

        // value access pattern
        public object Value => ...;
    }
```

### Construction
A constructor exists for each case type, initializing the type union instance with the argument value. These constructors are the minimum API necessary to create type union instances from case type values using language idioms such as casting or implicit coercion.
An optional API, outlined in [conditional construction](#conditional-construction) may also exist to enable construction of type unions from values that are not known to be case types.

#### Constructing with Nulls
 A nominal type union is not meant to have a null value. It would never match its case type in a type pattern match. The compiler warns when a type union is declared to include case types that can be null. However, it is still possible that a null value may be passed as an argument to a constructor by ignoring this warning or others. When this happens, the type union is initialized to the equivalent of its default state instead, indicating that it does not contain a value.

 The [Nullable Unions](#nullable-unions) section covers techniques for enabling null or non values in type unions.

### Access Patterns
A nominal type union exposes one or more methods or properties used to access the value contained by the union. These are referred to in this proposal as *access patterns*.

A nominal type union always has the [Value Access Pattern](#value-access-pattern), a single weakly-typed property that returns the value used to construct the type union. This is the minimum API required to translate type tests, pattern matching and explicit coercion, but not always the best way to access the value. If supported, other optional access patterns may be preferred.

Possible Access Patterns:  
- [Value Access Pattern](#value-access-pattern)
- [Discriminator Access Pattern](#discriminator-access-pattern)  
- [TryGet Access Pattern](#tryget-access-pattern)  
- [Generic TryGet Access Pattern](#generic-tryget-access-pattern)  

### Storage Layouts
Because the truth of how the value is stored within the nominal type union is hidden behind the facade of the access patterns it is possible to store the value in one of many possible and reasonable ways. For example, there may be a strongly-typed field for each possible case type or there may only be one field typed as object, shared by all the cases, boxing any struct types.

*Due to runtime limitations it is not possible to simply overlay the memory used by the different cases (like a C++ union would) so an alternative storage layout must be chosen that finds a balance between the footprint (the size of the type itself) and the potential use of heap allocation due to boxing.*

The compiler choses a layout that best suits the case types given a heuristic and possible hint by the author. In future versions of C#, the set of layouts known by the compiler may grow, the heuristic changed or the ability to specify preferences in the declaration may increase, meaning when the code is recompiled in the future the storage layout of the type may change.

By default, a nominal type unions uses the [Boxed Layout](#boxed-layout) which simply backs the `Value` property with a single field. If supported, other optional layouts may be preferred.

Possible Layouts:
- [Boxed Layout](#boxed-layout)
- [Discriminated Boxed Layout](#discriminated-boxed-layout)
- [Fat Layout](#fat-layout)
- [Skim Layout](#skim-layout)
- [Overlapped Layout](#overlapped-layout)
- [Deconstructed Layout](#deconstructed-layout)
- [Overlapped Deconstructed Layout](#overlapped-deconstructed-layout)
- [Hybrid Layout](#hybrid-layout)

*Storage fields are not accessible to user written logic in member declarations within the body of the union declaration. They can only be assigned via the constructors and accessed via the access patterns or the language operations that use them.*

### Metadata Encoding
The emitted struct is annotated with a `TypeUnion` attribute to indicate in metadata that this type is a type union. Constructors corresponding to case types are annotated with the `TypeUnionCase` attribute, indicating that the parameter type of the constructor is one of the case types.

```csharp
    [TypeUnion]
    public readonly struct Pet
    {
        [TypeUnionCase]
        public Pet(Cat value) {...}
        ...
    }
```

>*Case types cannot be listed in the `TypeUnion` attribute since type parameters cannot be specified in an attribute literal so they must appear as part of a member signature.*

## Language Integration
When integrated into the language a type union instance behaves as if it were the value it contains for some operations when the instance is known to be a type union. While this is a gray area that may not be practical or reasonable to push to the extremes, it is a useful illusion to maintain as it simplifies code.

To implement the illusion some language operations are defined to instead operate on the value contained by the union. These operations are translated into interactions with the type union's API like how cast operations are translated to calls on user-defined operator methods.

This section details the minimum necessary integration points for type unions to be considered a part of the language.

----
### Implicit Conversion to Union
To make type-union types feel like they are the value they contain, assignment from a case type value to a type-union variable should happen automatically, without needing to manually construct one from the other, similar to how a value of any type can be assigned to an object variable.

To enable this, an implicit conversion is introduced for any value that would match the parameter type of a case constructor. This conversion is emitted as an invocation of the constructor.

```csharp
    Pet pet = dog;
```

Translates to:

```csharp
    Pet pet = new Pet(dog);
```

*Note: This conversion cannot be specified using the user-defined conversion operators feature since case types may be interface types and user-defined conversion operators cannot be specified for interface types.*

----
### Conversion to Union from Default
Assigning default to a type union variable or converting default to a type-union results in a compiler warning.

```csharp
    public union Pet(Cat, Dog, Bird);
    Pet pet = default; // warning: cannot assign default to union
```
- It is still possible to have a type union in a default state. This rule just catches unintentional conversions similar to assigning null to a non-nullable reference type.
- **Optional**: We allow use of the forgiveness operator `!` to silence this warning.

----
### Null Pattern Match
A nominal type union's value is never null. When a null pattern match is made against a type union type, it always fails.

```csharp
    if (pet is null) {...}
```

While the `Value` property may return a null value when the struct wrapper is in the default state, this accessor is only used for a type pattern match and not a null pattern match.

To have a type union with a null value see the [Nullable Unions](#nullable-unions) section.

----
### Type Pattern Match from Union
The illusion of the union being its value means that accessing the value of the type union should feel like asking an object variable if it is one of the case-types via pattern matching. Type pattern matches on a statically known type-union type are translated to matches against the value contained in the type union.

```csharp
    pet is X
```
Some target types, however, might also apply to the type-union type itself. A target type of an interface that the type union is known to implement or that any case type might implement or just being a type parameter, means the match must be translated to a match on both the type union and the contained value.

| Example                           | Translation                                  |
|-----------------------------------|----------------------------------------------|
| `pet is Dog`                      | `pet is { Value: Dog }`                      |
| `pet is Dog dog`                  | `pet is { Value: Dog dog }`                  |
| `pet is Dog { Name: "Spot" } dog` | `pet is { Value: Dog { Name: "Spot" } dog }` |
| `pet is Corgi corgi`              | `pet is { Value: Corgi corgi }`              |
| `pet is ICase value`              | `pet is { Value: ICase value }`              |
| `pet is ISomething value`         | `pet is { Value: ISomething value }`         |
| `pet is IUnion value`             | N/A - no translation                         |
| `pet is TCase value`              | `pet is TCase value or { Value: TCase value }` |
| `pet is T value`                  | `pet is T value or { Value: T value }`       |

- Dog: A case type.
- Corgi: A sub-type of Dog.
- ISomething: an interface not implemented by the union but may be implemented by case types.
- ICase: A case type that is an interface and not implemented by the type union itself.
- IUnion: An interface that is implemented by the union.
- TCase: A case type that is a type parameter
- T: An arbitrary type parameter not known to be a case type.
- N/A: Translation not supported, original interpretation of operation is used.

----
### Type Pattern Match to Union
A value of a known case type can be converted to a type-union type via pattern matching.

```csharp
    dog is Pet pet
```
Being similar to [implicit conversion](#implicit-conversion-to-union), the pattern match translates into an invocation of a corresponding constructor.

```csharp   
    {Pet pet = new Pet(value); true}
```

*Note: Pattern matching to a type-union type from values of types not known to be a case type are supported via the optional [Type Pattern Match Unknown to Union](#type-pattern-match-unknown-to-union) feature.*

----
### Explicit Conversion
Explicit conversions involving a type-union type as either the source or target of the conversion where special rules for pattern matching would apply for the same source and target types are translated into an `is` operator applying those pattern matching rules, returning the value on success or throwing an exception on failure.

```csharp
    (Pet)dog;
```

Translates to:

```csharp
    <<dog is Pet tmp>> ? tmp : throw new InvalidCastException(...);    
```

And the reverse:

```csharp
    (Dog)pet;
```

Translates to:

```csharp
    <<pet is Dog tmp>> ? tmp : throw new InvalidCastException(...);
```

*Note: the << >> brackets denote where additional translation occurs.*

----
### As Operator
An `as` operator involving a type-union type as either the source or target of the conversion where special rules for pattern matching would apply for the same source and target types are translated into an `is` operator applying those pattern matching rules, returning the value of on success or the default of the target type on failure.
```csharp
    pet as Dog
```

Translates to:
```csharp
    <<pet is Dog tmp>> ? tmp : default(Dog)
```

*Note: Normal rules apply for which types can be used on the right side of an `as` operator.*  
*Note: the << >> brackets denote where additional translation occurs.*

----
### Switch Statement and Expression
The switch statement and expression employ the same translations for type pattern matching involving type-union types in switch cases as outlined above, with switch case using the preferred translation for its target type.

```csharp
    pet switch 
    {
        Cat cat => ...,
        Dog dog => ...,
        Bird bird => ...
        _ => ...
    }
```

Translate to:

```csharp
    pet switch
    {
        { Value: Cat cat } => ...,
        { Value: Dog dog } => ...,
        { Value: Bird bird } => ...,
        _ => ...
    }
```

#### Exhaustive Unions
Since type unions are limited to a closed set of case types they are exhaustible in a switch. If all cases of a type union are handled in the switch cases, a default does not need to be specified. However, a default case will still be added to handle cases of invalid unions as is normally added to an exhaustive switch.

----
### Nullable Unions
Type unions are not allowed to have a null value. Nullable case types are warned against and null values are intercepted during construction placing the type union instance into a default state that does not have a case type value.

To represent the equivalent of null in a type union it is preferred to use an explicit case type to represent a non value. For example, an `Option` type union might have a `None` case type that represents not having a value.

Still, you may have reasons why a null value makes sense to assign to your type union variable. If this is the case, you can easily declare the type as the nullable version of the union using the question mark syntax.

```csharp
    Pet? maybePet = null;
```

Because the type union `Pet` is emitted as a `struct` type, this will mean references to `Pet?` are references to `Nullable<Pet>` at runtime. However, this may cause issues with the translation of some language operations which can be solved with some additional rules that are used when nullables are combined with type unions.

When a nullable type union is used in a type pattern match, the match is translated to match on the non-nullable value of the nullable type union.

```csharp
    maybePet is Dog dog
```
```csharp
    maybePet is Pet tmp && <<tmp is Dog dog>>
```

The other operations, type conversion, etc, are translated likewise. A switch over a nullable union value can be optimized to only access the non-nullable value once.

----
### Unknown Unions
The special translations outlined in this document for nominal type unions only occur when a type union is known statically to be involved. If a type union value exists but is statically typed as object or a type parameter no special translations will occur and the normal meaning of the operation made against the union's struct wrapper type will remain in effect.

Code that deals solely with generic types will not have recognized a type union being used and will not translate type tests or casts using these new rules. 

For example, the `OfType<T>` method does a type test with generics only.
```csharp
    List<Pet> pets = [new Dog(...), new Cat(...)];
    pets.OfType<Dog>()  // will never match
```

However, doing the equivalent manually in a context that sees the type union will succeed.
```csharp
    List<Pet> pets = [new Dog(...), new Cat(...)];
    pets.Where(p => p is Dog).Select(p => (Dog)p);  // will match
```
*Note: It is possible that a future version of the runtime may become type union aware and allow type tests in generic-only contexts to succeed.*

---
### Union Constraints

It may seem to make sense to specify a type-union type in a generic type parameter constraint in order to constrain a type parameter to be one of the type union's case types.

```csharp
    TPet Feed<TPet>(TPet pet) where TPet : Pet {...}
```
For example, the `Feed` method takes a pet as input, feeds it and returns a new fed pet instance on output. It uses the type parameter to guarantee the same type going in is the same type going out.

```csharp
    Dog fedDog = Feed(unfedDog);
```

However, since the type union is represented as a struct wrapper and struct's cannot be used in constraints like this, it is not possible to do so. The proposed feature of a type union is language only and does not exist in the runtime.

The best we can do to actually constrain to a type union is to use the type union as the parameter type, but this will not get you the desired return type.

```csharp
    Pet Feed(Pet pet) {...}
```

This is an interesting area to consider perhaps adding a compile-time only constraint to C# or creating a new kind of runtime constraint that includes an *or* condition.

```csharp
    TPet Feed<TPet>(TPet pet) where TPet : Cat or Dog or Bird {...}
```

----
----

## Optional Features
The rest of the proposal includes optional features that may enhance capability or improve performance of nominal type unions.

#### Features that improve the representation or access of the value
  - Access Patterns
    - [Discriminator Access Pattern](#discriminator-access-pattern)  
    - [TryGet Access Pattern](#tryget-access-pattern)  
    - [Generic TryGet Access Pattern](#generic-tryget-access-pattern)
  - Storage Layouts
    - [Discriminated Boxed Layout](#discriminated-boxed-layout)  
    - [Fat Layout](#fat-layout)  
    - [Skim Layout](#skim-layout)  
    - [Overlapped Layout](#overlapped-layout)
    - [Deconstructed Layout](#deconstructed-layout)  
    - [Overlapped Deconstructed Layout](#overlapped-deconstructed-layout)  
    - [Hybrid Layout](#hybrid-layout)  
  
#### Features that make the union behave more like the value in more places
  - More Conversions
    - [Conversion from unknown case to union](#type-pattern-match-unknown-to-union)  
    - [Conversion from union to union](#type-pattern-match-from-union-to-union)  
    - [Implicit conversion from union to union](#implicit-conversion-from-union-to-union)  
  - Common Members 
    - [Common Member Proxies](#common-member-proxies)  
    - [Common Member Access](#common-member-access)
    - [Common Base Types](#common-base-types)  
    - [Common Interface Implementation](#common-interface-implementation)  
    - [ToString](#tostring)  
    - [Equality](#equality)  
  
#### Features that help with interop or reflection
  - [Common Helper Methods](#common-helper-methods)  
  - [Common Interfaces](#common-interface)  

#### Miscellaneous
  - [Default Unions](#default-unions)
  - [Combined Unions](#combined-unions)
</br>

----
### Type Pattern Match Unknown to Union
To enable conversion of a value not statically known to be a case type to a union type, a special translation exists.

```csharp
    object value = ...;
    if (value is Pet pet) {...}
```

To do this translation the source is checked against all possible case types, calling the corresponding constructor when a match is found.

If the [TypeUnion.TryConvert](#typeuniontryconvert) feature is available, the pattern match is translated into a call on the `TryConvert` method that does these checks and more.

```csharp
    if (value is Pet Pet || TypeHelper.TryConvert(value, out tmp)) {...}
```

Otherwise, if the [Conditional Construction](#conditional-construction) feature is available, the pattern match is translated into a call on the `TryCreate` method that does exactly these checks.

```csharp
    if (value is Pet Pet || Pet.TryCreate(value, out tmp)) {...}
```

If neither are available, the pattern match has no special translation, leaving just the original test for the type union type.

```csharp
    if (value is Pet pet) {...}
```

**Optional:** The compiler generates the checks against all case types inline, including all the same logic that would appear in the [Conditional Construction](#conditional-construction) implementation of the `TryCreate` method.

```csharp
    if (value is Pet pet || (value is Cat tmp1 && new Pet(tmp1) is Pet pet) || ...) {...}
```

**Optional:** The compiler generates a per-compilation-unit helper method with the same logic

```csharp
    public static class PrivateTypeUnionHelpers
    {
        public static bool TryCreate<T>(T value, out Pet pet) {...}
    }
    ...

    if (value is Pet pet || PrivateTypeUnionHelpers.TryCreate(value, out pet)) {...}
```

----
### Conditional Construction
The Type Union's generated struct includes a `TryCreate` method to help in conditionally constructing type unions.

```csharp
    public static bool TryCreate<TValue>(TValue value, out Pet pet);
```

The method tests the input value against the known case types and constructs the union using the corresponding constructor.

The implementation would be similar to this:

```csharp
    public static bool TryCreate<TValue>(TValue value, out Pet union)
    {
        switch (value)
        {
            case Cat value1:
                union = new Pet(value1);
                return true;
            case Dog value2:
                union = new Pet(value2);
                return true;
            case Bird value3:
                union = new Pet(value3);
                return true;
            default:
                pet = default;
                return false;
        }
    }
```

This may be used by the [Type Pattern Match Unknown to Union](#type-pattern-match-unknown-to-union) feature, may be used by the [TypeUnion.TryConvert](#typeuniontryconvert) feature, may be an implementation detail of [Common Interface](#common-interface) feature, or may just be used manually by users to perform this construction/conversion.

----
### Type Pattern Match from Union to Union
Converting between two union types will be a common operation when multiple union types exist that share some of the same case types, or similar case types such as some values from one union would be legal values of another.

If the [TypeUnion.TryConvert](#typeuniontryconvert) helper method is available, it is used to conditionally convert between the two type union types.

```csharp
    animal is Pet pet
```
```csharp
    TypeHelper.TryConvert(animal, out Pet tmp);
```

Otherwise, if the [conditional construction](#conditional-construction) `TryCreate` method is available, it is used with the value of the source obtained via the [value access pattern](#value-access-pattern).

```csharp
    Pet.TryCreate(animal.Value, out var pet);
```

**Optional:** this is only exposed in the explicit conversion form and not a pattern match.

```csharp
    (Pet)animal
```

----
### Implicit Conversion from Union to Union
When one type union has at least all the same case types as another, a value of the other is assignable to a variable of the first without requiring an explicit cast.

This is translated the same as with [type pattern match from union to union](#type-pattern-match-from-union-to-union) and [explicit conversion](#explicit-conversion) rules.

```csharp
    Animal animal = pet;
```
```csharp
    Animal animal = TypeHelper.TryConvert(pet, out Animal tmp) ? tmp : default;
```

----
### Combined Unions
The case types of one type union can be included in another type union without manually listing them all using a spread-like operator in its declaration.

```csharp
    public union Pet(Cat, Dog, Bird);
    public union Animal(..Pet, Alligator, Bison, Platypus);
```

- Should there be an operators to exclude types? Maybe: !, ~, -
- What about including all but some cases?  ..Pet-Cat

----
### Default Unions
A nominal type union always has a value. Yet, since it is implemented as a struct wrapper it may not actually have a value when the union is uninitialized or initialized to default. This is mostly dealt with by fact that type tests do not succeed when the union is in the default state. Yet, this can lead to runtime exceptions in an exhausted switch.

You may want to give a nominal type union a meaningful value when it is in the default state, especially when it has a case that corresponds to not having a value, like `None` used by the `Option` type.

To enable this, one case in the nominal type union's case type list can be designated as the default by assigning it to `default`, as long as that default is not null.

```csharp
    record None();
    record Some<T>(T Value);

    union Option<T>(None=default, Some<T>);
```

When the type union is in the default state, the union behaves like it has the specified value, with the `Value` property returning this value and the other accessor patterns acting likewise. 
If the [Discriminated Access Pattern](#discriminator-access-pattern) is available, the discriminator property returns the value corresponding to this case.

```csharp
    readonly struct Option<T>
    {
        private readonly object _value;
        ...
        public object Value => _value ?? default(None);
    }
```

**Optional:** a full initializer expression may be specified to allow the default to be any non-null value.

```csharp
    union Pet(Cat, Dog, Bird=new Bird("Polly"));
```
```csharp
    readonly struct Pet
    {
        private readonly object _value;
        private static readonly Bird _default = new Bird("Polly");
        ...
        public object Value => _value ?? _default;
    }
```

**Conversion from Default**  

A nominal type union with a default case can be converted to from default without warning as listed in [Conversion to Union from Default](#conversion-to-union-from-default).

```csharp
    union Number(long=default, double, string);
    ...
    Number n = default; // okay here
```
----
### ToString
The type union provides an overload to the `ToString` method, dispatching to the method on the contained value.

```csharp
    public override string ToString()
    {
        return this switch
        {
            Cat v => v.ToString(),
            Dog v => v.ToString(),
            Bird v => v.ToString(),
            _ => ""
        }
    }
```

----
### Equality
To support comparing two type-union instances of the same type-union type, necessary to use the union type as a key in a dictionary for example, the compiler emits code that implements the standard equality interfaces and methods by deferring to the contained value when the unions both have the same cases.

```csharp
    public readonly struct Pet : IEquatable<Pet>
    {
        public bool Equals(Pet other) {...}
        {
            return (this, other) switch 
            {
                (Cat tv, Cat ov) => EqualityComparer<Dog>.Default.Equals(tv, ov),
                (Dog tv, Dog ov) => EqualityComparer<Cat>.Default.Equals(tv, ov),
                (Bird tv, Bird ov) => EqualityComparer<Bird>.Default.Equals(tv, ov),
                _ => false
            }
        }

        public override Equals(object other) =>
            return other is Pet typedOther && Equals(typedOther); 

        public static bool operator == (Pet other) => Equals(other);
        public static bool operator != (Pet other) => !Equals(other);
    }
```

- Optional: In addition, the weakly-type Equals overload supports comparing instances of different type-union types, or comparing type union instances to case type values.

This might be implemented using the [Common Helper Methods](#common-helper-methods) feature.
```csharp
    public override Equals(object other) =>
        object.Equals(
            TypeUnion.GetValueUnwrapped(this), 
            TypeUnion.TryGetValueUnwrapped(other, out object otherValue) ? otherValue : other
            );
```

----
### Common Member Proxies
Sometimes all cases of a type union share similar members. It would be convenient to be able to access those common members directly on the union type instance rather than need to first switch over all the cases and interact with each case separately. 

This feature adds proxy versions of those members onto the type-union type automatically generated by the compiler for all methods and properties shared by all case types.

```csharp
    public record Cat(...) { public bool IsHairy => true; }
    public record Dog(...) { public bool IsHairy => true; }
    public record Bird(...) { public bool IsHairy => false; }

    public union Pet(Cat, Dog, Bird);
```
Becomes
```csharp
    public readonly struct Pet
    {
        ...
        public bool IsHairy =>
            this switch 
            {
                Cat value1 => value1.IsHairy,
                Dog value2 => value2.IsHairy,
                Bird value3 => value3.IsHairy
            };
    }
```
And now it is possible to use the common property directly.
```csharp
    Pet pet = ...;
    if (pet.IsHairy) {...}
```

- Note: Works well with cases that are structs or don't otherwise share a base type.
  
----
### Common Base Types
Sometimes all cases of a type union share a common base type. It would be convenient to be able to access the value as that type and also the members declared by it directly on the union type instance rather than need to first convert the union instance to that common type. 

This is an alternative to the the [Common Member Proxies](#common-member-proxies) feature. But instead of generating proxy members on the type-union type, it would simply introduce additional translations so the members of the common base type would appear to be accessible from type union instance.

If all case types share a common base type, the union type takes advantage of this fact and uses that it the `Value` property instead of object. This common base type could either be inferred byt he compiler or declared using some additional syntax.

```csharp
    public record Animal { public virtual bool HasFourLegs { get; } }
    public record Cat(...): Animal;
    public record Dog(...): Animal;
    public record Bird(...): Animal;

    public union Pet(Cat, Dog, Bird);           // inferred?
    public union Pet(Cat, Dog, Bird)[Animal];   // syntax?
``` 
Becomes
```csharp
    public readonly struct Pet
    {
        public Animal Value { get; }
    }
```

- Note: Works well with case types that share a base type and does not require proxy members, but does not work for fully disjoint case types.

#### Translations
When a common base type exists, member access operations against the type union instance that do not bind to the type-union instance itself are translated to apply to the value instead via the `Value` property. This helps maintain the illusion that the union type is its value.

```csharp
    if (pet.HasFourLegs)
```    
Translates to:

```csharp
    if (pet.Value.HasFourLegs)
```

Likewise, conversions to the base type (or any of its base types or interfaces) translate into accesses directly on the `Value` property.

```csharp 
    Animal animal = pet;
```
Translates to:
```csharp
    Animal animal = pet.Value;
```

----
### Common Member Access
Sometimes all cases of a type union share similar members. It would be convenient to be able to access those common members directly on the union type instance rather than need to first switch over all the cases and interact with each case separately. 

This feature is an alternative to both the [Common Member Proxies](#common-member-proxies) and [Common Base Types](#common-base-types) features.

Instead of generating proxies for members or requiring them to share a base type, this feature enables access of common members by translating accesses to members not found on the union into into access of the members on the value by generating a switch inline (at the use site) or in a generated helper method within the emitted module.

```csharp
    public record Cat(...) { public bool HasFourLegs => true; }
    public record Dot(...) { public bool HasFourLegs => true; }
    public record Bird(...) { public bool HasFourLegs => false; }
    public union (Cat, Dog, Bird);
```
```csharp
    if (pet.HasFourLegs) {...}
```
Becomes
```csharp
    if (pet switch {
        Cat value => value.HasFourLegs,
        Dog value => value.HasFourLegs,
        Bird value => value.HasFourLegs
        }) {...}
```

----
### Common Interface Implementation
While rare, it may be necessary to declare and implement an interface on a type union that is also declared and implemented by the case types. You may have an interface you need to be available even in a context that is not aware of the union, such as when the type union is represented boxed as an object or as a type parameter.

```csharp
    interface ICustomSerialize { void SerializeTo(Stream stream); }

    record Cat(...): ICustomSerialize {...}
    record Dog(...): ICustomSerialize {...}
    record Bird(...): ICustomSerialize {...}

    union Pet(Cat, Dog, Bird);

    Pet pet = ...;
    Serialize(pet);
    
    void Serialize<T>(T item) where T : ICustomSerialize
    {
        item.SerializeTo(...);
    }
```

You could declare the interface on the type union and implement it yourself to delegate to the union's value. It would be easier if you could avoid writing the boilerplate implementation and let the compiler generate it for you in a manner similar to the [Common Member Proxies](#common-member-proxies) feature.

*When a type union declares an interface that all case types share, but does not implement a member of the interface, that member is auto-generated by the compiler on your behalf to delegate to the specific case type values.*

```csharp
    // declare without implementation
    union Pet(Cat, Dog, Bird) : ICustomSerialize;
```

```csharp
    // implementation generated private
    public readonly struct Pet : ICustomSerialize
    {
        void ICustomSerializable.SerializeTo(Stream stream) =>
            ((ICustomSerialize)this.Value)?.SerializeTo(stream);
    }
```

----
### Common Helper Methods
Some code may need to interact with type unions when the unions or case type values are weakly-typed. For instance, a serialization tool may need to deserialize a value that is a case type and store it via reflection in a field that is a type-union type.

To enable this, the `TypeUnion` static class of helper methods exists with methods that help identify facts about type unions and converting values to and from unions and even conditionally converting unions to other kinds of unions.

#### TypeUnion.IsTypeUnion
```csharp
    public static bool IsTypeUnion(Type type);
```
Returns true if the type is a type union.

#### TypeUnion.GetCaseTypes
```csharp
    public static IReadOnlyList<Type> GetCaseTypes(Type type);
```
Returns a read only list of the case types of a type union. If the type is not a type union it returns an empty list.

#### TypeUnion.GetValue
```csharp
    public static object? GetValue(object union);
```
The `GetValue` method gets the current value of the union. 
If the `union` parameter is not actually a type union it returns null.

#### TypeUnion.GetUnwrappedValue
```csharp
    public static object? GetUnwrappedValue(object union);
```
The `GetUnwrappedValue` method is similar to the `GetValue` method, except it continues to drill down through values that might also be type unions until a non-type-union value is reached.

#### TypeUnion.TryConvert (boxed)
```csharp
    public static bool TryConvert(object source, Type targetType, out object target);
```
The `TryConvert` method is used to conditionally convert a source value to a target union type, a source union's value to a target type or a source union to a target union type.

#### TypeUnion.TryConvert
```csharp
    public static bool TryConvert<TSource, TTarget>(TSource source, [NotNullWhen(true)] out TTarget target);
```
The `TryConvert` method is used to conditionally convert a source value to a target union type, a source union's unwrapped value to a target type or a source union to a target union type.

*This method may be targeted by the compiler to offer additional language conversions of type unions.*

----
### Common Interface
Using [helper methods](#helper-methods) is a good way to write code that constructs, accesses and converts type unions. But those helper methods would be relying on reflection to function, which may require runtime dependencies that you would rather not have and extra performance costs you would rather avoid. Having an interface that type unions implement would solve this. The helper methods could then defer to the type union via its interface for all interactions.

*The `ITypeUnion` interface (actually two interfaces) provides a common means to construct and access the value of arbitrary type unions. Type unions that implement these interfaces can be handled more efficiently.*

```csharp
public interface ITypeUnion
{
    object Value { get; }
    bool TryGetValue<TValue>([NotNullWhen(true)] out TValue value);
}

public interface ITypeUnion<TSelf> : ITypeUnion
{
    static bool TryCreate<TValue>(TValue value, [NotNullWhen(true)] out TSelf union);
}
```

- The `Value` property accesses the value of the type union weakly-typed. This may cause boxing of struct values not already boxed.
- The `TryGetValue` method offers a way to conditionally access the value without boxing. 
- The `TryCreate` static method enables conditional construction of the type union without boxing the value.

----
### Discriminator Access Pattern
The discriminator access pattern offers an alternative to the [value access pattern](#value-access-pattern) that can be significantly more performant with storage layouts that use an underlying discriminator.

This pattern offers a discriminator property that returns a number corresponding to the case type of the value contained by the union and an accessor property for each case type to access the value without boxing.

```csharp
    public readonly struct Pet
    {
        // discriminator
        public int Kind { get; }

        // accessors
        public Cat Value1 { get; }
        public Dog Value2 { get; }
        public Bird Value3 { get; }
    }
```

- The discriminator property returns an integer value 1 through N, indicating which accessor property should be used to access the value. It returns 0 when the type union has not been formally initialized or has been assigned default, indicting that the type union is in an invalid state and none of the accessor properties should be used to access the value, unless the union has a case that is declared to be default and then the number corresponding to this default case is returned.

- The accessor properties are named Value#N. The number N corresponds to the ordinal of the corresponding case type in the type union declaration.

- The accessor properties are intended to only be accessed when they correspond to the current value of the discriminator. They always return a non-null value when they do correspond to the discriminator, and always return the default value of the corresponding case type when they do not.

- The introduction of this access pattern will cause the order of the case types in the type union declaration to become significant. A change in the order will become a breaking change.


#### Pattern Matching
When used to translate a type pattern match the discriminator property is used to match the associated case and the corresponding accessor property is used to perform any additional type tests or matches. 

| Example                           | Translation                                  |
|-----------------------------------|----------------------------------------------|
| `pet is Dog`                      | `pet is { Kind: 2 }`                         |
| `pet is Dog dog`                  | `pet is { Kind: 2, Value2: var dog }`        |
| `pet is Dog { Name: "Spot" } dog` | `pet is { Kind: 2, Value2: { Name: "Spot" } dog }` |
| `pet is Corgi corgi`              | `pet is { Kind: 2, Value2: Corgi corgi }`     |
| `pet is ICase value`              | N/A |
| `pet is ISomething value`         | N/A |
| `pet is IUnion value`             | N/A |
| `pet is TCase value`              | N/A |
| `pet is T value`                  | N/A |

While it is always safe to use the discriminator access pattern to access the value of the union, since it tells you which accessor to use to access the value, it is not always safe for the compiler to assume a single discriminator value comparison can be used to replace a type check. Target types that are interfaces or type parameters may match multiple cases. This is why the translation table is incomplete. Its not safe to use the discriminator access pattern in these cases.

In addition, the existence of any case type that is an interface potentially pollutes the use of the discriminator access pattern for any other case. If other case types might also implement the same interface it is not possible for the compiler to know that the other case types can be provably accessed via one and only one discriminated accessor.

```csharp
    public union Pet(IAnimal, Dog);
    ...
    Pet pet = (IAnimal)new Dog(...);
    ...
    if (pet is Dog dog) {...}  // comparing Kind to 2 (Dog) fails, it is encoded as 1 (IAnimal)
```

The existence of any case types that are type parameters has similar issues. However, it is possible that an instantiated generic type union has replaced the type parameters with types that are now sufficiently disjoint such that the compiler can prove that a target type can be accessed from one and only one discriminated accessor. It all depends on what case types are known to exist in the context that the type union is being used.

```csharp
    public union OneOf<T1, T2>(T1, T2);
    ...
    OneOf<int, long> number = 1;
    if (number is int n) {...} // succeeds because int can only occur when Kind==1
    ...
    OneOf<Tx, Ty> something = ...;
    if (something is Tx x) {...} // may fail because cannot know which Kind is Tx is stored.
```

Pros:
- Does not box to test or access the value.
- Significantly faster than a type test and can help optimize switch statements and expressions that match multiple cases.

Cons:
- A change in the set or order of case types is a binary breaking change because it changes the meaning of the discriminator values and the naming of the accessor properties.
- Some case types may not be sufficiently disjoint to correspond to a single discriminated accessor.

----
### TryGet Access Pattern
The TryGet access pattern is another alternative to the value access pattern, that like the [discriminator access pattern](#discriminator-access-pattern) can access the values strongly-typed, but may not be as favorable to optimization.

The TryGet access pattern has a `TryGetValue` method for each case type, that returns a bool indicating if the value is successfully accessed as that type and the value itself as a strongly-typed out parameter.

```csharp
    public readonly struct Pet
    {
        public bool TryGetValue(out Cat value);
        public bool TryGetValue(out Dog value);
        public bool TryGetValue(out Bird value);
    }
```
- Each `TryGetValue` method tests and accesses the value in one call, with the semantics exactly the same as pattern matching using the [Value Access Pattern](#value-access-pattern), regardless of the storage model.

#### Pattern Matching
When a corresponding `TryGetValue` method is available, a type pattern match can be translated using this method.

| Example                           | Translation                                  |
|-----------------------------------|----------------------------------------------|
| `pet is Dog`                      | `pet.TryGetValue(out Dog _)`                      |
| `pet is Dog dog`                  | `pet.TryGetValue(out Dog dog)`                    |
| `pet is Dog { Name: "Spot" } dog` | `pet.TryGetValue(out Dog tmp) && tmp is { Name: "Spot" } dog }` |
| `pet is Corgi corgi`              | `pet.TryGetValue(out Dog tmp) && tmp is Corgi corgi`     |
| `pet is ICase value`              | `pet.TryGetValue(out ICase value)`                |
| `pet is ISomething value`         | N/A |
| `pet is IUnion value`             | N/A |
| `pet is TCase value`              | `pet.TryGetValue(out TCase value)`                |
| `pet is T value`                  | N/A |

Some examples cannot be translated. Only cases with `TryGetValue` overloads that correspond to the target type can be used.

Pros:
- Does not box to test or access the value like the [Value Access Pattern](#value-access-pattern).
- Does not have the drawback of the [Discriminated Access Pattern](#discriminator-access-pattern) with respect to target types can case types that are not sufficiently disjoint.
- Faster than the [Generic TryGet Access Pattern](#generic-tryget-access-pattern).

Cons:
- Cannot help optimize a switch statement or expression.

----
### Generic TryGet Access Pattern
The generic TryGet access pattern is similar to the [TryGet Access Pattern](#tryget-access-pattern) but only has a single generic `TryGetValue` method that can be used to test and access any value without boxing.

```csharp
    public readonly struct Pet 
    {
        public bool TryGetValue<T>([NotNullWhen(true)] out T value);
    }
```

#### Pattern Matching
When this access pattern is available, all type pattern matches can be translated using the generic `TryGetValue` method.

| Example                           | Translation                                  |
|-----------------------------------|----------------------------------------------|
| `pet is Dog`                      | `pet.TryGetValue(out Dog _)`                      |
| `pet is Dog dog`                  | `pet.TryGetValue(out Dog dog)`                    |
| `pet is Dog { Name: "Spot" } dog` | `pet.TryGetValue(out Dog tmp) && tmp is { Name: "Spot" } dog }` |
| `pet is Corgi corgi`              | `pet.TryGetValue(out Dog tmp) && tmp is Corgi corgi`     |
| `pet is ICase value`              | `pet.TryGetValue(out ICase value)`                |
| `pet is ISomething value`         | `pet.TryGetValue(out ISomething value)`           |
| `pet is IUnion value`             | N/A                                          |
| `pet is TCase value`              | `pet is TCase value or pet.TryGetValue(out value)` |
| `pet is T value`                  | `pet is T value or pet.TryGetValue(out value)` |

Pros:
- Does not box to test or access the value like the [Value Access Pattern](#value-access-pattern).
- Does not have the drawback of the [Discriminated Access Pattern](#discriminator-access-pattern) with respect to target types can case types that are not sufficiently disjoint.
- Only requires one method, unlike the [TryGet Access Pattern](#tryget-access-pattern).

Cons:
- Slower than the [TryGet Access Pattern](#tryget-access-pattern) due to generic method overhead.

----
### Value Access Pattern 
The value access pattern is the default access pattern and is always generated for a type union by the compiler. It is listed here only for completeness.

```csharp
    public readonly struct Pet
    {
        // value access pattern
        public object Value => ...;
    }
```

- The `Value` property returns the same value used to construct the type union, even if the case type was another type union.

#### Pattern Matching
The translation for the value access pattern is outlined in the [Type Pattern Matching From Union](#type-pattern-match-from-union) section.

- Depending on the nature of the case types, this access pattern may not be best suited for use in translating language operations. Other optional access patterns, if included may be preferred.

----
### Access Pattern Heuristic
The compiler chooses the access pattern to use for a pattern match or conversion translation given the patterns available on the type union.

1. If the [discriminated access pattern](#discriminator-access-pattern) is available and the target type of a type pattern match or conversion can be reduced to a single discriminated accessor, then this access pattern is used.
2. If the non-generic [TryGet access pattern](#tryget-access-pattern) is available and the target type of a type pattern match or conversion has a corresponding `TryGet` method, then this access pattern is used.
3. Either the [value access pattern](#value-access-pattern) or the generic [TryGet access pattern](#generic-tryget-access-pattern) is used. The choices are:
   - The value access pattern is always used. (current plan)
   - The generic TryGet access pattern is always used.
   - If all cases are known to be reference types, the value access pattern is used.
   - If all cases are known to be value types, the generic TryGet access pattern is used.

----
### Boxed Layout
The boxed layout includes a single field typed as object for all values. This is the default layout unless other layouts are available and chosen by the compiler.

```csharp
    public record Cat(...);
    public record Dog(...);
    public record Bird(...);

    public union Pet(Cat, Dog, Bird);
```
```csharp
    public readonly struct Pet
    {
        private readonly object _value;

        private Pet(object value) 
        {
            _value = value;
        }

        public Pet(Cat value): this(value) {}
        public Dog(Dog value): this(value) {}
        public Bird(Bird value): this(value) {}

        public object Value => _value;
    }
```

- The boxed layout is never combined with a [discriminated access pattern](#discriminator-access-pattern) because determining a discriminator value would defeat the performance advantage of using the pattern.

----
### Discriminated Boxed Layout
The discriminated boxed layout includes a field typed as object for all values and a discriminator field. 

```csharp
    public record Cat(...);
    public record Dog(...);
    public record Bird(...);

    public union Pet(Cat, Dog, Bird);
```
```csharp
    public readonly struct Pet
    {
        private readonly int _kind;
        private readonly object _value;

        private Pet(int kind, object value) 
        {
            _kind = kind;
            _value = value;
        }

        public Pet(Cat value): this(1, value) {}
        public Dog(Dog value): this(2, value) {}
        public Bird(Bird value): this(3, value) {}

        public object Value => _value;

        // optional discriminator access pattern
        public int Kind => _kind;
        public Cat Value1 => _kind == 1 ? (Cat)_value : default!;
        public Dog Value2 => _kind == 2 ? (Dog)_value : default!;
        public Bird Value3 => _kind == 3 ? (Bird)_value : default!;
    }
```

- This layout is practical only when the [discriminator access pattern](#discriminator-access-pattern) is being exposed.

----
### Fat Layout
The fat layout includes a strongly-typed field for each case type and a discriminator field typed as int.

```csharp
    public union Pet(Cat, Dog, Bird);
```
```csharp
    public readonly struct Pet
    {
        private readonly int _kind;
        private readonly Cat _value1;
        private readonly Dog _value2;
        private readonly Bird _value3;

        public Pet(Cat value)
        {
            _kind = 1;
            _value1 = value;
        }

        public Dog(Dog value)
        {
            _kind = 2;
            _value2 = value;
        }

        public Bird(Bird value)
        {
            _kind = 3;
            _value3 = value;
        }

        // value access pattern
        public object Value =>
            _kind switch 
            {
                1 => _value1,
                2 => _value2,
                3 => _value3,
                _ => null!
            };

        // optional discriminator access pattern
        public int Kind => _kind;
        public Cat Value1 => _value1;
        public Dog Value2 => _value2;
        public Bird Value3 => _value3;
    }
```
- This layout never boxes a case type value since all cases have their own strongly-typed field.
- Use of the [value access pattern](#value-access-pattern) may cause boxing.

----
### Skim Layout
The skim layout is similar to the [Fat Layout](#fat-layout) except when multiple case types are known to be reference types then those cases stored in a single shared object field and cast back to the case type on access.

```csharp
    public record Cat(...);
    public record Dog(...);
    public record struct Bird(...); 

    public union Pet(Cat, Dog, Bird);
```
```csharp
    public readonly struct Pet
    {
        private readonly int _kind;
        private readonly object _value;
        private readonly Bird _value3;

        public Pet(Cat value)
        {
            _kind = 1;
            _value = value;
        }

        public Dog(Dog value)
        {
            _kind = 2;
            _value = value;
        }

        public Bird(Bird value)
        {
            _kind = 3;
            _value3 = value;
        }

        // value access pattern
        public object Value =>
            _kind switch 
            {
                1 => _value,
                2 => _value,
                3 => _value3,
                _ => null!
            };

        // optional discriminator access pattern
        public int Kind => _kind;
        public Cat Value1 => _kind == 1 ? (Cat)_value : default!;
        public Dog Value2 => _kind == 2 ? (Dog)_value : default!;
        public Bird Value3 => _kind == 3 ? _value3 : default!;
    }   
```

- When all cases are known reference type this reduces to the same as the [discriminated boxed layout](#discriminated-boxed-layout).
- When fewer than two cases are known to be reference types this layout reduces to the [fat layout](#fat-layout).

----
### Overlapped Layout
The overlapped layout is similar to the [Skim Layout](#skim-layout), except that it overlaps the values of case types that can be overlapped.

If two or more case types can be overlapped, a special `Overlap` struct type is generated using `StructLayout` and `FieldOffset` attributes to store the values of different cases in the same memory area. Those cases with types that can be overlapped have a field corresponding to them in the `Overlap` type instead of a field in type-union type. A single field of the `Overlap` type is then added to store and access these cases.

Types that are safe to overlap are:
- primitive value-types
- value-types from the runtime libraries that do not contain reference values
- value-types from the current compilation unit that do not contain reference values

*Other value types from outside the compilation unit cannot be proven to be free of reference values at compile time, since compile-time metadata may not show all private fields of a type.*

```csharp
    public union Number(long, double, decimal, string);
```
```csharp
    public readonly struct Number
    {
        private readonly int _kind;
        private readonly Overlap _overlap;
        private readonly string _value4;

        [StructLayout(LayoutKind.Explicit)]
        file struct Overlap
        {
            [FieldOffset(0)]
            public long value1;
            [FieldOffset(0)]
            public double value2;
            [FieldOffset(0)]
            public decimal value3;
        }

        public Number(long value)
        {
            _kind = 1;
            _overlap.value1 = value;
        }

        public Number(double value)
        {
            _kind = 2;
            _overlap.value2 = value;
        }

        public Number(decimal value)
        {
            _kind = 3;
            _overlap.value3 = value;
        }

        public Number(string value)
        {
            _kind = 4;
            _value4 = value;
        }

        // value access pattern
        public object Value =>
            _kind switch 
            {
                1 => _overlap.value1,
                2 => _overlap.value2,
                3 => _overlap.value3,
                4 => _value4,
                _ => null!,
            };

        // optional discriminator access pattern
        public int Kind => _kind;
        public long Value1 => _kind == 1 ? _overlap.value1 : default!;
        public double Value2 => _kind == 2 ? _overlap.value2 : default!;
        public decimal Value3 => _kind == 3 ? _overlap.value3 : default!;
        public string Value4 => _kind == 4 ? _value4 : default!;
    }
```

----
### Deconstructed Layout
This deconstructed layout is similar to the [Skim Layout](#skim-layout) except that it also deconstructs value tuples and record structs into their constituent parts, allocating storage for each element using rules to share fields with other cases when possible, and then reconstructing the values on access.

Type union cases under this technique end up having zero or more elements each stored in zero or more fields. If the case type cannot be deconstructed then it simply represents its own single element. If it can it is deconstructed into its elements, and then the same process is applied to each of these elements until a final set of elements that cannot be deconstructed is obtained.

Each element of a case is assigned a storage location using the same pool of fields shared with all other cases. Multiple fields of the same type may be required to satisfy storage of all elements.

- Elements of reference type are stored in a field with type object, unless a field of the reference type can satisfy all uses of the field across all cases.
- Elements of value type (struct) are stored in a field of that type.
- Case types that deconstruct into zero elements take up no additional storage.

```csharp
    public record struct Cat(string Name, int Lives);
    public record struct Dog(string Name, bool Hunts);
    public record struct Bird(string Name, bool WantsCracker);

    public union Pet(Cat, Dog, Bird);
```
```csharp
    public readonly struct Pet
    {
        private readonly int _kind;
        private readonly string _element1;
        private readonly int _element2;
        private readonly bool _element3;

        public Pet(Cat value)
        {
            _kind = 1;
            (_element1, _element2) = value;
        }

        public Pet(Dog value)
        {
            _kind = 2;
            (_element1, _element3) = value;
        }

        public Pet(Bird value)
        {
            _kind = 3;
            (_element1, _element3) = value;
        }

        // value access pattern
        public object Value =>
            _kind switch 
            {
                1 => new Cat(_element1, _element2),
                2 => new Dog(_element1, _element3),
                3 => new Bird(_element1, _element3)
                _ => null!
            };

        // optional discriminator access pattern
        public int Kind => _kind;
        public Cat Value1 => _kind == 1 ? new Cat(_element1, _element2) : default!;
        public Dot Value2 => _kind == 2 ? new Dog(_element1, _element3) : default!;
        public Bird Value3 => _kind == 3 ? new Bird(_element1, _element3) : default;
    }
```

----
### Overlapped Deconstructed Layout
The [Overlapped Layout](#overlapped-layout) and the [Deconstructed Layout](#deconstructed-layout) can be combined. Deconstructed elements with types that can be overlapped are overlapped. 

```csharp
    public record struct Cat(string Name, int Lives);
    public record struct Dog(string Name, bool Hunts);
    public record struct Bird(string Name, bool WantsCracker);

    public union Pet(Cat, Dog, Bird);
```
```csharp
    public readonly struct Pet
    {
        private readonly int _kind;
        private readonly string _element1;
        private readonly Overlap _overlap;

        [StructLayout(LayoutKind.Explicit)]
        file struct Overlap
        {
            [FieldOffset(0)]
            public int element2;
            [FieldOffset(0)]
            public bool element3;
        }

        public Pet(Cat value)
        {
            _kind = 1;
            (_element1, _overlap.element2) = value;
        }

        public Pet(Dog value)
        {
            _kind = 2;
            (_element1, _overlap.element3) = value;
        }

        public Pet(Bird value)
        {
            _kind = 3;
            (_element1, _overlap.element3) = value;
        }

        // value access pattern
        public object Value =>
            _kind switch 
            {
                1 => new Cat(_element1, _overlap.element2),
                2 => new Dog(_element1, _overlap.element3),
                3 => new Bird(_element1, _overlap.element3)
                _ => null!
            };

        // optional discriminator access pattern
        public int Kind => _kind;
        public Cat Value1 => _kind == 1 ? new Cat(_element1, _overlap.element2) : default!;
        public Dot Value2 => _kind == 2 ? new Dog(_element1, _overlap.element3) : default!;
        public Bird Value3 => _kind == 3 ? new Bird(_element1, _overlap.element3) : default;
    }
```

- Each case that has at least one element that can be overlapped has a field corresponding to it in generated `Overlap` type. If a case has multiple elements that can be overlapped, those elements are grouped into a value-tuple.

----
### Hybrid Layout
This layout stores both the value (in all its cases) and discriminator in a special hybrid type that can store both reference and struct value types in a minimal representation without requiring custom code generation particular to the case types.

```csharp
    public union Pet(Cat, Dog, Bird);
```    
```csharp
    public readonly struct Pet
    {
        private readonly HybridLayout _data;

        private static HybridLayout.Encoding<Cat> _encoding1 = ...;
        private static HybridLayout.Encoding<Dog> _encoding2 = ...;
        private static HybridLayout.Encoding<Bird> _encoding3 = ...;

        file Pet(HybridLayout data) 
        {
            _data = data;
        }

        public Pet(Cat value): this(_encoding1.Create(value)) {}
        public Pet(Dog value): this(_encoding2.Create(value)) {}
        public Pet(Bird value): this(_encoding3.Create(value)) {}

        // value access pattern
        public object Value => _data.Value;

        // discriminator access pattern
        public int Kind => _data.Kind;
        public Cat Value1 => _encoding1.Decode(_data);
        public Dog Value2 => _encoding2.Decode(_data);
        public Bird Value3 => _encoding3.Decode(_data);
    }   
```

- This layout contains an object field for reference type values and 64 bits of extra memory for small structs that do not contain references. Large structs or structs with references are boxed and stored in the same field as reference type values.
- Structs wrapping a single reference value are deconstructed and the reference value stored in the reference type field.
- A discriminator value is also recorded using *tricks* not explained here.
- This layout is a compromise that avoid boxing for some commonly used small value types, and boxing for larger value types.

----
### Layout Heuristic
The compiler chooses a layout that best suits the case types of the union. The following options are listed in order of preference, assuming the each layout is available to the compiler.

1. If all case types are reference types, the [Boxed Layout](#boxed-layout) is used.
2. The [Overlapped Deconstructed Layout](#overlapped-deconstructed-layout) is used.
3. The [Deconstructed Layout](#deconstructed-layout) is used.
4. The [Overlapped Layout](#overlapped-layout) is used.
5. The [Skim Layout](#skim-layout) is used.
6. The [Fat Layout](#fat-layout) is used.
7. The [Hybrid Layout](#hybrid-layout) is used.
8. The [Boxed Layout](#boxed-layout) is used.

----
### Layout Hints
A hint can be given to the compiler to influence the heuristic into choosing a different layout.

The `UnionLayout` attribute can be specified to influence the layout used, by allowing you specify a value of the `UnionLayoutKind` enum.

The `UnionLayoutKind` contains the following values:

- Boxed: The [Boxed Layout](#boxed-layout) is used.
- Fat: The [Fat Layout](#fat-layout) is used.
- Balanced: The Standard Heuristic is used.

Example:
```csharp
    [UnionLayout(UnionLayoutKind.Fat)]
    public union Pet(Cat, Dog, Bird)
```

----
# END OF PROPOSAL
