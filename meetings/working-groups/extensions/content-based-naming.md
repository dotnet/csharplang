# Content Based Naming

## Terminology

An extension member in source will result in two members in metadata:

1. The implementation member: this is the method generated into the type which contains the extension block declaration and used at runtime.
2. The declaration member: this is the method generated for the purposes of the compiler to support build and design time capabilities of extension members.

The declaration type is the type that contains the declaration members.

The marker method is the method in a declaration type which is used to recover the original C# declaration semantics of the extension block parameter.

## Summary

The current metadata strategy for declaration members relies on ordinals to create declaration types from extension blocks. The use of ordinals causes friction in a few parts of the C# ecosystem:

- Edit and Continue: any use of ordinals tied to source ordering means that adding or removing elements can create unnecessary, and possibly unresolvable, conflicts during ENC operations. Natural operations like adding a new extension block in the middle of two others can lead to renaming of existing blocks which is difficult to reconcile.
- Public API Tracking: the declaration methods are necessarily generated as `public` and there are many tools in the .NET ecosystem that track `public` method usage. The current design means moving extension blocks around in source can observably change the set of `public` API in an assembly. Shipping with this design would mean this ecosystem of tools would need to change to treat this `public` API as _special_ in some way.
- CREF generate an observable reference to declaration methods via `inheritdoc`. This means source reordering can break any place CREFs are treated as a durable item.

This proposal wants to revamp our approach to declaration type and method metadata such that the following goals are achieved:

1. The declaration type and set of declaration and marker that will be generated for a given extension member can be determined by looking at solely that extension member. There is no need to consider other members in the same extension block, `partial` declarations in the containing type, etc ...
2. Declaration types and methods are treated like `public`. This proposal accepts that declaration types and members will be observable by design and build time processes. This means they need to have, as much as possible, the same characteristics as other `public` API. Specifically the following types of actions should **not** break runtime binary compatibility for `public` members in a declaration type:
    1. Reordering extension blocks or members in source code
    2. Changing C# specific aspects of the type like adding / removing nullable annotations, changing tuple names, etc ...
3. The C# compiler should be able to fully rehydrate the original extension paramater from metadata. This means for each declaration member the compiler can recreate the _exact_ type name, ref-ness, type parameter names, parameter name, etc... that was written in source.

This will be achieved by moving to a content based naming scheme for declaration types and members.

## Detailed Design

This proposal relies heavily on using content based naming for declaration types and members.  The proposal is going to focus on what items are included in the content name, it will not discuss the actual name produced. That is because the specific hashing algorithm used will be an implementation detail of the compiler.

For an extension block the content name of the declaration type will be determined by using the following parts of the extension block declaration:

- The fully qualified CLR name of the type. This will include namespaces, type constraints, named constraints (like `new`).
    - Type parameter names will be normalized to `T0`, `T1`, etc ... based on the order they appear in the type declaration. This means an extension block with a type parameter `Dictionary<TKey, TValue>` will result in the fully qualified name being `System.Collections.Generic.Dictionary<T0, T1>`.
    - The fully qualified name will not include the containing assembly. It is common for types to be moved between assemblies and that should not break the public API.
- Constraints will included and sorted such that reordering them in source code does not change the name. Specifically:
    - Type parameter constraints will be listed in declaration order. The constraints for the Nth type parameter will occur before the Nth+1 type parameter.
    - Base type and interface constraints will be sorted by comparing the full names ordinally
    - Non-type constraints will be sorted by comparing the C# text ordinally
- This will not include any C# isms like tuple names, nullability, etc ...

This will achieve the goal that the name of declaration types will only change when the underlying CLR type of an extension block parameter changes. Any change to C# parts of the type such as adding nullable annotations or tuple names will impact the declaration type name.

The marker method for an extension block will be a `private` method in the declaration type. This method will have a single parameter which mirrors the extension block `this` parameter. Specifically it will have the same attributes, ref declaration, parameter name and C# type. The name of this marker method will be a content name that is determined by using the following parts of the extension block parameter declaration:

