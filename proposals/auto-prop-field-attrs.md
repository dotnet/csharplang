# Auto-Implemented Property Field-Targeted Attributes

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

This feature intends to allow developers to apply attributes directly to the backing fields of auto-implemented properties.

## Motivation
[motivation]: #motivation

Currently it is not possible to apply attributes to the backing fields of auto-implemented properties.  In those cases where the developer must use a field-targetting attribute they are forced to declare the field manually and use the more verbose property syntax.  Given that C# has always supported field-targetted attributes on the generated backing field for events it makes sense to extend the same functionality to their property kin.

## Detailed design
[design]: #detailed-design

In short, the following would be legal C# and not produce a warning:

```cs
[Serializable]
public class Foo {
    [field: NonSerialized]
    public string MySecret { get; set; }
}
```

This would result in the field-targetted attributes being applied to the compiler-generated backing field:

```cs
[Serializable]
public class Foo {
    [NonSerialized]
    private string MySecret_backingField;
    
    public string MySecret {
        get { return MySecret_backingField; }
        set { MySecret_backingField = value; }
    }
}
```

As mentioned, this brings parity with event syntax from C# 1.0 as the following is already legal and behaves as expected:

```cs
[Serializable]
public class Foo {
    [field: NonSerialized]
    public event EventHandler MyEvent;
}
```

## Drawbacks
[drawbacks]: #drawbacks

Currently attempting to apply an attribute to the field of an auto-implemented property produces a compiler warning that the attributes in that block will be ignored.  There is a chance that there is existing code containing these ignored attribute blocks which will result in a potential change in behavior if the feature is properly supported.

## Alternatives
[alternatives]: #alternatives

## Unresolved questions
[unresolved]: #unresolved-questions

## Design meetings


