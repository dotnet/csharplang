# Using alias improvements

## Summary

Extend `using` aliases to allow generic type aliases and additional alias target types.

## Generic type aliases

Type aliases may include type parameters.
```C#
using MyList<T> = System.Collections.Generic.List<T>; // ok
```

Type parameters do not need to be referenced in the target.
```C#
using MyType1<T> = System.String; // ok
using MyType2<T, U> = T;          // ok
```

Type parameter references in an alias target have the same restrictions as type parameter references in other generic contexts.
```C#
using MyType3<T, U> = T.U;  // error: reported at use site only?
using MyType3<T, U> = T<U>; // error: reported at use site only?
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

### Constraints
If type parameter constraints cannot be declared explicitly, then constraints in the alias target are verified at the use site only.
```C#
using Option<T> = System.Nullable<T>; // ok
static void F<T>(Option<T> t) { ... } // error: 'T' must be value type in 'Nullable<T>'
```

However, without explicit constraints, the compiler will treat `T?` as `Nullable<T>` in an alias target which means nullable reference types cannot be used with type parameters in aliases.

The alternative is to allow explicit type parameter constraints, perhaps using the same syntax as generic type and method constraint clauses.
```C#
using MaybeNull<T> = T? where T : class;
using Option<T> = System.Nullable<T> where T : struct;
```

### Variance
Type parameter variance cannot be specified explicitly in the alias declaration, because any resulting variance constraints would not be enforced at the public boundary of the assembly.

Instead variance is implied by the alias target and verified at the use site only.
```C#
using MyEnumerable<T> = System.Collections.Generic.IEnumerable<T>;
MyEnumerable<object> e = new string[0]; // ok
```

## Additional alias target types

Alias targets may include primitive types, arrays, pointers, function pointers, tuples, and `?`.
```C#
using MyInt = int;
using MyArray = int[];
using Option<T> = T?;
```

Tuples may include element names.
```C#
using MyTuple = (int Id, string Name);
```

Alias targets may not reference aliases.
```C#
using MyType1 = System.Int32;
using MyType2 = System.Nullable<MyType1>; // error: type or namespace 'MyType1' not found
```

`unsafe` is required for aliases with pointers at the use site but not at the declaration.
```C#
using MyPointer = int*; // ok
static void F(MyPointer ptr) { ... } // error: pointers require unsafe context
```

## Syntax

```antlr
using_directive
    : 'using' ('static')? name ';'
    | 'using' identifier_token type_parameter_list? '=' (name | type) ';'
    ;
```

## Alternatives
`using` aliases are not first class types, and although this proposal extends the expressiveness of aliases, it does not address that fundamental limitation.

Should type aliases be represented with an alternative approach (such as "roles") where aliases are first class types in metadata and member signatures?

## See also

- https://github.com/dotnet/csharplang/issues/1239
- https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-07-13.md#generics-and-generic-type-parameters-in-aliases
- https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-09-28.md#proposal-support-generics-and-generic-type-parameters-in-aliases
