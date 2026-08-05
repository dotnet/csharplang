Factory Methods
=====

Champion issue: <https://github.com/dotnet/csharplang/issues/10292>

## Summary
This proposal will extend the scenarios in which object initializers can be
used to include type factory methods in addition to constructors. 

## Motivation
Records need to support initializer style expressions in scenarios other than
standard object construction to support `with` expressions. This though is 
simply adding a special property to a specific named method in a type. Records
though are not alone in wanting to denote methods as being used exclusively for
construction. Factory methods serve the same purpose in .NET and are common
throughout our framework.

The ability to mark methods as being used exclusively for creation should 
become a standalone feature. Developers will be able to mark members which 
exist soley to create objects with `[Factory]` and the language will
give such methods the same flexibility as object constructors. That means 
those methods can now be used in combination with object and collection 
initializers.

```cs
public class Widget
{
    public string Name { get; set; }
    public int Id { get; init; }

    internal Widget() { }
}

public static class WidgetFactory
{
    [Factory]
    public static Widget Create() => new Widget();
}

var w = WidgetFactory.Create()
{
    Name = "JaredPar",
    Id = 42;
}
```

## Detailed Design

### Factory methods
Methods are properties annotated with `FactoryAttribute` will be treated like
constructors invocations for the purpose of using object and collection
initializer expressions on the result. This includes the ability to invoke
`init` only property accessors during initialization:

```cs
class Student
{
    public string FirstName { get; init; }
    public string LastName { get; init; }

    [Factory]
    public static Student Create() => new Student();
}

var s = Student.Create()
{
    FirstName = "Jared",
    LastName = "Parsons",
};
```

This does place some restrictions on the type of expressions that can be 
returned from a member that contains the `Factory` attribute. Specifically
the `return` expression must be either a `new` expression, `with` expression,
a method / property invocation which is marked with `Factory`, a `struct` value
or `null`.

```cs
class StudentFactory
{
    // Okay: new expression
    [Factory]
    public Student Create1() => new Student() 
    {
        FirstName = "Jared",
    };

    // Okay: returning another Factory method
    [Factory]
    public Student Create2() => Create1();

    [Factory]
    public Student Create3() => default;

    // Error: Not a valid return
    [Factory]
    public Student Create4()
    {
        var s = new Student();
        return s;
    }
}
```

When `Factory` is used on a `virtual` member then all overrides must also be
annotated with `Factory`. Likewise a `Factory` method cannot `override` a 
non-Factory method. The matching of `Factory` also applies to interface
implementations.

```cs
abstract class Base
{
    [Factory]
    public abstract Widget Create();
}

class Derived1 : Base
{
    // Okay
    [Factory]
    public override Widget Create() => new Widget();
}

class Derived2 : Base
{
    // Error: The override Create does not contain a `Factory` attribute
    public override Widget Create() => new Widget();
}
```

Restrictions:
- The return expression in a `[Factory]` method must be
    - `new` expression including object / collection initializers
    - Call to a member annotated with `[Factory]`
    - A `struct` value
    - A ternary or collascing expression where both branches meet the above
    requirements
- All overrides or `interface` implementations must match the original member
with respect to having the `[Factory]` attribute.

### Metadata Encoding
The `FactoryAttribute` will be defined as follows:

```cs
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class FactoryAttribute : Attribute
    {

    }
}
```

The compiler will also emit a modreq of the `IsFactory` type on virtual members
in metadata: 

```cs
namespace System.Runtime.CompilerServices
{
    public sealed class IsFactory
    {

    }
}
```

The justification for emitting a modreq here is that it's desirable for changing
a method to add or remove `[Factory]` results in a binary breaking change to
the member. 

## Questions

### Should null be a valid return
A factory method is really nothing more than a named constructor. Given that 
constructors can never return `null` why should the language allow for them
in this case?

This is certainly a logical argument to make. The problem is that until we have 
IL verification rules in place it's really hard to enforce. 

### Keyword vs. attribute
Syntax debate ... fight!

## Considerations 

### IL Verification
This needs to be rationalized with the IL verification rules of the init
proposal

### Validators
As the validator design comes online we'll need to consider how they interact
with `[Factory]` methods. 



