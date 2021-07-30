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

As with [class field initializers](https://github.com/dotnet/csharplang/blob/main/spec/classes.md#instance-field-initialization):
> A variable initializer for an instance field cannot reference the instance being created. 

### Constructors
A struct may declare a parameterless instance constructor.

A parameterless instance constructor is valid for all struct kinds including `struct`, `readonly struct`, `ref struct`, and `record struct`.

If the struct declaration does not contain any explicit instance constructors, and the struct has field initializers, the compiler will synthesize a `public` parameterless instance constructor.
The parameterless constructor may be synthesized even if all initializer values are zeros.

Otherwise, the struct (see [struct constructors](https://github.com/dotnet/csharplang/blob/main/spec/structs.md#constructors)) ...
> implicitly has a parameterless instance constructor which always returns the value that results from setting all value type fields to their default value and all reference type fields to null.

### Modifiers
A parameterless instance struct constructor must be declared `public`.
```csharp
struct S0 { }                   // ok
struct S1 { public S1() { } }   // ok
struct S2 { internal S2() { } } // error: parameterless constructor must be 'public'
```

Non-public constructors are ignored when importing types from metadata.

Constructors can be declared `extern` or `unsafe`.
Constructors cannot be `partial`.

### Executing field initializers
Execution of struct instance field initializers matches execution of [class field initializers](https://github.com/dotnet/csharplang/blob/main/spec/classes.md#instance-variable-initializers) with **one qualifier**:
> When an instance constructor has no constructor initializer, **or when the constructor initializer `this()` represents the default parameterless constructor**, ... that constructor implicitly performs the initializations specified by the _variable_initializers_ of the instance fields ... . This corresponds to a sequence of assignments that are executed immediately upon entry to the constructor ... . The variable initializers are executed in the textual order in which they appear in the ... declaration.

### Definite assignment
Instance fields (other than `fixed` fields) must be definitely assigned in struct instance constructors that do not have a `this()` initializer (see [struct constructors](https://github.com/dotnet/csharplang/blob/main/spec/structs.md#constructors)).

Definite assignment of struct instance fields is required within synthesized and explicit parameterless constructors.
```csharp
struct S0 // ok: no synthesized constructor
{
    int x;
    object y;
}

struct S1
{
    int x = 1;
    object y;  // error: field 'y' must be assigned
}

struct S2
{
    int x = 1;
    object y;
    public S2() { } // error: field 'y' must be assigned
}
```

### No `base()` initializer
A `base()` initializer is disallowed in struct constructors.

The compiler will not emit a call to the base `System.ValueType` constructor from any struct instance constructors including explicit and synthesized parameterless constructors.

### `record struct`
If a `record struct` does not contain a primary constructor nor any instance constructors, and the `record struct` has field initializers, the compiler will synthesize a `public` parameterless instance constructor.
```csharp
record struct R0;                      // no parameterless .ctor
record struct R1 { int F = 42; }       // synthesized .ctor: public R1() { F = 42; }
record struct R2(int F) { int F = F; } // no parameterless .ctor
```

A `record struct` with an empty parameter list will have a parameterless primary constructor.
```csharp
record struct R3();                // primary .ctor: public R3() { }
record struct R4() { int F = 42; } // primary .ctor: public R4() { F = 42; }
```

An explicit parameterless constructor in a `record struct` must call the primary constructor.
```csharp
record struct R5(int F)
{
    public R5() { } // error: must call 'this(int F)'
    public int F =  F;
}
```

### Fields
The implicitly-defined parameterless constructor will zero fields rather than calling any parameterless constructors for the field types. No warnings are reported that field constructors are ignored.
_No change from C#9._

```csharp
struct S0
{
    public S0() { }
}

struct S1
{
    S0 F; // S0 constructor ignored
}

struct S<T> where T : struct
{
    T F; // constructor (if any) ignored
}
```

### `default` expression
`default` ignores the parameterless constructor and generates a zeroed instance.
_No change from C#9._
```csharp
// struct S { public S() { } }

_ = default(S); // constructor ignored, no warning
```

### `new()`
Object creation invokes the parameterless constructor if public; otherwise the instance is zeroed.
_No change from C#9._
```csharp
// public struct PublicConstructor { public PublicConstructor() { } }
// public struct PrivateConstructor { private PrivateConstructor() { } }

_ = new PublicConstructor();  // call PublicConstructor::.ctor()
_ = new PrivateConstructor(); // initobj PrivateConstructor
```

A warning wave may report a warning for use of `new()` with a struct type that has constructors but no parameterless constructor.
No warning will be reported when using substituting such a struct type for a type parameter with a `new()` or `struct` constraint.
```csharp
struct S { public S(int i) { } }
static T CreateNew<T>() where T : new() => new T();

_ = new S();        // warning: no constructor called
_ = CreateNew<S>(); // ok
```

### Uninitialized values
A local or field of a struct type that is not explicitly initialized is zeroed.
The compiler reports a definite assignment error for an uninitialized struct that is not empty. 
_No change from C#9._
```csharp
NoConstructor s1;
PublicConstructor s2;
s1.ToString(); // error: use of unassigned local (unless type is empty)
s2.ToString(); // error: use of unassigned local (unless type is empty)
```

### Array allocation
Array allocation ignores any parameterless constructor and generates zeroed elements.
_No change from C#9._
```csharp
// struct S { public S() { } }

var a = new S[1]; // constructor ignored, no warning
```

### Parameter default value `new()`
A parameter default value of `new()` binds to the parameterless constructor if public (and reports an error that the value is not constant); otherwise the instance is zeroed.
_No change from C#9._
```csharp
// public struct PublicConstructor { public PublicConstructor() { } }
// public struct PrivateConstructor { private PrivateConstructor() { } }

static void F1(PublicConstructor s1 = new()) { }  // error: default value must be constant
static void F2(PrivateConstructor s2 = new()) { } // ok: initobj
```

### Type parameter constraints: `new()` and `struct`
The `new()` and `struct` type parameter constraints require the parameterless constructor to be `public` if defined (see [satisfying constraints](https://github.com/dotnet/csharplang/blob/main/spec/types.md#satisfying-constraints)).

The compiler assumes all structs satisfy `new()` and `struct` constraints.
_No change from C#9._
```csharp
// public struct PublicConstructor { public PublicConstructor() { } }
// public struct InternalConstructor { internal InternalConstructor() { } }

static T CreateNew<T>() where T : new() => new T();
static T CreateStruct<T>() where T : struct => new T();

_ = CreateNew<PublicConstructor>();      // ok
_ = CreateStruct<PublicConstructor>();   // ok

_ = CreateNew<InternalConstructor>();    // compiles; may fail at runtime
_ = CreateStruct<InternalConstructor>(); // compiles; may fail at runtime
```

`new T()` is emitted as a call to `System.Activator.CreateInstance<T>()`, and the compiler assumes the implementation of `CreateInstance<T>()` invokes the `public` parameterless constructor if defined.

_With .NET Framework, `Activator.CreateInstance<T>()` invokes the parameterless constructor if the constraint is `where T : new()` but appears to ignore the parameterless constructor if the constraint is `where T : struct`._

### Optional parameters
Constructors with optional parameters are not considered parameterless constructors.
_No change from C#9._
```csharp
struct S1 { public S1(string s = "") { } }
struct S2 { public S2(params object[] args) { } }

_ = new S1(); // ok: ignores constructor
_ = new S2(); // ok: ignores constructor
```

### Metadata
Explicit and synthesized parameterless struct instance constructors will be emitted to metadata.

Public parameterless struct instance constructors will be imported from metadata; non-public struct instance constructors will be ignored.
_No change from C#9._

## See also

- https://github.com/dotnet/roslyn/issues/1029

## Design meetings

- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-28.md#open-questions-in-record-and-parameterless-structs
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-10.md#parameterless-struct-constructors
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-01-27.md#field-initializers
