# Extensions 2023-06-07

## Reviewing the "invocation expression", "member access" and "extension invocation" sections

We reviewed the proposals in PR https://github.com/dotnet/csharplang/pull/7179 and discussed some open issues.  

### Invoking members from extended type

The first issue is that we want to access members from the extended type on a type or value of an extension type.

```csharp
extension E for C
{
  void M()
  {
    this.MethodFromUnderlying(); // problem
    MethodFromUnderlying(); // problem
  }
}
```

The current proposal had a bullet in "member access" 
which would make member access scenarios work for non-invocations, 
but that left invocations and simple name scenarios unsupported.

Decision: we want those scenarios to work and we can achieve that 
by modifying "member lookup" to also find members from the underlying type.
The "member lookup" section already handles lookups 

### Pattern-based method binding

The second issue is that the proposed rules raise a question about how pattern-based invocations,
such as the one involved in deconstruction.

For instance, should the following work?

```csharp
implicit extension E for C
{
   public dynamic Deconstruct { get { ... } } 
}

class C
{
   void M()
   {
     (int i, int j) = this;
   }
}
```

Decision: If the scenario allowed instance properties today, then extension properties should work, if not, then not.
So, given that an instance `Deconstruct` property would not participate in deconstruction today,
we don't want an extension `Deconstruct` property to participate either.

Any scenario that allowed extension methods should allow methods from extension types to participate.  
This means we'll need to review all pattern-based sections of the spec to come up with some language
to only bind to methods in the extension type case. Maybe something like "Do a member access and if that resolves to a method, then ..."
or "if the expression ... resolves at compile-time to a unique instance or extension method or extension type method, that expression is evaluated".  

Also, we'll need to make decisions for
non-invocation members, such as the `Current` property involved in a `foreach`, as those were not previously
covered by extension methods.

## Forward compatible emit strategy for interface support 

We briefly reviewed an alternative to the `ref struct` approach that is currently proposed.  
We will need to experiment and flesh out a proposal.
