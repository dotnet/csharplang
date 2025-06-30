# Type Unions for C#

Champion issue: <https://github.com/dotnet/csharplang/issues/8928>

## Summary
[summary]: #summary

A proposal for type unions (aka discriminated unions) in C#.

## Motivation

When developing software you may encounter situations where the values that you want to store in a variable are not always the same kind each time through. While you are usually not concerned about storing strings and numbers in the same spot, you may need to store one of a few related types depending on what that data is meant to represent at that moment.

For example, your application may have both a customer and a supplier definition that share only some of the same properties and you may need to perform a similar operation on both in a fashion that depends on the differences. 

Typically, this is where you might choose to distribute those specialized implementations into the types themselves and expose them through common abstract methods or interfaces. However, this is only good practice when those types exist primarily for the purpose of the operation or it makes sense for the operation to appear as an intrinsic part of the type. If the types have a broader purpose, polluting them with methods like this can be undesirable. 

The alternative is to make the same logic handle both types, and if you do this, at some point you will need to declare a parameter or variable that can contain either.  

You might think you can still solve this through inheritance, by defining both `Customer` and `Supplier` as classes in a hierarchy with a common base type like `Contact`. However, if you are not able to define such a relationship, because either you don't own the definition of these types, or you have too many similar situations and can only solve one of them through inheritance or you choose to not leak the requirements of the specific operation into the definition of the data, the only easy choice you have is to declare the variable as object and let it be just anything.

While this may work, it leaves you policing your code through documentation and comments. If you are brave, you can devise such things as special-case hierarchies of wrapper types to put around your values, or custom aggregate types that act as guardians around all the kinds of values you want to possibly store in the variable,
which is time consuming and cumbersome, especially if you have many similar situations but they all involve different sets of types.

It would be better if C# gave you a way to declare a type that allows you to store one of a limited number of other types in the same place, and let it do all the hard work guarding the variables for you.

Many other languages already do this. They typically call these special types discriminated unions, tagged unions, sum types or type unions.
All of them solve the problem of allowing a single variable to hold values of one or more limited forms.

It is time C# had a feature that did this too.

### Solutions

You might imagine that the most appropriate implementation for union types in C# is as a hierarchy of classes with an abstract base representing the union itself and all the specific cases of the union as derived classes, just like you or I might make to solve the problem on our own, because it fits really well with the concepts already in the language. This usually works well when you have a specific use case in mind as you design a specific set of classes to solve that specific problem. However, there are some drawbacks to implementing unions as class hierarchies.

One is the inability to constrain the hierarchy, as object-oriented languages are usually open for inheritance.

> I know there are only three possible subtypes, why does the compiler require me to have a default in my switch expression?

Another is the inability to represent unions of unrelated types that exist outside a single hierarchy or even to restrict values to a subset of types from within the same hierarchy.

> I want this parameter to be restricted to only Cats and Dogs, not all Animals.

Because of the class hierarchy implementation, the only way to include a value from a type that already exists is to use a class that is part of the union hierarchy to wrap the value.

> I either have to type these fields as object and trust myself and my team to always do the right thing, or wrap my values in new class instances each time I want to store them in the variable.

And lastly, classes in C# require allocation to represent and cannot contain values such as ref types, which may be requirements for specific scenarios.

> I wish I could use unions in my graphics pipeline, but they cause too much gen 0.

For these reasons, it may be necessary to have more than one kind of union,
as it may not be possible to satisfy some use cases without compromising others, and if there are multiple kinds it is best to strive to make them appear to look and work the same as as much as possible.

This proposal attempts to provide solutions to all use cases by declaring four categories for them to fall into, listing some examples for each.

