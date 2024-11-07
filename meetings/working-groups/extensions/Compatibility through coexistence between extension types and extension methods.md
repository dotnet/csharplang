# Compatibility through coexistence between extension types and extension methods

This proposal approaches compatibility between classic extension methods and new extension types by emphasizing smooth coexistence and avoiding arbitrary behavior differences.

* **Goal:** Bring _expressiveness_ of new extension methods close enough to that of old ones that new code won't need to use classic extension method syntax
* **Goal:** Avoid arbitrary _behavior differences_ that may be jarring or surprising
* **Goal:** Allow smooth _coexistence_ between new and old extension methods for the same underlying type
* **Non-goal:** Allow all existing extension methods to be faithfully ported to the new paradigm
* **Non-goal:** Allow reliance on details of lowered format for new extension members

The dual purpose of extension types is to allow other member kinds and also raise the level of abstraction. The two are linked: other member kinds are possible - and pleasant - because the declaration syntax is detached from the generated format. We should protect that by not exposing the lowered shape of declarations at the language level.

The basis of this proposal is the [extension proposal](https://github.com/dotnet/csharplang/blob/main/proposals/extensions.md) resulting from [separating extensions from roles](https://github.com/dotnet/csharplang/blob/1e8255a438517bc3ad067c726c28cfa20cb60f1e/meetings/working-groups/extensions/rename-to-roles-and-extensions.md) and [dropping their ability to act as instance types](https://github.com/dotnet/csharplang/blob/1e8255a438517bc3ad067c726c28cfa20cb60f1e/meetings/working-groups/extensions/extensions-as-static-types.md).

We'll address the three goals in turn. At the end is a brief summary of the features proposed throughout.

## Expressiveness

In classic extension methods, the fact that the receiver is expressed as a parameter enables a lot of variation in how it is specified, some of which is useful, much of which isn't.

In extension types, the receiver is specified as a shared underlying type for all the members. This proposal takes the viewpoint that where variations allowed by old extension methods are useful, the new syntax should enable them to be specified at the member level. This way they won't be split between multiple extension types.

Not all the variations are worth carrying forward. In the following, some are kept, and some are discarded based on perceived prevalence and usefulness.

### Attributes

Only about .7% of extension methods have attributes on the receiver, and an overwhelming fraction (95%) of those are nullability-related. However, the next section on nullability may add to that.

We could add a new attribute target `this`, so that such attributes can be placed on the member declaration:

``` c#
[this:NotNullWhen(false)] public bool IsNullOrEmpty() { ... }
```

### Nullability

About 1.5% of extension methods have a nullable receiver type. Some fraction of those are nullable value types. Nullable reference types are useful in this position for extension methods like `IsNullOrEmpty` that are explicitly null-safe.

This feels like a useful scenario, and some core infrastructural extension methods rely on it. However, it is also quite rare and probably doesn't warrant dedicated language syntax. Instead, we can use attributes in conjunction with the `this` attribute target proposed above:

``` c#
[this:AllowNull][this:NotNullWhen(false)] public bool IsNullOrEmpty() { ... }
```

Using attributes to refine the nullability profile of members is already quite common, and this doesn't seem out of place.

### Ref and readonly

Extension method receivers are usually value parameters, but if the receiver type is a value type they can also be declared as `ref`, `in` or `ref readonly` parameters. The latter was eventually allowed in C# so that value types can be mutated by extension methods as well as passed to them more efficiently.

Extension types in their current design take a different approach: Reference type receivers are _always_ passed by value and value type receivers are _always_ passed by reference. (If the receiver type is an unconstrained type parameter, the decision is made at runtime!) This closely emulates the behavior of `this` inside instance member declarations, which extension members syntactically imitate, leading to the least surprising semantics of `this`.

The new design does not allow for passing the reference receivers by reference or value receivers by value. Classic extension methods do not allow reference types to be passed by reference either, so that is no loss of expressiveness. There hasn't been a user ask for this.

Passing value receivers by value, however, is common in classic extension methods. It's what happens by default, and passing by reference wasn't even allowed in C# for several versions. Any extension method that doesn't actively need to mutate the receiver is likely to take its receiver by value, and only about 1% of extension methods in fact take the receiver by reference.

The main benefit of choosing to pass a value receiver by value is that it protects the original from being mutated. The proposal here is to instead satisfy that desire in a different way: Members of extension types for value types can be declared `readonly`, just as members of a struct can be declared `readonly`. 

``` c#
public extension TupleExtensions for (int x, int y)
{
    public readonly int Sum() => x + y; // `this` is readonly and cannot me mutated
}
```

Just as in struct declarations, the extension type itself could be declared `readonly` as a shorthand for declaring `readonly` on all members.

A `readonly` annotated member will have its receiver passed as `ref readonly`, which protects it from mutation. It differs semantically from pass-by-value only in subtle ways that aren't likely to be important in usage scenarios. However, unlike pass-by-value, it prevents mutation of `this`, avoiding undetected bugs where mutation is accidentally applied to a copy and changes are lost.

### Disambiguation

Classic extension methods allow disambiguation simply by calling them as a static method. Extension types do not allow this, because from a user perspective there *are* no static methods.

When extensions were still intended to be usable as instance types, disambiguation could happen simply by casting the receiver to the extension type. This option is no longer open - or at least it would need special context-specific semantics.

Disambiguation for certain static extension members such as methods and properties is likely to be simple: Just dot off the extension type instead of the underlying type:

``` c#
MyStringExtensions.Format(...); // Instead of string.Format(...)
```

For instance members and e.g. operators it is still possible to disambiguate through other means, but they are not elegant, and the LDM has agreed that we should work towards a good disambiguation solution.

### Grouping

Classic extension methods can - and often do - co-inhabit the same static class even when they have different receiver types. Extension types do not offer the same possibility, since the receiver type is tied to the extension type.

While this is a difference to get used to, it does not seem to be a limit to expressiveness. It may lead to more type declarations, but probably not excessively so. This proposal does not attempt to address it.

## Behavior differences

### Overload resolution and type inference

The current proposal for extension types does overload resolution in two stages: First, suitable extension types are found, inferring any type arguments to the extension type in the process. Then, type inference and overload resolution is performed on eligible candidate members in those extensions. This two-stage approach seems intuitively right: the first stage identifies the receivers, and once determined, the second stage works exactly the same as would an instance method call.

However, this differs from how overload resolution and type inference work on classic extensions methods, where all type parameters are already on the method, and the receiver participates in one big round of type inference alongside other arguments to the underlying static method.

Most behavioral differences currently stem from using a weaker inference approach in the first phase of extension type resolution. At minimum this should be replaced with full type inference. 

This still leaves scenarios where, with classic extension methods, non-receiver arguments can impact type inference for type parameters that occur in the receiver type. However, those situations are likely to be exceedingly rare in practice.

There's a proposal for how to do the one-phase approach with extension types. Embracing that would allow overload resolution to mix old and new extension methods in the same candidate sets, something which we rely upon in the Coexistence section below. At the same time, with behavior differences to the more intuitive two-phase approach being so rare, it is unlikely to lead to user confusion.

### Refness

As already mentioned, extension types pass the receiver using the same semantics - by-value, `ref` or `ref readonly` - as the `this` parameter in corresponding instance members. This is a deliberate behavior deviation from that of extension methods, intended to minimize behavioral differences from the instance members that extension members are syntactically imitating.

## Coexistence

This proposal specifically does not attempt to preserve semantics from old to new syntax. This means that library authors concerned with compatibility may need to keep their classic extension methods around for a while, possibly forever. In recognition of this, we should strive to make coexistence of new and old extensions friction-free.

There are several points on the dial that a library author may aim for; possibly going through them from one to the next over time in a deprecation process. Staying at any one of these stages forever is a fully supported and encouraged choice, depending on the library author's priorities.

1. *Keep old extension methods, add new extension members:* You should not ever need to write another classic extension method, and if you already have some, that should not hamper the ability to seamlessly add new ones in the new style. This should give library users full source and binary compatibility.

2. *Migrate old extension methods, keep most back compat:* Most old extension methods should be able to migrate to new ones in ways that are fully source compatible - or very close to it - as long as they are used *as* extension methods. If the old static methods are kept *without* the `this` modifier and made to redirect to the new ones, then binary compatibility is achieved, and invocations *as* static methods stay source compatible as well. 

3. *Deprecate old extension methods:* The old static methods - formerly extension methods - could be deprecated. This would drive users to switch to new disambiguation syntax, while still maintaining binary compatibility.

4. *Remove old extension methods:* This would force recompilation of code that was compiled against the old extension methods.

Let's look at what it takes to allow this.

### Precedence

If either kind of extension method - old or new - is given precedence in overload resolution then it would allow old and new versions of the same extension method to coexist without giving rise to ambiguity. However, this could lead to surprising results where less suitable members of the preferred kind are chosen.

Allowing two simultaneous versions of the same extension method is not a goal. While it could ensure slightly higher source compatibility in rare cases, the interactions are confusing, and there's a risk of the two versions drifting apart.

The proposal here is to have new and old extension methods share the same precedence. For that to work, we need to use the one-phase type inference and overload resolution approach described earlier.

### Type names

Static classes with extension methods often already have the "good" names. It would be great if new non-generic extension types could optionally use the same type name. The easiest way is for the two to just *be* the same type. There are some different ways you could imagine allowing this in syntax; the simplest and most readable would probably be to allow both to be declared, and then implicitly merge them as if they were declared `partial`.

## Summary of proposed decisions

1. New `this:` target for attributes
2. Nullable reference receivers expressed with `[this:AllowNull]` attribute
3. Allow `readonly` modifier on extension members and extension types with a value-type underlying type
4. Disambiguation mechanism still to be designed
5. Use one-phase approach to type inference and overload resolution of extension type members
6. Share overload resolution precedence between old and new extension methods
7. Allow non-generic top-level extension type and static class of the same name to implicitly merge
