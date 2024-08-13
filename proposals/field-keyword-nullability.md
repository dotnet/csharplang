# `field` keyword nullability

## Goals

Ensure a reasonable level of null-safety for various usage patterns of the `field` keyword feature. Avoid requiring the user to apply `[field: AllowNull, MaybeNull]` or similar attributes to the property in order to express common patterns around lazy initialization, etc.

One of the key scenarios we would like to "just work", if possible, is the little-l lazy property scenario:

```cs
public class C
{
    public C() { } // it would be undesirable to warn about 'Prop' being uninitialized here

    string Prop => field ??= GetPropValue();
}
```

## Terms

A property is a *field-backed property* if it meets any of the following conditions:
1. It contains any automatically implemented accessors (`get;`/`set;`/`init;`).
2. It uses the `field` keyword within any accessor bodies.

Some examples of *field-backed properties* include:
```cs
public string Prop1 { get; set; }
public string Prop2 { get => field; set; }
public string? Prop3 { get => field; }
```

The variable denoted by the `field` keyword in a property's accessors is the *backing field* of that property.

## Nullability of the *backing field*

The *backing field* has the same type as the property. However, its nullable annotation may differ from the property. To determine this nullable annotation, we introduce the concept of *null-resiliency*. *Null-resiliency* intuitively means that the property's `get` accessor behaves properly even when the field contains the `default` value for its type.

A *field-backed property* is determined to be *null-resilient* or not by performing a special nullable analysis of its `get` accessor.
- For the purposes of this analysis, `field` is temporarily assumed to have *annotated* nullability, e.g. `string?`. This causes `field` to have *maybe-null* or *maybe-default* initial state in the `get` accessor, depending on its type.
- Then, if nullable analysis of the getter yields no nullable warnings, the property is *null-resilient*. Otherwise, it is not *null-resilient*.
- If the property does not have a get accessor, it is (vacuously) null-resilient.
- If the get accessor is auto-implemented, the property is not null-resilient.

The nullability of the backing field is determined as follows:
- If the field has nullability attributes such as `[field: MaybeNull]`, `AllowNull`, `NotNull`, or `DisallowNull`, then the field's nullable annotation is the same as the property's nullable annotation.
    - This is because when the user starts applying nullability attributes to the field, we no longer want to infer anything, we just want the nullability to be *what the user said*.
- If the containing property has ***oblivious*** or ***annotated*** nullability, then the backing field has the same nullability as the property.
- If the containing property has *not-annotated* nullability (e.g. `string` or `T`) or has the `[NotNull]` attribute, and the property is ***null-resilient***, then the backing field has ***annotated*** nullability.
- If the containing property has *not-annotated* nullability (e.g. `string` or `T`) or has the `[NotNull]` attribute, and the property is ***not null-resilient***, then the backing field has ***not-annotated*** nullability.

## Constructor analysis

Currently, an auto-property is treated very similarly to an ordinary field in [nullable constructor analysis](nullable-constructor-analysis.md). We extend this treatment to *field-backed properties*, by treating every *field-backed property* as a proxy to its backing field.

