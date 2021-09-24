# Allow using alias directive to reference any kind of Type

## Summary
Relax the [using_alias_directive](https://github.com/dotnet/csharplang/blob/main/spec/namespaces.md#using-alias-directives) to allow it to point at any sort of type, not just named types.  This would support types not allowed today, like: tuple types, pointer types, array types, etc.  For example, this would now be allowed:

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

Interestingly, most of the spec language in [using_alias_directive](https://github.com/dotnet/csharplang/blob/main/spec/namespaces.md#using-alias-directives) does not need to change.  Most language in it already refers to 'namespace or type', for example:

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


## Design meeting open questions.

This section needs to be resolved in a design meeting.

The intent of this specification is to allow one to write something like:

```
using MyPointer = My*;
```

The spec is currently unclear if this would be ok or not.  Technically, the `using_alias_directive` here is not in an `unsafe` context, so the `My*` could be considered an error.  However, the spirit of this specification is that should be allowed, and only the *usages* of `MyPointer` would themselves have to either be in another `using_alias_directive` or in an `unsafe` context.  Another way this could be formalized is that the `(namespace_name | type)` portion of a `using_alias_directive` would always be an `unsafe` context, but that wouldn't negate the fact that any place that alias was referenced would also need to be an `unsafe` context.


--

Similarly what should be done about `using NullablePerson = Person?; // Person is a reference type`?  My intuition is that this is fine (though should only be legal if the *using* is in a `#nullable enable` section).  The meaning of `NullablePerson` in all reference locations is `Person?` (even if that location is `#nullable disable`).  However, depending on the nullability region where it is referenced you may or may not get nullable warnings around it.
