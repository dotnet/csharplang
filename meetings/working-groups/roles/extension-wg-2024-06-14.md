# Extensions WG 2024-06-14

Agenda
- type erasure

TL;DR: we'll pursue the attribute with typeof-like string.

## Type erasure

We did a quick recap of various options we'd considered so far, and explored using an attribute
with a string serialization to represent the erased types.

1. modopts
2. generic attribute
3. attribute+typeof 
4. parallel method
5. typeref/blobs

### Attribute with typeof-like string

The main problem is how to represent type parameters, since they are currently disallowed in typeof inside attributes.  
We could extend the typeof format with existing convention "!N" (type parameters), "!!N" (method type parameters), stopping at first container (method or type) when resolving.  
We would use a `string` instead of a `Type`: `ExtensionAttribute(string encodedInformation)`.  
```csharp
// source
void M(C<E<int>>)

// metadata
void M([ExtensionAttribute("E<int>")] C<U>)
```

There are some concerns with re-hydration reliability, but those are not worse than what typeof in attributes already encounters.  
In terms of specification, we would just say we use the existing (not well-specified) format from typeof and just add the type parameter case.  
For reflection users, this string would be less useful than a `Type`. Maybe some helper APIs could be offered (somewhere outside the compiler-synthesized attribute and not in roslyn).  
This format feels verbose (compared to typerefs), but it seems the most feasible and comparatively simpler overall compared to other options.  

Encoding one attribute per typed entity (return type, parameter type, constraint type) fits with current codegen for tuple names, nullability, dynamic, etc. We will let the runtime team know in case this is a major concern.  

We need separate encoding for tuple names, nullability on non-erased type. This can be stored in the attribute itself.  
```csharp
class ExtensionAttribute
{
    ExtensionAttribute(string) { }
    Nullability { get; }
    TupleNames { get; }
    Dynamic { get; }
}
```

As for other such compiler-generated attributes, we'll disallow direct usage of this attribute in source.
