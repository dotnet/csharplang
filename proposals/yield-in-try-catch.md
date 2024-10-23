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

These restrictions exist in large part because it presented technical challenges for the native compiler and it wasn't a high enough priority item. That is no longer a blocker as the state machine in Roslyn supports the types of transforms necessary to support this feature. There are still several semantic challenges to work through and this proposal has been written to address those.

This proposal will allow for the common cases of `yield` within `try` and `catch` without the awkward workarounds that are necessary today to move the `yield` outside the `try`.

## Detailed Design

### yield inside try and catch

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

The `yield` inside the `catch` block will go through the same rewriting as an `await` inside of a `catch` block. That will be observable when the `throw;` statement is used to rethrow an exception as it will reset the stack trace vs. preserving it (just as it is for an `async` method).

Detailed notes:

- The `yield` statement will be allowed in a `try / catch` block
- The `yield` statement will be allowed in a `catch` block

### Dispose and finally in iterators

A lesser known detail of iterators is that `finally` blocks can be executed as part of the `IDisposable.Dispose` implementation. The `Dispose` method has the same state machine structure as the generated `MoveNext` except it only has the parts necessary for executing the `finally` blocks. That allows `Dispose` to _resume_ the method from the last suspend and execute the `finally` that were _active_ at the last suspend point.

The `Dispose` method _only_ executes the `finally` blocks and ignores all code inbetween them. For example consider this code sample:

```csharp
var e = M1().GetEnumerator();
e.MoveNext();
e.Dispose();

IEnumerable<int> M1()
{
    try
    {
        try
        {
            yield return 13;
        }
        finally
        {
            Console.WriteLine("inner finally");
        }

        Console.WriteLine("before yield return 1");
        yield return 1;
        Console.WriteLine("after yield return 1");
    }
    finally
    {
        Console.WriteLine("outer finally");
    }
}

```

This program will output:

```cmd
inner finally
outer finally
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

### Code generation of yield inside try with catch in iterators

The code generation for iterators that have `yield` inside a `try / catch` will preserve the `catch` semantics in the `Dispose` method. That is _only_ the contents of the `finally` blocks will execute even in the case exceptions come into play. For example:

```csharp
var e = M1(true).GetEnumerator();
e.MoveNext();
e.Dispose();

IEnumerable<int> M1(bool b)
{
    try
    {
        try
        {
            try
            {
                yield return 13;
            }
            finally
            {
                Console.WriteLine("inner finally");
                if (b)
                {
                    throw new Exception();
                }
            }
        }
        catch
        {
            Console.WriteLine("catch");
        }

        Console.WriteLine("after catch");
    }
    finally
    {
        Console.WriteLine("outer finally");
    }
}
```

This program will output:

```cmd
inner finally
outer finally
Unhandled exception. System.Exception: Exception of type 'System.Exception' was thrown
```

The `"after catch"` is not printed beacuse only the `finally` structure is mirrored in the `Dispose` method. The `catch`, like all other statements between `finally` is not included. Statements in between the `catch` and `finally` are not executed in `Dispose`. This may seem odd at first glance but is leaning into the specified behavior for iterator `Dispose`.

### Dispose and finally in async iterators

The `DiposeAsync` behavior for async iterators mirrors that of traditional iterators in that it executes the `finally` blocks at the the point the state machine is suspended. However it does this by setting the `state` variable to a value that represents disposing and then calls `MoveNextAsync`. The implementation of `MoveNextAsync` will then execute only the `finally` blocks for the suspend point. The `DisposeAsync` method does not have a mirror copy of the `finally` blocks.

### Code generation of yield inside try / catch in async iterators

The code generation of async iterators will change such that `catch` blocks do not observably execute during `DisposeAsync`. To achieve this all `catch` blocks visible from a `yield` will rethrow exceptions if the state machine is in a disposing state. For example consider the following code:

```csharp
var e = M(true).GetEnumerator();
await e.MoveNext();
await e.DisposeAsync();

async IAsyncEnumerable<int> M(bool b)
{
    try
    {
        try
        {
            yield return 13;
            await Task.Yield();
        }
        finally
        {
            Console.WriteLine("inner finally");
            if (b)
            {
                throw new Exception();
            }
        }
    }
    catch
    {
        Console.WriteLine("catch");
    }
    finally
    {
        Console.WriteLine("outer finally");
    }
}
```

This will output:

```cmd
inner finally
outer finally
```

To achieve this the `catch` will be effectively rewritten as follows:

```csharp
catch (Exception ex)
{
    if (<>1__state == /* disposing state */)
    {
        throw;
    }
    Console.WriteLine("catch");
}
```

The generator will also need to modify any `when` clauses on `catch` blocks to ensure they don't execute during `DisposeAsync`. To do this a prefix will be generated that returns `false` when the state machine is in a disposing state. For example:

```csharp
// User code
catch (Exception ex) when (SomeMethod(ex))

// Generated code
catch (Exception ex) when (<>1__state == /* disposing state */ ? false : SomeMethod(ex))
```

### Code generation of yield inside catch

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

### yield inside finally

Consideration was given to supporting `yield` inside a `finally` block but this creates significant semantic challenges. Consider the following code as an example:

```csharp
var e = M().GetEnumerator();
e.MoveNext();
e.Dispose();

void M()
{
    try
    {
        yield return 42;
    }
    finally
    {
        yield return 13;
    }
}
```

The `finally` block would be executed in `Dispose` which means the language has to decide on the behavior of `yield` during `Dispose`. That could be modeled as:

1. Throw an exception when `yield` is encountered in `Dispose`.
2. Silently ignore the value and continue executing the `finally` block without suspend.

Neither of these seem like desirable outcomes and as such `yield` will not be allowed in `finally` blocks.

## Open Issues

### Preserve catch in Dispose paths

The `Dispose` method could include mirroring both `catch` and `finally` blocks. This would allow the `catch` block to be executed in `Dispose` when `finally` blocks threw an exception. The approach for this would be to do the following.

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

This would add a bit of complexity to the feature and it only produces observable differences when a `finally` block throws an exception on the `Dispose` path. That is likely a rare case. Further it potentially increases the complexity for developers. The `Dispose` method at first glance is likely unintuitive in that it only mirrors `finally` blocks and no other statements but that is also a very simple rule to learn. Preserving `catch` and `finally` and discussing how the code flows between them is potentially more complex.

This also brings into question what happens when a `yield` occurs in a `catch` during `Dispose`. That cannot execute correctly as the state machine can't suspend during `Dispose`. To account for this the feature likely needs further restrictions like:

- The `yield` statement will be allowed in a `catch` block provided that:
  - The `try` block does not contain a `finally` block.
  - The `try` block does not have a nested `finally` block.

On the whole it seems simpler to not support `catch` in `Dispose` paths.

## Related Issues

Related Items:

- https://github.com/dotnet/csharplang/discussions/765
