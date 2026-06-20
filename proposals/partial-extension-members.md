# Partial extension members

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Champion issue: https://github.com/dotnet/csharplang/issues/10227  

## Summary

Allow partial extension members in extension block in partial enclosing static classes:
```cs
public static partial class E
{
    extension(int i)
    {
        public partial void M();
    }
}
public static partial class E
{
    extension(int i)
    {
        public partial void M() { }
    }
}
```

## Motivation

It is possible to have a source generator produce the implementation of a partial classic extension method:
```cs
public static partial class E
{
    public static partial void M(this int i);
}
// generated:
public static partial class E
{
    public static partial void M(this int i) { }
}
```

But it is not possible with an extension block member (method or property):
```cs
public static partial class E
{
    extension(int i)
    {
        public partial void M(); // error: A partial member must be declared within a partial type
    }
}
// generated:
public static partial class E
{
    extension(int i)
    {
        public partial void M() { } // error: A partial member must be declared within a partial type
    }
}
```

## Details

Partial extension members can be declared in an `extension` block within a `partial` enclosing type.
The definition and implementation parts can be declared in the same or different extension blocks (within an enclosing static partial class).  

The member declarations must match according to the same rules as regular partial members,
and the signatures of the containing extension blocks must also match according to the same rules as regular partial members (treating the signature as a method signature with no return type).
Note: two extension blocks having matching signatures implies that they have the same marker type name.

Existing rules for partial methods still apply.
For instance:
- There must be only one definition part and no more than one implementation part for a valid partial member.  
- Partial methods without an accessibility modifier are erasable, meaning that if the implementation part is missing, the definition part is removed without error and the call-sites are also removed.  
- Partial extension methods with an accessibility modifier are non-erasable, meaning that an error is produced if the implementation part is missing.  

Matching definition and implementation parts are not required to have matching name for parameters, the extension parameter or type parameters on the method or the extension block. 
The canonical names for callers are those from the definition part, both for method parameters and for the extension receiver parameter. 

Callers can only call the partial extension member using the signature of the definition part:
```cs
E.M(t: 1, i1: 2); // ok
1.M(i1: 2); // ok

E.M(t2: 1, 2); // error
E.M(1, i2: 2); // error
1.M(i2: 2); // error

partial static class E
{
    extension<T>(T t) { public partial T M<U>(U i1); }
    extension<T2>(T2 t2) { public partial T2 M<U2>(U2 i2) => t2; }
}
```

The `partial` modifier remains disallowed on an `extension` block:
```cs
partial class E
{
    partial extension(int i) { } // error
}
```

## Open issues

### Attributes on extension parameters

Attributes on regular partial methods or their parameters get merged across all parts.
But extension block members are associated with a specific marker type name
which includes any attributes on the extension parameter.

What should we do if the extension parameter has different attributes in the definition and implementation parts?
```cs
extension([A] string s) { public partial void M(); }
extension([B] string s) { public partial void M() { } }
```

Options:
1. treat the definition part as canonical (drop `[B]` for this implementating part) and potentially warn
2. emit the member is a new marker type with the merged attributes (`[A, B]`) but otherwise ignoring specificities of the signature of the "implementing" extension block (such as parameter names)
3. require that the attributes on the extension parameter match in all parts

### Doc comments

For regular partial methods, doc comments on the implementation take priority, doc comments on the definition are used as fallback.  
I propose we do the same for partial extension members.

Doc comments on the extension block get emitted for the marker type. If multiple extension blocks get merged, their doc comments get merged too.  
I propose we do the same for extension members in partial types: each marker type that is emitted will get its doc, combining the doc comments from any extension blocks that are emitted as that marker type.

### Dropping otherwise empty extension blocks

We have considered, but not yet implemented, dropping empty extension blocks such as:
```cs
extension(int i) { }
```

There was not a strong motivation to do this, as one could simply not write the block in the first place.  
But partial extension members may make this more common.

The following example effectively yields an `extension<T>(T t)` block
with a method and an empty `extension<T2>(T2 t2)` block.
```cs
extension<T>(T t) { public partial T Id(); }
extension<T2>(T2 t2) { public partial T2 Id() => t2; }
```
Should we drop extension blocks that only contain an implementation part?

We should also consider whether to drop extension blocks that are empty once
erasable partial extension methods are removed:
```cs
extension(int i) { partial void Unused(); }
```

Note: this choice (whether to skip emitting otherwise empty extension blocks) interacts with the two earlier questions (attributes and xml docs).  

## References
- https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/classes.md#1569-partial-methods
- https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/extending-partial-methods.md
- https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/partial-properties.md
- https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md
- https://github.com/dotnet/roslyn/issues/81165 (issue tracking some motivating scenarios)

