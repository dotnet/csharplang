# Proposal: Top-Level Methods in Namespaces

## Summary

This proposal introduces the ability to declare top-level methods inside any file in the program. These methods will then be available for invocation from within the program, or any referencing assembly, when the corresponding namespace is imported.

## Motivation

The language currently requires utility methods to be declared inside `static` classes, even when they don’t maintain state or invariants. This adds unnecessary ceremony to common patterns such as web applications, extension methods, and simple programs.

With the introduction of top-level statements in C# 9, C# began to reduce this ceremony by supportting methods outside of types. This proposal generalizes that direction, enabling *named* methods at the namespace scope and integrating them cleanly into the language and tooling ecosystem. This support will also enable us to satisfy long standing requests about top level extension methods and open the door to allowing extension members outside a container type.

## Scenarios

```csharp
// util.cs

void Print(string s) => Console.WriteLine(s);

string Capitalize(this string input) =>
    input.Length == 0 ? input : char.ToUpper(input[0]) + input[1..];

// app.cs
Print($"hello {args[0].Capitalize()}!");
```

## Detailed Design

### Declaration Form

Top-level methods will be allowed inside a namespace declaration. 


- Top-level methods may be declared directly in a namespace (file-scoped or block-scoped).
- The `static` modifier is **disallowed** (these methods are implicitly static).
- The default accessibility is `internal`. `public`, `internal`, and `private` are allowed.
- `protected` is disallowed.
- Attributes may be applied to the method.

```csharp
namespace MyUtils;

[Obsolete]
void Log(string s) => Console.WriteLine(s);

public int Add(int x, int y) => x + y;
```

### Extension Methods

Top-level methods support extension method declarations by using `this` on the first parameter. The same rules as current extension methods apply:

```csharp
namespace MyLib.Extensions;

public string Capitalize(this string s) =>
    string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
```

These become visible to consumers via `using` just as with static classes.

### Method Overloading

Overloading by signature is supported. Signature conflicts across files in the same namespace produce errors.

```csharp
namespace Demo;

void Print(string s) { }
void Print(int i) { } // ok
void Print(string s) { } // error
```

### Compilation Model

Top-level methods are lowered into **internal static synthetic types** that group methods by namespace and potentially by method arity or other grouping heuristics.

For example:

```csharp
namespace MyLib;

T Identity<T>(T value) => value;
```

May compile to:

```csharp
internal static class <TopLevel>.MyLib__Arity1
{
    internal static T Identity<T>(T value) => value;
}
```

All metadata for accessibility, attributes, generic constraints, etc., is preserved.

The generated types:

- Are not addressable from source.
- Are visible in metadata analysis tools like `System.Reflection.Metadata`.

### Name Resolution and Usage

- Methods are imported with `using <namespace>;`.
- They can also be called using fully qualified names: `MyLib.Identity(...)`.
- Extension methods follow the same resolution rules as existing static extension methods.

### Entry Point Behavior

When top-level methods are present, the model for determining the program’s entry point is revised:

- A top-level method named `Main` with a valid signature becomes the program’s entry point.
- If top-level statements are present and no `Main` method is defined, they are compiled into a callable method named `Main` in a generated type.
- If both a top-level method and a `Main` method declared inside a type are present, a compile-time error is produced.

```csharp
namespace MyApp;

int Main(string[] args) => 0; // valid entry point
```

```csharp
// error: conflicting entry points
namespace MyApp;
void SayHi() {}

class Program { static void Main() {} }
```

## Drawbacks

- May encourage polluting namespaces with loosely organized helpers.
- Requires tooling updates to properly surface and organize top-level methods in IntelliSense, refactorings, etc.
- Introduces new edge cases for entry point resolution.

## Alternatives Considered

### Making top-level methods outside of a namespace invocable across assemblies

The problem with invoking top-level methods outside of a namespace across assembly boundaries is there is no good method to bring them into scope. There is no namespace to place in a `using` directive. Bringing them into scope by default simply by referencing the assembly is equally problematic as it can easily lead to unresolvable name conflicts.

The one mechanism available to the language is `extern alias`. The language could allow such methods to be invoked when they are qualified with an `extern alias`. For example:

```csharp
// Util.dll
void Print(string s) => Console.WriteLine(s);

// App.dll which references Util.dll
extern alias Util;
Util::Print("hello world!");
```

This is not an ideal solution though as `extern alias` requires changes to the project file. This takes users out of their editting flow. It also cuts against our `dotnet run app.cs` effort where simple programs can be authored without any project file.

This could be addressed by extending the `extern alias` syntax to allow creating an alias, not just referencing an alias created in the project file. For example:

```csharp
// App.dll
extern alias Util = Util.dll
Util::Print("hello world!");
```

## Unresolved Questions

- Should top-level methods be allowed to declare `extern` or `partial`?
- Should we allow file-local top-level methods (e.g. using the `file` modifier)?

## Design Notes

- Multiple synthetic containers may be emitted per namespace to handle generic arity or platform-specific grouping.
- Tooling is expected to show top-level methods grouped under their namespace in IDEs.
- Reflection APIs will see these as static methods on internal types.

