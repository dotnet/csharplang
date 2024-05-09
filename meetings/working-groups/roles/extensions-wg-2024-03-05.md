# Extensions WG 2024-03-05

## finding extension members on underlying type

Just like extension methods on a base type are eligible when given an instance of the derived type,
extension members on the underlying type should be eligible when given an extension type or value.  
The extension member lookup rule was updated to look at the underlying type when given an extension type.

```
var x = E1.Member;
System.Console.Write(x);

class C { }

extension E1 for C { }

implicit extension E2 for C
{
    public static string Member = "ran";
}
```

Decision: that seems fine.

Note: like other extension lookups, that would not apply on Simple Names. For example:
```
class C { }

extension E1 for C 
{ 
    void M()
    {
        var x = Member; // error
        string s = Member; // error
    }
}

implicit extension E2 for C
{
    public static string Member = "";
}
```

## merging extension methods and extension type members within same scope

LDM decided that we should merge extension methods and extension members within the same scope.
But how should we deal with ambiguities, either of the same kind or of different kinds?

Decision: let's be strict on merging different kinds of members (produce ambiguity).  
Proposal: Consider doing a breaking change or warning wave warning (would be error for extension case)?

## preference for more specific extension members

We discussed two ways of doing this:
1. stop once we find something on a more specific extension? ("shadowing" type of rule)
2. ambiguity when different kinds are found, disambiguate within one kind when overload resolution involved? (no shadowing across different kinds)

Decision: we'll pursue proposal 2.

## Follow-up investigations

1. Look at how lookup within a type works today (not inside an interface)
  - for example, derived defines a field and base defines a method
  - for example, derived defines a method and a base defines a field
2. find existing merging logic for different kinds of members
3. find existing logic for overload resolution picking between extension methods based on receiver
4. need to understand how the field shadowing works in the following example:
```
System.Console.WriteLine(I4.F); 

interface I1
{
    static int F = 1;
}

interface I2 : I1 
{
    static int F = 2;
}

interface I3 : I1 {}

interface I4 : I3, I2 {}
```

## brainstorm on inapplicable members hiding applicable ones

We did not cover this yet.

```
var x = C.Member; // error: member lookup finds C.Member (method group) and lacks type arguments to apply to that match

class C 
{
    public static void Member<T>() { }
}

implicit extension E for C
{
    public static int Member = 42;
}
```
