# Required Members

## Summary

This proposal adds a way of specifying that a property or field is required to be set during object initialization, forcing the instance creator to provide an initial value for the
member in an object initializer at the creation site.

## Motivation

Object hierarchies today require a lot of boilerplate to carry data across all levels of the hierarchy. Let's look at a simple hierarchy involving a `Person` as might be defined in
C# 8:

```cs
class Person
{
    public string FirstName { get; }
    public string MiddleName { get; }
    public string LastName { get; }

    public Person(string firstName, string lastName, string? middleName = null)
    {
        FirstName = firstName;
        LastName = lastName;
        MiddleName = middleName ?? string.Empty;
    }
}

class Student : Person
{
    public int ID { get; }
    public Person(int id, string firstName, string lastName, string? middleName = null)
        : base(firstName, lastName, middleName)
    {
        ID = id;
    }
}
```

There's lots of repetition going on here:

1. At the root of the hierarchy, the type of each property had to be repeated twice, and the name had to be repeated four times.
2. At the derived level, the type of each inherited property had to be repeated once, and the name had to be repeated twice.

This is a simple hierarchy with 3 properties and 1 level of inheritance, but many real-world examples of these types of hierarchies go many levels deeper, accumulating larger and
larger numbers of properties to pass along as they do so. Roslyn is one such codebase, for example, in the various tree types that make our CSTs and ASTs. This nesting is tedious
enough that we have code generators to generate the constructors and definitions of these types, and many customers take similar approaches to the problem. C# 9 introduces records,
which for some scenarios can make this better:

```cs
record Person(string FirstName, string LastName, string MiddleName = "");
record Student(int ID, string FirstName, string LastName, string MiddleName = "") : Person(FirstName, LastName, MiddleName);
```

`record`s eliminate the first source of duplication, but the second source of duplication remains unchanged: unfortunately, this is the source of duplication that grows as the
hierarchy grows, and is the most painful part of the duplication to fix up after making a change in the hierarchy as it required chasing the hierarchy through all of its locations,
possibly even across projects and potentially breaking consumers.

As a workaround to avoid this duplication, we have long seen consumers embracing object initializers as a way of avoiding writing constructors. Prior to C# 9, however, this had 2
major downsides:

1. The object hierarchy has to be fully mutable, with `set` accessors on every property.
2. There is no way to ensure that every instantation of an object from the graph sets every member.

C# 9 again addressed the first issue here, by introducing the `init` accessor: with it, these properties can be set on object creation/initialization, but not subsequently. However,
we again still have the second issue: properties in C# have been optional since C# 1.0. Nullable reference types, introduced in C# 8.0, addressed part of this issue: if a constructor
does not initialize a non-nullable reference-type property, then the user is warned about it. However, this doesn't solve the problem: the user here wants to not repeat large parts
of their type in the constructor, they want to pass the _requirement_ to set properties on to their consumers. It also doesn't provide any warnings about `ID` from `Student`, as that
is a value type. These scenarios are extremely common in database model ORMs, such as EF Core, which need to have a public parameterless constructor but then drive nullability of the
rows based on the nullability of the properties.

This proposal seeks to address these concerns by introducing a new feature to C#: required members. Required members will be required to be initialized by consumers, rather than by
the type author, with various customizations to allow flexibility for multiple constructors and other scenarios.

## Detailed Design

`class`, `struct`, and `record` types gain the ability to declare a _required\_member\_list_. This list is the list of all the properties and fields of a type that are considered
_required_, and must be initialized during the construction and initialization of an instance of the type. Types inherit these lists from their base types automatically, providing
a seemless experience that removes boilerplate and repetitive code.

### `required` modifier

We add `'required'` to the list of modifiers in _field\_modifier_ and _property\_modifier_. The _required\_member\_list_ of a type is composed of all the members that have had
`required` applied to them. Thus, the `Person` type from earlier now looks like this:

```cs
public class Person
{
    // The default constructor requires that FirstName and LastName be set at construction time
    public required string FirstName { get; init; }
    public string MiddleName { get; init; } = "";
    public required string LastName { get; init; }
}
```

