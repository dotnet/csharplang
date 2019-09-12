# Primary constructors

* [x] Proposed
* [ ] Prototype: Not started
* [ ] Implementation: Not started
* [ ] Specification: Not started

## Summary
[summary]: #summary

Classes can have a parameter list, and when they do, their base class specification can have an argument list.
Primary constructor parameters are in scope throughout the class declaration, and if they are captured by a function member or anonymous function, they are stored as private fields in the class.

## Motivation
[motivation]: #motivation

It is common to have a lot of boilerplate in program initialization code. In the general case, a given piece of data `x` is mentioned many times:

- As a private field `_x`
- As a parameter `x` to a constructor
- In an assignment `_x = x;` of the field from the parameter in the constructor
- As a property `X`
- In the property setter `x = value;`
- In the property getter `return x;`

``` c#
class C
{
    private string _x;
    
    public C(string x)
    {
        _x = x;
    }
    public string X
    {
        get => _x;
        set { if (value == null) throw new NullArgumentException(nameof(X)); _x = value; }
    }
}
```

For properties that don't require validation or computation, the tedium can be reduced using auto-properties, thus cutting out the need to declare an explicit backing field for the property. But if your property requires any sort of logic beyond what an auto-property provides, the above is the best you an do.

Primary constructors instead reduce the overhead by putting constructor arguments directly in scope throughout the class, again obviating the need to explicitly declare a backing field. Thus, the above example would become:

``` c#
class C(string x)
{
    public string X
    {
        get => x;
        set { if (value == null) throw new NullArgumentException(nameof(X)); x = value; }
    }
}
```

In this example, the primary constructor reduces the number of named entities for `x` from three to two, obviating the `_x` backing field. It removes two out of three member declarations (keeping only the property declaration itself), and reduces the total number of mentions of `x`/`_x`/`X` from eight to five.


## Detailed design
[design]: #detailed-design

Classes can have a parameter list:

``` c#
public class C(int i, string s)
{
    ...
}
```

The parameter list causes a constructor to be implicitly declared for the class, with the same accessibility as the class itself.

``` c#
new C(5, "Hello");
```

Primary constructor parameters are in scope throughout the class body. If they are captured by a function member or anonymous function, they become stored as private fields in the class. If they are only used during initialization they will not be stored in the object.

``` c#
public class C(int i, string s)
{
	int[] a = new int[i]; // i not captured
    public int S => s;    // s captured
}
```

If a class with a primary constructor has a base class specification, that one can have an argument list. This serves as the argument list to a `base(...)` initializer of the implicitly declared constructor. If no argument list is provided, an empty one is assumed.

``` c#
public class C(int i, string s) : B(s)
{
    ...
}
```
The class can have explicitly defined constructors as well, but those all have to use a `this(...)` initializer. This ensures that the primary constructor is always called when a new instance is constructed.

All initializers in the class body will become assignments in the generated constructor. This means that, unlike other classes, initializers will run *after* the base constructor has been invoked, not before. In addition, the generated class will contain assignments to initialize any private fields that were generated to store primary constructor parameters that were captured by members. Those members are rewritten to use the private field instead of the parameter in a manner similar to closures for lambda expressions. The generated primary fields are initialized first, and then the initializer-generated assignments are executed in the order of appearance in the class.

For the above example, the effect of the class declaration is as if rewritten like this:

``` c#
public class C : B
{
    public C(int i, string s) : base(s)
    {
        __s = s;        // store parameter s for captured use
        a = new int[i]; // initialize a
    }
    int __s; // generated field for capture of s
    
    int[] a;
    public int S => __s; // s replaced with captured __s
}
```

The capture has similar restrictions to the capture of local variables by lambda expressions. For instance, `ref` and `out` parameters are allowed in primary constructors, but cannot be captured my member bodies.


## Drawbacks
[drawbacks]: #drawbacks

In rough order of significance.

* The proposal uses syntax that has also been proposed for positional records. If we desire both features, some accommodation is required. E.g. a `data` modifier on records has been proposed.
* The allocation size of constructed objects is less obvious, as the compiler determines whether to allocate a field for a primary constructor parameter based on the full text of the class. This risk is similar to the implicit capture of variables by lambda expressions.
* A common temptation (or accidental pattern) might be to capture the "same" parameter at multiple levels of inheritance as it is passed up the constructor chain instead of explicitly allotting it a protected field at the base class, leading to duplicated allocations for the same data in objects. This is very similar to today's risk of overriding auto-properties with auto-properties. 
* As proposed above, there is no place for additional logic that might usually expressed in constructor bodies. The "Primary constructor bodies" extension below addresses that.
* As proposed, execution order semantics are subtly different than with ordinary constructors. This could probably be remedied, but at the cost of some of the extension proposals (notably "Primary constructor bodies").
* The proposal only works when a single constructor can be designated primary.
* There is no way to have separate accessibility of the class and the primary constructor. For instance, in situations where public constructors all delegate to one private "build-it-all" constructor that would be needed. If necessary, syntax could be proposed for that later.


## Alternatives
[alternatives]: #alternatives

Full-on positional records may be an alternative, or may coexist with primary constructors, depending on the specifics. They would allow for *more* abbreviation in a *smaller* number of scenarios. So both are potentially useful, but having both may be overkill, unless they can be somewhat neatly integrated with each other.


## Possible extensions
[extensions]: #possible-extensions

These are variations or additions to the core proposal that may be considered in conjunction with it, or at a later stage if deemed useful.

### Primary constructor bodies

Constructors themselves often contain parameter validation logic or other nontrivial initialization code that cannot be expressed as initializers.

Primary constructors could be extended to allow statement blocks to appear directly in the class body. Those statements would be inserted in the generated constructor at the point where they appear between initializing assignments, and would thus be executed interspersed with initializers. For instance:

``` c#
public class C(int i, string s) : B(s)
{
    {
        if (i < 0) throw new ArgumentOutOfRangeException(nameof(i));
    }
	int[] a = new int[i];
    public int S => s;
}
```

### Initializer fields and initializer functions

In a class with a primary constructor we could consider field and method declarations without accessibility modifiers to be more like local variables and local functions:

* Just like primary constructor parameters the "initializer fields" would only be captured into an actual private field if they were used in function members.
* The "initializer functions" would only be considered to capture primary constructor parameters and initializer fields if they were themselves used in other function members. If not captured, they could be generated in a more optimal fashion, like local functions.
* Just like primary constructor parameters they would not be available via member access, but only as a simple name.

This could be used for temporary variables and helper functions that are only relevant to initialization:

``` c#
public class C(string s)
{
    int size = s.Length;             // not captured
    int[] Create() => new int[size]; // not captured
	int[] a = Create();
    ...
}
```

This may be too subtle, especially since the absence of accessibility modifiers elsewhere simply means `private`. 

### Initializer statements

A radical combination of the above to extensions would be to simply allow statements directly in the class body. Such statements are exactly as the interspersed constructor bodies proposed above, except they don't need to be enclosed in `{ }`. For this to be sufficiently useful, "local" variables and helper functions would need to also be expressible at the top level of the class, in the manner explored in the extension "Initializer fields and initializer functions" above.


### Member access

The core proposal treats primary constructor parameters as parameters that can only be referred as simple names. An alternative is to allow them to be referenced as if they were private fields, i.e. with a member access, *even* if they are sometimes not generated as fields. This would allow them to be referenced as `this.x` when shadowed by local variables, and accessed from a different instance as `other.x`.

If applied to the "initializer fields and initializer functions" extension this would also reduce the degree to which those were different from ordinary private members. The only difference would then be that the compiler is free to elide them from the object if only used during initialization.

