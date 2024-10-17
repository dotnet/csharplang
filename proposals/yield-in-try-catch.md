# Permit yield in a try with a catch clause

## Summary

This proposal will allow `yield` statements to be written inside `try` and `catch` blocks.

## Motivation

The inability to use `yield` inside a `try` and `catch` block is a long standing pain point for customers. The restriction prevents the use of `yield` in a number of appealing scenarios. For example:

```csharp
IEnumerable<int> M1(IEnumerable<e> col)
{
    try
    {
        foreach (var item in col)
        {
            var otherItem = Translate(item);
            yield return otherItem;
        }
    }
    catch (Exception ex)
    {
        throw new MoreSpecificException(ex);
    }
}

IEnumerable<int> M2(IEnumerable<e> col)
{
    foreach (var item in col)
    {
        try
        {
            var otherItem = Translate(item);
            yield return otherItem;
        }
        catch (Exception ex)
        {
            Log(ex);
        }
    }
}
```

These restrictions exist in large part because it presented technical challenges for the native compiler and it wasn't a high enough priority item. The state machine support in Roslyn already supports the type of transforms necessary for this feature.

This proposal will allow for the common cases of `yield` within `try` and `catch` without the awkward workarounds that are necessary today to move the `yield` outside the `try`.

## Detailed Design

### yield in try and catch

The `yield` statement will be allowed inside `try` and `catch` blocks. The behavior will be the same as a `yield` statement today:

- `yield return` will cause the method to suspend and return the value to the caller via `Current`.
- `yield break` will cause the iterator to return `false` from `MoveNext`.

For example:

```csharp
foreach (var e in Iterator())
{
    Console.WriteLine(e);
}

IEnumerable<int> Iterator()
{
    try
    {
        yield return 1;
        throw new Exception("");
    }
    catch (Exception ex)
    {
        yield return 2;
    }
}
```

This code will output:

```cmd
1
2
```

The `catch` block will go through the same rewriting as an `await` inside of a `catch` block. That will be observable when the `throw;` statement is used to rethrow an exception as it will reset the stack trace vs. preserving it (just as it is for an `async` method).

The `yield` statement will not be allowed inside a `catch` when there is an associated or nested `finally` block. That would allow `yield` to be executed in the `Dispose` method which is not supported ([more details][catch-finally]).

Detailed notes:

- The `yield` statement will be allowed in a `try` block.
- The `yield` statement will be allowed in a `catch` block provided that:
  - The `try` block does not contain a `finally` block.
  - The `try` block does not have a nested `finally` block.

### Dispose and finally

A lesser known detail of iterators is that `finally` blocks can be executed as part of the `IDisposable.Dispose` implementation. The `Dispose` method has the same state machine implementation as the generated `MoveNext` except it only has the parts necessary for executing the `finally` blocks. That allows `Dispose` to _resume_ the method from the last suspend and execute the `finally` that were _active_ at the last suspend point.

For example consider this code sample:

```csharp
var e = Iterator().GetEnumerator();
e.MoveNext();
e.Dispose();

static T M<T>(T t) => t;

static IEnumerable<int> Iterator()
{
    try
    {
        try
        {
            yield return M(1);
            Console.WriteLine("After yield");
        }
        finally
        {
            Console.WriteLine("Finally Inner");
        }
    }
    finally
    {
        Console.WriteLine("Finally Outer");
    }
}
```

This program will output:

```cmd
Finally Inner
Finally Outer
```

The code generation for the `Dispose` method is meant to mirror the original `finally` structure as closely as possible. This includes execution of the code in the face of an exception during `Dispose`. This is achieved by refactoring the contents of the `finally` block into a method on the iterator and then having both `MoveNext` and `Dispose` generate the same `try / finally` structure and call into the methods.

For example this is the `Dispose` method for the above iterator:

```csharp
[DebuggerHidden]
void IDisposable.Dispose()
{
    int num = <>1__state;
    if ((uint)(num - -4) > 1u && num != 1)
    {
        return;
    }
    try
    {
        if (num != -4 && num != 1)
        {
            return;
        }
        try
        {
        }
        finally
        {
            <>m__Finally2();
        }
    }
    finally
    {
        <>m__Finally1();
    }
}
```

This behavior is important to understand when considering the code generation for `try / catch` blocks.

### Code generation yield inside try with catch

The code generation for iterators that have `yield` inside `try` blocks must preserve the same exception semantics for the `catch` in both `MoveNext` and `Dispose`. To achieve this the compiler will take a similar approach to what it does for `finally` blocks.

For every `catch` block where the `try` has a nested `try / finally` with `yield`:

