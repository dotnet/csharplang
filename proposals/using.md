# using patterns and locals

## Summary



## Motivation

## Detailed Design 

### using pattern

The language will add the notion of the disposable pattern: that is a type which has an accessible 
`Dispose` method.
The language will no longer require a type implement `IDisposable` in order to 

Restrictions:

- The `Dispose` method must have a void return type.
- The `Dispose` method must be parameterless. 

## Considerations

### fixed locals

A `fixed` statement has all of the properties of `using` statements that motivated the ability
to have `using` locals. Consideration should be given to extending this feature to `fixed` locals
as well. The lifetime and ordering rules should apply equally well for `using` and `fixed` here.


