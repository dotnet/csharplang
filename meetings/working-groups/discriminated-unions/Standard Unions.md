# Standard Type Unions

## Summary

A family of nominal type unions exist in the `System` namespace that can be used across assemblies as the standard way to specify a type union without needing to declare and name one.

```csharp
public union Union<T1, T2>(T1, T2);
public union Union<T1, T2, T3>(T1, T2, T3);
public union Union<T1, T2, T3, T4>(T1, T2, T3, T4);
...

Union<int, string> x = 10;
...
var _ = x switch 
{
    int v => ...,
    string v => ...
}
```

In addition, the existence of these standardized unions can be used as the basis of *Anonymous Type Unions* and *Inferred Type Unions* features.

## Specification

### Runtime Library

The union types are added to the same assembly that houses the `ValueTuple`, `Func` and `Action` types. 
