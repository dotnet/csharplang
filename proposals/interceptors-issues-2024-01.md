# Open issues for interceptors

The following issues were identified during the interceptors preview, and should be addressed before the feature is transitioned to stable.

Further down there is a "Future" section containing issues which do not block the move to stable, but could be addressed in the longer term.

## File path portability

One of our design goals with interceptors was for them to conform to the "portability" requirement of source generators. That means we should be able to run the source generators for a project containing interceptors, commit the results to disk, then remove the source generators from the project and be able to build in a variety of environments. Note that this is different from *determinism*--we don't mind if the resulting assembly differs in things like layout, etc. in each build environment, but want it to be the case that if an interceptor declaration functions in one environment, then it also functions in a different environment, assuming all the sources and dependencies are fully available in each environment.

However, we currently require absolute paths for `InterceptsLocationAttribute`, with pathmap substitution performed if present. This doesn't work between local builds on different paths, or across local builds and CI, because only CI will actually pass `/pathmap` during the build. The local build prefers to preserve local paths (e.g. in the PDB) so that the local versions of the sources can be discovered easily during debugging. The different environments will have different answers for what they expect the paths to be, and the InterceptsLocation will have errors in environments which differ from the "generation-time" environment.

Having looked at several strategies to address here, including adjusting when the build system passes `/pathmap` to the compiler, it seems the most straightforward way is to permit relative paths. This is a strategy which is already proven out by `#line` directives.

When a relative path appears in an `[InterceptsLocation]`, we will resolve it relative to the path of the containing file, just as we would for `#line`. We recommend that relative paths appearing in source use `/` directory separators for portability.

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

Since we want generated files to refer relatively to "user" files, we now need to define the paths of generated files. Also, for source generators to be able to insert the correct relative paths into the generated source, a source generator needs to know what the file path will be before the file is actually added to the compilation. We may want to add new public API like the following for this:

```diff
 namespace Microsoft.CodeAnalysis;

 public readonly struct SourceProductionContext
 {
     public void AddSource(string hintName, string source);
+    public string GetFilePath(string hintName);
+    public string GetInterceptsFilePath(SyntaxNode intercepted, string hintName);
 }
```

`GetFilePath()` will return the absolute file path which the generated file *would be written to* if `$(EmitCompilerGeneratedFiles)` is true in the project. Additionally, the syntax tree for the file added by a subsequent call to `AddSource` using the same `hintName` will have a `FilePath` which exactly matches the result of `GetFilePath()`.

`GetInterceptsFilePath(intercepted, hintName)` will return a relative file path equivalent to the path returned by `GetFilePath(hintName)` relative to `intercepted.SyntaxTree.FilePath`. We think this API will be helpful for generator authors, in significant part, because no built-in API exists on netstandard2.0 to get a relative path.

This solution will not work when the intercepted call and the interceptor method are in completely "disjunct" parts of the file system, for example, if one of the source files is included through a NuGet package which is outside the project folder, and the other is within the project folder. This limitation is similar in nature to what already exists for `#line` directives.

## Need for interceptor semantic info in IDE

Interceptors have a problematic interaction with the ILLinker analyzer, which is responsible for determining if code may be Native AOT-incompatible.

What happens is, the user calls an "original" method which is marked `[RequiresUnreferencedCode]` (i.e., incompatible with NativeAOT). A source generator adds an interceptor for the call, which *is* compatible with NativeAOT. But this is not reflected in the symbol info for the call in the Roslyn APIs.

We ended up working around this in .NET 8 by having the interceptor author also ship a *diagnostic suppressor* for the warnings produced by the ILLinker analyzer, when it knows the call will be intercepted.

```cs
routes.MapGet("/products/", () => { ... }); // ASP.NET suppressor is suppressing warning: MapGet is not NativeAOT-compatible
```

This is not ideal as it essentially requires interceptor authors to know the meaning of all diagnostics reported by analyzers whose results would be influenced by knowing the interceptor method in use at a call site. The interceptor authors must also know whether suppression is appropriate based on what the interceptor and the analyzer are each doing. For example, if the interceptor were inserting a method which *also* `[RequiresUnreferencedCode]`, it would be bad to suppress the ILLinker analyzer warning.

