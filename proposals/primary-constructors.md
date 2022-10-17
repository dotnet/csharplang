# Primary constructors

* [x] Proposed
* [ ] Prototype: Not started
* [ ] Implementation: Not started
* [ ] Specification: Not started

## Summary
[summary]: #summary

Classes and structs can have a parameter list, and their base class specification can have an argument list.
Primary constructor parameters are in scope throughout the class or struct declaration, and if they are captured by a function member or anonymous function, they are appropriately stored (e.g. as unspeakable private fields of the declared class or struct).

The proposal "retcons" the primary constructors already available on records in terms of this more general feature with some additional members synthesized.

## Motivation
[motivation]: #motivation

The ability of a class or struct in C# to have more than one constructor provides for generality, but at the expense of some tedium in the declaration syntax, because the constructor input and the class state need to be cleanly separated.

Primary constructors put the parameters of one constructor in scope for the whole class or struct to be used for initialization or directly as object state. The trade-off is that any other constructors must call through the primary constructor.

``` c#
public class C(bool b, int i, string s) : B(b) // b passed to base constructor
{
    public int I { get; set; } = i; // i used for initialization
    public string S // s used directly in function members
    {
        get => s;
        set => s = value ?? throw new NullArgumentException(nameof(X));
    }
    public C(string s) : this(0, s) { } // must call this(...)
}
```

## Detailed design
[design]: #detailed-design

This describes the generalized design across records and non-records, and then details how the existing primary constructors for records are specified by adding a set of synthesized members in the presence of a primary constructor.

### Syntax
Class and struct declarations are augmented to allow a parameter list on the type name, an argument list on the base class, and a body consisting of just a `;`:

``` antlr
class_declaration
  : attributes? class_modifier* 'partial'? class_designator identifier type_parameter_list?
  parameter_list? class_base? type_parameter_constraints_clause* class_body ';'?
  ;
  
class_designator
  : 'record' 'class'?
  | 'class'
  
class_base
  : ':' class_type argument_list?
  | ':' interface_type_list
  | ':' class_type  argument_list? ',' interface_type_list
  ;  
  
class_body
  : '{' class_member_declaration* '}' ';'?
  | ';'
  ;
  
struct_declaration
  : attributes? struct_modifier* 'partial'? 'record'? 'struct' identifier type_parameter_list?
    parameter_list? struct_interfaces? type_parameter_constraints_clause* struct_body ';'?
  ;

struct_body
  : '{' struct_member_declaration* '}' ';'?
  | ';'
  ;
```

