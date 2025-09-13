The structure for docID for extension blocks is `E.GroupingName.MarkerName` and that for extension members is `E.GroupingName.Member`.  
But there is an issue when it comes to dealing with arity on the grouping name.

The convention for metadata names is to add an arity suffix to the type name. For `List<T>`, the name is "List" and the name in metadata is "List\`1".  
This allows for overloading on arity while avoiding name conflicts. You can have "List" (with arity zero) and "List\`1" (with arity one).  
For extension grouping and marker types, the name is unique enough that we use it directly as the metadata name, without adding an arity suffix.  
But this is causing some issues.

The way we produce docIDs for types is to take the name and append the arity suffix.  
For extensions, we added special handling to use the grouping and marker names. 

Let's consider this example:
```csharp
static class E
{
  extension<T>(T t)
  {
    public void M() { }
  }
}
```
with corresponding metadata:
```
.class E
{
  .class '<G>$8048A6C8BE30A622530249B904B537EB'<T0> // grouping type
  {  
    .class '<M>$65CB762EDFDF72BBC048551FDEA778ED'<T> // marker type
    {
      .. marker method ..
    }
    public void M() => throw; // extension member without implementation
  }

  public static void M(this int i) { } // implementation method
}
```

**If we do include an arity suffix** when producing docIDs for extensions, then the docIDs don't match the metadata names.

The docIDs for the example would differ from the names in metadata:
- extension block: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1.<M>$65CB762EDFDF72BBC048551FDEA778ED"
- extension member: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1.M"


**If we don't include an arity suffix**, then:
- docIDs produced from VB symbols on extension metadata will diverge from those produce from C# symbols. VB doesn't have the concept of extension, so will have regular handling for types (which include an arity suffix)
- if someone makes metadata for an extension type using some other tool, and they do include the arity suffix in the metadata names of grouping and marker types, then docIDs won't match metadata names again.

To illustrate the first bullet, the docIDs from C# source or metadata for the example would be:
- extension block: "E.<G>$8048A6C8BE30A622530249B904B537EB.<M>$65CB762EDFDF72BBC048551FDEA778ED"
- extension member: "E.<G>$8048A6C8BE30A622530249B904B537EB.M"

But the docIDs from VB metadata would differ from those from C#:
- extension grouping type: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1"
- extension marker type: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1.<M>$65CB762EDFDF72BBC048551FDEA778ED"
- extension member: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1.M"

