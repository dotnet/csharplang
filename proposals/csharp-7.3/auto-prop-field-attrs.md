# Auto-Implemented Property Field-Targeted Attributes

## Summary
[summary]: #summary

This feature intends to allow developers to apply attributes directly to the backing fields of auto-implemented properties.

## Motivation
[motivation]: #motivation

Currently it is not possible to apply attributes to the backing fields of auto-implemented properties.  In those cases where the developer must use a field-targeting attribute they are forced to declare the field manually and use the more verbose property syntax.  Given that C# has always supported field-targeted attributes on the generated backing field for events it makes sense to extend the same functionality to their property kin.

## Detailed design
[design]: #detailed-design

In short, the following would be legal C# and not produce a warning:

```csharp
[Serializable]
public class Foo 
{
    [field: NonSerialized]
    public string MySecret { get; set; }
}
```

This would result in the field-targeted attributes being applied to the compiler-generated backing field:

```csharp
[Serializable]
public class Foo 
{
    [NonSerialized]
    private string _mySecretBackingField;
    
    public string MySecret
    {
        get { return _mySecretBackingField; }
        set { _mySecretBackingField = value; }
    }
}
```

As mentioned, this brings parity with event syntax from C# 1.0 as the following is already legal and behaves as expected:

```csharp
[Serializable]
public class Foo
{
    [field: NonSerialized]
    public event EventHandler MyEvent;
}
```

## Drawbacks
[drawbacks]: #drawbacks

There are two potential drawbacks to implementing this change:

1. Attempting to apply an attribute to the field of an auto-implemented property produces a compiler warning that the attributes in that block will be ignored.  If the compiler were changed to support those attributes they would be applied to the backing field on a subsequent recompilation which could alter the behavior of the program at runtime.
1. The compiler does not currently validate the AttributeUsage targets of the attributes when attempting to apply them to the field of the auto-implemented property.  If the compiler were changed to support field-targeted attributes and the attribute in question cannot be applied to a field the compiler would emit an error instead of a warning, breaking the build.

## Alternatives
[alternatives]: #alternatives

## Unresolved questions
[unresolved]: #unresolved-questions

## Design meetings
