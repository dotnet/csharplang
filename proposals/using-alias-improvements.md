# Using alias improvements

## Summary

Support extensions to `using` aliases: assembly-wide aliases; generic type aliases; and additional alias targets.

## Assembly-wide aliases

`using` aliases with a `global` modifier are assembly-wide aliases that apply to all compilation units in the assembly.

Assembly-wide aliases may be declared at the root scope, not within a `namespace`.
```C#
global using MyInt = System.Int32; // ok
namespace MyNamespace
{
    global using MyString = System.String; // error: declared in 'MyNamespace'
}
```
It is an error if there are multiple declarations of the same assembly-wide alias in the compilation, even if the declarations are identical.
Assembly-wide aliases can be hidden by names in the compilation unit, including local `using` aliases, although it is an error if the assembly-wide alias and local alias are declared in the same compilation unit.

Aliases are not represented in metadata and are not available outside the assembly, not even in assemblies with `[InternalsVisibleTo]` access.

## Generic type aliases

Type aliases may include type parameters.
```C#
using MyList<T> = System.Collections.Generic.List<T>; //ok
```

Type parameter constraints cannot be specified explicitly. Any constraints from type parameters in the alias target must be satisfied at the use site.
```C#
using Option<T> = System.Nullable<T>;
static void F<T>(Option<T> t) { ... } // error: 'T' must be value type in 'Nullable<T>'
```

Since constraints cannot be specified, use of `T?` is treated as `System.Nullable<T>` if `T` is a type parameter in the alias. _This prevents use of nullable reference types for type parameters in aliases._

Type parameter variance cannot be specified explicitly. Variance is inferred from the alias target.
```C#
using MyEnumerable<T> = IEnumerable<T>;
static void F(MyEnumerable<object> e) { ... };
F(new string[0]); // ok
```

The target for a generic alias must be a type. _What other restrictions are there on the target?_
```C#
using MyType1<T> = System.String; // ok?
using MyType2<T> = T;             // ok?
using MyType3<T, U> = T<U>;       // ok?
```

The same name may be used for multiple aliases with distinct arity.
```C#
using MyDictionary = System.Collections.Generic.Dictionary<string, object>;
using MyDictionary<T> = System.Collections.Generic.Dictionary<string, T>; // ok
using MyDictionary<U> = System.Collections.Generic.Dictionary<string, U>; // error: 'MyDictionary<>' already defined
```

A partially bound type is not valid.
```C#
using MyList<T> = System.Collections.Generic.List<T>;
using MyDictionary<T> = System.Collections.Generic.Dictionary<string, T>;
_ = typef(MyList<>);        // ok: 'List<>'
_ = typeof(MyDictionary<>); // error: 'Dictionary<string,>' is not valid
```

## Additional alias targets

Alias targets may include: primitive types, arrays, pointers, function pointers, `T?`, tuples, and other type aliases.
Tuples may include element names.
```C#
using MyInt = int;
using MyArray = int[];
using Option<T> = T?;
using MyTuple = (int Id, string Name);
```

`unsafe` is required at the use site but not at the declaration for aliases including pointers.
```C#
using MyPointer = int*; // ok
static void F(MyPointer ptr) { ... } // error: pointers require unsafe context
```

It is an error for a type alias target to depend on itself, directly or indirectly.
```C#
using MyType1 = List<MyType2>; // error: 'MyType1' cannot depend on 'MyType1'
using MyType2 = IEnumerable<MyType1>;
```

## Syntax

```antlr
using_directive
    : 'using' ('static')? name ';'
    | ('global')? 'using' identifier_token type_parameter_list? '=' (name | type) ';'
    ;
```

## Design meetings

Previous proposals:
- https://github.com/dotnet/csharplang/issues/1239
- https://github.com/dotnet/csharplang/discussions/1300
