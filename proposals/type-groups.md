# Type groups

Champion issue: None yet. This shared infrastructure may temporarily be associated with [constructor inference](https://github.com/dotnet/csharplang/issues/9627).

## Summary
[summary]: #summary

A *type group* represents the accessible types with a given name across all arities. It gives language features that need to infer a type's arity one shared, method-group-like lookup abstraction instead of making each feature invent its own subtly different lookup rules.

## Motivation
[motivation]: #motivation

Consider a library with a family of types:

```csharp
namespace Widgets
{
    class Queue { }
    class Queue<T> { }
    class Queue<T1, T2> { }
}
```

Today, `Widgets.Queue`, `Widgets.Queue<>`, and `Widgets.Queue<,>` each tell lookup how many type parameters to look for. That works when the programmer supplies the type arguments. It does not work for a feature such as constructor inference, where inference may need to discover not only the arguments but also whether there are zero, one, or two of them.

Lookup cannot first pick an arity and then ask inference to justify that choice. It needs to make the whole `Widgets.Queue` family available to the later binding operation. This is much like a method group: lookup gathers same-name declarations first, and invocation later decides which method works.

Type groups provide that first half. They do not say how to infer type arguments, which constructors are candidates, or which candidate is best.

## Detailed design
[design]: #detailed-design

The corresponding specification changes are tracked in [csharpstandard PR #2](https://github.com/MadsTorgersen/csharpstandard/pull/2). They add type-group-name lookup alongside [§7.8 Namespace and type names](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/basic-concepts.md#78-namespace-and-type-names), and type-group formation alongside [§8.4 Constructed types](https://github.com/dotnet/csharpstandard/blob/draft-v9/standard/types.md#84-constructed-types).

### Type group names

A *type_group_name* names a family without specifying an arity:

```diff
+ type_group_name
+     : identifier
+     | namespace_or_type_name '.' identifier
+     | identifier '::' identifier
+     ;
```

The three forms cover an unqualified name such as `Queue`, a qualified name such as `Widgets.Queue`, and an alias-qualified name such as `widgets::Queue`.

Type-group-name lookup follows the shape of namespace-or-type-name lookup, including accessibility, aliases, lexical scopes, imports, nested types, and hiding. The important difference is that it performs one lookup across all arities. Its successful result contains accessible unbound types with the given name, with at most one type for each arity.

For the lead example, lookup of `Widgets.Queue` produces:

```text
Widgets.Queue
Widgets.Queue<T>
Widgets.Queue<T1, T2>
```

The non-generic type has arity zero. It does not hide the generic types, nor do they hide one another, because different arities coexist in a type group.

### Type groups

Some future grammar contexts will accept a *type_group* where they currently accept a type:

```diff
+ type_group
+     : type
+     | type_group_name
+     ;
```

A `type` produces a singleton type group. A `type_group_name` produces the set found by type-group-name lookup.

This distinction lets explicit types keep their ordinary meaning:

```csharp
Widgets.Queue<int>  // Singleton group containing Widgets.Queue<int>
int                 // Singleton group containing System.Int32
(int X, int Y)      // Singleton group containing the tuple type
```

When both grammar alternatives can recognize the same text, *type_group_name* is preferred. Thus `Widgets.Queue` denotes the whole family in a type-group context, even though it can also name the non-generic `Widgets.Queue` type. Without this preference the presence of the arity-zero type would accidentally prevent the generic siblings from participating.

This proposal does not change any existing grammar context to accept a type group. The first intended consumer is constructor inference. Type patterns may become another consumer later, but their binding behavior is not designed here.

### Lookup as a single operation

Type-group-name lookup is one lookup across arities, not several ordinary lookups whose results are combined afterward. This matters because ordinary name lookup uses hiding and stopping rules.

For an unqualified name, lookup starts in the nearest relevant declaration and works outward:

1. Method and containing-type type parameters are considered in their usual scopes.
2. Accessible nested types are considered from the innermost containing type outward.
3. Namespace scopes are considered from the innermost namespace through the global namespace. At each namespace scope, directly declared entities and aliases are considered before imported types.
4. Lookup stops at the first non-empty lexical or import result.

A non-empty result stops lookup even if it cannot ultimately form a type group. For example, a namespace, a type parameter, or an alias for a bound constructed type blocks types with the same name in outer scopes. This preserves the way ordinary lookup says that the nearer declaration owns the name; type-group lookup does not tunnel through an unusable result in search of a more convenient one.

```csharp
using Queue = Widgets.Queue<int>;
using OtherQueues;

new Queue(...); // The bound alias blocks OtherQueues.Queue and Queue<T>.
```

An alias for an ordinary non-generic type can contribute that arity-zero unbound type and therefore forms a singleton result. Namespace and `extern` aliases participate in lookup but cannot themselves be members of a type group.

Qualified lookup follows the same principle. For `N.I`, `N` is resolved first. If it is a namespace, directly contained accessible declarations named `I` are considered across arities. If it is a class, struct, or interface, accessible nested types named `I` are considered across that type and its bases. For `A::I`, `A` is resolved using the existing alias-qualification rules and must identify a namespace for `I` to produce types.

### One result per arity

Different arities naturally coexist:

```csharp
namespace Widgets
{
    class Queue { }
    class Queue<T> { }
    class Queue<T1, T2> { }
}
```

Two unrelated results of the same arity do not:

```csharp
using North;
using South;

namespace North { class Queue<T> { } }
namespace South { class Queue<T> { } }

// Lookup of Queue is ambiguous: both imported results have arity one.
```

That ambiguity is diagnosed while forming the type group. A later consumer should not use inference or applicability to choose which declaration owns a given name and arity. Doing so would turn a name-lookup ambiguity into consumer-dependent behavior.

The same-arity rule also gives nested-type hiding its expected shape:

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
        // Derived.Queue<T> hides Base.Queue<T>, but not Base.Queue.
    }
}
```

For each arity, the declaration in the most derived type wins. If several same-arity nested types are found and none is more derived than all the others, lookup is ambiguous. Types of other arities remain in the group.

### Relationship to method groups

The method-group analogy is deliberate:

- Lookup gathers same-name declarations without deciding which one a later operation will use.
- The group itself is not a value or a runtime object.
- A consumer applies its own inference, applicability, and selection rules after lookup.

The analogy is not identity. Method overloads can share a parameter count and remain together until overload resolution. Type groups instead contain at most one declaration per generic arity. Arity is part of a type declaration's identity in metadata and in ordinary type-name lookup, so ambiguity at a single arity remains a lookup error.

This shared boundary is the main point of the proposal. Constructor inference should not have one interpretation of aliases and nested hiding while a possible future type-pattern feature has another. Both can consume the same type group and differ only in what they do with it.

### Scope and non-goals

This proposal owns:

- the syntax recognized as a *type_group_name*;
- lookup of accessible same-name unbound types across arities;
- hiding, stopping, alias, and ambiguity behavior during that lookup; and
- formation of type groups from either a type or a type group name.

It intentionally does not:

- infer type arguments;
- infer or select an arity;
- inspect or choose constructors;
- define candidate applicability or betterness;
- change overload resolution; or
- define how type patterns would consume a type group.

Those decisions belong to each consumer. Constructor inference is the first such consumer and is tracked by [#9627](https://github.com/dotnet/csharplang/issues/9627).

## Compatibility

Type groups are infrastructure. On their own they change no programs, because no existing language construct is changed by this proposal to consume one.

A feature that changes a grammar position from `type` to `type_group` can change lookup and candidate behavior, and must assess that compatibility impact. In particular, a nearer declaration of any arity can stop the single cross-arity lookup where an old arity-specific lookup might have continued outward, and newly considered arities can introduce candidates or ambiguity. That impact belongs to the consuming feature, not to type-group formation in isolation.

## Drawbacks
[drawbacks]: #drawbacks

This adds language and specification terminology for an entity that cannot be used by itself. The rules are also more substantial than "find every type with this spelling": preserving accessibility, aliases, hiding, and lexical/import stopping is necessary to keep the result aligned with the rest of C# name lookup.

The method-group analogy is useful but imperfect because type groups reject same-arity ambiguity before a consumer examines candidates. The term may therefore suggest more overload-resolution behavior than the abstraction actually provides.

## Alternatives
[alternatives]: #alternatives

### Consumer-specific lookup

Constructor inference and any future consumer could each specify how to find same-name generic types. This avoids introducing a shared term, but it creates a long-term consistency risk in precisely the difficult areas: aliases, nested hiding, accessibility, ambiguity, and stopping at imports. A shared abstraction keeps consumers focused on inference and applicability.

### Choose an arity before lookup

A consumer could decide which arity to look up from syntax or context and then use ordinary type-name lookup. That is circular for inference scenarios: the evidence needed to choose the arity may only appear while inferring type arguments for candidates of that arity.

### Run ordinary lookup once per arity

A consumer could perform independent lookups for arity zero, one, two, and so on, then combine the results. Besides requiring an arbitrary search bound, this gets hiding wrong. Ordinary lookup for one arity can continue past a nearer declaration of another arity, whereas the proposed abstraction deliberately performs one name lookup and stops at the first non-empty result across all arities.

### Prefer the non-generic type

We could recognize a type group name only when ordinary type lookup fails. That would make the common `Queue`/`Queue<T>` family behave differently depending on whether its arity-zero member exists. Constructor inference would then be unable to consider `Queue<T>` whenever `Queue` was also declared, defeating a central use case. The grammar therefore prefers *type_group_name* in a type-group context.

### Defer same-arity ambiguity to consumers

All same-name results could remain in the group and let inference or applicability distinguish them. This would make basic name identity depend on the consuming feature and could cause constructors and patterns to resolve the same name differently. The proposal instead requires at most one declaration per arity.

## Open questions
[open]: #open-questions

### Is "type group" durable user-facing terminology?

The concept is useful in language design and specification because it names the shared boundary between lookup and later binding. It is less clear whether diagnostics, APIs, and documentation should expose *type group* as a user-facing term, or whether it should remain primarily a language/specification abstraction explained through the more familiar method-group analogy.

### Which champion issue owns the shared infrastructure?

There is no dedicated champion issue today. Since constructor inference is the first consumer, [#9627](https://github.com/dotnet/csharplang/issues/9627) can temporarily track the work, but a dedicated issue may better represent the abstraction if it gains additional consumers.
