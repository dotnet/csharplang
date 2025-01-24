# Allow direct use of backing field during construction

Champion issue: <https://github.com/dotnet/csharplang/issues/9031  >
Champion issue for `field` keyword: https://github.com/dotnet/csharplang/issues/8635  
Related discussion: https://github.com/dotnet/csharplang/discussions/8704

## Summary
[summary]: #summary

<!-- One paragraph explanation of the feature. -->

Allow direct assignment and use of a property's backing field during construction, without having to invoke the setter, via a new `fieldof(Prop)` expression.

```cs
class C
{
    public C(DataStore store)
    {
        this.store = store;

        fieldof(this.Prop) = store.ReadPropFromDisk();
        M(ref fieldof(this.Prop));
    }

    void Method()
    {
        // error: 'fieldof' can only be used during initialization (see also Alternatives)
        fieldof(this.Prop) = "a";
    }

    private DataStore store;

    public string Prop
    {
        get => field;
        set
        {
            if (value != field)
            {
                field = value;
                store.WritePropToDisk(value);
            }
        }
    }
}
```

## Motivation
[motivation]: #motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

We are seeing prominent source generators such as [MVVM Toolkit](https://github.com/CommunityToolkit/dotnet) and [ComputeSharp](https://github.com/Sergio0694/ComputeSharp) making heavy use of *partial field-backed properties*. Using a backing field for a partial property implementation comes with a number of experience improvements, including:
1. avoiding the need for either generator or user to introduce an additional member with a distinct name (and in practice, often the generator needs to introduce it, which is bad for discoverability).
2. avoiding the need for the generator to "wire up" the relationship between the field and property, in order to make it clear to user and compiler (e.g. for nullable constructor analysis).
3. allowing user to put `[field: Attr]` on the definition part of the property, and have the attributes just go where they're supposed to, without any hacky workarounds from the generator itself.
4. allowing user to put a property initializer on the definition part, letting the user initialize the field during construction without invoking the setter logic.

Users are hitting limitations related to (4). Specifically, by *only* allowing the property initializer itself to assign the backing field, we are imposing an inconvenient limitation on what values are allowed to "bypass the setter" during construction:

```cs
class C1
{
    // "stuff available in a static context" can be used:
    public partial string Prop { get; set; } = ValueFactory.GetValue();
}

class C2(string prop)
{
    // primary constructor parameters can be used:
    public partial string Prop { get; set; } = prop;
}

public class C3
{
    // but non-primary constructors must go through setter logic.
    internal C3()
    {
        // even though users may have reasons that a primary constructor isn't suitable.
        // e.g. in this case, even if we figure out how to make things work with a primary constructor,
        // we may not want that constructor to have the same accessibility as the containing type.
        var (first, second) = GetValues();
        Prop1 = first;
        Prop2 = second;
    }

    public partial string Prop1 { get; set; }
    public partial string Prop2 { get; set; }
}
```

See also [field-keyword.md#property-initializers](https://github.com/dotnet/csharplang/blob/main/proposals/field-keyword.md#property-initializers). It's fairly easy to imagine the `bool IsActive` example from that proposal, which motivated the property initializer behavior we have today, where the initial value doesn't simply come from a constant or a static, but needs to be passed in through a constructor.

```cs
class SomeViewModel
{
    public SomeViewModel(bool isActive)
    {
        // without a way to assign the field directly,
        // 'HasPendingChanges' is set to true, only when 'isActive' is true.
        // But all we're trying to do is rehydrate state from a previous session/user setting/etc..
        IsActive = isActive;
    }

    public bool HasPendingChanges { get; private set; }

    public bool IsActive { get; set => Set(ref field, value); }

    private bool Set<T>(ref T location, T value)
    {
        if (RuntimeHelpers.Equals(location, value))
            return false;

        location = value;
        HasPendingChanges = true;
        return true;
    }
}
```

### Why not just declare the field explicitly?

We believe that solutions involving explicitly declaring the backing field will significantly degrade the end user experience in source generator scenarios. Essentially, hand-rolled substitutes for the benefits outlined in [Motivation](#motivation) are unlikely to be uniform and fully correct across various generators. Users would have to get oriented with different solutions across different generators for associating the field and property, locating the related declarations in user code and generated code, applying attributes independently to the field and property, and applying field initializers. See also [Alternate generator patterns](#alternate-generator-patterns).

### What about encapsulation?

One purported benefit of the `field` keyword feature is that it is *only* usable from within the property accessors. It may seem questionable that this proposal is to seemingly change that, and allow the `field` to *also* be used in constructors.

However, this encapsulation has never been as complete as the above statement implies. Today, a type's constructors need to be concerned with which properties are *field-backed*, because it is directly related to nullable constructor analysis--forgetting to assign or check a field-backed property can result in a warning, while doing the same on a non-field-backed property will not.

```cs
class C
{
    public string Prop1 { get => ValueStore.Get(); set => ValueStore.Set(value); }
    public string Prop2 { get => field; set => field = value; }

    // warning for Prop2, but not for Prop1
    public C() { }
}
```

The fact that a property initializer (and by extension, a primary constructor) is permitted to "bypass" the setter logic is necessary and useful. We think that allowing such "bypass" to occur in ordinary constructors of the same type is useful for the same reasons. Because the capability remains limited to construction-time, we believe it preserves and reinforces the benefits of using the `field` keyword.

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement, and include examples of how the feature is used. This section can start out light before the prototyping phase but should get into specifics and corner-cases as the feature is iteratively designed and implemented. -->

The grammar is updated as follows:

```diff
 primary_no_array_creation_expression
     : literal
     | interpolated_string_expression
     | simple_name
     | parenthesized_expression
 (...)
     | nameof_expression
+    | fieldof_expression
 (...)
     ;

+fieldof_expression
+    : 'fieldof' '(' expression ')'
+    ;

```

A `fieldof_expression` of the form `fieldof(P)` is evaluated and classified as follows:
- If the containing member of the expression is not a constructor or `init` accessor, a compile-time error occurs.
- If `P` is not classified as a property access of a [field-backed property](https://github.com/dotnet/csharplang/blob/main/proposals/field-keyword.md#glossary), a compile-time error occurs.
- If `P` is classified as a property access of a field-backed property, then `fieldof(P)` is classified as a variable, specifically the backing field of `P`.
- If `P` is static and the containing constructor is not static, or vice-versa, a compile-time error occurs.
- If `P` is not declared in the containing type, a compile-time error occurs. (`fieldof()` does not work with a property declared on a base type.)
- Otherwise, `fieldof(P)` denotes the backing field of `P`.

A `fieldof_expression` is subject to limitations on the receiver of its property access, similar to an assignment to a `readonly` field. Specifically, the receiver must be the instance being initialized by the containing constructor, i.e. explicit or implicit `this`. Otherwise, a compile-time error occurs.

### Ref safety

The *ref-safe-context* ([§9.7.2.4](https://github.com/dotnet/csharpstandard/blob/81d9d57826f289fbf772e10dfec776227fab1006/standard/variables.md#9724-field-ref-safe-context)) for an expression of the form `fieldof(e.P)` is determined as follows:
- If `e` is of a value type, then the *ref-safe-context* of `fieldof(e.P)` is the same as the *ref-safe-context* of `e`.
- Otherwise, its *ref-safe-context* is *caller-context*.

The *safe-context* ([§16.4.12.4](https://github.com/dotnet/csharpstandard/blob/81d9d57826f289fbf772e10dfec776227fab1006/standard/structs.md#164124-field-safe-context)) for an expression of the form `fieldof(e.P)` is determined as follows:
- If `e.P` is of ref struct type, then the *safe-context* of `fieldof(e.P)` is the same as the *safe-context* of `e`.
- Otherwise, its *safe-context* is *caller-context*.

The above ref safety rules are strongly analogous to the existing, linked rules which apply to ordinary field accesses.

### Compat

This design makes no concession to preserving existing `fieldof(P)` behavior when a symbol `fieldof` is already in scope. This is a divergence from existing `nameof` behavior, but aligns with the existing breaking change design of the `field` keyword itself.

This proposal also reserves `fieldof(P)` in expression contexts generally, rather than reserving it only in constructors and `init` accessors. Existing code containing calls like `fieldof(P)` would need to be changed to `@fieldof(P)` in order to avoid breaks. We could consider instead limiting the break to only apply within constructors and `init` accessors.

Depending on feedback, we could also adjust the design so that `fieldof` works more like `nameof`, and simply becomes unavailable when a symbol `fieldof` is in scope.

To give a sense of relative risk of the break with `field`, versus `fieldof`, there are about ~86k results for `field` in C# source and comments on GitHub, and about 5 of the same for `fieldof`, at time of writing.

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

The motivating scenarios may not rise to the level of justifying a new contextual keyword or specialized syntax form in the language.

## Alternatives
[alternatives]: #alternatives

### Permit anywhere in the same type

Except for encapsulation, there isn't a specific reason we *need* to limit use of `fieldof()` to initialization. We could instead allow it anywhere in the same type if we wanted, which would effectively make `field` itself just a shorthand for `fieldof()` for the current property.

```cs
class C
{
    string P { get => fieldof(P); set => field = value; }

    void M0()
    {
        M1(ref fieldof(P));
    }

    void M1(ref string s) { }
}
```

The "encapsulation" behavior, as it currently exists in absence of `fieldof()`, seems appealing, as prevents misuse of the field outside the policy of the associated property. However, since `fieldof()` is always a more nested expression than a property access, it seems like users will tend to use the property *anyway* unless they have a specific reason for needing to use the field.

If we think that there are justified construction-specific cases for using the backing field directly, then perhaps there are also valid post-construction cases as well, that we may not know about yet, and it's not justified to put in a *cliff*, saying: sorry, only during initialization or in the accessors. Instead, we could simply see what the user is trying to do, and get out of their way.

At the same time, `field` was thought to be a stepping stone toward a more general "property scoped fields" feature--where it seems much harder to justify accessing the fields outside of the property. The *just get out of the user's way* line of reasoning also seems to lead to letting people specify any accessibility for the backing field, which somehow seems a little too far.

Essentially, the `field` feature is providing both *encapsulation* and *association* benefits. The question is whether to allow users to drop the *encapsulation* part, if they wish, and keep the *association* part.

### Alternate syntaxes

We could consider alternative syntax for doing the same thing, such as an [init prefix](https://github.com/dotnet/csharplang/discussions/8704#discussioncomment-11450489):
```cs
public C(string prop)
{
    // 'init' prefix appearing before an assignment means assign a backing field.
    init Prop = prop;
}
```

Arguably, `fieldof(Prop)` is more clear than `init Prop`. The latter is more of a "knowledge check" that a property initializer assigns the field without calling the setter, and that we are using `init` to "simulate" such an initializer outside of the property declaration. While `fieldof(Prop)` more directly states "we are using the field here".

Also, due to reusing an accessor keyword, it may be confusing to unfamiliar users:

```cs
class C
{
    public string Prop { get; init { SideEffect(); field = value; } }
    public C(string prop)
    {
        // wait.. putting 'init' here means "don't use the init accessor"?
        init Prop = prop;
    }
}
```

### Alternate generator patterns

Generator authors could come up with a pattern where the field is explicitly declared by either generator or user, and associated to the property in a way that generator and compiler can understand (e.g., nullability attributes, naming conventions, and/or generator-specific attributes to associate members by name). Then, user can simply refer to the explicit field in a constructor.

We think this is a bad solution, because it requires generator authors to solve all the bullet points mentioned in [Motivation](#motivation), and necessarily results in a compromised end user experience.

### `initialized` flag pattern

We could advise users in this situation to introduce a flag which is set at the end of construction:

```cs
class C
{
    private readonly bool _initialized;

    public C(string prop)
    {
        Prop = prop;
        _initialized = true;
    }

    public string Prop
    {
        get => field;
        set
        {
            if (!_initialized)
            {
                field = value;
                return;
            }

            if (value != field)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }
}
```

We think this is not a palatable solution compared to simply being able to set the field, due to increasing memory usage and complicating setter and constructor logic.

## Open questions
[open]: #open-questions

<!-- What parts of the design are still undecided? -->

See [Compat](#compat).
