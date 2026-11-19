# Type groups

Champion issue: none dedicated yet; tracked provisionally under [#9627](https://github.com/dotnet/csharplang/issues/9627).

## Summary
[summary]: #summary

A *type group* contains the accessible types with a given name across all generic arities.

## Motivation
[motivation]: #motivation

Consider a family of types:

```csharp
namespace Widgets
{
    class Queue { }
    class Queue<T> { }
    class Queue<T1, T2> { }
}
```

Ordinary lookup of `Widgets.Queue`, `Widgets.Queue<>`, or `Widgets.Queue<,>` knows the arity from the written type arguments. [Constructor inference](https://github.com/dotnet/csharplang/pull/10312) does not: lookup must find the candidate types before [generic inference](https://github.com/dotnet/csharplang/pull/10311) can determine their type arguments.

A type group separates those operations. Like member lookup that produces a method group, type-group-name lookup gathers same-name declarations and a later operation selects among them.

## Detailed design
[design]: #detailed-design

The normative changes are in [csharpstandard PR #2](https://github.com/MadsTorgersen/csharpstandard/pull/2). They add type-group-name lookup to [§7.8 Namespace and type names](https://github.com/dotnet/csharpstandard/blob/alpha-v12/standard/basic-concepts.md#78-namespace-and-type-names), and type groups to [§8.4 Constructed types](https://github.com/dotnet/csharpstandard/blob/alpha-v12/standard/types.md#84-constructed-types). The grammar additions below summarize those changes.

### Type group names

```diff
+ type_group_name
+     : identifier
+     | namespace_or_type_name '.' identifier
+     | identifier '::' identifier
+     ;
```

A *type_group_name* is resolved like a *namespace_or_type_name*, except that one lookup considers all generic arities and produces a set of accessible unbound types. The set contains at most one type of each arity.

For the example above, `Widgets.Queue` produces the three declarations `Widgets.Queue`, `Widgets.Queue<T>`, and `Widgets.Queue<T1, T2>`. Different arities coexist; none hides another.

### Type groups

```diff
+ type_group
+     : type
+     | type_group_name
+     ;
```

A *type* produces a singleton type group. A *type_group_name* produces the set found by type-group-name lookup. This permits explicit types such as `Widgets.Queue<int>`, `int`, and `(int X, int Y)` wherever a consumer accepts a type group.

When both alternatives recognize the same text, *type_group_name* is preferred. Thus `Widgets.Queue` denotes the whole family in a type-group context even when the non-generic `Widgets.Queue` exists.

This proposal does not change an existing grammar context to accept a type group. [Constructor inference](https://github.com/dotnet/csharplang/pull/10312) is the first consumer.

### Lookup

Type-group-name lookup is one lookup across arities, not independent ordinary lookups combined afterward. For an unqualified name, it follows the existing lexical and import order: type parameters, nested types from the innermost containing type outward, and namespace scopes from the innermost namespace to the global namespace. At each namespace scope, directly declared entities and aliases precede imported types. Lookup stops at the first non-empty result.

A blocking result still stops lookup even when it cannot form a type group. A namespace, type parameter, or alias for a constructed type therefore prevents lookup from continuing to same-name types in an outer scope. If the first result contains anything other than unbound types, the *type_group_name* is invalid. An alias for a non-generic type can form a singleton group.

For `N.I`, `N` is resolved first. A namespace contributes its directly contained entities named `I`; a class, struct, or interface contributes accessible nested types named `I` from itself and its bases. For `A::I`, the alias `A` must identify a namespace. As above, the result is valid only when every resulting entity is an accessible unbound type.

### Ambiguity and nested hiding

Different arities coexist, but two distinct results of the same arity are ambiguous:

```csharp
using North;
using South;

namespace North { class Queue<T> { } }
namespace South { class Queue<T> { } }

// Queue is ambiguous: both imported results have arity one.
```

The ambiguity is diagnosed during lookup rather than left for a consumer to resolve through inference or applicability.

For nested types, a declaration hides a base declaration only at the same arity:

```csharp
class Base
{
    public class Queue { }
    public class Queue<T> { }
}

class Derived : Base
{
    public class Queue<T> { }

    void M()
    {
        // Queue contains Base.Queue and Derived.Queue<T>.
    }
}
```

For each arity, the declaration in the most derived type wins. If no same-arity declaration is more derived than all the others, lookup is ambiguous.

### Relationship to method groups

The analogy is limited to the separation of lookup from later binding. A method group may contain several methods with the same parameter count; a type group contains at most one declaration per arity. Same-arity ambiguity is a lookup error, not a candidate-selection question.

### Scope

This proposal defines type-group syntax, lookup, and formation. It does not define generic inference, constructor candidates, applicability, betterness, or overload resolution. Those mechanisms are defined by the [generalized inference](https://github.com/dotnet/csharplang/pull/10311) and [constructor inference](https://github.com/dotnet/csharplang/pull/10312) proposals.

## Compatibility

Type groups alone change no programs because this proposal does not add them to an existing grammar context.

A consuming feature can change lookup and candidate behavior. In particular, a nearer declaration of any arity can stop cross-arity lookup where an existing arity-specific lookup would continue, and additional arities can introduce candidates or ambiguity. Each consumer must account for that impact.

## Alternatives
[alternatives]: #alternatives

- **Consumer-specific lookup:** Each consumer could define its own cross-arity lookup, but then aliases, accessibility, hiding, ambiguity, and stopping rules could differ between consumers.
- **Lookup once per arity:** Combining independent ordinary lookups would allow a lookup for one arity to pass a nearer declaration of another arity, contrary to the single name lookup defined here.
- **Prefer a non-generic type:** Recognizing a type group only when ordinary lookup fails would prevent `Queue<T>` from participating whenever `Queue` also exists.
- **Defer same-arity ambiguity:** Letting consumers distinguish same-arity declarations through applicability would make name lookup consumer-dependent.

## Open questions
[open]: #open-questions

### Is "type group" user-facing terminology?

The specification needs a term for the set passed from lookup to a consumer. It remains to decide whether diagnostics and user documentation should expose *type group* or reserve it for the language specification.
