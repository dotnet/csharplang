# Attribute target for Main

## Summary

C# should allow an attribute to target the main method / program entrypoint
without the need for it to be syntactically placed next to it.

## Motivation

Today, top level (simple) programs have no way to place attributes on the main
method, as there is no syntactic place for them to attach to. This means that
application models, such as Windows Forms, cannot take advantage of the
simplified entry point as they require additional attributes to be placed on
main (see
<https://github.com/dotnet/designs/blob/main/accepted/2021/winforms/streamline-application-bootstrap.md>).

Further if a source generator wishes to add an attribute to the main method, the
user of the generator must not use top level statements and explicitly make
their main method `partial`.

## Detailed design

A new global attribute target `main` will be recognized alongside `assembly` and
`module`, that allows an attribute to specify it should be attached to the
entrypoint of the program.

For example:

```csharp
[main: STAThread]
```

Can be placed in any syntax tree of the compilation, and the `[STAThread]`
attribute will be attached the entrypoint of the program.

Both top level statements and regular entrypoints are supported by the attribute
target. In the case of multiple entrypoints the attribute will only apply to the
first chosen location.

### Assemblies with no Entry Point

It is an error to use the `main` target location for an attribute in an assembly
with no entry point (e.g. a class library).

### Attribute locations

There is no new
[`AttributeTargets`](https://docs.microsoft.com/en-us/dotnet/api/system.attributetargets?view=net-6.0)
enum value defined as part of this proposal, and it is an error to prefix an
attribute with `[main:` that has any target other than `All` or `Method`.

The
[`AllowMultiple`](https://docs.microsoft.com/en-us/dotnet/api/system.attributeusageattribute.allowmultiple?view=net-6.0)
field of `AttributeUsage` is respected, and it is an error to prefix an
attribute with `[main:` multiple times unless `AllowMultiple` is set to true.
This applies across all syntax trees, only one prefixed attribute is allowed
regardless of location defined.

```csharp
public class SingleAttribute : Attribute { }

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public class MultiAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public class ClassOnlyAttribute : Attribute { }

/// file1.cs
[main: Single]
[main: Single] // error CS0579: Duplicate 'Single' attribute
[main: Multi]
[main: Multi]

/// file2.cs

[main: Single] // error CS0579: Duplicate 'Single' attribute
[main: Multi]
[main:ClassOnly] //error CS0592: Attribute 'ClassOnly' is not valid on this declaration type. It is only valid on 'class' declarations.
```

## Open Questions

### Targeting non simple entry points

There is some complexity in identifying exactly which method is the entrypoint
for regular (non-simple) entrypoints in the compiler. It currently happens
fairly late and we will need to bring it forward in order just to bind for this
to work.

However, if we don't do it, then it pushes that complexity onto the generator
author to identify which kind of entrypoint the user is using and change
generation strategies accordingly.

That might be considered a feature: source generators can't edit user code and
one could argue that adding attributes to a regular entrypoint without a
`partial` modifier is effectively editing it. The same argument then applies to
simple programs.

If we decide *not* to support source generation scenarios, then we could limit
the syntactic location to be required to be in the top level syntax tree itself.

A further alternative would be to loosen the restrictions on source generators
and allow them to apply attributes to *any* method, regardless of its partial
status. This would need some level of language design not considered here.

### Naming

This proposal is using `main` as straw man syntax for the real value. We should
decide exactly what to call it. Suggestions include `main` or `entrypoint`.

### Attribute Target

Should we introduce a corresponding `AttributeTargets.Main` that allows an
attribute to specify that it only applies to main methods? Currently we don't,
and users can apply any regular Method targeted attribute (or All) to the main
method.
