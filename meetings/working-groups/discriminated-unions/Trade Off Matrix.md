# Union Trade Offs

## Solution Space

There are only three possible ways to represent a type that behaves like a discriminated union or type union in C# without a large runtime overhaul.

1) As a class Hierarchy
```csharp
    public abstract record MyUnion
    {
        public record Case1(...);
        public record Case2(...);
        public record Case3(...);
    }

    MyUnion union = new MyUnion.Case1(...);
```

2) As an object reference
```csharp
    object union; // only assign Case1, Case2 or Case3 here please.
    ...
    union = new Case1(...);
```

3) As a wrapper type
```csharp
    public struct MyUnion
    {
        public MyUnion(Case1 value) {...}
        public MyUnion(Case2 value) {...}
        public MyUnion(Case3 value) {...}
        public object Value { get; }
    }

    MyUnion union = new MyUnion(new Case1(...));
```

These are exactly what developers use when defining the equivalent of discriminated unions and type unions in C# code today.

However, none of these user defined solutions allow for exhaustiveness in pattern matching, because they are either too open ended or not understood as being closed by the runtime and language.

The following proposals offer solutions for exhaustiveness and more, with a matrix of trade-offs between them.

- The `Closed Hierarchies` proposal offers an easy way to make class hierarchies closed.
- The `Runtime Type Unions` proposal offers a special kind of object reference that is understood by the runtime.
- The `Nominal Type Unions` proposal offers a wrapper type solution understood by the language.

## Trade Off Matrix

| Feature                               |  Runtime Type Unions | Nominal Type Unions | Closed Hierarchies |
|---------------------------------------|----------------------|---------------------|--------------------|
| Declared Cases                        | **Yes**              | **Yes**             | **Yes**            |
| Singleton Cases                       | **Yes**              | **Yes**      	     | **Yes**            |
| Existing Cases                       	| **Yes**              | **Yes**             |                    |
| Anonymous Syntax                      | **Yes**              |                     |                    |
| Pattern Matching                      | **Yes**              | **Yes**             | **Yes**            |
| Dynamic Pattern Matching              | **Yes**		       |                     | **Yes**            |
| Subtype Relationship                  |                      |                     | **Yes**            |
| Conversion Relationship               | **Yes**              |                     | **Yes**            |
| Back-Compat       			        |                      | **Yes**		     | **Yes**            |
| Non-ABI Breaking                      | *named only*         | **Yes**             | **Yes**            |
| Non-Allocating/Boxing			        |                      | *future*            |                    |
| Custom Unions 	                    |                      | *future*            |                    |
| Any Time Soon                         |	                   | **Yes**             | **Yes**            |

- *Declared Cases* - Case types can be declared as part of the union type declaration.
- *Singleton Cases* - Singleton cases can be declared and used without additional allocations.
- *Existing Cases* - Existing types can be used as cases w/o declaring and allocating case type wrappers.
- *Anonymous Syntax* - A type expression syntax exists to refer to a union without declaring a named typed.
- *Pattern Matching* - Pattern matching works directly on union instance.
- *Dynamic Pattern Matching* - Pattern matching works when union types are not statically known.
- *Subtype Relationship* - A subtype relationship exists between Union and Case types.
- *Conversion Relationship* - A conversion relationship exists between Union and Case types. 
- *Back-Compat* - Union types can be used with older runtimes and older language versions.
- *Non-ABI Breaking* - Adding or reordering cases does not cause binary breaks.
- *Non-Allocating/Boxing* - Union and case values can be used without any kind of allocation or boxing.
- *Custom Unions* - Possible to declare a type that behaves like a union type in the language but has a custom implementation.
- *Any Time Soon* - Could appear in next few C# releases.



