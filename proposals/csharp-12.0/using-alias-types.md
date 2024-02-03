# Allow using alias directive to reference any kind of Type

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
Relax the using_alias_directive ([§13.5.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/namespaces.md#1352-using-alias-directives)) to allow it to point at any sort of type, not just named types.  This would support types not allowed today, like: tuple types, pointer types, array types, etc.  For example, this would now be allowed:

```c#
using Point = (int x, int y);
```

## Motivation
For ages, C# has had the ability to introduce aliases for namespaces and named types (classes, delegated, interfaces, records and structs).  This worked acceptably well as it provided a means to introduce non-conflicting names in cases where a normal named pulled in from `using_directive`s might be ambiguous, and it allowed a way to provide a simpler name when dealing with complex generic types.  However, the rise of additional complex type symbols in the language has caused more use to arise where aliases would be valuable but are currently not allowed.  For example, both tuples and function-pointers often can have large and complex regular textual forms that can be painful to continually write out, and a burden to try to read.  Aliases would help in these cases by giving a short, developer-provided, name that can then be used in place of those full structural forms.

## Detailed design
We will change the grammar of `using_alias_directive` thusly:

```
using_alias_directive
-    : 'using' identifier '=' namespace_or_type_name ';'
+    : 'using' identifier '=' (namespace_name | type) ';'
    ;
```

Top-level reference type nullability annotations are disallowed.

Interestingly, most of the spec language in [§13.5.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/namespaces.md#1352-using-alias-directives) does not need to change.  Most language in it already refers to 'namespace or type', for example:

> A using_alias_directive introduces an identifier that serves as an alias for a namespace or type within the immediately enclosing compilation unit or namespace body.

This remains true, just that the grammar now allows the 'type' to be any arbitrary type, not the limited set allowed for by `namespace_or_type_name` previously.

The sections that do need updating are:

```diff
- The order in which using_alias_directives are written has no significance, and resolution of the namespace_or_type_name referenced by a using_alias_directive is not affected by the using_alias_directive itself or by other using_directives in the immediately containing compilation unit or namespace body. In other words, the namespace_or_type_name of a using_alias_directive is resolved as if the immediately containing compilation unit or namespace body had no using_directives. A using_alias_directive may however be affected by extern_alias_directives in the immediately containing compilation unit or namespace body. In the example
+ The order in which using_alias_directives are written has no significance, and resolution of the `(namespace_name | type)` referenced by a using_alias_directive is not affected by the using_alias_directive itself or by other using_directives in the immediately containing compilation unit or namespace body. In other words, the `(namespace_name | type)` of a using_alias_directive is resolved as if the immediately containing compilation unit or namespace body had no using_directives. A using_alias_directive may however be affected by extern_alias_directives in the immediately containing compilation unit or namespace body. In the example
```

```diff
- The namespace_name referenced by a using_namespace_directive is resolved in the same way as the namespace_or_type_name referenced by a using_alias_directive. Thus, using_namespace_directives in the same compilation unit or namespace body do not affect each other and can be written in any order.
+ The namespace_name referenced by a using_namespace_directive is resolved in the same way as the namespace_or_type_name referenced by a using_alias_directive. Thus, using_namespace_directives in the same compilation unit or namespace body do not affect each other and can be written in any order.
```

```diff
+ It is illegal for a using alias type to be a nullable reference type.

    1. `using X = string?;` is not legal.
    2. `using X = List<string?>;` is legal.  The alias is to `List<...>` which is itself not a nullable reference type itself, even though it contains one as a type argument.
    3. `using X = int?;` is legal.  This is a nullable *value* type, not a nullable *reference* type.
```

## Supporting aliases to types containing pointers.

A new [unsafe context](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/unsafe-code.md#222-unsafe-contexts) is added through an optional 'unsafe' keyword in the using_alias_directive production:

```diff
using_alias_directive
+    : 'using' 'unsafe'? identifier '=' (namespace_name | type) ';'
    ;
    
using_static_directive
+    : 'using' 'static' 'unsafe'? type_name ';'
    ;

+ 'unsafe' can only be used with an using_alias_directive or using_static_directive, not a using_directive.
+ The 'unsafe' keyword present in a 'using_alias_directive' causes the entire textual extent of the 'type' portion (not the 'namespace_name' portion) to become an unsafe context. 
+ The 'unsafe' keyword present in a 'using_static_directive' causes the entire textual extent of the 'type_name' portion to become an unsafe context. 
```


