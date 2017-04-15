# Property Backing Field

* [x] Proposed
* [ ] Prototype: [Not Started]
* [ ] Implementation: [Not Started]
* [ ] Specification: [Not Started]

## Summary
[summary]: #summary

Expose auto property backing field into getter and setter scope through `field` contextual keyword.

## Motivation
[motivation]: #motivation

Consider a property with checking on its value set.

```C#
class Person
{
    private int _age;

    public int Age
    {
        get { return _age; }
        set
        {
            if (value < 0 || value > 300)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            _age = value;
        }
    }
}
```

For checking the value, we have to define a field on the class, which is 'globally' used between getter and setter. As all 'global' things are evil, developper may set invalid value to the `_age` field elsewhere in the class, for example:

```C#
class Person
{
    private int _age;

    public int Age
    {
        get { return _age; }
        set
        {
            if (value < 0 || value > 300)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            _age = value;
        }
    }

    // Far away from the above peroperty definition,
    // probably in another file of a partial class.
    void Foo()
    {
        _age = 500;  // It's definitily invalid.
    }
}
```

This could happen when codebase going large and developers involved go many.

Auto property may help to hide the backing field of a property, but we lose the change to check value.

```C#
class Person
{
    public int Age
    {
        get;
        set;  // How can we check the value being set?
    }
}
```

So it's necessary to involve a contextual keyword, which stand for the backing field of a property but can only be accessed in the scope of the property itself, cross getter and setter accessors.

## Detailed design
[design]: #detailed-design

###  Define contextual `field` keyword

Make `field` a contextual keyword inside property definition.

Note, `field` has already been a contextual keyword, though it's rarely seen in documents, it could be used in attribute declarations. See [Attribute specification](https://github.com/dotnet/csharplang/blob/master/spec/attributes.md#attribute-specification) in C# specification.

That means the `field` keyword won't appear in property definition context, so we can reuse it. And this also help to avoid involving new keyword.

```C#
class Person
{
    public int Age
    {
        // `field` stands for the auto-generated
        // backing field for the `Age` property.
        get { return field; }
        set
        {
            if (value < 0 || value > 300)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            field = value;
        }
    }
}
```

### Backward compatible considerations

This change probably break existing code. Consider the following code:

```C#
class Person
{
    // Already have a `field` field defined.
    private int field;

    public int Age
    {
        // What does `field` stand for?
        get { return field; }
        set
        {
            if (value < 0 || value > 300)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            field = value;
        }
    }
}
```

#### Option 1: define `UseBackingFieldAttribute` class

Define a new `UseBackingFieldAttribute` class, and use this attribute on the property which needs auto-generated backing field:

```C#
using System;

class Person
{
    private int field;

    [UseBackingField]
    public int Age
    {
        // `field` stands for the auto-generated
        // backing field for the `Age` property.
        //
        // The `field` field is hidden in scope of this property.
        // But you can use `this.field` to referece it.
        get { return field; }
        set
        {
            if (value < 0 || value > 300)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            if (value != this.field)
            {
                field = value;
            }
        }
    }
}
```

This option involves new class definition, and emits attribute to the output assembly, which is unnecessary, and require the output assembly depends on the last version of .NET Framework.

#### Option 2: define contextual `auto` keyword

Make `auto` contextual keyword inside property definition as a modifier.

```C#
class Person
{
    private int field;

    // The `auto` modifier tells compiler that
    // this property needs auto-generated backing field.
    public auto int Age
    {
        // `field` stands for the auto-generated
        // backing field for the `Age` property.
        //
        // The `field` field is hidden in scope of this property.
        // But you can use `this.field` to referece it.
        get { return field; }
        set
        {
            if (value < 0 || value > 300)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            if (value != this.field)
            {
                field = value;
            }
        }
    }
}
```

This involves new keyword, but makes code shorter and cleaner, and the output assembly won't depend on the last version of .NET Framework.

### Semi-auto properties

Mostly, the getter of a property contains only one `return field;` statement, which is so boring. So we can leverage the triditional auto property syntax for only one of the accessors.

```C#
class Person
{
    public auto int Age
    {
        // Auto getter,
        // emit `return field;` same as auto property.
        get;
        set
        {
            // Do value checking here.
            field = value;
        }
    }
}
```

And, may not that common, we can also define auto-setter-only property:

```C#
class Person
{
    public auto int Age
    {
        get
        {
            // Do some calculations before return a value.
            return field * 10;
        }
        // Auto setter,
        // emit `field = value;` same as auto property.
        set;
    }
}
```


## Drawbacks
[drawbacks]: #drawbacks

TBD

## Alternatives
[alternatives]: #alternatives

We can also define a new `PropertyBackingFieldAttribute` class, and use this attribute on a field, to inform compiler that this field can only be accessed through properties.

But developer probably forget to add this attribute to the field. And such fields can be accessed cross two or more properies, so it still can't stop setting invalid value outside the target property. And again, the new class definition requires output assembly depends on last version of .NET Framework.

## Unresolved questions
[unresolved]: #unresolved-questions

n/a

## Design meetings

n/a

