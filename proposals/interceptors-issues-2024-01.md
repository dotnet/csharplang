# Open issues for interceptors

The following issues were identified during the interceptors preview, and should be addressed before the feature is transitioned to stable.

Further down there is a "Future" section containing issues which do not block the move to stable, but could be addressed in the longer term.

## File path portability

One of our design goals with interceptors was for them to conform to the "portability" requirement of source generators. That means we should be able to run the source generators for a project containing interceptors, commit the results to disk, then remove the source generators from the project and be able to build in a variety of environments. Note that this is different from *determinism*--we don't mind if the resulting assembly differs in things like layout, etc. in each build environment, but want it to be the case that if an interceptor declaration *functions* in one environment, then it also *functions* in a different environment, assuming all the sources and dependencies are fully available in each environment.

However, we currently require absolute paths for `InterceptsLocationAttribute`, with pathmap substitution performed if present. This doesn't work between local builds on different paths, or across local builds and CI, because only CI will actually pass `/pathmap` during the build. The local build prefers to preserve local paths (e.g. in the PDB) so that the local versions of the sources can be discovered easily during debugging. The different environments will have different answers for what they expect the paths to be, and the InterceptsLocation will have errors in environments which differ from the "generation-time" environment.

### Permit relative paths in `[InterceptsLocation(filePath, line, column)]`

Having looked at several strategies to address here, including adjusting when the build system passes `/pathmap` to the compiler, it seems the most straightforward way is to permit relative paths for interceptors. This is a strategy which is already proven out by `#line` directives.

When a relative path appears in an `[InterceptsLocation]`, we will resolve it relative to the path of the containing file, just as we would for `#line`.

```cs
// /project/src/file1.cs
class Original
{
    static void Interceptable() { }

    void M()
    {
        Interceptable();
    }
}

// /project/obj/generated/file2.g.cs
class Interceptors
{
    [InterceptsLocation("../../src/file1.cs", ...)] // resolves to "/project/src/file1.cs"
    public static void Interceptor() { }
}
```

We recommend that relative paths appearing in source use `/` directory separators for portability.

Note that the "portability" requirement of generators still cannot be met by interceptors when there is no relative path from the interceptor to the interceptee. For example, if the two source files are on different drives. This limitation already exists for `#line` directives, and we do not expect it to be problematic for real-world usage.

### Define generated `SyntaxTree.FilePath`

Generated files need to refer to user files using relative paths. The sample in the previous section shows this. However, generated files don't currently have file paths which can be related to user files. Instead, they use an invented path with the form `$"{assemblyName}/{generatorTypeName}/{hintName}.cs"`.

We propose changing this to define the `SyntaxTree.FilePath` for a generated file to be equivalent to the file path which the generated file *would be written to* if `$(EmitCompilerGeneratedFiles)` is true in the project. This will also have the effect of increasing the behavioral consistency of a project which is compiled with generators in-box, versus emitting the generated files, removing the generators, and recompiling.

In the compiler, when no argument is specified for `/generatedfilesout`, the implementation will use the containing directory of the `/out` argument (i.e. for the resulting DLL), as the base path for `SyntaxTree.FilePath` of generated files, but we will not write the generated files to disk. In most normal project configurations, this path can be made relative to the source files.

### Add `[InterceptsLocation(string locationSpecifier)]`

We propose adding the following well-known constructor:
```diff
 namespace System.Runtime.CompilerServices;
 class InterceptsLocationAttribute
 {
     public InterceptsLocationAttribute(string filePath, int line, int character) { }
+    public InterceptsLocationAttribute(string locationSpecifier) { }
 }
```

Example usage:
```cs
static file class Interceptors
{
    [InterceptsLocation("v1:../../src/MyFile.cs(12,34)")]
    public void Interceptor() { }
}
```

The location specifier encoding is intended to resemble the way diagnostic locations are written out on the command line: `v1:path(line, character)`.

We reserve the ability to evolve the exact encoding of the location specifier in future versions of the language and compiler. For example, in order to introduce an encoding which denotes the location of call(s) based on criteria other than a simple line and column number. At the same time, we expect to maintain "back compatibility" with consuming previous "versions" of the location specifier encoding. We expect to be able to indicate future versions of the location format by prefixing with `v2:`, `v3:`, and so on.

This constructor will be introduced simultaneously with the following public API, which generators will consume:

### Add public API for obtaining a location specifier for a call

We propose adding a public API for obtaining a "location specifier", which is intended to be easily passed through as an attribute argument in generated code.

```diff
 namespace Microsoft.CodeAnalysis;

 public readonly struct SourceProductionContext
 {
     public void AddSource(string hintName, string source);
+    public string GetInterceptsLocationSpecifier(InvocationExpressionSyntax intercepted, string interceptorFileHintName);
 }
```

```cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

[Generator(LanguageNames.CSharp)]
public class MyGenerator : IIncrementalGenerator
{
    private void AddInterceptorMethod(SourceProductionContext context, StringBuilder builder, InvocationExpressionSyntax invocation, string hintName)
    {
        builder.Add($$"""
            [InterceptsLocation({{context.GetInterceptsLocationSpecifier(invocation, hintName)}})]
            public static void Interceptor(this ReceiverType receiver, ParamType param) { ... }
            """);
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interceptableCalls = context.SyntaxProvider
            .FindInterceptableCalls() // impl intentionally elided
            .Collect();

        context.RegisterSourceOutput(interceptableMethods, (context, interceptableCalls) => {
            var hintName = "MyInterceptors.cs";
            var builder = new StringBuilder();
            // builder boilerplate..
            foreach (var interceptableCall in interceptableCalls)
            {
                // interceptableCall is of some type declared by the generator
                // which holds the call that the 
                InvocationExpressionSyntax invocationSyntax = interceptableCall.InvocationSyntax;
                AddInterceptorMethod(context, builder, invocationSyntax, hintName);
            }
            // builder boilerplate..

            context.AddSource(builder.ToString(), hintName);
        });
    }
}
```

