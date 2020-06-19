Features Added in C# Language Versions
====================

# C# 8.0 - .NET Core 3.0 and Visual Studio 2019 version 16.3 
- [Nullable reference types](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/nullable-reference-types-specification.md): express nullability intent on reference types with `?`, `notnull` constraint and annotations attributes in APIs, the compiler will use those to try and detect possible `null` values being dereferenced or passed to unsuitable APIs.
- [Default interface members](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/default-interface-methods.md): interfaces can now have members with default implementations, as well as static/private/protected/internal members except for state (ie. no fields).
- [Recursive patterns](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/patterns.md): positional and property patterns allow testing deeper into an object, and switch expressions allow for testing multiple patterns and producing corresponding results in a compact fashion.
- [Async streams](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/async-streams.md): `await foreach` and `await using` allow for asynchronous enumeration and disposal of `IAsyncEnumerable<T>` collections and `IAsyncDisposable` resources, and async-iterator methods allow convenient implementation of such asynchronous streams.
- [Enhanced using](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/using.md): a `using` declaration is added with an implicit scope and `using` statements and declarations allow disposal of `ref` structs using a pattern.
- [Ranges and indexes](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-8.0/ranges.md): the `i..j` syntax allows constructing `System.Range` instances, the `^k` syntax allows constructing `System.Index` instances, and those can be used to index/slice collections.
- [Null-coalescing assignment](https://github.com/dotnet/csharplang/issues/34): `??=` allows conditionally assigning when the value is null.
- [Static local functions](https://github.com/dotnet/csharplang/issues/1565): local functions modified with `static` cannot capture `this` or local variables, and local function parameters now shadow locals in parent scopes.
- [Unmanaged generic structs](https://github.com/dotnet/csharplang/issues/1744): generic struct types that only have unmanaged fields are now considered unmanaged (ie. they satisfy the `unmanaged` constraint).
- [Readonly members](https://github.com/dotnet/csharplang/issues/1710): individual members can now be marked as `readonly` to indicate and enforce that they do not modify instance state.
- [Stackalloc in nested contexts](https://github.com/dotnet/csharplang/issues/1412): `stackalloc` expressions are now allowed in more expression contexts.
- [Alternative interpolated verbatim strings](https://github.com/dotnet/csharplang/issues/1630): `@$"..."` strings are recognized as interpolated verbatim strings just like `$@"..."`.
- [Obsolete on property accessors](https://github.com/dotnet/csharplang/issues/2152): property accessors can now be individually marked as obsolete.
- [Permit `t is null` on unconstrained type parameter](https://github.com/dotnet/csharplang/issues/1284)

# C# 7.3 - Visual Studio 2017 version 15.7
- `System.Enum`, `System.Delegate` and [`unmanaged`](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/blittable.md) constraints.
- [Ref local re-assignment](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/ref-local-reassignment.md): Ref locals and ref parameters can now be reassigned with the ref assignment operator (`= ref`).
- [Stackalloc initializers](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/stackalloc-array-initializers.md): Stack-allocated arrays can now be initialized, e.g. `Span<int> x = stackalloc[] { 1, 2, 3 };`.
- [Indexing movable fixed buffers](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/indexing-movable-fixed-fields.md): Fixed buffers can be indexed into without first being pinned.
- [Custom `fixed` statement](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/pattern-based-fixed.md): Types that implement a suitable `GetPinnableReference` can be used in a `fixed` statement.
- [Improved overload candidates](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/improved-overload-candidates.md): Some overload resolution candidates can be ruled out early, thus reducing ambiguities.
- [Expression variables in initializers and queries](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/expression-variables-in-initializers.md): Expression variables like `out var` and pattern variables are allowed in field initializers, constructor initializers and LINQ queries.
-	[Tuple comparison](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/tuple-equality.md): Tuples can now be compared with `==` and `!=`.
-	[Attributes on backing fields](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/auto-prop-field-attrs.md): Allows `[field: …]` attributes on an auto-implemented property to target its backing field.

# [C# 7.2](https://blogs.msdn.microsoft.com/dotnet/2017/11/15/welcome-to-c-7-2-and-span/) - Visual Studio 2017 version 15.5
- [Span and ref-like types](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md)
- [In parameters and readonly references](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/readonly-ref.md)
- [Ref conditional](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/conditional-ref.md)
- [Non-trailing named arguments](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/non-trailing-named-arguments.md)
- [Private protected accessibility](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/private-protected.md)
- [Digit separator after base specifier](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/leading-separator.md)

# [C# 7.1](https://blogs.msdn.microsoft.com/dotnet/2017/10/31/welcome-to-c-7-1/) - Visual Studio 2017 version 15.3
- [Async main](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.1/async-main.md)
- [Default expressions](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.1/target-typed-default.md)
- [Reference assemblies](https://github.com/dotnet/roslyn/blob/master/docs/features/refout.md)
- [Inferred tuple element names](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.1/infer-tuple-names.md)
- [Pattern-matching with generics](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.1/generics-pattern-match.md)

# [C# 7.0](https://blogs.msdn.microsoft.com/dotnet/2017/03/09/new-features-in-c-7-0/) - Visual Studio 2017
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

# [C# 6](https://github.com/dotnet/roslyn/wiki/New-Language-Features-in-C%23-6) - Visual Studio 2015
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

# [C# 5](https://blogs.msdn.microsoft.com/mvpawardprogram/2012/03/26/an-introduction-to-new-features-in-c-5-0/) - Visual Studio 2012
- Asynchronous methods
- Caller info attributes
- foreach loop was changed to generates a new loop variable rather than closing over the same variable every time

# [C# 4](https://msdn.microsoft.com/magazine/ff796223.aspx) - Visual Studio 2010
- Dynamic binding
- Named and optional arguments
- Co- and Contra-variance for generic delegates and interfaces
- Embedded interop types ("NoPIA")

# [C# 3](https://msdn.microsoft.com/library/bb308966.aspx) - Visual Studio 2008
- Implicitly typed local variables
- Object and collection initializers
- Auto-Implemented properties
- Anonymous types
- Extension methods
- Query expressions, a.k.a LINQ (Language Integrated Query)
- Lambda expression
- Expression trees
- Partial methods

# [C# 2](https://msdn.microsoft.com/library/7cz8t42e(v=vs.80).aspx) - Visual Studio 2005
- Generics
- Partial types
- Anonymous methods
- Iterators
- Nullable types
- Getter/setter separate accessibility
- Method group conversions (delegates)
- Static classes
- Delegate inference
- Type and namespace aliases

# [C# 1.2](https://docs.microsoft.com/dotnet/csharp/whats-new/csharp-version-history#c-version-12) - Visual Studio .NET 2003
- Dispose in foreach
- foreach over string specialization

# [C# 1.0](https://en.wikipedia.org/wiki/Microsoft_Visual_Studio#.NET_.282002.29) - Visual Studio .NET 2002
- Classes
- Structs
- Enums
- Interfaces
- Events
- [Operator overloading](https://docs.microsoft.com/dotnet/csharp/language-reference/operators/operator-overloading)
- [User-defined conversion operators](https://docs.microsoft.com/dotnet/csharp/language-reference/operators/user-defined-conversion-operators)
- Properties
- [Indexers](https://docs.microsoft.com/dotnet/csharp/programming-guide/indexers/)
- Output parameters (out and ref)
- [Array trailing parameter (params)](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/params)
- Delegates
- Expressions
- [Statements](https://docs.microsoft.com/dotnet/csharp/programming-guide/statements-expressions-operators/statements)
- [using statement](https://docs.microsoft.com/dotnet/csharp/language-reference/language-specification/statements#the-using-statement)
- [goto statement](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/goto)
- [Preprocessor Directives](https://docs.microsoft.com/dotnet/csharp/language-reference/preprocessor-directives/)
- [Unsafe code and pointers](https://docs.microsoft.com/dotnet/csharp/programming-guide/unsafe-code-pointers/)
- Attributes
- Literals
- [Verbatim identifier](https://docs.microsoft.com/dotnet/csharp/language-reference/tokens/verbatim)
- Unsigned integer types
