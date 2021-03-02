# Parameterless struct constructors

## Summary

Support parameterless constructors and instance field initializers for struct types.

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
This has consequences for certain scenarios detailed later.

The same set of modifiers can be used for parameterless constructors as other constructors: `static`, `extern`, and `unsafe`.

### Executing field initializers
Execution of struct instance field initializers matches execution of [class field initializers](https://github.com/dotnet/csharplang/blob/master/spec/classes.md#instance-variable-initializers):
> When an instance constructor has no constructor initializer, ... that constructor implicitly performs the initializations specified by the _variable_initializers_ of the instance fields ... . This corresponds to a sequence of assignments that are executed immediately upon entry to the constructor and before the implicit invocation of the direct base class constructor. The variable initializers are executed in the textual order in which they appear in the ... declaration.

### Definite assignment
Instance fields must be definitely assigned in struct instance constructors that do not have a `this()` initializer (see [struct constructors](https://github.com/dotnet/csharplang/blob/master/spec/structs.md#constructors)).

Definite assignment of instance fields is required within explicit parameterless constructors as well.
Definite assignment of instance fields _is not required_ within synthesized parameterless constructors.

_Should the definite assignment rule be applied consistently? That is, should we require that all instance fields of a struct are explicitly assigned even when the parameterless constructor is synthesized, or should we drop the existing requirement that all fields are explicitly assigned in struct constructors?_
```csharp
struct S0
{
    object x = null;
    object y;
    // ok
}

struct S1
{
    object x = null;
    object y;
    S() { } // error: field 'y' must be assigned
}

struct S2
{
    object x = null;
    object y;
    S() : this(null) { }        // ok
    S(object y) { this.y = y; } // ok
}
```

_The synthesized parameterless constructor may need to emit `ldarg.0 initobj S` before any field initializers. Describe the specifics. How is this handled in Visual Basic?_

### No `base()` initializer
A `base()` initializer is disallowed in struct constructors.

The compiler will not emit a call to the base `System.ValueType` constructor from any struct instance constructors including explicit and synthesized parameterless constructors.

### Constructor use

The parameterless constructor may be less accessible than the containing value type.
```csharp
public struct NoConstructor { }
public struct PublicConstructor { public PublicConstructor() { } }
public struct InternalConstructor { internal InternalConstructor() { } }
public struct PrivateConstructor { private PrivateConstructor() { } }
```

`default` ignores the parameterless constructor and generates a zeroed instance.
```csharp
_ = default(NoConstructor);      // ok
_ = default(PublicConstructor);  // ok: constructor ignored
_ = default(PrivateConstructor); // ok: constructor ignored
```

Object creation expressions require the parameterless constructor to be accessible if defined.
The parameterless constructor is invoked explicitly.
_This is a breaking change if the struct type with parameterless constructor is from an existing assembly._
```csharp
_ = new NoConstructor();       // ok: initobj NoConstructor
_ = new InternalConstructor(); // ok: call InternalConstructor::.ctor()
_ = new PrivateConstructor();  // error: 'PrivateConstructor..ctor()' is inaccessible
```

A local or field of a struct type that is not explicitly initialized is zeroed.
The compiler reports a definite assignment error for an uninitialized struct that is not empty. 
```csharp
NoConstructor s1;
PublicConstructor s2;
s1.ToString(); // error: use of unassigned local (unless type is empty)
s2.ToString(); // error: use of unassigned local (unless type is empty)
```

Array allocation ignores any parameterless constructor and generates zeroed elements.
_Should the compiler warn that the parameterless constructor is ignored? How would such a warning be avoided?_
```csharp
_ = new NoConstructor[1];      // ok
_ = new PublicConstructor[1];  // ok: constructor ignored
_ = new PrivateConstructor[1]; // ok: constructor ignored
```

Parameterless constructors cannot be used as parameter default values.
_This is a breaking change if the struct type with parameterless constructor is from an existing assembly._
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
_ = CreateNew<PrivateConstructor>();  // error: 'PrivateConstructor..ctor()' is not public
```

`new T()` is emitted as a call to `System.Activator.CreateInstance<T>()`, and the compiler assumes the implementation of `CreateInstance<T>()` invokes the `public` parameterless constructor if defined.

There is a gap in type parameter constraint checking because the `new()` constraint is satisfied by a type parameter with a `struct` constraint (see [satisfying constraints](https://github.com/dotnet/csharplang/blob/master/spec/types.md#satisfying-constraints)).

As a result, the following will be allowed by the compiler but `Activator.CreateInstance<InternalConstructor>()` will fail at runtime.
The issue is not introduced by this proposal though - the issue exists with C# 9 if the struct type with inaccessible parameterless constructor is from metadata.
```csharp
static T CreateNew<T>() where T : new() => new T();
static T CreateStruct<T>() where T : struct => CreateNew<T>();

_ = CreateStruct<InternalConstructor>(); // compiles; 'MissingMethodException' at runtime
```

### Metadata
Explicit and synthesized parameterless struct instance constructors will be emitted to metadata.

Parameterless struct instance constructors will be imported from metadata.
_This is a breaking change for consumers of existing assemblies with structs with parameterless constructors._

Parameterless struct instance constructors will be emitted to ref assemblies regardless of accessibility to allow consumers to differentiate between no parameterless constructor an inaccessible constructor.

## See also

- https://github.com/dotnet/roslyn/issues/1029

## Design meetings

- https://github.com/dotnet/csharplang/blob/master/meetings/2021/LDM-2021-01-27.md#field-initializers
