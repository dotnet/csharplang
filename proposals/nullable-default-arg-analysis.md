# Nullable call analysis
Nullable analysis of calls with default arguments has a whole other set of concerns, which only overlap a bit with the declaration scenarios. Consider the following samples:

```cs
M1().ToString(); // no warning
M1(null).ToString(); // warning
M1("a").ToString(); // no warning

[return: NotNullIfNotNull("s")]
string? M1(string? s = "a") => s;
```

```cs
M2().ToString(); // no warning
M2(null).ToString(); // warning
M2("a").ToString(); // no warning

[return: NotNullIfNotNull("s")]
string? M2([CallerMemberName] string? s = null) => s;
```

Here the constant value that we pass implicitly affects the result of the analysis. For quite some time the code that comes up with the "real" default argument, the one we will actually emit based on presence of `[CallerMemberName]`, `[CallerLineNumber]` etc. attributes, has lived in LocalRewriter. It is somewhat hacked up to make different decisions based on whether it is being called from lowering or from IOperation. The places that need to know this information are:
1. The compiler lowering layer
2. IOperation
3. NullableWalker

NullableWalker must call into the code in LocalRewriter to generate a BoundExpression that wraps the ConstantValue associated with the optional parameter. This tends to make behavior uniform between source and metadata methods, so I believe we should keep doing that. The following example illustrates what I mean:

```cs
M1().ToString(); // we warn here since the synthesized default argument just looks at ConstantValue.Null without considering the suppression

[return: NotNullIfNotNull("s")]
string? M1(string s = null!) => s; // default value warning is suppressed
```

Because we have no way of encoding suppression in constant values in metadata (and probably no desire to come up with such an encoding), this approach gets us the same behavior at the call site whether `M1` is from source or from metadata. It's also worth noting that nullable warnings from the synthesized implicit arguments are **always suppressed**, because they are only a symptom of a bad default value that needs to be fixed in the signature.

Synthesizing the BoundExpression on the fly in NullableWalker has [negative impact on the implementation](https://github.com/dotnet/roslyn/blob/21ca45690196dfd7bd159636a9acf3fd2a86949b/src/Compilers/CSharp/Portable/FlowAnalysis/NullableWalker.cs#L5174-L5200), particularly because we must maintain a dictionary of `_defaultValuesOpt` and we have to use the `_disableNullabilityAnalysis` flag when visiting the synthesized arguments to keep from triggering debug assertions.

## Suggested change to calls

Instead of keeping the logic for "what's the actual implicit argument being passed" in LocalRewriter, let's determine what these arguments are *at the time the call is bound*. I propose modifying BoundCall roughly as follows:

```xml
<Node Name="BoundCall" Base="BoundExpression">
    <!-- ...leave all existing properties as-is -->

    <!-- Implicitly passed default arguments to the method. -->
    <Field Name="DefaultArgumentsOpt" Type="ImmutableArray&lt;BoundDefaultArgument&gt;" Null="allow" />
</Node>

<Node Name="BoundDefaultArgument" Base="BoundNode">
    <Field Name="Parameter" Type="ParameterSymbol" />
    <Field Name="Argument" Type="BoundExpression" />
</Node>
```

This can accomplish a few things:

1. Fix the layering violation in IOperation and NullableWalker by making it so those components no longer need to reach into lowering.
2. Simplify usages and allow removal of some ugly workarounds by making the set of "arguments that will actually be emitted" available in the bound tree.
3. Reduce multiple binding of the default arguments, since we know the arguments will be needed to emit in batch scenarios, and the arguments will be needed if nullable is enabled or if the project uses IOperation-based analyzers.

Lowering would handle the `BoundCall.DefaultArgumentsOpt` by simply folding them into the `BoundCall.Arguments` at the appropriate position when rewriting the call.
