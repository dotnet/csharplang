
# Extensions as static types

* [x] Proposed
* [ ] Prototype: Not Started
* [ ] Implementation: Not Started
* [ ] Specification: Not Started

## Summary
[summary]: #summary

Disallow use of an extension as an instance type. Just like static classes this means that it cannot be the type of a value or variable, and cannot be a type parameter.

## Motivation
[motivation]: #motivation

A large part of the complexity of the roles and extensions pair of features comes from allowing them as types of values. For roles, that is the core of the feature; an indispensable part of the design. For extensions, however, their "typeness" is a more peripheral aspect, mainly to do with disambiguation. If we can live without those aspects of the extension feature, we can ship it faster and with less implementation complexity and risk. This does not prevent us from adding roles later, and in the process "upgrading" extensions to also be instance types. It allows us to further stratify the work across multiple waves of effort.

## Detailed design
[design]: #detailed-design

Separate out the design for extensions from that for roles. Disallow the use of extension types as the type of values and variables, including in variable and member declarations, cast expressions, and as type arguments.

Inside extension declarations, change the type of `this` to be the underlying type rather than the extension type. Other members of the extension can still be accessed on (implicit or explicit) `this`, as they show up as extension members on the underlying type.

## Drawbacks
[drawbacks]: #drawbacks

### The type of `this` in extension declarations

In the current design, the type of `this` in an extension declaration is the extension type itself. This enables inheritance-like lookup behavior, where the members of the extension take precedence over the members of the underlying type. With `this` having the underlying type, that would no longer be the case - members of the underlying type would win over extension members, even ones from the enclosing declaration.

This does not seem like a big loss in practice. Why would an extension declare a member that the underlying type would hide? Such an extension member would be effectively unusable, given the other restrictions of this proposal. In fact, such a declaration might warrant a warning:

``` c#
public extension StringExtensions for string
{
    public string Length => ...; // Useless - warning?
    public bool IsUtf8 => ... Length ...; // Would bind to string.Length
}
```

It is still the case that the extension's members would compete on equal terms with other extensions and could clash with them. This is a special case of the more general ambiguity issue, that we will address next. 

In this particular case, ambiguities would be quite rare. If the other extension is imported with a `using` it would lose due to existing "closeness" rules. If it is defined on a base type, it would lose due to existing overload resolution rules.

If we still think it is a problem, we could consider an additional closeness rule that an extension is closer than others inside its own declaration.

``` c#
public extension E1 for string
{
    public void M() => ...;
}
extension E2 for string
{
    public void M() => ...;
    public void P => ... M() ...; // Ambiguity? Closeness rule?
}
```

It would likely be a breaking change if at a later point (when extensions were allowed as instance types) we changed the type of `this` to the extension type. 

### Disambiguation

The other place where the type-ness of extensions plays a part in the current design is in disambiguation between members of two imported extensions.

``` c#
using E1; // Brings in void M() on string;
using E2; // Also brings in void M() on string;

"Hello".M(); // Ambiguous
((E1)"Hello").M(); // No longer allowed
```

Given current rules, ambiguities like this would have to be handled by playing tricks with using clauses. E.g. putting `using` inside vs outside of the namespace (the one inside is nearer). Or defining a helper extension member in a separate file that only imports one of the extensions, and simply redirects to it:

``` c#
// OtherFile.cs
using E1;

extension HelperExtensions for string
{
    public void E1_M() => M();
}
```

We know from extension methods that ambiguities are rare, but do occur. If the above is not satisfactory, we could recognize very specific patterns in code (such as `((E1)"Hello").M()`) or consider new syntax for disambiguation.

## Alternatives
[alternatives]: #alternatives

These possible supplementary mitigating features were mentioned in the Drawbacks section:

- A warning when an extension declares a member that would be hidden by a member of the underlying type
- An additional closeness rule preferring members of a given extension within the declaration of that extension
- Syntax or special rules to help with disambiguation between extension members

## Unresolved questions
[unresolved]: #unresolved-questions

## Design meetings

