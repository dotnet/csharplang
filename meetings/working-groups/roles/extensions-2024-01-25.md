# Extensions 2024-01-25

### Scope and priorities for C#13
static without inheritance -> instance -> inheritance support -> (out of scope: interfaces)

### Tracking open issues and status with LDM
We're using TODO/TODO2/TODO3 tags in the speclet to track and prioritize open issues.  
I'll add "TODO review with LDM" tags to mark sections that need reviewing. Those will be removed when reviewed.  
We'll use a PR format to bring speclet updates to review to LDM.  

### Adjusting lookup for members of underlying type
The current rules state that we look in the underlying type only when the lookup failed in the extension type.  
Those rules are inadequate for method group, invocation and indexer access scenarios as illustrated by example below.  
We instead want to treat the underlying type as a base type. It still comes into play last (meaning after all base extensions) but not conditionally.  
This allows for a method group with both methods from extension and underlying type.  

```
var a1 = EDerived.M2; // method group should incorporate members of EDerived, EBase and C
a1(42);

static class C
{
    public static void M2(int i) { }
}

static implicit extension EBase for C
{
    private static void M2(int i) { }
}

static implicit extension EDerived for C : EBase 
{ 
  void Method()
  {
    var a2 = M2;
  }
}
```

### Downlevel support
The new proposed lowering strategy (using `Unsafe.As`) doesn't benefit from using a `ref struct`.  
Using a `ref struct` prohibits any downlevel support.  
So we're going to change back to emitting extensions as regular `struct` types.  
 
### Ambiguous extensions
By analogy with extension methods, we'd like the scenario illustrated below to work.  
I'll follow up to propose a disambiguation rule to prefer the extension on more specific underlying type.  

```csharp
// See ExtensionInvocation_AmbiguityWithExtensionOnBaseType
System.Console.Write(new C().M(42)); // error CS0121: The call is ambiguous between the following methods or properties: 'E1.M(int)' and 'E2.M(int)'

class Base { }

class C : Base { }

implicit extension E1 for Base
{
    public int M(int i) => throw null;
}

implicit extension E2 for C
{
    public int M(int i) => i; // We'd like to prefer this extension member
}
```

### using static
We clarified this open issue.  
The question is what `using static NewExtensionType;` means.  
There is precedent because `using static ClassicExtensionType;` brings static methods into scope.  
We'll keep this issue open for later (not urgent).