This is intended to streamline the communication which is occurring between the generator and compiler:
1. Generator analyzes an interceptable call and decides it should insert an interceptor for it.
2. Generator calls `GetInterceptsLocationSpecifier`, to effectively ask the compiler for a "handle" to the interceptable call.
3. Generator inserts the location specifier into generated source text, and adds the generated source to the compilation using `AddSource`.
4. Post-generators compilation sees the location specifier in the generated source and understands which call to associate the interceptor with.

Where parts (1), (2), and (3) are occurring in the above sample, and (4) occurs in the compiler and analyzers which are working with a "post-generators" compilation.

The above API shape is specific to `InvocationExpressionSyntax`. The reason for this is that we are trying to make correct usage easy. If we took a more general syntax type such as `ExpressionSyntax` or `SyntaxNode`, then correct usage would become more ambiguous. Which node is the generator supposed to pass in? Should it be the `NameSyntax` whose location is used to denote the location of the intercepted call? If so, what is the proper way to dig through an `InvocationExpressionSyntax` to obtain that name syntax? What is the failure mode supposed to be if the syntax node is of a kind which doesn't support interceptors?

We expect that if support for intercepting more member kinds is permitted, then additional API for those member kinds should be introduced. The commitment we are making when we support intercepting a particular member kind is similar in significance to the commitment we make when introducing a public API. It doesn't serve generator authors to try and optimize for fewest number of public APIs in this case.

## Need for interceptor semantic info in IDE

Interceptors have a problematic interaction with the ILLinker analyzer, which is responsible for determining if code may be Native AOT-incompatible.

What happens is, the user calls an "original" method which is marked `[RequiresUnreferencedCode]` (i.e., incompatible with NativeAOT). A source generator adds an interceptor for the call, which *is* compatible with NativeAOT. But this is not reflected in the symbol info for the call in the Roslyn APIs.

We ended up working around this in .NET 8 by having the interceptor author also ship a *diagnostic suppressor* for the warnings produced by the ILLinker analyzer, when it knows the call will be intercepted.

```cs
routes.MapGet("/products/", () => { ... }); // ASP.NET suppressor is suppressing warning: MapGet is not NativeAOT-compatible
```

This is not ideal, because it requires interceptor authors to know, when a particular diagnostic is reported on a call, that the diagnostic "wouldn't have been reported" if the interceptor method were being used instead.

This is not scalable, and it is error prone. For example, if the interceptor were inserting a method which *also* `[RequiresUnreferencedCode]`, it would be bad to suppress the ILLinker analyzer warning.

Ideally if an analyzer needs to know if an interceptor is being used, it could simply make a public API call to ask. This adds an extra decision that component authors need to make. For some use cases, such as rename-refactoring, it doesn't make sense to inspect the interceptor method--instead the original method is what needs to be renamed. We think that in most cases, the original method is what should be analyzed, but it should still be noted as a pit that people may fall into.

That public API could look something like the following:

```diff
 namespace Microsoft.CodeAnalysis;

 public static class CSharpExtensions
 {
+    public static IMethodSymbol? GetInterceptorMethod(this SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken = default);
 }
```

Note that if we do something like this, there will be a deep need to discover interceptors, and incrementally update compilations containing interceptors, with as little binding work as possible. It will be non-tenable perf-wise if, for example, we had to bind all the attributes in a compilation in order to decide what the interceptor for a given invocation is. We may need to store some additional information in the declaration tree, which can be incrementally updated, in order to address this.

## Future (possibly post-C# 13)

### Intercepting properties
Currently the interceptors feature only supports intercepting ordinary methods. Much like with partial methods and the upcoming partial properties, users want to be able to intercept usages of more kinds of members, such as properties and constructors.

    - UnsafeAccessor does not support properties directly. Instead, a given usage of UnsafeAccessor decorates a method which calls through to a single property accessor.
    - Consider: What happens when we want to intercept `obj.Prop += a`? We can't choose a single method to replace usage of `Prop`.
    - Conclusion: We should probably favor intercepting a property with an extension property. We are likely blocked here until we ship extensions.

### Intercepting constructors and other member kinds

For all the member kinds we want to support, we will need to decide how usages of each member kind are denoted syntactically. Where possible, we should try to be consistent with [UnsafeAccessorAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafeaccessorattribute?view=net-8.0), which also works by referencing members in attribute arguments.

For method invocations like `receiver.M(args)`, we use the location of the `M` token to intercept the call. Perhaps for property accesses like `receiver.P` we can use location of the `P` token. For constructors should use the location of the `new` token.

For member usages such as user-defined implicit conversions which do not have any specific corresponding syntax, we expect that interception will never be possible.

### Interceptor signature variance
    - https://github.com/dotnet/aspnetcore/issues/47338
    - ASP.NET has indicated this can wait till later in cycle
    - Argument type is `Func<TCaptured>`. Interceptee parameter is `System.Delegate`. Interceptor parameter wants to be `Func<T>` and to have call site pass the `TCaptured` as a type argument.


### Generic unification

Currently interceptors have a limited support for generics which is outlined [here](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md#arity). However, users have raised [concerns](https://github.com/dotnet/roslyn/pull/68218#discussion_r1220428975) about the inability to intercept methods where the original signature includes type parameters which are "captured" from a containing scope.

```cs
class C<T>
{
    public void Original(T t);
}
```