- The fully qualified C# name of the type. This will include items like nullable annotations, tuple names, etc ... The constraints will have the same ordering as CLR types for the marker type name.
- The name of the extension `this` parameter
- The ref-ness of the extension `this` parameter
- The fully qualified name + attribute arguments for any attributes applied to the extension `this` parameter.

For every member in an extension block, the generated declaration member will have an attribute which contains the name of the marker method that represents the original extension `this` parameter. This allows the compiler to fully rehydrate the C# extension `this` parameter for any declaration member.

Here is an example of how this approach would look in real code:

```cs
class E
{
    extension<T>(IEnumerable<T> source)
    {
        public bool IsEmpty => !source.Any();
        public int Count => sourec.Count();
    }

    extension<T>(ref IEnumerable<T?> p)
    {
        public bool AnyNull => p.Any(x => x == null);
        public bool NullToEmpty() => this ??= [];
    }

    extension<T>(IEnumerable<U> source)
        where T : IEquatable<U>
    {
        public bool IsPresent(U value) => source.Any(x => x.Equals(value));
    }
}
```

This would generate the following:

```cs
class E
{
    public class <>ContentName_For_IEnumerable_T<T>
    {
        private void <>ContentName1(IEnumerable<T> source) { }

        private void <>ContentName2(ref IEnumerable<T?> p) { }

        [MarkerMethodName("ContentName1")]
        public bool IsEmpty => throw null!;

        [MarkerMethodName("ContentName1")]
        public int Count => throw null!;
    
        [MarkerMethodName("ContentName2")]
        public bool AnyNull => throw null!;
    
        [MarkerMethodName("ContentName2")]
        public bool NullToEmpty() => throw null!;
    }
  
    public class <>ContentName_For_IEnumerable_T_With_Constraint<U>
       where U : IEquatable<U>
    {
        private void <>ContentName3(IEnumerable<U> source) { }

        [MarkerMethodName("ContentName3")]
        public static bool IsPresent(U value) => throw null!;
    }
}
```

This approach will result in stable names for our declaration types and members that will make it much more consumable for the existing C# ecosystem.

The content hash algorithm will be left as an implementation detail to the compiler. The _recommendation_ is picking a hash that has the following properties:

1. Resilient to collisions from common elements in C# type names.
2. Has a bounded length on generated names as unbounded names at scale could negatively contribute to metadata limitations on names.

The only _requirement_ of the hash is that it cannot be a cryptographic hash. Because this is a non-cryptographic hash the compiler will be responsible for doing collision detection. Specifically:

- When the extension block parameter type for two extension blocks have different CLR types but produce the same declaration type name an error must be produced.
- When the extension block parameter type for two extension blocks have different C# types but produce the same marker method name an error must be produced.

This design will result in the following restrictions for extension blocks:

- All extension blocks that map to the same declaration type must have type parameters with the same name. This mirrors the existing restrictions that we have for `partial` types.
- All extension blocks which generate a declaration type name X must refer to the same CLR type. Essentially there cannot be two extension blocks in the same container which map to the same declaration type but different CLR types for the extension parameter. This is not resolvable with `extern alias`. Instead such extension block declarations must be put into different containing types.
- Changing constraints on an extension block will result in breaking changes for existing CREF in the ecosystem

## Alternatives

### Reduce scope of constraint breaking changes to non-methods

The biggest challenge with constraints and breaking changes is that for member types like properties the only place constraints can exist is on the type. That means changing constraints means that constraints on the containing type must change. That, combined with other requirements of the design, force the name of the marker type to change as constraints change.

This problem does not _inherently_ exist for methods. Those can declare their own type parameters and hence be independent of the type parameter / constraints on the containing type.

This means that we could do the following to make constraint changes for extension methods only non-breaking. Extension blocks would generate into two marker types:

1. A declaration type which contained all extension members that were methods. This declaration type would be non-generic. Type parameters on the original extension type would be copied to each declaration method.
2. A declaration type which contained all extension members that were not methods. This declaration type would be as previously described in this document.

This design has a few downsides:

- Significantly increases the complexity of the compiler implementation.
- Yes it does reduce scope of breaking changes but at the expense of creating a decoder ring for understanding the subtleties of what does / doesn't break.

