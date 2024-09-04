# `field` keyword in properties

## Summary

Extend all properties to allow them to reference an automatically generated backing field using the new contextual keyword `field`. Properties may now also contain an accessor _without_ a body alongside an accessor _with_ a body.

## Motivation

Auto properties only allow for directly setting or getting the backing field, giving some control only by placing access modifiers on the accessors. Sometimes there is a need to have additional control over what happens in one or both accessors, but this confronts users with the overhead of declaring a backing field. The backing field name must then be kept in sync with the property, and the backing field is scoped to the entire class which can result in accidental bypassing of the accessors from within the class.

There are several common scenarios. Within the getter, there is lazy initialization, or default values when the property has never been given. Within the setter, there is applying a constraint to ensure the validity of a value, or detecting and propagating updates such as by raising the `INotifyPropertyChanged.PropertyChanged` event.

In these cases by now you always have to create an instance field and write the whole property yourself.  This not only adds a fair amount of code, but it also leaks the backing field into the rest of the type's scope, when it is often desirable to only have it be available to the bodies of the accessors.

## Glossary

- **Auto property**: Short for "automatically implemented property" ([§15.7.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1574-automatically-implemented-properties)). Accessors on an auto property have no body. The implementation and backing storage are both provided by the compiler. Auto properties have `{ get; }`, `{ get; set; }`, or `{ get; init; }`.

- **Auto accessor**: Short for "automatically implemented accessor." This is an accessor that has no body. The implementation and backing storage are both provided by the compiler. `get;`, `set;` and `init;` are auto accessors.

- **Full accessor**: This is an accessor that has a body. The implementation is not provided by the compiler, though the backing storage may still be (as in the example `set => field = value;`).

- **Field-backed property**: This is either a property using the `field` keyword within an accessor body, or an auto property.

- **Backing field**: This is the variable denoted by the `field` keyword in a property's accessors, which is also implicitly read or written in automatically implemented accessors (`get;`, `set;`, or `init;`).


## Detailed design

For properties with an `init` accessor, everything that applies below to `set` would apply instead to the `init` accessor.

There are two syntax changes:

1. There is a new contextual keyword, `field`, which may be used within property accessor bodies to access a backing field for the property declaration ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-05-15.md#should-field-and-value-be-keywords-in-property-or-accessor-signatures-what-about-nameof-in-those-spaces)).

2. Properties may now mix and match auto accessors with full accessors ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-06-24.md#mixing-auto-accessors)). "Auto property" will continue to mean a property whose accessors have no bodies. None of the examples below will be considered auto properties.

Examples:

```cs
{ get; set => Set(ref field, value); }
```

```cs
{ get => field ?? parent.AmbientValue; set; }
```

Both accessors may be full accessors with either one or both making use of `field`:

```cs
{ get => field; set => field = value; }
```

```cs
{ get => field; set => throw new InvalidOperationException(); }
```

```cs
{ get => overriddenValue; set => field = value; }
```

```cs
{
    get;
    set
    {
        if (field == value) return;
        field = value;
        OnXyzChanged();
    }
}
```

Expression-bodied properties and properties with only a `get` accessor may also use `field`:

```cs
public string LazilyComputed => field ??= Compute();
```

```cs
public string LazilyComputed { get => field ??= Compute(); }
```

Set-only properties may also use `field`:

```cs
{
    set
    {
        if (field == value) return;
        field = value;
        OnXyzChanged(new XyzEventArgs(value));
    }
}
```

### Breaking changes

