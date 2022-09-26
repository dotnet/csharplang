# File-local types

## Summary
[summary]: #summary

Permit a `file` modifier on top-level type declarations. The type only exists in the file where it is declared.

```cs
// File1.cs
namespace NS;

file class Widget
{
}

// File2.cs
namespace NS;

file class Widget // different symbol than the Widget in File1
{
}

// File3.cs
using NS;

var widget = new Widget(); // error: The type or namespace name 'Widget' could not be found.
```

## Motivation
[motivation]: #motivation

Our primary motivation is from source generators. Source generators work by adding files to the user's compilation.
1. Those files should be able to contain implementation details which are hidden from the rest of the compilation, yet are usable throughout the file they are declared in.
2. We want to reduce the need for generators to "search" for type names which won't collide with declarations in user code or code from other generators.

## Detailed design
[design]: #detailed-design

- We add the `file` modifier to the following modifier sets:
  - [class](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/classes.md#1422-class-modifiers)
  - [struct](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/structs.md#1522-struct-modifiers)
  - [interface](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/interfaces.md#1722-interface-modifiers)
  - [enum](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/enums.md#183-enum-modifiers)
  - [delegate](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/delegates.md#192-delegate-declarations)
  - record
  - record struct.
- The `file` modifier can only be used on a top-level type.

When a type has the `file` modifier, it is said to be a *file-local* type.

### Accessibility
No accessibility modifiers can be used in combination with `file` on a type. `file` is treated as an independent concept from accessibility. Since file-local types can't be nested, only the default accessibility `internal` is usable with `file` types.

```cs
public file class C1 { } // error
internal file class C2 { } // error
file class C3 { } // ok
```

### Naming
The implementation guarantees that file-local types in different files with the same name will be distinct to the runtime. The type's accessibility and name in metadata is implementation-defined. The intention is to permit the compiler to adopt any future access-limitation features in the runtime which are suited to the feature. It's expected that in the initial implementation, an `internal` accessibility would be used and an unspeakable generated name will be used which depends on the file the type is declared in.

### Lookup
We amend the [member lookup](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#115-member-lookup) section as follows (new text in **bold**):

> - Next, if `K` is zero, all nested types whose declarations include type parameters are removed. If `K` is not zero, all members with a different number of type parameters are removed. When `K` is zero, methods having type parameters are not removed, since the type inference process ([§11.6.3](expressions.md#1163-type-inference)) might be able to infer the type arguments.
> - **Next, let *F* be the compilation unit which contains the expression where member lookup is occurring. All members which are file-local types and are not declared in *F* are removed from the set.**
> - **Next, if the set of accessible members contains file-local types, all non-file-local types are removed from the set.**

#### Remarks
These rules disallow usage of file-local types outside the file in which they are declared.

These rules also permit *shadowing* of a non-file-local type by a file-local type:
```cs
// File1.cs
class C
{
    public static void M() { }
}
```

```cs
// File2.cs
file class C
{
    public static void M() { }
}

class Program
{
    static void Main()
    {
        C.M(); // refers to the 'C' in File2.cs
    }
}
```

Note that we don't update the [scopes](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/basic-concepts.md#77-scopes) section of the spec. This is because, as the spec states:

> The ***scope*** of a name is the region of program text within which it is possible to refer to the entity declared by the name without qualification of the name.

In effect, scope only impacts the lookup of non-qualified names. This isn't quite the right concept for us to leverage because we need to also impact the lookup of qualified names:

```cs
// File1.cs
namespace NS1
{
    file class C
    {
        public static void M() { }
    }
}

namespace NS2
{
    class Program
    {
        public static void M()
        {
            C.M(); // error: C is not in scope
            NS1.C.M(); // ok: C can be accessed through NS1.
        }
    }
}
```

```cs
// File2.cs
namespace NS1
{
    class Program
    {
        C.M(); // error
        NS1.C.M(); // error
    }
}
```

Therefore, we don't specify the feature in terms of which scope the type is contained in, but rather as additional "filtering rules" in member lookup.

### Attributes
File-local classes are permitted to be attribute types, and can be used as attributes within both file-local types and non-file-local types, just as if the attribute type were a non-file-local type. The metadata name of the file-local attribute type still goes through the same name generation strategy as other file-local types. This means detecting the presence of a file-local type by a hard-coded string name is likely to be impractical, because it requires depending on the internal name generation strategy of the compiler, which may change over time. However, detecting via `typeof(MyFileLocalAttribute)` works.

```cs
using System;
using System.Linq;

file class MyFileLocalAttribute : Attribute { }

[MyFileLocalAttribute]
public class C
{
    public static void Main()
    {
        var attribute = typeof(C).CustomAttributes.Where(attr => attr.AttributeType == typeof(MyFileLocalAttribute)).First();
        Console.Write(attribute); // outputs the generated name of the file-local attribute type
    }
}
```

### Usage in signatures
There is a general need to prevent file-local types from appearing in member parameters, returns, and type parameter constraints where the file-local type might not be in scope at the point of usage of the member.

Note that non-file-local types are permitted to implement file-local interfaces, similar to how types can implement less-accessible interfaces. Depending on the types present in the interface members, it could result in a violation of the rules in the following section.

#### Only allow signature usage in members of file-local types
Perhaps the simplest way to ensure this is to enforce that file-local types can only appear in signatures or as base types of other file-local types:

```cs
file class FileBase
{
}

public class Derived : FileBase // error
{
    private FileBase M2() => new FileBase() // error
}

file class FileDerived : FileBase // ok
{
    private FileBase M2() => new FileBase() // ok
}
```

Note that this does restrict usage in explicit implementations, even though such usages are safe. We do this in order to simplify the rules for the initial iteration of the feature.

```cs
file interface I
{
    void M(I i);
}

class C : I
{
    void I.M(I i) { } // error
}
```

### Implementation/overrides
file-local type declarations can implement interfaces, override virtual methods, etc. just like regular type declarations.

```cs
file struct Widget : IEquatable<Widget>
{
    public bool Equals(Widget other) => true;
}
```
