# Extension constants

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Champion issue: https://github.com/dotnet/csharplang/issues/10242

## Summary

Allow `const` declarations inside extension blocks:

```cs
public static class E
{
    extension(object)
    {
        public const int Member = 42;
    }
}
```

The constant is then consumable through extension member lookup on the receiver type:

```cs
_ = object.Member; // 42
```

## Motivation

Extension blocks currently support methods, properties, operators, and indexers, but not constants. This leaves a gap for API shapes where a compile-time constant is naturally part of an extension surface.

Today, authors must choose between:

- a static property (`public static int Member => 42;`), which is not a compile-time constant and cannot be used in constant-expression contexts, or
- a separate non-extension static constant, which is not discovered through extension member lookup.

Adding extension constants enables extension-centric APIs to expose symbolic values while retaining constant-expression semantics.

## Declaration

### Grammar

Extension constants are added to the set of permitted members inside an extension declaration by extending the grammar as follows (relative to [proposals/csharp-14.0/extensions.md](proposals/csharp-14.0/extensions.md#syntax)):

```antlr
extension_member_declaration
    : method_declaration
    | property_declaration
    | constant_declaration // new
    | operator_declaration
    ;
```

Only `constant_declaration` is permitted. Non-const fields remain disallowed in extension blocks.

```cs
public static class E
{
    extension(object)
    {
        public const int I = 1; // OK
        public static int F;    // Error: not allowed in an extension block
    }
}
```

### Modifiers and existing constant rules

Extension constants follow the same language rules as other constants:

- `static` is not permitted on a `const` declaration.
- The declared type and initializer must satisfy constant rules.
- `public`, `internal`, `private`, and `new` are permitted.
- `protected` (and related forms) remain disallowed by extension-member accessibility rules.

In particular, extension constants preserve existing `const` rules and diagnostics:

- The declared type must be a permitted constant type.
- The initializer must be a `constant_expression`.
- For reference types other than `string`, the only permitted constant value is `null`.
- Normal compile-time diagnostics for invalid constant types/initializers continue to apply.

## Consumption

Extension constants participate in extension member lookup and resolution as non-method extension members.

- If normal member lookup on the receiver type finds a member, normal precedence rules apply.
- Otherwise, extension member resolution considers applicable extension constants.
- Applicability uses the same receiver compatibility model as extension members generally (including extension block type parameters and constraints).
- If multiple extension constants with the same name are applicable in the selected extension scope, resolution applies an overload-resolution-like betterness step using the receiver type as the single argument.
- Like other `const` fields, extension constants are static members and therefore must be accessed through a type receiver (`T.Member`), not an instance receiver (`t.Member`).
- Extension constants can also be accessed through the enclosing static class (`E.Member`) as a disambiguation syntax.

Examples:

```cs
public static class E
{
    extension<T>(T) where T : class
    {
        public const int Member = 42;
    }
}

_ = string.Member; // OK
_ = E.Member;      // OK
```

```cs
public static class E1
{
    extension(object) { public const int Member = 42; }
}
public static class E2
{
    extension(object) { public static int Member => 42; }
}

_ = object.Member; // ambiguity
```

```cs
public static class E
{
    extension(object) { public const int Member = 1; }
    extension(string) { public const int Member = 2; } // error: cannot have two `E.Member` members
}
```

```cs
public static class E1
{
    extension(object) { public const int Member = 1; }
}
public static class E2
{
    extension(string) { public const int Member = 2; }
}

_ = object.Member; // E1.Member
_ = string.Member; // E2.Member, extension(string) is better than extension(object)
```

### Constant-expression contexts

Because extension constants are constants, they are valid in constant-expression contexts where ordinary constants are valid, for example:

- attribute arguments,
- `const` initializers,
- default parameter values,
- pattern constants,
- `case` labels.

## Metadata model

Extension constants follow the extension-member lowering model by being emitted on the synthesized extension marker type corresponding to the extension block. A corresponding constant field is also emitted on the enclosing static class, enabling the enclosing-class disambiguation form (`E.Member`).

The metadata encoding of the constant is consistent with normal constants: a static literal field with constant value metadata (for non-`decimal` types) or a static field with the appropriate decimal constant attribute (for `decimal` types).

## References

- [C# 14 extension members proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md)
- [Extension indexers proposal](https://github.com/dotnet/csharplang/blob/main/proposals/extension-indexers.md)