The existence of the `field` contextual keyword within property accessor bodies is a potentially breaking change, proposed as part of a larger [Breaking Changes](https://github.com/dotnet/csharplang/issues/7964) feature.

Since `field` is a keyword and not an identifier, it can only be "shadowed" by an identifier using the normal keyword-escaping route: `@field`. All identifiers named `field` declared within property accessor bodies can safeguard against breaks when upgrading from C# versions prior to 13 by adding the initial `@`.

### Field-targeted attributes

As with auto properties, any property that uses a backing field in one of its accessors will be able to use field-targeted attributes:

```cs
[field: Xyz]
public string Name => field ??= Compute();

[field: Xyz]
public string Name { get => field; set => field = value; }
```

A field-targeted attribute will remain invalid unless an accessor uses a backing field:

```cs
// ❌ Error, will not compile
[field: Xyz]
public string Name => Compute();
```

### Property initializers

Properties with initializers may use `field`. The backing field is directly initialized rather than the setter being called ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-02.md#open-questions-in-field)).

Calling a setter for an initializer is not an option; initializers are processed before calling base constructors, and it is illegal to call any instance method before the base constructor is called. This is also important for default initialization/definite assignment of structs.

This yields flexible control over initialization. If you want to initialize without calling the setter, you use a property initializer. If you want to initialize by calling the setter, you use assign the property an initial value in the constructor.

Here's an example of where this is useful. We believe the `field` keyword will find a lot of its use with view models because of the elegant solution it brings for the `INotifyPropertyChanged` pattern. View model property setters are likely to be databound to UI and likely to cause change tracking or trigger other behaviors. The following code needs to initialize the default value of `IsActive` without setting `HasPendingChanges` to `true`:

```cs
class SomeViewModel
{
    public bool HasPendingChanges { get; private set; }

    public bool IsActive { get; set => Set(ref field, value); } = true;

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

This difference in behavior between a property initializer and assigning from the constructor can also be seen with virtual auto properties in previous versions of the language:

```cs
using System;

// Nothing is printed; the property initializer is not
// equivalent to `this.IsActive = true`.
_ = new Derived();

class Base
{
    public virtual bool IsActive { get; set; } = true;
}

class Derived : Base
{
    public override bool IsActive
    {
        get => base.IsActive;
        set
        {
            base.IsActive = value;
            Console.WriteLine("This will not be reached");
        }
    }
}
```

### Constructor assignment

As with auto properties, assignment in the constructor calls the (potentially virtual) setter if it exists, and if there is no setter it falls back to directly assigning to the backing field.

```cs
class C
{
    public C()
    {
        P1 = 1; // Assigns P1's backing field directly
        P2 = 2; // Assigns P2's backing field directly
        P3 = 3; // Calls P3's setter
        P4 = 4; // Calls P4's setter
    }

    public int P1 => field;
    public int P2 { get => field; }
    public int P4 { get => field; set => field = value; }
    public int P3 { get => field; set; }
}
```

### Definite assignment in structs

Even though they can't be referenced in the constructor, backing fields denoted by the `field` keyword are subject to default-initialization and disabled-by-default warnings under the same conditions as any other struct fields ([LDM decision 1](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-02.md#property-assignment-in-structs), [LDM decision 2](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-05-02.md#definite-assignment-of-manually-implemented-setters)).

For example (these diagnostics are silent by default):

```cs
public struct S
{
    public S()
    {
        // CS9020 The 'this' object is read before all of its fields have been assigned, causing preceding implicit
        // assignments of 'default' to non-explicitly assigned fields.
        _ = P1;
    }

    public int P1 { get => field; }
}
```

```cs
public struct S
{
    public S()
    {
        // CS9020 The 'this' object is read before all of its fields have been assigned, causing preceding implicit
        // assignments of 'default' to non-explicitly assigned fields.
        P2 = 5;
    }

    public int P2 { get => field; set => field = value; }
}
```

### Nullability

A principle of the the Nullable Reference Types feature was to understand existing idiomatic coding patterns in C# and to require as little ceremony as possible around those patterns. The `field` keyword proposal enables simple, idiomatic patterns to address widely asked-for scenarios, such as lazily initialized properties. It's important for the Nullable Reference Types to mesh well with these new coding patterns.

Goals:

- A reasonable level of null-safety should be ensured for various usage patterns of the `field` keyword feature.

- Patterns that use the `field` keyword should feel as though they've always been part of the language. Avoid making the user jump through hoops to enable Nullable Reference Types in code that is perfectly idiomatic for the `field` keyword feature.

One of the key scenarios is lazily initialized properties:

```cs
public class C
{
    public C() { } // It would be undesirable to warn about 'Prop' being uninitialized here

    string Prop => field ??= GetPropValue();
}
```

The following nullability rules will apply not just to properties that use the `field` keyword, but also to existing auto properties.

#### Nullability of the *backing field*

See [Glossary](#glossary) for definitions of new terms.

The *backing field* has the same type as the property. However, its nullable annotation may differ from the property. To determine this nullable annotation, we introduce the concept of *null-resilience*. *Null-resilience* intuitively means that the property's `get` accessor preserves null-safety even when the field contains the `default` value for its type.

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

#### Constructor analysis

Currently, an auto property is treated very similarly to an ordinary field in [nullable constructor analysis](nullable-constructor-analysis.md). We extend this treatment to *field-backed properties*, by treating every *field-backed property* as a proxy to its backing field.

We update the following spec language from the previous [proposed approach](nullable-constructor-analysis.md#proposed-approach) to accomplish this:

> At each explicit or implicit 'return' in a constructor, we give a warning for each member whose flow state is incompatible with its annotations and nullability attributes. **If the member is a field-backed property, the nullable annotation of the backing field is used for this check. Otherwise, the nullable annotation of the member itself is used.** A reasonable proxy for this is: if assigning the member to itself at the return point would produce a nullability warning, then a nullability warning will be produced at the return point.

Note that this is essentially a constrained interprocedural analysis. We anticipate that in order to analyze a constructor, it will be necessary to do binding and "null-resilience" analysis on all applicable get accessors in the same type, which use the `field` contextual keyword and have *not-annotated* nullability. We speculate that this is not prohibitively expensive because getter bodies are usually not very complex, and that the "null-resilience" analysis only needs to be performed once regardless of how many constructors are in the type.

#### Setter analysis

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
        Prop = "a"; // ok
    }

    public static void Main()
    {
        new C().Prop.ToString(); // NRE at runtime
    }
}
```

The initial flow state of the *backing field* in the setter of a *field-backed property* is determined as follows:
- If the property has an initializer, then the initial flow state is the same as the flow state of the property after visiting the initializer.
- Otherwise, the initial flow state is the same as the flow state given by `field = default;`.

At each explicit or implicit 'return' in the setter, a warning is reported if the flow state of the *backing field* is incompatible with its annotations and nullability attributes.

#### Remarks

This formulation is intentionally very similar to ordinary fields in constructors. Essentially, because only the property accessors can actually refer to the backing field, the setter is treated as a "mini-constructor" for the backing field.

Much like with ordinary fields, we usually know the property was initialized in the constructor because it was set, but not necessarily. Simply returning within a branch where `Prop != null` was true is also good enough for our constructor analysis, since we understand that untracked mechanisms may have been used to set the property.

Alternatives were considered; see the [Nullability alternatives](#nullability-alternatives) section.

### `nameof`

In places where `field` is a keyword, `nameof(field)` will fail to compile ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-05-15.md#usage-in-nameof)), like `nameof(nint)`. It is not like `nameof(value)`, which is the thing to use when property setters throw ArgumentException as some do in the .NET core libraries. In contrast, `nameof(field)` has no expected use cases.

### Overrides

Overriding properties may use `field`. Such usages of `field` refer to the backing field for the overriding property, separate from the backing field of the base property if it has one. There is no ABI for exposing the backing field of a base property to overriding classes since this would break encapsulation.

Like with auto properties, properties which use the `field` keyword and override a base property must override all accessors ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-05-02.md#partial-overrides-of-virtual-properties)).

### Captures

`field` should be able to be captured in local functions and lambdas, and references to `field` from inside local functions and lambdas are allowed even if there are no other references ([LDM decision 1](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-21.md#open-question-in-semi-auto-properties), [LDM decision 2](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-05-15.md#should-field-and-value-be-considered-keywords-in-lambdas-and-local-functions-within-property-accessors)):

```cs
public class C
{
    public static int P
    {
        get
        {
            Func<int> f = static () => field;
            return f();
        }
    }
}
```

## Field usage warnings

When the `field` keyword is used in an accessor, the compiler's existing analysis of unassigned or unread fields will include that field.

- CS0414: The backing field for property 'Xyz' is assigned but its value is never used
- CS0649: The backing field for property 'Xyz' is never assigned to, and will always have its default value

## Specification changes

### Syntax

When compiling with language version 13 or higher, `field` is considered a keyword when used as a *primary expression* ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-07-15.md)) in the following locations ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2024/LDM-2024-05-15.md#field-and-value-as-contextual-keywords)):
- In method bodies of `get`, `set`, and `init` accessors in properties *but not* indexers
- In attributes applied to those accessors
- In nested lambda expressions and local functions, and in LINQ expressions in those accessors

In all other cases, including when compiling with language version 12 or lower, `field` is considered an identifier.

```diff
primary_no_array_creation_expression
    : literal
+   | 'field'
    | interpolated_string_expression
    | ...
    ;
```

### Properties

[§15.7.1](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1571-general) *Properties - General*

> A *property_initializer* may only be given for ~~an automatically implemented property, and~~ **a property that has a backing field that will be emitted. The *property_initializer*** causes the initialization of the underlying field of such properties with the value given by the *expression*.

[§15.7.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1574-automatically-implemented-properties) *Automatically implemented properties*

> An automatically implemented property (or auto-property for short), is a non-abstract, non-extern, non-ref-valued
> property with ~~semicolon-only accessor bodies. Auto-properties shall have a get accessor and may optionally have a set accessor.~~ **either or both of:**
> 1. **an accessor with a semicolon-only body**
> 2. **usage of the `field` contextual keyword within the accessors or**
>    **expression body of the property**
> 
> When a property is specified as an automatically implemented property, a hidden **unnamed** backing field is automatically
> available for the property ~~, and the accessors are implemented to read from and write to that backing field~~.
> **For auto-properties, any semicolon-only `get` accessor is implemented to read from, and any semicolon-only**
> **`set` accessor to write to its backing field.**
> 
> ~~The hidden backing field is inaccessible, it can be read and written only through the automatically implemented property accessors, even within the containing type.~~
> **The backing field can be referenced directly using the `field` keyword**
> **within all accessors and within the property expression body. Because the field is unnamed, it cannot be used in a**
> **`nameof` expression.**
> 
> If the auto-property has ~~no set accessor~~ **only a semicolon-only get accessor**, the backing field is considered `readonly` ([§15.5.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1553-readonly-fields)).
> Just like a `readonly` field, a read-only auto-property **(without a set accessor or an init accessor)** may also be assigned to in the body of a constructor
> of the enclosing class. Such an assignment assigns directly to the ~~read-only~~ backing field of the property.
> 
> **An auto-property is not allowed to only have a single semicolon-only `set` accessor without a `get` accessor.**
> 
> An auto-property may optionally have a *property_initializer*, which is applied directly to the backing field as a *variable_initializer* ([§17.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/arrays.md#177-array-initializers)).

The following example:
```csharp
// No 'field' symbol in scope.
public class Point
{
    public int X { get; set; }
    public int Y { get; set; }
}
```
is equivalent to the following declaration:
```csharp
// No 'field' symbol in scope.
public class Point
{
    public int X { get { return field; } set { field = value; } }
    public int Y { get { return field; } set { field = value; } }
}
```
which is equivalent to:
```csharp
// No 'field' symbol in scope.
public class Point
{
    private int __x;
    private int __y;
    public int X { get { return __x; } set { __x = value; } }
    public int Y { get { return __y; } set { __y = value; } }
}
```

The following example:
```csharp
// No 'field' symbol in scope.
public class LazyInit
{
    public string Value => field ??= ComputeValue();
    private static string ComputeValue() { /*...*/ }
}
```
is equivalent to the following declaration:
```csharp
// No 'field' symbol in scope.
public class Point
{
    private string __value;
    public string Value { get { return __value ??= ComputeValue(); } }
    private static string ComputeValue() { /*...*/ }
}
```

## Alternatives

### Nullability alternatives

In addition to the *null-resilience* approach outlined in the [Nullability](#nullability) section, the working group suggested the following alternatives for the LDM's consideration:

#### Do nothing

We could introduce no special behavior at all here. In effect:
- Treat a field-backed property the same way auto-properties are treated today--must be initialized in constructor except when marked required, etc.
- No special treatment of the field variable when analyzing property accessors. It is simply a variable with the same type and nullability as the property.

Note that this would result in nuisance warnings for "lazy property" scenarios, in which case users would likely need to assign `null!` or similar to silence constructor warnings.  
A "sub-alternative" we can consider is to also completely ignore properties using `field` keyword for nullable constructor analysis. In that case, there would be no warnings anywhere about the user needing to initialize anything, but also no nuisance for the user, regardless of what initialization pattern they may be using.

Because we are only planning to ship the `field` keyword feature under the Preview LangVersion in .NET 9, we expect to have some ability to change the nullable behavior for the feature in .NET 10. Therefore, we could consider adopting a "lower-cost" solution like this one in the short term, and growing up to one of the more complex solutions in the long term.

#### `field`-targeted nullability attributes

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

## Answered LDM questions

### Syntax locations for keywords

In accessors where `field` and `value` could bind to a synthesized backing field or an implicit setter parameter, in which syntax locations should the identifiers be considered keywords?
1. always
1. *primary expressions* only
1. never

The first two cases are breaking changes.

If the identifiers are *always* considered keywords, that is a breaking change for the following for instance:
```csharp
class MyClass
{
    private int field;
    public int P => this.field; // error: expected identifier

    private int value;
    public int Q
    {
        set { this.value = value; } // error: expected identifier
    }
}
```

If the identifiers are keywords when used as *primary expressions* only, the breaking change is smaller. The most common break may be unqualified use of an existing member named `field`.
```csharp
class MyClass
{
    private int field;
    public int P => field; // binds to synthesized backing field rather than 'this.field'
}
```

There is also a break when `field` or `value` is redeclared in a nested function. This may be the only break for `value` for *primary expressions*.
```csharp
class MyClass
{
    private IEnumerable<string> _fields;
    public bool HasNotNullField
    {
        get => _fields.Any(field => field is { }); // 'field' binds to synthesized backing field
    }
    public IEnumerable<string> Fields
    {
        get { return _fields; }
        set { _fields = value.Where(value => Filter(value)); } // 'value' binds to setter parameter
    }
}
```

If the identifiers are *never* considered keywords, the identifiers will only bind to a synthesized backing field or the implicit parameter when the identifiers do not bind to other members. There is no breaking change for this case.

#### Answer

`field` is a keyword in appropriate accessors when used as a *primary expression* only; `value` is never considered a keyword.

### Scenarios similar to `{ set; }`

`{ set; }` is currently disallowed and this makes sense: the field which this creates can never be read. There are now new ways to end up in a situation where the setter introduces a backing field that is never read, such as the expansion of `{ set; }` into `{ set => field = value; }`.

Which of these scenarios should be allowed to compile? Assume that the "field is never read" warning would apply just like with a manually declared field.

   1. `{ set; }` - Disallowed today, continue disallowing
   1. `{ set => field = value; }`
   1. `{ get => unrelated; set => field = value; }`
   1. `{ get => unrelated; set; }`
   1. ```cs
      {
          set
          {
              if (field == value) return;
              field = value;
              SendEvent(nameof(Prop), value);
          }
      }
      ```
   1. ```cs
      {
          get => unrelated;
          set
          {
              if (field == value) return;
              field = value;
              SendEvent(nameof(Prop), value);
          }
      }
      ```

#### Answer

Only disallow what is already disallowed today in auto properties, the bodyless `set;`.

### `field` in event accessor

Should `field` be a keyword in an event accessor, and should the compiler generate a backing field?

```csharp
class MyClass
{
    public event EventHandler E
    {
        add { field += value; }
        remove { field -= value; }
    }
}
```

**Recommendation**: `field` is *not* a keyword within an event accessor, and no backing field is generated.

#### Answer

Recommendation taken. `field` is *not* a keyword within an event accessor, and no backing field is generated.

### Nullability of `field`

Should the proposed nullability of `field` be accepted? See the [Nullability](#nullability) section, and the open question within.

#### Answer

General proposal is adopted. Specific behavior still needs more review.

### `field` in property initializer

Should `field` be a keyword in a property initializer and bind to the backing field?

```csharp
class A
{
    const int field = -1;

    object P1 { get; } = field; // bind to const (ok) or backing field (error)?
}
```

Are there useful scenarios for referencing the backing field in the initializer?

```csharp
class B
{
    object P2 { get; } = (field = 2);        // error: initializer cannot reference instance member
    static object P3 { get; } = (field = 3); // ok, but useful?
}
```

In the example above, binding to the backing field should result in an error: "initializer cannot reference non-static field".

#### Answer

We will bind the initializer as in previous versions of C#. We won't put the backing field in scope, nor will we prevent referencing other members named `field`.

### Interaction with partial properties

#### Initializers

When a partial property uses `field`, which parts should be allowed to have an initializer?

```cs
partial class C
{
    public partial int Prop { get; set; } = 1;
    public partial int Prop { get => field; set => field = value; } = 2;
}
```

- It seems clear that an error should occur when both parts have an initializer.
- We can think of use cases where either the definition or implementation part might want to set the initial value of the `field`.
- It seems like if we permit the initializer on the definition part, it is effectively forcing the implementer to use `field` in order for the program to be valid. Is that fine?
- We think it will be common for generators to use `field` whenever a backing field of the same type is needed in the implementation. This is in part because generators often want to enable their users to use `[field: ...]` targeted attributes on the property definition part. Using the `field` keyword saves the generator implementer the trouble of "forwarding" such attributes to some generated field and suppressing the warnings on the property. Those same generators are likely to also want to allow the user to specify an initial value for the field.

**Recommendation**: Permit an initializer on either part of a partial property when the implementation part uses `field`. Report an error if both parts have an initializer.

#### Answer

Recommendation accepted. Either declaring or implementing property locations can use an initializer, but not both at the same time.

#### Auto-accessors

As originally designed, partial property implementation must have bodies for all the accessors. However, recent iterations of the `field` keyword feature have included the notion of "auto-accessors". Should partial property implementations be able to use such accessors? If they are used exclusively, it will be indistinguishable from a defining declaration.

```cs
partial class C
{
    public partial int Prop0 { get; set; }
    public partial int Prop0 { get => field; set => field = value; } // this is equivalent to the two "semi-auto" forms below.

    public partial int Prop1 { get; set; }
    public partial int Prop1 { get => field; set; } // is this a valid implementation part?

    public partial int Prop2 { get; set; }
    public partial int Prop2 { get; set => field = value; } // what about this? will there be disagreement about which is the "best" style?

    public partial int Prop3 { get; }
    public partial int Prop3 { get => field; } // it will only be valid to use at most 1 auto-accessor, when a second accessor is manually implemented.
```

**Recommendation**: Disallow auto-accessors in partial property implementations, because the limitations around when they would be usable are more confusing to follow than the benefit of allowing them.

#### Answer

At least one implementing accessor must be manually implemented, but the other accessor can be automatically implemented.

### Readonly field

When should the synthesized backing field be considered *read-only*?

```csharp
struct S
{
    readonly object P0 { get => field; } = "";         // ok
    object P1          { get => field ??= ""; }        // ok
    readonly object P2 { get => field ??= ""; }        // error: 'field' is readonly
    readonly object P3 { get; set { _ = field; } }     // ok
    readonly object P4 { get; set { field = value; } } // error: 'field' is readonly
}
```

When the backing field is considered *read-only*, the field emitted to metadata is marked `initonly`, and an error is reported if `field` is modified other than in an initializer or constructor.

**Recommendation**: The synthesized backing field is *read-only* when the containing type is a `struct` and the property or containing type is declared `readonly`.

#### Answer

Recommendation is accepted.

## Open LDM questions

### Feature name

Some options for the name of the feature:
1. semi-auto properties
1. field access for auto properties [LDM-2023-07-17](https://github.com/dotnet/csharplang/blob/main/meetings/2023/LDM-2023-07-17.md#compiler-check-in)
1. field-backed properties
1. field keyword

### Readonly context and `set`

Should a `set` accessor be allowed in a `readonly` context for a property that uses `field`?

```csharp
readonly struct S1
{
    readonly object _p1;
    object P1 { get => _p1; set { } }   // ok
    object P2 { get; set; }             // error: auto-prop in readonly struct must be readonly
    object P3 { get => field; set { } } // ok?
}

struct S2
{
    readonly object _p1;
    readonly object P1 { get => _p1; set { } }   // ok
    readonly object P2 { get; set; }             // error: auto-prop with set marked readonly
    readonly object P3 { get => field; set { } } // ok?
}
 ```

### `[Conditional]` code

Should the synthesized field be generated when `field` is used only in omitted calls to [*conditional methods*](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/attributes.md#22532-conditional-methods)?

For instance, should a backing field be generated for the following in a non-DEBUG build?
```csharp
class C
{
    object P
    {
        get
        {
            Debug.Assert(field is null);
            return null;
        }
    }
}
```

For reference, fields for *primary constructor parameters* are generated in similar cases - see [sharplab.io](https://sharplab.io/#v2:EYLgxg9gTgpgtADwGwBYA+ABADAAgwRgDoARASwEMBzAOwgGcAXUsOgbgFgAoLjAJhwDCACgjAAVjDAMcAM1IwANgBMAlFwDeXHNrwocAWSFrOOnJpOmdxGMACulQgEE6dGFAZC5ipTlJ0c1LYKCiocFtoAvlxR3JyMULZSOADKIuKS0l7KxuamGHqGxqa5ltrWdg7Oru6e8sq+/oHBoVo6MRFAA).

**Recommendation**: The backing field is generated when `field` is used only in omitted calls to *conditional methods*.