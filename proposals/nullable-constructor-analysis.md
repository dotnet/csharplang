# Nullable constructor analysis

This proposal is intended to resolve a few of the outstanding problems with nullable constructor analysis.

First is that the initial flow state of members is not accurate:

```cs
public class C
{
    public string Prop { get; set; }
    public C() // we get no uninitialized member warning, as expected
    {
        Prop.ToString(); // unexpectedly, there is no possible null receiver warning here
        Prop = "";
    }
}
```

Another is that assertions/'throw' statements do not prevent field initialization warnings:

```cs
public class C
{
    public string Prop { get; set; }
    public C() // warning: 'Prop' is not initialized
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

I think the best way to address this is to take an approach similar to `[MemberNotNull]` analysis, where fields are marked maybe-null and a warning is given if we ever exit the method when a field is still in a maybe-null state. We can do this by introducing the following rules:

**A constructor on a reference type without a `: this(...)` initializer** has an initial nullable flow state determined by:
- First initializing all applicable members to the state given by assigning a `default` literal to the member. A member is applicable if it is instance and the constructor being analyzed is instance, or if the member is static and the constructor being analyzed is static. 
  - We expect the `default` expression to yield a `NotNull` state for non-nullable value types, a `MaybeNull` state for reference types or nullable value types, and a `MaybeDefault` state for unconstrained generics.
- Then visiting the initializers for the applicable members, updating the flow state accordingly.
  - This allows some non-nullable reference members to be initialized using a field/property initializer, and others to be initialized within the constructor.
  - The expectation is that the compiler will flow-analyze and report diagnostics on the initializers once, then copy the resulting flow state as the initial state for each constructor which does not have a `: this(...)` initializer.

**A constructor on a value type with a `: this()` initializer** (referencing the default value type constructor) has an initial flow state given by initializing all applicable members to the state given by assigning a `default` literal to the member.

**A constructor on a value type or a constructor with a `: this(...)` initializer** has the same initial nullable flow state as an ordinary method. In the case of value types, we expect definite assignment analysis to provide the desired level of safety. This is the same as the existing behavior.

**At each explicit or implicit 'return' in a constructor**, we give a warning for each member whose flow state is incompatible with its annotations and nullability attributes. A reasonable proxy for this is: if assigning the member to itself at the return point would produce a nullability warning, then a nullability warning will be produced at the return point.

It's possible this could result in a lot of warnings for the same members in some scenarios. As a "stretch goal" I think we should consider the following "optimizations":
- If a member has an incompatible flow state at all return points in an applicable constructor, we warn on the constructor's name syntax instead of on each return point individually.
- If a member has an incompatible flow state at all return points in all applicable constructors, we warn on the member declaration itself.

---

A few notable consequences of this approach:

```cs
public class C
{
    public string Prop { get; set; }
    public C()
    {
        Prop = null; // Warning: cannot assign null to 'Prop'
    } // Warning: Prop may be null when exiting 'C.C()'
}
```

The above scenario produces multiple warnings corresponding to the same property. If there are more return points in the method, indefinitely many warnings could be produced depending on the number of return points in which a member has a bad flow state.

```cs
public class C
{
    public string Prop { get; set; }
    public C()
    {
        Prop.ToString(); // Warning: dereference of a possible null reference.
    } // No warning: Prop's flow state was 'promoted' to NotNull after dereference
}
```

In this scenario we never really initialize `Prop`, but we know that if we return normally from this constructor then Prop must have somehow gotten initialized. Thus this warning, while not ideal, does seem to be adequate for pointing the user toward where their problem lies.

```cs
public class C<T>
{
    public T Prop { get; set; }
    public C(T prop)
    {
        // While 'Prop' begins in a maybe-default state, 'prop' begins in a maybe-null state.
        Prop = prop;
    } // No warning: Prop's flow state satisfies its type.

    [MemberNotNull(nameof(Prop))]
    public void M1(T prop)
    {
        Prop = prop;
    } // warning: Prop needs to have a not-null state when exiting.

    [MemberNotNull(nameof(Prop))]
    public void M2([NotNull] T prop)
    {
        Prop = prop;
    } // no warning
}
```

This scenario highlights one of the more subtle differences between the `[MemberNotNull]` analysis and the proposed constructor analysis.

```cs
class C
{
    string Prop { get; set; }

    public C(bool a)
    {
        if (a)
            return; // warning 1
        else
            return; // warning 2
    }
}
```

This scenario demonstrates the way warnings can multiply for a single member within a single constructor. It feels like there might be methods of reducing redundancy of the warnings, but this refinement could come later and improve `[MemberNotNull]` analysis at the same time. For constructors with complex conditional logic, it does seem to be an improvement to say "at this return, you haven't initialized something yet" versus simply saying "somewhere in here, you didn't initialize something".
