Features Added in C# Language Versions
====================

# [C# 1.0](https://en.wikipedia.org/wiki/Microsoft_Visual_Studio#.NET_.282002.29) (Visual Studio.NET)

- Classes
- Structs
- Interfaces
- Events
- Properties
- Delegates
- Expressions
- Statements
- Attributes
- Literals

# [C# 2](https://msdn.microsoft.com/en-us/library/7cz8t42e(v=vs.80).aspx) (VS 2005)
- Generics
- Partial types
- Anonymous methods
- Iterators
- Nullable types
- Getter/setter separate accessibility
- Method group conversions (delegates)
- Static classes
- Delegate inference

# [C# 3](https://msdn.microsoft.com/en-us/library/bb308966.aspx) (VS 2008)
- Implicitly typed local variables
- Object and collection initializers
- Auto-Implemented properties
- Anonymous types
- Extension methods
- Query expressions
- Lambda expression
- Expression trees
- Partial methods

# [C# 4](https://msdn.microsoft.com/en-us/magazine/ff796223.aspx) (VS 2010)
- Dynamic binding
- Named and optional arguments
- Co- and Contra-variance for generic delegates and interfaces
- Embedded interop types ("NoPIA")

# [C# 5](https://blogs.msdn.microsoft.com/mvpawardprogram/2012/03/26/an-introduction-to-new-features-in-c-5-0/) (VS 2012)
- Asynchronous methods
- Caller info attributes

# [C# 6](https://github.com/dotnet/roslyn/wiki/New-Language-Features-in-C%23-6) (VS 2015)
- [Draft Specification online](https://github.com/dotnet/csharplang/blob/master/spec/README.md)
- Compiler-as-a-service (Roslyn)
- Import of static type members into namespace
- Exception filters
- Await in catch/finally blocks
- Auto property initializers
- Default values for getter-only properties
- Expression-bodied members
- Null propagator (null-conditional operator, succinct null checking)
- String interpolation
- nameof operator
- Dictionary initializer

# [C# 7.0](https://blogs.msdn.microsoft.com/dotnet/2017/03/09/new-features-in-c-7-0/) (Visual Studio 2017)
- [Out variables](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/out-var.md)
- [Pattern matching](https://github.com/dotnet/csharplang/blob/master/proposals/patterns.md)
- [Tuples](https://github.com/dotnet/roslyn/blob/master/docs/features/tuples.md)
- [Deconstruction](https://github.com/dotnet/roslyn/blob/master/docs/features/deconstruction.md)
- [Discards](https://github.com/dotnet/roslyn/blob/master/docs/features/discards.md)
- [Local Functions](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/local-functions.md)
- [Binary Literals](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/binary-literals.md)
- [Digit Separators](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/digit-separators.md)
- Ref returns and locals
- [Generalized async return types](https://github.com/dotnet/roslyn/blob/master/docs/features/task-types.md)
- More expression-bodied members
- [Throw expressions](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/throw-expression.md)

# [C# 7.1](https://blogs.msdn.microsoft.com/dotnet/2017/10/31/welcome-to-c-7-1/) (Visual Studio 2017 version 15.3)
- [Async main](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.1/async-main.md)
- [Default expressions](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.1/target-typed-default.md)
- [Reference assemblies](https://github.com/dotnet/roslyn/blob/master/docs/features/refout.md)
- [Inferred tuple element names](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.1/infer-tuple-names.md)
- [Pattern-matching with generics](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.1/generics-pattern-match.md)

# [C# 7.2](https://blogs.msdn.microsoft.com/dotnet/2017/11/15/welcome-to-c-7-2-and-span/) (Visual Studio 2017 version 15.5)
- [Span and ref-like types](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md)
- [In parameters and readonly references](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/readonly-ref.md)
- [Ref conditional](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/conditional-ref.md)
- [Non-trailing named arguments](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/non-trailing-named-arguments.md)
- [Private protected accessibility](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/private-protected.md)
- [Digit separator after base specifier](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/leading-separator.md)

# C# 7.3 (Visual Studio 2017 version 15.7)
- `System.Enum`, `System.Delegate` and [`unmanaged`](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/blittable.md) constraints.
- [Ref local re-assignment](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/ref-local-reassignment.md): Ref locals and ref parameters can now be reassigned with the ref assignment operator (`= ref`).
- [Stackalloc initializers](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/stackalloc-array-initializers.md): Stack-allocated arrays can now be initialized, e.g. `Span<int> x = stackalloc[] { 1, 2, 3 };`.
- [Indexing movable fixed buffers](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/indexing-movable-fixed-fields.md): Fixed buffers can be indexed into without first being pinned.
- [Custom `fixed` statement](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/pattern-based-fixed.md): Types that implement a suitable `GetPinnableReference` can be used in a `fixed` statement.
- [Improved overload candidates](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/improved-overload-candidates.md): Some overload resolution candidates can be ruled out early, thus reducing ambiguities.
- [Expression variables in initializers and queries](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/expression-variables-in-initializers.md): Expression variables like `out var` and pattern variables are allowed in field initializers, constructor initializers and LINQ queries.
-	[Tuple comparison](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/tuple-equality.md): Tuples can now be compared with `==` and `!=`.
-	[Attributes on backing fields](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/auto-prop-field-attrs.md): Allows `[field: …]` attributes on an auto-implemented property to target its backing field.
