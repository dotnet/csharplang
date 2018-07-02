# readonly object initializers 

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Below

## Summary

This feature will allow for assigning of `readonly` fields and get only auto-implemented properties
in object initialezrs when the type being constructed is a `struct` or `ref struct`.

## Motivation

The intent of `readonly` in C# is to prevent the modification of a value after construction has 
completed. That is once the code calling `new` has a reference to the value there should be no 
further modification of `readonly` fields. Until the calling code has a reference to the object
modifications can continue to be made via `this`.

This makes it even easier to define usable `readonly struct` types in C#. The simplest form of
declaring the fields now creates a fully usable value:

``` csharp
readonly struct Point { 
    readonly int X;
    readonly int Y; 
}

var p = new Point { X = 42; Y = 13 };
```

This means authoring `readonly struct` types in C# is just as easy as creating the less
perferred mutable `struct` types. There is no longer a need to define a constructor which simply 
re-lists every field as a parameter and then assigns them one at a time. The definition of the 
field itself allows provides a mechanism for initialization

## Detailed Design 

### Language

The language will extend the object initialization rules to allow for an accessible readonly field 
or auto-implemented property to appear in the member initialization list. In such a context the 
members will allow for assignment just as it would inside a constructor of the object. 

The following restrictions will apply to this extension:

1. The type being instantiated must be a `struct` or `ref struct`. 
2. The constructor used in the initializer cannot have any `out` or `ref` parameters.

The (2) restriction exists because such a parameter could smuggle out `this` from the object 
constructor. That would make a reference to the constructed object visible to the code calling
`new` and hence allow it to observe mutations to the value.

### Compiler 

The compiler will change how the backing field of an auto-implemented property is emitted in the 
following ways:

- The accessibility of the backing field will match the accessibility of the property.
- The name of the field will be unspeakable (illegal C# identifier hence never imported) but it 
will be possible to map such fields to the properties that defined them.

Taken together this allows the C# compiler to translate a get only auto-implemented property into
a simple field initialization when used as an object initializer. 

``` csharp 
struct Name {
    // Backing field: public readonly string <FirstName>__BackingField;
    public string FirstName { get; }
    // Backing field: public readonly string <LastName>__BackingField;
    public string LastNmae { get; }
}

var name = new Name { FirstName = "Jared", LastName = "Parsons" };

// Emitted as 
var name = new Name { <FirstName>__BackingField = "Jared", <LastName>__BackingField = "Parsons" };
```

This does mean that the feature will only apply to auto-implemented properties that are 
re-compiled with a new compiler. The compiler today emits the fields as `private readonly` and 
hence will be inaccessible to calling code. Upon recompile the backing fields will be emitted 
as specified above.

### Verification

As is the case with several features added since C# 7.2 this feature will not pass any known 
peverify implementation as it makes no real distinction between read only and mutable memory. 
However it is still desirable for C#, and other languages, to emit IL that can be verified 
with appropriate adjustments to the verification spec. Eventually allowing for projects like
[ILVerify](https://github.com/dotnet/corert/tree/master/src/ILVerify) to take the place of 
peverify today. 

This rules could be adjusted to allow for mutation of a `readonly` field following an `.initobj` 
or `.call .ctor` instruction if:

1. The reciever of the call is a local 
1. The type of the reciever is a `struct` or `ref struct`
1. There are no `ref` locals which refer to that local

The `readonly` fields would remain mutable until one of the following occured:

1. The reciever was passed as any `ref` to another function except when being used as `this`
1. A `ref` local was taken to the local or any of its members

This may force conforming compilers to create extra locals in some cases to allow for a
successful verification of the emitted code.

## Considerations

### Type Parameters

The feature does not apply to type parameters constrained to a `struct`. There is just no case 
where such a type would have a field (`readonly` or not) or an auto-implemented property. In the
future if we allow for `struct` inheritance it's possible this could change and the decision 
re-evaluated.

## classes

This can't apply to `class` instances as it's much easier to smuggle a reference out of the 
constructor. The `this` value can be easily stored in a `static` making it accessible to the 
calling code long before the object initialization has completed. 

``` csharp
static C Global;

class C { 
    public readonly int Field;

    public C() { 
        Global = this;
    }
}

int SomeMethod() => Global.Field++;

// The instance is observed by caller while mutations are still in progress.
var c = new C() { Field = SomeMethod()}
```

A `struct` can't be smuggled out in such a way as it would require a direct copy to another 
instance of the same type or by boxing to an interface / object. In either case it would be a 
different value and not interesting here.

### Future Considerations

There is another proposal to allow for simple data types in the language via the `data` modifier.
Such types would have auto-matic equality, hashing, ToString, etc ... Combined with this proposal
and immutable data now has the simplest possible definition in C#:

``` csharp
public readonly data struct Point { 
    public readonly int X;
    public readonly int Y;
}
```

This type now has initialazation via object initializers, equality, hashing and ToString. As the 
type evolves by adding new fields these pieces of functionality will evolve with it.

Note: this begins to look very much like F# record types. 