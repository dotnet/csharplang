ExperimentalAttribute
=====================
Report warnings for references to types and members marked with `System.Diagnostics.CodeAnalysis.ExperimentalAttribute`.
```
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Module |
                    AttributeTargets.Class |
                    AttributeTargets.Struct |
                    AttributeTargets.Enum |
                    AttributeTargets.Constructor |
                    AttributeTargets.Method |
                    AttributeTargets.Property |
                    AttributeTargets.Field |
                    AttributeTargets.Event |
                    AttributeTargets.Interface |
                    AttributeTargets.Delegate, Inherited = false)]
    public sealed class ExperimentalAttribute : Attribute
    {
        public ExperimentalAttribute(string diagnosticId)
        {
            DiagnosticId = diagnosticId;
        }

        public string DiagnosticId { get; }
        public string? UrlFormat { get; set; }
    }
}
```

## Warnings
The warning message is a specific message, where `'{0}'` is the fully-qualified type or member name.
```
'{0}' is for evaluation purposes only and is subject to change or removal in future updates.
```

The warning is reported for any reference to a type or member that is either:
- marked with the attribute,
- in an assembly or module marked with the attribute,
except when the reference occurs  within `[Experimental]` members (automatic suppression).

It is also possible to suppress the warning by usual means, such as an explicit compiler option or `#pragma`.

The attribute is not inherited from base types or overridden members.

## ObsoleteAttribute and DeprecatedAttribute

Warnings for `[Experimental]` are reported within `[Obsolete]` or `[Deprecated]` members.  
Warnings and errors for `[Obsolete]` and `[Deprecated]` are reported inside `[Experimental]` members.  
But warnings and errors for `[Obsolete]` and `[Deprecated]` are reported instead of `[Experimental]` if there are multiple attributes.  

## Open questions
- confirm the behavior we want for assembly/module-level attributes
- discuss warning vs. error
