# Iterators in lambdas

Champion issue: https://github.com/dotnet/csharplang/issues/9467

## Summary

This proposal will remove the restriction that lambda expressions cannot be iterators.

## Motivation

Over the years the idea of allowing lambdas to be iterators has come up several times. The proposal has generally [garnered lack luster support][iterator-meeting]. The conclusion is that it's not harmful to the language but also not a high priority.

However, the [use of async streams][iterator-discussion-async-streams] in minimal API programs have made this proposal signifantly more relevant. `IAsyncEnumerator<T>` is now used heavily in streaming APIs, including AI related services. This means that the ability to create such APIs cannot be done with the existing style of top level statements. Developers must refactor their code out to a local method once they want to use async streaming.

```cs

// Desired code
app.MapGet("/search", async IAsyncEnumerable<Product>(
    string query, VectorStoreCollection<int, Product> collection) =>
{
    await foreach (var result in collection.SearchAsync(query, top: 5, new() { Filter = r => r.TenantId == 8 }))
    {
        yield return result.Record;
    }
}

// Actual code
app.MapGet("/search", MapForSearch);

async IAsyncEnumerable<Product> MapForSearch(string query, VectorStoreCollection<int, Product> collection)
{
    await foreach (var result in collection.SearchAsync(query, top: 5, new() { Filter = r => r.TenantId == 8 }))
    {
        yield return result.Record;
    }
}

```

This motivation is simalar to the motivation for [lambdas to have optional parameters][lambda-optional-parameters]. The inability to have optional parameters in lambdas forced minimal API programs to unnecessarily fall back to local methods.

## Detailed Design

This will allow lambdas that have a return type that is a recognized iterator type and contains a `yield` statement to be considered an iterator. Such lambdas will have all of the functionality and restrictions of method based iterators:

- The `yield type` will be determined from the iterator type.
- The iterator cannot have any `in / ref / out` parameters.
- The iterator can be `async` or synchronous.
- etc ...

The return type can be explicit or inferred. For example both of the following are valid iterator lambdas:

```cs
var lambda1 = () =>
{
    yield return 1;
    yield return 2;
};

Func<IEnumerable<int>> lambda2 = () =>
{
    yield return 1;
    yield return 2;
};
```

## Miscelaneous

## Open Issues

### Iterator and Yield Type Inference

This proposal needs to dig into our inference rules and how it interacts with existing passes like return type inference.

[iterator-meeting]: meetings\2018\LDM-2018-05-21.md
[iterator-discussion-async-streams]: https://github.com/dotnet/csharplang/discussions/9393
[lambda-optional-parameters]: https://github.com/dotnet/csharplang/issues/6051
