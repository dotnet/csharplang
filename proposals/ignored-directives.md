# Ignored directives

Champion issue: <https://github.com/dotnet/csharplang/issues/3507>

## Summary

Add `#` directives to be used by tooling, but ignored by the language.

```cs
#!/usr/bin/dotnet run
#sdk Microsoft.NET.Sdk.Web
#package Microsoft.AspNetCore.OpenApi 9.0.2

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Builder();

app.MapGet("/", () => new { Message = "Hello, World!" })
    .WithName("HelloWorld");

app.Run();
```

## Motivation

We are adding `dotnet run file.cs` support [in .NET SDK][dotnet-run-file].
Real-world file-based programs need to reference NuGet packages.
It would be also useful if it were possible to execute file-based programs directly like `./file.cs` when they have [the shebang directive][shebang] (`#!`).

The language should ignore these directives, but compiler implementations and other tooling can recognize them.
We already have similar directives in the language:
- `#region`
- `#pragma`: details are implementation specific
- `#error version`: the `version` part is not in the spec, but Roslyn will report its version

## Detailed design

Introduce new *ignored pre-processing directives* ([§6.5][directives]):

```antlr
PP_Kind
    : ... // Existing directive kinds
    | PP_Ignored
    ;

PP_Ignored
    : PP_IgnoredToken Input_Character*
    ;

PP_IgnoredToken
    : '!'
    | 'package'
    | 'sdk'
    ;
```

### Restrictions

Ignored directives must occur before the first token ([§6.4][tokens]) in the compilation unit, just like `#define`/`#undef` directives.
This improves readability (all package references and other configuration is in one place), tooling performance (no need to scan long files in full).
Ignored directives must also occur before any `#if` directives because the tooling might not know the full set of conditional compilation symbols while parsing ignored directives.

Furthermore, the compiler should report a warning if the `#!` directive is not placed at the first line and the first character in the file
(not even a BOM marker can be in front of it), because otherwise shells won't recognize it.

Compilers are also free to report errors if these directives are used in unsupported scenarios,
e.g., Roslyn will report an error if these directives are present in a file compiled as part of "project-based programs" as opposed to "file-based programs"
(and tooling will remove these directives when migrating file-based programs to project-based programs).
That error should not be reported for the `#!` directive, it can be placed on any file because it might invoke some other tool than `dotnet run`.

<!--
## Drawbacks
-->

## Alternatives

### Other syntax forms

Other syntax forms could be used except for shebang which shells recognize only by `#!`.

Reusing an existing syntax form or introducing just a single new ignored prefix
would allow tooling to introduce new directives without needing to change the language spec.
However, we do not anticipate needing to add many directives.
- For `dotnet run file.cs` specifically, we aim to add only as few directives as possible,
  and for anything more advanced, project-based programs should be used instead
  (we don't want to have two ways to configure everything).
- In any case, it seems good if new directives are discussed and approved by the language design team
  since they are part of the overall C# language experience.

#### Pragma

We could reuse `#pragma` directive although that's originally meant for compiler options, not SDK (project-wide) options.

```cs
#pragma package Microsoft.CodeAnalysis 4.14.0
```

#### Single directive

We could introduce only a single new directive for everything (packages, sdks, and possibly more in the future).
For example, `#r` is already supported by C# scripting and other existing tooling.
However, the naming of the directive is less clear.

```cs
#r "nuget: Microsoft.CodeAnalysis, 4.14.0"
```

#### Sigil

We could reserve one sigil prefix (e.g., `#!`/`#@`/`#$`/`#:`) for any directives that should be ignored by the language.
Note that `#!` would be interpreted as shebang by shells if it is at the first line of the file.

```cs
#!/usr/bin/dotnet run
##package Microsoft.CodeAnalysis 4.14.0
##anything // compiler or SDK might still error on unrecognized directives to "reserve" them for future use by the official .NET tooling
```

With that we could also allow file-based programs to specify any project metadata:

```cs
#!/usr/bin/dotnet run
#:sdk      Microsoft.NET.Sdk.Web
#:property TargetFramework/net11.0
#:property LangVersion/preview
#:package  System.CommandLine/2.0.0-*

Console.WriteLine("Hello, World!");
```

#### Comments

We could use comments instead of introducing new directives.
Reusing normal comments with `//#` might be confused with directives that have been commented out unless we use some other syntax like `//!` but both of these could be breaking.
Documentation XML comments are more verbose and we would need to ensure they do not apply to the class below them when placed at the top of the file.

```cs
//#package Microsoft.CodeAnalysis 4.14.0
```

```cs
/// <package name="Microsoft.CodeAnalysis" version="4.14.0" />
```

## Open questions

### More directives

We can consider also `#tfm`.

<!--
## Links
-->

[dotnet-run-file]: https://github.com/dotnet/sdk/pull/46915
[shebang]: https://en.wikipedia.org/wiki/Shebang_%28Unix%29
[tokens]: https://github.com/dotnet/csharpstandard/blob/f885375267570784d8d529d94893555494781abb/standard/lexical-structure.md#64-tokens
[directives]: https://github.com/dotnet/csharpstandard/blob/f885375267570784d8d529d94893555494781abb/standard/lexical-structure.md#65-pre-processing-directives
