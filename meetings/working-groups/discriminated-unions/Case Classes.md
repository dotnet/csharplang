# Case Classes

## Summary

A case class is a shorthand syntax for declaring a nested case of a closed type.
It infers much of what it is from context. 

```csharp
public closed record GateState
{
    case Closed;
    case Locked;
    case Open(float Percent);
}
```

When used within a record declaration, it is the equivalent of writing:

```csharp
public closed record GateState
{
    public sealed record Closed : GateState;
    public sealed record Locked : GateState;
    public sealed record Open(float Percent) : GateState;
}
```

## Motivation
To provide a concise way to specify simple type cases of a closed type that can be transitioned into more complex declarations without falling off a syntax cliff.

## Specification

### Semantics
- A case class can only be declared in the body of a closed class, record or union. 

- The existence of a case class declaration is independent of other declarations in the containing class.

### Declaration
A case class declaration includes the `case` keyword followed by a type declaration, similar to a class or record, without the keywords, modifiers or base type.

```csharp
case Open(float Percent);
```

#### Grammar
TBD

#### Attributes
A case class may declare custom attributes.

```csharp
[Attribute] case Open(float Percent);
```

#### Modifiers
The case class may not specify any modifiers. The class or record is always `public` and `sealed`. 

#### Partial Case Classes
TBD. Probably not, since you cannot specify partial modifier.

#### Type Parameters
The case class may not specify type parameters, as that would make exhaustiveness impossible, but it may refer to any type parameter declared by the containing class.

#### Base Type
A case class may not declare a base type. For a containing class or record, the case types base type is always the containing class.

#### Interfaces
A case class may declare interfaces.

```csharp
case Open(float percent) : ISomeInterface { ... }
```

#### Body
A case class may declare a body with member declarations.

```csharp
case Open(float percent) : { pubic void SomeMethod() {...} }
```

----
## Optional Features

Optional features are suggestions for additional work that could be done to enhance the core proposal.

### Singleton Case Classes
Case classes records without properties include a static property that is a singleton value for the class.

For example,
```csharp
case Closed;
```
becomes:
```csharp
public sealed record Closed : GateState { 
    public static Closed Instance => field ??= new Closed();
}
```

References to singleton case values can be reduced to referencing the static property.

```csharp
GateState state = GateState.Closed.Instance;
```

With the addition of a static value operator feature, not specified here, the case class type can be converted to the singleton value when referenced in non-type contexts that would shorten this to:

```csharp
GateState state = GateState.Closed;
```

With the addition of a target typed member lookup feature, not specified here, this would shorted further to:
```csharp
GateState state = Closed;
```
