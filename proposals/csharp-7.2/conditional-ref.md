# Conditional ref expressions

The pattern of binding a ref variable to one or another expression conditionally is not currently expressible in C#.

The typical workaround is to introduce a method like:

```csharp
ref T Choice(bool condition, ref T consequence, ref T alternative)
{
    if (condition)
    {
         return ref consequence;
    }
    else
    {
         return ref alternative;
    }
}
```

Note that this is not an exact replacement of a ternary since all arguments must be evaluated at the call site.

The following will not work as expected:

```csharp
       // will crash with NRE because 'arr[0]' will be executed unconditionally
      ref var r = ref Choice(arr != null, ref arr[0], ref otherArr[0]);
```

The proposed syntax would look like:

```csharp
     <condition> ? ref <consequence> : ref <alternative>;
```

The above attempt with "Choice" can be _correctly_ written using ref ternary as:

```csharp
     ref var r = ref (arr != null ? ref arr[0]: ref otherArr[0]);
```

The difference from Choice is that consequence and alternative expressions are accessed in a _truly_ conditional manner, so we do not see a crash if ```arr == null```

The ternary ref is just a ternary where both alternative and consequence are refs. It will naturally require that consequence/alternative operands are LValues. 
It will also require that consequence and alternative have types that are identity convertible to each other.

The type of the expression will be computed similarly to the one for the regular ternary. I.E. in a case if consequence and alternative have identity convertible, but different types, the existing type-merging rules will apply.

Safe-to-return will be assumed conservatively from the conditional operands. If either is unsafe to return the whole thing is unsafe to return.

Ref ternary is an LValue and as such it can be passed/assigned/returned by reference;

```csharp
     // pass by reference
     foo(ref (arr != null ? ref arr[0]: ref otherArr[0]));

     // return by reference
     return ref (arr != null ? ref arr[0]: ref otherArr[0]);
```

Being an LValue, it can also be assigned to. 

```csharp
    // assign to
    (arr != null ? ref arr[0]: ref otherArr[0]) = 1;
```

Ref ternary can be used in a regular (not ref) context as well. Although it would not be common since you could as well just use a regular ternary.

```csharp
     int x = (arr != null ? ref arr[0]: ref otherArr[0]);
```


___

Implementation notes: 

The complexity of the implementation would seem to be the size of a moderate-to-large bug fix. - I.E not very expensive.
I do not think we need any changes to the syntax or parsing.
There is no effect on metadata or interop. The feature is completely expression based.
No effect on debugging/PDB either
