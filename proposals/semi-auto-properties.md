# Semi-auto-properties (a.k.a. `field` keyword in properties)

## Summary
Extend auto-properties to allow them to still have an automatically generated backing field, while still allowing for bodies to be provided for accessors.  Auto-properties can also use a new contextual `field` keyword in their body to refer to the auto-prop field.

## Motivation
Standard auto-properties only allow for setting or getting the backing field directly, giving some control only by access modifying the accessor methods. Sometimes there is more need to have control over what happens when accessing an auto-property, without being confronted with all overhead of a standard property.

Two common scenarios are that you want to apply a constraint on the setter, ensuring the validity of a value. The other being raising an event that informs about the property going to be changed/having been changed.

In these cases by now you always have to create an instance field and write the whole property yourself.  This not only adds a fair amount of code, but it also leaks the `field` into the rest of the type's scope, when it is often desirable to only have it be available to the bodies of the accessors.

## Specification changes

The following changes are to be made to [classes.md](https://github.com/dotnet/csharplang/blob/main/spec/classes.md):
```
### Automatically implemented properties
```

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

## Open LDM questions:

1. If a type does have an existing accessible `field` symbol in scope (like a field called `field`) should there be any way for an auto-prop to still use `field` internally to both create and refer to an auto-prop field.  Under the current rules there is no way to do that.  This is certainly unfortunate for those users, however this is ideally not a significant enough issue to warrant extra dispensation.  The user, after all, can always still write out their properties like they do today, they just lose out from the convenience here in that small case.

2. Should initializers use the backing field or the property setter? If the latter, what about `public int P { get => field; } = 5;`?
    * Calling a setter for an initializer is not an option because initializers are processed before calling base constructor and it is illegal to call any instance method before the base constructor is called.
    * If we say that initializer assigns directly to the backing field. If there is a setter, then we are getting into a situation when initializer does one thing and an assignment to the property within constructor does a different thing (calls the setter). A behavior like that can be a trap for a user. Today, there is no semantical difference between an initializer and an assignment in constructor when both are allowed. This invariant will be broken. However, people are changing between the forms often.

3. Definite assignment related questions:
    - https://github.com/dotnet/csharplang/issues/5563
    - https://github.com/dotnet/csharplang/pull/5573#issuecomment-1002110830

## LDM history:
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-03-10.md#field-keyword
- https://github.com/dotnet/csharplang/blob/main/meetings/2021/LDM-2021-04-14.md#field-keyword
