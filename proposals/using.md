# using patterns and locals

## Summary

The language will add two need capabilities around the `using` statement in order to make resoure
managemente simpler: recognize a `using` pattern in addition to `IDisposable` and add a `using`
declaration to the language.

## Motivation

The `using` statement is an effective tool for resource management today but it requires quite a 
bit of ceremony. Methods that have a number of resources to manage can get syntactically bogged 
down with a series of `using` statements. This syntax burden is enough that most coding style 
guidelines explicitly have an exception around braces for this scenario. 

The `using` declaration removes much of the ceremony here and gets C# on par with other languages
that include resource management blocks. Additionally the `using` pattern lets a developers expand
the set of tyse that can participate here. In many cases removing the need to create wrapper types 
that only exist to allow for a values use in a `using` statement. 

Together these features allow developers to simplify and expand the scenarios where `using` can
be applied.

## Detailed Design 

### using pattern

The language will add the notion of a disposable pattern: that is a type which has an accessible 
Dispose instance method. Types which fit the disposable pattern can participate in a `using` 
statement or declaration without being required to implement `IDisposable`. 

``` csharp
class Resource { 
    public void Dispose() { ... }
}

using (var r = new Resource()) {
    // statements
}
```

This will allow developers to leverage `using` in a number of new scenarios:

- `ref struct`: These types can't implement interfaces today and hence can't participate in `using`
statements.
- Extension methods will allow developers to augment types in other assemblies to participate 
in `using` statements.

In the situation where a type can be implicitly converted to `IDisposable` and also fits the
`using` pattern, then `IDisposable` will be prefered. While this takes the opposite of approach
of `foreach` (pattern prefered over interface) it is necessary for backwards compatibility.

The same restrictions from a traditional `using` statement apply here as well: local variables 
declared in the `using` are read-only, a `null` value will not cause an exception to be thrown, 
etc ... The code geneartion will be different only in that there will not be a cast to 
`IDisposable` before calling Dispose:

``` csharp
{
	Resource r = new Resource();
	try {
		// statements
	}
	finally {
		if (resource != null) resource.Dispose();
	}
}
```

In order to fit the `using` pattern the Dispose method must be accessible, parameterless and have 
a `void` return type. There are no other restrictions. This explicitly means that extension methods
can be used here.

### using declaration

## Considerations

### case labels without blocks

A `using declaration` is illegal directly inside a `case` label due to complications around it's 
actual lifetime. One potential solution is to simply give it the same lifetime as an `out var` 
in the same location. It was deemed the extra complexity to the feature implementation and the 
ease of the work around (just add a block to the `case` label) didn't justify taking this route.

## Future Expansions

### fixed locals

A `fixed` statement has all of the properties of `using` statements that motivated the ability
to have `using` locals. Consideration should be given to extending this feature to `fixed` locals
as well. The lifetime and ordering rules should apply equally well for `using` and `fixed` here.


