# Collection Expressions in `foreach`

## Summary

[Collection Expressions(https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md)
introduced a terse syntax `[e1, e2, e3, etc]` to create common collection values.
This proposal extends their usage to `foreach` statements, where they can be used directly as the iteration
source without requiring an explicit target type.

## Motivation

It is common and reasonable for developers to want to iterate over a known set of values. This pattern appears
frequently in real-world code:

```csharp
// Today, developers must write:
foreach (var toggle in new[] { true, false })
{
    RunTestWithFeatureFlag(toggle);
}

// With this proposal, they can write:
foreach (var toggle in [true, false])
{
    RunTestWithFeatureFlag(toggle);
}
```

Another common scenario is iterating through a fixed set of stages or phases:

```csharp
// Today:
foreach (var phase in new[] { Phase.Parsing, Phase..Binding, Phase..Lowering", Phase..Emit })
{
    ExecuteCompilerPhase(phase);
}

// With this proposal:
foreach (var phase in [Phase.Parsing, Phase.Binding, Phase.Lowering, Phase.Emit])
{
    ExecuteCompilerPhase(phase);
}
```

Requests for this capability have been heard internally and throughout the ecosystem.  This feature
was originally part of the collection expressions work but was extracted to keep the initial scope
minimal. Additionally, implementing this in the general case would require giving collection expressions
a "natural type," which proved to be too large and complex a design space to tackle at that time.

However, for `foreach` statements specifically, the problem space is much simpler. The collection is
created and immediately consumed—user code cannot introspect the collection itself—giving the language
and compiler broad flexibility in implementation without the complexities of determining a universal
natural type.  This flexibility follows the design principles of collection-expresions themselves, 
allowing optimal performance, with minimal syntax.

## Detailed design

### Syntax

No grammar changes are required. Collection expressions are already valid expressions syntactically; thi
s proposal only extends where they can be used semantically.

### Semantics

#### Explicitly typed foreach

For an explicitly typed `foreach` statement of the form:

```csharp
foreach (T v in [e1, e2, ..s1, etc.])
{
    // ...
}
```

This is interpreted as:

```csharp
foreach (T v in (T[])[e1, e2, ..s1, etc.])
{
    // ...
}
```

In other words, the collection expression is target-typed to an array of the explicitly provided iteration type `T`.

#### Implicitly typed foreach

For an implicitly typed `foreach` statement of the form:

```csharp
foreach (var v in [e1, e2, ..s1, etc.])
{
    // ...
}
```

The element type `T_e` is computed using the [best common type](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#116315-finding-the-best-common-type-of-a-set-of-expressions)

- The types of all expression elements `e1`, `e2`, etc.
- The [element types](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#conversions)
  of all spread elements `..s1`, etc.

The statement is then interpreted as:

```csharp
foreach (T_e v in (T_e[])[e1, e2, ..s1, etc.])
{
    // ...
}
```

If no best common type can be determined, a compile-time error occurs.

### Implementation flexibility

While the semantics are defined in terms of array creation, a compliant implementation is free to optimize
the collection expression however it deems appropriate, provided the observable behavior remains the same. For example:

- When the element count is known and there are no intervening `await` expressions or `yield` statements, the implementation may use:
  - A stack-allocated `ReadOnlySpan<T>` for the elements
  - Data stored in the program's constant data segment
  - Complete elision of the collection when possible

For example, `foreach (var i in [0, 1, 2, 3])` could be translated to:
```csharp
for (var i = 0; i <= 3; i++)
{
    // ...
}
```

This follows the same implementation flexibility principle established in the base collection expressions
[translation specification](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#collection-literal-translation).

### Design decisions

#### Using explicit type vs. best common type

The explicitly typed case uses the provided type `T` rather than computing best common type. This ensures the
iteration type properly informs the collection expression elements. For example:

```csharp
foreach (byte b in [1, 2, 3])  // Values treated as bytes
{
    // ...
}
```

If best common type was used in both cases, the values `1`, `2`, `3` would be typed as `int`, which would then fail to convert to `byte`.

#### Empty collection expression

The empty collection expression `[]` is:
- **Legal** in the explicitly typed case: `foreach (int i in []) { }` - the target type is known (`int[]`)
- **Illegal** in the implicitly typed case: `foreach (var v in []) { }` - no element type can be inferred

This is not a special rule but follows naturally from the semantic translations above.

#### Pointer types

Because the semantics are defined in terms of arrays, pointer types are supported:

```csharp
unsafe
{
    int* p = null;
    foreach (var v in [p, p])  // Legal - creates int*[]
    {
        // ...
    }
}
```

This would not be possible if the target type were `Span<T>` or `ReadOnlySpan<T>`, which cannot contain pointer types.

## Examples

### Valid usage

```csharp
// Implicitly typed with naturally typed literals
foreach (var value in [1, 2, 3, 4, 5]) // Element type is int

// Explicitly typed with strings
foreach (string s in ["hello", "world"]) // Element type is string

// With spread elements
int[] existing = { 1, 2, 3 };
foreach (var n in [0, ..existing, 4]) // Element type is int

// Type inference with mixed elements
IEnumerable<int> enumerable = GetNumbers();
foreach (var n in [1, 2, ..enumerable, 3]) // Element type is int

// With lambda expressions (requires explicit typing)
foreach (Func<int, int> f in [null, i => i, i => i * i])

// Empty collection with explicit type
foreach (string s in [])

// Boolean values
foreach (var b in [true, false]) // Element type is bool

// Null with reference types infers nullable
foreach (var s in [null, "hello", "world"]) // Element type is string?

// Dynamic elements
dynamic d = GetDynamic();
foreach (var x in [1, d, 3]) // Element type is dynamic

// Await expressions (not await foreach)
foreach (var result in [await GetValueAsync(), await GetOtherValueAsync()]) // Element type is BCT of the expressions

// Anonymous types
foreach (var item in [new { A = 1 }, new { A = 2 }]) // Element type is the anonymous type.

// Mixed arrays with collection expression
int[] arr = { 1, 2 };
foreach (var a in [arr, [3, 4]]) // Legal.  Inner collection expression target typed to int[].  Outer to int[][]
```

### Invalid usage

```csharp
// Error: Cannot infer element type from empty collection
foreach (var x in [])

// Error: No best common type between incompatible types
foreach (var x in [SyntaxKind.IfKeyword, "string"])

// Error: Lambda expressions need target type
foreach (var transform in [node => node.WithoutTrivia()])

// Error: No best common type when all elements are collection expressions
foreach (var tokens in [[SyntaxKind.Public, SyntaxKind.Private], 
                        [SyntaxKind.Static, SyntaxKind.Async]])

// Error: await foreach doesn't work with collection expressions
// (IAsyncEnumerable cannot be created from collection expression)
await foreach (var compilation in [comp1, comp2, comp3])
{
    // ...
}
```

## Design notes

### Relationship to natural types

This feature deliberately avoids giving collection expressions a general "natural type." While there is
clear user demand for expressions like `var x = [1, 2, 3]`, determining what type `x` should be (array, `List<T>`,
`ImmutableArray<T>`, etc.) involves complex trade-offs around mutability, performance, and API design.

The `foreach` scenario sidesteps these issues because:
1. The collection is immediately consumed and cannot be stored or passed elsewhere
2. The compiler can choose the most efficient representation for each specific case
3. User code cannot depend on the specific collection type chosen

This allows us to provide value to users now while leaving the door open for a future "natural types" feature.

### Optimization opportunities

Implementations are encouraged to aggressively optimize these patterns. Since the collection's lifetime is limited
to the `foreach` statement itself, compilers can:

- Use stack allocation for small, known-size collections
- Embed constant data directly in the assembly
- Transform simple patterns into equivalent `for` loops
- Use specialized enumeration patterns that avoid allocations

The only requirement is that the iteration order and values match what would be produced by creating and iterating an array.
These optimizations are best-effort, consistent with the approach taken in the base collection expressions specification.

### Special cases

#### Nullability

When `null` literals appear in a collection expression with reference types, the best common type computation will produce a
nullable reference type. For example, `[null, "hello"]` has an element type of `string?`.

#### Dynamic

When any element in the collection expression is of type `dynamic`, the computed element type becomes `dynamic`. This
follows the standard best common type rules where `dynamic` acts as a "top type" for type inference purposes.

#### Nested collection expressions

Collection expressions cannot be nested when using implicit typing if all elements are collection expressions, as collection
expressions have no natural type. However, mixing arrays or other collections with collection expressions works:
`[existingArray, [1, 2, 3]]` is valid because `existingArray` provides a concrete type for best common type computation.

#### Async considerations

This feature does not support `await foreach` with collection expressions, as `IAsyncEnumerable<T>` cannot be created
from a collection expression. However, `await` expressions can appear as elements:
`foreach (var x in [await GetValueAsync(), await GetOtherAsync()])` is valid and the awaits are evaluated before iteration begins.

## Open questions

None at this time.

## Design meetings

[TBD: Links to relevant LDM notes]
