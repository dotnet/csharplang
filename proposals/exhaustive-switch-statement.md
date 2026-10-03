# Exhaustive switch statement

## Summary

In C# we introduced the unions and closed classes features. A major part of the value of these features was the enrichment of switch expression exhaustiveness checks.

```cs
union Result<T>(T, Exception);

int M(Result<string> u)
{
    u switch
    {
        Exception => 1,
        // warning: switch is not exhaustive; pattern 'string' is not covered.
    }
}
```

However, switch statements often don't benefit from these exhaustiveness checks for a key reason: The implicit default label moves control flow to the end of the statement rather than throwing an exception like switch expressions do.

This means that a switch statement over a union type or closed class type often needs to handle the null case, even if nullable analysis determines the input is non-null.

```cs
void M1(MyResult<string> value)
{
    string successValue;
    switch (value)
    {
        case Exception:
            return;
        case string s:
            successValue = s;
            break;
    }

    successValue.ToString(); // error CS0165: Use of unassigned local variable 'successValue'
}

void M2(MyResult<string> value)
{
    string successValue;
    switch (value)
    {
        case Exception:
            return;
        case string s:
            successValue = s;
            break;
        case null:
            throw new ArgumentNullException(nameof(value));
    }

    successValue.ToString(); // ok
}
```

TODO2: gather past proposals. Review and look for gaps.