We update the following spec language from [An alternative approach (TODO rename that section?)](nullable-constructor-analysis.md#an-alternative-approach) to accomplish this:

> At each explicit or implicit 'return' in a constructor, we give a warning for each member whose flow state is incompatible with its annotations and nullability attributes. **If the member is a field-backed property, the nullable annotation of the backing field is used for this check. Otherwise, the nullable annotation of the member itself is used.** A reasonable proxy for this is: if assigning the member to itself at the return point would produce a nullability warning, then a nullability warning will be produced at the return point.

Note that this is essentially a constrained interprocedural analysis. We anticipate that in order to analyze a constructor, it will be necessary to do binding and "null-resiliency" analysis on all applicable get accessors in the same type, which use the `field` contextual keyword and have *not-annotated* nullability. We speculate that this is not prohibitively expensive because getter bodies are usually not very complex, and that the "null-resiliency" analysis only needs to be performed once regardless of how many constructors are in the type.

## Setter analysis

For simplicity, we use the terms "setter" and "set accessor" to refer to either a `set` or `init` accessor.

There is a need to check that setters of *field-backed properties* actually initialize the backing field.

```cs
class C
{
    string Prop
    {
        get => field;

        // getter is not null-resilient, so `field` is not-annotated.
        // We should warn here that `field` may be null when exiting.
        set { }
    }

    public C()
    {
        F = "a"; // ok
    }

    public static void Main()
    {
        new C().F.ToString(); // NRE at runtime
    }
}
```

The initial flow state of the *backing field* in the setter of a *field-backed property* is determined as follows:
- If the property has an initializer, then the initial flow state is the same as the flow state of the property after visiting the initializer.
- Otherwise, the initial flow state is the same as the flow state given by `field = default;`.

At each explicit or implicit 'return' in the setter, a warning is reported if the flow state of the *backing field* is incompatible with its annotations and nullability attributes.

### Remarks

This formulation is intentionally very similar to ordinary fields in constructors. Essentially, because only the property accessors can actually refer to the backing field, the setter is treated as a "mini-constructor" for the backing field.

Much like with ordinary fields, we usually know the property was initialized in the constructor because it was set, but not necessarily. Simply returning within a branch where `Prop != null` was true is also good enough for our constructor analysis, since we understand that untracked mechanisms may have been used to set the property.

## Alternatives

In addition to the *null-resilience* approach outlined above, the working group suggests the following alternatives for the LDM's consideration:

### Do nothing

We could introduce no special behavior at all here. In effect:
- Treat a field-backed property the same way auto-properties are treated today--must be initialized in constructor except when marked required, etc.
- No special treatment of the field variable when analyzing property accessors. It is simply a variable with the same type and nullability as the property.

Note that this would result in nuisance warnings for "lazy property" scenarios, in which case users would likely need to assign `null!` or similar to silence constructor warnings.  
A "sub-alternative" we can consider is to also completely ignore properties using `field` keyword for nullable constructor analysis. In that case, there would be no warnings anywhere about the user needing to initialize anything, but also no nuisance for the user, regardless of what initialization pattern they may be using.

### `field`-targeted nullability attributes

We could introduce the following defaults, achieving a reasonable level of null safety, without involving any interprocedural analysis at all:
1. The `field` variable always has the same nullable annotation as the property.
2. Nullability attributes `[field: MaybeNull, AllowNull]` etc. can be used to customize the nullability of the backing field.
3. field-backed properties are checked for initialization in constructors based on the field's nullable annotation and attributes.
4. setters in field-backed properties check for initialization of `field` similarly to constructors.

This would mean the "little-l lazy scenario" would look like this instead:

```cs
class C
{
    public C() { } // no need to warn about initializing C.Prop, as the backing field is marked nullable using attributes.

    [field: AllowNull, MaybeNull]
    public string Prop => field ??= GetPropValue();
}
```

One reason we shied away from using nullability attributes here is that the ones we have are really oriented around describing inputs and outputs of signatures. They are cumbersome to use to describe the nullability of long-lived variables.
- In practice, `[field: MaybeNull, AllowNull]` is required to make the field behave "reasonably" as a nullable variable, which gives maybe-null initial flow state, and allows possible null values to be written to it. This feels cumbersome to ask users to do for relatively common "little-l lazy" scenarios.
- If we pursued this approach, we would consider adding a warning when `[field: AllowNull]` is used, suggesting to also add `MaybeNull`. This is because AllowNull by itself doesn't do what users need out of a nullable variable: it assumes the field is initially not-null when we never saw anything write to it yet.
- We could also consider adjusting the behavior of `[field: MaybeNull]` on the `field` keyword, or even fields in general, to allow nulls to also be written to the variable, as if `AllowNull` were implicitly also present.
