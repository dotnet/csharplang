# Extension indexers

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Declaration

### Grammar

Extension indexers are added to the set of permitted members inside an extension
declaration by extending the grammar as follows (relative to
[proposals/csharp-14.0/extensions.md](proposals/csharp-14.0/extensions.md#declaration)):

```antlr
extension_member_declaration
        : method_declaration
        | property_declaration
        | indexer_declaration // new
        | operator_declaration
        ;
```

Like ordinary indexers, extension indexers have no identifier and are identified
by their parameter list. Extension indexers may use the full set of features that
ordinary indexers support today (accessor bodies, expression-bodied members,
ref-returning accessors, `scoped` parameters, attributes, etc.).

Because indexers are always instance members, an extension block that declares
an indexer must provide a named receiver parameter.  

The existing restrictions on extension members continue to apply: indexers inside an
extension declaration cannot specify `abstract`, `virtual`, `override`, `new`,
`sealed`, `partial`, `protected` (or any of the related accessibility modifiers),
or `init` accessors.

```csharp
public static class BitExtensions
{
    extension(int i)
    {
        public bool this[int index]
        {
            get => ...;
        }
    }
}
```

All rules from the C# standard that apply to ordinary indexers apply to extension indexers,
but extension members do not have an implicit or explicit `this`.

### `IndexerName` attribute

`IndexerNameAttribute` may be applied to an extension indexer. The attribute is
carried over to the extension member in metadata and its value determines
the name of the property and accessors in metadata, 
and the string used when emitting `[DefaultMemberAttribute]` (see [Metadata](#metadata)).

## Consumption

### Indexer access

The rules in [Indexer access](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#128124-indexer-access)
are updated: if the normal processing of the indexer access finds no applicable indexer,
an attempt is made to process the construct as an extension indexer access.

1. Attempt to bind using only the instance indexers declared (or inherited) on
    the receiver type. If an applicable candidate is found, overload resolution
    selects among those instance members as today and stops.
2. If the set of candidate indexers is empty, an attempt is made to process the 
    **element_access** as an extension indexer access.
3. If both steps fail to identify any applicable indexers, 
    an attempt is made to process the **element_access** as
    an implicit `System.Index`/`System.Range` indexer access
    (which relies on `Length`/`Count` plus `this[int]`/`Slice(int, int)`).

#### Extension indexer access

Extension members, including extension indexers, are never considered when the
receiver is a **base_access** expression.

Note: we only process an **element_access** as an indexer access if the receiver
is a variable or value, so extension indexers are never considered when the
receiver is a type.

Given an **element_access** `E[A]`, the objective is to identify an extension indexer.

A candidate extension indexer is ***applicable*** with respect to receiver `E` and argument list `A`
if an expanded signature, comprised of the type parameters of the extension block and
a parameter list combining the extension parameter with the indexer's parameters, is applicable
with respect to an argument list combining the receiver `E` with the argument list `A`.

We reuse the extension method scope-walk: we traverse the same scopes consulted for
extension method invocation, including the current and enclosing lexical scopes
and `using` namespace or `using static` imports.

Considering each scope in turn:
- Extension blocks in non-generic class declarations in the current scope are considered.
- The indexers in those extension blocks comprise the candidate set.
- Candidates that are not accessible are removed from the set.
- Candidates that are not applicable (as defined above) are removed from the set.
- If the resulting set of candidate indexers is empty, then we proceed to the next scope,
  or fail to resolve an extension indexer access if we reached the last scope
  (we'll continue on to attempt to resolve as an implicit indexer in that case).
- Otherwise, overload resolution is applied to the candidate set. 
  If a single best indexer cannot be identified, the extension indexer access is ambiguous,
  and a compile-time error occurs.

Using this single best indexer identified at the previous step, the indexer access 
is then processed as a static method invocation.  

Depending on the context in which it is used, an indexer access causes invocation of either 
the *get_accessor* or the *set_accessor* of the indexer.  
If the indexer access is the target of an assignment, the *set_accessor* static implementation method
is invoked to assign a new value.  
In all other cases, the *get_accessor* static implementation method is invoked
to obtain the current value.  
Either way, the invocation will use generic arguments inferred during the applicability check and
the receiver as the first argument.

### Other element-access forms

Any construct that defers to element-access binding (null-conditional element access or assignments,
index assignments in object initializers, or list and spread patterns) automatically
participates in the extension indexer resolution described above.

- There is an open question on the role that `Length` and `Count` extension
    properties should play in types being considered *countable* for the purpose of
    those patterns.
- The implicit `System.Index`/`System.Range` fallback still relies on instance
    `Length`/`Count` members plus instance `this[int]`/`Slice(int, int)` and ignores
    extension members.

### Expression trees

Extension indexers cannot be captured in expression trees.

## Metadata

Extension indexers follow the same lowering model as extension properties. For
each CLR-level extension grouping type that contains at least one indexer, the
compiler emits:

- An extension property named `Item` (or the value supplied by
    `IndexerNameAttribute`) with accessor bodies that `throw NotImplementedException()`
    and an `[ExtensionMarkerName]` attribute referencing the appropriate extension
    marker type.
- Implementation methods named `get_Item`/`set_Item` in the enclosing static
    class. These methods prepend the receiver parameter to the parameter list and
    contain the user-defined bodies. They are `static` and participate in overload
    resolution in the same way as implementation methods for extension properties.

To mirror the behavior of ordinary indexers, the compiler also emits
`[DefaultMemberAttribute]` on any extension grouping type that contains one or
more extension indexers. The attribute’s `MemberName` equals the metadata name of
the indexer (`Item` by default, or the value from `IndexerNameAttribute`).

### Example

Source code:

```csharp
static class BitExtensions
{
    extension<T>(T t)
    {
        public bool this[int index]
        {
            get => ...;
            set => ...;
        }
    }
}
```

Emitted metadata (simplified to C#-like syntax):

```csharp
[Extension]
static class BitExtensions
{
    [Extension, SpecialName, DefaultMember("Item")]
    public sealed class <G>$T0
    {
        [SpecialName]
        public static class <M>$T_t
        {
            [SpecialName]
            public static void <Extension>$(T t) { } // marker method
        }

        [ExtensionMarkerName("<M>$T_t")]
        public bool this[int index] // extension indexer
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }

    // accessor implementation methods
    public static bool get_Item<T>(T t, int index) => ...;
    public static void set_Item<T>(T t, int index, bool value) => ...;
}
```

## Open issues

### Should extension `Length`/`Count` properties make a type countable?

If we expose `this[Index]` or `this[Range]` extension indexers in element access scenarios,
it is natural to expect the target type to work in list patterns.  
List patterns, however, require a `Length` or `Count` property.

Should extension properties satisfy that requirement? (that would seem natural)

But then, should those properties also contribute to the implicit indexer fallback
(`Length`/`Count` + `Slice`) that is used when an explicit `Index`/`Range` indexer
is missing?
