# "pattern-based using" and "using declarations"

## Summary

The language will add two new capabilities around the `using` statement in order to make resource
management simpler: `using` should recognize a disposable pattern in addition to `IDisposable` and add a `using`
declaration to the language.

## Motivation

The `using` statement is an effective tool for resource management today but it requires quite a 
bit of ceremony. Methods that have a number of resources to manage can get syntactically bogged 
down with a series of `using` statements. This syntax burden is enough that most coding style 
guidelines explicitly have an exception around braces for this scenario. 

The `using` declaration removes much of the ceremony here and gets C# on par with other languages
that include resource management blocks. Additionally the pattern-based `using` lets developers expand
the set of types that can participate here. In many cases removing the need to create wrapper types 
that only exist to allow for a values use in a `using` statement. 

Together these features allow developers to simplify and expand the scenarios where `using` can
be applied.

## Detailed Design 

### using declaration

The language will allow for `using` to be added to a local variable declaration. Such a declaration
will have the same effect as declaring the variable in a `using` statement at the same location.

```csharp
if (...) 
{ 
   using FileStream f = new FileStream(@"C:\users\jaredpar\using.md");
   // statements
}

// Equivalent to 
if (...) 
{ 
   using (FileStream f = new FileStream(@"C:\users\jaredpar\using.md")) 
   {
    // statements
   }
}
```

The lifetime of a `using` local will extend to the end of the scope in which it is declared. The 
`using` locals will then be disposed in the reverse order in which they are declared. 

```csharp
{ 
    using var f1 = new FileStream("...");
    using var f2 = new FileStream("..."), f3 = new FileStream("...");
    ...
    // Dispose f3
    // Dispose f2 
    // Dispose f1
}
```

There are no restrictions around `goto`, or any other control flow construct in the face of 
a `using` declaration. Instead the code acts just as it would for the equivalent `using` statement:

```csharp
{
    using var f1 = new FileStream("...");
  target:
    using var f2 = new FileStream("...");
    if (someCondition) 
    {
        // Causes f2 to be disposed but has no effect on f1
        goto target;
    }
}
```

A local declared in a `using` local declaration will be implicitly read-only. This matches the 
behavior of locals declared in a `using` statement. 

The language grammar for `using` declarations will be the following:

```antlr
local-using-declaration:
  using type using-declarators

using-declarators:
  using-declarator
  using-declarators , using-declarator
  
using-declarator:
  identifier = expression
```

Restrictions around `using` declaration:

- May not appear directly inside a `case` label but instead must be within a block inside the
 `case` label.
- May not appear as part of an `out` variable declaration. 
- Must have an initializer for each declarator.
- The local type must be implicitly convertible to `IDisposable` or fulfill the `using` pattern.

### pattern-based using

The language will add the notion of a disposable pattern: that is a type which has an accessible 
`Dispose` instance method. Types which fit the disposable pattern can participate in a `using` 
statement or declaration without being required to implement `IDisposable`. 

```csharp
class Resource
{ 
    public void Dispose() { ... }
}

using (var r = new Resource())
{
    // statements
}
```

This will allow developers to leverage `using` in a number of new scenarios:

- `ref struct`: These types can't implement interfaces today and hence can't participate in `using`
statements.
- Extension methods will allow developers to augment types in other assemblies to participate 
in `using` statements.

In the situation where a type can be implicitly converted to `IDisposable` and also fits the
disposable pattern, then `IDisposable` will be preferred. While this takes the opposite approach
of `foreach` (pattern preferred over interface) it is necessary for backwards compatibility.

The same restrictions from a traditional `using` statement apply here as well: local variables 
declared in the `using` are read-only, a `null` value will not cause an exception to be thrown, 
etc ... The code generation will be different only in that there will not be a cast to 
`IDisposable` before calling Dispose:

```csharp
{
	  Resource r = new Resource();
	  try {
		    // statements
	  }
	  finally {
		    if (r != null) r.Dispose();
	  }
}
```

In order to fit the disposable pattern the `Dispose` method must be accessible, parameterless and have 
a `void` return type. There are no other restrictions. This explicitly means that extension methods
can be used here.

## Considerations

### case labels without blocks

A `using declaration` is illegal directly inside a `case` label due to complications around its 
actual lifetime. One potential solution is to simply give it the same lifetime as an `out var` 
in the same location. It was deemed the extra complexity to the feature implementation and the 
ease of the work around (just add a block to the `case` label) didn't justify taking this route.

## Future Expansions

### fixed locals

A `fixed` statement has all of the properties of `using` statements that motivated the ability
to have `using` locals. Consideration should be given to extending this feature to `fixed` locals
as well. The lifetime and ordering rules should apply equally well for `using` and `fixed` here.
