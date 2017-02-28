# Allow currently mandatory, but syntactically redundant, braces to be optional

* [ ] Proposal: Submitted as pull request.
* [ ] Prototype: Not Started.
* [ ] Implementation: Not Started.
* [ ] Specification: Not Started.

## Summary
[summary]: #summary

There are a few places within the C# syntax where braces must be used, but where they could be made optional:

1. Single namespace in a file declarations
2. Empty interface declarations
3. Constructors that just call `this` or `base`


## Motivation
[motivation]: #motivation

This feature causes no functional changes to the language and simply reduces "syntax noise".

## Detailed design
[design]: #detailed-design

### Single namespace in a file declarations ###

For many C# files, only one namespace is ever declared. As a result, the need to add `{}`'s after that namespace declaration results in an extra level of indentation for every line from that point on, to the closing `}` in the file.

Two possible solutions would be:
1. Allow the namespace to be a statement, rather than a block,
2. Allow the namespace to be declared as part of the type name.

Allowing the namespace to be a statement, rather than a block:
````cs
using System;
namespace X.Y;

class Z { ... }
````

Allowing the namespace to be declared as part of the type name:
````cs
using System;

class X.Y.Z { .... }
````

### Empty interface declarations ###
Sometimes, an interface is used as a means of specifying commonality between types, without any members being imposed on those implementations (commonly called marker interfaces):
````cs
interface ISomething {}
````
The `{}` aren't required here and could be replaced with `;`:
````cs
interface ISomething;
````
This feature would have the added advantage of making interfaces consistent with [the proposed syntax for records](https://github.com/dotnet/csharplang/blob/master/proposals/records.md#record-type-declarations), which would result in the side-effect of the following being valid class and struct definitions:
````cs
class C();
struct S();
````

### Constructors that just call `this` or `base` ###
It is common to have a set of constructors that just call each other with eg default parameters. Again, it is necessary to put `{}` after the `base` or `this` call:
````cs
class C
{
    private readonly int _field;

    public C(int field) => _field = field;

    public C() : this(0) {}
}
````
The `{}` in the second constructor aren't required here and could be replaced with `;`:
````cs
class C
{
    private readonly int _field;

    public C(int field) => _field = field;

    public C() : this(0);
}
````

## Drawbacks
[drawbacks]: #drawbacks

**Single namespace in a file declarations**</br>
Potential problems exist with `namespace` just being a statement, releated to the scope of `using` statements:
1. It could be mandated that `namespace` being the first non-trivia item in the file, but this then forces all `using` statements to either be treated as defined outside or inside the namespace scope. This could cause problems for edge cases where the scope matters.
2. The `namespace` statement could appear anywhere prior to the type declaration. This then could cause readability issues with it being lost amongst those statements.

There are no identified drawbacks with making the namespace part of the type name.

**Empty interface declarations**</br>
The use of marker interfaces conflicts with [official design guidelines](https://msdn.microsoft.com/en-us/library/ms229022.aspx), which recommends using attributes instead. Removing the need to specify the `{}` after an empty interface could be seen as a contradictory, implicit endorsement of marker interfaces.

## Alternatives
[alternatives]: #alternatives

No other alternatives identified.

## Unresolved questions
[unresolved]: #unresolved-questions

- [ ] Is the design guidelines' advice to use attributes instead of marker interfaces still valid? One of the main arguments centres on inheritance, which has numerous problems of its own. The argument (that sub-types may not want to support the attribute) has more than of a whiff of Liskov substitution principle violation about it, too. Further there is a large performance overhead with using attributes, rather than interfaces. Both weaken the argument against marker interfaces.

## Design meetings

None, yet.


