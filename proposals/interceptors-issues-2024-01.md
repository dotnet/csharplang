# Open issues for interceptors

The following issues were identified during the interceptors preview, and should be addressed before the feature is transitioned to stable.

Further down there is a "Future" section containing issues which do not block the move to stable, but could be addressed in the longer term.

## Checksums instead of file paths

We identified a number of issues with using file paths to identify the location of a call.

- Absolute paths, while simple and accurate, break portability (the ability to generate code in one environment and compile it in another).
- Relative paths require generator authors to consume contextual information about "where the generated file will be written to" on disk. This complicates the public API design and feels arbitrary.
- We considered also introducing "project-relative" paths for use in InterceptsLocationAttribute, which would reduce the amount of public API bloat. However, this effectively introduces a new requirement on compilations created through the public APIs: they need to have the path resolvers in compilation options properly configured to indicate where the "project root" is. Otherwise, the Roslyn APIs can't correctly interpret the meaning of source code in the compilation.

We finally arrived at checksums as a more satisfactory way of identifying the file containing the intercepted call. This essentially does away with the portability and complexity concerns around paths.

It also pushes us toward an opaque representation of "interceptable locations". In the design we have now, it's expected that InterceptsLocation attribute arguments are produced solely by calling the public APIs. We don't expect users to create or to interpret the arguments to InterceptsLocationAttribute.

See https://github.com/dotnet/roslyn/issues/72133 for further details on this.

## Need for interceptor semantic info in IDE

We added public API which allows determining if a call in a compilation is being intercepted, and if so, which method declaration is decorated with the InterceptsLocationAttribute referencing the call.

See https://github.com/dotnet/roslyn/issues/72093 for further details.

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
