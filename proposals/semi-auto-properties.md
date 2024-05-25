# `field` keyword in properties

## Summary

Extend all properties to allow them to reference an automatically generated backing field using the new contextual keyword `field`. Properties may now also contain an accessor _without_ a body alongside an accessor _with_ a body.

## Motivation

Auto properties only allow for directly setting or getting the backing field, giving some control only by placing access modifiers on the accessors. Sometimes there is a need to have additional control over what happens in one or both accessors, but this confronts users with the overhead of declaring a backing field. The backing field name must then be kept in sync with the property, and the backing field is scoped to the entire class which can result in accidental bypassing of the accessors from within the class.

There are two common scenarios in particular: applying a constraint on the setter to ensuring the validity of a value, and raising an event such as `INotifyPropertyChanged.PropertyChanged`.

In these cases by now you always have to create an instance field and write the whole property yourself.  This not only adds a fair amount of code, but it also leaks the backing field into the rest of the type's scope, when it is often desirable to only have it be available to the bodies of the accessors.

## Glossary

- **Auto property**: Short for "automatically implemented property" ([§15.7.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1574-automatically-implemented-properties)). Accessors on an auto property have no body. The implementation and backing storage are both provided by the compiler.

- **Auto accessor**: Short for "automatically implemented accessor." This is an accessor that has no body. The implementation and backing storage are both provided by the compiler.

- **Full accessor**: This is an accessor that has a body. The implementation is not provided by the compiler, though the backing storage may still be (as in the example `set => field = value;`).

## Detailed design

For properties with an `init` accessor, everything that applies below to `set` would apply instead to the `init` accessor.

**Principle 1:** Every property declaration can be thought of as having a backing field by default, which is elided when not used. The field is referenced using the keyword `field` and its visibility is scoped to the accessor bodies.

**Principle 2:** `get;` will now be considered syntactic sugar for `get => field;`, and `set;` will now be considered syntactic sugar for `set => field = value;`.

Both of these principles only apply under the same conditions where `{ get; }` or `{ get; set; }` already declares an auto property ([§15.7.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1574-automatically-implemented-properties)). For example, abstract and interface properties are excluded. Indexers also remain unaffected.

"Auto property" will continue to mean a property whose accessors have no bodies. None of the examples below will be considered auto properties.

This means that properties may now mix and match auto accessors with full accessors. For example:

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

As with auto properties, a setter that uses a backing field is disallowed when there is no getter. This restriction could be loosened in the future to allow the setter to do something only in response to changes, by comparing `value` to `field` (see open questions).

```cs
// ❌ Error, will not compile
{ set => field = value; }
```

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

This luckily gives control over whether or not you want to initialize the backing field directly or call the property setter: if you want to initialize without calling the setter, you use a property initializer. If you want to initialize by calling the setter, you use assign the property an initial value in the constructor.

Here's an example of where this is useful. The `field` keyword will find a lot of its use with view models because of the neat solution it brings for the `INotifyPropertyChanged` pattern. View model property setters are likely to be databound to UI and likely to cause change tracking or trigger other behaviors. The following code needs to initialize the default value of `IsActive` without setting `HasPendingChanges` to `true`:

```cs
using System.Runtime.CompilerServices;

class SomeViewModel
{
    public bool HasPendingChanges { get; private set; }

    public bool IsActive { get; set => Set(ref field, value); } = true;

    private bool Set<T>(ref T location, T value)
    {
        if (RuntimeHelpers.Equals(location, value)) return false;
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

As with auto properties, assignment in the constructor calls the setter if it exists, and if there is no setter it falls back to directly assigning to the backing field.

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

A property which uses an automatic backing field will be treated as an auto property for the purposes of calculating default backing field initialization if its setter is automatically implemented, or if it does not have a setter ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-02.md#property-assignment-in-structs)).

Default-initialize a struct when calling a manually implemented setter of a property which uses an automatic backing field, and issue a warning when doing so, like a regular property setter ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-05-02.md#definite-assignment-of-manually-implemented-setters)).

### Nullability

When `{ get; }` is written as `{ get => field; }`, or `{ get; set; }` is written as `{ get => field; set => field = value; }`, a similar warning should be produced when a non-nullable property is not initialized:

```cs
class C
{
    // ⚠️ CS8618: Non-nullable property 'P' must contain a
    // non-null value when exiting constructor.
    public string P { get => field; set => field = value; }
}
```

No warning should be produced if the property is initialized to a non-null value via constructor assignment or property initializer:

```cs
class C
{
    public C() { P = ""; }

