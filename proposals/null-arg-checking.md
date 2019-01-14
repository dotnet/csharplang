# Simplified Null Argument Checknig

## Summary
This proposal provides a simplified syntax for validating method arguments are not `null` and 
throwing `ArgumentNullException` appropriately.

## Motivation
The work on designing nullable reference types has caused us to examine the code necessary for 
null argument validation. Given that NRT doesn't affect code execution developers still must 
add `if (arg is null) throw` boiler plate code even in projects which are fully null clean. This
gave us the desire to explore a minimal syntax for argument `null` validation in the language. While
anticipated to pair often with NRT the proposal is independent of it. 

## Detailed Design 
The bang operator, `!`, can be positioned after any identifier in a parameter list and this will 
cause the C# compilet to emit standard `null` checking code for that parameter. For example:

``` csharp
void M(string name!) {
    ...
}
```

Will be translated into:

``` csharp
void M(string name!) {
    if (name is null) {
        throw new ArgumentNullException(nameof(name));
    }
    ...
}
```

The generated `null` check will occur before any user authored code in the method. When multiple 
parameters contain the `!` operator then the checks will occur in the same order as the parameters
are declared.

The check will be specifically for reference equality to `null`, it does not invoke `==` or any user 
defined operators. This also means the `!` operator can only be added to parameters whose type can
have the value `null`. Value types and type parameters not constrained to `class` or `interface`
cannot be used here. 

``` csharp
// Errro: Cannot use ! on parameter of type T. 
void G<T>(T arg!) {

}
```

In the case of a constructor that uses `this` or `base` to chain to another constructor the 
`null` validation will occur after such a call. 

``` csharp
class C {
    C(string name!) :this(name) {
        ...
    }
}
```

Will be translated into:

``` csharp
class C {
    C(string name!) :this(name) {
        if (name is null) {
            throw new ArgumentNullException(nameof(name));
        }
        ...
    }
}
```

The `!` operator can only be used for parameter lists which have an associated method body. This
means it cannot be used in an `abstract` method, `interface`, `delegate` or `partial` method 
definition.

## Open Issuess
- There is no way to check the `value` argument of a property setter for `null` using this syntax. 

## Considerations
-  In the case a constructor chains to another constructor with `this` or `base` the `null` 
validation could occur before the chaining call. This is a stronger guarantee the developer could 
desire. However C# code cannot be authored that way today and it seems unlikely that it's a 
significant issue. Leaving the check after the chaining call means existing code can be ported
to the new pattern without fear of compat concerns.

## Future Considerations
None