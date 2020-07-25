# Nullable constructor analysis

This proposal is intended to resolve a few of the outstanding problems with nullable constructor analysis.

## How it works currently

Nullable analysis of constructors works by essentially running a definite assignment pass and reporting a warning if a constructor does not initialize a non-nullable reference type member (for example: a field, auto-property, or field-like event) in all code paths. The constructor is otherwise treated like an ordinary method for analysis purposes. This approach comes with a few problems.

First is that the initial flow state of members is not accurate:

```cs
public class C
{
    public string Prop { get; set; }
    public C() // we get no "uninitialized member" warning, as expected
    {
        Prop.ToString(); // unexpectedly, there is no "possible null receiver" warning here
        Prop = "";
    }
}
```

Another is that assertions/'throw' statements do not prevent field initialization warnings:

```cs
public class C
{
    public string Prop { get; set; }
    public C() // unexpected warning: 'Prop' is not initialized
    {
        Init();

        if (Prop is null)
        {
            throw new Exception();
        }
    }

    void Init()
    {
        Prop = "some default";
    }
}
```

## An alternative approach

We can address this by instead taking an approach similar to `[MemberNotNull]` analysis, where fields are marked maybe-null and a warning is given if we ever exit the method when a field is still in a maybe-null state. We can do this by introducing the following rules:

**A constructor on a reference type without a `: this(...)` initializer** has an initial nullable flow state determined by:
- Initializing base type members to their declared state, since we expect the base constructor to initialize the base members.
- Then initializing all applicable members in the type to the state given by assigning a `default` literal to the member. A member is applicable if it is instance and the constructor being analyzed is instance, or if the member is static and the constructor being analyzed is static. 
  - We expect the `default` literal to yield a `NotNull` state for non-nullable value types, a `MaybeNull` state for reference types or nullable value types, and a `MaybeDefault` state for unconstrained generics.
- Then visiting the initializers for the applicable members, updating the flow state accordingly.
  - This allows some non-nullable reference members to be initialized using a field/property initializer, and others to be initialized within the constructor.
  - The expectation is that the compiler will flow-analyze and report diagnostics on the initializers once, then copy the resulting flow state as the initial state for each constructor which does not have a `: this(...)` initializer.

**A constructor on a value type with a `: this()` initializer** (referencing the default value type constructor) has an initial flow state given by initializing all applicable members to the state given by assigning a `default` literal to the member.

**A constructor on a value type or a constructor with a `: this(...)` initializer** has the same initial nullable flow state as an ordinary method: members have an initial state based on the declared annotations and nullability attributes. In the case of value types, we expect definite assignment analysis to provide the desired level of safety. This is the same as the existing behavior.

**At each explicit or implicit 'return' in a constructor**, we give a warning for each member whose flow state is incompatible with its annotations and nullability attributes. A reasonable proxy for this is: if assigning the member to itself at the return point would produce a nullability warning, then a nullability warning will be produced at the return point.

It's possible this could result in a lot of warnings for the same members in some scenarios. As a "stretch goal" I think we should consider the following "optimizations":
- If a member has an incompatible flow state at all return points in an applicable constructor, we warn on the constructor's name syntax instead of on each return point individually.
- If a member has an incompatible flow state at all return points in all applicable constructors, we warn on the member declaration itself.

## Consequences of this approach

```cs
public class C
{
    public string Prop { get; set; }
    public C()
    {
        Prop = null; // Warning: cannot assign null to 'Prop'
    } // Warning: Prop may be null when exiting 'C.C()'
    
    // This is consistent with currently shipped behavior:
    [MemberNotNull(nameof(Prop))]
    void M()
    {
        Prop = null; // Warning: cannot assign null to 'Prop'
    } // Warning: Prop may be null when exiting 'C.M()'
}
```

The above scenario produces multiple warnings corresponding to the same property. If there are more return points in the method, indefinitely many warnings could be produced depending on the number of return points in which a member has a bad flow state.

However, this is consistent with the behavior we have shipped for `[MemberNotNull]` and `[NotNull]` attributes: we warn when a bad value goes in, and we warn again when you return where the variable could contain a bad value.

---

```cs
public class C
{
    public string Prop { get; set; }
    public C()
    {
        Prop.ToString(); // Warning: dereference of a possible null reference.
    } // No warning: Prop's flow state was 'promoted' to NotNull after dereference
    
    // This is also consistent with currently shipped behavior:
    [MemberNotNull(nameof(Prop))]
    void M()
    {
        Prop.ToString(); // Warning: dereference of a possible null reference.
    } // No warning: Prop's flow state was 'promoted' to NotNull after dereference
}
```

In this scenario we never really initialize `Prop`, but we know that if we return normally from this constructor then Prop must have somehow gotten initialized. Thus this warning, while not ideal, does seem to be adequate for pointing the user toward where their problem lies.

Similarly, this is consistent with the shipped behavior of `[MemberNotNull]` and `[NotNull]`.

---

```cs
class C
{
    string Prop1 { get; set; }
    string Prop2 { get; set; }

    public C(bool a)
    {
        if (a)
        {
            Prop1 = "hello";
            return; // warning for Prop2
        }
        else
        {
            return; // warning for Prop1 and for Prop2
        }
    }
}
```

This scenario demonstrates the independence of warnings at each return point, as well as the way warnings can multiply for a single member within a single constructor. It feels like there might be methods of reducing redundancy of the warnings, but this refinement could come later and improve `[MemberNotNull]`/`[NotNull]` analysis at the same time. For constructors with complex conditional logic, it does seem to be an improvement to say "at this return, you haven't initialized something yet" versus simply saying "somewhere in here, you didn't initialize something".