Ideally if an analyzer needs to know if an interceptor is being used, it could simply make a public API call to ask. This adds an extra decision that component authors need to make. For some use cases, such as rename-refactoring, it doesn't make sense to rename the interceptor method--instead the original method is what needs to be renamed. We think that in most cases, the original method is what should be analyzed, but it should still be noted as a pit that people may fall into.

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

### Brittleness of line/column approaches

While we want interceptor info to be readily available to components in the IDE, we also want the IDE to be able to provide the best possible function while not running source generators on every "keystroke plus delay". Even after our work on incremental source generators, they are still quite expensive to run, and we will need a strategy such as "regenerate on save" or "regenerate on build" in order to claw back IDE performance.

The desire for less-frequent source generation is at odds with the desire for interceptor info in IDE components, as interceptors are currently designed.

For example, we want the ILLinker analyzer's results to be as useful as possible, and ideally to not include *spurious transient warnings* while the user is making edits. For example, if a user inserts some white spaces in a method body which contains intercepted calls, we don't want the ILLinker analyzer to report that some non-intercepted call is using an unsupported method. But this requires source generators to run. This *strongly* pushes the ILLinker analyzer to run *only as frequently as interceptor source generators do*.

This would be an undesirable degradation in the user experience. Instead of being able to give users confidence that their mistakes "Native AOT-wise" will be caught very quickly by the tooling and shown to them as they type, they instead are looking at longer delays, or even not being told something is wrong until they save or hit build.

#### Opaque identifier and "syntax path" approach

We could consider replacing the line/column approach of identifying a location in a source text, with one slightly more amenable to modification of the text: a "key path" of sorts which indicates the traversal to be made through the syntax tree to arrive at the desired location. e.g. for

```cs
class C
{
    void M0() { }

    void M()
    {
        Call();
    }
}
```

e.g. in the above, we might identify the invocation `Call` by saying:
- it's in the first member of the compilation unit (`class C`)
- it's in the second member of `class C`
- it's in the first statement of method `M()`
- it's in the expression of expression statement `Call();`
- it's in the receiver of invocation expression `Call()`.

This sequence could be encoded somehow and stuffed into the attribute. This would make it so that edits in other method bodies etc. will not necessarily require regenerating the interceptor. However, it's not clear how source generators, or the generator driver, could make use of this information in order to reduce the frequency of rerunning generators. Also, public API would likely be needed for this in order for generators to create the expected InterceptsLocationAttribute argument.

#### "Handle all"

Some SGs might want to mitigate the need to rerun by simply "blanket handling" all calls to a certain set of methods in a certain context (e.g. an entire source file) at design time. Then during command line build we can actually generate granular calls.

```cs
public static void Usage()
{
    Original(42); // intercepted
    Original(42, 43); // NOT intercepted, no diagnostic reported
}

public static void Original(int item) { }
public static void Original(int item, int item2) { }

[InterceptsLocation("path/to/file.cs", memberName: "Original")]
public static void Interceptor(int item) { }
```

There is a risk with this approach that somehow a method would end up being intercepted with the "handle all" approach but not with the granular interceptors which are actually emitted.
TODO: how to find misuse and help generator authors fix? maybe if *nothing* is intercepted by it?
TODO: when we f12 on SG'd code, do we go to implementation code, even if SG is publishing "definition-only" versions of the sources?

### Intercepting more member kinds

Currently the interceptors feature only supports intercepting ordinary methods. Much like with partial methods and the upcoming partial properties, users want to be able to intercept usages of more kinds of members, such as properties and constructors.

For all the member kinds we want to support, we will need to decide how usages of each member kind are denoted syntactically. Where possible, we should try to be consistent with [UnsafeAccessorAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafeaccessorattribute?view=net-8.0), which also works by referencing members in attribute arguments.

For method invocations like `receiver.M(args)`, we use the location of the `M` token to intercept the call. Perhaps for property accesses like `receiver.P` we can use location of the `P` token. For constructors should use the location of the `new` token.

For member usages such as user-defined implicit conversions which do not have any specific corresponding syntax, we expect that interception will never be possible.
