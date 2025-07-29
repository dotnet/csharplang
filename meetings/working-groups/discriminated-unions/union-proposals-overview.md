# Union proposals overview

- [Nominal type unions](#nominal-type-unions)
- [Standard type unions](#standard-type-unions)
- [Union interfaces](#union-interfaces)
- [Custom unions](#custom-unions)
- [Non-boxing access pattern for custom unions](#non-boxing-access-pattern-for-custom-unions)
- [Closed enums](#closed-enums)
- [Closed hierarchies](#closed-hierarchies)
- [Case declarations](#case-declarations)
- [Target-typed static member access](#target-typed-static-member-access)

``` mermaid
flowchart LR

%% Features
Unions[Nominal type unions]:::approved
Standard[Standard type unions]:::unapproved
Interfaces[Union interfaces]:::unapproved
Custom[Custom unions]:::unapproved
NonBoxingAccess[Non-boxing access pattern]:::unapproved
Enums[Closed enums]:::approved
Hierarchies[Closed hierarchies]:::approved
Cases[Case declarations]:::approved
Target[Target-typed access]:::approved

%% Dependencies
Unions --> Standard
Unions <--> Interfaces --> Custom --> NonBoxingAccess
Unions & Hierarchies --> Cases -.-> Target
Hierarchies <-.-> Enums

%% Colors
classDef approved fill:#cfc,stroke:#333,stroke-width:1.5px;
classDef consideration fill:#ffd,stroke:#333,stroke-width:1.5px;
classDef unapproved fill:#ddd,stroke:#333,stroke-width:1.5px;
classDef rejected fill:#fdd,stroke:#333,stroke-width:1.5px;
```

## Nominal type unions

- **Proposal**: [Nominal Type Unions](https://github.com/dotnet/csharplang/blob/main/proposals/nominal-type-unions.md)
- **LDM**: Approved. 
- **Dependencies**: None.

Introduces declaration of `union` types, which can only contain values from a specified list of types. A consuming switch expression implicitly applies to the contents, and can assume only the listed case types can occur, avoiding exhaustiveness warnings.


```csharp
public union Pet(Cat, Dog);

Pet pet = dog;

_ = pet switch
{
    Cat cat => ...,
    Dog dog => ...,
    // No warning about missing cases
}
```

## Standard type unions

- **Proposal**: [Standard type unions](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/standard-unions.md)
- **LDM**: Not approved. 
- **Dependencies**:  *Nominal type unions*. 

A family of nominal type unions in the `System` namespace:

```csharp
public union Union<T1, T2>(T1, T2);
public union Union<T1, T2, T3>(T1, T2, T3);
public union Union<T1, T2, T3, T4>(T1, T2, T3, T4);
...
```

## Union interfaces

- **Proposal**: [Union interfaces](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/union-interfaces.md)
- **LDM**: Not approved. 
- **Dependencies**: Design with *Nominal type unions*.

Interfaces, at least some of which are implemented by compiler-generated unions, identify types as unions at runtime and facilitate access and construction.

```csharp
object obj = ...;

if (obj is IUnion union && union.Value is string x) {...}

void M<TUnion>() where TUnion : IUnion<TUnion>
{
    object val = ...;
    if (TUnion.TryCreate(val, out var union)) {...}
}
```

## Custom unions

- **Proposal**: [Custom unions](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/custom-unions.md)
- **LDM**: Not approved. 
- **Dependencies**: *Nominal type unions*.

Allow hand-authored types to be consumed as unions (creation, pattern matching, exhaustiveness).

This can be used for optimization or to make existing types union-like.

## Non-boxing access pattern for custom unions

- **Proposal**: [Non-boxing access pattern for custom unions](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/non-boxing-access-pattern.md)
- **LDM**: Not approved. 
- **Dependencies**: *Custom unions*.

Allow custom union types to use an alternative access pattern that does not incur boxing.

## Closed enums

- **Proposal**: [Closed enums](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md)
- **LDM**: Approved. 
- **Dependencies**: None. Design together with *Closed hierarchies* to ensure coherence.

Allow enums to be declared `closed`, preventing creation of values other than the explicitly declared enum members. A consuming switch expression can assume only those values can occur, avoiding exhaustiveness warnings.

```csharp
public closed enum Color { Red, Green, Blue }

var infrared = Color.Red - 1; // Error, not a declared member

_ = color switch
{
    Red => "red",
    Green => "green",
    Blue => "blue"
    // No warning about missing cases
};
```

## Closed hierarchies

- **Proposal**: [Closed hierarchies](https://github.com/dotnet/csharplang/blob/main/proposals/closed-hierarchies.md)
- **LDM**: Approved. 
- **Dependencies**: None. Design with *Closed enums* to ensure coherence.

Allow classes to be declared `closed`, preventing its use as a base class outside of the assembly. A consuming switch expression can assume only derived types from within that assembly can occur, avoiding exhaustiveness warnings.

```csharp
// Assembly 1
public closed class C { ... } 
public class D : C { ... }     // Ok, same assembly

// Assembly 2
public class E : C { ... }     // Error, 'C' is closed and in a different assembly

_ = c switch
{
    D d => ...,
    // No warning about missing cases
}
```

## Case declarations

- **Proposal**: [Case declarations](https://github.com/dotnet/csharplang/blob/main/proposals/case-declarations.md)
- **LDM**: Approved. 
- **Dependencies**:  *Closed hierarchies* and *Nominal type unions*.

A shorthand for declaring nested case types of a closed type (closed class or union type).

```csharp
public closed record GateState
{
    case Closed;
    case Locked;
    case Open(float Percent);
}

public union Pet
{
    case Cat(...);
    case Dog(...);
}
```

## Target-typed static member access

- **Proposal**: [Target-typed static member lookup](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-lookup.md)
- **LDM**: Approved. 
- **Dependencies**: None. Informed by union scenarios.

Enables a type name to be omitted from static member access when it is the same as the target type.

``` c#
return result switch
{
    .Success(var val) => val,
    .Error => defaultVal,
};
```