All constructors on a type that has a _required\_member\_list_ automatically advertise a _contract_ that consumers of the type must initialize all of the properties in the list. It
is an error for a constructor to advertise a contract that requires a member that is not at least as accessible as the constructor itself. For example:

```cs
public class C
{
    public required int Prop { get; protected init; }

    // Advertises that Prop is required. This is fine, because the constructor is just as accessible as the property initer.
    protected C() {}

    // Error: ctor C(object) is more accessible than required property Prop.init.
    public C(object otherArg) {}
}
```

`required` is only valid in `class`, `struct`, and `record` types. It is not valid in `interface` types.

### `init` Clauses

A constructor can remove a required member from its contract by adding an `init` clause that specifies the name of the member to remove. For example:

```cs
public class C
{
    public required int Prop1 { get; init; }
    public required int Prop2 { get; init; }

    // Advertises that just Prop1 is required.
    public C() : init(Prop2)
    {
        Prop2 = 2;
        Console.WriteLine($"Prop2 is {Prop2}")
    }
}
```

An init clause can also provide the initialization value for the property inline. These assignments are run before the body of the constructor is executed, after the base call
if one exists:

```cs
public class C
{
    public required int Prop1 { get; init; }
    public required int Prop2 { get; init; }

    // Sets Prop2 to 2 before the constructor body is run
    public C() : init(Prop2 = 2)
    {
        Console.WriteLine($"Prop2 is {Prop1}")
    }
}
```

An init clause can remove all requirements by using the `init required` shorthand:

```cs
public class C
{
    public required int Prop1 { get; init; }
    public required int Prop2 { get; init; }

    // Advertises that there are no requirements
    public C() : init required
    {
        Prop1 = 1;
        Prop2 = 2;
    }
}
```

_required\_member\_lists_ chain across a type hierarchy. A constructor's _contract_ not only includes the required members from the current type, but also the required members
from the base type and any interfaces that it implements. If derived constructor calls a base constructor that removes some of those members from the list, the derived constructor
also removes those members from the list.

```cs
public class Base
{
    public required int Prop1 { get; init; }
    public required int Prop2 { get; init; }

    public Base() : init(Prop1 = 1) {}
}

public class Derived
{
    public required int Prop3 { get; set; }
    public required int Prop4 { get; set; }

    // Only advertises that Prop2 and Prop3 are required, because `base()` removed Prop1 from the list, and the init clause removes Prop4
    public Derived : base() init(Prop4 = 4) { }
}
```

### Initialization Requirement

Members specified in an init clause must be definitely assigned at the end of the constructor body. If they are not, an error is produced. To support more complicated
initialization logic, this error can be suppressed using the `!` operator:

```cs
public class C
{
    public required int Prop { get; set; }

    // Error: Prop is not definitely assigned at the end of the constructor body
    public C(int param) : init(Prop)
    {
        Initialize(param);
    }

    // No error: Prop! suppresses it
    public C() : init(Prop!)
        => Initialize(1);

    public void Initialize(int param) => Prop = param;
}
```

The `!` operator can also be applied to `init required`, to suppress the checking for all required properties on a type.

```cs
public class C
{
    public required int Prop1 { get; init; }
    public required int Prop2 { get; init; }

    // No errors: init required! suppresses
    public C() : init required! {}
}
```

#### Grammar

The grammar for a `constructor_initializer` is modified as follows:

```antlr
constructor_initializer
    : ':' constructor_chain
    | ':' init_clause
    | ':' constructor_chain init_clause
    ;

constructor_chain
    : 'base' '(' argument_list? ')'
    | 'this' '(' argument_list? ')'
    ;

init_clause
    : 'init' '(' init_argument_list ')'
    | 'init' 'required' '!'?
    ;

init_argument_list
    : init_argument (',' init_argument)*
    ;

init_argument
    : identifier init_argument_initializer?
    | identifier '!'
    ;

init_argument_initializer
    : '=' expression
    ;
```

### `new()` constraint

A type with a parameterless constructor that advertises a _contract_ is not allowed to be substituted for a type parameter constrained to `new()`, as there is no way
for the generic instantiation to ensure that the requirements are satisfied.

## Open Questions

### Syntax questions

