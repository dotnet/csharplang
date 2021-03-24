Features Added in C# Language Versions
====================

# C# 9.0 - .NET 5 and Visual Studio 2019 version 16.8 
- [Records](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/records.md) and `with` expressions: succinctly declare reference types with value semantics (`record Point(int X, int Y);`, `var newPoint = point with { X: 100 }`).
- [Init-only setters](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/init.md): init-only properties can be set during object creation (`int Property { get; init; }`).
- [Top-level statements](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/top-level-statements.md): the entry point logic of a program can be written without declaring an explicit type or `Main` method.
- [Pattern matching enhancements](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/patterns3.md): relational patterns (`is < 30`), combinator patterns (`is >= 0 and <= 100`, `case 3 or 4:`, `is not null`), parenthesized patterns (`is int and (< 0 or > 100)`), type patterns (`case Type:`).
- [Native sized integers](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/native-integers.md): the numeric types `nint` and `nuint` match the platform memory size.
- [Function pointers](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/function-pointers.md): enable high-performance code leveraging IL instructions `ldftn` and `calli` (`delegate* <int, void> local;`)
- [Suppress emitting `localsinit` flag](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/skip-localsinit.md): attributing a method with `[SkipLocalsInit]` will suppress emitting the `localsinit` flag to reduce cost of zero-initialization.
- [Target-typed new expressions](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/target-typed-new.md): `Point p = new(42, 43);`.
- [Static anonymous functions](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/static-anonymous-functions.md): ensure that anonymous functions don't capture `this` or local variables (`static () => { ... };`).
- [Target-typed conditional expressions](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/target-typed-conditional-expression.md): conditional expressions which lack a natural type can be target-typed (`int? x = b ? 1 : null;`).
- [Covariant return types](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/covariant-returns.md): a method override on reference types can declare a more derived return type.
- [Lambda discard parameters](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/lambda-discard-parameters.md): multiple parameters `_` appearing in a lambda are allowed and are discards.
- [Attributes on local functions](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/local-function-attributes.md).
- [Module initializers](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/module-initializers.md): a method attributed with `[ModuleInitializer]` will be executed before any other code in the assembly.
- [Extension `GetEnumerator`](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/extension-getenumerator.md): an extension `GetEnumerator` method can be used in a `foreach`.
- [Partial methods with returned values](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/extending-partial-methods.md): partial methods can have any accessibility, return a type other than `void` and use `out` parameters, but must be implemented.
- [Source Generators](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/)

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
- [Ref returns and locals](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/ref-returns)
- [Generalized async return types](https://github.com/dotnet/roslyn/blob/master/docs/features/task-types.md)
- [More expression-bodied members](https://docs.microsoft.com/dotnet/csharp/programming-guide/statements-expressions-operators/expression-bodied-members)
- [Throw expressions](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.0/throw-expression.md)

# [C# 6](https://github.com/dotnet/roslyn/blob/master/docs/wiki/New-Language-Features-in-C%23-6.md) - Visual Studio 2015
- [Draft Specification online](https://github.com/dotnet/csharplang/blob/master/spec/README.md)
- Compiler-as-a-service (Roslyn)
- [Import of static type members into namespace](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/using-static)
- [Exception filters](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/when)
- Await in catch/finally blocks
- Auto property initializers
- Default values for getter-only properties
- [Expression-bodied members](https://docs.microsoft.com/dotnet/csharp/programming-guide/statements-expressions-operators/expression-bodied-members)
- Null propagator (null-conditional operator, succinct null checking)
- [String interpolation](https://docs.microsoft.com/dotnet/csharp/language-reference/tokens/interpolated)
- [nameof operator](https://docs.microsoft.com/dotnet/csharp/language-reference/operators/nameof)
- Dictionary initializer

# [C# 5](https://blogs.msdn.microsoft.com/mvpawardprogram/2012/03/26/an-introduction-to-new-features-in-c-5-0/) - Visual Studio 2012
- [Asynchronous methods](https://docs.microsoft.com/dotnet/csharp/programming-guide/concepts/async/)
- [Caller info attributes](https://docs.microsoft.com/dotnet/csharp/language-reference/attributes/caller-information)
- foreach loop was changed to generates a new loop variable rather than closing over the same variable every time

# [C# 4](https://msdn.microsoft.com/magazine/ff796223.aspx) - Visual Studio 2010
- [Dynamic binding](https://docs.microsoft.com/dotnet/csharp/programming-guide/types/using-type-dynamic)
- [Named and optional arguments](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments)
- [Co- and Contra-variance for generic delegates and interfaces](https://docs.microsoft.com/dotnet/standard/generics/covariance-and-contravariance)
- [Embedded interop types ("NoPIA")](https://docs.microsoft.com/dotnet/framework/interop/type-equivalence-and-embedded-interop-types)

# [C# 3](https://msdn.microsoft.com/library/bb308966.aspx) - Visual Studio 2008
- [Implicitly typed local variables](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/implicitly-typed-local-variables)
- [Object and collection initializers](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers)
- [Auto-Implemented properties](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties)
- [Anonymous types](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/anonymous-types)
- [Extension methods](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/extension-methods)
- [Query expressions, a.k.a LINQ (Language Integrated Query)](https://docs.microsoft.com/dotnet/csharp/linq/query-expression-basics)
- [Lambda expression](https://docs.microsoft.com/dotnet/csharp/programming-guide/statements-expressions-operators/lambda-expressions)
- [Expression trees](https://docs.microsoft.com/dotnet/csharp/programming-guide/concepts/expression-trees/)
- [Partial methods](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/partial-method)
- [Lock statement](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/lock-statement)

# [C# 2](https://msdn.microsoft.com/library/7cz8t42e(v=vs.80).aspx) - Visual Studio 2005
- [Generics](https://docs.microsoft.com/dotnet/csharp/programming-guide/generics/)
- [Partial types](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/partial-type)
- [Anonymous methods](https://docs.microsoft.com/dotnet/csharp/programming-guide/statements-expressions-operators/anonymous-functions)
- [Iterators, a.k.a yield statement](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/yield)
- [Nullable types](https://docs.microsoft.com/dotnet/csharp/language-reference/builtin-types/nullable-value-types)
- Getter/setter separate accessibility
- Method group conversions (delegates)
- [Static classes](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members)
- Delegate inference
- Type and namespace aliases
- [Covariance and contravariance](https://docs.microsoft.com/dotnet/csharp/programming-guide/concepts/covariance-contravariance/)

# [C# 1.2](https://docs.microsoft.com/dotnet/csharp/whats-new/csharp-version-history#c-version-12) - Visual Studio .NET 2003
- Dispose in foreach
- foreach over string specialization

# [C# 1.0](https://en.wikipedia.org/wiki/Microsoft_Visual_Studio#.NET_.282002.29) - Visual Studio .NET 2002
- [Classes](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/classes)
- [Structs](https://docs.microsoft.com/dotnet/csharp/language-reference/builtin-types/struct)
- [Enums](https://docs.microsoft.com/dotnet/csharp/language-reference/builtin-types/enum)
- [Interfaces](https://docs.microsoft.com/dotnet/csharp/programming-guide/interfaces/)
- [Events](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/event)
- [Operator overloading](https://docs.microsoft.com/dotnet/csharp/language-reference/operators/operator-overloading)
- [User-defined conversion operators](https://docs.microsoft.com/dotnet/csharp/language-reference/operators/user-defined-conversion-operators)
- [Properties](https://docs.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/properties)
- [Indexers](https://docs.microsoft.com/dotnet/csharp/programming-guide/indexers/)
- Output parameters ([out](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/out) and [ref](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/ref))
- [`params` arrays](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/params)
- [Delegates](https://docs.microsoft.com/dotnet/csharp/programming-guide/delegates/)
- Expressions
- [using statement](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/using-statement)
- [goto statement](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/goto)
- [Preprocessor directives](https://docs.microsoft.com/dotnet/csharp/language-reference/preprocessor-directives/)
- [Unsafe code and pointers](https://docs.microsoft.com/dotnet/csharp/programming-guide/unsafe-code-pointers/)
- [Attributes](https://docs.microsoft.com/dotnet/csharp/programming-guide/concepts/attributes/)
- Literals
- [Verbatim identifier](https://docs.microsoft.com/dotnet/csharp/language-reference/tokens/verbatim)
- Unsigned integer types
- [Boxing and unboxing](https://docs.microsoft.com/dotnet/csharp/programming-guide/types/boxing-and-unboxing)