The working group does not feel the extra complexity is justified by the limited relief of CREF breaking changes.

### Emit everything as methods in the declaration types

As mentioned above when dealing with methods it's easier to avoid breaking changes around constraints. One strategy could be to simply emit everything in an extension block as a method and move all the type parameters to these methods.

For example: when emitting the declaration member for a property named `Example` declared in an extension block we could find a strategy where:

1. Pick a naming scheme that identifies it as property like prefix with `<>Property_Example`
2. Emit the accessors with a naming scheme like `<>Property_Example_get`

The exact details would be involved but we could identify a naming scheme that let us map back and forth to the original property name. It would increase the complexity of the compiler implementation but it seems reasonable we could create a name mapping scheme here.

This design though would require us to generate _invalid_ metadata. For example we'd need to map attributes from the property declaration to the generated method. In the case these were marked as `AttributeTargets.Property` that would be invalid. This is a case where the binary would execute as the CLR does not verify attribute targets are correct but it is likely that it would cause friction in the ecosystem for tools that assume such target are correct.

That is the biggest issue with this approach. There are several aspects like this where there is no clean mapping. Also there a lot of items like this were are likely missing. We'd need to go through every aspect of metadata and language, find every part that is specific to properties and rationalize them with this approach. This is why the working group discarded this idea (previously and in the context of this specific discussion).

### Remove the matching type parameter name restriction

The design could be altered such that the restriction on all extension blocks that map to the same marker type must have type parameters with the same name is removed. This would roughly mean that the code generation would do the following:

1. Type parameter names would be normalized to `T0`, `T1`, etc ... in the marker type name.
2. Type parameter names would be part of the source type marker method content hash.
3. An attribute would be generated on the source type method that contains the original type parameter names.

This would allow us to remove this restriction but at the extra cost to the compiler code generation strategy.

## Miscellaneous

### Why not use a Cryptographic Hash?

The generated names must be the same through the lifetime of this feature in C#. This is not compatible with any use of cryptography which must be agile. Specifically the compiler must assume there is a future where SHA-256, the current defacto crypto algorithm, will be broken and banned in applications. That would leave the compiler in a place where it's using a banned cryptographic algorithm. This is just not compatible with our current security posture.

The only downside to using a non-cryptographic hashing algorithm is that the compiler cannot take uniqueness for granted. It instead must be verified during compilation time to ensure there are no accidental collisions which is straight forward to implement in the compiler.

## Open Questions

### Type Parameter Names

This design normalizes type parameter names to `T0`, `T1`, etc ... The original names are encoded in an attribute on the source type method. Need to validate from the compiler team if this is a workable solution for rehydrating the original type parameter names. If this is not viable then we will need to consider adding the following restriction:

- All extension blocks that map to the same declaration type must have type parameters with the same name.

At a glance this may seem like an unreasonable restriction but there is precedence as this is what we require for `partial` types today.

### Categorizing the breaking change

Changes to the source code that result in only changes to the declaration types are a new kind of breaking change.  Let's consider concretely a case where a constraint on a type parameter is removed.

```cs
// Before
class E
{
    extension<T>(C<T> source)
        where T : IDisposable
    {
        public void M() { }
    }
}

// After
class E
{
    extension<T>(C<T> source)
    {
        public void M() { }
    }
}
```

This source change will only result in a change to the declaration types. That means it is **not** a runtime binary breaking change as that would bind against the implementation methods. This change to the declaration type is also not a source breaking change (yes modifying constraints can break source but there is nothing about the declaration types / methods itself that are used in source).

This is a breaking change for the following items:

- CREF: changing the declaration type / method can break generated CREF in the wild. These would be fixed upon recompilation against the new assembly.
- Design Time: the Roslyn API can be used to observe the declaration types, CREF being one case.

Time should be taken to better outline and categorize these type of breaks so teams like .NET libraries can make informed decisions about changing aspects of declaration types between releases.

### The MarkerMethodName name

This proposal uses `[MarkerMethodName]` as the attribute to map between the declaration method and its marker method. This name is a place holder and likely a better name should be considered.
