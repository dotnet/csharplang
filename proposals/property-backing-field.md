# Property Backing Field

* [x] Proposed
* [ ] Prototype: [In Progress]
* [ ] Implementation: [Not Started]
* [ ] Specification: [Not Started]

## Summary
[summary]: #summary

Expose auto property backing field into getter and setter scope through `field` contextual keyword.

## Motivation
[motivation]: #motivation

Consider a property with checking on its value is being set.

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
                throw new System.ArgumentOutOfRangeException();

            _age = value;
        }
    }
}
```

For checking the value, we have to define a field on the class, which is 'globally' used between getter and setter, as well as other scopes inside a type definition. As all 'global' things are evil, developper may set invalid value to the `_age` field elsewhere in the class, for example:

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
                throw new System.ArgumentOutOfRangeException();

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

This could happen when codebase goes large and developers involved go many.

Auto property may help to hide the backing field of a property, but we lose the chance to check value.

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

So it's necessary to involve a contextual keyword, which stands for the backing field of a property but can only be accessed in the scope of the property itself, across getter and setter accessors.

## Detailed design
[design]: #detailed-design

###  Define contextual `auto` and `field` keyword

Make `auto` and `field` contextual keywords inside property definition, where `auto` indifies a property will use exposed backing field, and `field` stands for the auto-generated backing field of a property.

```C#
class Person
{
    // The `auto` modifier tells compiler that
    // this property needs auto-generated backing field.
    public auto int Age
    {
        // `field` stands for the auto-generated
        // backing field for the `Age` property.
        get { return field; }
        set
        {
            if (value < 0 || value > 300)
                throw new System.ArgumentOutOfRangeException();

            field = value;
        }
    }
}
```

The `auto` should be a new contextual keyword, which is required because `field` will come into conflict with any class member named `field`.

The `field` has already been a contextual keyword, though it's rarely seen in documents. See [Attribute specification](https://github.com/dotnet/csharplang/blob/master/spec/attributes.md#attribute-specification) in C# specification. That means the `field` keyword won't appear in property definition context, so we can reuse it. And this also help to avoid involving one more new keyword.

### Backward compatible considerations

With the newly defined `auto` keyword, this change won't break existing code. Consider the following code:

```C#
class Person
{
    // Already have a `field` field defined.
    private int field;

    public int Age
    {
        // Without `auto` modifier, the `field` is bound to `field` field.
        get { return field; }
        set
        {
            if (value < 0 || value > 300)
                throw new System.ArgumentOutOfRangeException();

            field = value;
        }
    }
}
```

Without `auto` modifier in the property definition, the `field` will be explained as a usual identifier, and will be bound to the class member `field`, which is a field here.

With `auto` modifier, the `field` inside a property will be explained as keyword and be bound to the backing field of the property, while you can still use `this.field` to reference a class field.

```C#
class Person
{
    private int field;

    public auto int Age
    {
        // The class `field` field is hidden in scope of this property.
        get { return field; }
        // But you can use `this.field` to referece it.
        set
        {
            if (value != this.field)  // Check equality with class field
                field = value;  // Set backing field.
        }
    }
}
```

### Semi-auto properties

Mostly, the getter of a property contains only one `return field;` statement, which is so boring. So we can leverage the triditional auto property syntax for only one of the accessors.

```C#
class Person
{
    public int Age
    {
        // Auto getter, emit `return field;` same as auto property.
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
    public int Age
    {
        get
        {
            // Do some calculations before return a value.
            return field * 10;
        }
        // Auto setter, emit `field = value;` same as auto property.
        set;
    }
}
```

Note, since existing auto implemented property feature doesn't allow 'semi-auto' property, we can ommit `auto` modifier here, it's safe and won't break any existing code.

## Drawbacks
[drawbacks]: #drawbacks

TBD

## Alternatives
[alternatives]: #alternatives

#### Option 1: define `UseBackingFieldAttribute` class

Instead of involving new `auto` keyword, we can define a new `UseBackingFieldAttribute` class, and use this attribute on the property which needs auto-generated backing field:

```C#
using System;

class Person
{
    [UseBackingField]
    public int Age
    {
        get { return field; }
        set
        {
            if (value < 0 || value > 300)
                throw new ArgumentOutOfRangeException();

            field = value;
        }
    }
}
```

This option involves new class definition, and emits attribute to the output assembly, which is unnecessary for runtime, and require the output assembly depends on the last version of .NET Framework.

#### Option 2: define`PropertyBackingFieldAttribute` class

We can also define a new `PropertyBackingFieldAttribute` class, and use this attribute on a field, to inform compiler that this field can only be accessed through property with the given name.

```C#
using System;

class Person
{
    [PropertyBackingField("Age")]
    private int _age;

    public int Age
    {
        get { return _age; }
        set
        {
            if (value < 0 || value > 300)
                throw new System.ArgumentOutOfRangeException();

            _age = value;
        }
    }
}
```

But developer probably forget to add this attribute to the field. And again, the new class definition requires output assembly depends on last version of .NET Framework.

## Unresolved questions
[unresolved]: #unresolved-questions

n/a

## Design meetings

n/a