    public string P { get => field; set => field = value; }
}
```

```cs
class C
{
    public string P { get => field; set => field = value; } = "";
}
```

In the same vein as how `var` infers as nullable for reference types, the `field` type should be nullable for reference types. This makes sense of `field ??` as not being followed by dead code, and it avoids producing a misleading warning in the following example:

```cs
public string AmbientValue
{
    get => field ?? parent.AmbientValue;
    set
    {
        if (value == parent.AmbientValue)
            field = null; // No warning here. Resume following the parent's value.
        else
            field = value; // Stop following the parent's value
    }
}
```

Just like with nullability of `var`, it's expected that such patterns will exist with manually-declared backing fields.

To land in this sweet spot implicitly, without having to write an attribute each time, nullability analysis will combine an inherent nullability of the field with the behavior of `[field: NotNull]`. This allows maybe-null assignments without warning, which is desirable as shown above, while simultaneously allowing a scenario like `=> field.Trim();` without requiring an intervention to silence a warning that `field` could be null. Making sure `field` has been assigned is already covered by the warning that ensures non-nullable properties are assigned by the end of each constructor.

### `nameof`

In places where `field` is a keyword (see the [Shadowing](#shadowing) section), `nameof(field)` will fail to compile, like `nameof(nint)`. It is not like `nameof(value)`, which is the thing to use when property setters throw ArgumentException as some do in the .NET core libraries. In contrast, `nameof(field)` has no expected use cases. If it did anything, it would return the string `"field"`, consistent with how `nameof` behaves in other circumstances by returning the C# name or alias, rather than the metadata name.

### Overrides

Overriding properties may use `field`. Such usages of `field` refer to the backing field for the overriding property, separate from the backing field of the base property if it has one. There is no ABI for exposing the backing field of a base property to overriding classes since this would break encapsulation.

Like with auto properties, properties which use the `field` keyword and override a base property must override all accessors ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-05-02.md#partial-overrides-of-virtual-properties)).

### Shadowing

`field` can be shadowed by parameters or locals in a nested scope ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-16.md#open-questions-in-field)). Since `field` represents a field in the type, even if anonymously, the shadowing rules of regular fields should apply.

### Captures

`field` should be able to be captured in local functions and lambdas, and references to `field` from inside local functions and lambdas should be allowed even if there are no other references ([LDM decision](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-21.md#open-question-in-semi-auto-properties)):

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

## Specification changes

The following changes are to be made to [§14.7.4](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/classes.md#1474-automatically-implemented-properties):
```
### Automatically implemented properties
```

...

```diff
- A *property_initializer* may only be given for an automatically implemented property ([Automatically implemented properties](classes.md#automatically-implemented-properties)), and causes the initialization of the underlying field of such properties with the value given by the *expression*.
+ A *property_initializer* may only be given for a property that has a backing field that will be emitted and the property either does not have a setter, or its setter is auto-implemented. The *property_initializer* causes the initialization of the underlying field of such properties with the value given by the *expression*.
```

...

```diff
- An automatically implemented property (or ***auto-property*** for short), is a non-abstract non-extern
- property with semicolon-only accessor bodies. Auto-properties must have a get accessor and can optionally
- have a set accessor.
+ An automatically implemented property (or ***auto-property*** for short), is a non-abstract non-extern
+ property with either or both of:
```

```diff
- When a property is specified as an automatically implemented property, a hidden backing field is automatically
- available for the property, and the accessors are implemented to read from and write to that backing field. If
- the auto-property has no set accessor, the backing field is considered `readonly` ([Readonly fields](classes.md#readonly-fields)).
- Just like a `readonly` field, a getter-only auto-property can also be assigned to in the body of a constructor 
- of the enclosing class. Such an assignment assigns directly to the readonly backing field of the property.
+ 1. an accessor with a semicolon-only body
+ 2. usage of the `field` contextual keyword ([Keywords](lexical-structure.md#keywords)) within the accessors or
+    expression body of the property. The `field` identifier is only considered the `field` keyword when there is
+    no existing symbol named `field` in scope at that location.
+
+ When a property is specified as an auto-property, a hidden, unnamed, backing field is automatically available for
+ the property. For auto-properties, any semicolon-only `get` accessor is implemented to read from, and any semicolon-only
+ `set` accessor to write to its backing field. The backing field can be referenced directly using the `field` keyword
+ within all accessors and within the property expression body. Because the field is unnamed, it cannot be used in a
+ `nameof` expression.
+
+ If the auto-property does not have a set accessor, the backing field can still be assigned to in the body of a 
+ constructor of the enclosing class. Such an assignment assigns directly to the backing field of the property.
+
+ If the auto-property has only a semicolon-only get accessor, the backing field is considered `readonly` ([Readonly fields](classes.md#readonly-fields)).
+
+ An auto-property is not allowed to only have a single semicolon-only `set` accessor without a `get` accessor.
```

...

```diff
- If the auto-property has no set accessor, the backing field is considered `readonly` ([Readonly fields](classes.md#readonly-fields)). Just like a `readonly` field, a getter-only auto-property can also be assigned to in the body of a constructor of the enclosing class. Such an assignment assigns directly to the readonly backing field of the property.
+ If the auto-property has semicolon-only get accessor (without a set accessor or with an init accessor), the backing field is considered `readonly` ([Readonly fields](classes.md#readonly-fields)). Just like a `readonly` field, a getter-only auto property (without a set accessor or an init accessor) can also be assigned to in the body of a constructor of the enclosing class. Such an assignment assigns directly to the backing field of the property.
```

...

````diff
+The following example:
+```csharp
+// No 'field' symbol in scope.
+public class Point
+{
+    public int X { get; set; }
+    public int Y { get; set; }
+}
+```
+is equivalent to the following declaration:
+```csharp
+// No 'field' symbol in scope.
+public class Point
+{
+    public int X { get { return field; } set { field = value; } }
+    public int Y { get { return field; } set { field = value; } }
+}
+```
+which is equivalent to:
+```csharp
+// No 'field' symbol in scope.
+public class Point
+{
+    private int __x;
+    private int __y;
+    public int X { get { return __x; } set { __x = value; } }
+    public int Y { get { return __y; } set { __y = value; } }
+}
+```

+The following example:
+```csharp
+// No 'field' symbol in scope.
+public class LazyInit
+{
+    public string Value => field ??= ComputeValue();
+    private static string ComputeValue() { /*...*/ }
+}
+```
+is equivalent to the following declaration:
+```csharp
+// No 'field' symbol in scope.
+public class Point
+{
+    private string __value;
+    public string Value { get { return __value ??= ComputeValue(); } }
+    private static string ComputeValue() { /*...*/ }
+}
+```
````

## Open LDM questions

1. If a type does have an existing accessible `field` symbol in scope (like a field called `field`) should there be any way for a property to still use `field` internally to both create and refer to an automatically-implemented backing field.  Under the current rules there is no way to do that.  This is certainly unfortunate for those users, however this is ideally not a significant enough issue to warrant extra dispensation.  The user, after all, can always still write out their properties like they do today, they just lose out from the convenience here in that small case.

1. Which of these scenarios should be allowed to compile? Assume that the "field is never read" warning would apply just like with a manually declared field.

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

## LDM history:
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-10.md#field-keyword
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-14.md#field-keyword
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-01-12.md#open-questions-for-field
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-02-16.md#open-questions-in-field
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-02.md#open-questions-in-field
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-03-21.md#open-question-in-semi-auto-properties
- https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-05-02.md#field-questions
