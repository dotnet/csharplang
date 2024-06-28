# Partial properties
https://github.com/dotnet/csharplang/issues/6420

### Grammar

The partial modifier on properties is treated just like any other modifier and can be included in any position in a modifier list.

The property grammar [(§15.7.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1571-general) is updated as follows:

```diff
 property_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'static'
     | 'virtual'
     | 'sealed'
     | 'override'
     | 'abstract'
     | 'extern'
+    | 'partial'
     ;
```

See also [Relaxed ordering requirement](#relaxed-ordering-requirement) for similar changes to the method, class, struct and interface grammars.

### Defining and implementing declarations
When a property declaration includes a *partial* modifier, that property is said to be a *partial property*. Partial properties may only be declared as members of partial types.

A *partial property* declaration is said to be a *defining declaration* when its accessors all have semicolon bodies, and it lacks the `extern` modifier. Otherwise, it is an *implementing declaration*.

```cs
partial class C
{
    // Defining declaration
    public partial string Prop { get; set; }

    // Implementing declaration
    public partial string Prop { get => field; set => field = value; }
}
```

Because we have reserved the syntactic form with semicolon accessor bodies for the *defining declaration*, a partial property cannot be *automatically implemented*. We therefore adjust [Automatically implemented properties (§15.7.4)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1574-automatically-implemented-properties) as follows:

> An automatically implemented property (or auto-property for short), is a non-abstract, non-extern, **non-partial,** non-ref-valued property with semicolon-only accessor bodies.

**Remarks**. It is useful for the compiler to be able to look at a single declaration in isolation and know whether it is a defining or an implementing declaration. Therefore we don't want to permit auto-properties by including two identical `partial` property declarations, for example. We don't think that the use cases for this feature involve implementing the partial property with an auto-property, but in cases where a trivial implementation is desired, we think the `field` keyword makes things simple enough.

---

A partial property must have one *defining declaration* and one *implementing declaration*.

**Remarks**. We also don't think it is useful to allow splitting the declaration across more than two parts, to allow different accessors to be implemented in different places, for example. Therefore we simply imitate the scheme established by partial methods.

---

Only the defining declaration of a partial property participates in lookup, similar to how only the defining declaration of a partial method participates in overload resolution.

**Remarks**. In the compiler, we would expect that only the symbol for the defining declaration appears in the member list, and the symbol for the implementing part can be accessed through the defining symbol. However, some features like nullable analysis might *see through* to the implementing declaration in order to provide more useful behavior.

```cs
partial class C
{
    public partial string Prop { get; set; }
    public partial string Prop { get => field; set => field = value; }

    public C() // warning CS8618: Non-nullable property 'Prop' must contain a non-null value when exiting constructor. Consider declaring the property as nullable.
    {
    }
}
```

---

A partial property is not permitted to have the `abstract` modifier.

A partial property cannot explicitly implement interface properties.

### Attribute merging

Similar to partial methods, the attributes in the resulting property are the combined attributes of the parts are concatenated in an unspecified order, and duplicates are not removed.

### Caller-info attributes
We adjust the following language from the [standard](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/attributes.md#22551-general):

> It is an error to have the same caller-info attribute on a parameter of both the defining and implementing part of a partial ~~method~~ **member** declaration. Only caller-info attributes in the defining part are applied, whereas caller-info attributes occurring only in the implementing part are ignored.

- The described error falls out from the definitions of these attributes not having `AllowMultiple = true`. Using them multiple times, including across partial declarations, results in an error.
- When caller-info attributes are applied to a parameter in the implementation part of a partial method, the Roslyn compiler reports a warning. It will also report a warning for the same scenario in a partial property.

### Matching signatures

The LDM meeting on [14th September 2020](https://github.com/dotnet/csharplang/blob/main/meetings/2020/LDM-2020-09-14.md#partial-method-signature-matching) defined a set of "strict" requirements for signature matching of partial methods, which were introduced in a warning wave. Partial properties have analogous requirements to partial methods for signature matching as much as is possible, except that all of the diagnostics for mismatch are reported by default, and are not held behind a warning wave.

Signature matching requirements include:
1. Type and ref kind differences between partial property declarations which are significant to the runtime result in a compile-time error.
2. Differences in tuple element names within partial property declarations results in a compile-time error, same as for partial methods.
3. The property declarations and their accessor declarations must have the same modifiers, though the modifiers may appear in a different order.
    - Exception: this does not apply to the `extern` modifier, which may only appear on an *implementing declaration*.
4. All other syntactic differences in the signatures of partial property declarations result in a compile-time warning, with the following exceptions:
    - Attribute lists on or within partial property declarations do not need to match. Instead, merging of attributes in corresponding positions is performed, per [Attribute merging](#attribute-merging).
    - Nullable context differences do not cause warnings. In other words, a difference where one of the types is nullable-oblivious and the other type is either nullable-annotated or not-nullable-annotated does not result in any warnings.
    - Default parameter values do not need to match. A warning is reported when the implementation part of a partial indexer has default parameter values. This is similar to an existing warning which occurs when the implementation part of a partial method has default parameter values.
5. A warning occurs when parameter names differ across defining and implementing declarations. The parameter names from the definition part are used at use sites and in emit.
6. Nullability differences which do not involve oblivious nullability result in warnings. When analyzing an accessor body, the implementation part signature is used. The definition part signature is used when analyzing use sites and in emit. This is consistent with partial methods.

```cs
partial class C1
{
    public partial string Prop { get; private set; }

    // Error: accessor modifier mismatch in 'set' accessor of 'Prop'
    public partial string Prop { get => field; set => field = value; }
}

partial class C2
{
    public partial string Prop { get; init; }

    // Error: implementation of 'Prop' must have an 'init' accessor to match definition
    public partial string Prop { get => field; set => field = value; }
}

partial class C3
{
    public partial string Prop { get; }

    // Error: implementation of 'Prop' cannot have a 'set' accessor because the definition does not have a 'set' accessor.
    public partial string Prop { get => field; set => field = value; }
}

partial class C4
{
    public partial string this[string s = "a"] { get; set; }
    public partial string this[string s] { get => s; set { } } // ok

    public partial string this[int i, string s = "a"] { get; set; }
    public partial string this[int i, string s = "a"] { get => s; set { } } // CS1066: The default value specified for parameter 's' will have no effect because it applies to a member that is used in contexts that do not allow optional arguments
}
```

### Documentation comments

We want the behavior of doc comments on partial properties to be consistent with what we shipped for partial methods. That behavior is detailed in https://github.com/dotnet/csharplang/issues/5193.

It is permitted to include doc comments on either the definition or implementation part of a partial property. (Note that doc comments are not supported on property accessors.)

When doc comments are present on only one of the parts of the property, those doc comments are used normally (surfaced through `ISymbol.GetDocumentationCommentXml()`, written out to the documentation XML file, etc.).

When doc comments are present on both parts, all the doc comments on the definition part are dropped, and only the doc comments on the implementation part are used.

For example, the following program:
```cs
/// <summary>
/// My type
/// </summary>
partial class C
{
    /// <summary>Definition part comment</summary>
    /// <returns>Return value comment</returns>
    public partial int Prop { get; set; }
    
    /// <summary>Implementation part comment</summary>
    public partial int Prop { get => 1; set { } }
}
```

Results in the following XML documentation file:
```xml
<?xml version="1.0"?>
<doc>
    <assembly>
        <name>ConsoleApp1</name>
    </assembly>
    <members>
        <member name="T:C">
            <summary>
            My type
            </summary>
        </member>
        <member name="P:C.Prop">
            <summary>
            Implementation part comment
            </summary>
        </member>
    </members>
</doc>
```

When parameter names differ between partial declarations, `<paramref>` elements use the parameter names from the declaration associated with the documentation comment in source code. For example, a paramref on a doc comment placed on an implementing declaration refers to the parameter symbols on the implementing declaration using their parameter names. This is consistent with partial methods.

```cs
/// <summary>
/// My type
/// </summary>
partial class C
{
    public partial int this[int x] { get; set; }

    /// <summary>
    /// <paramref name="x"/> // warning CS1734: XML comment on 'C.this[int]' has a paramref tag for 'x', but there is no parameter by that name
    /// <paramref name="y"/> // ok. 'Go To Definition' will go to 'int y'.
    /// </summary>
    public partial int this[int y] { get => 1; set { } } // warning CS9256: Partial property declarations 'int C.this[int x]' and 'int C.this[int y]' have signature differences.
}
```

Results in the following XML documentation file:
```xml
<?xml version="1.0"?>
<doc>
    <assembly>
        <name>ConsoleApp1</name>
    </assembly>
    <members>
        <member name="T:C">
            <summary>
            My type
            </summary>
        </member>
        <member name="P:C.Item(System.Int32)">
            <summary>
            <paramref name="x"/> // warning CS1734: XML comment on 'C.this[int]' has a paramref tag for 'x', but there is no parameter by that name
            <paramref name="y"/> // ok. 'Go To Definition' will go to 'int y'.
            </summary>
        </member>
    </members>
</doc>
```

This can be confusing, because the metadata signature will use parameter names from the definition part. It is recommended to ensure that parameter names match across parts to avoid this confusion.

### Indexers

Per [LDM meeting on 2nd November 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-11-02.md#partial-properties), indexers will be supported with this feature.

The indexers grammar is modified as follows:
```diff
indexer_declaration
-    : attributes? indexer_modifier* indexer_declarator indexer_body
+    : attributes? indexer_modifier* 'partial'? indexer_declarator indexer_body
-    | attributes? indexer_modifier* ref_kind indexer_declarator ref_indexer_body
+    | attributes? indexer_modifier* 'partial'? ref_kind indexer_declarator ref_indexer_body
    ;
```

Partial indexer parameters must match across declarations per the same rules as [Matching signatures](#matching-signatures). [Attribute merging](#attribute-merging) is performed across partial indexer parameters.

```cs
partial class C
{
    public partial int this[int x] { get; set; }
    public partial int this[int x]
    {
        get => this._store[x];
        set => this._store[x] = value;
    }
}

// attribute merging
partial class C
{
    public partial int this[[Attr1] int x]
    {
        [Attr2] get;
        set;
    }

    public partial int this[[Attr3] int x]
    {
        get => this._store[x];
        [Attr4] set => this._store[x] = value;
    }

    // results in a merged member emitted to metadata:
    public int this[[Attr1, Attr3] int x]
    {
        [Attr2] get => this._store[x];
        [Attr4] set => this._store[x] = value;
    }
}
```

### Relaxed ordering requirement

- https://github.com/dotnet/csharplang/issues/946
- https://github.com/dotnet/csharplang/blob/main/meetings/2020/LDM-2020-09-09.md#champion-relax-ordering-constraints-around-ref-and-partial-modifiers-on-type-declarations

In C# 12 and prior, `partial` can only be used as the last modifier before a method return type, or as the last modifier before type declaration keywords `class`, `struct`, or `interface`. Similar restrictions are in place for the `ref` modifier on a `struct` declaration.

In C# 13, `partial` will become just like any other modifier on the above-mentioned declarations, and can appear in any order in the modifier list. The same behavior will apply for properties. The `ref` modifier will be similarly relaxed on `struct` declarations.

#### Detailed design

The property grammar is given in [Grammar](#grammar).

The method grammar [(§15.6.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1561-general) is updated as follows:

```diff
 method_modifiers
-    : method_modifier* 'partial'?
+    : method_modifier*
     ;

 method_modifier
     : ref_method_modifier
     | 'async'
     ;
 
 ref_method_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'static'
     | 'virtual'
     | 'sealed'
     | 'override'
     | 'abstract'
     | 'extern'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

The classes grammar [(§15.2.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1521-general) is updated as follows:

```diff
 class_declaration
-    : attributes? class_modifier* 'partial'? 'class' identifier
+    : attributes? class_modifier* 'class' identifier
         type_parameter_list? class_base? type_parameter_constraints_clause*
         class_body ';'?
     ;

 class_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'abstract'
     | 'sealed'
     | 'static'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

The interfaces grammar [(§18.2.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/interfaces.md#1821-general) is updated as follows:

```diff
 interface_declaration
-    : attributes? interface_modifier* 'partial'? 'interface'
+    : attributes? interface_modifier* 'interface'
       identifier variant_type_parameter_list? interface_base?
       type_parameter_constraints_clause* interface_body ';'?
     ;

 interface_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

The structs grammar [(§18.2.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1621-general) is updated as follows:

```diff
 struct_declaration
-    : attributes? struct_modifier* 'ref'? 'partial'? 'struct'
+    : attributes? struct_modifier* 'struct'
       identifier type_parameter_list? struct_interfaces?
       type_parameter_constraints_clause* struct_body ';'?
     ;
```

```diff
 struct_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'readonly'
+    | 'ref'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

## Open Issues

### Other member kinds

A community member opened a discussion to request support for [partial events](https://github.com/dotnet/csharplang/discussions/8064). In the [LDM meeting on 2nd November 2022](https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-11-02.md#partial-properties), we decided to punt on support for events, in part because nobody at the time had requested it. We may want to revisit this question, since this request has now come in, and it has been over a year since we last discussed it.

We could also go even further in permitting partial declarations of constructors, operators, fields, and so on, but it's unclear if the design burden of these is justified, just because we are already doing partial properties.