***Note:*** These productions replace `record_declaration` in [Records](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#records) and `record_struct_declaration` in [Record structs](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#record-structs), which both become obsolete. 

It is an error for a `class_base` to have an `argument_list` if the enclosing `class_declaration` does not contain a `parameter_list`. At most one partial type declaration of a partial class or struct may provide a `parameter_list`. The parameters in the `parameter_list` must all be value parameters.

It is an error for a `class_body` or `struct_body` to consist of just a `;` unless the corresponding `class_declaration` or `struct_declaration` has a `record` keyword and a `parameter_list`.

A class or struct with a `parameter_list` has an implicit public constructor whose signature corresponds to the value parameters of the type declaration. This is called the ***primary constructor*** for the type, and causes the implicitly declared parameterless constructor, if present, to be suppressed. It is an error to have a primary constructor and a constructor with the same signature already present in the type declaration.

### Lookup

The [lookup of simple names](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1174-simple-names) is augmented to handle primary constructor parameters. The changes are highlighted in **bold** in the following excerpt:

> - Otherwise, for each instance type `T` ([§14.3.2](classes.md#1432-the-instance-type)), starting with the instance type of the immediately enclosing type declaration and continuing with the instance type of each enclosing class or struct declaration (if any):
>   - If `e` is zero and the declaration of `T` includes a type parameter with name `I`, then the *simple_name* refers to that type parameter.
>   - **Otherwise, if the declaration of `T` includes a primary constructor parameter `I` and the reference occurs within the `argument_list` of `T`'s `class_base` or within an initializer of a field, property or event, the result is the primary constructor parameter `I`**
>   - Otherwise, if a member lookup ([§11.5](expressions.md#115-member-lookup)) of `I` in `T` with `e` type arguments produces a match:
>     - If `T` is the instance type of the immediately enclosing class or struct type and the lookup identifies one or more methods, the result is a method group with an associated instance expression of `this`. If a type argument list was specified, it is used in calling a generic method ([§11.7.8.2](expressions.md#11782-method-invocations)).
>     - Otherwise, if `T` is the instance type of the immediately enclosing class or struct type, if the lookup identifies an instance member, and if the reference occurs within the *block* of an instance constructor, an instance method, or an instance accessor ([§11.2.1](expressions.md#1121-general)), the result is the same as a member access ([§11.7.6](expressions.md#1176-member-access)) of the form `this.I`. This can only happen when `e` is zero.
>     - Otherwise, the result is the same as a member access ([§11.7.6](expressions.md#1176-member-access)) of the form `T.I` or `T.I<A₁, ..., Aₑ>`.
>   - **Otherwise, if the declaration of `T` includes a primary constructor parameter `I`, the result is the primary constructor parameter `I`. It is an error if the reference does not occur within the body of an instance method or an instance accessor.**

The first addition corresponds to the change incurred by [primary constructors on records](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#primary-constructor), and ensures that primary constructor parameters are found before any corresponding fields within initializers and base class arguments.

The second addition allows primary constructor parameters to be found elsewhere within the class body, but only if not shadowed by members. It produces an error if the reference is not from within the body of an instance member or accessor. Note that instance constructors are excluded from this list.

Thus, in the following declaration:

``` c#
class C(int i)
{
    protected int i = i; // references parameter
    public int I => i; // references field
}
```

The initializer for the field `i` references the parameter `i` (as per the first addition), whereas the body of the property `I` references the field `i` (since member lookup comes before the second addition).

## Semantics

A primary constructor leads to the generation of an instance constructor on the enclosing type with the given parameters. If the `class_base` has an argument list, the generated instance constructor will have a `base` initializer with the same argument list.

All instance member initializers in the class body will become assignments in the generated constructor. This means that, unlike other classes, initializers will run *after* the base constructor has been invoked, not before.

If a primary constructor parameter is referenced from within an instance member, it is captured into the state of the enclosing type, so that it remains accessible after the termination of the constructor. A likely implementation strategy is via a private field using a mangled name. The private field is initialized by the generated constructor before , and all references to the parameter are replaced with references to the field.

If a primary constructor parameter is only referenced from within instance member initializers, those can directly reference the parameter of the generated constructor, as they are executed as part of it.

For instance this declaration:

``` c#
public class C(bool b, int i, string s) : B(b) // b passed to base constructor
{
    public int I { get; set; } = i; // i used for initialization
    public string S // s used directly in function members
    {
        get => s;
        set => s = value ?? throw new NullArgumentException(nameof(X));
    }
    public C(string s) : this(true, 0, s) { } // must call this(...)
}
```

Generates code similar to the following:

``` c#
public class C : B
{
    public int I { get; set; }
    public string S
    {
        get => __s;
        set => __s = value ?? throw new NullArgumentException(nameof(X));
    }
    public C(string s) : this(0, s) { ... } // must call this(...)
    
    // generated members
    private string __s; // for capture of s
    public C(bool b, int i, string s) : base(b)
    {
        __s = s; // capture s
        I = i; // run I's initializer
    }
}
```

It is an error for a non-primary constructor declaration to have the same parameter list as the primary constructor. All non-primary constructor declarations must use a `this` initializer, so that the primary constructor is ultimately called.

## Primary constructors on records

With this proposal, records no longer need to separately specify a primary constructor mechanism. Instead, record (class and struct) declarations that have primary constructors would follow the general rules, with these simple additions:

- For each primary constructor parameter, if a member with the same name already exists it must be an instance property or field, and it must be assignable to. If not, a public init-only auto-property of the same name is synthesized with a property initializer assigning from the parameter.
- A deconstructor is synthesized with out parameters to match the primary constructor parameters.
- If an explicit constructor declaration is a "copy constructor" - a constructor that takes a single parameter of the enclosing type - it is not required to call a `this` initializer, and will not execute the member initializers present in the record declaration.

## Drawbacks
[drawbacks]: #drawbacks

* The allocation size of constructed objects is less obvious, as the compiler determines whether to allocate a field for a primary constructor parameter based on the full text of the class. This risk is similar to the implicit capture of variables by lambda expressions.
* A common temptation (or accidental pattern) might be to capture the "same" parameter at multiple levels of inheritance as it is passed up the constructor chain instead of explicitly allotting it a protected field at the base class, leading to duplicated allocations for the same data in objects. This is very similar to today's risk of overriding auto-properties with auto-properties. 
* As proposed here, there is no place for additional logic that might usually be expressed in constructor bodies. The "primary constructor bodies" extension below addresses that.
* As proposed, execution order semantics are subtly different from within ordinary constructors, delaying member initializers to after the base calls. This could probably be remedied, but at the cost of some of the extension proposals (notably "primary constructor bodies").
* The proposal only works for scenarios where a single constructor can be designated primary.
* There is no way to express separate accessibility of the class and the primary constructor. An example is when public constructors all delegate to one private "build-it-all" constructor. If necessary, syntax could be proposed for that later.


## Alternatives
[alternatives]: #alternatives

### No capture

A much simpler version of the feature would prohibit primary constructor parameters from occurring in member bodies. Referencing them would be an error. Fields would have to be explicitly declared if storage is desired beyond the initialization code.

``` c#
public class C(string s)
{
    public S1 => s; // Nope!
    public S2 { get; } = s; // Still allowed
}
```

This could still be evolved to the full proposal at a later time, and would avoid a number of decisions and complexities, at the cost of removing less boilerplate initially, and probably also seeming unintuitive. 

### Explicit generated fields

An alternative approach is for primary constructor parameters to always and visibly generate a field of the same name. Instead of closing over the parameters in the same manner as local and anonymous functions, there would explicitly be a generated member declaration, similar to the public properties generated for primary construcor parameters in records. Just like for records, if a suitable member already exists, one would not be generated.

If the generated field is private it could still be elided when it is not used as a field in member bodies. In classes, however, a private field would often not be the right choice, because of the state duplication it could cause in derived classes. An option here would be to instead generating a protected field in classes, encouraging reuse of storage across inheritance layers. However, then we would not be able to elide the declaration, and would incur allocation cost for every primary constructor parameter.

This would align non-record primary constructors more closely with record ones, in that members are always (at least conceptually) generated, albeit different kinds of members with different accessibilities. But it would also lead to surprising differences from how parameters and locals are captured elsewhere in C#. If we were ever to allow local classes, for example, they would capture enclosing parameters and locals implicitly. Visibly generating shadowing fields for them would not seem to be a reasonable behavior.

Another problem often raised with this approach is that many developers have different naming conventions for parameters and fields. Which should be used for the primary constructor parameter? Either choice would lead to inconsistency with the rest of the code.

Finally, visibly generating member declarations is really the name of the game for records, but much more surprising and "out of character" for non-record classes and structs. All in all, those are the reasons why the main proposal opts for implicit capture, with sensible behavior (consistent with records) for explicit member declarations when they are desired.

### Remove instance members from initializer scope

The lookup rules above are intended to allow for the current behavior of primary constructor parameters in records when a corresponding member is manually declared, and to explain the behavior of the generated member when it is not. This requires lookup to differ between "initialization scope" (this/base initializers, member initializers) and "body scope" (member bodies), which the above proposal achieves by changing *when* primary constructor parameters are looked for, depending on where the reference occurs.

An observation is that referencing an instance member with a simple name in initializer scope always leads to an error. Instead of merely shadowing instance members in those places, could we simply take them out of scope? That way, there wouldn't be this weird conditional ordering of scopes.

This alternative is probably possible, but it would have some consequences that are somewhat far-reaching and potentially undesirable. First of all, if we remove instance members from initializer scope then a simple name that *does* correspond to an instance member and *not* to a primary constructor parameter could accidentally bind to something outside of the type declaration! This seems like it would rarely be intentional, and an error would be better.

Furthermore, *static* members are fine to reference in initialization scope. So we would have to distinguish between static and instance members in lookup, something we don't do today. (We do distinguish in overload resolution but that is not in play here). So that would have to also be changed, leading to yet more situations where e.g. in static contexts something would bind "further out" rather than error because it found an instance member.

All in all this "simplification" would lead to quite a downstream complication that no-one asked for.

## Possible extensions
[extensions]: #possible-extensions

These are variations or additions to the core proposal that may be considered in conjunction with it, or at a later stage if deemed useful.

### Primary constructor parameter access within constructors

The rules above make it an error to reference a primary constructor parameter within another constructor. This could be allowed within the *body* of other constructors, though, since the primary constructor runs first. However it would need to remain disallowed within the argument list of the `this` initializer.

``` c#
public class C(bool b, int i, string s) : B(b)
{
    public C(string s) : this(b, s) // b still disallowed
    { 
        i++; // could be allowed
    }
}
```

Such access would still incur capture, as that would be the only way the constructor body could get at the variable after the primary constructor has already run. 

The prohibition on primary constructor parameters in the this-initializer's arguments could be weakened to allow them, but make them not definitely assigned, but that does not seem useful.

### Allow constructors without a `this` initializer

Constructors without a `this` initializer (i.e. with an implicit or explicit `base` initializer) could be allowed. Such a constructor would *not* run instance field, property and event initializers, as those would be considered to be part of the primary constructor only.

In the presence of such base-calling constructors, there are a couple of options for how primary constructor parameter capture is handled. The simplest is to completely disallow capture in this situation. Primary constructor parameters would be for initialization only when such constructors exist.

Alternatively, if combined with the previously described option to allow access to primary constructor parameters within constructors, the parameters could enter the constructor body as not definitely assigned, and ones that are captured would need to be definitely assigned by the end of the constructor body. They would essentially be implicit out parameters. That way, captured primary constructor parameters would always have a sensible (i.e. explicitly assigned) value by the time they are consumed by other function members.

An attraction of this extension (in either form) is that it fully generalizes the current exemption for "copy constructors" in records, without leading to situations where uninitialized primary constructor parameters are observed. Essentially, constructors that initialize the object in alternative ways are fine. The capture-related restrictions would not be a breaking change for existing manually defined copy constructors in records, because records never capture their primary constructor parameters (they generate fields instead).

``` c#
public class C(bool b, int i, string s) : B(b)
{
    public int I { get; set; } = i; // i used for initialization
    public string S // s used directly in function members
    {
        get => s;
        set => s = value ?? throw new NullArgumentException(nameof(X));
    }
    public C(string s2) : base(true) // cannot use `string s` because it would shadow
    { 
        s = s2; // must initialize s because it is captured by S
    }
    protected C(C original) : base(original) // copy constructor
    {
        this.s = original.s; // assignment to b and i not required because not captured
    }
}
```

### Double storage warning

If a primary constructor parameter is passed to the base and *also* captured, there's a high risk that it is inadvertently stored twice in the object. It might make sense to issue a warning about this, if we can settle on good conditions for the warning, as well as recommended ways to shut it up if the code was intentional.

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

A lot of this scenario might be adequately be covered if we were to introduce "final initializers" which run after the constructors *and* any object/collection initializers have completed. However, argument validation is one thing that would ideally happen as early as possible.

Primary constructor bodies could also provide a place for allowing an access modifier for the primary constructor, allowing it to deviate from the accessibility of the enclosing type.

### Combined parameter and member declarations

A possible and often mentioned addition could be to allow primary constructor parameters to be annotated so that they would *also* declare a member on the type. Most commonly it is proposed to allow an access specifier on the parameters to trigger the member generation:

``` c#
public class C(bool b, protected int i, string s) : B(b) // i is a field as well as a parameter
{
    void M()
    {
        ... i ... // refers to the field i
        ... s ... // closes over the parameter s
    }
}
```

There are some problems: 
  - What if a property is desired, not a field? Having `{ get; set; }` syntax inline in a parameter list does not seem appetizing.
  - What if different naming conventions are used for parameters and fields? Then this feature would be useless.
  
This is a potential future addition that can be adopted or not. The current proposal leaves the possibility open.
