# Top-Level Members

Champion issue: (TODO)

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

static string? cache;

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

TODO: Why are we doing this? What use cases does it support? What is the expected outcome?

- Avoid boilerplate utility static classes.
- Evolve top-level statements from C# 9.

## Detailed design

TODO: This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement,  and include examples of how the feature is used. This section can start out light before the prototyping phase but should get into specifics and corner-cases as the feature is iteratively designed and implemented.

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
- Usage:
  - `using NS;` implies `using static NS.TopLevel;`.
  - Lookup for `NS.Method()` can find `NS.TopLevel.Method()`.
  - Nothing really changes for extensions.
- Entry points:
  - Top-level `Main` methods can be entry points.
  - Top-level statements are generated into `Program.Main` (speakable function).
    This is a breaking change (previously the main method was unspeakable).
  - Simplify the logic: TLS entry-points are normal candidates.
    This is a breaking change (previously they were not considered to be candidates and for example `-main` could not be used to point to them).

## Drawbacks

TODO: Why should we *not* do this?

- Polluting namespaces with loosely organized helpers.
- Requires tooling updates to properly surface and organize top-level methods in IntelliSense, refactorings, etc.
- Entry point resolution breaking changes.

## Alternatives

TODO: What other designs have been considered? What is the impact of not doing this?

- Support `args` keyword in top-level members (just like it can be accessed in top-level statements). But we have `System.Environment.GetCommandLineArgs()`.
- Allow capturing variables from top-level statements inside non-`static` top-level members.
  Could be used to refactor a single-file program into multi-file program just by extracting functions to separate files.
  But it would mean that a method's implementation (top-level statements) can influence what other methods see (which variables are available in top-level members).
- Allow declaring top-level members outside namespaces as well.
  - Would introduce ambiguities with top-level statements.
  - Could be brought to scope via `extern alias`.
    - To avoid needing to specify those in project files (e.g., so file-based apps also work),
      there could be a syntax for that like `extern alias Util = Util.dll`.

## Open questions

TODO: What parts of the design are still undecided?

- Which member kinds? Methods, fields, properties, indexers, events, constructors, operators.
- Allow `file` or `private` or both? What should `private` really mean? Visible to file, namespace, or something else?
- Name for the speakable static class (currently `TopLevel`)? Should it be speakable at all?
- Should we simplify the TLS entry point logic? Should it be a breaking change?
