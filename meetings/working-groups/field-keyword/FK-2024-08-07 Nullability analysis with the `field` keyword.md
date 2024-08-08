# Nullability analysis with the `field` keyword

Table of contents:

- [Motivation](#motivation)
- [Introduction](#introduction)
- [Constructor initialization](#constructor-initialization)
- [Field-targeted nullability attributes](#field-targeted-nullability-attributes)
- [Property-targeted nullability attributes](#property-targeted-nullability-attributes)
- [Null-resilient getters](#null-resilient-getters)
- [Alternatives](#alternatives)

## Motivation

Some vastly common patterns which are served by the `field` feature are lazy initialization, and defaulting. When a field-backed property is non-nullable, it's required to be initialized during construction. This flies in the face of these kinds of properties. They are well-formed. Warnings requiring constructor initialization would be noisy and unwelcome in these properties and in many similar forms.

```cs
public List<int> LazyProp => field ??= new();

public string DefaultingProp { get => field ?? GetDefault(); private set; }
```

These are not speculative use cases. They've been highly requested by the community and have been a core part of the design from the beginning. People have been expecting these scenarios to work. It would be a bitter pill to swallow if the scenarios were onerous to use.

First, we'll walk through enabling the user to provide existing nullability attributes explicitly for the backing field just as for manually declared fields, such as `[field: MaybeNull]`. Then, we'll walk through how the compiler builds on this capability to apply these attributes automatically in a simple and intuitive fashion.

## Introduction

This proposal brings together a few building blocks:

1. Properties can be considered initialized to non-null in the constructor by calling the setter, just as with `required` and normal flow analysis today.

1. `field`-targeted nullability attributes work just as with manually declared fields. (E.g. `[field: MaybeNull]`)

1. Null-resilient getters, a simple concept that allows maintaining a property's contract of not returning null, while flexibly allowing idiomatic patterns that use a nullable backing field. 

The following sections will walk through how these concepts play out.

## Constructor initialization

In C# today, a non-nullable auto property which has no property initializer and is not marked `required` is forced to be initialized in the constructor. We will preserve this enforcement for all field-backed properties. This enforcement is in the form of the existing warning "CS8618: Non-nullable property 'Prop' must contain a non-null value when exiting constructor."

Initializing the property this way, by calling the setter in the constructor, will appease the requirement. This is the case even though a manually implemented setter of a field-backed property does not guarantee that the backing field is actually initialized:

```cs
class C
{
    public C() { Prop = "..."; }

    public string Prop
    {
        get;
        set { if (condition) field = value; }
    }
}
```

While this introduces a possibility for null to end up being returned with no warnings, this level of trust in the setter's implementation is consistent with how C# 8 analyzes property assignments of non-null values:

```cs
obj.NullableProp.ToString(); // Warning
obj.NullableProp = "...";    // This could be calling `set { }`
obj.NullableProp.ToString(); // No warning
```

This is also the same level of trust that is necessary when the burden of initializing is moved via `required`, from the constructor to the caller of the constructor:

```cs
new C() { Prop = "..." };

class C
{
    public required string Prop
    {
        get;
        set { if (condition) field = value; }
    }
}
```

See [Constructor initialization alternatives](#constructor-initialization-alternatives) for alternatives that were considered.

## Field-targeted nullability attributes

The nullability attributes which are applicable to regular fields may be applied to the backing field of a property using the existing `field:` attribute target. These are the preconditions `AllowNull` and `DisallowNull` and the postconditions `MaybeNull` and `NotNull`.

When applied, they affect the nullability analysis of the `field` keyword within the accessors just as they would affect a manually declared field. In cases where the manually-declared field would be required to be assigned in constructors, the field-backed property will be required to be assigned in constructors.

### Postconditions (MaybeNull, NotNull)

When `[field: MaybeNull]` is applied on a non-nullable field-backed property, the property will not be required to be initialized in all construction paths. This is consistent with how a manually declared non-nullable field would no longer be required to be initialized when this attribute is applied to it.

The reverse is true for `[field: NotNull]` on a *nullable* property. Just like how this would cause warnings requiring a manually declared field to be initialized in all construction paths, applying `[field: NotNull]` to a field-backed property will cause the property to be required to be initialized in all construction paths.

#### Examples 

This will give "CS8603: Possible null reference return" on `field` just as with a manually declared field:

```cs
// CS8603: Possible null reference return
[field: MaybeNull]
public string Prop { get => field; set => field = value; }
                            ~~~~~
```

The same warning will be shown here on 'get', since `get;` expands to `get => field;`:

```cs
// CS8603: Possible null reference return
[field: MaybeNull]
public string Prop { get; set => field = value; }
                     ~~~
```

The next example is not real code we expect people to write, but for consistency its behavior will change. In C# 12, the attribute is ignored. In C# 13, applying this attribute to an auto-property will show the same warning as in the previous example, instead of showing "CS8618: Non-nullable property 'Prop' must contain a non-null value when exiting constructor."

```cs
// CS8603: Possible null reference return
[field: MaybeNull]
public string Prop { get; set; }
                     ~~~
```

### Preconditions (AllowNull, DisallowNull)

When `[field: AllowNull]` is applied on a non-nullable field-backed property, the property will be able to assign maybe-null values to the field without a warning. Here's an example:

```cs
[field: AllowNull]
public string ResetIfSetToDefault
{
    get => field ?? GetDefault();
    set => field = (value == GetDefault() ? null : value);
}
```

Automatic `field` nullability is covered by the [null-resilient getter proposal](#null-resilient-getters) below. `[field: AllowNull, MaybeNull]` will be automatically applied due to the `?? GetDefault();` in the example above, and will not be applied if the `?? Default();` is removed.

This is safer than manually declaring `[field: AllowNull]` because there is an existing hole in C#'s nullability analysis that allows null to be returned from the property with no warning:

```cs
// C# 12. No warning to initialize 'field' and no warning in the getter.
[AllowNull]
private string field;

public string ResetIfSetToDefault
{
    get => field; // Returns null! `?? GetDefault()` is missing
    set => field = value == GetDefault() ? null : value;
}
```

No use cases are known for `[field: DisallowNull]`, but it would be part of the automatic set of behaviors inherited from how manually-declared fields work.

```cs
// CS8601 Possible null reference assignment
[field: DisallowNull]
public string? Prop { get; set => field = value; }
                                          ~~~~~

// CS8601 Possible null reference assignment
[field: DisallowNull]
public string? Prop { get => field; set; }
                                    ~~~

// With C# 13, same warning as the previous example.
// With C# 12, the attribute is ignored.
[field: DisallowNull]
public string? Prop { get; set; }
                           ~~~
```

However, if the property also has `DisallowNull` applied, then there is no warning:

```cs
[DisallowNull]
[field: DisallowNull]
public string? Prop { get; set => field = value; } // No warning
```

## Property-targeted nullability attributes

Nullability attributes applied to the property itself will not be automatically inherited by the backing field. The intended scenario for applying `[AllowNull]` to a property is where the property setter sanitizes nulls to other values before storing in the field.

```cs
[AllowNull]
public string Prop { get; set => field = value ?? ""; }
```

If the user actually wants to store `null` in the field, the user can either rely on the automatic `field` nullability granted by the [null-resilient getter proposal](#null-resilient-getters) below:

```cs
[AllowNull]
public string Prop { get => field ?? GetDefault(); set; }
```

Or if that version is not taken, the user will have to add `[field: AllowNull]` as well:

```cs
[AllowNull]
[field: AllowNull]
public string Prop { get => field ?? GetDefault(); set; }
```

## Null-resilient getters

A central scenario for the `field` keyword is lazily-initialized properties. Without automatic nullability, users would have to manually place `[field: MaybeNull]` on every lazily-initialized property in order for the compiler to stop telling the user that the _lazily_-initialized property should be initialized in the _constructor_!

```cs
// This is ungainly.
[field: MaybeNull]
public List<int> Prop => field ??= new();
```

A null-resilient getter is a getter which continues to fulfill the property's contract of not returning a `null` value, even when `field` is maybe-null at the start of the getter. For properties with a null-resilient getter, the backing field does not need to be initialized, and it may be assigned a maybe-null value at any point, all without risk of returning `null` from a non-nullable property.

Null resilience is further extended to mean that there are no nullability warnings when `field` is maybe-null. This is inclusive of checking that no exit point returns a maybe-null expression. We would determine null resilience by analyzing the getter with a pass that starts `field` out as maybe-null and checks to see that there are no nullability warnings. (A less inclusive alternative is considered in [Definition of null resilience](#definition-of-null-resilience).)

These getters are null-resilient because there are no nullability warnings when `field` is maybe-null:

```cs
get => field ?? "";
get => field ??= new();
get => LazyInitializer.EnsureInitialized(ref field, ...);
get => field ?? throw new InvalidOperationException(...);
get => throw new NotImplementedException();
get => field!;
```

These getters are not null-resilient because there are nullability warnings when `field` is maybe-null:

```cs
get;
get => field;
get => field ?? SomethingNullable;
get => (T[]?)field?.Clone();
get => (T[])field.Clone();
get
{
    string unrelated = null; // Warning
    return field ?? "";
}
```

On field-backed properties with a null-resilient getter, if the property type is non-nullable and not a value type, and no field-targeted nullability attributes are manually specified, `[field: AllowNull, MaybeNull]` will be automatically applied. Or, equivalently the field type may become nullable.

Alternatively, AllowNull could be left off, which is considered in [Full or half nullability](#full-or-half-nullability).

In terms of explaining this feature to users, users are more used to thinking in terms of `string?` more than working with attributes.

### Open question: unrelated nullability warnings

Should two passes be done, so that the getter's null-resilience is only impacted by warnings that relate to the flow state of `field`?

```cs
get
{
    string unrelated = null; // Warning
    return field ?? "";
}
```

### Open question: interaction with manually-applied attributes

If the user directly applies `[field: MaybeNull]` or `[field: NotNull]`, the getter is not checked for null resilience because the user has already stated the outcome they intend.

However, if the user directly applies only the _precondition_ `[field: AllowNull]` or `[field: DisallowNull]` and not the _postcondition_ `[field: MaybeNull]` or `[field: NotNull]`, should the postcondition still be automatically determined based on the null resilience of the getter? Or, between the reading non-nullable property type and the modification of `[field: AllowNull]`, should the ommission of `[field: MaybeNull]` be taken to mean the same as if `[field: NotNull]` had explicitly been stated?

In other words, in the following example, does the user intend `[field: AllowNull, NotNull]`, or merely `[field: AllowNull]` with `MaybeNull` automatically applied as long as it is safe due to the getter being null-resilient?
```cs
[field: AllowNull] // Manually specified, though it could also be inferred due to null-resilience
public string Prop { get => field ?? GetDefault(); set; }
```

## Alternatives

### Constructor initialization alternatives

As proposed, a property assignment in the constructor will appease "CS8618: Non-nullable property 'Prop' must contain a non-null value when exiting constructor," even if the setter does not initialize the backing field. A more conservative alternative to this would be cross-body nullability analysis, analyzing each constructor body as though the setter bodies were inlined into the constructor bodies in order to establish whether initialization is statically guaranteed.

This would bring extra safety, but also extra noise. There would be cases where the user would have to suppress initialization warnings on valid code, such as the following:
```cs
class C
{
    // With cross-body analysis, this still warns!
    // CS8618: Non-nullable property 'Prop' must contain a non-null value when exiting constructor
    public C() { Prop = "..."; }

    public string Prop
    {
        get;
        set { if (condition) field = value; }
    }
}
```

The warning is due to the cross-body analysis seeing this inlined form, which does not assign on all construction paths:
```cs
    public C() { if (condition) <Prop>k__BackingField = "..."; }
```

This requires a suppression, such as `= null!` which initializes the field directly. After adding such a suppression, the user is in a scenario where even totally removing the constructor assignment does not cause a warning.
```cs
    public string Prop
    {
        get;
        set { if (condition) field = value; }
    } = null!;
```

The cross-body analysis approach is complex both for implementation and for users understanding how to react to warnings. The working group recommends not pursuing cross-body analysis.

A different alternative could be to ignore assignments to manually implemented setters. This would require users to generally add pragmas or `= null!` property initializers even when assigning the property in the constructor, which seems punishing and noisy.

### Definition of null resilience

Alternatively, null resilience could be defined more directly around what it means to uphold a non-nullable contract, namely that a null value is never returned. Instead of collecting _all_ nullability warnings during the analysis pass where `field` is initially maybe-null, we would only examine exit points and ensure that every exit point is still returning a not-null expression in spite of `field` being maybe-null.

Here's an example of where the difference would show. This would change from not being null-resilient, to being considered null-resilient. This is technically fulfilling the aspect of the property contract that a null value is never returned, even while it implicitly throws NullReferenceException.

In the alternative where this getter is considered null-resilient, the user will immediately get a warning in the code which will be confusing. Furthermore, there's no good way to respond to the warning; even when the property is initialized, the getter will be considered null-resilient and thus will allow `field` to hold nulls:
```cs
class C<T>
{
    public C(T[] prop) { Prop = prop; }

    // CS8602: Dereference of a possibly null reference.
    public T[] Prop { get => (T[])field.Clone(); private set; }
                                  ~~~~~
}
```

Not taking the alternative, and not considering this getter to be null-resilient, the warning is exchanged for a construction warning. This warning is desirable because it points the way to properly addressing it by making sure the property is initialized during construction, at which point the warning disappears.
```cs
class C<T>
{
    // CS8618: Non-nullable property 'Prop' must contain a non-null value when exiting constructor.
    public T[] Prop { get => (T[])field.Clone(); private set; }
               ~~~~
}
```

### Full or half nullability

Null resilience means that the backing field may always hold `null` at the start of the getter. The simplest way for the user to think of this is for the field type to be nullable. The field type being nullable could also be thought of as applying `[field: MaybeNull, AllowNull]`, which is what the user would have to manually specify in the absence of automatic `field` nullability.

Alternatively, instead of `[field: MaybeNull, AllowNull]`, the automatic nullability could be weakened to `[field: MaybeNull]`. This would retain the benefits of not requiring constructor initialization, but it would require the user to manually add `[field: AllowNull]` in scenarios where the user wants to take full advantage of the null-resilience of the property and assign maybe-null values to the backing field:

```cs
[field: AllowNull] // Required if we only go halfway.
public string ResetIfSetToDefault
{
    get => field ?? GetDefault();
    set => field = value == GetDefault() ? null : value;
}
```

The presence of `[field: AllowNull]` also opens up an existing kind of safety hole. Once it's specified, if you remove `?? GetDefault()`, there will be no nullability warnings in spite of the whole which this leaves. This can be shown in the language today:

```cs
class C
{
    [AllowNull]
    private string resetIfSetToDefault;

    public string ResetIfSetToDefault
    {
        // Removed `?? GetDefault()` without warnings!
        get => resetIfSetToDefault; 
        set => resetIfSetToDefault = value == GetDefault() ? null : value;
    }
}
```
