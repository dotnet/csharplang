# Top-Level Members

Champion issue: https://github.com/dotnet/csharplang/issues/9803

## Summary

Allow some members (methods, operators, extension blocks, fields, constants) to be declared in namespaces
and make them available when the corresponding namespace is imported
(this is a similar concept to instance extension members which are also usable without referencing the container class).

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
  - Allowed kinds currently are: methods, operators, extension blocks, fields, constants.
  - Existing declarations like classes still work the same, there shouldn't be any ambiguity.
  - There is no ambiguity with top-level statements because those are not allowed inside namespaces.

- Top-level members in a namespace are semantically members of an "implicit" class which:
  - is `static` and `partial`,
  - has accessibility either `internal` (by default) or `public` (if any member is also `public`),
  - has a generated unspeakable name `<>TopLevel`,
  - has the namespace in which it is declared in,
  - is synthesized per each namespace and compilation unit (so having top-level members in the same namespace across assemblies can lead to [ambiguities](#drawbacks)).

  For top-level members, this means:
    - The `static` modifier is disallowed (the members are implicitly static).
    - The default accessibility is `internal`.
      `public` and `private` is also allowed.
      `protected` and `file` is disallowed.
    - Overloading is supported.
    - `extern` and `partial` are supported.
    - XML doc comments work.

- Metadata:
  - The implicit class is recognized only if it has an attribute `[TopLevel]` (full attribute name is TBD).

- Usage (if there is an appropriately-shaped `[TopLevel]` type in namespace `NS`):
  - `using NS;` implies `using static NS.<>TopLevel;`.
  - Lookup for `NS.Member` can find `NS.<>TopLevel.Member` (useful for disambiguation).
  - Nothing really changes for extension member lookup (the class name is already not used for that).

- Entry points:
  - Top-level `Main` methods can be entry points.
  - Top-level statements are generated into `Program.Main` (speakable function; previously it was unspeakable).
    This is a breaking change: there could be a conflict with an existing `Program.Main` method declared by the user.
  - Simplify the logic: TLS entry-points are normal candidates (previously they were not considered to be candidates and for example `-main` could not be used to point to them).
    This is a breaking change: if the user has custom `Main` methods and top-level statements, they will get an error now because the compiler doesn't know which entrypoint to choose
    (to fix that, they can specify `-main`).

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
    - Or they could be in scope only in the current assembly.
- Allow declaring top-level statements inside namespaces as well.
  - Top-level local functions would introduce ambiguities with top-level methods. Wouldn't be a breaking change though, just need to decide which one wins.
- If we ever allow the `file` modifier on members (methods, fields, etc.), that would be naturally useful for top-level members, too.
  `file` members would be scoped to the current file.
  Compare that with `private` members which are scoped to the current _namespace_.

- Indentation concerns about current utility/extension methods could be resolved with
  [file-scoped types](https://github.com/dotnet/csharplang/discussions/928) instead, i.e., allowing something like `class MyNamespace.MyClass;`
  (although beware that `class MyClass;` has already a valid meaning today).
  That wouldn't solve the use-site though, where you'd still need `using static MyNamespace.MyClass;` instead of just `using MyNamespace;` as with this proposal.

- We could have something similar to VB's modules which are mostly like static classes
  but their members don't need to be qualified with the module name if they are brought to scope via an import:
  ```vb
  Imports N

  Namespace N
      Module M
          Sub F()
          End Sub
      End Module
  End Namespace

  Class C
      Sub Main()
          F()
      End Sub
  End Class
  ```

  F# has something similar, too:
  ```fs
  module Utilities =
    let M() = ()

  open Utilities
  M()
  ```

  For example, C# could have `implicit` classes like:
  ```cs
  public implicit static class Utilities
  {
      public static void M() { }
  }
  ```

  Combined with top-level classes feature mentioned above, this could look like:
  ```cs
  public implicit static class Utilities;
  public static void M() { }
  extension(int) { /* ... */ }
  ```

  This makes the declaration side a bit more complicated to write,
  but it avoids problems with naming the implicit static class.

  Open questions for this alternative:
  - Should we allow non-`static` members?

## Open questions

- Which member kinds? Methods, fields, constants, properties, indexers, events, constructors, operators.
- Accessibility: what should be the default and which modifiers should be allowed?
- Clustering: currently each namespace per assembly gets its `<>TopLevel` class.
- Shape of the synthesized static class (currently `[TopLevel] <>TopLevel`).
  - Should it be speakable at least in other languages (so it's usable from VB/F#)?
    - We could make that opt in via some attribute (`[file: TopLevel("MyTopLevelClassName")]`).
    - The naming could be based on the assembly name and/or the file name.
    - If the name was constant, that could lead to ambiguity errors
      when the same namespace is declared across multiple assemblies which are then referenced in one place.
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
- Should we [relax the order of mixing top-level statements and declarations](https://github.com/dotnet/csharplang/discussions/5780) as part of this feature?
- Do we need new name conflict rules for declarations and/or usages?
  For example, should the following be an error when declared (and/or when used)?
  ```cs
  namespace NS;
  int Foo;
  class Foo { }
  ```
