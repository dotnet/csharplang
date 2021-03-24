# Parameterless struct constructors

## Summary
Support parameterless constructors and instance field initializers for struct types.

## Motivation
Explicit parameterless constructors would give more control over minimally constructed instances of the struct type.
Instance field initializers would allow simplified initialization across multiple constructors.
Together these would close an obvious gap between `struct` and `class` declarations.

Support for field initializers would also allow initialization of fields in `record struct` declarations without explicitly implementing the primary constructor.
```csharp
record struct Person(string Name)
{
    public object Id { get; init; } = GetNextId();
}
```

If struct field initializers are supported for constructors with parameters, it seems natural to extend that to parameterless constructors as well.
```csharp
record struct Person()
{
    public string Name { get; init; }
    public object Id { get; init; } = GetNextId();
}
```

## Proposal

### Instance field initializers
Instance field declarations for a struct may include initializers.

As with [class field initializers](https://github.com/dotnet/csharplang/blob/master/spec/classes.md#instance-field-initialization):
> A variable initializer for an instance field cannot reference the instance being created. 

### Constructors
A struct may declare a parameterless instance constructor.

A parameterless instance constructor is valid for all struct kinds including `struct`, `readonly struct`, `ref struct`, and `record struct`.

If the struct does not declare a parameterless instance constructor, and the struct has no fields with variable initializers, the struct (see [struct constructors](https://github.com/dotnet/csharplang/blob/master/spec/structs.md#constructors)) ...
> implicitly has a parameterless instance constructor which always returns the value that results from setting all value type fields to their default value and all reference type fields to null.

If the struct does not declare a parameterless instance constructor, and the struct has field initializers, a `public` parameterless instance constructor is synthesized.
The parameterless constructor is synthesized even if all initializer values are zeros.

### Modifiers
A parameterless instance constructor may be less accessible than the containing struct.
```csharp
public struct NoConstructor { }
public struct PublicConstructor { public PublicConstructor() { } }
public struct InternalConstructor { internal InternalConstructor() { } }
public struct PrivateConstructor { private PrivateConstructor() { } }
```

The same set of modifiers can be used for parameterless constructors as other instance constructors: `extern`, and `unsafe`.

Constructors cannot be `partial`.

### Executing field initializers
Execution of struct instance field initializers matches execution of [class field initializers](https://github.com/dotnet/csharplang/blob/master/spec/classes.md#instance-variable-initializers):
> When an instance constructor has no constructor initializer, ... that constructor implicitly performs the initializations specified by the _variable_initializers_ of the instance fields ... . This corresponds to a sequence of assignments that are executed immediately upon entry to the constructor ... . The variable initializers are executed in the textual order in which they appear in the ... declaration.

### Definite assignment
Instance fields must be definitely assigned in struct instance constructors that do not have a `this()` initializer (see [struct constructors](https://github.com/dotnet/csharplang/blob/master/spec/structs.md#constructors)).

Definite assignment of instance fields is required within explicit parameterless constructors as well.
```csharp
struct S1
{
    int x = 1;
    object y;
    S() { } // error: field 'y' must be assigned
}

struct S2
{
    int x = 2;
    object y;
    S() : this(null) { }        // ok
    S(object y) { this.y = y; } // ok
}
```

_Should definite assignment of struct instance fields be required within synthesized parameterless constructors?_
_If so, then if any instance fields have initializers, all instance fields must have initializers._
```csharp
struct S0
{
    int x = 0;
    object y;
    // ok?
}
```

If fields are not explicitly initialized, the constructor will need to zero the instance before executing any field initializers.
```
.class S0 extends System.ValueType
{
    .field int32 x
    .field object y
    .method public instance void .ctor()
    {
        ldarg.0
        initobj S0
        ldarg.0
        ldc.i4.0
        stfld int32 S0::x
        ret
    }
}
```

### No `base()` initializer
A `base()` initializer is disallowed in struct constructors.

The compiler will not emit a call to the base `System.ValueType` constructor from any struct instance constructors including explicit and synthesized parameterless constructors.

### Fields
The synthesized parameterless constructor will zero fields rather than calling any parameterless constructors for the field types.

_Should the compiler report a warning when constructors for fields are ignored?_
```csharp
struct S0
{
    public S0() { }
}

struct S1
{
    S0 F; // S0::.ctor() ignored
}

struct S<T> where T : struct
{
    T F; // constructor ignored
}
```

### `default` expression
`default` ignores the parameterless constructor and generates a zeroed instance.

_Should the compiler report a warning when a constructor is ignored?_
```csharp
_ = default(NoConstructor);      // ok
_ = default(PublicConstructor);  // ok: constructor ignored
_ = default(PrivateConstructor); // ok: constructor ignored
```

### Object creation
Object creation expressions require the parameterless constructor to be accessible if defined.
The parameterless constructor is invoked explicitly.

_This is a breaking change if the struct type with parameterless constructor is from an existing assembly._
_Should the compiler report a warning rather than an error for `new()` if the constructor is inaccessible, and emit `initobj`, for compatability?_
```csharp
_ = new NoConstructor();      // ok: initobj NoConstructor
_ = new PublicConstructor();  // ok: call PublicConstructor::.ctor()
_ = new PrivateConstructor(); // error: 'PrivateConstructor..ctor()' is inaccessible
```

### Uninitialized values
A local or field of a struct type that is not explicitly initialized is zeroed.
The compiler reports a definite assignment error for an uninitialized struct that is not empty. 
```csharp
NoConstructor s1;
PublicConstructor s2;
s1.ToString(); // error: use of unassigned local (unless type is empty)
s2.ToString(); // error: use of unassigned local (unless type is empty)
```

### Array allocation
Array allocation ignores any parameterless constructor and generates zeroed elements.

_Should the compiler warn that the parameterless constructor is ignored? How would such a warning be avoided?_
```csharp
_ = new NoConstructor[1];      // ok
_ = new PublicConstructor[1];  // ok: constructor ignored
_ = new PrivateConstructor[1]; // ok: constructor ignored
```

### Parameter default values
Parameterless constructors cannot be used as parameter default values.

_This is a breaking change if the struct type with parameterless constructor is from an existing assembly._
_Should the compiler report a warning rather than an error for `new()` if the constructor is inaccessible, and emit `default`, for compatability?_
```csharp
static void F1(NoConstructor s1 = new()) { }     // ok
static void F2(PublicConstructor s1 = new()) { } // error: default value must be constant
```

### Constraints
The `new()` type parameter constraint requires the parameterless constructor to be `public` if defined (see [satisfying constraints](https://github.com/dotnet/csharplang/blob/master/spec/types.md#satisfying-constraints)).
```csharp
static T CreateNew<T>() where T : new() => new T();

_ = CreateNew<NoConstructor>();       // ok
_ = CreateNew<PublicConstructor>();   // ok
_ = CreateNew<InternalConstructor>(); // error: 'InternalConstructor..ctor()' is not public
```
_Should the compiler report a warning rather than an error when substituting a struct with a non-public constructor for a type parameter with a `new()` constraint, for compatability and to avoid assuming the type is actually instantiated?_

`new T()` is emitted as a call to `System.Activator.CreateInstance<T>()`, and the compiler assumes the implementation of `CreateInstance<T>()` invokes the `public` parameterless constructor if defined.

_With .NET Framework, `Activator.CreateInstance<T>()` invokes the parameterless constructor if the constraint is `where T : new()` but appears to ignore the parameterless constructor if the constraint is `where T : struct`._

There is a gap in type parameter constraint checking because the `new()` constraint is satisfied by a type parameter with a `struct` constraint (see [satisfying constraints](https://github.com/dotnet/csharplang/blob/master/spec/types.md#satisfying-constraints)).

As a result, the following will be allowed by the compiler but `Activator.CreateInstance<InternalConstructor>()` will fail at runtime.
The issue is not introduced by this proposal though - the issue exists with C# 9 if the struct type with inaccessible parameterless constructor is from metadata.
```csharp
static T CreateNew<T>() where T : new() => new T();
static T CreateStruct<T>() where T : struct => CreateNew<T>();

_ = CreateStruct<InternalConstructor>(); // compiles; 'MissingMethodException' at runtime
```

### Optional parameters
Constructors with optional parameters are not considered parameterless constructors. This behavior is unchanged from earlier compiler versions.
```csharp
struct S1 { public S1(string s = "") { } }
struct S2 { public S2(params object[] args) { } }

_ = new S1(); // ok: ignores constructor
_ = new S2(); // ok: ignores constructor
```

### Metadata
Explicit and synthesized parameterless struct instance constructors will be emitted to metadata.

Parameterless struct instance constructors will be imported from metadata regardless of accessibility.
_This might be a breaking change for consumers of existing assemblies with structs with private parameterless constructors if additional errors or warnings are reported._

Parameterless struct instance constructors will be emitted to ref assemblies regardless of accessibility to allow consumers to differentiate between no parameterless constructor an inaccessible constructor.

## See also

- https://github.com/dotnet/roslyn/issues/1029

## Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2021/LDM-2021-01-27.md#field-initializers
