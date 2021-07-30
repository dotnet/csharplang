# Top-level statements

## Summary
[summary]: #summary

Allow a sequence of *statements* to occur right before the *namespace_member_declaration*s of a *compilation_unit* (i.e. source file).

The semantics are that if such a sequence of *statements* is present, the following type declaration, modulo the actual type name and the method name, would be emitted:

``` c#
static class Program
{
    static async Task Main(string[] args)
    {
        // statements
    }
}
```

See also https://github.com/dotnet/csharplang/issues/3117.

## Motivation
[motivation]: #motivation

There's a certain amount of boilerplate surrounding even the simplest of programs,
because of the need for an explicit `Main` method. This seems to get in the way of
language learning and program clarity. The primary goal of the feature therefore is
to allow C# programs without unnecessary boilerplate around them, for the sake of
learners and the clarity of code.

## Detailed design
[design]: #detailed-design

### Syntax

The only additional syntax is allowing a sequence of *statement*s in a compilation unit,
just before the *namespace_member_declaration*s:

``` antlr
compilation_unit
    : extern_alias_directive* using_directive* global_attributes? statement* namespace_member_declaration*
    ;
```

Only one *compilation_unit* is allowed to have *statement*s. 

Example:

``` c#
if (args.Length == 0
    || !int.TryParse(args[0], out int n)
    || n < 0) return;
Console.WriteLine(Fib(n).curr);

(int curr, int prev) Fib(int i)
{
    if (i == 0) return (1, 0);
    var (curr, prev) = Fib(i - 1);
    return (curr + prev, curr);
}
```

### Semantics

If any top-level statements are present in any compilation unit of the program, the meaning is as if
they were combined in the block body of a `Main` method of a `Program` class in the global namespace,
as follows:

``` c#
static class Program
{
    static async Task Main(string[] args)
    {
        // statements
    }
}
```

Note that the names "Program" and "Main" are used only for illustrations purposes, actual names used by
compiler are implementation dependent and neither the type, nor the method can be referenced by name from
source code.

The method is designated as the entry point of the program. Explicitly declared methods that by convention 
could be considered as an entry point candidates are ignored. A warning is reported when that happens. It is
an error to specify `-main:<type>` compiler switch when there are top-level statements.

The entry point method always has one formal parameter, ```string[] args```. The execution environment creates and passes a ```string[]``` argument containing the command-line arguments that were specified when the application was started. The ```string[]``` argument is never null, but it may have a length of zero if no command-line arguments were specified. The ‘args’ parameter is in scope  within top-level statements and is not in scope outside of them. Regular name conflict/shadowing rules apply.

Async operations are allowed in top-level statements to the degree they are allowed in statements within
a regular async entry point method. However, they are not required, if `await` expressions and other async
operations are omitted, no warning is produced.

The signature of the generated entry point method is determined based on operations used by the top level
statements as follows:

| **Async-operations\Return-with-expression** | **Present** | **Absent** |
|----------------------------------------|-------------|-------------|
| **Present** | ```static Task<int> Main(string[] args)```| ```static Task Main(string[] args)``` |
| **Absent**  | ```static int Main(string[] args)``` | ```static void Main(string[] args)``` |

The example above would yield the following `$Main` method declaration:

``` c#
static class $Program
{
    static void $Main(string[] args)
    {
        if (args.Length == 0
            || !int.TryParse(args[0], out int n)
            || n < 0) return;
        Console.WriteLine(Fib(n).curr);
        
        (int curr, int prev) Fib(int i)
        {
            if (i == 0) return (1, 0);
            var (curr, prev) = Fib(i - 1);
            return (curr + prev, curr);
        }
    }
}
```

At the same time an example like this:
``` c#
await System.Threading.Tasks.Task.Delay(1000);
System.Console.WriteLine("Hi!");
```

would  yield:
``` c#
static class $Program
{
    static async Task $Main(string[] args)
    {
        await System.Threading.Tasks.Task.Delay(1000);
        System.Console.WriteLine("Hi!");
    }
}
```

An example like this:
``` c#
await System.Threading.Tasks.Task.Delay(1000);
System.Console.WriteLine("Hi!");
return 0;
```

would  yield:
``` c#
static class $Program
{
    static async Task<int> $Main(string[] args)
    {
        await System.Threading.Tasks.Task.Delay(1000);
        System.Console.WriteLine("Hi!");
        return 0;
    }
}
```

And an example like this:
``` c#
System.Console.WriteLine("Hi!");
return 2;
```

would  yield:
``` c#
static class $Program
{
    static int $Main(string[] args)
    {
        System.Console.WriteLine("Hi!");
        return 2;
    }
}
```

### Scope of top-level local variables and local functions

Even though top-level local variables and functions are "wrapped" 
into the generated entry point method, they should still be in scope throughout the program in
every compilation unit.
For the purpose of simple-name evaluation, once the global namespace is reached:
- First, an attempt is made to evaluate the name within the generated entry point method and 
  only if this attempt fails 
- The "regular" evaluation within the global namespace declaration is performed. 

This could lead to name shadowing of namespaces and types declared within the global namespace
as well as to shadowing of imported names.

If the simple name evaluation occurs outside of the top-level statements and the evaluation
yields a top-level local variable or function, that should lead to an error.

In this way we protect our future ability to better address "Top-level functions" (scenario 2 
in https://github.com/dotnet/csharplang/issues/3117), and are able to give useful diagnostics 
to users who mistakenly believe them to be supported.

