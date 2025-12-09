# Extension indexers

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Declaration

Like all extension members, extension indexers are declared within an extension block:

``` c#
public static class Indexers
{
    extension(int i)
    {
        public bool this[int bit] => ...;
    }
}
```

Extension indexers declarations generally follow the rules for 
non-extension [indexers in the Standard](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#159-indexers).