* Is `init` the right word? `init` as a postfix modifier on the constructor might interfere if we ever want to reuse it for factories and also enable `init`
methods with a prefix modifier. Other possibilities:
    * `set`
* Is `required` the right modifier for specifying that all members are initialized? Others suggested:
    * `default`
    * `all`
    * With a ! to indicate complex logic
* Should we require a separator between the `base`/`this` and the `init`?
    * `:` separator
    * ',' separator
* Is `required` the right modifier? Other alternatives that have been suggested:
    * `req`
    * `require`
    * `mustinit`
    * `must`
    * `explicit`

### Init clause restrictions

Should we allow access to `this` in the init clause? If we want the assignment in `init` to be a shorthand for assigning the member in the constructor itself, it seems
like we should.

Additionally, does it create a new scope, like `base()` does, or does it share the same scope as the method body? This is particularly important for things like local
functions, which the init clause may want to access, or for name shadowing, if an init expression introduces a variable via `out` parameter.

### Base requirement chaining representation in metadata

An ideal implementation of the metadata representation would have each constructor mark the base constructor that they call in some fashion, which would ensure that, if
the base and derived types are in different assemblies, a version update in the base assembly would be accurately reflected in usage of the derived type without the
derived assembly having to upgrade. However, we don't have a way to encode a method token into a signature, so we'd have to find some other encoding strategy. This
strategy will be inherently fragile to a number of potential scenarios, so it may be more pragmatic to simply repeat any removed members in the removed member list of
the derived constructor.

### Warning vs Error

Should not setting a required member be a warning or an error? It is certainly possible to trick the system, via `Activator.CreateInstance(typeof(C))` or similar, which
means we may not be able to fully guarantee all properties are always set. We also allow suppression of the diagnostics at the constructor-site by using the `!`, which
we generally do not allow for errors. However, the feature is similar to readonly fields or init properties, in that we hard error if users attempt to set such a member
after initialization, but they can be circumvented by reflection.

### "Silly" diagnostics

Given this code:

```cs
class C
{
    public required object? O;
    public C() : init(O = null) {}
}
```

Should issue some kind of diagnostic that `O` is marked required, but never required in a contract? Developers might find marking properties as required to be useful
as a safety net.

## Discussed Questions

### Level of enforcement for `init` clauses

Do we strictly enforce that members specified in a `init` clause without an initializer must initialize all members? It seems likely that we do, otherwise we create an
easy pit-of-failure. However, we also run the risk of reintroducing the same problems we solved with `MemberNotNull` in C# 9. If we want to strictly enforce this, we
will likely need a way for a helper method to indicate that it sets a member. Some possible syntaxes we've discussed for this:

* Allow `init` methods. These methods are only allowed to be called from a constructor or from another `init` method, and can access `this` as if it's in the constructor
(ie, set `readonly` and `init` fields/properties). This can be combined with `init` clauses on such methods. A `init` clause would be considered satisfied if the member
in the clause is definitely assigned in the body of the method/constructor. Calling a method with a `init` clause that includes a member counts as assigning to that member.
If we do decided that this is a route we want to pursue, now or in the future, it seems likely that we should not use `init` as the keyword for the init clause on a
constructor, as that would be confusing.
* Allow the `!` operator to suppress the warning/error explicitly. If initializing a member in a complicated way (such as in a shared method), the user can add a `!`
to the init clause to indicate the compiler should not check for initialization.

After discussion we like the idea of the `!` operator. It allows the user to be intentional about more complicated scenarios while also not creating a large design hole
around init methods and annotating every method as setting members X or Y. `!` was chosen because we already use it for suppressing nullable warnings, and using it to
tell the compiler "I'm smarter than you" in another place is a natural extension of the syntax form.

### Required interface members

This proposal does not allow interfaces to mark members as required. This protects us from having to figure out complex scenarios around `new()` and interface
constraints in generics right now, and is directly related to both factories and generic construction. In order to ensure that we have design space in this area, we
forbid `required` in interfaces, and forbid types with _required\_member\_lists_ from being substituted for type parameters constrained to `new()`. When we want to
take a broader look at generic construction scenarios with factories, we can revisit this issue.
