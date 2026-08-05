# Final initializers

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Final initializers are a proposed new kind of member declaration that runs at the end of an object's initialization - after constructors, object initializers and collection initializers.

*Early notes refer to these as "validators".*

## Motivation
[motivation]: #motivation

With object and collection initializers in the language, there is not a place in a type declaration to write code that runs *after* the object is otherwise fully initialized - e.g. to do final validation, trim or clean up input, compute additional private state, register the object somewhere, etc. Final initializers provide a new kind of member declaration specifically for that purpose.

As an example, consider a type which has properties for the year, month and the day. The month should be a value in the range 1-12 and the day checked against the `DateTime.DaysInMonth` method which uses the year and month. While this check can easily be done in a constructor, there is currently no way to do the check when object initializers are allowed. These checks could be done in the final initializer which would run after all constructors and initialization, including initialization done in code instantiating a member of the class.

## Detailed design
[design]: #detailed-design

Running example:

``` c#
public class Person
{
    public required string FirstName { get; init; }
    public string? MiddleName { get; init; }
    public required string LastName { get; init; }

    private readonly string fullName;

    init
    {
        // Fix up provided state
        FirstName = FirstName.Trim();
        MiddleName = MiddleName?.Trim();
        LastName = LastName.Trim();

        // Validate state
        if (FirstName is "") throw new ArgumentException("Empty names not allowed", nameof(FirstName));
        if (LastName is "") throw new ArgumentException("Empty names not allowed", nameof(LastName));

        // Compute additional state
        fullName = (MiddleName is null)
            ? $"{FirstName} {LastName}"
            : $"{FirstName} {MiddleName} {LastName}";
    }

    public override string ToString() => fullName;
}
```

### Syntax

This production is added to `class_member_declaration`, etc.:

``` antlr
final_initializer_declaration
    : attributes? `init` method_body
    ;
```

No modifiers can be specified. In some ways the declaration is a counterpart to a finalizer [14.13](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/classes.md#1413-finalizers), in that it has a very specific format with few points of decision for the programmer.

### Semantics

Unless one is specified, all types are considered to have an implicit, empty final initializer. A final initializer is considered a public virtual instance member. A final initializer declaration is considered an override, and will implicitly perform a call to the final initializer of the base class before executing the specified body, all the way up to the (empty) final initializer of the `object` class.

Just like within constructor bodies and `init` accessor bodies, `readonly` fields and init-only properties can be assigned to within a final initializer body.

In the above example, all the assignments in the final initializer body are to readonly fields and init-only properties.

At the end of the execution of an object creation expression ([11.7.15.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#117152-object-creation-expressions)) or a `with` expression, the resulting object's final initializer is executed as a function member invocation [11.6.6](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1166-function-member-invocation) on the resulting object, which means that it will be a virtual call based on the runtime type of that object.

### Nullability

When it comes to nullability analysis, the body of the final initializer would be assumed to benefit from member initializers and would consider `required` member annotations when it comes to *reading* non-nullable reference fields. It would in turn contribute to preventing nullability warnings by *writing* to non-nullable reference fields.

In the above example `FirstName.Trim()` does not yield a nullability warning, because the property is required. At the same time, the declaration of the non-nullable `fullName` does not yield a nullability error, because it is assigned to in the final initializer.

### Inheritance hierarchies

Final initializers would always call the base class final initializer before performing the code of the initializer.

### Implementation strategies

Final initializers would probably be implemented as a public virtual method with an unspeakable name (same across all final initializers), no parameters and a `void` return type. This proposal is for `object` itself to have such a virtual method, and for all final initializer declarations to be turned into overrides of this method with a `base` call prepended to the body. This would lead to every single object having such a method on it, which may or may not be an issue. The name of this method would be consistent across all final initializers and unspeakable, assuming use cases for users to directly call it are not discovered.


Using this strategy, the final initializer in the above example would generate a method override like the following:

``` c#
    public override void __final_initializer()
    {
        base.__final_initializer();

        ...
    }
```

An object creation expression

``` c#
new Person { FirstName = "Marie", LastName = "Curie" }
```

would generate code to create the object and initialize the properties, as today, followed by a call to `__final_initializer()``:

``` c#
var __tmp = new Person();
__tmp.FirstName = "Marie";
__tmp.LastName = "Curie";
__.tmp.__final_initializer();
```

## Drawbacks
[drawbacks]: #drawbacks

This is yet another mechanism related to object initialization. While it provides a means for behaviors and guarantees around object state that weren't available before, it needs to be weighed against the complexity it adds.

## Alternatives
[alternatives]: #alternatives

### Implementation
[implementation]: #implementation

Other alternatives include introducing method declarations only when necessitated by final initializer declarations. Under such strategies, it is important that the presence of final initializer methods is dynamically discoverable (either through reflection, interface implementation or otherwise), so that even when the runtime type is not statically known, the final initializer can be found and called. This can be the case for `with` expressions.

The latter set of approaches can be vulnerable to binary breaks - if a base class introduces a final initializer, a derived class that is not recompiled may have the wrong semantics and not call the base.

### Syntax/naming
[syntax-naming]: #syntax-naming

- Is the `init` syntax the best choice? Should it rather mirror finalizers in some way, with a glyph and an empty parameter list `()`?
- Is unconditionally calling the base final initializer before the body too inflexible? Should there be an explicit base call syntax instead?

### Inheritance
[inheritance]: #inheritance

Alternatively, the user be expected to call the base final initializer explicitly at some point in the execution of their final initializer, rather than emitting the code to call base automatically.

## Unresolved questions
[unresolved]: #unresolved-questions

- Which implementation strategy best balances performance with binary compatibility?
- Should `extern` be supported, as it is for finalizers?

## Design meetings

https://github.com/dotnet/csharplang/blob/main/meetings/2020/LDM-2020-04-27.md#primary-constructor-bodies-and-validators
