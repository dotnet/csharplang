# Extension member disambiguation

It's possible for separate extensions to add identical extension members to the same underlying type. In those cases it's fairly tricky to pick one over the other. We've resolved to add a syntax to help with this, but we haven't decided what that syntax should be.

Here are some requirements for a disambiguation syntax:

- Usable with all kinds of extension members, not just those that use member access (`a.b`)
- Specifies the static class where the desired extension member is declared

The easiest type of syntax to imagine is one where a receiver or operand expression is augmented with a "start lookup here" annotation of some sort.

An extension disambiguation syntax will also provide a way to target extension members that are otherwise hidden, e.g. by regular members or by other extension members that are "closer".

### Scope

Extension members are not the only place in the language where it would be helpful to be able to specify where to begin lookup. For instance, interface members cannot be directly invoked on values of classes and structs that implement the interface, and, especially for structs, casting the receiver has impact on both semantics (copying) and performance (allocation). Again, workarounds are tedious.

We should keep these broader scenarios in mind when selecting a syntax, even if we do not implement them yet beyond extensions.

### Existing or new

Some features today, such as casts or `as`, come pretty close to addressing the problem. Could we just bend them to this new scenario? Risks when using an existing syntax include breaking changes and user confusion. But new syntax is a steeper price to pay.

## Candidates

Shown here with extension property and extension operator examples.

- **Cast**: `((MyExtensions)e).Prop`, `((MyExtensions)e1) + e2`. Existing syntax, may clash with existing uses or cause breaks. May seem odd since `MyExtensions` isn't really a type. 
- **As-operator**: `(e as MyExtensions).Prop`, `(e1 as MyExtensions) + e2`. Existing syntax, may clash with existing uses or cause breaks. May seem odd since `MyExtensions` isn't really a type. 
- **Invocation syntax**: `MyExtensions(e).Prop`, `MyExtensions(e1) + e2`. Existing syntax, but less likely to break. May clash with proposed feature of implicit `new` in object construction. May be a confusing syntax overload. But very short and can have high precedence (primary expression) minimizing extraneous parentheses.
- **At-operator**: `(e at MyExtensions).Prop`, `(e1 at MyExtensions) + e2`. New syntax, analogous to `as`. Possibly slightly breaking in corner cases.
- **@-operator**: `(e @ MyExtensions).Prop`, `(e1 @ MyExtensions) + e2`. New syntax, glyph version of `at`. Possibly slightly breaking in corner cases.
- ...

Let's get more proposals on the table and discuss pros and cons.