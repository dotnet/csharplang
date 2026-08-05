# Extensions 2023-02-21

## Problem with lookup rules on implicit extensions

The issue is that the proposed rules would allow a member from a base implicit extension
to be found by extension member lookups in two way: via import and via inheritance.  
This would result into having duplicates (same member appears twice in lookup) or conflicts
(hidden member conflicts with hiding member).  
Below are a few examples. 

### Various examples
```
// No definition in Derived
implicit extension Base for object 
{
    void M() { }
}
implicit extension Derived for object : Base
{
}

object o = ...;
o.M(); 
```

```
// Hiding in Derived
implicit extension Base for object 
{
    void M() { }
}
implicit extension Derived for object : Base
{
    new void M() { }
}

object o = ...;
o.M(); // ambiguous
```

```
// Better in Base
implicit extension Base for object 
{
    void M(int i) { }
}
implicit extension Derived for object : Base
{
    void M(double d) { }
}

object o = ...;
o.M(1); // Which does it pick?
```

Beyond those invocation scenarios, there are also non-invocation scenarios:

```
//  Non invocation
implicit extension Base for object 
{
    int Prop => 0;
}
implicit extension Derived for object : Base
{
}

object o = ...;
o.Prop; // find it twice, but it's only Base.Prop, so okay
```

```
// Non invocation, shadowing
implicit extension Base for object 
{
    int Prop => 0;
}
implicit extension Derived for object : Base
{
    new long Prop => 0; // warning so you put "new"
}

object o = ...;
o.Prop; // ambiguity (this is an argument for pruning)
derived.Prop; // would find Derived.Prop
```

### Options

We considered a few different ways of handling this:

First, we thought of relying on overload resolution to prefer more derived members. But this doesn't work for non-invocation scenarios.

Then we considered removing/pruning base extensions from the set of candidates during extension member lookup.  
This means a member from a base extension is only visible via inheritance, but not via import when it
is hidden.

But then we realized that existing lookup rules have to solve this already, as they are able to deal with 
interface members in multi-inheritance scenarios, or in scenario with a type parameter with two interface constraints.

Here's an example to illustrate the current behavior:
```
I2 i2 = default!;
var r2 = i2.Prop; // Finds I2.Prop, infers long

I4 i4 = default!;
var r4 = i4.Prop; // Finds I2.Prop, infers long

public interface I1 { public int Prop { get; } }
public interface I2 : I1 { new public long Prop { get; } }
public interface I3 : I1 { }
public interface I4 : I2, I3 { }
```

We tracked down the relevant rule from member lookup:  
"Next, members that are hidden by other members are removed from the set."

Conclusion: We'll incorporate a similar rule in extension member lookup.

## Unification of base extensions?

For interfaces, we check that directly implemented interfaces cannot unify.
For example:

```
interface I<T> { }
class C<U> : I<int>, I<U> // error
{
    void I<int>.M() { }
    void I<U>.M() { }
}
C<int>.M() // allowing such unifying scenario would cause a problem here (because we would have two V-tables to merge)
```

Do we similarly need to check for potential unification of base extensions?

The scenario would be:
```
explicit extension E<T> for object { }
explicit extension E2<U> for object : E<int>, E<U>
{ 
    void M() { }
}
```

We couldn't think of a problem with allowing this at the moment. Lookup on `E2<int>` would work.  
But if we ended up having some special support for extension types in the runtime, we could have a problem.  

Conclusion: Let's move ahead to unblock basic scenario and revisit

## Mixing implicit/explicit in a hierarchy

Do we need some check on implicit/explicit consistency in an extension hierarchy?

For example:
```
implicit extension Base for object { }
explicit extension Derived for object : Base { }
```

```
explicit extension Base for object { }
implicit extension Derived for object : Base { } // why? this may be scoped in a namespace
```

Conclusion: Some of the mixing scenarios seem questionable, but adding a restriction
would not give much benefit/protection. So we'll allow any mix of implicit/explicit-ness.
