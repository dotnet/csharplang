Features Added in C# Language Versions
====================

# C# 14.0 - .NET 10 and Visual Studio 2026 version 18.0
- [Extension methods and properties](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md): allows extending an existing type with instance or static methods and properties.
- [Extension operators](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extension-operators.md): allows extending an existing type with operators.
- [`field` keyword in properties](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/field-keyword.md): `field` allows access to the property's backing field without having to declare it.
- [Partial events and constructors](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/partial-events-and-constructors.md): allows the partial modifier on events and constructors to separate declaration and implementation parts.
- [User-defined compound assignment operators](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/user-defined-compound-assignment.md): allow user types to customize behavior of compound assignment operators in a way that the target of the assignment is modified in-place (`public void operator +=(int x)`).
- [First-class `Span` types](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/first-class-span-types.md): streamlines usage of `Span`-based APIs by improving type inference and overload resolution.
- [Null-conditional assignment](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/null-conditional-assignment.md): permits assignment to occur conditionally within a `a?.b` or `a?[b]` expression (`a?.b = c`).
- [Unbound generic types in `nameof`](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/unbound-generic-types-in-nameof.md): relaxes some restrictions on usage of generic types inside `nameof` (`nameof(List<>)`).
- [Simple lambda parameters with modifiers](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/simple-lambda-parameters-with-modifiers.md): allows lambda parameters to be declared with modifiers without requiring their types (`(ref entry) => ...`).
- [Ignored directives](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/ignored-directives.md): add `#:` directive prefix to be used by `dotnet run app.cs` tooling but ignored by the language.
- [Optional and named arguments in `Expression` trees](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/optional-and-named-parameters-in-expression-trees.md): relaxes some restrictions on `Expression` trees (`Expression<...> e = (a, i) => a.Contains(i, comparer: null);`).

# C# 13.0 - .NET 9 and Visual Studio 2022 version 17.12
- [ESC escape sequence](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/esc-escape-sequence.md): introduces the `\e` escape sequence to represent the ESCAPE/ESC character (U+001B).
- [Method group natural type improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/method-group-natural-type-improvements.md): look scope-by-scope and prune inapplicable candidates early when determining the natural type of a method group.
- [`Lock` object](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/lock-object.md): allow performing a `lock` on `System.Threading.Lock` instances.
- Implicit indexer access in object initializers: allows indexers in object initializers to use implicit Index/Range indexers (`new C { [^1] = 2 }`).
- [`params` collections](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/params-collections.md): extends `params` support to collection types (`void M(params ReadOnlySpan<int> s)`).
- [`ref`/`unsafe` in iterators/async](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/ref-unsafe-in-iterators-async.md): allows using `ref`/`ref struct` locals and `unsafe` blocks in iterators and async methods between suspension points.
- [`ref struct` interfaces](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/ref-struct-interfaces.md): allows `ref struct` types to implement interfaces and introduces the `allows ref struct` constraint.
- [Overload resolution priority](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/overload-resolution-priority.md): allows API authors to adjust the relative priority of overloads within a type using `System.Runtime.CompilerServices.OverloadResolutionPriority`.
- [Partial properties](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/partial-properties.md): allows splitting a property into multiple parts using the `partial` modifier.
- [Better conversion from collection expression element](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/collection-expressions-better-conversion.md): improves overload resolution to account for the element type of collection expressions.

# C# 12.0 - .NET 8 and Visual Studio 2022 version 17.8

