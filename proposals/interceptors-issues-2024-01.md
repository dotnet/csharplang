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

We propose changing this to define the `SyntaxTree.FilePath` for a generated file to be *either* within the directory indicated by the `/generatedfilesout` compiler argument, or, if no value is specified for `/generatedfilesout`, to be within the containing directory of the `/out` argument instead (i.e. the containing directory of the output assembly). In most normal project configurations, either of these paths can be made relative to the user source files.

In the `/out` case, the generated files are not written to disk, so the generated file path is only used to inform the language semantics. At time of writing, that includes the behavior of interceptors and `[CallerFilePath]`.

We are only proposing to change the prefix of the generated file paths. We expect that the existing format `$"{assemblyName}/{generatorTypeName}/{hintName}.cs"` will remain as the suffix of generated file paths.

TODO: samples of what paths are used for generated files with a few different combinations of `/out` and `/generatedfilesout`, including when the generated files are read back in from disk.

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

The location specifier encoding is intended to resemble the way diagnostic locations are written out on the command line, e.g. `ConsoleApp1\Program.cs(2,34): error CS1003: Syntax error, ',' expected`. A side benefit of this encoding is that when a suffix of the location specifier is pasted into IDE "Go To" commands, the editor can often go to the exact file, line and column of the indicated call.

We prefix the location specifier with a "version tag", starting with `v1:`. We reserve the ability to evolve the exact encoding of the location specifier in future versions of the language and compiler. For example, in order to introduce an encoding which denotes the location of intercepted call(s) based on criteria other than a simple line and column number. We anticipate that any future version of the encoding will be prefixed with `v2:`, `v3:`, and so on.

When new versions of the location specifier are introduced, we will still maintain "back compatibility" with consuming previous versions of the location specifier.

The constructor `InterceptsLocationAttribute(string locationSpecifier)` will be introduced simultaneously with the following public API, which generators will consume:

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

Usage:
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
                // which holds the call which should be intercepted
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

The above API shape is specific to `InvocationExpressionSyntax`. The reason for this is that we are trying to make correct usage easy. If we took a more general syntax type such as `ExpressionSyntax` or `SyntaxNode`, then correct usage would become more ambiguous. Which node is the generator supposed to pass in? Should it be the `NameSyntax` whose location is used to denote the location of the intercepted call? If so, what is the proper way to dig through an `InvocationExpressionSyntax` to obtain that name syntax? What is the failure mode supposed to be if the syntax passed as an argument doesn't support interceptors?

We expect that if support for intercepting more member kinds is permitted, then additional API for those member kinds should be introduced. The commitment we are making when we support intercepting a particular member kind is similar in significance to the commitment we make when introducing a public API. It doesn't serve generator authors to try and optimize for fewest number of public APIs in this case.

## Need for interceptor semantic info in IDE

Interceptors have a problematic interaction with the ILLinker analyzer, which is responsible for determining if code may be Native AOT-incompatible.

What happens is, the user calls an "original" method which is marked `[RequiresUnreferencedCode]` (i.e., incompatible with NativeAOT). A source generator adds an interceptor for the call, which *is* compatible with NativeAOT. But this is not reflected in the symbol info for the call in the Roslyn APIs.

We ended up working around this in .NET 8 by having the interceptor author also ship a *diagnostic suppressor* for the warnings produced by the ILLinker analyzer, when it knows the call will be intercepted.

```cs
routes.MapGet("/products/", () => { ... }); // ASP.NET suppressor is suppressing warning: MapGet is not NativeAOT-compatible

static class Extensions
{
    // Original method signature looks like:
    [RequiresUnreferencedCode]
    public static IRouteEndpointBuilder MapGet(this IRouteEndpointBuilder builder, string route, Delegate handler) => ...;

    // Interceptor signature looks like (i.e. lacks '[RequiresUnreferencedCode]'):
    [InterceptsLocation(...)]
    public static IRouteEndpointBuilder Interceptor(this IRouteEndpointBuilder builder, string route, Delegate handler) => ...;
}
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

### `<InterceptorsNamespaces>` property

We introduced an MSBuild property `<InterceptorsPreviewNamespaces>` as part of the experimental release of the interceptors feature. A compilation error occurs if an interceptor is declared outside of one of the namespaces listed in this property. We suggest keeping this property under the name `<InterceptorsNamespaces>`, eventually phasing out the original name. This will help us get a better level of performance with the `GetInterceptorMethod` public API, by reducing the set of declarations which need to be searched for possible `[InterceptsLocation]` attributes.

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

### Interceptor signature variance and advanced generics cases

In the experimental release of interceptors, we've used a fairly stringent [signature matching](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md#signature-matching) requirement which does not permit variance of the parameter or return types between the interceptor and original methods.

We also have limited support for [generics](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md#arity). Essentially, an interceptor needs to either have the same arity as the original method, or it needs to not be generic at all.

ASP.NET has a [use case](https://github.com/dotnet/aspnetcore/issues/47338), which in order to address, we would need to make adjustments in both the above areas.

```cs
class Original<TCaptured>
{
    public void M1()
    {
        API.M2(() => this);
    }
}

class API
{
    public static void M2(Delegate del) { }
}

class Interceptors
{
    [InterceptsLocation(/* API.M2(() => this) */)]
    public static void M3<T>(Func<T> func) { }
}
```

In `M3<T>`, the generator author wants to be able to use the the type parameter `TCaptured` within the return value `=> this`. This means that we would need to permit a parameter type difference between interceptor and original methods where an implicit reference conversion exists from the interceptor parameter type (`Func<T>`) to the original method parameter type (`Delegate`). We would need to start reporting errors when the argument of an intercepted call is not convertible to the interceptor parameter type.

Any change we make in this vein, we should be careful has no possibility for "ripple effects", e.g. use of an interceptor changing the type of an expression, then changing the overload resolution of another call, changing the type of a `var`, and so on.

We would also need to define how the type arguments to the `M3<T>` call are determined. This might require doing a type argument inference on the intercepted "version" of the call.

There are more specifics which need to be investigated here before we can be sure how to move forward. ASP.NET has indicated this can wait till later in the .NET 9 cycle, so we'd like to push this question out until we've addressed the more urgent aspects of the feature design.
