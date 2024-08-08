# `field` keyword nullability

## Goals

Ensure a reasonable level of null-safety for various usage patterns of the `field` keyword feature. Avoid requiring the user to apply `[field: AllowNull, MaybeNull]` or similar attributes to the property in order to express common patterns around lazy initialization, etc.

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

A *field-backed property* is determined to be *null-resilient* or not by performing a special nullable analysis of its get accessor.
- For the purposes of this analysis, `field` is temporarily assumed to have *annotated* nullability, e.g. `string?`. This causes `field` to have *maybe-null* or *maybe-default* initial state in the get accessor, depending on its type.
- Then, if nullable analysis of the getter yields no nullable warnings, the property is *null-resilient*. Otherwise, it is not *null-resilient*.
- If the property does not have a get accessor, it is (vacuously) null-resilient.
- If the get accessor is auto-implemented, the property is not null-resilient.

The nullability of the backing field is determined as follows:
- If the containing property has ***oblivious*** nullability, then the backing field has ***oblivious*** nullability.
- If the containing property has ***annotated*** nullability (e.g. `string?` or `T?`) or the `[MaybeNull]` attribute, then the backing field has ***annotated*** nullability.
- If the containing property has *not-annotated* nullability (e.g. `string` or `T`) or the `[NotNull]` attribute, and the property is ***null-resilient***, then the backing field has ***annotated*** nullability.
- If the containing property has *not-annotated* nullability (e.g. `string` or `T`) or the `[NotNull]` attribute, and the property is ***not null-resilient***, then the backing field has ***not-annotated*** nullability.

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
