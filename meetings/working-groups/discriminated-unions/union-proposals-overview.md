# Union proposals overview

**Umbrella issue**: https://github.com/dotnet/csharplang/issues/9582

- [Union proposals overview](#union-proposals-overview)
  - [Unions](#unions)
  - [Standard type unions](#standard-type-unions)
  - [Closed enums](#closed-enums)
  - [Closed hierarchies](#closed-hierarchies)
  - [Case declarations](#case-declarations)
  - [Target-typed static member access](#target-typed-static-member-access)
  - [Target-typed generic type inference](#target-typed-generic-type-inference)
  - [Inference for constructor calls](#inference-for-constructor-calls)
  - [Inference for type patterns](#inference-for-type-patterns)
  - [Type value conversion](#type-value-conversion)

``` mermaid
flowchart LR

%% Features
Unions[Unions]:::approved
Standard[Standard type unions]:::approved
Enums[Closed enums]:::approved
Hierarchies[Closed hierarchies]:::approved
Cases[Case declarations]:::approved
TargetAccess[Target-typed access]:::approved
TargetInfer[Target-typed inference]:::approved
InferNew[Inference for constructors]:::approved
InferPattern[Inference for type patterns]:::consideration
TypeValue[Type value conversion]:::consideration

%% Dependencies
Unions --> Standard
Unions & Hierarchies --> Cases -.-> TargetAccess
Hierarchies <-.-> Enums
TargetInfer --> InferNew & InferPattern

%% Colors
classDef approved fill:#cfc,stroke:#333,stroke-width:1.5px;
classDef consideration fill:#ffd,stroke:#333,stroke-width:1.5px;
classDef unapproved fill:#ddd,stroke:#333,stroke-width:1.5px;
classDef rejected fill:#fdd,stroke:#333,stroke-width:1.5px;
```

## Unions

- **Proposal**: [Unions](https://github.com/dotnet/csharplang/blob/main/proposals/unions.md)
- **LDM**: Approved. 
- **Dependencies**: None.

A set of interlinked features that combine to provide C# support for union types, including a declaration syntax and several useful behaviors.

```csharp
public union Pet(Cat, Dog); // Declaration syntax

Pet pet = dog;              // Implicit conversion

_ = pet switch
{
    Cat cat => ...,         // Implicit matching
    Dog dog => ...,
}                           // Exhaustive switching
```

## Standard type unions

- **Proposal**: [Standard type unions](https://github.com/dotnet/csharplang/blob/main/proposals/standard-unions.md)
- **LDM**: Approved. 
- **Dependencies**: [Nominal type unions](#nominal-type-unions). 

A family of nominal type unions in the `System` namespace:

```csharp
public union Union<T1, T2>(T1, T2);
public union Union<T1, T2, T3>(T1, T2, T3);
public union Union<T1, T2, T3, T4>(T1, T2, T3, T4);
...
```

## Closed enums

- **Proposal**: [Closed enums](https://github.com/dotnet/csharplang/blob/main/proposals/closed-enums.md)
- **LDM**: Approved. 
- **Dependencies**: None. Design together with [Closed hierarchies](#closed-hierarchies) to ensure coherence.

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
- **Dependencies**: None. Design with [Closed enums](#closed-enums) to ensure coherence.

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
- **Dependencies**:  [Closed hierarchies](#closed-hierarchies) and [Nominal type unions](#nominal-type-unions).

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

## Target-typed generic type inference

- **Proposal**: [Target-typed generic type inference](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-generic-type-inference.md)
- **LDM**: Approved. 
- **Dependencies**: None. Informed by union scenarios.

Generic type inference may take a target type into account.

```csharp
MyCollection<string> c = MyCollection.Create(); // 'T' = 'string' inferred from target type
```

## Inference for constructor calls

- **Proposal**: [Inference for constructor calls](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-constructor-calls.md)
- **LDM**: Approved. 
- **Dependencies**: [Target-typed generic type inference](#target-typed-generic-type-inference)

'new' expressions may infer type arguments for the newly created class or struct, including [from a target type](target-typed-generic-type-inference) if present.

```csharp
Option<int> option = new Some(5); // Infer 'int' from argument and target type
```

## Inference for type patterns

- **Proposal**: [Inference for type patterns](https://github.com/dotnet/csharplang/blob/main/proposals/inference-for-type-patterns.md)
- **LDM**: Needs more work. 
- **Dependencies**: [Target-typed generic type inference](#target-typed-generic-type-inference)

Type patterns may omit a type argument list when it can be inferred from the pattern input value.

```csharp
void M(Option<int> option) => option switch
{
    Some(var i) => ..., // 'Some<int>' inferred
    ...
};
```

## Type value conversion

- **Proposal**:[Type value conversion](https://github.com/dotnet/csharplang/blob/12e6f5b0d512d15d32c8e7ae95674bd070b2758f/meetings/working-groups/discriminated-unions/type-value-conversion.md)
- **LDM**: Needs more work.
- **Dependencies**: None.

A type expression specified in a value context can be converted to a value if the type supports a conversion to value.

```csharp
GateState value = GateState.Locked;
```