To illustrate the second bullet, if some other tool cooks up extension metadata including arity suffixes like this:
```
.class E
{
  .class 'GroupingName`1' // grouping type
  {  
    .class 'MarkerName' // marker type
    {
      .. marker method ..
    }
    public void M() => throw; // extension member without implementation
  }

  public static void M(this int i) { } // implementation method
}
```

Then the docIDs would not match the metadata names:
- extension block: "E.GroupingName.MarkerName"
- extension member: "E.GroupingName.M"

# Proposal 

We're proposing to update the metadata design to include arity suffix in the metadata names for grouping and marker types.  
The metadata name for grouping type would be mangled from the `ExtensionGroupingName`.  
`ExtensionGroupingName` should reflect names of the emitted grouping typs from language perspective, not its emitted names. 
That would be more conventional.  
No change to `ExtensionMarkerName` (name and metadata names match).  
Then we'd produce the docIDs as described above, by appending an arity suffix to `ExtensionGroupingName` as needed.

For the above example, the compiler would produce:
```
.class E
{
  .class '<G>$8048A6C8BE30A622530249B904B537EB`1'<T0> // grouping type with arity suffix
  {  
    .class '<M>$65CB762EDFDF72BBC048551FDEA778ED'<T> // marker type
    {
      .. marker method ..
    }
    public void M() => throw; // extension member without implementation
  }

  public static void M(this int i) { } // implementation method
}
```
The `ExtensionGroupingName` would remain "<G>$8048A6C8BE30A622530249B904B537EB" (both for source and metadata symbols).
The `ExtensionMarkerName` would remain "<M>$65CB762EDFDF72BBC048551FDEA778ED" (both for source and metadata symbols).

Then the docIDs for C# symbols would be:
- extension block: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1.<M>$65CB762EDFDF72BBC048551FDEA778ED"
- extension member: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1.M"

And the docIDs for VB metadata symbols would be:
- extension grouping type: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1"
- extension marker type: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1.<M>$65CB762EDFDF72BBC048551FDEA778ED"
- extension member: "E.<G>$8048A6C8BE30A622530249B904B537EB\`1.M"

Everything aligns.

And if some other tool cooks up extension metadata without arity suffix like this:
```
.class E
{
  .class 'GroupingName'<T0> // grouping type (without arity suffix)
  {  
    .class 'MarkerName'<T> // marker
    {
      .. marker method ..
    }
    public void M() => throw; // extension member without implementation
  }

  public static void M(this int i) { } // implementation method
}
```
then the docIDs would be:
- extension block: "E.GroupingType\`1.MarkerType"
- extension member: "E.GroupingType\`1.M"

Those docIDs are not ideal (they differ from metadata names), but that's not a new problem.

# Practical considerations

If we're going to make this change, we have about 1 week to get this into the RC2 compiler.  
But the RC2 SDK/BCL will be compiled using the RC1 compiler, so the change would only be visible in the GA SDK/BCL assemblies.  
The new compiler will still be able to consume extensions produced by the RC1 compiler: when loading metadata symbols, the unmangling is optional (if there is no arity suffix in the metadata name, we just use the whole metadata name as the name).  
Given that only the compiler and docIDs make use of grouping and marker types, there would be no binary breaking change. Only implementation methods are referenced in IL, and those are unaffected by the change.  

# Alternative proposals

We also brainstormed a design where docIDs for extensions would be produced by taking `ExtensionGroupingName` and `ExtensionMarkerName`, would strip any arity suffix (to deal with metadata from another tool), then would proceed as usual (add an arity suffix.

# References
Some pointers to roslyn codebase for reference:

## Producing docIDs
All implementations use `Name` from the symbol (as opposed to `MetadataName`) and append a suffix for generics (either an arity suffix for docIds or type arguments for reference Ids on constructed symbols)

1. `ISymbol.GetDocumentationCommentId()` (implemented as part of C# and VB symbols)
This API also produces the docIDs emitted in xml output (see `DocumentationCommentCompiler.WriteDocumentationCommentXml` and `GenerateDocumentationComments`)
Uses `Name`, either appends an arity suffix (for definitions) or type arguments (for constructed symbols). (see `DocumentationCommentIDVisitor.PartVisitor.VisitNamedType`)

2. `DocumentationCommentId.CreateDeclarationId(ISymbol)` (implemented in core compiler layer)
Uses `Name`, appends an arity suffix (see `DocumentationCommentId.PrefixAndDeclarationGenerator.DeclarationGenerator.VisitNamedType`)

3. `DocumentationCommentId.CreateReferenceId()` (implemented in core compiler layer)
Uses Name (see `BuildDottedName`), either appends an arity suffix or type arguments (see `DocumentationCommentId.ReferenceGenerator`)

## Reading docIDs
1. `DocumentationCommentId.GetSymbolsForDeclarationId(string id, Compilation compilation)`
`DocumentationCommentId.Parser.ParseDeclaredId`  
Splits identifier into name and arity, then searches symbols with matching Name and Arity (exact).

2. CREF binding
`BindNameMemberCref`, `ComputeSortedCrefMembers`  
Takes a name followed by type argument list (which provides arity), looks up by name and arity (exact, for types), then constructs with type arguments


## Metadata names and metadata loading
The C# and VB compilers generally appends the arity suffix for generic types, to make the metadata name for a named type. There's some exceptions (notably EE named type symbols).
For extension grouping and marker types, we use the grouping and marker names as-is, without appending a suffix. The grouping and marker names are the metadata names.

When loading types from metadata, the C# and VB compilers remove the arity suffix when it matches the arity found in metadata (see `MetadataHelpers.UnmangleMetadataNameForArity`).

This means that we can load a type whose name doesn't include a suffix.

