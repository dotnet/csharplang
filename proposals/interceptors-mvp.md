Interceptors MVP:

- generated SyntaxTree.FilePath
- `InterceptsLocation(string location)` accepts a *location specifier* which denotes the source file and location of the interceptee. Format is *implementation defined*.
- public API:
    - `string SourceProductionContext.GetInterceptableLocation(InvocationExpressionSyntax intercepted, string interceptorFileHintName)`
        - Returns a string which is the "latest version" of the implementation-defined location format
        - We expect the compiler to support *consuming* previous versions of the location format, but we don't need a point of control for *producing* previous versions of that format
    - `bool CSharpExtensions.TryGetInterceptor(this SemanticModel model, InvocationExpressionSyntax intercepted, [NotNullWhen(true)] out IMethodSymbol? interceptor)`

- Why is the location format implementation-defined?
    - We want to reserve the ability to improve it in the future. Because we expect generators to obtain the attribute argument by calling a public API, we think it's useful for the location specifier to be "opaque" to the generator itself.
    - We want to continue to optimize for the source generator scenario, at least for now. The current format that we have is already not amenable to hand-authoring.
    - We intend that producing and consuming format-string is a "conversation" between generator and compiler, where compiler is parsing out the location specifier from a fragment of source code which the compiler itself produced in `GetInterceptableLocation` on behalf of a generator.

- Why are these APIs specialized to intercepting methods?
    - If we had `TryGetInterceptableLocation(SyntaxNode node)` we would still be leaving in a usability/clarity gap for generators. Which syntax node is the generator supposed to pass in? Should it be the `NameSyntax` whose location is currently used to denote the location of the intercepted call? Or should it be the containing `InvocationExpressionSyntax`? What is the failure mode supposed to be if the syntax node is of a kind which doesn't support interceptors? Do we just return null?
    - When we add support for more member kinds, we should add more public APIs corresponding to that. e.g. `MemberAccessExpression`/`ElementAccessExpression` overload for a property/indexer access or `ObjectCreationExpression` for a constructor call.
    - The commitment we are making when we support intercepting a particular member kind, is equal in significance to the commitment we make when introducing a public API. It doesn't serve generator authors to try and optimize for fewest number of public APIs in this case.

-----

- Variance (later on in C# 13)
    - https://github.com/dotnet/aspnetcore/issues/47338
    - ASP.NET has indicated this can wait till later in cycle
    - Argument type is `Func<TCaptured>`. Interceptee parameter is `System.Delegate`. Interceptor parameter wants to be `Func<T>` and to have call site pass the `TCaptured` as a type argument.

- Properties (post C# 13)
    - UnsafeAccessor does not support properties directly. Instead, a given usage of UnsafeAccessor decorates a method which calls through to a single property accessor.
    - Consider: What happens when we want to intercept `obj.Prop += a`? We can't choose a single method to replace usage of `Prop`.
    - Conclusion: We should probably favor intercepting a property with an extension property. We are likely blocked here until we ship extensions.