- [Standard](#standard---union-classes) - Use cases where the union and its members can be defined together, because they have a predominant reason to belong together and you intend to use the members as classes on their own. Allocation is not a problem because you would have been allocating the classes regardless.
    - Protocols, serialization and data transfer types
    - UI data models (XAML)
    - Syntax trees
    - Infrequently changed state machine states
    - Other polymorphic data models
    - Values that last a while in union form (fields/properties)

- [Specialized](#specialized---union-structs) - Use cases that need to avoid allocations or require use of special types and are willing to accept some limitations to achieve it.
    - Allocated in contiguous arrays
    - Mapped over blocks of memory (interop)
    - Frequently changed state machine states
    - Values that last briefly in union form (arguments/return values)
    - Library types with potentially specialized uses
    
- [Ad Hoc](#ad-hoc---ad-hoc-unions) - Use cases that require unions to be formed from existing, possibly unrelated, types and where similarly declared unions with the same member types are interchangeable with one another.
    - Same examples as standard.

- [Custom](#custom-unions) - Use cases that do not fit well with the other categories.
    - Already existing types and hierarchies that cannot easily be redefined.
    - Custom storage layouts.
    - Custom API shapes and behaviors.

*Note: Pre-declared unions like Option and Result are proposed in the [Common Unions](#common-unions) section.*

*Note: Many of the examples are written in a shorthand syntax made possible by related proposals
briefly described in the [Related Proposals](#related-proposals) section.*

----

## Standard - Union Classes

A union class is a named type union that declares all its member types in a single self-contained declaration.

### Declaration
A union class is declared similar to an enum, 
except each member is a type that itself can hold state in one or more state variables.

    union U 
    {
        A(int x, string y);
        B(int z);
        C;
    }

For each member, only the name and the list of state variables may be specified.

### Construction

Union classes are constructed via allocation of the member type.

    U u = new A(10, "ten");

The type of the constructed member is the member type `A`.
It is converted to type `U` when assigned to variable `u`.

### Deconstruction

Union classes are deconstructed by type tests and pattern matching.

    if (u is A a) { ... }

    if (u is A(var x, var y)) { ... }

    if (u is A { y: var y }) { ... }


### Exhaustiveness

Union classes are considered exhaustive.
If all member types are accounted for in a switch expression or statement, no default case is needed.

    var x = u switch { 
        A a => a.x,
        B b => b.z,
        C c => 0
        };

### Nullability

Nulls can be included in a union class variable using the standard nullability notation.

    U? u = null;

### Implementation

A union class is implemented as an abstract record class with the member types as nested derived record classes.

    [Closed]
    abstract record U 
    {
        public record A(int x, string y) : U;
        public record B(int z) : U;
        public record C : U { public static C Singleton = new C(); };
    }

*Note: The `Closed` attribute allows the language to understand that the type hierarchy is closed to sub-types declared outside the base type's module.
See the following section [Related Proposals](#related-proposals).*

*Note: Nested member types may be referred to without qualification using related proposal.*

----

## Specialized - Union Structs

Similar to a union class, a union struct is also a named type union that declares all its member types in a single self-contained declaration, except the union and the member types are all structs and are able to be used without heap allocation.

### Declaration
A union struct is declared similarly to a union class, with the addition of the `struct` keyword.

    union struct U 
    {
        A(int x, string y);
        B(int z);
        C;
    }

For each member, only the name and the list of state variables may be specified.

### Construction

Union structs are constructed via allocation of the member type.

    U u = new A(10, "ten");

The type of the constructed member is the member type `A`.
It is converted to type `U` when assigned to variable `u`.

### Deconstruction

Union structs are deconstructed by type tests and pattern matching.

    if (u is A a) { ... }

    if (u is A(var x, var y)) { ... }

    if (u is A { y: var y }) { ... }

### Exhaustiveness

Union structs are considered exhaustive.
If all member types are accounted for in a switch expression or statement, no default case is needed.

    var x = u switch { 
        A a => a.x,
        B b => b.z,
        C c => 0
        };

### Nullability

Nulls can be included in a union struct variable using the standard nullability notation.

    U? u = null;

### Default

Union structs can be in an undefined state due to being unassigned or assigned default. This state will not correspond to any declared member type, 
leading to a runtime exception in a switch that relies on exhaustiveness.

    U u = default;  

    // switch throws, since not A, B or C
    var x = u switch 
    {
        A a => a.x,
        B b => b.z,
        C c => 0
    }

To help avoid this, the compiler will produce a warning when a struct union is assigned default.

    // warning: default not a valid state
    U u = default;  

You may also avoid the warning by declaring a default state for the union struct, 
associating a member type as the default.

    union struct U
    {
        A(int x, string y);
        B(int z);
        C = default;
    }


### Implementation

A union struct is implemented as a struct with nested record structs as member types and an API that converts the member types to and from the aggregate union struct. The interior layout of the union struct is chosen to allow for efficient storage of the data found within the different possible member types with tradeoffs between speed and size chosen by the compiler. 

    [Union]
    struct U 
    {
        public record struct A(int x, string y);
        public record struct B(int z);
        public record struct C { public static C Singleton = default; };

        public static implicit operator U(A value) {...};
        public static implicit operator U(B value) {...};
        public static implicit operator U(C value) {...};

        public static explicit operator A(U union) {...};
        public static explicit operator B(U union) {...};
        public static explicit operator C(U union) {...};

        public bool TryGetA(out A value) {...};
        public bool TryGetB(out B value) {...};
        public bool TryGetC(out C value) {...};

        public enum UnionKind { A = 1, B = 2, C = 3 };
        public UnionKind Kind => {...};
    }

*Note: The `Union` attribute identifies this type as a union struct type.*

*Note: A union struct with a default state has its corresponding `UnionKind` declared as 0.*

*Note: this full generated API of the union struct is not shown.*

### Type Tests
Whenever a type test is made against a known union struct, the union structs API is invoked to determine the outcome instead of 
testing the union struct's type itself.

For example, the expression:

    u is A a

is translated to:

    u.TryGetA(out var a)

And the switch expression:

    u switch {
        A a => a.x,
        B b => b.z,
        C c => 0
    }

translates to:

    u.Kind switch {
       U.UnionKind.A when u.TryGetA(out var a) => a.x,
       U.UnionKind.B when u.TryGetB(out var b) => b.z,
       U.UnionKind.C when u.TryGetC(out var c) => 0,
       _ => throw ...;
    }

### Boxed Unions

A union struct that is boxed is a boxed union struct, not the boxed value of one of its member types. You may never need to be concerned about this since the primary use case for a union struct is to avoid boxing. However, it may be necessary on occasion to box a union struct.

Normally, type tests for two unrelated structs would never succeed when the boxed value is one type and the test is for the other. However, union struct types and their members are related to each other and so it is possible to type test and unbox a boxed union struct into one of its member types.

    U u = ...;
    object value = u;

    // will succeed since A is known to be a member type of U
    if (value is A a) {...}

Translates to:

    if (value is A a || (value is U u && u.TryGetA(out a))) {...}
    
Likewise, a boxed member type can be tested and unboxed into a union struct.

    A a = ...;
    object value = a;

    // will succeed since U is known to have member type A
    if (value is U u) {...}

Translates to:

    if (value is U u || U.TryCreate(value, out u)) { ... }

However, when neither type in the test is statically known to be related to a union struct, the type test will fail.

    bool IsType<T>(object value) => value is T;

    U u = new A(...);

    // always fails
    if (IsType<A>(u)) {...}

This is because the language will not special case the type test when no union struct members are involved, since its not clear which type to check for. Checks for a common union interface could be made to work, but that would unduly impact the vast majority of type tests that do not involve union structs.

### Reflection 

You may be required to interact with union structs when using reflection. 

For example, you may need to pass a union struct to a method, but you have a boxed instance of the member type `A` and not the union struct type `U`. You will need to convert the boxed `A` value to a boxed `U` value.

The struct union feature provides utility methods to help convert between boxed union structs and boxed member types at runtime.

    public static class TypeUnion
    {
        public bool TryConvert(Type unionType, object value, out object? boxedUnion);
        public bool TryConvert<TUnion>(object value, out TUnion union);
        public object? GetValue(object? boxedUnion);
    }

*Note: Union classes and ad hoc unions do not require conversion since they are already in the correct form for reflection use.*

### Ref Union Structs

A union struct with the `ref` modifier may contain state variables that are refs or ref structs.

    ref union struct U
    {
        A(ref int x);
        B(ReadOnlySpan<char> y);
        C;
    }

In this case, both the implementation of the union and the member types with ref struct values 
are translated to ref structs.

    ref struct U
    {
        public ref struct A { public ref int x; public A(ref int x) {...}; }
        public ref struct B { public ReadOnlySpan<char> y; public B(ReadOnlySpan<char> y) {...} }
        public record struct C { public static C Singleton = default; }
        ...
    }

*Note: The impacted member types may be able to continue to be record structs if a ref record struct type is added to C#.*


----

## Ad Hoc - Ad Hoc Unions

Ad hoc unions are anonymous unions of types declared elsewhere.

### Syntax

You refer to an ad hoc union using the `or` pattern syntax with parentheses.

    (A or B or C)

### Naming

You may desire to refer to an ad hoc union using a common name.
To do this, use a file or global using alias.

    global using U = (A or B or C);

### Construction

Ad hoc unions are constructed by assigning an instance of one of the union's member types to a variable of the ad hoc union type.

    record A(int x, string y);
    record B(int z);
    record C() { public static C Singleton = new C(); };

    (A or B or C) u = new A(10, "ten");

The type of the constructed member is the member type `A`.
It is converted to type `(A or B or C)` when assigned to variable `u`.

### Deconstruction

Ad hoc unions are deconstructed using type tests and pattern matching.

    if (u is A a) {...}

    if (u is A(var x, var y)) { ... }

### Exhaustiveness

Ad hoc unions are considered exhaustive.
If all member types are accounted for in a switch expression or statement, no default case is needed.

    var x = u switch { 
        A a => a.x,
        B b => b.z,
        C c => 0
        };

### Nullability

Nulls can be included in an ad hoc union using the standard nullability notation.

    (A or B)? x = null;

### Equivalence

Ad hoc unions with the same member types (regardless of order) are understood by the compiler to be the same type.

    (A or B) x = new A(10, "ten");
    (B or A) y = x;

### Assignability

#### Super and subset assignability 

Ad hoc unions with the same or a subset of member types are assignable to ad hoc unions with a super set, without runtime checks.

    (A or B) x = new A(10, "ten");
    (A or B or C) y = x;

Ad hoc unions with a superset of member types are assignable to ad hoc unions with a subset, with explicit coercions and runtime checks.

    (A or B or C) x = new A(10, "ten");
    var y = (A or B)x;

#### Subtyping assignability

A value of one ad hoc union type can be implicitly coerced to a value of another ad hoc union type without runtime checks if all member types of the source union are the same type or a sub type of at least one of the target union's member types.

    (Chihuahua or Siamese) pet = ...;
    (Cat or Dog) animal = pet;

Otherwise an explicit coercion can be made involving runtime checks if at least one member type of the source union is a sub type of one of the member types of the target union.

    (Cat or Chihuahua) mostlyCats = ...;
    (Dog or Siamese) mostlyDogs = (Dog or Siamese)mostlyCats;

For the purposes of assignability, you may consider a type that is not an ad hoc union to be an ad hoc union of a single type.

    Siamese pet = ...;
    (Cat or Chihuahua) mostlyCats = pet;
    Dog dog = (Dog)mostlyCats;

*Note: This works for implemented interfaces too.*

#### Generalized Coercions

A value of a type can be implicitly coerced to a union type if an implicit coercion from that type to one of the union's member types exists.

    (string or double) value = 10;

A value of an ad hoc union can be implicitly coerced to a type if all member type's of the union can be implicitly coerced to the type.

    (int or short) value = 10;
    double value2 = value;

A value of an ad hoc union can be explicitly coerced to a type if one of the member types is coercible to the type.

    (string or double) value = 10.0;
    int value2 = (int)value;

A value of an ad hoc union type can be implicitly coerced to another ad hoc union type if all member types of the source union type can be implicitly coerced to one of the member types of the target union type.

    (int or short) value = 10;
    (float or double) value2 = value;

A value of an ad hoc union type can be explicitly coerced to another ad hoc union type if at least one member of the source union type can be explicitly coerced to one of the member types of the target union type.

    (float or double) value = 10.0;
    (int or short) value2 = (int or short)value;

*Note: Need rule for which coercion if multiple are possible.*

*Note: This assignability relationship is not intended to be a sub typing relationship. One ad hoc union is not a sub type of another ad hoc union.*

### Interchangeability

Ad hoc unions with the same member types are interchangeable through generics and array elements.

For example, constructing an array of ad hoc unions of generic type parameters,
will return an array that is compatible with an array of ad hoc unions of concrete types.

    (T1 or T2)[] F<T1, T2>(T1 v1, T2 v2) => new (T1 or T2)[] { v1, v2 };

    (Dog or Cat)[] pets = F<Dog, Cat>(rufus, petunia);

Likewise, 

    IReadOnlyList<(Cat or Dog)> pets = F<Dog, Cat>(rufus, petunia);

### Covariance and Contravariance

Ad hoc union types used as with generic type arguments can be used with covariance and contra-variance, if all member types of the two ad hoc unions involved have sub type relationships with members of the other.

 I'd tell you the specific rules, but it hurts my head to think about it.

    void Groom(IEnumerable<(Dog or Cat)> animals) => ...;

    List<(Chihuahua or Siamese)> pets = ...;
    Groom(pet);

*Note: Have Mads write this part.*

### Patterns

Ad hoc unions may be used in pattern matching and behave similarly to the `or` pattern, and may also have a variable declaration.

    if (u is Dog or Cat) { ... }  // normal 'or' pattern

    if (u is (Dog or Cat)) { ... }  // type test with ad hoc union
    
    if (u is (Dog or Cat) pet) {...}  // type test with ad hoc union and variable

*Note: assigning into an ad hoc union variable may cause boxing of value types*

### Inference

Ad hoc unions can be inferred from context when that inference would not otherwise have been possible.

The conditional and switch expressions can have result types inferred as ad hoc unions from the constituent expressions.

    Dog rufus = ...;
    Cat petunia = ...;
    Bird polly = ...;

    // u : (Dog or Cat or Bird)
    var u = 
          x == 1 ? rufus
        : x == 2 ? petunia
        : polly;

Likewise, the return type of a lambda expression can also be inferred using an ad hoc union of the return types of the lambda body.

    T M<T>(F<int, T> f) => f(2);

    (Dog or Cat or Bird) pet = 
        M(x => 
        {
            if (x == 1)
                return rufus;
            else if (x == 2)
                return petunia;
            return polly;
        });


*Note: this may cause boxing of value types*

### Implementation

Ad hoc unions are implemented through erasure and runtime checks.

    (A or B) ab = new A(10, "ten");

translates to:

    object ab = new A(10, "ten");

#### Runtime checks

Assignments that are not statically known to be correct require runtime checks.
The compiler generates a custom method for each unique ad hoc union used in the module.

    object value = ...;
    var ab = (A or B)value;
    
translates to:

    object value = ...;
    object ab = <ValidateAB>(value);

    object <ValidateAB>(object? value) =>
        value is A or B ? value : throw ...;

*note: Parameters are not checked at entry of a method.*

#### Metadata encoding

The type of the ad hoc union is encoded in metadata using custom attributes.

    void M((A or B) x);

translates to:

    void M([AdHocUnion([typeof(A), typeof(B)])] object x);

*note: The details of this attribute are not yet specified.*

#### Overloading

Since all ad hoc unions erase to the same type, true runtime overloading of methods with ad hoc union parameters is not possible.

    public void Wash((Cat or Dog) pet) { ... }
    public void Wash((Compact or Sedan) car) { ... }

This is still an open area of discussion.

----

## Custom Unions

If you need to declare a union type that cannot be specified as a union class or a union struct,
due to specific behaviors that cannot be specified via the union syntax or for other reasons,
you may declare you own custom class or struct and have C# recognize it as a custom union type.

For example, if your union is specified as a class hierarchy, you can give it the same exhaustiveness behavior
as union classes using the `Closed` attribute.  It will be functionally the same as a union class.

    [Closed]
    public class U { ... }
    public class A(int x, string y) : U { ... }
    public class B(int z) : U { ... }

If your union is implemented as a struct wrapper with specialized storage rules, you can annotate your struct with the `Union` attribute
and as long as you provide API's following the union pattern, your struct will be functionally the same as a union struct.

    [Union]
    public struct U    
    {
        public record struct A(int x, string y);
        public record struct B(int z);

        public bool TryGetA(out var A a) { ... }
        public bool TryGetB(out var B b) { ... }
    }

If your union does not include member types or uses a different API pattern
you may provide the API the compiler is expecting via extensions.

    [Union]
    public struct U
    {
        public bool IsA { get; }
        public void GetA(out int x, out string y);
        public bool IsB { get; }
        public void GetB(out int z);
    }

    public implicit extension UX for U 
    {
        public record struct A(int x, int y);
        public record struct B(int z);

        public bool TryGetA(out A a) { ... }
        public bool TryGetB(out B b) { ... }
    }

*Note: The full union struct API pattern is not yet specified.*

*Note: You cannot customize the behavior of an ad hoc union, other than your ability to modify the behaviors of
the individual member types.*

----

## Common Unions

### Option

Option is a struct union, similar to the type of the same name or purpose found in other languages.
It is used to represent a value that may exist or not.

    public union struct Option<TValue>
    {
        Some(TValue value);
        None = default;
    }

usage:

    Option<string> x = new Some("text");
    Option<string> y = None;

    if (x is Some(var value)) {...}

    var v = x is Some(var value) ? value : 0;

*Note: Option type not fully specified.*

### Result

Result is a struct union, similar to the type of the same name or purpose found in other languages.
It is used to return either a successful result or an error from a function.

    public union struct Result<TValue, TError>
    {
        Success(TValue value);
        Failure(TError error);
    }

usage:

    Result<string, string> x = Success("hurray!");
    Result<string, string> y = Failure("boo");

    switch (x)
    {
        case Success(var value): ...;
        case Failure(var error): ...;
    }

*Note: Result type not fully specified.*

----

## Related Proposals

These are proposed (or yet to be proposed) features that are presumed to exist by this proposal.

### Closed Hierarchies

A `Closed` attribute applied to an abstract base type declares the closed set of sub-types to be all the 
sub-types in the declaring module.

The compiler errors when sub types are declared outside the declaring module.

A closed hierarchy is treated as exhaustive by the compiler.
If all sub-types are accounted for in a switch expression or statement, no default case is needed.

### Singleton values

Types that are singletons (with a static `Singleton` property) can be used as values in non-type contexts by implicitly accessing the property.

Instead of:
    
    var x = U.C.Singleton;

You can write:

    var x = U.C;

### Nested Member Shorthand

Names that are otherwise not bound, can be bound to static members or nested types of the target type.

Instead of:

    Color color = Color.Red;

You can write:

    Color color = Red;

Instead of:

    U u = new U.A(10, "ten");

You can write:

    U u = new A(10, "ten");

----

## Q & A

Q: If I can easily declare my own nested hierarchy of records, are union classes needed?  
A: No, not really. However, it is nice to have a concise syntax that is easy to transition to union structs when necessary with the addition of a single modifier.

Q: If union structs can use more kinds of types with little or no allocation why do union classes and ad hoc unions exist?  
A: While union structs are necessary in some scenarios, they do not work well in others.

- They may not cause their own allocations but that does not mean they perform better.  
Union structs typically have a larger footprint on the stack that is normally copied when assigned, passed or returned.

- Union structs do not work well as a solution for anonymous ad hoc unions, since they are not easily interchangeable.  
For example, a union of statically known generic type parameters is a different type at runtime that the same union with the statically known member types,
and an array of one is not interchangeable with an array of the other.

- Union structs have problems with type tests, casts and pattern matching when boxed or represented in code statically as a generic type parameter.

Q: Why are there no tagged unions?  
A: Union structs are both tagged unions and type unions. 
Under the hood, a union struct is a tagged union, even exposing an enum property that is the tag to enable faster compiler generated code, but in the language it is presented as a type union to allow you to interact with it in familiar ways, like type tests, casts and pattern matching.

Q: Can the compiler skip constructing a union struct's member type if I immediately assign it to union struct variable?  
A: Yes, this is an expected optimization.

Q: Can the compiler skip copying my union struct state variables into a member type if I deconstruct my union directly to variables?   
A: Yes, this is an expected optimization.

Q: Is the union struct, like the union class, the base type of its member types?  
A: No, struct types do not allow for actual inheritance.
Logically, the union struct acts as the base type such that there are automatic conversions between them,
but this illusion falls apart eventually as the relationship extends no further into the type system and runtime.

Q: Can I declare ad hoc unions with names?  
A: Not at this time. If you need a name to help describe or avoid repeating a lengthy union, use a global using alias.

Q: Can I have an ad hoc union that does not box value types?  
A: Ad hoc unions box value types. If you need to avoid boxing use a union struct.

Q: Can I have an ad hoc union that includes ref types?  
A: Ad hoc unions cannot include ref types. If you need to include ref types use a union struct.

Q: If variables typed as object are bad, why are ad hoc unions erased to object?  
A: Using `object` is the solution developers are most likely using today. 
The ad hoc union feature is an improvement.
Represented as an object, the value is in the best form to be understood by the type system at runtime, it is only lacking static type safety at compile time.
Using a wrapper type to enforce safety interferes with simple operations like type tests and casts.
The Ad Hoc Union feature adds support for understanding ad hoc union types at compile time and generates validation checks to help keep your code correct at runtime.

Q: Can I access common properties and methods of the ad hoc union without handling each type case?  
A: No, you can only access the values of an individual type by first successfully converting the union to that type.

Q: Why don't you just use the same kinds of unions that are in F#?  
A: F# has union types that correspond to both the union classes and union structs definitions in this specification, with the difference of treating the members as types in the language instead of tag states with associated state variables.
Ad hoc unions are similar to the kind of type unions found in Typescript.

Q: Why do I need the `Option` type if I can do the same thing with nulls and nullable reference types?  
A: You many not need the `Option` type at all if you are comfortable with using nulls for the same purpose. Some developers prefer an option type as it has stronger enforcement than nullable types do in C#.

Q: Will the `Option` and `Result` type also include the monadic behaviors that these types enable in F#?  
A: No, C# will not include any monadic behaviors in the language for these types at this time.

Q: Why do I need the `Result` type when I already have exception handling?  
A: Many of the use cases for the `Result` type are solved using exception handling in C#. However, you may prefer to avoid exception handling and require the caller to deal with errors explicitly when errors are expected and occur commonly at runtime.

Q: Types similar to the `Option` and `Result` types are already available in other 3rd party libraries. Why are you adding them to the runtime?  
A: Many developers have asked us to include these types in the runtime to standardize them for interoperability between libraries.
