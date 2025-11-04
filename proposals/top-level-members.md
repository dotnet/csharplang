# Top-Level Members

Champion issue: https://github.com/dotnet/csharplang/issues/9803

## Summary

Allow some members (methods, operators, extension blocks, and fields) to be declared in namespaces
and make them available when the corresponding namespace is imported.

```cs
// util.cs
namespace MyApp;

void Print(string s) => Console.WriteLine(s);

string Capitalize(this string input) =>
    input.Length == 0 ? input : char.ToUpper(input[0]) + input[1..];
```

```cs
// app.cs
#!/usr/bin/env dotnet

using MyApp;

Print($"Hello, {args[0].Capitalize()}!");
```

```cs
// Fields are useful:
namespace MyUtils;

string? cache;

string GetValue() => cache ??= Compute();
```

```cs
// Simplifies extensions:
namespace System.Linq;

extension<T>(IEnumerable<T> e)
{
    public IEnumerable<T> AsEnumerable() => e;
}
```

## Motivation

- Avoid boilerplate utility static classes.
- Evolve top-level statements from C# 9.

## Detailed design

- Some members can be declared directly in a namespace (file-scoped or block-scoped).
  - Allowed kinds currently are: methods, operators, extension blocks, and fields.
  - Existing declarations like classes still work the same, there shouldn't be any ambiguity.
  - There is no ambiguity with top-level statements because those are not allowed inside namespaces.
- It is as if the members were in an "implicit" `static` class
  whose accessibility is either `internal` (by default) or `public` (if any member is also `public`).
  For top-level members, this means:
    - The `static` modifier is disallowed (the members are implicitly static).
    - The default accessibility is `internal`.
      `public` and `private` is also allowed.
      `protected` and `file` is disallowed.
    - Overloading is supported.
    - `extern` and `partial` are supported.
    - XML doc comments work.
- Metadata:
  - A type synthesized per namespace and file. That means `private` members are only visible in the file.
  - Cannot be addressed from C#, but has speakable name `TopLevel` so it is callable from other languages.
    This means that custom types named `TopLevel` become disallowed in a namespace where top-level members are used.
  - It needs to have an attribute `[TopLevel]` otherwise it is considered a plain old type. This prevents a breaking change.
- Usage (if there is an appropriately-shaped `NS.TopLevel` type):
  - `using NS;` implies `using static NS.TopLevel;`.
  - Lookup for `NS.Member` can find `NS.TopLevel.Member`.
  - Nothing really changes for extensions.
- Entry points:
  - Top-level `Main` methods can be entry points.
  - Top-level statements are generated into `Program.Main` (speakable function).
    This is a breaking change (previously the main method was unspeakable).
  - Simplify the logic: TLS entry-points are normal candidates.
    This is a breaking change (previously they were not considered to be candidates and for example `-main` could not be used to point to them).

## Drawbacks

- Polluting namespaces with loosely organized helpers.
- Requires tooling updates to properly surface and organize top-level methods in IntelliSense, refactorings, etc.
- Entry point resolution breaking changes.

## Alternatives

- Support `args` keyword in top-level members (just like it can be accessed in top-level statements). But we have `System.Environment.GetCommandLineArgs()`.
- Allow capturing variables from top-level statements inside non-`static` top-level members.
  Could be used to refactor a single-file program into multi-file program just by extracting functions to separate files.
  But it would mean that a method's implementation (top-level statements) can influence what other methods see (which variables are available in top-level members).
- Allow declaring top-level members outside namespaces as well.
  - Would introduce ambiguities with top-level statements.
  - Could be brought to scope via `extern alias`.
    - To avoid needing to specify those in project files (e.g., so file-based apps also work),
      there could be a syntax for that like `extern alias Util = Util.dll`.
- Allow declaring top-level statements inside namespaces as well.
  - Top-level local functions would introduce ambiguities with top-level methods. Wouldn't be a breaking change though, just need to decide which one wins.

## Open questions

- Which member kinds? Methods, fields, properties, indexers, events, constructors, operators.
- Allow `file` or `private` or both? What should `private` really mean? Visible to file, namespace, or something else?
- Shape of the synthesized static class (currently `[TopLevel] TopLevel`)? Should it be speakable?
- Should we simplify the TLS entry point logic? Should it be a breaking change?
- Should we require the `static` modifier (and keep our doors open if we want to introduce some non-`static` top-level members in the future)?
- Should we disallow mixing top-level members and existing declarations in one file?
  - Or we could limit their relative ordering, like top-level statements vs. other declarations are limited today.
  - Allowing such mixing might be surprising, for example:
    ```cs
    namespace N;
    int s_field;
    int M() => s_field; // ok
    static class C
    {
      static int M() => s_field; // error, `s_field` is not visible here
    }
    ```
  - Disallowing such mixing might be surprising too, for example, consider there is an existing code:
    ```cs
    namespace N;
    class C;
    ```
    and I just want to add a new declaration to it which fails and forces me to create a new file or namespace block:
    ```cs
    namespace N;
    extension(object) {} // error
    class C;
    ```
- Do we need new name conflict rules for declarations and/or usages?
  For example, should the following be an error when declared (and/or when used)?
  ```cs
  namespace NS;
  int Foo;
  class Foo { }
  ```