1. The contents of the `catch` block will be generated into a parameterless method on the iterator with a `void` return.
2. The contents of the `when` clause will be generated into a parameterless method on the iterator with a `bool` return.
3. The `catch` block will be replaced with a call to the generated method in `MoveNext`.
4. The `when` clause will be replaced with a call to the generated method in `MoveNext`.
5. The `Dispose` method will mirror `catch` and `finally` blocks in the same way it mirrors `finally` blocks today and dispatch to the appropriate method

The exception object will be lifted into a field just as any other local would be and accesses to it in the generated `catch` and `when` methods will use the field. In the case the language allowed `when` clauses the pattern variables would be lifted into fields as well.

For example consider this code sample:

```csharp
static IEnumerable<int> Iterator()
{
    try
    {
        try
        {
            yield return M(1);
        }
        finally
        {
            Console.WriteLine("Finally Inner");
        }
    }
    catch (InvalidOperationException ex1) when (ex.Message.Contains("hello"))
    {
        Console.WriteLine("Catch1");
    }
    catch (Exception ex2) 
    {
        Console.WriteLine("Catch2");
    }
    finally
    {
        Console.WriteLine("Finally Outer");
    }
}
static T M<T>(T t) => t;
```

Will generate the following `Dispose` method:

```csharp
[DebuggerHidden]
void IDisposable.Dispose()
{
    int num = <>1__state;
    if ((uint)(num - -4) > 1u && num != 1)
    {
        return;
    }
    try
    {
        if (num != -4 && num != 1)
        {
            return;
        }
        try
        {
        }
        finally
        {
            <>m__Finally2();
        }
    }
    catch (InvalidOperationException ex) when (<>1__ex1 = ex, <>m__When1())
    {
        <>m__Catch1();
    }
    catch (Exception ex)
    {
        <>1__ex2 = ex;
        <>m__Catch1();
    }
    finally
    {
        <>m__Finally1();
    }
}
```

The `<>1__ex1 = ex` in the `when` clause is not legal but the IL generated for the `when` will conceptually have this behavior.

### Code generation yield inside try / catch in async iterators

The code generation for `try / catch` blocks in async iterators will be largely the same as traditional iterators. The difference is that the return type of generated `catch` methods will be `ValueTask` instead of `void`.

### Code generation yield inside catch

The restrictions on the feature mean that `yield` inside a `catch` cannot be observed from a `finally` block. That means the code generation does not need to consider the impact on `Dispose` and can focus soley on `MoveNext`.

Given that the code generation for `yield` inside `catch` will have the same structure as `await` inside of `catch`. Essentially the user written contents of the `catch` block will be moved outside the `catch`. The `catch` block will be replaced with saving the `Exception` object into the state machine and updating of the state variable to reflect execution is logically inside the catch block.

For example consider this code sample:

```csharp
IEnumerable<int> M()
{
    try
    {
        M();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Catch1");
        yield return 1;
        Console.WriteLine("Catch2");
    }
    Console.WriteLine("Done");
}
```

This would be generated as effectively:

```csharp
bool MoveNext()
{
    switch (<>1__state)
    {
    case 0:
        <>1__state = -1;
        try
        {
            M();
        }
        catch (Exception ex)
        {
            <>3__ex = ex;
            <>1__state = 1;
        }

        int num2 = <>1__state;
        if (num2 != 1)
        {
            goto case 2;
        }

        Console.WriteLine("Catch1");
        <>1__state = 1;
        <>2__current = 1;
        return true;
    case 1:
        <>1__state = -1;
        Console.WriteLine("Catch2");
        goto case 2;
    case 2:
        Console.WriteLine("Done");
        return true;
    default:
        return false;
    }
}
```

## Considerations

### yield inside catch with nested finally

[catch-finally]: #yield-inside-catch-with-nested-finally

The `yield` statement cannot be reasonbly supported inside a `catch` blocks with a nested `finally` due to the behavior of the `Dispose` method. It is possible that a `catch` block will run as part of executing a `finally` in the `Dispose` method.

For example consider the following:

```csharp
var e = InCatchFinally();
e.MoveNext();
e.Dispose();

IEnumerable<int> InCatchFinally()
{
    try
    {
        try
        {
            yield return 1;
        }
        finally
        {
            throw new Exception();
        }
    }
    catch 
    {
        yield return 2;
    }
}
```

This code would cause the statement `yield return 2` to be executed in the `Dispose` method. The state machine is not executing at this point hence it cannot be returned. Ignoring the statement would certainly be surprising the users.

For these reasons this proposal will not support `yield` inside a `catch` block that is observable from a `finally`.

### yield inside finally

The `yield` statement inside a `finally` creates the same type of code generation issues as [catch with finally][catch-finally]. For that reason it was excluded from this proposal.

## Open Issues

### try only

The proposal does allow `yield` inside of `catch` but it comes with a lot of caveats around `finally`. It's possible that this will lead to enoguh customer confusion that we should hold off on this until there is more demand for it.

## Related Issues

Related Items:

- https://github.com/dotnet/csharplang/discussions/765