- [Collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md): provides a uniform and efficient way of creating collections using collection-like types (`List<int> list = [1, 2, 3];`)
- [Primary Constructors](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/primary-constructors.md): helps reduce field and constructor boilerplate (`class Point(int x, int y);`)
- [Inline Arrays](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/inline-arrays.md): provides a general-purpose and safe mechanism for declaring arrays using the `[InlineArray(size)]` attribute.
- [Using aliases for any type](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/using-alias-types.md): relaxes many restrictions on `using` alias declarations, allowing built-in types, tuple types, pointer types, array types (`using Point = (int x, int y);`)
- [Ref readonly parameters](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/ref-readonly-parameters.md): `ref readonly` parameters mandate that arguments are passed by reference instead of potentially copied, can’t be modified, and warn if a temporary variable must be created.
- [Nameof accessing instance members](https://github.com/dotnet/csharplang/issues/4037): relaxes some restrictions on usage of instance members inside `nameof` (`nameof(field.ToString)`)
- [Lambda optional parameters](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/lambda-method-group-defaults.md): allows lambda parameters to declare default values (`(int i = 42) => { }`)

# C# 11.0 - .NET 7 and Visual Studio 2022 version 17.4

- [Raw string literals](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/raw-string-literal.md): introduces a string literal where the content never needs escaping (`var json = """{ "summary": "text" }""";` or `var json = $$"""{ "summary": "text", "length": {{length}} }""";`).
- [UTF-8 string literals](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/utf8-string-literals.md): UTF-8 string literals with the `u8` suffix (`ReadOnlySpan<byte> s = "hello"u8;`)
- [Pattern match `Span<char>` on a constant string](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/pattern-match-span-of-char-on-string.md): an input value of type `Span<char>` or `ReadonlySpan<char>` can be matched with a constant string pattern (`span is "123"`).
- [Newlines in interpolations](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/new-line-in-interpolation.md): allows newline characters in single-line interpolated strings.
- [List patterns](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/list-patterns.md): allows matching indexable types (`list is [1, 2, ..]`).
- [File-local types](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/file-local-types.md): introduces the `file` type modifier (`file class C { ... }`).
- [Ref fields](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/low-level-struct-improvements.md): allows `ref` field declarations in a `ref struct` (`ref struct S { ref int field; ... }`), introduces `scoped` modifier and `[UnscopedRef]` attribute.
- [Required members](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/required-members.md): introduces the `required` field and property modifier and `[SetsRequiredMembers]` attribute.
- [Static abstract members in interfaces](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/static-abstracts-in-interfaces.md): allows an interface to specify abstract static members.
- [Unsigned right-shift operator](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/unsigned-right-shift-operator.md): introduces the `>>>` operator and `>>>=`.
- [`checked` user-defined operators](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/checked-user-defined-operators.md): numeric and conversion operators support defining `checked` variants (`public static Int128 operator checked +(Int128 lhs, Int128 rhs) { ... }`).
- [Relaxing shift operator requirements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/relaxing_shift_operator_requirements.md): the right-hand-side operand of a shift operator is no longer restricted to only be `int`
- [Numeric IntPtr](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/numeric-intptr.md): `nint`/`nuint` become simple types aliasing `System.IntPtr`/`System.UIntPtr`.
- [Auto-default structs](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/auto-default-structs.md): struct constructors automatically default fields that are not explicitly assigned.
- [Generic attributes](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/generic-attributes.md): allows attributes to be generic (`[MyAttribute<int>]`).
- [Extended `nameof` scope in attributes](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/extended-nameof-scope.md): allows `nameof(parameter)` inside an attribute on a method or parameter (`[MyAttribute(nameof(parameter))] void M(int parameter) { }`).

# C# 10.0 - .NET 6 and Visual Studio 2022 version 17.0

- [Record structs](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-10.0/record-structs.md) (`record struct Point(int X, int Y);`, `var newPoint = point with { X = 100 };`).
- [With expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/record-structs.md#allow-with-expression-on-structs) on structs and anonymous types.
- [Global using directives](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-10.0/GlobalUsingDirective.md): `global using` directives avoid repeating the same `using` directives across many files in your program.
- [Improved definite assignment](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/improved-definite-assignment.md): definite assignment and nullability analysis better handle common patterns such as `dictionary?.TryGetValue(key, out value) == true`.
- [Constant interpolated strings](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/constant_interpolated_strings.md): interpolated strings composed of constants are themselves constants.
- [Extended property patterns](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/extended-property-patterns.md): property patterns allow accessing nested members (`if (e is MethodCallExpression { Method.Name: "MethodName" })`).
- [Sealed record ToString](https://github.com/dotnet/csharplang/issues/4174): a record can inherit a base record with a sealed `ToString`.
- [Incremental source generators](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md): improve the source generation experience in large projects by breaking down the source generation pipeline and caching intermediate results.
- [Mixed deconstructions](https://github.com/dotnet/csharplang/issues/125): deconstruction-assignments and deconstruction-declarations can be blended together (`(existingLocal, var declaredLocal) = expression`).
- [Method-level AsyncMethodBuilder](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/async-method-builders.md): the AsyncMethodBuilder used to compile an `async` method can be overridden locally.
- [#line span directive](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/enhanced-line-directives.md): allow source generators like Razor fine-grained control of the line mapping with `#line` directives that specify the destination span (`#line (startLine, startChar) - (endLine, endChar) charOffset "fileName"`).
- [Lambda improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/lambda-improvements.md): attributes and return types are allowed on lambdas; lambdas and method groups have a natural delegate type (`var f = short () => 1;`).
- [Interpolated string handlers](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/improved-interpolated-strings.md): interpolated string handler types allow efficient formatting of interpolated strings in assignments and invocations.
- [File-scoped namespaces](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/file-scoped-namespaces.md): files with a single namespace don't need extra braces or indentation (`namespace X.Y.Z;`).
- [Parameterless struct constructors](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/parameterless-struct-constructors.md): support parameterless constructors and instance field initializers for struct types.
- [CallerArgumentExpression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/caller-argument-expression.md): this attribute allows capturing the expressions passed to a method as strings.

# C# 9.0 - .NET 5 and Visual Studio 2019 version 16.8 
- [Records](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/records.md) and `with` expressions: succinctly declare reference types with value semantics (`record Point(int X, int Y);`, `var newPoint = point with { X = 100 };`).
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
- [Pattern matching](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-7.0/pattern-matching.md)
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
- [Draft Specification online](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/README.md)
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
